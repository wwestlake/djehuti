module Djehuti.Api.RagApi

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

let private tryGetAuthClaims (ctx: HttpContext) =
    match ctx.Request.Cookies.TryGetValue("djehuti_auth") with
    | true, token -> Auth.verifyToken token
    | _ ->
        match ctx.Request.Headers.TryGetValue("Authorization") with
        | true, v when v.Count > 0 && v.[0].StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ->
            Auth.verifyToken (v.[0].Substring(7).Trim())
        | _ -> None

// 1. GET /api/semantic/app-context/{appName}
let getAppContext (ctx: HttpContext) (appName: string) : IResult =
    match tryGetAuthClaims ctx with
    | Some _ ->
        match SemanticGraphRepository.getAppContextMetadata appName with
        | Some metadata ->
            Results.Ok({|
                appName = appName
                version = metadata.version
                checksum = metadata.checksum
                lastUpdated = metadata.lastUpdated
            |})
        | None -> Results.NotFound()
    | None -> Results.Unauthorized()

// 2. POST /api/semantic/app-context/{appName}
let postAppContext (ctx: HttpContext) (appName: string) (body: {| version: string; instructions: string; examples: string list; checksum: string |}) : IResult =
    match tryGetAuthClaims ctx with
    | Some claims ->
        match Guid.TryParse(claims.UserId) with
        | true, userId ->
            if SemanticGraphRepository.upsertAppContext appName userId body.version body.instructions body.examples body.checksum then
                Results.Ok({|
                    appName = appName
                    version = body.version
                    checksum = body.checksum
                    indexed = true
                    timestamp = System.DateTimeOffset.UtcNow
                |})
            else
                Results.Problem(detail = "Failed to index app context", statusCode = 500, title = "Error")
        | false, _ -> Results.BadRequest()
    | None -> Results.Unauthorized()

// 3. GET /api/semantic/context
let getContext (ctx: HttpContext) : IResult =
    match tryGetAuthClaims ctx with
    | Some claims ->
        let query = ctx.Request.Query.["query"] |> fun q -> if q.Count > 0 then q.[0] else ""
        let appName = ctx.Request.Query.["app"] |> fun q -> if q.Count > 0 then q.[0] else ""
        let limit =
            match ctx.Request.Query.["limit"] |> fun q -> if q.Count > 0 then Int32.TryParse(q.[0]) else (false, 0) with
            | true, n -> Math.Max(1, Math.Min(n, 50))
            | false, _ -> 5

        match Guid.TryParse(claims.UserId) with
        | true, userId ->
            try
                let hits = SemanticGraphRepository.searchChunks query None (Some userId) limit
                Results.Ok({|
                    query = query
                    appName = appName
                    chunks = hits |> List.map (fun h -> {|
                        sourceType = h.SourceType
                        sourceKey = h.SourceKey
                        title = h.Title
                        content = h.Content
                        similarity = h.Similarity
                        position = h.ChunkPosition
                    |})
                    count = hits.Length
                |})
            with _ ->
                Results.Problem(detail = "Error retrieving context", statusCode = 500, title = "Error")
        | false, _ -> Results.BadRequest()
    | None -> Results.Unauthorized()

// 4. POST /api/semantic/save-conversation
let saveConversation (ctx: HttpContext) (body: {| appName: string; title: string; turns: {| role: string; content: string |} list |}) : IResult =
    match tryGetAuthClaims ctx with
    | Some claims ->
        match Guid.TryParse(claims.UserId) with
        | true, userId ->
            try
                let conversationId = System.Guid.NewGuid()
                let s3Path = $"s3://djehuti-conversations/{body.appName}/{userId}/{conversationId}.json"
                use conn = Database.openConnection()
                use cmd = new Npgsql.NpgsqlCommand(
                    """INSERT INTO conversations (id, user_id, app_name, title, s3_path)
                       VALUES (@id, @userId, @appName, @title, @s3Path)""",
                    conn)
                cmd.Parameters.AddWithValue("id", conversationId) |> ignore
                cmd.Parameters.AddWithValue("userId", userId) |> ignore
                cmd.Parameters.AddWithValue("appName", body.appName) |> ignore
                cmd.Parameters.AddWithValue("title", body.title) |> ignore
                cmd.Parameters.AddWithValue("s3Path", s3Path) |> ignore
                cmd.ExecuteNonQuery() |> ignore
                Results.Created($"/api/semantic/conversations/{conversationId}", {|
                    id = conversationId
                    appName = body.appName
                    title = body.title
                    turnCount = body.turns.Length
                    s3Path = s3Path
                    createdAt = System.DateTimeOffset.UtcNow
                |})
            with _ ->
                Results.Problem(detail = "Failed to save conversation", statusCode = 500, title = "Error")
        | false, _ -> Results.BadRequest()
    | None -> Results.Unauthorized()

// 5. GET /api/semantic/conversations
let listConversations (ctx: HttpContext) : IResult =
    match tryGetAuthClaims ctx with
    | Some claims ->
        let appFilter = ctx.Request.Query.["app"] |> fun q -> if q.Count > 0 then Some(q.[0]) else None
        let limit =
            match ctx.Request.Query.["limit"] |> fun q -> if q.Count > 0 then Int32.TryParse(q.[0]) else (false, 0) with
            | true, n -> Math.Max(1, Math.Min(n, 100))
            | false, _ -> 20
        let offset =
            match ctx.Request.Query.["offset"] |> fun q -> if q.Count > 0 then Int32.TryParse(q.[0]) else (false, 0) with
            | true, n -> Math.Max(0, n)
            | false, _ -> 0

        match Guid.TryParse(claims.UserId) with
        | true, userId ->
            try
                use conn = Database.openConnection()
                let whereClause =
                    match appFilter with
                    | Some app -> $"AND app_name = '{app}'"
                    | None -> ""
                use cmd = new Npgsql.NpgsqlCommand(
                    $"""SELECT id, app_name, title, created_at, updated_at
                       FROM conversations
                       WHERE user_id = @userId {whereClause}
                       ORDER BY updated_at DESC
                       LIMIT @limit OFFSET @offset""",
                    conn)
                cmd.Parameters.AddWithValue("userId", userId) |> ignore
                cmd.Parameters.AddWithValue("limit", limit) |> ignore
                cmd.Parameters.AddWithValue("offset", offset) |> ignore
                use reader = cmd.ExecuteReader()
                let conversations = ResizeArray()
                while reader.Read() do
                    conversations.Add({|
                        id = reader.GetGuid(0)
                        appName = reader.GetString(1)
                        title = reader.GetString(2)
                        createdAt = reader.GetFieldValue<System.DateTimeOffset>(3)
                        updatedAt = reader.GetFieldValue<System.DateTimeOffset>(4)
                    |})
                Results.Ok({|
                    conversations = conversations |> Seq.toList
                    count = conversations.Count
                    limit = limit
                    offset = offset
                |})
            with _ ->
                Results.Problem(detail = "Error listing conversations", statusCode = 500, title = "Error")
        | false, _ -> Results.BadRequest()
    | None -> Results.Unauthorized()

// 6. GET /api/semantic/conversations/{id}
let getConversation (ctx: HttpContext) (conversationId: Guid) : IResult =
    match tryGetAuthClaims ctx with
    | Some claims ->
        match Guid.TryParse(claims.UserId) with
        | true, userId ->
            try
                use conn = Database.openConnection()
                use cmd = new Npgsql.NpgsqlCommand(
                    """SELECT id, user_id, app_name, title, s3_path, created_at
                       FROM conversations
                       WHERE id = @id AND user_id = @userId""",
                    conn)
                cmd.Parameters.AddWithValue("id", conversationId) |> ignore
                cmd.Parameters.AddWithValue("userId", userId) |> ignore
                use reader = cmd.ExecuteReader()
                if reader.Read() then
                    Results.Ok({|
                        id = reader.GetGuid(0)
                        appName = reader.GetString(2)
                        title = reader.GetString(3)
                        s3Path = reader.GetString(4)
                        createdAt = reader.GetFieldValue<System.DateTimeOffset>(5)
                    |})
                else
                    Results.NotFound()
            with _ ->
                Results.Problem(detail = "Error loading conversation", statusCode = 500, title = "Error")
        | false, _ -> Results.BadRequest()
    | None -> Results.Unauthorized()

// 7. DELETE /api/semantic/conversations/{id}
let deleteConversation (ctx: HttpContext) (conversationId: Guid) : IResult =
    match tryGetAuthClaims ctx with
    | Some claims ->
        match Guid.TryParse(claims.UserId) with
        | true, userId ->
            try
                use conn = Database.openConnection()
                use cmd = new Npgsql.NpgsqlCommand(
                    """DELETE FROM conversations
                       WHERE id = @id AND user_id = @userId""",
                    conn)
                cmd.Parameters.AddWithValue("id", conversationId) |> ignore
                cmd.Parameters.AddWithValue("userId", userId) |> ignore
                let affected = cmd.ExecuteNonQuery()
                if affected > 0 then
                    Results.Ok({| deleted = true; id = conversationId |})
                else
                    Results.NotFound()
            with _ ->
                Results.Problem(detail = "Error deleting conversation", statusCode = 500, title = "Error")
        | false, _ -> Results.BadRequest()
    | None -> Results.Unauthorized()

let registerEndpoints (app: WebApplication) =
    app.MapGet("/api/semantic/app-context/{appName}", Func<HttpContext, string, IResult>(getAppContext)) |> ignore
    app.MapPost("/api/semantic/app-context/{appName}", Func<HttpContext, string, {| version: string; instructions: string; examples: string list; checksum: string |}, IResult>(postAppContext)) |> ignore
    app.MapGet("/api/semantic/context", Func<HttpContext, IResult>(getContext)) |> ignore
    app.MapPost("/api/semantic/save-conversation", Func<HttpContext, {| appName: string; title: string; turns: {| role: string; content: string |} list |}, IResult>(saveConversation)) |> ignore
    app.MapGet("/api/semantic/conversations", Func<HttpContext, IResult>(listConversations)) |> ignore
    app.MapGet("/api/semantic/conversations/{id}", Func<HttpContext, Guid, IResult>(getConversation)) |> ignore
    app.MapDelete("/api/semantic/conversations/{id}", Func<HttpContext, Guid, IResult>(deleteConversation)) |> ignore
