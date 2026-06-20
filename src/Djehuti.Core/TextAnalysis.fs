namespace Djehuti.Core

open System
open System.Text.RegularExpressions

type TextParts =
    { Characters: int
      Words: string list
      Sentences: string list
      Lines: string list
      WordFrequencies: Map<string, int> }

type TextPartMetrics =
    { Characters: int
      WordCount: int
      UniqueWordCount: int
      SentenceCount: int
      LineCount: int
      LexicalDiversity: float
      AverageWordLength: float
      AverageSentenceLength: float }

type TextComparisonKind =
    | PromptResponseComparison
    | PromptPromptComparison
    | ResponseResponseComparison

type TextComparisonMetrics =
    { ComparisonId: string
      Kind: TextComparisonKind
      Left: TextPartMetrics
      Right: TextPartMetrics
      WordCountDelta: int
      CharacterCountDelta: int
      SharedWordCount: int
      JaccardSimilarity: float
      CosineSimilarity: float
      NormalizedEditSimilarity: float }

type TextCorpusMetrics =
    { ComparisonCount: int
      PromptResponseCount: int
      PromptPromptCount: int
      ResponseResponseCount: int
      AverageJaccardSimilarity: float
      AverageCosineSimilarity: float
      AverageEditSimilarity: float
      AverageWordCountDelta: float
      MetricsByComparison: TextComparisonMetrics list }

module Decompose =
    let private wordPattern = Regex("[\p{L}\p{N}']+", RegexOptions.Compiled)

    let private sentencePattern =
        Regex(@"[^.!?]+[.!?]*", RegexOptions.Compiled)

    let private normalizeWord (value: string) =
        value.Trim().ToLowerInvariant()

    let words (text: string) =
        if String.IsNullOrWhiteSpace text then
            []
        else
            wordPattern.Matches text
            |> Seq.cast<Match>
            |> Seq.map (fun m -> normalizeWord m.Value)
            |> Seq.toList

    let sentences (text: string) =
        if String.IsNullOrWhiteSpace text then
            []
        else
            sentencePattern.Matches text
            |> Seq.cast<Match>
            |> Seq.map (fun m -> m.Value.Trim())
            |> Seq.filter (String.IsNullOrWhiteSpace >> not)
            |> Seq.toList

    let lines (text: string) =
        if String.IsNullOrEmpty text then
            []
        else
            text.Split([| "\r\n"; "\n"; "\r" |], StringSplitOptions.None)
            |> Array.map (fun line -> line.Trim())
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
            |> Array.toList

    let parts (text: string) =
        let ws = words text

        { Characters = if isNull text then 0 else text.Length
          Words = ws
          Sentences = sentences text
          Lines = lines text
          WordFrequencies =
            ws
            |> Seq.countBy id
            |> Map.ofSeq }

module TextMetrics =
    let private divide numerator denominator =
        if denominator = 0.0 then
            0.0
        else
            numerator / denominator

    let private average values =
        match values with
        | [] -> 0.0
        | _ -> values |> List.average

    let partMetrics parts =
        let wordCount = parts.Words.Length
        let sentenceCount = parts.Sentences.Length

        let averageWordLength =
            parts.Words
            |> List.sumBy (fun word -> word.Length)
            |> float
            |> fun total -> divide total (float wordCount)

        { Characters = parts.Characters
          WordCount = wordCount
          UniqueWordCount = parts.WordFrequencies.Count
          SentenceCount = sentenceCount
          LineCount = parts.Lines.Length
          LexicalDiversity = divide (float parts.WordFrequencies.Count) (float wordCount)
          AverageWordLength = averageWordLength
          AverageSentenceLength = divide (float wordCount) (float sentenceCount) }

    let private wordSet parts =
        parts.Words |> Set.ofList

    let sharedWordCount leftParts rightParts =
        Set.intersect (wordSet leftParts) (wordSet rightParts)
        |> Set.count

    let jaccardSimilarity leftParts rightParts =
        let left = wordSet leftParts
        let right = wordSet rightParts
        let unionSize = Set.union left right |> Set.count
        let intersectionSize = Set.intersect left right |> Set.count

        divide (float intersectionSize) (float unionSize)

    let cosineSimilarity leftParts rightParts =
        let keys =
            Seq.append (leftParts.WordFrequencies |> Map.keys) (rightParts.WordFrequencies |> Map.keys)
            |> Seq.distinct
            |> Seq.toList

        let dot =
            keys
            |> List.sumBy (fun key ->
                let left = leftParts.WordFrequencies |> Map.tryFind key |> Option.defaultValue 0
                let right = rightParts.WordFrequencies |> Map.tryFind key |> Option.defaultValue 0
                left * right)
            |> float

        let magnitude frequencies =
            frequencies
            |> Map.values
            |> Seq.sumBy (fun count -> count * count)
            |> float
            |> sqrt

        divide dot ((magnitude leftParts.WordFrequencies) * (magnitude rightParts.WordFrequencies))

    let editDistance (left: string) (right: string) =
        let left = defaultArg (Option.ofObj left) String.Empty
        let right = defaultArg (Option.ofObj right) String.Empty
        let distances = Array2D.zeroCreate<int> (left.Length + 1) (right.Length + 1)

        for i in 0 .. left.Length do
            distances[i, 0] <- i

        for j in 0 .. right.Length do
            distances[0, j] <- j

        for i in 1 .. left.Length do
            for j in 1 .. right.Length do
                let substitutionCost =
                    if left[i - 1] = right[j - 1] then 0 else 1

                distances[i, j] <-
                    min
                        (min (distances[i - 1, j] + 1) (distances[i, j - 1] + 1))
                        (distances[i - 1, j - 1] + substitutionCost)

        distances[left.Length, right.Length]

    let normalizedEditSimilarity left right =
        let left = defaultArg (Option.ofObj left) String.Empty
        let right = defaultArg (Option.ofObj right) String.Empty
        let maxLength = max left.Length right.Length

        if maxLength = 0 then
            1.0
        else
            1.0 - divide (float (editDistance left right)) (float maxLength)

    let private textOfSample sample =
        match sample with
        | PromptSample prompt -> prompt.Text
        | ResponseSample response -> response.Text

    let private kindOfComparison comparison =
        match comparison with
        | PromptToResponse _ -> PromptResponseComparison
        | PromptToPrompt _ -> PromptPromptComparison
        | ResponseToResponse _ -> ResponseResponseComparison
        | StateTransition _ ->
            invalidArg (nameof comparison) "StateTransition comparisons require resolved turns."

    let private samplesOfComparison comparison =
        match comparison with
        | PromptToResponse (prompt, response) -> PromptSample prompt, ResponseSample response
        | PromptToPrompt (left, right) -> PromptSample left, PromptSample right
        | ResponseToResponse (left, right) -> ResponseSample left, ResponseSample right
        | StateTransition _ ->
            invalidArg (nameof comparison) "StateTransition comparisons require resolved turns."

    let comparisonMetrics comparisonId comparison =
        let leftSample, rightSample = samplesOfComparison comparison
        let leftText = textOfSample leftSample
        let rightText = textOfSample rightSample
        let leftParts = Decompose.parts leftText
        let rightParts = Decompose.parts rightText
        let leftMetrics = partMetrics leftParts
        let rightMetrics = partMetrics rightParts

        { ComparisonId = comparisonId
          Kind = kindOfComparison comparison
          Left = leftMetrics
          Right = rightMetrics
          WordCountDelta = rightMetrics.WordCount - leftMetrics.WordCount
          CharacterCountDelta = rightMetrics.Characters - leftMetrics.Characters
          SharedWordCount = sharedWordCount leftParts rightParts
          JaccardSimilarity = jaccardSimilarity leftParts rightParts
          CosineSimilarity = cosineSimilarity leftParts rightParts
          NormalizedEditSimilarity = normalizedEditSimilarity leftText rightText }

    let corpusMetrics comparisons =
        let metricsByComparison =
            comparisons
            |> List.mapi (fun index comparison -> comparisonMetrics $"comparison-{index + 1}" comparison)

        let countByKind kind =
            metricsByComparison
            |> List.filter (fun metrics -> metrics.Kind = kind)
            |> List.length

        { ComparisonCount = metricsByComparison.Length
          PromptResponseCount = countByKind PromptResponseComparison
          PromptPromptCount = countByKind PromptPromptComparison
          ResponseResponseCount = countByKind ResponseResponseComparison
          AverageJaccardSimilarity = metricsByComparison |> List.map _.JaccardSimilarity |> average
          AverageCosineSimilarity = metricsByComparison |> List.map _.CosineSimilarity |> average
          AverageEditSimilarity = metricsByComparison |> List.map _.NormalizedEditSimilarity |> average
          AverageWordCountDelta = metricsByComparison |> List.map (fun m -> float m.WordCountDelta) |> average
          MetricsByComparison = metricsByComparison }
