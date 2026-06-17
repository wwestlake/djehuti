namespace Djehuti.Core

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Threading
open System.Threading.Tasks

type JsonValue =
    | JsonNull
    | JsonString of string
    | JsonNumber of float
    | JsonBoolean of bool
    | JsonArray of JsonValue list
    | JsonObject of Map<string, JsonValue>

type JsonInteractionDataSet =
    { Source: DataSourceDescriptor
      Constants: Map<string, JsonValue>
      Interactions: InteractionRecord list }

module JsonInterop =
    let private options =
        JsonSerializerOptions(WriteIndented = true)

    let private requireProperty (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value -> value
        | false, _ -> invalidArg name $"JSON property '{name}' is required."

    let private optionalProperty (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value -> Some value
        | false, _ -> None

    let private stringProperty (name: string) (element: JsonElement) =
        (requireProperty name element).GetString()

    let private optionalStringProperty (name: string) (element: JsonElement) =
        optionalProperty name element
        |> Option.bind (fun value ->
            if value.ValueKind = JsonValueKind.Null then None else Some(value.GetString()))

    let private intProperty (name: string) (element: JsonElement) =
        (requireProperty name element).GetInt32()

    let private optionalIntProperty (name: string) (element: JsonElement) =
        optionalProperty name element
        |> Option.map _.GetInt32()

    let private parseDate (value: string) =
        match DateTimeOffset.TryParse(value) with
        | true, parsed -> Some parsed
        | false, _ -> invalidArg "observedAt" $"Invalid DateTimeOffset value '{value}'."

    let private metadataFromElement (element: JsonElement option) =
        match element with
        | None -> Map.empty
        | Some value when value.ValueKind = JsonValueKind.Null -> Map.empty
        | Some value ->
            value.EnumerateObject()
            |> Seq.map (fun property -> property.Name, property.Value.ToString())
            |> Map.ofSeq

    let rec private jsonValueFromElement (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Null
        | JsonValueKind.Undefined -> JsonNull
        | JsonValueKind.String -> JsonString(element.GetString())
        | JsonValueKind.Number -> JsonNumber(element.GetDouble())
        | JsonValueKind.True -> JsonBoolean true
        | JsonValueKind.False -> JsonBoolean false
        | JsonValueKind.Array ->
            element.EnumerateArray()
            |> Seq.map jsonValueFromElement
            |> Seq.toList
            |> JsonArray
        | JsonValueKind.Object ->
            element.EnumerateObject()
            |> Seq.map (fun property -> property.Name, jsonValueFromElement property.Value)
            |> Map.ofSeq
            |> JsonObject
        | _ -> JsonNull

    let private constantsFromElement (element: JsonElement) =
        match optionalProperty "constants" element with
        | None -> Map.empty
        | Some value when value.ValueKind = JsonValueKind.Null -> Map.empty
        | Some value when value.ValueKind = JsonValueKind.Object ->
            value.EnumerateObject()
            |> Seq.map (fun property -> property.Name, jsonValueFromElement property.Value)
            |> Map.ofSeq
        | Some _ -> invalidArg "constants" "JSON property 'constants' must be an object when present."

    let private kindFromString (value: string) =
        match value with
        | "live-provider" -> LiveProvider
        | "replay-file" -> ReplayFile
        | "benchmark-harness" -> BenchmarkHarness
        | "manual-transcript" -> ManualTranscript
        | "message-queue" -> MessageQueue
        | other -> UnknownSource other

    let private kindToString kind =
        match kind with
        | LiveProvider -> "live-provider"
        | ReplayFile -> "replay-file"
        | BenchmarkHarness -> "benchmark-harness"
        | ManualTranscript -> "manual-transcript"
        | MessageQueue -> "message-queue"
        | UnknownSource value -> value

    let private strategyFromString (value: string) =
        match value with
        | "natural" -> Natural
        | "shock" -> Shock
        | "interleaved-with-history" -> InterleavedWithHistory
        | other -> invalidArg "strategy" $"Unknown sampling strategy '{other}'."

    let private strategyToString strategy =
        match strategy with
        | Natural -> "natural"
        | Shock -> "shock"
        | InterleavedWithHistory -> "interleaved-with-history"

    let private parseSource (element: JsonElement) =
        { Id = DataSourceId(stringProperty "id" element)
          Kind = kindFromString (stringProperty "kind" element)
          Name = stringProperty "name" element
          Metadata = metadataFromElement (optionalProperty "metadata" element) }

    let private parseInteraction (source: DataSourceDescriptor) (element: JsonElement) =
        let sequenceIndex = intProperty "sequenceIndex" element
        let turnId = optionalStringProperty "turnId" element |> Option.defaultValue $"turn-{sequenceIndex}"
        let promptId = optionalStringProperty "promptId" element |> Option.defaultValue $"prompt-{sequenceIndex}"
        let responseId = optionalStringProperty "responseId" element |> Option.defaultValue $"response-{sequenceIndex}"
        let observedAt = optionalStringProperty "observedAt" element |> Option.bind parseDate

        { TurnId = TurnId turnId
          SessionId = SessionId(stringProperty "sessionId" element)
          ModelId = ModelId(stringProperty "modelId" element)
          Clock =
            { SequenceIndex = LogicalTime sequenceIndex
              ObservedAt = observedAt }
          Prompt = Domain.promptWithMetadata promptId (stringProperty "prompt" element) Map.empty
          Response = Domain.responseWithMetadata responseId (stringProperty "response" element) Map.empty
          Strategy =
            optionalStringProperty "strategy" element
            |> Option.defaultValue "natural"
            |> strategyFromString
          PriorShockCount = optionalIntProperty "priorShockCount" element |> Option.defaultValue 0
          Source = source
          Metadata = metadataFromElement (optionalProperty "metadata" element) }

    let readDataSetFromString (json: string) =
        use document = JsonDocument.Parse(json)
        let root = document.RootElement
        let source = parseSource (requireProperty "source" root)

        let interactions =
            requireProperty "interactions" root
            |> fun value ->
                if value.ValueKind <> JsonValueKind.Array then
                    invalidArg "interactions" "JSON property 'interactions' must be an array."

                value.EnumerateArray()
                |> Seq.map (parseInteraction source)
                |> Seq.toList

        { Source = source
          Constants = constantsFromElement root
          Interactions = interactions }

    let readDataSetFromFile (path: string) =
        File.ReadAllText path
        |> readDataSetFromString

    let eventsFromDataSet (dataSet: JsonInteractionDataSet) =
        dataSet.Interactions
        |> List.map InteractionObserved

    let eventsFromString (json: string) =
        readDataSetFromString json
        |> eventsFromDataSet

    let private writeMetadata (writer: Utf8JsonWriter) (metadata: Map<string, string>) =
        writer.WriteStartObject()

        metadata
        |> Map.iter (fun key value -> writer.WriteString(key, (value: string)))

        writer.WriteEndObject()

    let rec private writeJsonValue (writer: Utf8JsonWriter) value =
        match value with
        | JsonNull -> writer.WriteNullValue()
        | JsonString value -> writer.WriteStringValue value
        | JsonNumber value -> writer.WriteNumberValue value
        | JsonBoolean value -> writer.WriteBooleanValue value
        | JsonArray values ->
            writer.WriteStartArray()
            values |> List.iter (writeJsonValue writer)
            writer.WriteEndArray()
        | JsonObject values ->
            writer.WriteStartObject()

            values
            |> Map.iter (fun key value ->
                writer.WritePropertyName key
                writeJsonValue writer value)

            writer.WriteEndObject()

    let private writeSource (writer: Utf8JsonWriter) (source: DataSourceDescriptor) =
        writer.WriteStartObject()
        writer.WriteString("id", let (DataSourceId value) = source.Id in value)
        writer.WriteString("kind", kindToString source.Kind)
        writer.WriteString("name", source.Name)
        writer.WritePropertyName "metadata"
        writeMetadata writer source.Metadata
        writer.WriteEndObject()

    let private writeInteraction (writer: Utf8JsonWriter) (record: InteractionRecord) =
        let (TurnId turnId) = record.TurnId
        let (SessionId sessionId) = record.SessionId
        let (ModelId modelId) = record.ModelId
        let (PromptId promptId) = record.Prompt.Id
        let (ResponseId responseId) = record.Response.Id
        let (LogicalTime sequenceIndex) = record.Clock.SequenceIndex

        writer.WriteStartObject()
        writer.WriteString("turnId", turnId)
        writer.WriteString("sessionId", sessionId)
        writer.WriteString("modelId", modelId)
        writer.WriteNumber("sequenceIndex", sequenceIndex)

        match record.Clock.ObservedAt with
        | Some observedAt -> writer.WriteString("observedAt", observedAt)
        | None -> ()

        writer.WriteString("promptId", promptId)
        writer.WriteString("prompt", record.Prompt.Text)
        writer.WriteString("responseId", responseId)
        writer.WriteString("response", record.Response.Text)
        writer.WriteString("strategy", strategyToString record.Strategy)
        writer.WriteNumber("priorShockCount", record.PriorShockCount)
        writer.WritePropertyName "metadata"
        writeMetadata writer record.Metadata
        writer.WriteEndObject()

    let writeDataSetToString (dataSet: JsonInteractionDataSet) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = options.WriteIndented))

        writer.WriteStartObject()
        writer.WritePropertyName "source"
        writeSource writer dataSet.Source
        writer.WritePropertyName "constants"
        writer.WriteStartObject()

        dataSet.Constants
        |> Map.iter (fun key value ->
            writer.WritePropertyName key
            writeJsonValue writer value)

        writer.WriteEndObject()
        writer.WritePropertyName "interactions"
        writer.WriteStartArray()
        dataSet.Interactions |> List.iter (writeInteraction writer)
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

        Text.Encoding.UTF8.GetString(stream.ToArray())

    let writeDataSetToFile (path: string) dataSet =
        File.WriteAllText(path, writeDataSetToString dataSet)

type JsonDataSource(dataSet: JsonInteractionDataSet) =
    interface IDataSource with
        member _.Descriptor = dataSet.Source

        member _.Read cancellationToken =
            let events = JsonInterop.eventsFromDataSet dataSet

            { new IAsyncEnumerable<IngestionEvent> with
                member _.GetAsyncEnumerator(token: CancellationToken) =
                    let token =
                        if token = CancellationToken.None then cancellationToken else token

                    let enumerator = (events :> seq<IngestionEvent>).GetEnumerator()

                    { new IAsyncEnumerator<IngestionEvent> with
                        member _.Current = enumerator.Current

                        member _.MoveNextAsync() =
                            if token.IsCancellationRequested then
                                ValueTask<bool>(Task.FromCanceled<bool>(token))
                            else
                                ValueTask<bool>(enumerator.MoveNext())

                        member _.DisposeAsync() =
                            enumerator.Dispose()
                            ValueTask() } }

    new(json: string) = JsonDataSource(JsonInterop.readDataSetFromString json)

    static member FromFile path =
        JsonDataSource(JsonInterop.readDataSetFromFile path)
