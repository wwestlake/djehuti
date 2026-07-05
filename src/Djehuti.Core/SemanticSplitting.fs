namespace Djehuti.Core

type SemanticTokenSplit =
    { Token: string
      ScopeKind: string
      ScopeValue: string
      VariantKey: string }

module SemanticSplitting =
    let buildSourceTypeVariantKey (token: string) (sourceType: string) =
        $"{token}::source::{sourceType}"

    let resolveTokenForContext (context: Map<string, string>) (splits: SemanticTokenSplit list) (token: string) =
        splits
        |> List.tryFind (fun split ->
            split.Token = token
            && match context.TryFind split.ScopeKind with
               | Some value -> value = split.ScopeValue
               | None -> false)
        |> Option.map _.VariantKey
        |> Option.defaultValue token

    let expandQueryToken (sourceType: string option) (splits: SemanticTokenSplit list) (token: string) =
        let matching =
            splits
            |> List.filter (fun split ->
                split.Token = token
                && match sourceType with
                   | Some value -> split.ScopeKind = "source-type" && split.ScopeValue = value
                   | None -> true)
            |> List.map _.VariantKey

        token :: matching
        |> List.distinct
