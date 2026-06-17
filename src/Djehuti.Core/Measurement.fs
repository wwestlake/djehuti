namespace Djehuti.Core

type DistanceMetric =
    | JaccardDistance
    | CosineDistance
    | NormalizedEditDistance

type DistributionSummary =
    { Count: int
      Mean: float
      Variance: float }

type MarginalSummary =
    { ContextClassId: ContextClassId
      NaturalVelocity: DistributionSummary option
      PerturbationVelocity: DistributionSummary option
      EffectiveMass: DistributionSummary option
      Basis: MeasurementBasis
      Sources: MeasurementSource list }

type EnergyState =
    { TurnId: TurnId
      PotentialEnergy: MeasuredValue
      EffectiveMassExpectation: MeasuredValue
      Velocity: MeasuredValue
      TotalEnergy: MeasuredValue }

type DissipationCheck =
    | NonIncreasing
    | Violation of previous: EnergyState * current: EnergyState * delta: float
    | InsufficientEnergyStates

module Measurement =
    let private divide numerator denominator =
        if denominator = 0.0 then 0.0 else numerator / denominator

    let private summary values =
        match values with
        | [] ->
            { Count = 0
              Mean = 0.0
              Variance = 0.0 }
        | _ ->
            let count = values.Length
            let mean = values |> List.average

            let variance =
                values
                |> List.averageBy (fun value ->
                    let difference = value - mean
                    difference * difference)

            { Count = count
              Mean = mean
              Variance = variance }

    let distance metric leftText rightText =
        let leftParts = Decompose.parts leftText
        let rightParts = Decompose.parts rightText

        match metric with
        | JaccardDistance -> 1.0 - TextMetrics.jaccardSimilarity leftParts rightParts
        | CosineDistance -> 1.0 - TextMetrics.cosineSimilarity leftParts rightParts
        | NormalizedEditDistance -> 1.0 - TextMetrics.normalizedEditSimilarity leftText rightText

    let velocityFromTurnPair metric (previousTurn: Turn) (currentTurn: Turn) =
        let value = distance metric previousTurn.Response.Text currentTurn.Response.Text

        Domain.measured
            value
            DirectObservation
            [ FromTurn previousTurn.Id; FromTurn currentTurn.Id ]
            []

    let perturbationVelocity metric (prefix: ContextPrefix) (trial: ShockTrial) =
        let prefixText =
            prefix.PromptResponseHistory
            |> List.tryLast
            |> Option.map (fun (_, response) -> response.Text)
            |> Option.defaultValue ""

        let value = distance metric prefixText trial.Response.Text

        Domain.measured value MarginalEstimate [ FromShockTrial trial.Id ] [ ContinuityOfPerturbationVelocity ]

    let effectiveMassFromPerturbationVelocity (perturbationVelocity: MeasuredValue) =
        if perturbationVelocity.Value <= 0.0 then
            Domain.refused "Effective mass is undefined when perturbation velocity is zero or negative."
        else
            { perturbationVelocity with
                Value = 1.0 / perturbationVelocity.Value
                Assumptions =
                    perturbationVelocity.Assumptions
                    |> List.append [ ContinuityOfPerturbationVelocity ] }

    let checkWindowFeasibility (calibration: CalibrationRecord) =
        let missing =
            [ "lambda_min(epsilon)", calibration.LambdaMinEpsilon
              "lambda_max(delta)", calibration.LambdaMaxDelta
              "lambda_quantum", calibration.TokenGranularityLambdaQuantum ]
            |> List.choose (fun (name, value) ->
                match value with
                | Some _ -> None
                | None -> Some name)

        match missing with
        | _ :: _ -> MissingCalibration missing
        | [] ->
            let lambdaMin = calibration.LambdaMinEpsilon.Value.Value
            let lambdaMax = calibration.LambdaMaxDelta.Value.Value
            let lambdaQuantum = calibration.TokenGranularityLambdaQuantum.Value.Value
            let lowerBound = max lambdaMin lambdaQuantum

            if lowerBound <= lambdaMax then
                Feasible(lowerBound, lambdaMax)
            else
                Infeasible(
                    lowerBound,
                    lambdaMax,
                    "Local measurement is infeasible at this context; use global calibration fallback."
                )

    let marginalEstimate (batch: ForkedReplicationBatch) =
        let naturalVelocities =
            batch.Members
            |> List.choose (function
                | NaturalContinuation turn ->
                    match turn.Metadata.TryFind "velocity" with
                    | Some value ->
                        match System.Double.TryParse value with
                        | true, parsed -> Some parsed
                        | false, _ -> None
                    | None -> None
                | ShockContinuation _ -> None)

        let perturbationVelocities =
            batch.Members
            |> List.choose (function
                | ShockContinuation trial ->
                    trial.ResultingPerturbationVelocity
                    |> Option.map _.Value
                | NaturalContinuation _ -> None)

        let effectiveMassValues =
            perturbationVelocities
            |> List.choose (fun value ->
                if value <= 0.0 then None else Some(1.0 / value))

        { ContextClassId = batch.ContextClassId
          NaturalVelocity =
            if naturalVelocities.IsEmpty then None else Some(summary naturalVelocities)
          PerturbationVelocity =
            if perturbationVelocities.IsEmpty then None else Some(summary perturbationVelocities)
          EffectiveMass =
            if effectiveMassValues.IsEmpty then None else Some(summary effectiveMassValues)
          Basis = MarginalEstimate
          Sources = [ FromForkedReplicationBatch batch.Id ] }

    let computeEnergy turnId (potentialEnergy: MeasuredValue) (effectiveMassExpectation: MeasuredValue) (velocity: MeasuredValue) =
        let total =
            potentialEnergy.Value + (0.5 * effectiveMassExpectation.Value * velocity.Value * velocity.Value)

        let assumptions =
            [ potentialEnergy.Assumptions
              effectiveMassExpectation.Assumptions
              velocity.Assumptions ]
            |> List.concat
            |> List.distinct

        let basis =
            match effectiveMassExpectation.Basis with
            | DirectObservation ->
                HypothesisDependent [ CouplingHypothesis "Energy uses calibrated effective mass expectation, not a joint per-instance m_eff(t)." ]
            | basis -> basis

        { TurnId = turnId
          PotentialEnergy = potentialEnergy
          EffectiveMassExpectation = effectiveMassExpectation
          Velocity = velocity
          TotalEnergy =
            Domain.measured
                total
                basis
                (potentialEnergy.Sources @ effectiveMassExpectation.Sources @ velocity.Sources)
                assumptions }

    let checkDissipation states =
        match states with
        | []
        | [ _ ] -> InsufficientEnergyStates
        | first :: rest ->
            ((NonIncreasing, first), rest)
            ||> List.fold (fun (status, previous) current ->
                match status with
                | Violation _ -> status, current
                | InsufficientEnergyStates -> status, current
                | NonIncreasing ->
                    let delta = current.TotalEnergy.Value - previous.TotalEnergy.Value

                    if delta <= 0.0 then
                        NonIncreasing, current
                    else
                        Violation(previous, current, delta), current)
            |> fst
