namespace Djehuti.Api

open System
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Djehuti.Core

type OpenAiResponsesOptions =
    { ApiKey: string
      Model: string
      Endpoint: Uri }

module OpenAiResponses =
    let defaultEndpoint =
        Uri("https://api.openai.com/v1/responses")

    let private environmentValue name =
        let processValue = Environment.GetEnvironmentVariable(name)

        if String.IsNullOrWhiteSpace processValue then
            Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
        else
            processValue

    let tryOptionsFromEnvironment () =
        let apiKey = environmentValue "OPENAI_API_KEY"

        if String.IsNullOrWhiteSpace apiKey then
            Error(AiConnectionUnavailable "OPENAI_API_KEY is not configured.")
        else
            let model =
                let value = environmentValue "DJEHUTI_ANALYST_MODEL"
                if String.IsNullOrWhiteSpace value then "gpt-4.1" else value

            Ok
                { ApiKey = apiKey
                  Model = model
                  Endpoint = defaultEndpoint }

    let private roleLabel role =
        match role with
        | System -> "system"
        | User -> "user"
        | Assistant -> "assistant"
        | Tool -> "tool"

    let private messageText messages =
        messages
        |> List.map (fun message -> $"{roleLabel message.Role}: {message.Content}")
        |> String.concat "\n\n"

    let systemInstructions messages =
        messages
        |> List.filter (fun message -> message.Role = System)
        |> List.map _.Content
        |> String.concat "\n\n"

    let nonSystemInput messages =
        messages
        |> List.filter (fun message -> message.Role <> System)
        |> function
            | [] -> messageText messages
            | values -> messageText values

    let private tryStringProperty (name: string) (element: JsonElement) =
        match element.TryGetProperty name with
        | true, value when value.ValueKind = JsonValueKind.String -> Some(value.GetString())
        | _ -> None

    let private compactProviderError (body: string) =
        if String.IsNullOrWhiteSpace body then
            "Provider returned an empty error response."
        else
            try
                use document = JsonDocument.Parse(body)
                let root = document.RootElement

                let errorElement =
                    match root.TryGetProperty "error" with
                    | true, value when value.ValueKind = JsonValueKind.Object -> value
                    | _ -> root

                let message =
                    tryStringProperty "message" errorElement
                    |> Option.defaultValue body

                let code =
                    tryStringProperty "code" errorElement

                match code with
                | Some value when not (String.IsNullOrWhiteSpace value) ->
                    $"OpenAI error {value}: {message}"
                | _ -> message
            with
            | :? JsonException -> body

    let mapStatusError status body =
        let message = compactProviderError body

        match status with
        | HttpStatusCode.Unauthorized
        | HttpStatusCode.Forbidden -> AiAuthenticationFailed message
        | enumStatus when int enumStatus = 429 -> AiRateLimited message
        | HttpStatusCode.BadRequest -> AiProviderRejectedRequest message
        | _ -> AiProviderFailure message

    let extractText (json: string) =
        use document = JsonDocument.Parse(json)
        let root = document.RootElement

        match tryStringProperty "output_text" root with
        | Some text -> Ok text
        | None ->
            match root.TryGetProperty "output" with
            | true, output when output.ValueKind = JsonValueKind.Array ->
                let texts =
                    output.EnumerateArray()
                    |> Seq.collect (fun item ->
                        match item.TryGetProperty "content" with
                        | true, content when content.ValueKind = JsonValueKind.Array ->
                            content.EnumerateArray()
                            |> Seq.choose (fun contentItem -> tryStringProperty "text" contentItem)
                        | _ -> Seq.empty)
                    |> Seq.toList

                match texts with
                | [] -> Error(AiResponseInvalid "OpenAI response did not contain output text.")
                | values -> Ok(String.concat "\n" values)
            | _ -> Error(AiResponseInvalid "OpenAI response did not contain an output array.")

type OpenAiResponsesConnection(httpClient: HttpClient, options: OpenAiResponsesOptions) =
    let serializerOptions =
        JsonSerializerOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)

    let requestBody (request: AiRequest) =
        let model =
            request.Model
            |> Option.map (fun (ModelId value) -> value)
            |> Option.defaultValue options.Model

        let temperature =
            request.Temperature
            |> Option.map Nullable
            |> Option.defaultValue (Nullable())

        let maxOutputTokens =
            request.MaxOutputTokens
            |> Option.map Nullable
            |> Option.defaultValue (Nullable())

        {| model = model
           instructions =
            let instructions = OpenAiResponses.systemInstructions request.Messages
            if String.IsNullOrWhiteSpace instructions then null else instructions
           input = OpenAiResponses.nonSystemInput request.Messages
           temperature = temperature
           max_output_tokens = maxOutputTokens
           store = false
           metadata = request.Metadata |}

    interface IAiConnection with
        member _.Submit request =
            async {
                try
                    use httpRequest = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
                    httpRequest.Headers.Authorization <- AuthenticationHeaderValue("Bearer", options.ApiKey)

                    let json = JsonSerializer.Serialize(requestBody request, serializerOptions)
                    httpRequest.Content <- new StringContent(json, Encoding.UTF8, "application/json")

                    let! response =
                        httpClient.SendAsync(httpRequest)
                        |> Async.AwaitTask

                    let! body =
                        response.Content.ReadAsStringAsync()
                        |> Async.AwaitTask

                    if not response.IsSuccessStatusCode then
                        return Error(OpenAiResponses.mapStatusError response.StatusCode body)
                    else
                        return
                            OpenAiResponses.extractText body
                            |> Result.map (fun text ->
                                { ConnectionId = request.ConnectionId
                                  Model = request.Model |> Option.orElse (Some(ModelId options.Model))
                                  Content = text
                                  Metadata =
                                    Map.ofList
                                        [ "provider", "openai"
                                          "endpoint", string options.Endpoint ] })
                with
                | :? HttpRequestException as ex -> return Error(AiConnectionUnavailable ex.Message)
                | :? TaskCanceledException as ex -> return Error(AiConnectionUnavailable ex.Message)
                | :? JsonException as ex -> return Error(AiResponseInvalid ex.Message)
            }
