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
      TokenSplitCount: int
      NodeCount: int
      ChunkNodeCount: int
      EdgeCount: int
      EmbeddedChunkCount: int
      EmbeddingProvider: string
      EmbeddingReady: bool }

type SemanticTokenDispersionCandidate =
    { Token: string
      ChunkCount: int
      DocumentCount: int
      SourceTypeCount: int
      NeighborCount: int
      DispersionScore: float
      DispersionBand: string }

type SemanticTokenSplitRecord =
    { Token: string
      ScopeKind: string
      ScopeValue: string
      VariantKey: string }

type SemanticTokenSplitProposalValue =
    { ScopeValue: string
      ChunkCount: int }

type SemanticTokenSplitProposal =
    { Token: string
      ScopeKind: string
      ChunkCount: int
      DocumentCount: int
      SourceTypeCount: int
      DispersionScore: float
      DispersionBand: string
      ScopeValueCount: int
      ScopeValues: SemanticTokenSplitProposalValue list
      Reason: string }

type SemanticAdminActionRecord =
    { Id: Guid
      AdminUserId: Guid
      AdminDisplayName: string
      Action: string
      Token: string option
      ScopeKind: string option
      ScopeValue: string option
      VariantKey: string option
      CreatedCount: int
      ProposalCount: int
      DetailsJson: string
      CreatedAt: DateTimeOffset }

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
      MudRoomsIndexed: int
      MudItemsIndexed: int
      MudRecipesIndexed: int }

type SemanticBackgroundSyncSummary =
    { ForumThreadsRequested: int
      ForumThreadsIndexed: int
      BlogArticlesRequested: int
      BlogArticlesIndexed: int
      MudRoomsRequested: int
      MudRoomsIndexed: int
      MudItemsRequested: int
      MudItemsIndexed: int
      MudRecipesRequested: int
      MudRecipesIndexed: int }

let private sha256 (text: string) =
    use hasher = SHA256.Create()
    let bytes = Encoding.UTF8.GetBytes(text)
    let hash = hasher.ComputeHash(bytes)
    Convert.ToHexString(hash).ToLowerInvariant()

let private opt (value: string option) : obj =
    value |> Option.map box |> Option.defaultValue (box DBNull.Value)

let private getSemanticTokenSplits (conn: NpgsqlConnection) (txn: NpgsqlTransaction option) =
    use cmd =
        match txn with
        | Some transaction ->
            new NpgsqlCommand(
                """SELECT token, scope_kind, scope_value, variant_key
                   FROM semantic_token_splits
                   ORDER BY token ASC, scope_kind ASC, scope_value ASC""",
                conn,
                transaction)
        | None ->
            new NpgsqlCommand(
                """SELECT token, scope_kind, scope_value, variant_key
                   FROM semantic_token_splits
                   ORDER BY token ASC, scope_kind ASC, scope_value ASC""",
                conn)

    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield
            { Token = reader.GetString(0)
              ScopeKind = reader.GetString(1)
              ScopeValue = reader.GetString(2)
              VariantKey = reader.GetString(3) } ]

let listTokenSplits () =
    use conn = openConnection()
    getSemanticTokenSplits conn None

let private normalizeScopeKey (key: string) =
    if String.IsNullOrWhiteSpace key then
        key
    else
        key
        |> Seq.collect (fun ch ->
            if Char.IsUpper ch then [ '-'; Char.ToLowerInvariant ch ] else [ Char.ToLowerInvariant ch ])
        |> Array.ofSeq
        |> String
        |> fun value -> value.TrimStart('-')

let private buildScopeContext (sourceType: string) (provenance: Map<string, string>) =
    provenance
    |> Map.fold (fun state key value ->
        state
        |> Map.add key value
        |> Map.add (normalizeScopeKey key) value) Map.empty
    |> Map.add "source-type" sourceType
    |> Map.add "source_type" sourceType

let upsertTokenSplit (token: string) (scopeKind: string) (scopeValue: string) (variantKey: string) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """INSERT INTO semantic_token_splits (token, source_type, scope_kind, scope_value, variant_key)
           VALUES (@token,
                   CASE WHEN @scopeKind = 'source-type' THEN @scopeValue ELSE 'custom' END,
                   @scopeKind,
                   @scopeValue,
                   @variantKey)
           ON CONFLICT (token, source_type) DO UPDATE
           SET scope_kind = EXCLUDED.scope_kind,
               scope_value = EXCLUDED.scope_value,
               variant_key = EXCLUDED.variant_key""",
        conn)
    cmd.Parameters.AddWithValue("token", token) |> ignore
    cmd.Parameters.AddWithValue("scopeKind", scopeKind) |> ignore
    cmd.Parameters.AddWithValue("scopeValue", scopeValue) |> ignore
    cmd.Parameters.AddWithValue("variantKey", variantKey) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let deleteTokenSplit (token: string) (scopeKind: string) (scopeValue: string) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """DELETE FROM semantic_token_splits
           WHERE token = @token
             AND scope_kind = @scopeKind
             AND scope_value = @scopeValue""",
        conn)
    cmd.Parameters.AddWithValue("token", token) |> ignore
    cmd.Parameters.AddWithValue("scopeKind", scopeKind) |> ignore
    cmd.Parameters.AddWithValue("scopeValue", scopeValue) |> ignore
    cmd.ExecuteNonQuery()

let private resolveTokensForSource (sourceType: string) (provenance: Map<string, string>) (splits: SemanticTokenSplitRecord list) (tokens: string list) =
    let context = buildScopeContext sourceType provenance

    let coreSplits : SemanticTokenSplit list =
        splits
        |> List.map (fun split ->
            { Token = split.Token
              ScopeKind = split.ScopeKind
              ScopeValue = split.ScopeValue
              VariantKey = split.VariantKey })

    tokens
    |> List.map (SemanticSplitting.resolveTokenForContext context coreSplits)

let private createSourceRecord
    (sourceType: string)
    (sourceKey: string)
    (title: string)
    (text: string)
    (metadataJson: string option)
    (provenance: (string * string) list) =
    { SourceType = sourceType
      SourceKey = sourceKey
      Title = title
      Text = text
      MetadataJson = metadataJson
      Provenance = provenance |> Map.ofList }

let private getOrCreateSemanticNodeId (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (nodeKey: string) (displayText: string) =
    use selectCmd = new NpgsqlCommand(
        """SELECT id
           FROM semantic_nodes
           WHERE node_key = @nodeKey""",
        conn,
        txn)
    selectCmd.Parameters.AddWithValue("nodeKey", nodeKey) |> ignore

    match selectCmd.ExecuteScalar() with
    | null
    | :? DBNull ->
        use insertCmd = new NpgsqlCommand(
            """INSERT INTO semantic_nodes (node_key, node_type, display_text, updated_at)
               VALUES (@nodeKey, 'token', @displayText, now())
               ON CONFLICT (node_key) DO UPDATE
               SET display_text = EXCLUDED.display_text,
                   updated_at = now()
               RETURNING id""",
            conn,
            txn)
        insertCmd.Parameters.AddWithValue("nodeKey", nodeKey) |> ignore
        insertCmd.Parameters.AddWithValue("displayText", displayText) |> ignore
        insertCmd.ExecuteScalar() :?> Guid
    | value -> value :?> Guid

let private rebuildChunkGraphFromMetrics
    (conn: NpgsqlConnection)
    (txn: NpgsqlTransaction)
    (chunkId: Guid)
    (tokenMetrics: (string * string * int * int) list) =
    use deleteEdgesCmd = new NpgsqlCommand("DELETE FROM semantic_node_edges WHERE chunk_id = @chunkId", conn, txn)
    deleteEdgesCmd.Parameters.AddWithValue("chunkId", chunkId) |> ignore
    deleteEdgesCmd.ExecuteNonQuery() |> ignore

    use deleteNodesCmd = new NpgsqlCommand("DELETE FROM semantic_chunk_nodes WHERE chunk_id = @chunkId", conn, txn)
    deleteNodesCmd.Parameters.AddWithValue("chunkId", chunkId) |> ignore
    deleteNodesCmd.ExecuteNonQuery() |> ignore

    let graphNodes =
        tokenMetrics
        |> List.map (fun (token, displayToken, weight, firstPosition) ->
            let nodeId = getOrCreateSemanticNodeId conn txn token displayToken

            use linkCmd = new NpgsqlCommand(
                """INSERT INTO semantic_chunk_nodes (chunk_id, node_id, node_weight, first_position)
                   VALUES (@chunkId, @nodeId, @nodeWeight, @firstPosition)
                   ON CONFLICT (chunk_id, node_id) DO UPDATE
                   SET node_weight = EXCLUDED.node_weight,
                       first_position = EXCLUDED.first_position""",
                conn,
                txn)
            linkCmd.Parameters.AddWithValue("chunkId", chunkId) |> ignore
            linkCmd.Parameters.AddWithValue("nodeId", nodeId) |> ignore
            linkCmd.Parameters.AddWithValue("nodeWeight", weight) |> ignore
            linkCmd.Parameters.AddWithValue("firstPosition", firstPosition) |> ignore
            linkCmd.ExecuteNonQuery() |> ignore

            token, nodeId, weight)

    let nodeArray = graphNodes |> List.toArray

    for leftIndex = 0 to nodeArray.Length - 1 do
        let _, leftNodeId, leftWeight = nodeArray[leftIndex]

        for rightIndex = leftIndex + 1 to nodeArray.Length - 1 do
            let _, rightNodeId, rightWeight = nodeArray[rightIndex]
            let edgeWeight = Math.Max(leftWeight * rightWeight, 1)

            use edgeCmd = new NpgsqlCommand(
                """INSERT INTO semantic_node_edges (chunk_id, from_node_id, to_node_id, edge_type, edge_weight)
                   VALUES (@chunkId, @fromNodeId, @toNodeId, 'cooccurrence', @edgeWeight)
                   ON CONFLICT (chunk_id, from_node_id, to_node_id, edge_type) DO UPDATE
                   SET edge_weight = EXCLUDED.edge_weight""",
                conn,
                txn)
            edgeCmd.Parameters.AddWithValue("chunkId", chunkId) |> ignore
            edgeCmd.Parameters.AddWithValue("fromNodeId", leftNodeId) |> ignore
            edgeCmd.Parameters.AddWithValue("toNodeId", rightNodeId) |> ignore
            edgeCmd.Parameters.AddWithValue("edgeWeight", edgeWeight) |> ignore
            edgeCmd.ExecuteNonQuery() |> ignore

let private rebuildChunkGraph
    (conn: NpgsqlConnection)
    (txn: NpgsqlTransaction)
    (chunkId: Guid)
    (tokens: string list)
    (displayTokens: string list) =
    let tokenMetrics =
        List.zip tokens displayTokens
        |> List.mapi (fun position (token, displayToken) -> token, displayToken, position)
        |> List.groupBy (fun (token, _, _) -> token)
        |> List.map (fun (token, positions) ->
            token,
            (positions |> List.head |> fun (_, displayToken, _) -> displayToken),
            positions.Length,
            positions |> List.map (fun (_, _, position) -> position) |> List.min)
        |> List.sortBy (fun (token, _, _, _) -> token)

    rebuildChunkGraphFromMetrics conn txn chunkId tokenMetrics

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

let private buildMudItemText
    (itemName: string)
    (itemSlug: string)
    (description: string option)
    (readableText: string option)
    (roomName: string option)
    (zoneName: string option) =
    let builder = StringBuilder()
    builder.AppendLine(itemName) |> ignore
    builder.AppendLine($"Slug: {itemSlug}") |> ignore
    roomName |> Option.iter (fun value -> builder.AppendLine($"Room: {value}") |> ignore)
    zoneName |> Option.iter (fun value -> builder.AppendLine($"Zone: {value}") |> ignore)
    builder.AppendLine() |> ignore
    builder.AppendLine(description |> Option.defaultValue "The item has no description yet.") |> ignore
    readableText |> Option.iter (fun value -> builder.AppendLine().AppendLine($"Readable text: {value}") |> ignore)
    builder.ToString().Trim()

let private buildMudRecipeText (recipe: MudAdminRepository.MudRecipe) =
    let builder = StringBuilder()
    builder.AppendLine(recipe.Name) |> ignore
    builder.AppendLine($"Creates: {recipe.OutputName}") |> ignore
    builder.AppendLine($"Recipe slug: {recipe.Slug}") |> ignore
    builder.AppendLine($"Output slug: {recipe.OutputSlug}") |> ignore
    builder.AppendLine() |> ignore
    builder.AppendLine(recipe.OutputDescription) |> ignore
    recipe.OutputReadableText |> Option.iter (fun value -> builder.AppendLine().AppendLine($"Readable text: {value}") |> ignore)
    if not recipe.Ingredients.IsEmpty then
        builder.AppendLine().AppendLine("Ingredients:") |> ignore
        recipe.Ingredients
        |> List.sortBy _.Position
        |> List.iter (fun ingredient -> builder.AppendLine($"- {ingredient.Quantity} x {ingredient.Slug}") |> ignore)
    builder.ToString().Trim()

let private replaceDocumentChunks (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (documentId: Guid) (source: SemanticSourceRecord) =
    use deleteCmd = new NpgsqlCommand("DELETE FROM semantic_chunks WHERE document_id = @documentId", conn, txn)
    deleteCmd.Parameters.AddWithValue("documentId", documentId) |> ignore
    deleteCmd.ExecuteNonQuery() |> ignore

    let chunks = SemanticRecords.buildChunkRecords 700 source
    let splits = getSemanticTokenSplits conn (Some txn)

    for chunk in chunks do
        let embedding, provider = SemanticEmbeddings.embed chunk.ChunkText
        let resolvedTokens = resolveTokensForSource source.SourceType source.Provenance splits chunk.Tokens
        use chunkCmd = new NpgsqlCommand(
            """INSERT INTO semantic_chunks (document_id, chunk_position, content, token_count, embedding_values, embedding_provider, embedding_dimension, embedded_at)
               VALUES (@documentId, @position, @content, @tokenCount, @embeddingValues, @embeddingProvider, @embeddingDimension, now())
               RETURNING id""",
            conn,
            txn)
        chunkCmd.Parameters.AddWithValue("documentId", documentId) |> ignore
        chunkCmd.Parameters.AddWithValue("position", chunk.ChunkPosition) |> ignore
        chunkCmd.Parameters.AddWithValue("content", chunk.ChunkText) |> ignore
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

        rebuildChunkGraph conn txn chunkId resolvedTokens chunk.Tokens

let indexSourceDocument (source: SemanticSourceRecord) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    try
        let contentHash = sha256 source.Text

        use selectCmd = new NpgsqlCommand(
            """SELECT id, content_hash
               FROM semantic_documents
               WHERE source_type = @sourceType AND source_key = @sourceKey""",
            conn,
            txn)
        selectCmd.Parameters.AddWithValue("sourceType", source.SourceType) |> ignore
        selectCmd.Parameters.AddWithValue("sourceKey", source.SourceKey) |> ignore

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
                updateCmd.Parameters.AddWithValue("title", source.Title) |> ignore
                updateCmd.Parameters.AddWithValue("contentHash", contentHash) |> ignore
                updateCmd.Parameters.AddWithValue("metadataJson", opt source.MetadataJson) |> ignore
                updateCmd.ExecuteNonQuery() |> ignore
                replaceDocumentChunks conn txn id source
                id
            | None ->
                use insertCmd = new NpgsqlCommand(
                    """INSERT INTO semantic_documents (source_type, source_key, title, content_hash, metadata_json)
                       VALUES (@sourceType, @sourceKey, @title, @contentHash, @metadataJson::jsonb)
                       RETURNING id""",
                    conn,
                    txn)
                insertCmd.Parameters.AddWithValue("sourceType", source.SourceType) |> ignore
                insertCmd.Parameters.AddWithValue("sourceKey", source.SourceKey) |> ignore
                insertCmd.Parameters.AddWithValue("title", source.Title) |> ignore
                insertCmd.Parameters.AddWithValue("contentHash", contentHash) |> ignore
                insertCmd.Parameters.AddWithValue("metadataJson", opt source.MetadataJson) |> ignore
                let id = insertCmd.ExecuteScalar() :?> Guid
                replaceDocumentChunks conn txn id source
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
        createSourceRecord
            "forum-thread"
            (string thread.Id)
            thread.Title
            text
            (Some metadata)
            [ "surface", "forum"
              "threadId", string thread.Id
              "forumId", string thread.ForumId ]
        |> indexSourceDocument
        |> Some

let indexBlogArticle (articleId: Guid) =
    match BlogRepository.getArticleById articleId with
    | None -> None
    | Some article ->
        let text = buildBlogArticleText article
        let metadata = $"""{{"articleId":"{article.Id}","sectionId":"{article.SectionId}","status":"{article.Status}"}}"""
        createSourceRecord
            "blog-article"
            (string article.Id)
            article.Title
            text
            (Some metadata)
            [ "surface", "blog"
              "articleId", string article.Id
              "sectionId", string article.SectionId
              "status", string article.Status ]
        |> indexSourceDocument
        |> Some

let indexMudRoom (roomId: Guid) =
    use conn = openConnection()

    use roomCmd = new NpgsqlCommand(
        """SELECT r.id, r.name, r.slug, r.description, z.id, z.name, z.slug, z.realm_slug
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
                roomReader.GetString(6),
                roomReader.GetString(7))
        else
            None

    roomReader.Close()

    match roomData with
    | None -> None
    | Some(roomGuid, roomName, roomSlug, description, zoneGuid, zoneName, zoneSlug, realmSlug) ->
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
            $"""{{"roomId":"{roomGuid}","roomSlug":"{roomSlug}","zoneId":"{zoneGuid}","zoneSlug":"{zoneSlug}","realmSlug":"{realmSlug}"}}"""
        createSourceRecord
            "mud-room"
            (string roomGuid)
            title
            text
            (Some metadata)
            [ "surface", "mud"
              "entity", "room"
              "roomId", string roomGuid
              "roomSlug", roomSlug
              "zoneId", string zoneGuid
              "zoneSlug", zoneSlug
              "realm", realmSlug
              "realmSlug", realmSlug ]
        |> indexSourceDocument
        |> Some

let indexMudItem (itemId: Guid) =
    use conn = openConnection()
    use itemCmd = new NpgsqlCommand(
        """SELECT i.id, i.name, i.slug, i.description, i.readable_text,
                  r.name, r.slug, z.name, z.slug, z.realm_slug
           FROM mud_items i
           LEFT JOIN mud_rooms r ON r.id = i.room_id
           LEFT JOIN mud_zones z ON z.id = r.zone_id
           WHERE i.id = @itemId AND i.owner_character_id IS NULL""",
        conn)
    itemCmd.Parameters.AddWithValue("itemId", itemId) |> ignore
    use itemReader = itemCmd.ExecuteReader()
    if not (itemReader.Read()) then
        None
    else
        let itemGuid = itemReader.GetGuid(0)
        let itemName = itemReader.GetString(1)
        let itemSlug = itemReader.GetString(2)
        let description = if itemReader.IsDBNull(3) then None else Some(itemReader.GetString(3))
        let readableText = if itemReader.IsDBNull(4) then None else Some(itemReader.GetString(4))
        let roomName = if itemReader.IsDBNull(5) then None else Some(itemReader.GetString(5))
        let roomSlug = if itemReader.IsDBNull(6) then None else Some(itemReader.GetString(6))
        let zoneName = if itemReader.IsDBNull(7) then None else Some(itemReader.GetString(7))
        let zoneSlug = if itemReader.IsDBNull(8) then None else Some(itemReader.GetString(8))
        let realmSlug = if itemReader.IsDBNull(9) then None else Some(itemReader.GetString(9))
        let roomNameJson =
            match roomName with
            | Some value -> $"\"{value}\""
            | None -> "null"
        let title =
            match roomName, zoneName with
            | Some roomValue, Some zoneValue -> $"{zoneValue} / {roomValue} / {itemName}"
            | Some roomValue, None -> $"{roomValue} / {itemName}"
            | _ -> itemName
        let text = buildMudItemText itemName itemSlug description readableText roomName zoneName
        let metadata =
            $"""{{"itemId":"{itemGuid}","itemSlug":"{itemSlug}","roomName":{roomNameJson}}}"""
        let provenance =
            [ Some("surface", "mud")
              Some("entity", "item")
              Some("itemId", string itemGuid)
              Some("itemSlug", itemSlug)
              roomSlug |> Option.map (fun value -> "roomSlug", value)
              zoneSlug |> Option.map (fun value -> "zoneSlug", value)
              realmSlug |> Option.map (fun value -> "realm", value)
              realmSlug |> Option.map (fun value -> "realmSlug", value) ]
            |> List.choose id
        createSourceRecord
            "mud-item"
            (string itemGuid)
            title
            text
            (Some metadata)
            provenance
        |> indexSourceDocument
        |> Some

let indexMudRecipe (recipeId: Guid) =
    MudAdminRepository.getRecipes()
    |> List.tryFind (fun recipe -> recipe.Id = recipeId)
    |> Option.map (fun recipe ->
        let text = buildMudRecipeText recipe
        let metadata = $"""{{"recipeId":"{recipe.Id}","recipeSlug":"{recipe.Slug}","active":{if recipe.Active then "true" else "false"}}}"""
        createSourceRecord
            "mud-recipe"
            (string recipe.Id)
            recipe.Name
            text
            (Some metadata)
            [ "surface", "mud"
              "entity", "recipe"
              "recipeId", string recipe.Id
              "recipeSlug", recipe.Slug ]
        |> indexSourceDocument)

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

let reindexMudItems () =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT id
           FROM mud_items
           WHERE owner_character_id IS NULL
           ORDER BY created_at ASC, name ASC""",
        conn)
    use reader = cmd.ExecuteReader()
    let itemIds = [ while reader.Read() do yield reader.GetGuid(0) ]
    let mutable indexedCount = 0
    for itemId in itemIds do
        match indexMudItem itemId with
        | Some _ -> indexedCount <- indexedCount + 1
        | None -> ()
    indexedCount

let reindexMudRecipes () =
    let recipes = MudAdminRepository.getRecipes()
    let mutable indexedCount = 0
    for recipe in recipes do
        match indexMudRecipe recipe.Id with
        | Some _ -> indexedCount <- indexedCount + 1
        | None -> ()
    indexedCount

let private readGuidList (cmd: NpgsqlCommand) =
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield reader.GetGuid(0) ]

let private staleForumThreadIds (limit: int) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT t.id
           FROM forum_threads t
           LEFT JOIN semantic_documents d
             ON d.source_type = 'forum-thread'
            AND d.source_key = CAST(t.id AS text)
           WHERE d.id IS NULL
              OR d.updated_at < t.updated_at
           ORDER BY t.updated_at DESC, t.created_at DESC
           LIMIT @limit""",
        conn)
    cmd.Parameters.AddWithValue("limit", Math.Max(limit, 0)) |> ignore
    readGuidList cmd

let private staleBlogArticleIds (limit: int) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT a.id
           FROM blog_articles a
           LEFT JOIN semantic_documents d
             ON d.source_type = 'blog-article'
            AND d.source_key = CAST(a.id AS text)
           WHERE a.deleted_at IS NULL
             AND (d.id IS NULL OR d.updated_at < a.updated_at)
           ORDER BY a.updated_at DESC, a.created_at DESC
           LIMIT @limit""",
        conn)
    cmd.Parameters.AddWithValue("limit", Math.Max(limit, 0)) |> ignore
    readGuidList cmd

let private missingMudRoomIds (limit: int) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT r.id
           FROM mud_rooms r
           LEFT JOIN semantic_documents d
             ON d.source_type = 'mud-room'
            AND d.source_key = CAST(r.id AS text)
           WHERE d.id IS NULL
           ORDER BY r.created_at ASC, r.name ASC
           LIMIT @limit""",
        conn)
    cmd.Parameters.AddWithValue("limit", Math.Max(limit, 0)) |> ignore
    readGuidList cmd

let private missingMudItemIds (limit: int) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT i.id
           FROM mud_items i
           LEFT JOIN semantic_documents d
             ON d.source_type = 'mud-item'
            AND d.source_key = CAST(i.id AS text)
           WHERE i.owner_character_id IS NULL
             AND d.id IS NULL
           ORDER BY i.created_at ASC, i.name ASC
           LIMIT @limit""",
        conn)
    cmd.Parameters.AddWithValue("limit", Math.Max(limit, 0)) |> ignore
    readGuidList cmd

let private missingMudRecipeIds (limit: int) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT r.id
           FROM mud_craft_recipes r
           LEFT JOIN semantic_documents d
             ON d.source_type = 'mud-recipe'
            AND d.source_key = CAST(r.id AS text)
           WHERE d.id IS NULL
           ORDER BY r.sort_order ASC, r.name ASC
           LIMIT @limit""",
        conn)
    cmd.Parameters.AddWithValue("limit", Math.Max(limit, 0)) |> ignore
    readGuidList cmd

let runBackgroundSync (forumLimit: int) (blogLimit: int) (mudRoomLimit: int) (mudItemLimit: int) (mudRecipeLimit: int) =
    let forumIds = staleForumThreadIds forumLimit
    let blogIds = staleBlogArticleIds blogLimit
    let mudRoomIds = missingMudRoomIds mudRoomLimit
    let mudItemIds = missingMudItemIds mudItemLimit
    let mudRecipeIds = missingMudRecipeIds mudRecipeLimit

    let mutable forumIndexed = 0
    let mutable blogIndexed = 0
    let mutable mudRoomsIndexed = 0
    let mutable mudItemsIndexed = 0
    let mutable mudRecipesIndexed = 0

    for threadId in forumIds do
        match indexForumThread threadId with
        | Some _ -> forumIndexed <- forumIndexed + 1
        | None -> ()

    for articleId in blogIds do
        match indexBlogArticle articleId with
        | Some _ -> blogIndexed <- blogIndexed + 1
        | None -> ()

    for roomId in mudRoomIds do
        match indexMudRoom roomId with
        | Some _ -> mudRoomsIndexed <- mudRoomsIndexed + 1
        | None -> ()

    for itemId in mudItemIds do
        match indexMudItem itemId with
        | Some _ -> mudItemsIndexed <- mudItemsIndexed + 1
        | None -> ()

    for recipeId in mudRecipeIds do
        match indexMudRecipe recipeId with
        | Some _ -> mudRecipesIndexed <- mudRecipesIndexed + 1
        | None -> ()

    { ForumThreadsRequested = forumIds.Length
      ForumThreadsIndexed = forumIndexed
      BlogArticlesRequested = blogIds.Length
      BlogArticlesIndexed = blogIndexed
      MudRoomsRequested = mudRoomIds.Length
      MudRoomsIndexed = mudRoomsIndexed
      MudItemsRequested = mudItemIds.Length
      MudItemsIndexed = mudItemsIndexed
      MudRecipesRequested = mudRecipeIds.Length
      MudRecipesIndexed = mudRecipesIndexed }

let backfillGraphChunks (limit: int) =
    use conn = openConnection()
    use selectCmd = new NpgsqlCommand(
        """SELECT scn.chunk_id
           FROM semantic_chunk_tokens scn
           LEFT JOIN semantic_chunk_nodes cn ON cn.chunk_id = scn.chunk_id
           LEFT JOIN semantic_node_edges ne ON ne.chunk_id = scn.chunk_id
           GROUP BY scn.chunk_id
           HAVING COUNT(cn.id) = 0 OR COUNT(ne.id) = 0
           ORDER BY scn.chunk_id
           LIMIT @limit""",
        conn)
    selectCmd.Parameters.AddWithValue("limit", Math.Max(limit, 0)) |> ignore
    let chunkIds = readGuidList selectCmd

    let mutable rebuiltCount = 0

    for chunkId in chunkIds do
        use txn = conn.BeginTransaction()
        try
            use metricsCmd = new NpgsqlCommand(
                """SELECT token, token_count, position
                   FROM semantic_chunk_tokens
                   WHERE chunk_id = @chunkId
                   ORDER BY token ASC, position ASC""",
                conn,
                txn)
            metricsCmd.Parameters.AddWithValue("chunkId", chunkId) |> ignore
            use reader = metricsCmd.ExecuteReader()
            let tokenMetrics =
                [ while reader.Read() do
                    yield reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2) ]
            reader.Close()

            use sourceTypeCmd = new NpgsqlCommand(
                """SELECT d.source_type
                   FROM semantic_chunks c
                   JOIN semantic_documents d ON d.id = c.document_id
                   WHERE c.id = @chunkId""",
                conn,
                txn)
            sourceTypeCmd.Parameters.AddWithValue("chunkId", chunkId) |> ignore
            let sourceType =
                match sourceTypeCmd.ExecuteScalar() with
                | :? string as value -> value
                | _ -> "unknown"

            let splits = getSemanticTokenSplits conn (Some txn)
            let resolvedMetrics =
                tokenMetrics
                |> List.map (fun (token, count, position) ->
                    let resolved =
                        resolveTokensForSource sourceType Map.empty splits [ token ]
                        |> List.tryHead
                        |> Option.defaultValue token
                    resolved, token, count, position)

            rebuildChunkGraphFromMetrics conn txn chunkId resolvedMetrics
            txn.Commit()
            rebuiltCount <- rebuiltCount + 1
        with ex ->
            txn.Rollback()
            raise ex

    rebuiltCount

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
    let mutable mudItemCount = 0
    let mutable mudRecipeCount = 0

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
            | "mud-item" ->
                match indexMudItem sourceId with
                | Some _ ->
                    indexedCount <- indexedCount + 1
                    mudItemCount <- mudItemCount + 1
                | None -> ()
            | "mud-recipe" ->
                match indexMudRecipe sourceId with
                | Some _ ->
                    indexedCount <- indexedCount + 1
                    mudRecipeCount <- mudRecipeCount + 1
                | None -> ()
            | _ -> ()
        | _ -> ()

    { DocumentsRequested = documents.Length
      DocumentsIndexed = indexedCount
      ForumThreadsIndexed = forumCount
      BlogArticlesIndexed = blogCount
      MudRoomsIndexed = mudRoomCount
      MudItemsIndexed = mudItemCount
      MudRecipesIndexed = mudRecipeCount }

let getStats () =
    use conn = openConnection()
    let provider = SemanticEmbeddings.getProviderInfo()
    use cmd = new NpgsqlCommand(
        """SELECT
               (SELECT COUNT(*) FROM semantic_documents),
               (SELECT COUNT(*) FROM semantic_chunks),
               (SELECT COUNT(*) FROM semantic_chunk_tokens),
               (SELECT COUNT(*) FROM semantic_token_splits),
               (SELECT COUNT(*) FROM semantic_nodes),
               (SELECT COUNT(*) FROM semantic_chunk_nodes),
               (SELECT COUNT(*) FROM semantic_node_edges),
               (SELECT COUNT(*) FROM semantic_chunks WHERE embedding_values IS NOT NULL)""",
        conn)
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        { DocumentCount = reader.GetInt32(0)
          ChunkCount = reader.GetInt32(1)
          TokenCount = reader.GetInt32(2)
          TokenSplitCount = reader.GetInt32(3)
          NodeCount = reader.GetInt32(4)
          ChunkNodeCount = reader.GetInt32(5)
          EdgeCount = reader.GetInt32(6)
          EmbeddedChunkCount = reader.GetInt32(7)
          EmbeddingProvider = provider.Name
          EmbeddingReady = provider.IsReady }
    else
        { DocumentCount = 0
          ChunkCount = 0
          TokenCount = 0
          TokenSplitCount = 0
          NodeCount = 0
          ChunkNodeCount = 0
          EdgeCount = 0
          EmbeddedChunkCount = 0
          EmbeddingProvider = provider.Name
          EmbeddingReady = provider.IsReady }

let getDispersionCandidates (limit: int) (minChunkCount: int) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT n.node_key,
                  COUNT(DISTINCT scn.chunk_id) AS chunk_count,
                  COUNT(DISTINCT sc.document_id) AS document_count,
                  COUNT(DISTINCT d.source_type) AS source_type_count,
                  COUNT(DISTINCT CASE
                      WHEN e.from_node_id = n.id THEN e.to_node_id
                      WHEN e.to_node_id = n.id THEN e.from_node_id
                      ELSE NULL
                  END) AS neighbor_count
           FROM semantic_nodes n
           JOIN semantic_chunk_nodes scn ON scn.node_id = n.id
           JOIN semantic_chunks sc ON sc.id = scn.chunk_id
           JOIN semantic_documents d ON d.id = sc.document_id
           LEFT JOIN semantic_node_edges e
             ON e.from_node_id = n.id
             OR e.to_node_id = n.id
           WHERE n.node_type = 'token'
           GROUP BY n.node_key, n.id
           HAVING COUNT(DISTINCT scn.chunk_id) >= @minChunkCount
           ORDER BY COUNT(DISTINCT d.source_type) DESC,
                    COUNT(DISTINCT CASE
                        WHEN e.from_node_id = n.id THEN e.to_node_id
                        WHEN e.to_node_id = n.id THEN e.from_node_id
                        ELSE NULL
                    END) DESC,
                    COUNT(DISTINCT sc.document_id) DESC,
                    n.node_key ASC
           LIMIT @limit""",
        conn)
    cmd.Parameters.AddWithValue("limit", Math.Max(1, limit)) |> ignore
    cmd.Parameters.AddWithValue("minChunkCount", Math.Max(1, minChunkCount)) |> ignore
    use reader = cmd.ExecuteReader()

    [ while reader.Read() do
        let evaluated =
            { Token = reader.GetString(0)
              ChunkCount = reader.GetInt32(1)
              DocumentCount = reader.GetInt32(2)
              SourceTypeCount = reader.GetInt32(3)
              NeighborCount = reader.GetInt32(4) }
            |> SemanticDispersion.evaluate

        yield
            { Token = evaluated.Token
              ChunkCount = evaluated.ChunkCount
              DocumentCount = evaluated.DocumentCount
              SourceTypeCount = evaluated.SourceTypeCount
              NeighborCount = evaluated.NeighborCount
              DispersionScore = evaluated.DispersionScore
              DispersionBand = evaluated.DispersionBand } ]

type private SemanticSplitScopeAssessment =
    { Rows: (string * int) list
      DistinctCount: int
      TotalCount: int
      TopShare: float }

let private choosePreferredSplitScope
    (rows: (string * int) list)
    (minimumDistinctValues: int) =
    let cleaned =
        rows
        |> List.filter (fun (_, count) -> count > 0)
        |> List.sortByDescending snd

    let distinctCount = cleaned.Length
    let totalCount = cleaned |> List.sumBy snd

    if distinctCount < minimumDistinctValues || totalCount <= 0 then
        None
    else
        let topShare =
            cleaned
            |> List.head
            |> snd
            |> fun top -> float top / float totalCount

        if topShare >= 0.85 then
            None
        else
            Some
                { Rows = cleaned
                  DistinctCount = distinctCount
                  TotalCount = totalCount
                  TopShare = topShare }

let private loadTokenScopeCounts (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (token: string) =
    let readScopeCounts (sql: string) (parameterName: string) =
        use cmd = new NpgsqlCommand(sql, conn, txn)
        cmd.Parameters.AddWithValue(parameterName, token) |> ignore
        use reader = cmd.ExecuteReader()
        let rows =
            [ while reader.Read() do
                if not (reader.IsDBNull(0)) then
                    yield reader.GetString(0), reader.GetInt32(1) ]
        reader.Close()
        rows

    let realmCounts =
        readScopeCounts
            """SELECT d.metadata_json ->> 'realmSlug' AS scope_value,
                      COUNT(DISTINCT sc.id)::int
               FROM semantic_chunk_tokens sct
               JOIN semantic_chunks sc ON sc.id = sct.chunk_id
               JOIN semantic_documents d ON d.id = sc.document_id
               WHERE sct.token = @token
                 AND d.metadata_json ? 'realmSlug'
               GROUP BY scope_value
               ORDER BY COUNT(DISTINCT sc.id) DESC, scope_value ASC"""
            "token"

    let zoneCounts =
        readScopeCounts
            """SELECT d.metadata_json ->> 'zoneSlug' AS scope_value,
                      COUNT(DISTINCT sc.id)::int
               FROM semantic_chunk_tokens sct
               JOIN semantic_chunks sc ON sc.id = sct.chunk_id
               JOIN semantic_documents d ON d.id = sc.document_id
               WHERE sct.token = @token
                 AND d.metadata_json ? 'zoneSlug'
               GROUP BY scope_value
               ORDER BY COUNT(DISTINCT sc.id) DESC, scope_value ASC"""
            "token"

    let sourceTypeCounts =
        readScopeCounts
            """SELECT d.source_type,
                      COUNT(DISTINCT sc.id)::int
               FROM semantic_chunk_tokens sct
               JOIN semantic_chunks sc ON sc.id = sct.chunk_id
               JOIN semantic_documents d ON d.id = sc.document_id
               WHERE sct.token = @token
               GROUP BY d.source_type
               ORDER BY COUNT(DISTINCT sc.id) DESC, d.source_type ASC"""
            "token"

    [ "zone-slug", zoneCounts
      "realm", realmCounts
      "source-type", sourceTypeCounts ]

let private insertTokenSplitVariants
    (conn: NpgsqlConnection)
    (txn: NpgsqlTransaction)
    (token: string)
    (scopeKind: string)
    (rows: (string * int) list) =
    let mutable createdCount = 0

    for scopeValue, _ in rows do
        let variantKey =
            match scopeKind with
            | "source-type" -> SemanticSplitting.buildSourceTypeVariantKey token scopeValue
            | _ -> $"{token}::{scopeKind}::{scopeValue}"

        use insertCmd = new NpgsqlCommand(
            """INSERT INTO semantic_token_splits (token, source_type, scope_kind, scope_value, variant_key)
               VALUES (@token,
                       CASE WHEN @scopeKind = 'source-type' THEN @scopeValue ELSE 'custom' END,
                       @scopeKind,
                       @scopeValue,
                       @variantKey)
               ON CONFLICT (token, source_type) DO NOTHING""",
            conn,
            txn)
        insertCmd.Parameters.AddWithValue("token", token) |> ignore
        insertCmd.Parameters.AddWithValue("scopeKind", scopeKind) |> ignore
        insertCmd.Parameters.AddWithValue("scopeValue", scopeValue) |> ignore
        insertCmd.Parameters.AddWithValue("variantKey", variantKey) |> ignore
        let inserted = insertCmd.ExecuteNonQuery()
        if inserted > 0 then
            createdCount <- createdCount + 1

    createdCount

let private selectSplitScopeAssessment
    (scopeKindFilter: string option)
    (scopeOptions: (string * (string * int) list) list) =
    let normalizedFilter =
        scopeKindFilter
        |> Option.map (fun value -> value.Trim().ToLowerInvariant())
        |> Option.filter (String.IsNullOrWhiteSpace >> not)

    let filteredScopeOptions =
        match normalizedFilter with
        | Some scopeKind ->
            scopeOptions
            |> List.filter (fun (candidateScopeKind, _) -> candidateScopeKind = scopeKind)
        | None -> scopeOptions

    filteredScopeOptions
    |> List.tryPick (fun (scopeKind, rows) ->
        choosePreferredSplitScope rows 2
        |> Option.map (fun assessment -> scopeKind, assessment))

let materializeSourceTypeTokenSplits (limit: int) (minChunkCount: int) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    try
        let candidates =
            getDispersionCandidates limit minChunkCount
            |> List.filter (fun candidate -> candidate.SourceTypeCount >= 2 && candidate.DispersionBand = "high")

        let mutable createdCount = 0

        for candidate in candidates do
            let scopeOptions = loadTokenScopeCounts conn txn candidate.Token

            let selectedScope =
                selectSplitScopeAssessment None scopeOptions

            match selectedScope with
            | Some(scopeKind, assessment) ->
                createdCount <- createdCount + insertTokenSplitVariants conn txn candidate.Token scopeKind assessment.Rows
            | None -> ()

        txn.Commit()
        createdCount
    with ex ->
        txn.Rollback()
        raise ex

let applyTokenSplitProposal (token: string) (scopeKind: string) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    try
        let normalizedToken = token.Trim().ToLowerInvariant()
        let normalizedScopeKind = scopeKind.Trim().ToLowerInvariant()
        let scopeOptions = loadTokenScopeCounts conn txn normalizedToken

        let createdCount =
            selectSplitScopeAssessment (Some normalizedScopeKind) scopeOptions
            |> Option.map (fun (_, assessment) ->
                insertTokenSplitVariants conn txn normalizedToken normalizedScopeKind assessment.Rows)
            |> Option.defaultValue 0

        txn.Commit()
        createdCount
    with ex ->
        txn.Rollback()
        raise ex

let applyTokenSplitProposals (limit: int) (minChunkCount: int) (scopeKindFilter: string option) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    try
        let mutable createdCount = 0
        let mutable proposalCount = 0

        let candidates =
            getDispersionCandidates limit minChunkCount
            |> List.filter (fun candidate -> candidate.SourceTypeCount >= 2 && candidate.DispersionBand = "high")

        for candidate in candidates do
            let scopeOptions = loadTokenScopeCounts conn txn candidate.Token

            let selectedScope =
                selectSplitScopeAssessment scopeKindFilter scopeOptions

            match selectedScope with
            | Some(scopeKind, assessment) ->
                let inserted = insertTokenSplitVariants conn txn candidate.Token scopeKind assessment.Rows
                if inserted > 0 then
                    proposalCount <- proposalCount + 1
                createdCount <- createdCount + inserted
            | None -> ()

        txn.Commit()
        createdCount, proposalCount
    with ex ->
        txn.Rollback()
        raise ex

let getTokenSplitProposals (limit: int) (minChunkCount: int) (scopeKindFilter: string option) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    try
        let proposals =
            getDispersionCandidates limit minChunkCount
            |> List.filter (fun candidate -> candidate.SourceTypeCount >= 2 && candidate.DispersionBand = "high")
            |> List.choose (fun candidate ->
                let scopeOptions = loadTokenScopeCounts conn txn candidate.Token

                selectSplitScopeAssessment scopeKindFilter scopeOptions
                |> Option.map (fun (scopeKind, assessment) ->
                        { Token = candidate.Token
                          ScopeKind = scopeKind
                          ChunkCount = candidate.ChunkCount
                          DocumentCount = candidate.DocumentCount
                          SourceTypeCount = candidate.SourceTypeCount
                          DispersionScore = candidate.DispersionScore
                          DispersionBand = candidate.DispersionBand
                          ScopeValueCount = assessment.DistinctCount
                          ScopeValues =
                              assessment.Rows
                              |> List.map (fun (scopeValue, chunkCount) ->
                                  { ScopeValue = scopeValue
                                    ChunkCount = chunkCount })
                          Reason =
                              sprintf
                                  "Selected %s because %s spans %d values and the busiest value carries %.1f%% of chunk coverage."
                                  scopeKind
                                  candidate.Token
                                  assessment.DistinctCount
                                  (assessment.TopShare * 100.0) }))

        txn.Commit()
        proposals
    with ex ->
        txn.Rollback()
        raise ex

let expandQueryTokens (sourceType: string option) (tokens: string list) =
    use conn = openConnection()
    let splits = getSemanticTokenSplits conn None
    let coreSplits : SemanticTokenSplit list =
        splits
        |> List.map (fun split ->
            { Token = split.Token
              ScopeKind = split.ScopeKind
              ScopeValue = split.ScopeValue
              VariantKey = split.VariantKey })

    tokens
    |> List.collect (SemanticSplitting.expandQueryToken sourceType coreSplits)
    |> List.distinct

let private readChunkHit (reader: DbDataReader) (queryEmbedding: float32 array) =
    let embedding =
        if reader.IsDBNull(7) then
            [||]
        else
            reader.GetFieldValue<float32 array>(7)

    let similarity = SemanticEmbeddings.cosineSimilarity queryEmbedding embedding
    { SourceType = reader.GetString(0)
      SourceKey = reader.GetString(1)
      Title = reader.GetString(2)
      ChunkPosition = reader.GetInt32(3)
      Content = reader.GetString(4)
      MatchedTokenCount = reader.GetInt64(5) |> int
      MatchedWeight = reader.GetInt64(6) |> int
      Similarity = similarity }

let searchChunks (query: string) (sourceType: string option) (limit: int) =
    let tokens =
        SemanticPreprocessing.tokenize query
        |> List.distinct
        |> expandQueryTokens sourceType
        |> List.toArray

    let queryEmbedding, _ = SemanticEmbeddings.embed query
    use conn = openConnection()
    let coOccurrenceHits =
        try
            use cmd = new NpgsqlCommand(
                """WITH query_nodes AS (
                       SELECT id, node_key
                       FROM semantic_nodes
                       WHERE node_key = ANY(@tokens)
                   ),
                   neighbor_nodes AS (
                       SELECT
                           CASE
                               WHEN e.from_node_id = q.id THEN e.to_node_id
                               ELSE e.from_node_id
                           END AS node_id,
                           SUM(e.edge_weight) AS neighbor_weight
                       FROM semantic_node_edges e
                       JOIN query_nodes q
                         ON e.from_node_id = q.id
                         OR e.to_node_id = q.id
                       GROUP BY 1
                   )
                   SELECT d.source_type,
                          d.source_key,
                          d.title,
                          c.chunk_position,
                          c.content,
                          COUNT(DISTINCT qn.id) AS matched_token_count,
                          COALESCE(SUM(CASE WHEN qn.id IS NOT NULL THEN scn.node_weight ELSE 0 END), 0)
                            + COALESCE(SUM(CASE WHEN nn.node_id IS NOT NULL THEN nn.neighbor_weight ELSE 0 END), 0) AS matched_weight,
                          c.embedding_values
                   FROM semantic_chunks c
                   JOIN semantic_documents d ON d.id = c.document_id
                   LEFT JOIN semantic_chunk_nodes scn ON scn.chunk_id = c.id
                   LEFT JOIN query_nodes qn ON qn.id = scn.node_id
                   LEFT JOIN neighbor_nodes nn ON nn.node_id = scn.node_id
                   WHERE c.embedding_values IS NOT NULL
                     AND (@sourceType IS NULL OR d.source_type = @sourceType)
                     AND (qn.id IS NOT NULL OR nn.node_id IS NOT NULL)
                   GROUP BY d.source_type, d.source_key, d.title, d.updated_at, c.chunk_position, c.content, c.embedding_values
                   ORDER BY matched_token_count DESC, matched_weight DESC, d.updated_at DESC, c.chunk_position ASC
                   LIMIT @candidateLimit""",
                conn)
            cmd.Parameters.AddWithValue("tokens", tokens) |> ignore
            cmd.Parameters.AddWithValue("sourceType", sourceType |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
            cmd.Parameters.AddWithValue("candidateLimit", Math.Max(limit * 10, 50)) |> ignore

            use reader = cmd.ExecuteReader()
            [ while reader.Read() do
                yield readChunkHit reader queryEmbedding ]
        with _ ->
            use fallbackCmd = new NpgsqlCommand(
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
            fallbackCmd.Parameters.AddWithValue("tokens", tokens) |> ignore
            fallbackCmd.Parameters.AddWithValue("sourceType", sourceType |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
            fallbackCmd.Parameters.AddWithValue("candidateLimit", Math.Max(limit * 10, 50)) |> ignore

            use fallbackReader = fallbackCmd.ExecuteReader()
            [ while fallbackReader.Read() do
                yield readChunkHit fallbackReader queryEmbedding ]

    let isolatedCandidateLimit = Math.Max(limit * 8, 40)
    use isolatedCmd = new NpgsqlCommand(
        """SELECT d.source_type,
                  d.source_key,
                  d.title,
                  c.chunk_position,
                  c.content,
                  0 AS matched_token_count,
                  0 AS matched_weight,
                  c.embedding_values
           FROM semantic_chunks c
           JOIN semantic_documents d ON d.id = c.document_id
           WHERE c.embedding_values IS NOT NULL
             AND (@sourceType IS NULL OR d.source_type = @sourceType)
           ORDER BY c.embedded_at DESC NULLS LAST, d.updated_at DESC, c.chunk_position ASC
           LIMIT @candidateLimit""",
        conn)
    isolatedCmd.Parameters.AddWithValue("sourceType", sourceType |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    isolatedCmd.Parameters.AddWithValue("candidateLimit", isolatedCandidateLimit) |> ignore

    use isolatedReader = isolatedCmd.ExecuteReader()
    let isolatedHits =
        [ while isolatedReader.Read() do
            yield readChunkHit isolatedReader queryEmbedding ]

    let coOccurrenceKeys =
        coOccurrenceHits
        |> List.map (fun hit -> $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}")
        |> Set.ofList

    let isolatedSweepHits =
        isolatedHits
        |> List.filter (fun hit ->
            let key = $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}"
            not (Set.contains key coOccurrenceKeys))
        |> List.filter (fun hit -> hit.Similarity > 0.2)
        |> List.sortByDescending (fun hit -> hit.Similarity)
        |> List.truncate (Math.Max(limit / 2, 3))

    List.append coOccurrenceHits isolatedSweepHits
    |> List.sortByDescending (fun hit -> hit.Similarity, hit.MatchedTokenCount, hit.MatchedWeight)
    |> List.truncate limit

let private semanticEvidenceLabel (hit: SemanticChunkHit) =
    $"{hit.SourceType} · {hit.Title}"

let private semanticEvidenceValue (hit: SemanticChunkHit) =
    let scoreText =
        $"similarity={hit.Similarity:F3}; matchedTokens={hit.MatchedTokenCount}; matchedWeight={hit.MatchedWeight}"
    $"{scoreText}; content={hit.Content}"

let selectSemanticEvidence (question: string) (sourceType: string option) (limit: int) =
    searchChunks question sourceType limit
    |> List.mapi (fun index hit ->
        { Label = $"semantic {index + 1} · {semanticEvidenceLabel hit}"
          Value = semanticEvidenceValue hit
          Source = None })

let buildAnalystContextPacket (question: string) (context: DjehutiAnalysisContext) (contextLimit: int) (semanticLimit: int) =
    let localEvidence =
        Ai.evidenceFromContext context
        |> SemanticPreprocessing.selectEvidence question contextLimit

    let semanticEvidence =
        selectSemanticEvidence question None semanticLimit

    List.append localEvidence semanticEvidence

let selectAnalystEvidence (question: string) (context: DjehutiAnalysisContext) (limit: int) =
    Ai.evidenceFromContext context
    |> SemanticPreprocessing.selectEvidence question limit

let logSemanticAdminAction
    (adminUserId: Guid)
    (action: string)
    (token: string option)
    (scopeKind: string option)
    (scopeValue: string option)
    (variantKey: string option)
    (createdCount: int)
    (proposalCount: int)
    (detailsJson: string option) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """INSERT INTO semantic_admin_action_log (
               admin_user_id, action, token, scope_kind, scope_value, variant_key, created_count, proposal_count, details_json
           )
           VALUES (@adminUserId, @action, @token, @scopeKind, @scopeValue, @variantKey, @createdCount, @proposalCount, CAST(@detailsJson AS jsonb))""",
        conn)
    cmd.Parameters.AddWithValue("adminUserId", adminUserId) |> ignore
    cmd.Parameters.AddWithValue("action", action.Trim().ToLowerInvariant()) |> ignore
    cmd.Parameters.AddWithValue("token", opt token) |> ignore
    cmd.Parameters.AddWithValue("scopeKind", opt scopeKind) |> ignore
    cmd.Parameters.AddWithValue("scopeValue", opt scopeValue) |> ignore
    cmd.Parameters.AddWithValue("variantKey", opt variantKey) |> ignore
    cmd.Parameters.AddWithValue("createdCount", createdCount) |> ignore
    cmd.Parameters.AddWithValue("proposalCount", proposalCount) |> ignore
    cmd.Parameters.AddWithValue("detailsJson", defaultArg detailsJson "{}") |> ignore
    cmd.ExecuteNonQuery() |> ignore

let listSemanticAdminActions (limit: int) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT id,
                  sal.admin_user_id,
                  COALESCE(NULLIF(u.display_name, ''), 'Anonymous') AS admin_display_name,
                  sal.action,
                  sal.token,
                  sal.scope_kind,
                  sal.scope_value,
                  sal.variant_key,
                  sal.created_count,
                  sal.proposal_count,
                  sal.details_json::text,
                  sal.created_at
           FROM semantic_admin_action_log sal
           JOIN users u ON u.id = sal.admin_user_id
           ORDER BY created_at DESC
           LIMIT @limit""",
        conn)
    cmd.Parameters.AddWithValue("limit", Math.Max(1, limit)) |> ignore
    use reader = cmd.ExecuteReader()

    [ while reader.Read() do
        yield
            { Id = reader.GetGuid(0)
              AdminUserId = reader.GetGuid(1)
              AdminDisplayName = reader.GetString(2)
              Action = reader.GetString(3)
              Token = if reader.IsDBNull(4) then None else Some(reader.GetString(4))
              ScopeKind = if reader.IsDBNull(5) then None else Some(reader.GetString(5))
              ScopeValue = if reader.IsDBNull(6) then None else Some(reader.GetString(6))
              VariantKey = if reader.IsDBNull(7) then None else Some(reader.GetString(7))
              CreatedCount = reader.GetInt32(8)
              ProposalCount = reader.GetInt32(9)
              DetailsJson = if reader.IsDBNull(10) then "{}" else reader.GetString(10)
              CreatedAt = reader.GetFieldValue<DateTimeOffset>(11) } ]
