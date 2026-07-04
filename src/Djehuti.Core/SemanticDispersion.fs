namespace Djehuti.Core

open System

type SemanticDispersionObservation =
    { Token: string
      ChunkCount: int
      DocumentCount: int
      SourceTypeCount: int
      NeighborCount: int }

type SemanticTokenDispersion =
    { Token: string
      ChunkCount: int
      DocumentCount: int
      SourceTypeCount: int
      NeighborCount: int
      DispersionScore: float
      DispersionBand: string }

module SemanticDispersion =
    let private log1p value = Math.Log(1.0 + value)

    let scoreObservation (observation: SemanticDispersionObservation) =
        let chunkTerm = log1p (float observation.ChunkCount) * 0.2
        let documentTerm = log1p (float observation.DocumentCount) * 0.45
        let sourceTerm = float observation.SourceTypeCount * 0.9
        let neighborTerm = log1p (float observation.NeighborCount) * 0.5
        chunkTerm + documentTerm + sourceTerm + neighborTerm

    let classify score =
        if score >= 4.5 then "high"
        elif score >= 3.0 then "medium"
        else "low"

    let evaluate (observation: SemanticDispersionObservation) =
        let score = scoreObservation observation

        { Token = observation.Token
          ChunkCount = observation.ChunkCount
          DocumentCount = observation.DocumentCount
          SourceTypeCount = observation.SourceTypeCount
          NeighborCount = observation.NeighborCount
          DispersionScore = Math.Round(score, 3)
          DispersionBand = classify score }
