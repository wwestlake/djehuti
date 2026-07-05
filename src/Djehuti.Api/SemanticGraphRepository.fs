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
      DriftPressure: float
      AdjustedMinChunkCount: int
      MediumBandEnabled: bool
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
      Similarity: float
      RankingScore: float
      CoOccurrenceWeightMultiplier: float }

type SemanticCurvatureStatus =
    { SampleCount: int
      Curvature: float
      WeightMultiplier: float }

type SemanticRecoveryStatus =
    { Triggered: bool
      Reason: string
      SimilarityFloor: float
      CandidateLimit: int
      ResultLimit: int
      TriggerScore: float }

type SemanticQuerySessionSummary =
    { Id: Guid
      AdminUserId: Guid
      TurnCount: int
      LastQueryText: string option
      LastSourceTypeFilter: string option
      CreatedAt: DateTimeOffset
      UpdatedAt: DateTimeOffset }

type SemanticQueryTurnRecord =
    { Id: Guid
      TurnIndex: int
      QueryText: string
      SourceTypeFilter: string option
      TokenCount: int
      HitCount: int
      SourceTypeDiversity: int
      MatchedTokenTotal: int
      MatchedWeightTotal: int
      TopSimilarity: float
      MeanSimilarity: float
      DriftFromPrevious: float option
      CreatedAt: DateTimeOffset }

type SemanticSearchResponse =
    { Session: SemanticQuerySessionSummary option
      CurrentTurn: SemanticQueryTurnRecord
      RecentTurns: SemanticQueryTurnRecord list
      Curvature: SemanticCurvatureStatus
      Recovery: SemanticRecoveryStatus
      Hits: SemanticChunkHit list
      Recorded: bool }

type SemanticSearchComparison =
    { BaselineHits: SemanticChunkHit list
      TrajectoryHits: SemanticChunkHit list
      Curvature: SemanticCurvatureStatus
      Recovery: SemanticRecoveryStatus
      OverlapCount: int
      BaselineOnlyCount: int
      TrajectoryOnlyCount: int
      BaselineOnlyHits: SemanticChunkHit list
      TrajectoryOnlyHits: SemanticChunkHit list }

type SemanticSearchComparisonSummary =
    { QueryText: string
      SourceTypeFilter: string option
      OverlapCount: int
      BaselineOnlyCount: int
      TrajectoryOnlyCount: int
      Curvature: float
      RecoveryTriggered: bool
      RecoveryReason: string }

type SemanticSessionSearchEvaluation =
    { SessionId: Guid
      TurnCount: int
      ComparedTurnCount: int
      MeanOverlapCount: float
      MeanBaselineOnlyCount: float
      MeanTrajectoryOnlyCount: float
      RecoveryTriggerCount: int
      MeanCurvature: float
      Turns: SemanticSearchComparisonSummary list }

type SemanticQuerySessionDetail =
    { Session: SemanticQuerySessionSummary
      Turns: SemanticQueryTurnRecord list }

type SemanticDriftStatus =
    { RecentTurnCount: int
      DriftSampleCount: int
      MeanDrift: float
      HighDriftCount: int
      HighDriftRatio: float
      PressureMultiplier: float
      BaseMinChunkCount: int
      AdjustedMinChunkCount: int
      MediumBandEnabled: bool }

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

let summarizeDriftSamples (baseMinChunkCount: int) (samples: float list) =
    let cleanedSamples =
        samples
        |> List.filter (fun sample -> not (Double.IsNaN sample) && not (Double.IsInfinity sample))
        |> List.map (fun sample -> Math.Clamp(sample, 0.0, 2.0))

    let sampleCount = cleanedSamples.Length
    let meanDrift =
        if sampleCount = 0 then 0.0
        else cleanedSamples |> List.average

    let highDriftCount =
        cleanedSamples
        |> List.filter (fun sample -> sample >= 0.35)
        |> List.length

    let highDriftRatio =
        if sampleCount = 0 then 0.0
        else float highDriftCount / float sampleCount

    let pressureRaw =
        if sampleCount < 4 then
            1.0
        else
            1.0 + (meanDrift * 0.9) + (highDriftRatio * 0.35)

    let pressureMultiplier = Math.Round(Math.Clamp(pressureRaw, 1.0, 1.75), 3)

    let thresholdReduction =
        if pressureMultiplier >= 1.55 then 2
        elif pressureMultiplier >= 1.25 then 1
        else 0

    let adjustedMinChunkCount = Math.Max(baseMinChunkCount - thresholdReduction, 2)
    let mediumBandEnabled = sampleCount >= 6 && pressureMultiplier >= 1.45

    { RecentTurnCount = sampleCount
      DriftSampleCount = sampleCount
      MeanDrift = Math.Round(meanDrift, 3)
      HighDriftCount = highDriftCount
      HighDriftRatio = Math.Round(highDriftRatio, 3)
      PressureMultiplier = pressureMultiplier
      BaseMinChunkCount = baseMinChunkCount
      AdjustedMinChunkCount = adjustedMinChunkCount
      MediumBandEnabled = mediumBandEnabled }

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

let getSemanticDriftStatus (baseMinChunkCount: int) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT drift_from_previous
           FROM semantic_query_turns
           WHERE drift_from_previous IS NOT NULL
           ORDER BY created_at DESC
           LIMIT 64""",
        conn)
    use reader = cmd.ExecuteReader()
    let samples =
        [ while reader.Read() do
            yield reader.GetDouble(0) ]

    let summary = summarizeDriftSamples baseMinChunkCount samples
    { summary with RecentTurnCount = samples.Length }

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

let private isDriftEligibleCandidate (driftStatus: SemanticDriftStatus) (candidate: SemanticTokenDispersionCandidate) =
    candidate.SourceTypeCount >= 2
    && candidate.ChunkCount >= driftStatus.AdjustedMinChunkCount
    &&
    match candidate.DispersionBand with
    | "high" -> true
    | "medium" -> driftStatus.MediumBandEnabled
    | _ -> false

let private getEligibleTokenSplitCandidates (limit: int) (minChunkCount: int) =
    let driftStatus = getSemanticDriftStatus minChunkCount

    let candidates =
        getDispersionCandidates limit driftStatus.AdjustedMinChunkCount
        |> List.filter (isDriftEligibleCandidate driftStatus)

    driftStatus, candidates

let materializeSourceTypeTokenSplits (limit: int) (minChunkCount: int) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    try
        let _, candidates = getEligibleTokenSplitCandidates limit minChunkCount

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

        let _, candidates = getEligibleTokenSplitCandidates limit minChunkCount

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
        let driftStatus, candidates = getEligibleTokenSplitCandidates limit minChunkCount

        let proposals =
            candidates
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
                          DriftPressure = driftStatus.PressureMultiplier
                          AdjustedMinChunkCount = driftStatus.AdjustedMinChunkCount
                          MediumBandEnabled = driftStatus.MediumBandEnabled
                          ScopeValues =
                              assessment.Rows
                              |> List.map (fun (scopeValue, chunkCount) ->
                                  { ScopeValue = scopeValue
                                    ChunkCount = chunkCount })
                          Reason =
                              sprintf
                                  "Selected %s because %s spans %d values, the busiest value carries %.1f%% of chunk coverage, drift pressure is %.3f, and the active chunk threshold is %d."
                                  scopeKind
                                  candidate.Token
                                  assessment.DistinctCount
                                  (assessment.TopShare * 100.0)
                                  driftStatus.PressureMultiplier
                                  driftStatus.AdjustedMinChunkCount }))

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

let summarizeSemanticQueryTurn (tokens: string list) (hits: SemanticChunkHit list) (previousEmbedding: float32 array option) (queryEmbedding: float32 array) =
    let hitCount = List.length hits
    let sourceTypeDiversity = hits |> List.map (fun hit -> hit.SourceType) |> List.distinct |> List.length
    let matchedTokenTotal = hits |> List.sumBy (fun hit -> hit.MatchedTokenCount)
    let matchedWeightTotal = hits |> List.sumBy (fun hit -> hit.MatchedWeight)
    let topSimilarity = if hitCount = 0 then 0.0 else hits |> List.maxBy (fun hit -> hit.Similarity) |> fun hit -> hit.Similarity
    let meanSimilarity = if hitCount = 0 then 0.0 else hits |> List.averageBy (fun hit -> hit.Similarity)

    let driftFromPrevious =
        previousEmbedding
        |> Option.map (fun embedding ->
            let similarity = SemanticEmbeddings.cosineSimilarity embedding queryEmbedding
            Math.Clamp(1.0 - similarity, 0.0, 2.0))

    {|
        TokenCount = tokens.Length
        HitCount = hitCount
        SourceTypeDiversity = sourceTypeDiversity
        MatchedTokenTotal = matchedTokenTotal
        MatchedWeightTotal = matchedWeightTotal
        TopSimilarity = topSimilarity
        MeanSimilarity = meanSimilarity
        DriftFromPrevious = driftFromPrevious
    |}

let private driftFromPreviousEmbedding (previousEmbedding: float32 array option) (queryEmbedding: float32 array) =
    previousEmbedding
    |> Option.map (fun embedding ->
        let similarity = SemanticEmbeddings.cosineSimilarity embedding queryEmbedding
        Math.Clamp(1.0 - similarity, 0.0, 2.0))

let private baseRankingScore (hit: SemanticChunkHit) (weightMultiplier: float) =
    hit.Similarity
    + (float hit.MatchedTokenCount * 0.01)
    + (float hit.MatchedWeight * 0.001 * weightMultiplier)

let private vectorDifference (left: float32 array) (right: float32 array) =
    if left.Length = 0 || right.Length = 0 || left.Length <> right.Length then
        [||]
    else
        Array.init left.Length (fun index -> left.[index] - right.[index])

let private vectorMagnitude (values: float32 array) =
    values
    |> Array.sumBy (fun value -> let value64 = float value in value64 * value64)
    |> Math.Sqrt

let summarizeCurvatureFromEmbeddings (previousEmbeddings: float32 array list) (queryEmbedding: float32 array) =
    match previousEmbeddings with
    | [ olderEmbedding; newerEmbedding ] when olderEmbedding.Length = queryEmbedding.Length && newerEmbedding.Length = queryEmbedding.Length ->
        let delta1 = vectorDifference newerEmbedding olderEmbedding
        let delta2 = vectorDifference queryEmbedding newerEmbedding
        let sampleCount = 2

        let curvature =
            if delta1.Length = 0 || delta2.Length = 0 || vectorMagnitude delta1 = 0.0 || vectorMagnitude delta2 = 0.0 then
                0.0
            else
                let cosine = SemanticEmbeddings.cosineSimilarity delta1 delta2
                Math.Round(Math.Clamp(1.0 - cosine, 0.0, 2.0), 3)

        let weightMultiplier =
            Math.Round(Math.Clamp(1.0 - (curvature * 0.35), 0.55, 1.0), 3)

        { SampleCount = sampleCount
          Curvature = curvature
          WeightMultiplier = weightMultiplier }
    | _ ->
        { SampleCount = previousEmbeddings.Length
          Curvature = 0.0
          WeightMultiplier = 1.0 }

let applyCurvatureWeighting (curvature: SemanticCurvatureStatus) (hits: SemanticChunkHit list) =
    hits
    |> List.map (fun hit ->
        let score = baseRankingScore hit curvature.WeightMultiplier
        { hit with
            RankingScore = score
            CoOccurrenceWeightMultiplier = curvature.WeightMultiplier })
    |> List.sortByDescending (fun hit -> hit.RankingScore, hit.Similarity, hit.MatchedTokenCount, hit.MatchedWeight)

let summarizeRecoveryStatus (limit: int) (driftFromPrevious: float option) (curvature: SemanticCurvatureStatus) =
    let driftScore = defaultArg driftFromPrevious 0.0
    let triggerScore = Math.Round((driftScore * 0.6) + (curvature.Curvature * 0.4), 3)
    let triggered = triggerScore >= 0.55

    let similarityFloor =
        if triggered then
            Math.Round(Math.Clamp(0.12 + (curvature.Curvature * 0.04), 0.12, 0.2), 3)
        else
            0.2

    let candidateLimit =
        if triggered then Math.Max(limit * 12, 60)
        else Math.Max(limit * 8, 40)

    let resultLimit =
        if triggered then Math.Max(limit, 6)
        else Math.Max(limit / 2, 3)

    let reason =
        if triggered then
            if driftScore >= curvature.Curvature then "drift-led recovery"
            else "curvature-led recovery"
        else
            "stable primary path"

    { Triggered = triggered
      Reason = reason
      SimilarityFloor = similarityFloor
      CandidateLimit = candidateLimit
      ResultLimit = resultLimit
      TriggerScore = triggerScore }

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
      Similarity = similarity
      RankingScore = similarity
      CoOccurrenceWeightMultiplier = 1.0 }

let private searchChunksDetailed
    (query: string)
    (sourceType: string option)
    (limit: int)
    (recovery: SemanticRecoveryStatus) =
    let tokens =
        SemanticPreprocessing.tokenize query
        |> List.distinct
        |> expandQueryTokens sourceType

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
            cmd.Parameters.AddWithValue("tokens", tokens |> List.toArray) |> ignore
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
            fallbackCmd.Parameters.AddWithValue("tokens", tokens |> List.toArray) |> ignore
            fallbackCmd.Parameters.AddWithValue("sourceType", sourceType |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
            fallbackCmd.Parameters.AddWithValue("candidateLimit", Math.Max(limit * 10, 50)) |> ignore

            use fallbackReader = fallbackCmd.ExecuteReader()
            [ while fallbackReader.Read() do
                yield readChunkHit fallbackReader queryEmbedding ]

    let isolatedCandidateLimit = recovery.CandidateLimit
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
        |> List.filter (fun hit -> hit.Similarity > recovery.SimilarityFloor)
        |> List.sortByDescending (fun hit -> hit.Similarity)
        |> List.truncate recovery.ResultLimit

    let hits =
        List.append coOccurrenceHits isolatedSweepHits
        |> List.map (fun hit ->
            let score = baseRankingScore hit 1.0
            { hit with RankingScore = score })
        |> List.sortByDescending (fun hit -> hit.RankingScore, hit.Similarity, hit.MatchedTokenCount, hit.MatchedWeight)
        |> List.truncate limit

    tokens, queryEmbedding, hits

let searchChunks (query: string) (sourceType: string option) (limit: int) =
    let stableRecovery =
        { Triggered = false
          Reason = "stable primary path"
          SimilarityFloor = 0.2
          CandidateLimit = Math.Max(limit * 8, 40)
          ResultLimit = Math.Max(limit / 2, 3)
          TriggerScore = 0.0 }
    let _, _, hits = searchChunksDetailed query sourceType limit stableRecovery
    hits

let private readQueryTurnRecord (reader: DbDataReader) =
    { Id = reader.GetGuid(0)
      TurnIndex = reader.GetInt32(1)
      QueryText = reader.GetString(2)
      SourceTypeFilter = if reader.IsDBNull(3) then None else Some(reader.GetString(3))
      TokenCount = reader.GetInt32(4)
      HitCount = reader.GetInt32(5)
      SourceTypeDiversity = reader.GetInt32(6)
      MatchedTokenTotal = reader.GetInt32(7)
      MatchedWeightTotal = reader.GetInt32(8)
      TopSimilarity = reader.GetDouble(9)
      MeanSimilarity = reader.GetDouble(10)
      DriftFromPrevious = if reader.IsDBNull(11) then None else Some(reader.GetDouble(11))
      CreatedAt = reader.GetFieldValue<DateTimeOffset>(12) }

let private loadSessionSummary (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (adminUserId: Guid) (sessionId: Guid) =
    use cmd = new NpgsqlCommand(
        """SELECT s.id,
                  s.admin_user_id,
                  s.created_at,
                  s.updated_at,
                  COALESCE(COUNT(t.id), 0) AS turn_count,
                  (
                      SELECT st.query_text
                      FROM semantic_query_turns st
                      WHERE st.session_id = s.id
                      ORDER BY st.turn_index DESC
                      LIMIT 1
                  ) AS last_query_text,
                  (
                      SELECT st.source_type_filter
                      FROM semantic_query_turns st
                      WHERE st.session_id = s.id
                      ORDER BY st.turn_index DESC
                      LIMIT 1
                  ) AS last_source_type_filter
           FROM semantic_query_sessions s
           LEFT JOIN semantic_query_turns t ON t.session_id = s.id
           WHERE s.id = @sessionId
             AND s.admin_user_id = @adminUserId
           GROUP BY s.id, s.admin_user_id, s.created_at, s.updated_at""",
        conn,
        txn)
    cmd.Parameters.AddWithValue("sessionId", sessionId) |> ignore
    cmd.Parameters.AddWithValue("adminUserId", adminUserId) |> ignore

    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some
            { Id = reader.GetGuid(0)
              AdminUserId = reader.GetGuid(1)
              CreatedAt = reader.GetFieldValue<DateTimeOffset>(2)
              UpdatedAt = reader.GetFieldValue<DateTimeOffset>(3)
              TurnCount = reader.GetInt64(4) |> int
              LastQueryText = if reader.IsDBNull(5) then None else Some(reader.GetString(5))
              LastSourceTypeFilter = if reader.IsDBNull(6) then None else Some(reader.GetString(6)) }
    else
        None

let private loadRecentQueryTurns (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (sessionId: Guid) (limit: int) =
    use cmd = new NpgsqlCommand(
        """SELECT id,
                  turn_index,
                  query_text,
                  source_type_filter,
                  token_count,
                  hit_count,
                  source_type_diversity,
                  matched_token_total,
                  matched_weight_total,
                  top_similarity,
                  mean_similarity,
                  drift_from_previous,
                  created_at
           FROM semantic_query_turns
           WHERE session_id = @sessionId
           ORDER BY turn_index DESC
           LIMIT @limit""",
        conn,
        txn)
    cmd.Parameters.AddWithValue("sessionId", sessionId) |> ignore
    cmd.Parameters.AddWithValue("limit", limit) |> ignore

    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield readQueryTurnRecord reader ]
    |> List.sortBy (fun turn -> turn.TurnIndex)

let private loadPreviousSessionEmbedding (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (sessionId: Guid) =
    use cmd = new NpgsqlCommand(
        """SELECT query_embedding
           FROM semantic_query_turns
           WHERE session_id = @sessionId
           ORDER BY turn_index DESC
           LIMIT 1""",
        conn,
        txn)
    cmd.Parameters.AddWithValue("sessionId", sessionId) |> ignore
    match cmd.ExecuteScalar() with
    | null
    | :? DBNull -> None
    | value -> Some(value :?> float32 array)

let private loadRecentSessionEmbeddings (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (sessionId: Guid) (limit: int) =
    use cmd = new NpgsqlCommand(
        """SELECT query_embedding
           FROM semantic_query_turns
           WHERE session_id = @sessionId
             AND query_embedding IS NOT NULL
           ORDER BY turn_index DESC
           LIMIT @limit""",
        conn,
        txn)
    cmd.Parameters.AddWithValue("sessionId", sessionId) |> ignore
    cmd.Parameters.AddWithValue("limit", limit) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield reader.GetFieldValue<float32 array>(0) ]
    |> List.rev

let private loadSessionTurnEmbeddings (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (sessionId: Guid) =
    use cmd = new NpgsqlCommand(
        """SELECT turn_index, query_embedding
           FROM semantic_query_turns
           WHERE session_id = @sessionId
             AND query_embedding IS NOT NULL
           ORDER BY turn_index ASC""",
        conn,
        txn)
    cmd.Parameters.AddWithValue("sessionId", sessionId) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield reader.GetInt32(0), reader.GetFieldValue<float32 array>(1) ]

let private ensureQuerySession (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (adminUserId: Guid) (sessionId: Guid option) =
    let existingSummary =
        sessionId
        |> Option.bind (loadSessionSummary conn txn adminUserId)

    match existingSummary with
    | Some summary ->
        let previousEmbedding = loadPreviousSessionEmbedding conn txn summary.Id
        summary.Id, summary.CreatedAt, summary.TurnCount + 1, previousEmbedding
    | None ->
        use cmd = new NpgsqlCommand(
            """INSERT INTO semantic_query_sessions (admin_user_id)
               VALUES (@adminUserId)
               RETURNING id, created_at""",
            conn,
            txn)
        cmd.Parameters.AddWithValue("adminUserId", adminUserId) |> ignore
        use reader = cmd.ExecuteReader()
        reader.Read() |> ignore
        let newSessionId = reader.GetGuid(0)
        let createdAt = reader.GetFieldValue<DateTimeOffset>(1)
        newSessionId, createdAt, 1, None

let private insertQueryTurn
    (conn: NpgsqlConnection)
    (txn: NpgsqlTransaction)
    (sessionId: Guid)
    (turnIndex: int)
    (queryText: string)
    (sourceType: string option)
    (queryEmbedding: float32 array)
    (metrics: {| TokenCount: int
                 HitCount: int
                 SourceTypeDiversity: int
                 MatchedTokenTotal: int
                 MatchedWeightTotal: int
                 TopSimilarity: float
                 MeanSimilarity: float
                 DriftFromPrevious: float option |}) =
    use cmd = new NpgsqlCommand(
        """INSERT INTO semantic_query_turns (
               session_id,
               turn_index,
               query_text,
               source_type_filter,
               token_count,
               hit_count,
               source_type_diversity,
               matched_token_total,
               matched_weight_total,
               top_similarity,
               mean_similarity,
               drift_from_previous,
               query_embedding
           )
           VALUES (
               @sessionId,
               @turnIndex,
               @queryText,
               @sourceType,
               @tokenCount,
               @hitCount,
               @sourceTypeDiversity,
               @matchedTokenTotal,
               @matchedWeightTotal,
               @topSimilarity,
               @meanSimilarity,
               @driftFromPrevious,
               @queryEmbedding
           )
           RETURNING id, created_at""",
        conn,
        txn)
    cmd.Parameters.AddWithValue("sessionId", sessionId) |> ignore
    cmd.Parameters.AddWithValue("turnIndex", turnIndex) |> ignore
    cmd.Parameters.AddWithValue("queryText", queryText) |> ignore
    cmd.Parameters.AddWithValue("sourceType", sourceType |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("tokenCount", metrics.TokenCount) |> ignore
    cmd.Parameters.AddWithValue("hitCount", metrics.HitCount) |> ignore
    cmd.Parameters.AddWithValue("sourceTypeDiversity", metrics.SourceTypeDiversity) |> ignore
    cmd.Parameters.AddWithValue("matchedTokenTotal", metrics.MatchedTokenTotal) |> ignore
    cmd.Parameters.AddWithValue("matchedWeightTotal", metrics.MatchedWeightTotal) |> ignore
    cmd.Parameters.AddWithValue("topSimilarity", metrics.TopSimilarity) |> ignore
    cmd.Parameters.AddWithValue("meanSimilarity", metrics.MeanSimilarity) |> ignore
    cmd.Parameters.AddWithValue("driftFromPrevious", metrics.DriftFromPrevious |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("queryEmbedding", queryEmbedding) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    let id = reader.GetGuid(0)
    let createdAt = reader.GetFieldValue<DateTimeOffset>(1)
    { Id = id
      TurnIndex = turnIndex
      QueryText = queryText
      SourceTypeFilter = sourceType
      TokenCount = metrics.TokenCount
      HitCount = metrics.HitCount
      SourceTypeDiversity = metrics.SourceTypeDiversity
      MatchedTokenTotal = metrics.MatchedTokenTotal
      MatchedWeightTotal = metrics.MatchedWeightTotal
      TopSimilarity = metrics.TopSimilarity
      MeanSimilarity = metrics.MeanSimilarity
      DriftFromPrevious = metrics.DriftFromPrevious
      CreatedAt = createdAt }

let private touchQuerySession (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (sessionId: Guid) =
    use cmd = new NpgsqlCommand(
        """UPDATE semantic_query_sessions
           SET updated_at = now()
           WHERE id = @sessionId""",
        conn,
        txn)
    cmd.Parameters.AddWithValue("sessionId", sessionId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let executeTrackedSearch
    (adminUserId: Guid)
    (query: string)
    (sourceType: string option)
    (limit: int)
    (sessionId: Guid option)
    (recordTurn: bool) =
    let tokens =
        SemanticPreprocessing.tokenize query
        |> List.distinct
        |> expandQueryTokens sourceType
    let queryEmbedding, _ = SemanticEmbeddings.embed query
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    try
        let sessionSummary, recentTurns, currentTurn, curvature, recovery, hits =
            if recordTurn then
                let sessionIdValue, _, nextTurnIndex, previousEmbedding =
                    ensureQuerySession conn txn adminUserId sessionId

                let recentEmbeddings = loadRecentSessionEmbeddings conn txn sessionIdValue 2
                let curvature = summarizeCurvatureFromEmbeddings recentEmbeddings queryEmbedding
                let drift = driftFromPreviousEmbedding previousEmbedding queryEmbedding
                let recovery = summarizeRecoveryStatus limit drift curvature
                let tokens, _, initialHits = searchChunksDetailed query sourceType limit recovery
                let hits = applyCurvatureWeighting curvature initialHits
                let metrics = summarizeSemanticQueryTurn tokens hits previousEmbedding queryEmbedding
                let insertedTurn = insertQueryTurn conn txn sessionIdValue nextTurnIndex query sourceType queryEmbedding metrics
                touchQuerySession conn txn sessionIdValue
                let summary = loadSessionSummary conn txn adminUserId sessionIdValue
                let turns = loadRecentQueryTurns conn txn sessionIdValue 8
                summary, turns, insertedTurn, curvature, recovery, hits
            else
                let summary = sessionId |> Option.bind (loadSessionSummary conn txn adminUserId)
                let previousEmbedding, recentEmbeddings =
                    summary
                    |> Option.map (fun value ->
                        loadPreviousSessionEmbedding conn txn value.Id,
                        loadRecentSessionEmbeddings conn txn value.Id 2)
                    |> Option.defaultValue (None, [])

                let curvature = summarizeCurvatureFromEmbeddings recentEmbeddings queryEmbedding
                let drift = driftFromPreviousEmbedding previousEmbedding queryEmbedding
                let recovery = summarizeRecoveryStatus limit drift curvature
                let tokens, _, initialHits = searchChunksDetailed query sourceType limit recovery
                let hits = applyCurvatureWeighting curvature initialHits
                let metrics = summarizeSemanticQueryTurn tokens hits previousEmbedding queryEmbedding
                let previewTurn =
                    { Id = Guid.Empty
                      TurnIndex = summary |> Option.map (fun value -> value.TurnCount + 1) |> Option.defaultValue 1
                      QueryText = query
                      SourceTypeFilter = sourceType
                      TokenCount = metrics.TokenCount
                      HitCount = metrics.HitCount
                      SourceTypeDiversity = metrics.SourceTypeDiversity
                      MatchedTokenTotal = metrics.MatchedTokenTotal
                      MatchedWeightTotal = metrics.MatchedWeightTotal
                      TopSimilarity = metrics.TopSimilarity
                      MeanSimilarity = metrics.MeanSimilarity
                      DriftFromPrevious = metrics.DriftFromPrevious
                      CreatedAt = DateTimeOffset.UtcNow }

                let turns =
                    summary
                    |> Option.map (fun value -> loadRecentQueryTurns conn txn value.Id 8)
                    |> Option.defaultValue []

                summary, turns, previewTurn, curvature, recovery, hits

        txn.Commit()

        { Session = sessionSummary
          CurrentTurn = currentTurn
          RecentTurns = recentTurns
          Curvature = curvature
          Recovery = recovery
          Hits = hits
          Recorded = recordTurn }
    with ex ->
        txn.Rollback()
        raise ex

let listSemanticQuerySessions (adminUserId: Guid) (limit: int) =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT s.id,
                  s.admin_user_id,
                  s.created_at,
                  s.updated_at,
                  COALESCE(COUNT(t.id), 0) AS turn_count,
                  (
                      SELECT st.query_text
                      FROM semantic_query_turns st
                      WHERE st.session_id = s.id
                      ORDER BY st.turn_index DESC
                      LIMIT 1
                  ) AS last_query_text,
                  (
                      SELECT st.source_type_filter
                      FROM semantic_query_turns st
                      WHERE st.session_id = s.id
                      ORDER BY st.turn_index DESC
                      LIMIT 1
                  ) AS last_source_type_filter
           FROM semantic_query_sessions s
           LEFT JOIN semantic_query_turns t ON t.session_id = s.id
           WHERE s.admin_user_id = @adminUserId
           GROUP BY s.id, s.admin_user_id, s.created_at, s.updated_at
           ORDER BY s.updated_at DESC
           LIMIT @limit""",
        conn)
    cmd.Parameters.AddWithValue("adminUserId", adminUserId) |> ignore
    cmd.Parameters.AddWithValue("limit", limit) |> ignore

    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
        yield
            { Id = reader.GetGuid(0)
              AdminUserId = reader.GetGuid(1)
              CreatedAt = reader.GetFieldValue<DateTimeOffset>(2)
              UpdatedAt = reader.GetFieldValue<DateTimeOffset>(3)
              TurnCount = reader.GetInt64(4) |> int
              LastQueryText = if reader.IsDBNull(5) then None else Some(reader.GetString(5))
              LastSourceTypeFilter = if reader.IsDBNull(6) then None else Some(reader.GetString(6)) } ]

let getSemanticQuerySessionDetail (adminUserId: Guid) (sessionId: Guid) (turnLimit: int) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    try
        match loadSessionSummary conn txn adminUserId sessionId with
        | Some summary ->
            let turns = loadRecentQueryTurns conn txn sessionId turnLimit
            txn.Commit()
            Some { Session = summary; Turns = turns }
        | None ->
            txn.Commit()
            None
    with ex ->
        txn.Rollback()
        raise ex

let compareSearchModes
    (adminUserId: Guid)
    (query: string)
    (sourceType: string option)
    (limit: int)
    (sessionId: Guid option) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    let compareSearchModesCore
        (queryText: string)
        (querySourceType: string option)
        (previousEmbedding: float32 array option)
        (recentEmbeddings: float32 array list) =
        let baselineHits = searchChunks queryText querySourceType limit
        let queryEmbedding, _ = SemanticEmbeddings.embed queryText
        let curvature = summarizeCurvatureFromEmbeddings recentEmbeddings queryEmbedding
        let drift = driftFromPreviousEmbedding previousEmbedding queryEmbedding
        let recovery = summarizeRecoveryStatus limit drift curvature
        let _, _, initialHits = searchChunksDetailed queryText querySourceType limit recovery
        let trajectoryHits =
            applyCurvatureWeighting curvature initialHits
            |> List.truncate limit

        let baselineKeys =
            baselineHits
            |> List.map (fun hit -> $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}")
            |> Set.ofList

        let trajectoryKeys =
            trajectoryHits
            |> List.map (fun hit -> $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}")
            |> Set.ofList

        let overlapCount =
            trajectoryHits
            |> List.filter (fun hit ->
                let key = $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}"
                Set.contains key baselineKeys)
            |> List.length

        let baselineOnlyHits =
            baselineHits
            |> List.filter (fun hit ->
                let key = $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}"
                not (Set.contains key trajectoryKeys))

        let trajectoryOnlyHits =
            trajectoryHits
            |> List.filter (fun hit ->
                let key = $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}"
                not (Set.contains key baselineKeys))

        { BaselineHits = baselineHits
          TrajectoryHits = trajectoryHits
          Curvature = curvature
          Recovery = recovery
          OverlapCount = overlapCount
          BaselineOnlyCount = baselineOnlyHits.Length
          TrajectoryOnlyCount = trajectoryOnlyHits.Length
          BaselineOnlyHits = baselineOnlyHits
          TrajectoryOnlyHits = trajectoryOnlyHits }

    try
        let previousEmbedding, recentEmbeddings =
            sessionId
            |> Option.bind (loadSessionSummary conn txn adminUserId)
            |> Option.map (fun summary ->
                loadPreviousSessionEmbedding conn txn summary.Id,
                loadRecentSessionEmbeddings conn txn summary.Id 2)
            |> Option.defaultValue (None, [])

        let result = compareSearchModesCore query sourceType previousEmbedding recentEmbeddings
        txn.Commit()
        result
    with ex ->
        txn.Rollback()
        raise ex

let evaluateSearchSession
    (adminUserId: Guid)
    (sessionId: Guid)
    (limit: int)
    (turnLimit: int) =
    use conn = openConnection()
    use txn = conn.BeginTransaction()

    let compareSearchModesCore
        (queryText: string)
        (querySourceType: string option)
        (previousEmbedding: float32 array option)
        (recentEmbeddings: float32 array list) =
        let baselineHits = searchChunks queryText querySourceType limit
        let queryEmbedding, _ = SemanticEmbeddings.embed queryText
        let curvature = summarizeCurvatureFromEmbeddings recentEmbeddings queryEmbedding
        let drift = driftFromPreviousEmbedding previousEmbedding queryEmbedding
        let recovery = summarizeRecoveryStatus limit drift curvature
        let _, _, initialHits = searchChunksDetailed queryText querySourceType limit recovery
        let trajectoryHits =
            applyCurvatureWeighting curvature initialHits
            |> List.truncate limit

        let baselineKeys =
            baselineHits
            |> List.map (fun hit -> $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}")
            |> Set.ofList

        let trajectoryKeys =
            trajectoryHits
            |> List.map (fun hit -> $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}")
            |> Set.ofList

        let overlapCount =
            trajectoryHits
            |> List.filter (fun hit ->
                let key = $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}"
                Set.contains key baselineKeys)
            |> List.length

        let baselineOnlyCount =
            baselineHits
            |> List.filter (fun hit ->
                let key = $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}"
                not (Set.contains key trajectoryKeys))
            |> List.length

        let trajectoryOnlyCount =
            trajectoryHits
            |> List.filter (fun hit ->
                let key = $"{hit.SourceType}|{hit.SourceKey}|{hit.ChunkPosition}"
                not (Set.contains key baselineKeys))
            |> List.length

        { QueryText = queryText
          SourceTypeFilter = querySourceType
          OverlapCount = overlapCount
          BaselineOnlyCount = baselineOnlyCount
          TrajectoryOnlyCount = trajectoryOnlyCount
          Curvature = curvature.Curvature
          RecoveryTriggered = recovery.Triggered
          RecoveryReason = recovery.Reason }

    try
        match loadSessionSummary conn txn adminUserId sessionId with
        | None ->
            txn.Commit()
            None
        | Some summary ->
            let turns = loadRecentQueryTurns conn txn sessionId turnLimit
            let turnEmbeddings =
                loadSessionTurnEmbeddings conn txn sessionId
                |> Map.ofList

            let evaluations =
                turns
                |> List.mapi (fun index turn ->
                    let priorTurns = turns |> List.take index
                    let priorEmbeddings =
                        priorTurns
                        |> List.choose (fun priorTurn -> turnEmbeddings |> Map.tryFind priorTurn.TurnIndex)

                    let previousEmbedding = priorEmbeddings |> List.tryLast
                    let recentEmbeddings = priorEmbeddings |> List.rev |> List.truncate 2 |> List.rev

                    compareSearchModesCore turn.QueryText turn.SourceTypeFilter previousEmbedding recentEmbeddings)

            let comparedTurnCount = evaluations.Length
            let meanOverlapCount = if comparedTurnCount = 0 then 0.0 else evaluations |> List.averageBy (fun item -> float item.OverlapCount)
            let meanBaselineOnlyCount = if comparedTurnCount = 0 then 0.0 else evaluations |> List.averageBy (fun item -> float item.BaselineOnlyCount)
            let meanTrajectoryOnlyCount = if comparedTurnCount = 0 then 0.0 else evaluations |> List.averageBy (fun item -> float item.TrajectoryOnlyCount)
            let meanCurvature = if comparedTurnCount = 0 then 0.0 else evaluations |> List.averageBy (fun item -> item.Curvature)
            let recoveryTriggerCount = evaluations |> List.filter (fun item -> item.RecoveryTriggered) |> List.length

            txn.Commit()
            Some
                { SessionId = sessionId
                  TurnCount = summary.TurnCount
                  ComparedTurnCount = comparedTurnCount
                  MeanOverlapCount = Math.Round(meanOverlapCount, 3)
                  MeanBaselineOnlyCount = Math.Round(meanBaselineOnlyCount, 3)
                  MeanTrajectoryOnlyCount = Math.Round(meanTrajectoryOnlyCount, 3)
                  RecoveryTriggerCount = recoveryTriggerCount
                  MeanCurvature = Math.Round(meanCurvature, 3)
                  Turns = evaluations }
    with ex ->
        txn.Rollback()
        raise ex

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
