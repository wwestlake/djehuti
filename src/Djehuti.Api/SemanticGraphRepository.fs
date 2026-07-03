module Djehuti.Api.SemanticGraphRepository

open System
open System.Data.Common
open System.Security.Cryptography
open System.Text
open Djehuti.Core
open Npgsql
open Database

type SemanticGraphStats =
    { DocumentCount: int
      ChunkCount: int
      TokenCount: int
      EmbeddedChunkCount: int
      EmbeddingProvider: string
      EmbeddingReady: bool }

type SemanticChunkHit =
    { SourceType: string
      SourceKey: string
      Title: string
      ChunkPosition: int
      Content: string
      MatchedTokenCount: int
      MatchedWeight: int
      Similarity: float }

type SemanticReindexSummary =
    { DocumentsRequested: int
      DocumentsIndexed: int
      ForumThreadsIndexed: int
      BlogArticlesIndexed: int
      MudRoomsIndexed: int }

let private sha256 (text: string) =
    use hasher = SHA256.Create()
    let bytes = Encoding.UTF8.GetBytes(text)
    let hash = hasher.ComputeHash(bytes)
    Convert.ToHexString(hash).ToLowerInvariant()

let private opt (value: string option) : obj =
    value |> Option.map box |> Option.defaultValue (box DBNull.Value)

let private buildForumThreadText (thread: ForumRepository.ForumThread) (posts: ForumRepository.ForumPost list) =
    let builder = StringBuilder()
    builder.AppendLine(thread.Title) |> ignore
    builder.AppendLine() |> ignore
    for post in posts do
        builder.AppendLine($"{post.AuthorName}:") |> ignore
        builder.AppendLine(post.Content) |> ignore
        builder.AppendLine() |> ignore
    builder.ToString().Trim()

let private buildBlogArticleText (article: BlogRepository.BlogArticle) =
    let builder = StringBuilder()
    builder.AppendLine(article.Title) |> ignore
    article.Subtitle |> Option.iter (fun value -> builder.AppendLine(value) |> ignore)
    article.Excerpt |> Option.iter (fun value -> builder.AppendLine().AppendLine(value) |> ignore)
    builder.AppendLine().AppendLine(article.Content) |> ignore
    builder.ToString().Trim()

let private buildMudRoomText
    (zoneName: string)
    (roomName: string)
    (description: string option)
    (exits: (string * string * string option) list)
    (items: (string * string option * string option) list) =
    let builder = StringBuilder()
    builder.AppendLine(roomName) |> ignore
    builder.AppendLine($"Zone: {zoneName}") |> ignore
    builder.AppendLine() |> ignore
    builder.AppendLine(description |> Option.defaultValue "The room has no description yet.") |> ignore

    if not exits.IsEmpty then
        builder.AppendLine().AppendLine("Exits:") |> ignore
        exits
        |> List.iter (fun (direction, targetRoomName, label) ->
            match label with
            | Some value when not (String.IsNullOrWhiteSpace value) ->
                builder.AppendLine($"- {direction}: {targetRoomName} ({value})") |> ignore
            | _ ->
                builder.AppendLine($"- {direction}: {targetRoomName}") |> ignore)

    if not items.IsEmpty then
        builder.AppendLine().AppendLine("Visible items:") |> ignore
        items
        |> List.iter (fun (name, itemDescription, readableText) ->
            builder.AppendLine($"- {name}") |> ignore
            itemDescription |> Option.iter (fun value -> builder.AppendLine($"  {value}") |> ignore)
            readableText |> Option.iter (fun value -> builder.AppendLine($"  Readable text: {value}") |> ignore))

    builder.ToString().Trim()

let private replaceDocumentChunks (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (documentId: Guid) (text: string) =
    use deleteCmd = new NpgsqlCommand("DELETE FROM semantic_chunks WHERE document_id = @documentId", conn, txn)
    deleteCmd.Parameters.AddWithValue("documentId", documentId) |> ignore
    deleteCmd.ExecuteNonQuery() |> ignore

    let chunks = SemanticPreprocessing.buildChunks 700 text

    for chunk in chunks do
        let embedding, provider = SemanticEmbeddings.embed chunk.Text
        use chunkCmd = new NpgsqlCommand(
            """INSERT INTO semantic_chunks (document_id, chunk_position, content, token_count, embedding_values, embedding_provider, embedding_dimension, embedded_at)
               VALUES (@documentId, @position, @content, @tokenCount, @embeddingValues, @embeddingProvider, @embeddingDimension, now())
               RETURNING id""",
            conn,
            txn)
        chunkCmd.Parameters.AddWithValue("documentId", documentId) |> ignore
        chunkCmd.Parameters.AddWithValue("position", chunk.Position) |> ignore
        chunkCmd.Parameters.AddWithValue("content", chunk.Text) |> ignore
        chunkCmd.Parameters.AddWithValue("tokenCount", chunk.Tokens.Length) |> ignore
        chunkCmd.Parameters.AddWithValue("embeddingValues", embedding) |> ignore
        chunkCmd.Parameters.AddWithValue("embeddingProvider", provider.Name) |> ignore
        chunkCmd.Parameters.AddWithValue("embeddingDimension", provider.Dimension) |> ignore
        let chunkId = chunkCmd.ExecuteScalar() :?> Guid

        chunk.Tokens
        |> List.countBy id
        |> List.sortBy fst
        |> List.iteri (fun index (token, count) ->
            use tokenCmd = new NpgsqlCommand(
                """INSERT INTO semantic_chunk_tokens (chunk_id, token, token_count, position)
                   VALUES (@chunkId, @token, @tokenCount, @position)""",
                conn,
                txn)
            tokenCmd.Parameters.AddWithValue("chunkId", chunkId) |> ignore
            tokenCmd.Parameters.AddWithValue("token", token) |> ignore
            tokenCmd.Parameters.AddWithValue("tokenCount", count) |> ignore
            tokenCmd.Parameters.AddWithValue("position", index) |> ignore
            tokenCmd.ExecuteNonQuery() |> ignore)

let indexTextDocument (sourceType: string) (sourceKey: string) (title: string) (text: string) (metadataJson: string option) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    try
        let contentHash = sha256 text

        use selectCmd = new NpgsqlCommand(
            """SELECT id, content_hash
               FROM semantic_documents
               WHERE source_type = @sourceType AND source_key = @sourceKey""",
            conn,
            txn)
        selectCmd.Parameters.AddWithValue("sourceType", sourceType) |> ignore
        selectCmd.Parameters.AddWithValue("sourceKey", sourceKey) |> ignore

        use reader = selectCmd.ExecuteReader()
        let existing =
            if reader.Read() then
                Some(reader.GetGuid(0), reader.GetString(1))
            else
                None
        reader.Close()

        let documentId =
            match existing with
            | Some(id, existingHash) when existingHash = contentHash -> id
            | Some(id, _) ->
                use updateCmd = new NpgsqlCommand(
                    """UPDATE semantic_documents
                       SET title = @title,
                           content_hash = @contentHash,
                           metadata_json = @metadataJson::jsonb,
                           updated_at = now()
                       WHERE id = @id""",
                    conn,
                    txn)
                updateCmd.Parameters.AddWithValue("id", id) |> ignore
                updateCmd.Parameters.AddWithValue("title", title) |> ignore
                updateCmd.Parameters.AddWithValue("contentHash", contentHash) |> ignore
                updateCmd.Parameters.AddWithValue("metadataJson", opt metadataJson) |> ignore
                updateCmd.ExecuteNonQuery() |> ignore
                replaceDocumentChunks conn txn id text
                id
            | None ->
                use insertCmd = new NpgsqlCommand(
                    """INSERT INTO semantic_documents (source_type, source_key, title, content_hash, metadata_json)
                       VALUES (@sourceType, @sourceKey, @title, @contentHash, @metadataJson::jsonb)
                       RETURNING id""",
                    conn,
                    txn)
                insertCmd.Parameters.AddWithValue("sourceType", sourceType) |> ignore
                insertCmd.Parameters.AddWithValue("sourceKey", sourceKey) |> ignore
                insertCmd.Parameters.AddWithValue("title", title) |> ignore
                insertCmd.Parameters.AddWithValue("contentHash", contentHash) |> ignore
                insertCmd.Parameters.AddWithValue("metadataJson", opt metadataJson) |> ignore
                let id = insertCmd.ExecuteScalar() :?> Guid
                replaceDocumentChunks conn txn id text
                id

        txn.Commit()
        documentId
    with ex ->
        txn.Rollback()
        raise ex

let indexForumThread (threadId: Guid) =
    match ForumRepository.getThreadById threadId with
    | None -> None
    | Some thread ->
        let posts = ForumRepository.getPostsByThread threadId 1 500
        let text = buildForumThreadText thread posts
        let metadata = $"""{{"threadId":"{thread.Id}","forumId":"{thread.ForumId}"}}"""
        indexTextDocument "forum-thread" (string thread.Id) thread.Title text (Some metadata)
        |> Some

let indexBlogArticle (articleId: Guid) =
    match BlogRepository.getArticleById articleId with
    | None -> None
    | Some article ->
        let text = buildBlogArticleText article
        let metadata = $"""{{"articleId":"{article.Id}","sectionId":"{article.SectionId}","status":"{article.Status}"}}"""
        indexTextDocument "blog-article" (string article.Id) article.Title text (Some metadata)
        |> Some

let indexMudRoom (roomId: Guid) =
    use conn = openConnection()

    use roomCmd = new NpgsqlCommand(
        """SELECT r.id, r.name, r.slug, r.description, z.id, z.name, z.slug
           FROM mud_rooms r
           JOIN mud_zones z ON z.id = r.zone_id
           WHERE r.id = @roomId""",
        conn)
    roomCmd.Parameters.AddWithValue("roomId", roomId) |> ignore
    use roomReader = roomCmd.ExecuteReader()

    let roomData =
        if roomReader.Read() then
            let roomDescription =
                if roomReader.IsDBNull(3) then None else Some(roomReader.GetString(3))
            Some(
                roomReader.GetGuid(0),
                roomReader.GetString(1),
                roomReader.GetString(2),
                roomDescription,
                roomReader.GetGuid(4),
                roomReader.GetString(5),
                roomReader.GetString(6))
        else
            None

    roomReader.Close()

    match roomData with
    | None -> None
    | Some(roomGuid, roomName, roomSlug, description, zoneGuid, zoneName, zoneSlug) ->
        use exitsCmd = new NpgsqlCommand(
            """SELECT e.direction, rt.name, e.label
               FROM mud_exits e
               JOIN mud_rooms rt ON rt.id = e.to_room_id
               WHERE e.from_room_id = @roomId
               ORDER BY e.direction, rt.name""",
            conn)
        exitsCmd.Parameters.AddWithValue("roomId", roomGuid) |> ignore
        use exitsReader = exitsCmd.ExecuteReader()
        let exits =
            [ while exitsReader.Read() do
                let exitLabel =
                    if exitsReader.IsDBNull(2) then None else Some(exitsReader.GetString(2))
                yield (
                    exitsReader.GetString(0),
                    exitsReader.GetString(1),
                    exitLabel
                ) ]
        exitsReader.Close()

        use itemsCmd = new NpgsqlCommand(
            """SELECT name, description, readable_text
               FROM mud_items
               WHERE room_id = @roomId
               ORDER BY position, name""",
            conn)
        itemsCmd.Parameters.AddWithValue("roomId", roomGuid) |> ignore
        use itemsReader = itemsCmd.ExecuteReader()
        let items =
            [ while itemsReader.Read() do
                let itemDescription =
                    if itemsReader.IsDBNull(1) then None else Some(itemsReader.GetString(1))
                let readableText =
                    if itemsReader.IsDBNull(2) then None else Some(itemsReader.GetString(2))
                yield (
                    itemsReader.GetString(0),
                    itemDescription,
                    readableText
                ) ]
        itemsReader.Close()

        let title = $"{zoneName} / {roomName}"
        let text = buildMudRoomText zoneName roomName description exits items
        let metadata =
            $"""{{"roomId":"{roomGuid}","roomSlug":"{roomSlug}","zoneId":"{zoneGuid}","zoneSlug":"{zoneSlug}"}}"""
        indexTextDocument "mud-room" (string roomGuid) title text (Some metadata)
        |> Some

let reindexMudRooms () =
    use conn = openConnection()
    use cmd = new NpgsqlCommand("SELECT id FROM mud_rooms ORDER BY created_at ASC, name ASC", conn)
    use reader = cmd.ExecuteReader()

    let roomIds =
        [ while reader.Read() do
            yield reader.GetGuid(0) ]

    let mutable indexedCount = 0
    for roomId in roomIds do
        match indexMudRoom roomId with
        | Some _ -> indexedCount <- indexedCount + 1
        | None -> ()

    indexedCount

let reindexIndexedDocuments () =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT source_type, source_key
           FROM semantic_documents
           ORDER BY updated_at DESC, created_at DESC""",
        conn)
    use reader = cmd.ExecuteReader()

    let documents =
        [ while reader.Read() do
            yield reader.GetString(0), reader.GetString(1) ]

    let mutable indexedCount = 0
    let mutable forumCount = 0
    let mutable blogCount = 0
    let mutable mudRoomCount = 0

    for sourceType, sourceKey in documents do
        match Guid.TryParse(sourceKey) with
        | true, sourceId ->
            match sourceType with
            | "forum-thread" ->
                match indexForumThread sourceId with
                | Some _ ->
                    indexedCount <- indexedCount + 1
                    forumCount <- forumCount + 1
                | None -> ()
            | "blog-article" ->
                match indexBlogArticle sourceId with
                | Some _ ->
                    indexedCount <- indexedCount + 1
                    blogCount <- blogCount + 1
                | None -> ()
            | "mud-room" ->
                match indexMudRoom sourceId with
                | Some _ ->
                    indexedCount <- indexedCount + 1
                    mudRoomCount <- mudRoomCount + 1
                | None -> ()
            | _ -> ()
        | _ -> ()

    { DocumentsRequested = documents.Length
      DocumentsIndexed = indexedCount
      ForumThreadsIndexed = forumCount
      BlogArticlesIndexed = blogCount
      MudRoomsIndexed = mudRoomCount }

let getStats () =
    use conn = openConnection()
    let provider = SemanticEmbeddings.getProviderInfo()
    use cmd = new NpgsqlCommand(
        """SELECT
               (SELECT COUNT(*) FROM semantic_documents),
               (SELECT COUNT(*) FROM semantic_chunks),
               (SELECT COUNT(*) FROM semantic_chunk_tokens),
               (SELECT COUNT(*) FROM semantic_chunks WHERE embedding_values IS NOT NULL)""",
        conn)
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        { DocumentCount = reader.GetInt32(0)
          ChunkCount = reader.GetInt32(1)
          TokenCount = reader.GetInt32(2)
          EmbeddedChunkCount = reader.GetInt32(3)
          EmbeddingProvider = provider.Name
          EmbeddingReady = provider.IsReady }
    else
        { DocumentCount = 0
          ChunkCount = 0
          TokenCount = 0
          EmbeddedChunkCount = 0
          EmbeddingProvider = provider.Name
          EmbeddingReady = provider.IsReady }

let searchChunks (query: string) (sourceType: string option) (limit: int) =
    let tokens =
        SemanticPreprocessing.tokenize query
        |> List.distinct
        |> List.toArray

    let queryEmbedding, _ = SemanticEmbeddings.embed query
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT d.source_type,
                  d.source_key,
                  d.title,
                  c.chunk_position,
                  c.content,
                  COALESCE(COUNT(DISTINCT t.token), 0) AS matched_token_count,
                  COALESCE(SUM(t.token_count), 0) AS matched_weight,
                  c.embedding_values
           FROM semantic_chunks c
           JOIN semantic_documents d ON d.id = c.document_id
           LEFT JOIN semantic_chunk_tokens t
             ON t.chunk_id = c.id
            AND (cardinality(@tokens) = 0 OR t.token = ANY(@tokens))
           WHERE c.embedding_values IS NOT NULL
             AND (@sourceType IS NULL OR d.source_type = @sourceType)
           GROUP BY d.source_type, d.source_key, d.title, d.updated_at, c.chunk_position, c.content, c.embedding_values
           ORDER BY matched_token_count DESC, matched_weight DESC, d.updated_at DESC, c.chunk_position ASC
           LIMIT @candidateLimit""",
        conn)
    cmd.Parameters.AddWithValue("tokens", tokens) |> ignore
    cmd.Parameters.AddWithValue("sourceType", sourceType |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("candidateLimit", Math.Max(limit * 10, 50)) |> ignore

    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        let embedding =
            if reader.IsDBNull(7) then
                [||]
            else
                reader.GetFieldValue<float32 array>(7)

        let similarity = SemanticEmbeddings.cosineSimilarity queryEmbedding embedding
        yield
            { SourceType = reader.GetString(0)
              SourceKey = reader.GetString(1)
              Title = reader.GetString(2)
              ChunkPosition = reader.GetInt32(3)
              Content = reader.GetString(4)
              MatchedTokenCount = reader.GetInt64(5) |> int
              MatchedWeight = reader.GetInt64(6) |> int
              Similarity = similarity } ]
    |> List.sortByDescending (fun hit -> hit.Similarity, hit.MatchedTokenCount, hit.MatchedWeight)
    |> List.truncate limit

let selectAnalystEvidence (question: string) (context: DjehutiAnalysisContext) (limit: int) =
    Ai.evidenceFromContext context
    |> SemanticPreprocessing.selectEvidence question limit
