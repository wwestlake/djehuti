namespace Djehuti.Core

type SemanticSourceRecord =
    { SourceType: string
      SourceKey: string
      Title: string
      Text: string
      MetadataJson: string option
      Provenance: Map<string, string> }

type SemanticChunkRecord =
    { SourceType: string
      SourceKey: string
      Title: string
      ChunkPosition: int
      ChunkText: string
      Tokens: string list
      MetadataJson: string option
      Provenance: Map<string, string> }

module SemanticRecords =
    let buildChunkRecords (maxChars: int) (source: SemanticSourceRecord) =
        SemanticPreprocessing.buildChunks maxChars source.Text
        |> List.map (fun chunk ->
            { SourceType = source.SourceType
              SourceKey = source.SourceKey
              Title = source.Title
              ChunkPosition = chunk.Position
              ChunkText = chunk.Text
              Tokens = chunk.Tokens
              MetadataJson = source.MetadataJson
              Provenance = source.Provenance })
