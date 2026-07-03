module Djehuti.Api.SemanticEmbeddings

open System
open System.IO
open System.Linq
open System.Net.Http
open Microsoft.ML.OnnxRuntime
open Microsoft.ML.OnnxRuntime.Tensors
open Tokenizers.HuggingFace.Tokenizer
open Djehuti.Core

type EmbeddingProviderKind =
    | OnnxLocal
    | HashFallback

type EmbeddingProviderInfo =
    { Kind: EmbeddingProviderKind
      Name: string
      Dimension: int
      IsReady: bool
      Detail: string option }

let [<Literal>] EmbeddingDimension = 384

let private modelName = "all-MiniLM-L6-v2"
let private modelUrlDefault = "https://huggingface.co/onnx-models/all-MiniLM-L6-v2-onnx/resolve/main/model.onnx"
let private tokenizerUrlDefault = "https://huggingface.co/Qdrant/all-MiniLM-L6-v2-onnx/resolve/main/tokenizer.json"
let private cacheDirDefault = Path.Combine(Path.GetTempPath(), "djehuti-semantic-model-cache")

let private env name fallback =
    Environment.GetEnvironmentVariable(name)
    |> Option.ofObj
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue fallback

let private modelPath () =
    match Environment.GetEnvironmentVariable("SEMANTIC_EMBEDDING_MODEL_PATH") with
    | null
    | "" ->
        Path.Combine(env "SEMANTIC_EMBEDDING_CACHE_DIR" cacheDirDefault, "model.onnx")
    | value -> value

let private tokenizerPath () =
    match Environment.GetEnvironmentVariable("SEMANTIC_EMBEDDING_TOKENIZER_PATH") with
    | null
    | "" ->
        Path.Combine(env "SEMANTIC_EMBEDDING_CACHE_DIR" cacheDirDefault, "tokenizer.json")
    | value -> value

let private ensureFile (path: string) (url: string) =
    let file = FileInfo(path)
    if not file.Directory.Exists then
        file.Directory.Create()
    if not file.Exists then
        use client = new HttpClient()
        client.Timeout <- TimeSpan.FromMinutes(3.0)
        let bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult()
        File.WriteAllBytes(path, bytes)

let private hashEmbedding (text: string) =
    let vector = Array.zeroCreate<float32> EmbeddingDimension
    let tokens = SemanticPreprocessing.tokenize text
    if List.isEmpty tokens then
        vector
    else
        for token in tokens do
            let bucket = Math.Abs(token.GetHashCode()) % EmbeddingDimension
            vector[bucket] <- vector[bucket] + 1.0f

        let norm =
            vector
            |> Array.sumBy (fun value -> float (value * value))
            |> sqrt

        if norm > 0.0 then
            let divisor = float32 norm
            for index = 0 to vector.Length - 1 do
                vector[index] <- vector[index] / divisor

        vector

let private normalize (values: float32 array) =
    let norm =
        values
        |> Array.sumBy (fun value -> float (value * value))
        |> sqrt

    if norm <= 0.0 then
        values
    else
        let divisor = float32 norm
        values |> Array.map (fun value -> value / divisor)

let private meanPool (sequenceLength: int) (attentionMask: int64 array) (values: float32 array) =
    if sequenceLength <= 0 then
        Array.zeroCreate<float32> EmbeddingDimension
    elif values.Length = EmbeddingDimension then
        normalize values
    else
        let result = Array.zeroCreate<float32> EmbeddingDimension
        let mutable count = 0.0f

        for tokenIndex = 0 to sequenceLength - 1 do
            let includeToken =
                if tokenIndex < attentionMask.Length then attentionMask[tokenIndex] <> 0L else true

            if includeToken then
                let offset = tokenIndex * EmbeddingDimension
                if offset + EmbeddingDimension <= values.Length then
                    for dimensionIndex = 0 to EmbeddingDimension - 1 do
                        result[dimensionIndex] <- result[dimensionIndex] + values[offset + dimensionIndex]
                    count <- count + 1.0f

        if count > 0.0f then
            for dimensionIndex = 0 to EmbeddingDimension - 1 do
                result[dimensionIndex] <- result[dimensionIndex] / count

        normalize result

type private OnnxModel =
    { Tokenizer: Tokenizer
      Session: InferenceSession
      Info: EmbeddingProviderInfo }
    interface IDisposable with
        member this.Dispose() =
            this.Session.Dispose()

let private tryLoadOnnxModel () : Result<OnnxModel, EmbeddingProviderInfo> =
    try
        let modelPathValue = modelPath ()
        let tokenizerPathValue = tokenizerPath ()
        ensureFile modelPathValue (env "SEMANTIC_EMBEDDING_MODEL_URL" modelUrlDefault)
        ensureFile tokenizerPathValue (env "SEMANTIC_EMBEDDING_TOKENIZER_URL" tokenizerUrlDefault)

        let tokenizer = Tokenizer.FromFile(tokenizerPathValue)
        let session = new InferenceSession(modelPathValue)
        Result.Ok
            { Tokenizer = tokenizer
              Session = session
              Info =
                { Kind = OnnxLocal
                  Name = modelName
                  Dimension = EmbeddingDimension
                  IsReady = true
                  Detail = Some modelPathValue } }
    with ex ->
        Result.Error(
            { Kind = HashFallback
              Name = "hash-fallback"
              Dimension = EmbeddingDimension
              IsReady = true
              Detail = Some ex.Message })

let private onnxState =
    lazy
        match tryLoadOnnxModel () with
        | Ok model -> Choice1Of2 model
        | Error info -> Choice2Of2 info

let private encodeWithOnnx (model: OnnxModel) (text: string) =
    let encoding: Encoding =
        model.Tokenizer.Encode(text, true, includeTypeIds = true, includeAttentionMask = true)
        |> Seq.head

    let sequenceLength = encoding.Ids.Count
    let inputIds = DenseTensor<int64>(encoding.Ids |> Seq.map int64 |> Seq.toArray, [| 1; sequenceLength |])
    let tokenTypeIds = DenseTensor<int64>(encoding.TypeIds |> Seq.map int64 |> Seq.toArray, [| 1; sequenceLength |])
    let attentionMaskValues = encoding.AttentionMask |> Seq.map int64 |> Seq.toArray
    let attentionMask = DenseTensor<int64>(attentionMaskValues, [| 1; sequenceLength |])

    use results =
        model.Session.Run(
            [ NamedOnnxValue.CreateFromTensor("input_ids", inputIds)
              NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
              NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask) ])

    let output = results.First().AsEnumerable<float32>().ToArray()
    meanPool sequenceLength attentionMaskValues output

let embed (text: string) =
    match onnxState.Value with
    | Choice1Of2 model ->
        encodeWithOnnx model text,
        model.Info
    | Choice2Of2 fallbackInfo ->
        hashEmbedding text,
        fallbackInfo

let getProviderInfo () =
    match onnxState.Value with
    | Choice1Of2 model -> model.Info
    | Choice2Of2 info -> info

let cosineSimilarity (left: float32 array) (right: float32 array) =
    if isNull left || isNull right || left.Length = 0 || right.Length = 0 || left.Length <> right.Length then
        0.0
    else
        let mutable dot = 0.0
        let mutable leftNorm = 0.0
        let mutable rightNorm = 0.0

        for index = 0 to left.Length - 1 do
            let l = float left[index]
            let r = float right[index]
            dot <- dot + (l * r)
            leftNorm <- leftNorm + (l * l)
            rightNorm <- rightNorm + (r * r)

        let divisor = sqrt leftNorm * sqrt rightNorm
        if divisor <= 0.0 then 0.0 else dot / divisor
