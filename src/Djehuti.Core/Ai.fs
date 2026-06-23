namespace Djehuti.Core

open System
open System.Text

type AiConnectionId = AiConnectionId of string
type AiConversationId = AiConversationId of string

type AiMessageRole =
    | System
    | User
    | Assistant
    | Tool

type AiMessage =
    { Role: AiMessageRole
      Content: string
      Metadata: Map<string, string> }

type AiRequest =
    { ConnectionId: AiConnectionId
      ConversationId: AiConversationId option
      Model: ModelId option
      Messages: AiMessage list
      Temperature: float option
      MaxOutputTokens: int option
      Metadata: Map<string, string> }

type AiResponse =
    { ConnectionId: AiConnectionId
      Model: ModelId option
      Content: string
      Metadata: Map<string, string> }

type AiConnectionError =
    | AiConnectionUnavailable of message: string
    | AiAuthenticationFailed of message: string
    | AiRateLimited of message: string
    | AiProviderRejectedRequest of message: string
    | AiProviderFailure of message: string
    | AiResponseInvalid of message: string

type IAiConnection =
    abstract Submit: AiRequest -> Async<Result<AiResponse, AiConnectionError>>

type AiConnectionDescriptor =
    { Id: AiConnectionId
      Name: string
      Provider: string
      DefaultModel: ModelId option
      Capabilities: string list
      Metadata: Map<string, string> }

type DjehutiAnalystEvidence =
    { Label: string
      Value: string
      Source: MeasurementSource option }

type DjehutiAnalysisContext =
    { Turns: Turn list
      ObservableVectors: ObservableVector list
      Reports: MeasurementReport list
      AttractorEvents: AttractorEvent list
      Constants: Map<string, string>
      Warnings: string list }

type DjehutiAnalystInitialization =
    { Name: string
      Version: string
      BehaviorInstructions: string list
      CompactFormalism: string list
      TheoryRationale: string list
      AnswerDiscipline: string list }

type DjehutiAnalystRequest =
    { Question: string
      Context: DjehutiAnalysisContext
      ConversationId: AiConversationId option
      Model: ModelId option
      Temperature: float option
      MaxOutputTokens: int option }

type DjehutiAnalystResult =
    { Answer: string
      Evidence: DjehutiAnalystEvidence list
      AiResponse: AiResponse }

type IDjehutiAnalyst =
    abstract Ask: DjehutiAnalystRequest -> Async<Result<DjehutiAnalystResult, AiConnectionError>>

module Ai =
    let message role content =
        { Role = role
          Content = if isNull content then String.Empty else content
          Metadata = Map.empty }

    let system content = message System content
    let user content = message User content
    let assistant content = message Assistant content
    let tool content = message Tool content

    let private unwrapTurnId (TurnId value) = value
    let private unwrapModelId (ModelId value) = value

    let private basisText basis =
        match basis with
        | DirectObservation -> "direct observation"
        | MarginalEstimate -> "marginal estimate"
        | GlobalCalibrationEstimate -> "global calibration estimate"
        | LocalCalibrationEstimate(ContextClassId id) -> $"local calibration estimate for {id}"
        | HypothesisDependent _ -> "hypothesis dependent"
        | ContaminatedTrajectory depth -> $"contaminated trajectory depth {depth}"
        | Refused reason -> $"refused: {reason}"

    let private assumptionText assumption =
        match assumption with
        | ContinuityOfPerturbationVelocity -> "continuity of perturbation velocity"
        | IsotropicEmbeddingCurvatureApproximation -> "isotropic embedding curvature approximation"
        | CouplingHypothesis name -> $"coupling hypothesis: {name}"
        | ProviderSuppliedMetadata name -> $"provider supplied metadata: {name}"
        | GlobalCalibrationFallback -> "global calibration fallback"
        | LocalCalibrationAssumption(ContextClassId id) -> $"local calibration assumption: {id}"
        | TokenGranularityAggregation statistic -> $"token granularity aggregation: {statistic}"

    let private sourceText source =
        match source with
        | FromTurn turnId -> $"turn:{unwrapTurnId turnId}"
        | FromShockTrial(ShockTrialId id) -> $"shock:{id}"
        | FromForkedReplicationBatch(ForkedReplicationBatchId id) -> $"forked-batch:{id}"
        | FromCalibrationRecord(CalibrationRecordId id) -> $"calibration:{id}"
        | FromTextComparison comparisonId -> $"comparison:{comparisonId}"

    let defaultAnalystInitialization =
        { Name = "Djehuti Cyberscope AI+ embedded analyst"
          Version = "1"
          BehaviorInstructions =
            [ "You are the embedded Djehuti analyst AI."
              "Use a strict analytic style: concise, explicit, evidence-first, and careful about uncertainty."
              "Use only externally observable data supplied by the application: prompts, responses, metadata, calibration records, measurement reports, observable vectors, warnings, and attractor events."
              "Do not claim access to model weights, activations, attention maps, hidden sampling state, or provider-private runtime details."
              "Separate observation, estimate, diagnostic hypothesis, and interpretation."
              "When a user asks to be shown, walked through, or guided through something, generate a data-driven expert tour by including a fenced JSON code block tagged 'tour' in your response, alongside your prose answer."
              "Tours must read like an ISD expert guiding a student through their actual measurement run. Every tooltip must: (1) cite the real value or event from the supplied data, (2) explain what it means within Information Space Dynamics theory — not just the number, but its theoretical interpretation. Use the ISD formalism: velocity as semantic displacement rate, alpha as embedding alignment, stability margin (alpha minus v) as proximity to attractor boundary, torsional accumulation as trajectory rigidity, attractor events as boundary-crossing signatures, and so on."
              "For example: instead of 'this is the feature finder panel', write 'Your run logged 4 high-severity attractor approaches. In ISD, these occur when the stability margin alpha-v drops below threshold while torsional accumulation is elevated — the model trajectory is approaching a fixed point, resisting perturbation. Turn 47 is the sharpest: stability margin fell to 0.04, meaning the response almost exactly tracked the perturbation velocity, a signature of deep attractor lock-in.'"
              "Another example for timelines: instead of 'this chart shows velocity', write 'Velocity (v) here measures how far the model moved through embedding space in one turn. Your run shows a spike to 0.89 at turn 23 — unusually high. In ISD that means the response at turn 23 was a dramatic semantic departure from turn 22: either a topic jump, a refusal boundary, or a sudden context discontinuity. Everything before and after is below 0.3, so turn 23 is a genuine singularity in this trajectory.'"
              "Be specific, be theoretical, be expert. The user is learning what ISD measurements mean by seeing them in their own data."
              "The tour block contains an array of steps, each with: target (CSS selector or data-tour attribute), title (short heading), text (the expert ISD narration for this specific data), and side (optional: top|bottom|left|right)."
              "CRITICAL: Only use the exact targets listed here. Do not invent selectors, controls, or panels not in this list. If no matching target exists for a step, skip it."
              "Exact available targets: [data-tour=\"side-menu\"] = left nav sidebar; [data-tour=\"topbar\"] = top bar with dataset controls; [data-tour=\"dataset-picker\"] = dataset selector dropdown; [data-tour=\"analyze-button\"] = runs the analysis; [data-tour=\"tools-panel\"] = right panel with analyst AI; [data-tour=\"analyst-form\"] = ask the analyst a question; [data-tour=\"live-compose\"] = Live Lab compose form; [data-tour=\"live-save-form\"] = save a Live Lab run; [data-tour=\"live-transcript\"] = Live Lab conversation. Section anchors: #features = Feature Finder; #timelines = Metric Timelines; #phase-space = 3D Phase Space."
              "Only include a tour block when the user explicitly asks to be shown, walked through, or guided. For pure analytical questions, answer in prose only." ]
          CompactFormalism =
            [ "Logical time is the integer turn index t = 0, 1, 2, ..."
              "A turn is an externally observed prompt-response pair."
              "Observable vector components include alpha=prompt-response cosine, beta=Jaccard similarity, gamma=normalized edit similarity, delta=word-count delta, v=response-transition velocity, kappa=curvature diagnostic, tau=torsional resistance, and zeta4=four-component diagnostic norm."
              "Delta Psi compares shared observable components across states or runs."
              "Identity 1 diagnostic: stability margin alpha - v."
              "Identity 2 diagnostic: cumulative leakage sum(abs(delta))."
              "Identity 3 diagnostic: torsional accumulation sum(abs(tau)), with kappa fallback when tau is unavailable."
              "Attractor-approach events are diagnostic signatures produced when stability margin is below a configured threshold and torsional accumulation is above a configured threshold."
              "Forked replication reports natural and shock marginals separately; it must not synthesize a fabricated per-instance joint measurement." ]
          TheoryRationale =
            [ "Information Space Dynamics treats a conversation as an externally observed trajectory through a measurement space."
              "The framework is intentionally pure-observability constrained: hidden model internals are outside the instrument unless surfaced as external metadata."
              "Local measurements are allowed only when calibration conditions such as the Window Inequality support them; otherwise the measurement should be refused or treated as a global/marginal estimate."
              "Formal identities and attractor signatures are diagnostic scaffolds unless empirically calibrated; describe them as hypotheses or diagnostics when that is their basis."
              "Measurement basis, provenance, assumptions, contamination, and refusal status are part of the scientific result, not decoration." ]
          AnswerDiscipline =
            [ "Cite supplied evidence labels when useful."
              "Call out refused and hypothesis-dependent values explicitly."
              "Prefer metric-grounded explanations over narrative speculation."
              "When the data is insufficient, say what is missing and what measurement would reduce uncertainty."
              "Do not turn a dashboard pattern into a claim about model cognition unless the supplied measurement basis supports that claim." ] }

    let private section title lines =
        seq {
            yield title
            for line in lines do
                yield $"- {line}"
        }

    let frameworkGroundingFromInitialization initialization =
        String.concat
            "\n"
            [ yield $"Analyst profile: {initialization.Name} v{initialization.Version}"
              yield! section "Behavior instructions:" initialization.BehaviorInstructions
              yield! section "Compact Djehuti/ISD formalism:" initialization.CompactFormalism
              yield! section "Theory rationale:" initialization.TheoryRationale
              yield! section "Answer discipline:" initialization.AnswerDiscipline ]

    let frameworkGrounding =
        frameworkGroundingFromInitialization defaultAnalystInitialization

    let private measuredValueLine name (value: MeasuredValue option) =
        match value with
        | None -> None
        | Some measurement ->
            let assumptions =
                match measurement.Assumptions with
                | [] -> ""
                | flags -> flags |> List.map assumptionText |> String.concat "; " |> sprintf "; assumptions=%s"

            Some(sprintf "%s=%.3f (%s%s)" name measurement.Value (basisText measurement.Basis) assumptions)

    let private vectorLine index (vector: ObservableVector) =
        [ measuredValueLine "alpha" vector.Alpha
          measuredValueLine "beta" vector.Beta
          measuredValueLine "gamma" vector.Gamma
          measuredValueLine "delta" vector.Delta
          measuredValueLine "v" vector.Velocity
          measuredValueLine "kappa" vector.Curvature
          measuredValueLine "tau" vector.TorsionalResistance
          measuredValueLine "zeta4" vector.Zeta4 ]
        |> List.choose id
        |> function
            | [] -> $"observable[{index}] turn={unwrapTurnId vector.TurnId}: no usable components"
            | values ->
                let joinedValues = String.concat ", " values
                $"observable[{index}] turn={unwrapTurnId vector.TurnId}: {joinedValues}"

    let private reportLine (report: MeasurementReport) =
        let itemText =
            report.Items
            |> List.map (fun item ->
                let value =
                    item.Value
                    |> Option.map (sprintf "%.3f")
                    |> Option.defaultValue "n/a"

                let basis = basisText item.Basis
                $"{item.Name}={value} ({basis})")
            |> String.concat "; "

        $"report {report.Subject}: {itemText}"

    let private attractorLine (event: AttractorEvent) =
        let resistance =
            event.TorsionalResistance
            |> Option.map (fun value -> sprintf "%.3f (%s)" value.Value (basisText value.Basis))
            |> Option.defaultValue "n/a"

        $"attractor turn={unwrapTurnId event.TurnId}: {event.Description}; tau={resistance}"

    let private turnLine (turn: Turn) =
        let prompt =
            if turn.Prompt.Text.Length > 140 then turn.Prompt.Text.Substring(0, 137) + "..." else turn.Prompt.Text

        let response =
            if turn.Response.Text.Length > 140 then turn.Response.Text.Substring(0, 137) + "..." else turn.Response.Text

        $"turn {turn.SequenceIndex} ({unwrapTurnId turn.Id}): prompt=\"{prompt}\" response=\"{response}\""

    let evidenceFromContext (context: DjehutiAnalysisContext) =
        [ yield!
            context.Turns
            |> List.truncate 50
            |> List.map (fun turn ->
                { Label = $"turn {turn.SequenceIndex}"
                  Value = turnLine turn
                  Source = Some(FromTurn turn.Id) })

          yield!
            context.ObservableVectors
            |> List.truncate 50
            |> List.mapi (fun index vector ->
                { Label = $"observable {index}"
                  Value = vectorLine index vector
                  Source = Some(FromTurn vector.TurnId) })

          yield!
            context.Reports
            |> List.truncate 20
            |> List.map (fun report ->
                { Label = report.Subject
                  Value = reportLine report
                  Source = None })

          yield!
            context.AttractorEvents
            |> List.truncate 20
            |> List.map (fun event ->
                { Label = $"attractor {unwrapTurnId event.TurnId}"
                  Value = attractorLine event
                  Source = Some(FromTurn event.TurnId) })

          yield!
            context.Warnings
            |> List.truncate 20
            |> List.map (fun warning ->
                { Label = "warning"
                  Value = warning
                  Source = None }) ]

    let contextSummary (context: DjehutiAnalysisContext) =
        let builder = StringBuilder()
        builder.AppendLine("Djehuti analysis context:") |> ignore
        builder.AppendLine($"turns={context.Turns.Length}") |> ignore
        builder.AppendLine($"observableVectors={context.ObservableVectors.Length}") |> ignore
        builder.AppendLine($"reports={context.Reports.Length}") |> ignore
        builder.AppendLine($"attractorEvents={context.AttractorEvents.Length}") |> ignore

        if not context.Constants.IsEmpty then
            builder.AppendLine("constants:") |> ignore

            context.Constants
            |> Map.toList
            |> List.truncate 16
            |> List.iter (fun (key, value) -> builder.AppendLine($"- {key}: {value}") |> ignore)

        let evidence = evidenceFromContext context
        if not evidence.IsEmpty then
            builder.AppendLine("evidence:") |> ignore

            evidence
            |> List.iter (fun item -> builder.AppendLine($"- {item.Label}: {item.Value}") |> ignore)

        builder.ToString().Trim()

    let buildAnalystMessagesWithInitialization initialization question context =
        [ system (frameworkGroundingFromInitialization initialization)
          user (
              String.concat
                  "\n\n"
                  [ "Question:"
                    if String.IsNullOrWhiteSpace question then "(no question supplied)" else question.Trim()
                    "Available app data:"
                    contextSummary context
                    "Answer as a Djehuti analyst. Cite the supplied evidence labels when useful and call out refused or hypothesis-dependent measurements." ]) ]

    let buildAnalystMessages question context =
        buildAnalystMessagesWithInitialization defaultAnalystInitialization question context

    type DjehutiAnalyst(connection: IAiConnection, connectionId: AiConnectionId, initialization: DjehutiAnalystInitialization) =
        new(connection: IAiConnection, connectionId: AiConnectionId) =
            DjehutiAnalyst(connection, connectionId, defaultAnalystInitialization)

        interface IDjehutiAnalyst with
            member _.Ask request =
                async {
                    let aiRequest =
                        { ConnectionId = connectionId
                          ConversationId = request.ConversationId
                          Model = request.Model
                          Messages = buildAnalystMessagesWithInitialization initialization request.Question request.Context
                          Temperature = request.Temperature
                          MaxOutputTokens = request.MaxOutputTokens
                          Metadata =
                            Map.ofList
                                [ "djehuti.role", "embedded-analyst"
                                  "djehuti.framework", "Information Space Dynamics"
                                  "djehuti.analyst_profile", initialization.Name
                                  "djehuti.analyst_profile_version", initialization.Version
                                  "djehuti.turn_count", string request.Context.Turns.Length
                                  "djehuti.attractor_event_count", string request.Context.AttractorEvents.Length ] }

                    let! response = connection.Submit aiRequest

                    return
                        response
                        |> Result.map (fun aiResponse ->
                            { Answer = aiResponse.Content
                              Evidence = evidenceFromContext request.Context
                              AiResponse = aiResponse })
                }
