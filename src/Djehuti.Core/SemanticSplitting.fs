namespace Djehuti.Core

type SemanticTokenSplit =
    { Token: string
      SourceType: string
      VariantKey: string }

module SemanticSplitting =
    let buildSourceTypeVariantKey (token: string) (sourceType: string) =
        $"{token}::source::{sourceType}"

    let resolveTokenForSource (sourceType: string) (splits: SemanticTokenSplit list) (token: string) =
        splits
        |> List.tryFind (fun split -> split.Token = token && split.SourceType = sourceType)
        |> Option.map _.VariantKey
        |> Option.defaultValue token

    let expandQueryToken (sourceType: string option) (splits: SemanticTokenSplit list) (token: string) =
        let matching =
            splits
            |> List.filter (fun split ->
                split.Token = token
                && match sourceType with
                   | Some value -> split.SourceType = value
                   | None -> true)
            |> List.map _.VariantKey

        token :: matching
        |> List.distinct
