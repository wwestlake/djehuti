namespace Djehuti.Core

open System
open System.Text
open System.Text.RegularExpressions

type SemanticChunk<'T> =
    { Position: int
      Text: string
      Tokens: string list
      Payload: 'T }

module SemanticPreprocessing =
    let private stopWords =
        set
            [ "a"; "an"; "and"; "are"; "as"; "at"; "be"; "but"; "by"; "for"; "from"; "if"; "in"; "into"; "is"
              "it"; "its"; "of"; "on"; "or"; "that"; "the"; "their"; "then"; "there"; "these"; "this"; "to"; "was"
              "were"; "will"; "with"; "you"; "your" ]

    let normalizeToken (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            let normalized =
                value.Trim().ToLowerInvariant()
                |> Seq.filter Char.IsLetterOrDigit
                |> Array.ofSeq
                |> String

            if String.IsNullOrWhiteSpace normalized || normalized.Length < 2 || Set.contains normalized stopWords then
                None
            else
                Some normalized

    let tokenize (text: string) =
        if String.IsNullOrWhiteSpace text then
            []
        else
            Regex.Split(text, @"[^A-Za-z0-9]+")
            |> Array.toList
            |> List.choose normalizeToken

    let chunkText (maxChars: int) (text: string) =
        if String.IsNullOrWhiteSpace text then
            []
        else
            let paragraphs =
                text.Replace("\r\n", "\n").Split([| "\n\n" |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun part -> part.Trim())
                |> Array.filter (String.IsNullOrWhiteSpace >> not)

            let flush (builder: StringBuilder) (chunks: ResizeArray<string>) =
                if builder.Length > 0 then
                    chunks.Add(builder.ToString().Trim())
                    builder.Clear() |> ignore

            let chunks = ResizeArray<string>()
            let builder = StringBuilder()

            for paragraph in paragraphs do
                if paragraph.Length > maxChars then
                    flush builder chunks

                    let mutable offset = 0
                    while offset < paragraph.Length do
                        let take = min maxChars (paragraph.Length - offset)
                        chunks.Add(paragraph.Substring(offset, take).Trim())
                        offset <- offset + take
                else
                    let needed =
                        if builder.Length = 0 then paragraph.Length
                        else builder.Length + 2 + paragraph.Length

                    if needed > maxChars then
                        flush builder chunks

                    if builder.Length > 0 then
                        builder.AppendLine().AppendLine() |> ignore

                    builder.Append(paragraph) |> ignore

            flush builder chunks
            chunks |> Seq.toList

    let buildChunks (maxChars: int) (text: string) =
        chunkText maxChars text
        |> List.mapi (fun index chunkText ->
            { Position = index
              Text = chunkText
              Tokens = tokenize chunkText
              Payload = chunkText })

    let scoreChunk (queryTokens: string list) (chunkTokens: string list) =
        if List.isEmpty queryTokens || List.isEmpty chunkTokens then
            0
        else
            let querySet = Set.ofList queryTokens
            let matches = chunkTokens |> List.filter (fun token -> Set.contains token querySet)
            let distinctMatches = matches |> Set.ofList |> Set.count
            let frequencyWeight = matches.Length
            (distinctMatches * 100) + frequencyWeight

    let topChunksForQuery (query: string) (maxCount: int) (chunks: SemanticChunk<'T> list) =
        let queryTokens = tokenize query

        chunks
        |> List.map (fun chunk -> chunk, scoreChunk queryTokens chunk.Tokens)
        |> List.filter (fun (_, score) -> score > 0)
        |> List.sortBy (fun (chunk, score) -> -score, chunk.Position)
        |> List.truncate maxCount
        |> List.map fst

    let selectEvidence (question: string) (maxCount: int) (evidence: DjehutiAnalystEvidence list) =
        let evidenceChunks =
            evidence
            |> List.mapi (fun index item ->
                let text = $"{item.Label}: {item.Value}"
                { Position = index
                  Text = text
                  Tokens = tokenize text
                  Payload = item })

        let selected = topChunksForQuery question maxCount evidenceChunks |> List.map _.Payload

        if List.isEmpty selected then
            evidence |> List.truncate maxCount
        else
            selected
