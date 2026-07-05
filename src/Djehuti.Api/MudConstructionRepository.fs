module Djehuti.Api.MudConstructionRepository

open System
open System.Data.Common
open System.Text.Json
open System.Text.RegularExpressions
open Npgsql
open Database

type DirectorSpec =
    { Slug: string
      RealmSlug: string
      DisplayName: string
      Aliases: string list }

type BuilderAgent =
    { Id: Guid
      Slug: string
      RealmSlug: string
      DirectorSlug: string
      DisplayName: string
      Specialty: string
      Model: string
      BuildHourUtc: int
      Active: bool }

type RealmDirective =
    { Id: Guid
      RealmSlug: string
      DirectorSlug: string
      RawCommand: string
      NormalizedInstruction: string
      RequestedByUserId: Guid
      RequestedByCharacterId: Guid
      CreatedAt: DateTime }

type BuildJob =
    { Id: Guid
      BuilderAgentId: Guid
      BuilderSlug: string
      RealmSlug: string
      DirectiveId: Guid option
      BuildDate: DateOnly
      ScheduledFor: DateTime
      Status: string
      RetryCount: int
      AnchorRoomId: Guid option
      CreatedRoomId: Guid option
      Payload: string option
      Error: string option }

type AnchorRoom =
    { RoomId: Guid
      ZoneId: Guid
      ZoneSlug: string
      RoomSlug: string
      RoomName: string
      RoomDescription: string option
      MapX: int option
      MapY: int option
      Direction: string
      ReverseDirection: string
      NewMapX: int option
      NewMapY: int option }

[<CLIMutable>]
type BuildPlan =
    { RoomName: string
      RoomSlug: string
      RoomDescription: string
      ResourceName: string
      ResourceSlug: string
      ResourceDescription: string
      LoreFigureName: string
      LoreFigureSlug: string
      LoreFigureDescription: string
      LoreSpeech: string
      ForwardExitLabel: string
      BackwardExitLabel: string
      ExitType: string }

let private directorSpecs =
    [ { Slug = "headmaster"; RealmSlug = "medieval"; DisplayName = "Headmaster"; Aliases = [ "headmaster"; "hm" ] }
      { Slug = "firstspeaker"; RealmSlug = "sci-fi"; DisplayName = "FirstSpeaker"; Aliases = [ "firstspeaker"; "fs" ] } ]

let private builderSeeds =
    [ ("medieval-mason", "medieval", "headmaster", "Stonewright Avel", "mason", 0)
      ("medieval-gardener", "medieval", "headmaster", "Gardener Ysra", "gardener", 2)
      ("medieval-merchant", "medieval", "headmaster", "Quartermaster Pell", "merchant", 4)
      ("medieval-warden", "medieval", "headmaster", "Warden Brann", "warden", 6)
      ("medieval-scribe", "medieval", "headmaster", "Archivist Maelin", "scribe", 8)
      ("medieval-carpenter", "medieval", "headmaster", "Carpenter Hollis", "carpenter", 10)
      ("scifi-fabricator", "sci-fi", "firstspeaker", "Fabricator N-3", "fabricator", 12)
      ("scifi-horticulture", "sci-fi", "firstspeaker", "Growmaster Ilex", "horticulture", 14)
      ("scifi-logistics", "sci-fi", "firstspeaker", "Logistics Unit Vara", "logistics", 16)
      ("scifi-signal", "sci-fi", "firstspeaker", "Signal Tech Orin", "signal engineer", 18)
      ("scifi-structural", "sci-fi", "firstspeaker", "Hullwright Sera", "structural engineer", 20)
      ("scifi-cartographer", "sci-fi", "firstspeaker", "Cartographer Keph", "cartographer", 22) ]

let private serializerOptions =
    let options = JsonSerializerOptions()
    options.PropertyNameCaseInsensitive <- true
    options

let private slugify (value: string) =
    let normalized =
        if isNull value then ""
        else value.Trim().ToLowerInvariant()
    Regex.Replace(normalized, "[^a-z0-9]+", "-").Trim('-')

let private clip maxLength (value: string) =
    let trimmed =
        if isNull value then ""
        else Regex.Replace(value.Trim(), "\s+", " ")
    if trimmed.Length <= maxLength then trimmed else trimmed.Substring(0, maxLength).Trim()

let private nonBlank (value: string) =
    let trimmed = clip Int32.MaxValue value
    if String.IsNullOrWhiteSpace trimmed then None else Some trimmed

let private normalizeAlias (value: string) =
    let trimmed = if isNull value then "" else value.Trim().ToLowerInvariant()
    if trimmed.StartsWith("@") then trimmed.Substring(1) else trimmed

let private tryFindDirectorByAlias (value: string) =
    let alias = normalizeAlias value
    directorSpecs
    |> List.tryFind (fun spec -> spec.Aliases |> List.exists (fun candidate -> candidate = alias))

let private trySplitDirectorPrefixedText (text: string) =
    let trimmed = if isNull text then "" else text.Trim()
    if not (trimmed.StartsWith("@")) then
        None
    else
        let parts = trimmed.Split([|' '|], 2, StringSplitOptions.RemoveEmptyEntries)
        if parts.Length = 0 then None
        else
            match tryFindDirectorByAlias parts.[0] with
            | None -> None
            | Some spec ->
                let remainder = if parts.Length > 1 then parts.[1].Trim() else ""
                Some (spec, remainder)

let private readBuilder (reader: DbDataReader) =
    { Id = reader.GetGuid(0)
      Slug = reader.GetString(1)
      RealmSlug = reader.GetString(2)
      DirectorSlug = reader.GetString(3)
      DisplayName = reader.GetString(4)
      Specialty = reader.GetString(5)
      Model = reader.GetString(6)
      BuildHourUtc = reader.GetInt32(7)
      Active = reader.GetBoolean(8) }

let private readBuildJob (reader: DbDataReader) =
    { Id = reader.GetGuid(0)
      BuilderAgentId = reader.GetGuid(1)
      BuilderSlug = reader.GetString(2)
      RealmSlug = reader.GetString(3)
      DirectiveId = if reader.IsDBNull(4) then None else Some (reader.GetGuid(4))
      BuildDate = reader.GetFieldValue<DateOnly>(5)
      ScheduledFor = reader.GetFieldValue<DateTime>(6)
      Status = reader.GetString(7)
      RetryCount = reader.GetInt32(8)
      AnchorRoomId = if reader.IsDBNull(9) then None else Some (reader.GetGuid(9))
      CreatedRoomId = if reader.IsDBNull(10) then None else Some (reader.GetGuid(10))
      Payload = if reader.IsDBNull(11) then None else Some (reader.GetString(11))
      Error = if reader.IsDBNull(12) then None else Some (reader.GetString(12)) }

type private ActiveCharacter =
    { CharacterId: Guid
      CharacterName: string
      RoomId: Guid
      RealmSlug: string }

let private tryGetActiveCharacter (userId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT c.id, c.display_name, c.current_room_id, c.realm_slug
           FROM users u
           JOIN mud_characters c ON c.id = u.active_mud_character_id
           WHERE u.id = @user_id AND c.deleted_at IS NULL""", conn)
    cmd.Parameters.AddWithValue("user_id", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some
            { CharacterId = reader.GetGuid(0)
              CharacterName = reader.GetString(1)
              RoomId = reader.GetGuid(2)
              RealmSlug = reader.GetString(3) }
    else
        None

let private isUserAdmin (userId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("SELECT role FROM users WHERE id = @user_id", conn)
    cmd.Parameters.AddWithValue("user_id", userId) |> ignore
    match cmd.ExecuteScalar() with
    | :? string as role -> role.Equals("admin", StringComparison.OrdinalIgnoreCase)
    | _ -> false

let private insertPrivateDirectorReply (conn: NpgsqlConnection) (directorName: string) (recipientCharacterId: Guid) (recipientName: string) (body: string) =
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_chat_messages
               (channel, sender_name, recipient_character_id, recipient_name, body)
           VALUES ('whisper', @sender_name, @recipient_id, @recipient_name, @body)""", conn)
    cmd.Parameters.AddWithValue("sender_name", directorName) |> ignore
    cmd.Parameters.AddWithValue("recipient_id", recipientCharacterId) |> ignore
    cmd.Parameters.AddWithValue("recipient_name", recipientName) |> ignore
    cmd.Parameters.AddWithValue("body", body) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let private upsertDirective
    (conn: NpgsqlConnection)
    (spec: DirectorSpec)
    (userId: Guid)
    (character: ActiveCharacter)
    (rawCommand: string)
    (normalizedInstruction: string) =
    use closeCmd = new NpgsqlCommand(
        """UPDATE mud_director_directives
           SET active = FALSE,
               superseded_at = now()
           WHERE realm_slug = @realm_slug
             AND active = TRUE""", conn)
    closeCmd.Parameters.AddWithValue("realm_slug", spec.RealmSlug) |> ignore
    closeCmd.ExecuteNonQuery() |> ignore

    use insertCmd = new NpgsqlCommand(
        """INSERT INTO mud_director_directives
               (realm_slug, director_slug, raw_command, normalized_instruction, requested_by_user_id, requested_by_character_id)
           VALUES (@realm_slug, @director_slug, @raw_command, @normalized_instruction, @user_id, @character_id)
           RETURNING id, realm_slug, director_slug, raw_command, normalized_instruction, requested_by_user_id, requested_by_character_id, created_at""", conn)
    insertCmd.Parameters.AddWithValue("realm_slug", spec.RealmSlug) |> ignore
    insertCmd.Parameters.AddWithValue("director_slug", spec.Slug) |> ignore
    insertCmd.Parameters.AddWithValue("raw_command", rawCommand) |> ignore
    insertCmd.Parameters.AddWithValue("normalized_instruction", normalizedInstruction) |> ignore
    insertCmd.Parameters.AddWithValue("user_id", userId) |> ignore
    insertCmd.Parameters.AddWithValue("character_id", character.CharacterId) |> ignore
    use reader = insertCmd.ExecuteReader()
    if reader.Read() then
        { Id = reader.GetGuid(0)
          RealmSlug = reader.GetString(1)
          DirectorSlug = reader.GetString(2)
          RawCommand = reader.GetString(3)
          NormalizedInstruction = reader.GetString(4)
          RequestedByUserId = reader.GetGuid(5)
          RequestedByCharacterId = reader.GetGuid(6)
          CreatedAt = reader.GetFieldValue<DateTime>(7) }
    else
        failwith "Failed to store director directive."

let tryHandleDirectorMessage (userId: Guid) (targetName: string option) (text: string) : Result<string, string> option =
    let intercepted =
        match targetName |> Option.bind tryFindDirectorByAlias with
        | Some spec -> Some (spec, if isNull text then "" else text.Trim())
        | None -> trySplitDirectorPrefixedText text

    match intercepted with
    | None -> None
    | Some (spec, instruction) ->
        let cleanedInstruction = clip 500 instruction
        if not (isUserAdmin userId) then
            Some (Error $"Only administrators can direct {spec.DisplayName} right now.")
        else
            match tryGetActiveCharacter userId with
            | None -> Some (Error "Choose or create a character first.")
            | Some character when String.IsNullOrWhiteSpace cleanedInstruction ->
                Some (Error $"Tell {spec.DisplayName} what to build.")
            | Some character ->
                use conn = openConnection ()
                let directive = upsertDirective conn spec userId character text cleanedInstruction
                let reply =
                    $"{spec.DisplayName} whispers: Order received for the {spec.RealmSlug} crew. Today's standing directive is now '{directive.NormalizedInstruction}'. Six builders will work this realm on their daily cycle."
                insertPrivateDirectorReply conn spec.DisplayName character.CharacterId character.CharacterName reply
                Some (Ok reply)

// Seeds any builder agents from the bootstrap list that are missing from the
// table (e.g. a new spec added to builderSeeds). Never overwrites an
// existing row, so admin edits to display_name/specialty/model/etc. made via
// the roster admin panel persist across worker restarts and every tick.
let ensureBuilderRoster () =
    use conn = openConnection ()
    for (slug, realmSlug, directorSlug, displayName, specialty, buildHourUtc) in builderSeeds do
        use cmd = new NpgsqlCommand(
            """INSERT INTO mud_builder_agents (slug, realm_slug, director_slug, display_name, specialty, model, build_hour_utc, active)
               VALUES (@slug, @realm_slug, @director_slug, @display_name, @specialty, 'gpt-4o-mini', @build_hour_utc, TRUE)
               ON CONFLICT (slug) DO NOTHING""", conn)
        cmd.Parameters.AddWithValue("slug", slug) |> ignore
        cmd.Parameters.AddWithValue("realm_slug", realmSlug) |> ignore
        cmd.Parameters.AddWithValue("director_slug", directorSlug) |> ignore
        cmd.Parameters.AddWithValue("display_name", displayName) |> ignore
        cmd.Parameters.AddWithValue("specialty", specialty) |> ignore
        cmd.Parameters.AddWithValue("build_hour_utc", buildHourUtc) |> ignore
        cmd.ExecuteNonQuery() |> ignore

let getLatestDirective (realmSlug: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, realm_slug, director_slug, raw_command, normalized_instruction, requested_by_user_id, requested_by_character_id, created_at
           FROM mud_director_directives
           WHERE realm_slug = @realm_slug
             AND active = TRUE
           ORDER BY created_at DESC
           LIMIT 1""", conn)
    cmd.Parameters.AddWithValue("realm_slug", realmSlug) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some
            { Id = reader.GetGuid(0)
              RealmSlug = reader.GetString(1)
              DirectorSlug = reader.GetString(2)
              RawCommand = reader.GetString(3)
              NormalizedInstruction = reader.GetString(4)
              RequestedByUserId = reader.GetGuid(5)
              RequestedByCharacterId = reader.GetGuid(6)
              CreatedAt = reader.GetFieldValue<DateTime>(7) }
    else
        None

let enqueueDueBuildJobs (nowUtc: DateTime) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, slug, realm_slug, director_slug, display_name, specialty, model, build_hour_utc, active
           FROM mud_builder_agents
           WHERE active = TRUE
           ORDER BY realm_slug, build_hour_utc, slug""", conn)
    use reader = cmd.ExecuteReader()
    let builders = [ while reader.Read() do yield readBuilder reader ]
    reader.Close()

    let mutable queued = 0
    let today = DateOnly.FromDateTime(nowUtc)
    for builder in builders do
        let scheduledFor = DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, builder.BuildHourUtc, 0, 0, DateTimeKind.Utc)
        if scheduledFor <= nowUtc then
            use existingCmd = new NpgsqlCommand(
                """SELECT 1
                   FROM mud_build_jobs
                   WHERE builder_agent_id = @builder_id
                     AND build_date = @build_date
                   LIMIT 1""", conn)
            existingCmd.Parameters.AddWithValue("builder_id", builder.Id) |> ignore
            existingCmd.Parameters.AddWithValue("build_date", today) |> ignore
            let exists = existingCmd.ExecuteScalar() <> null
            if not exists then
                let directiveId = getLatestDirective builder.RealmSlug |> Option.map _.Id
                use insertCmd = new NpgsqlCommand(
                    """INSERT INTO mud_build_jobs (builder_agent_id, realm_slug, directive_id, build_date, scheduled_for, status)
                       VALUES (@builder_id, @realm_slug, @directive_id, @build_date, @scheduled_for, 'Pending')""", conn)
                insertCmd.Parameters.AddWithValue("builder_id", builder.Id) |> ignore
                insertCmd.Parameters.AddWithValue("realm_slug", builder.RealmSlug) |> ignore
                insertCmd.Parameters.AddWithValue("directive_id", directiveId |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
                insertCmd.Parameters.AddWithValue("build_date", today) |> ignore
                insertCmd.Parameters.AddWithValue("scheduled_for", scheduledFor) |> ignore
                queued <- queued + insertCmd.ExecuteNonQuery()
    queued

let fetchAndLockBuildJobs (limit: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_build_jobs j
           SET status = 'Processing',
               started_at = now()
           WHERE j.id IN (
               SELECT j2.id
               FROM mud_build_jobs j2
               WHERE j2.status = 'Pending'
                 AND j2.scheduled_for <= now()
               ORDER BY j2.scheduled_for, j2.created_at
               LIMIT @limit
               FOR UPDATE SKIP LOCKED
           )
           RETURNING j.id,
                     j.builder_agent_id,
                     (SELECT slug FROM mud_builder_agents WHERE id = j.builder_agent_id),
                     j.realm_slug,
                     j.directive_id,
                     j.build_date,
                     j.scheduled_for,
                     j.status,
                     j.retry_count,
                     j.anchor_room_id,
                     j.created_room_id,
                     j.payload::text,
                     j.error""", conn)
    cmd.Parameters.AddWithValue("limit", limit) |> ignore
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do yield readBuildJob reader ]

let getBuilderAgent (builderAgentId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT id, slug, realm_slug, director_slug, display_name, specialty, model, build_hour_utc, active
           FROM mud_builder_agents
           WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("id", builderAgentId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readBuilder reader) else None

let private oppositeDirection direction =
    match direction with
    | "north" -> "south"
    | "south" -> "north"
    | "east" -> "west"
    | "west" -> "east"
    | "up" -> "down"
    | "down" -> "up"
    | _ -> "back"

let tryPickAnchorRoom (realmSlug: string) =
    use conn = openConnection ()
    use roomCmd = new NpgsqlCommand(
        """SELECT r.id, r.zone_id, z.slug, r.slug, r.name, r.description, r.map_x, r.map_y
           FROM mud_rooms r
           JOIN mud_zones z ON z.id = r.zone_id
           WHERE z.realm_slug = @realm_slug
           ORDER BY z.position, r.position, r.created_at""", conn)
    roomCmd.Parameters.AddWithValue("realm_slug", realmSlug) |> ignore
    use roomReader = roomCmd.ExecuteReader()
    let rooms =
        [ while roomReader.Read() do
            let roomDescription =
                if roomReader.IsDBNull(5) then None else Some (roomReader.GetString(5))
            let mapX =
                if roomReader.IsDBNull(6) then None else Some (roomReader.GetInt32(6))
            let mapY =
                if roomReader.IsDBNull(7) then None else Some (roomReader.GetInt32(7))
            yield
                (roomReader.GetGuid(0),
                 roomReader.GetGuid(1),
                 roomReader.GetString(2),
                 roomReader.GetString(3),
                 roomReader.GetString(4),
                 roomDescription,
                 mapX,
                 mapY) ]
    roomReader.Close()

    let roomIds = rooms |> List.map (fun (roomId, _, _, _, _, _, _, _) -> roomId)
    use exitCmd = new NpgsqlCommand(
        """SELECT from_room_id, direction
           FROM mud_exits
           WHERE from_room_id = ANY(@room_ids)""", conn)
    exitCmd.Parameters.AddWithValue("room_ids", roomIds |> List.toArray) |> ignore
    use exitReader = exitCmd.ExecuteReader()
    let exits =
        [ while exitReader.Read() do
            yield exitReader.GetGuid(0), exitReader.GetString(1).ToLowerInvariant() ]
    exitReader.Close()

    let occupiedCoords =
        rooms
        |> List.choose (fun (_, _, _, _, _, _, x, y) ->
            match x, y with
            | Some xValue, Some yValue -> Some (xValue, yValue)
            | _ -> None)
        |> Set.ofList

    let exitMap =
        exits
        |> List.groupBy fst
        |> List.map (fun (roomId, rows) -> roomId, rows |> List.map snd |> Set.ofList)
        |> Map.ofList

    let directions =
        [ ("north", 0, -1)
          ("east", 1, 0)
          ("south", 0, 1)
          ("west", -1, 0) ]

    rooms
    |> List.tryPick (fun (roomId, zoneId, zoneSlug, roomSlug, roomName, roomDescription, mapX, mapY) ->
        let existing = exitMap |> Map.tryFind roomId |> Option.defaultValue Set.empty
        directions
        |> List.tryPick (fun (direction, dx, dy) ->
            if existing.Contains direction then
                None
            else
                let newCoords =
                    match mapX, mapY with
                    | Some xValue, Some yValue ->
                        let candidate = (xValue + dx, yValue + dy)
                        if occupiedCoords.Contains candidate then None else Some candidate
                    | _ -> None
                Some
                    { RoomId = roomId
                      ZoneId = zoneId
                      ZoneSlug = zoneSlug
                      RoomSlug = roomSlug
                      RoomName = roomName
                      RoomDescription = roomDescription
                      MapX = mapX
                      MapY = mapY
                      Direction = direction
                      ReverseDirection = oppositeDirection direction
                      NewMapX = newCoords |> Option.map fst
                      NewMapY = newCoords |> Option.map snd }))

let fallbackPlan (builder: BuilderAgent) (anchor: AnchorRoom) (directive: RealmDirective option) =
    let directiveText =
        directive
        |> Option.map _.NormalizedInstruction
        |> Option.defaultValue "Expand the realm with a practical, flavorful room that fits the surrounding area."
    let roomStem =
        if builder.RealmSlug = "sci-fi" then "relay annex" else "market nook"
    let roomName =
        if builder.RealmSlug = "sci-fi" then $"Relay Annex of {anchor.RoomName}"
        else $"Market Nook off {anchor.RoomName}"
    let roomSlug = slugify $"{builder.Slug}-{anchor.RoomSlug}-{roomStem}-{DateTime.UtcNow:MMdd}"
    let resourceName =
        if builder.RealmSlug = "sci-fi" then "Spare Conduit Coil" else "Fruit Crate"
    let resourceSlug = slugify resourceName
    let loreFigureName =
        if builder.RealmSlug = "sci-fi" then "Dock Relay Steward" else "Market Steward"
    let loreFigureSlug = slugify loreFigureName
    { RoomName = roomName
      RoomSlug = roomSlug
      RoomDescription = $"Built under the direction '{directiveText}', this new chamber opens from {anchor.RoomName} and reflects {builder.Specialty} work in the {builder.RealmSlug} realm."
      ResourceName = resourceName
      ResourceSlug = resourceSlug
      ResourceDescription = $"Freshly staged by {builder.DisplayName}, this resource is ready for players to gather."
      LoreFigureName = loreFigureName
      LoreFigureSlug = loreFigureSlug
      LoreFigureDescription = $"A resident figure stationed here to explain the newest work and keep the place feeling lived in."
      LoreSpeech = $"\"{directiveText}\" the figure says. \"That is the standing order, and this room is the first answer.\""
      ForwardExitLabel = $"Toward {roomName}"
      BackwardExitLabel = $"Back to {anchor.RoomName}"
      ExitType = "passage" }

let saveBuildPayload (jobId: Guid) (payload: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "UPDATE mud_build_jobs SET payload = @payload::jsonb WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", jobId) |> ignore
    cmd.Parameters.AddWithValue("payload", payload) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let failBuildJob (jobId: Guid) (error: string) (retryCount: int) =
    use conn = openConnection ()
    let nextStatus = if retryCount + 1 >= 3 then "Failed" else "Pending"
    use cmd = new NpgsqlCommand(
        """UPDATE mud_build_jobs
           SET status = @status,
               retry_count = retry_count + 1,
               error = @error,
               started_at = NULL
           WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("id", jobId) |> ignore
    cmd.Parameters.AddWithValue("status", nextStatus) |> ignore
    cmd.Parameters.AddWithValue("error", clip 1500 error) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let completeBuildJob (jobId: Guid) (anchorRoomId: Guid) (createdRoomId: Guid) (summary: string) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_build_jobs
           SET status = 'Completed',
               anchor_room_id = @anchor_room_id,
               created_room_id = @created_room_id,
               result_summary = @summary,
               completed_at = now(),
               error = NULL
           WHERE id = @id""", conn)
    cmd.Parameters.AddWithValue("id", jobId) |> ignore
    cmd.Parameters.AddWithValue("anchor_room_id", anchorRoomId) |> ignore
    cmd.Parameters.AddWithValue("created_room_id", createdRoomId) |> ignore
    cmd.Parameters.AddWithValue("summary", clip 1000 summary) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let private insertBuildAnnouncement (conn: NpgsqlConnection) (senderName: string) (body: string) =
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_chat_messages (channel, sender_name, body)
           VALUES ('announce', @sender_name, @body)""", conn)
    cmd.Parameters.AddWithValue("sender_name", senderName) |> ignore
    cmd.Parameters.AddWithValue("body", body) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let applyBuildPlan (job: BuildJob) (builder: BuilderAgent) (anchor: AnchorRoom) (plan: BuildPlan) =
    use conn = openConnection ()
    use txn = conn.BeginTransaction()

    let roomSlug =
        match nonBlank plan.RoomSlug with
        | Some slug -> slugify slug
        | None -> slugify $"{builder.Slug}-{plan.RoomName}"

    let resourceSlug =
        match nonBlank plan.ResourceSlug with
        | Some slug -> slugify slug
        | None -> slugify plan.ResourceName

    let loreSlug =
        match nonBlank plan.LoreFigureSlug with
        | Some slug -> slugify slug
        | None -> slugify plan.LoreFigureName

    let position =
        use positionCmd = new NpgsqlCommand(
            "SELECT COALESCE(MAX(position), -1) + 1 FROM mud_rooms WHERE zone_id = @zone_id", conn, txn)
        positionCmd.Parameters.AddWithValue("zone_id", anchor.ZoneId) |> ignore
        Convert.ToInt32(positionCmd.ExecuteScalar())

    use roomCmd = new NpgsqlCommand(
        """INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
           VALUES (@zone_id, @name, @slug, @description, @position, @map_x, @map_y)
           RETURNING id""", conn, txn)
    roomCmd.Parameters.AddWithValue("zone_id", anchor.ZoneId) |> ignore
    roomCmd.Parameters.AddWithValue("name", clip 120 plan.RoomName) |> ignore
    roomCmd.Parameters.AddWithValue("slug", roomSlug) |> ignore
    roomCmd.Parameters.AddWithValue("description", clip 1500 plan.RoomDescription) |> ignore
    roomCmd.Parameters.AddWithValue("position", position) |> ignore
    roomCmd.Parameters.AddWithValue("map_x", anchor.NewMapX |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    roomCmd.Parameters.AddWithValue("map_y", anchor.NewMapY |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    let createdRoomId = roomCmd.ExecuteScalar() :?> Guid

    let exitType =
        match nonBlank plan.ExitType with
        | Some value -> clip 40 value |> fun v -> v.ToLowerInvariant()
        | None -> "passage"

    let createExit fromRoomId toRoomId direction label =
        use exitCmd = new NpgsqlCommand(
            """INSERT INTO mud_exits (from_room_id, to_room_id, direction, exit_type, label)
               VALUES (@from_room_id, @to_room_id, @direction, @exit_type, @label)""", conn, txn)
        exitCmd.Parameters.AddWithValue("from_room_id", fromRoomId) |> ignore
        exitCmd.Parameters.AddWithValue("to_room_id", toRoomId) |> ignore
        exitCmd.Parameters.AddWithValue("direction", direction) |> ignore
        exitCmd.Parameters.AddWithValue("exit_type", exitType) |> ignore
        exitCmd.Parameters.AddWithValue("label", label |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
        exitCmd.ExecuteNonQuery() |> ignore

    createExit anchor.RoomId createdRoomId anchor.Direction (nonBlank plan.ForwardExitLabel)
    createExit createdRoomId anchor.RoomId anchor.ReverseDirection (nonBlank plan.BackwardExitLabel)

    let insertItem roomId name slug description readable portable positionValue =
        use itemCmd = new NpgsqlCommand(
            """INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
               VALUES (@room_id, @name, @slug, @description, @readable_text, @portable, @position)""", conn, txn)
        itemCmd.Parameters.AddWithValue("room_id", roomId) |> ignore
        itemCmd.Parameters.AddWithValue("name", clip 120 name) |> ignore
        itemCmd.Parameters.AddWithValue("slug", slug) |> ignore
        itemCmd.Parameters.AddWithValue("description", clip 1000 description) |> ignore
        itemCmd.Parameters.AddWithValue("readable_text", readable |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
        itemCmd.Parameters.AddWithValue("portable", portable) |> ignore
        itemCmd.Parameters.AddWithValue("position", positionValue) |> ignore
        itemCmd.ExecuteNonQuery() |> ignore

    insertItem createdRoomId plan.ResourceName resourceSlug plan.ResourceDescription None true 0
    insertItem createdRoomId plan.LoreFigureName loreSlug plan.LoreFigureDescription (nonBlank plan.LoreSpeech) false 1

    let summary = $"{builder.DisplayName} built {plan.RoomName} off {anchor.RoomName}."
    insertBuildAnnouncement conn builder.DisplayName $"{summary} Players can explore it now."

    use jobCmd = new NpgsqlCommand(
        """UPDATE mud_build_jobs
           SET anchor_room_id = @anchor_room_id,
               created_room_id = @created_room_id,
               result_summary = @summary,
               status = 'Completed',
               completed_at = now(),
               error = NULL
           WHERE id = @id""", conn, txn)
    jobCmd.Parameters.AddWithValue("id", job.Id) |> ignore
    jobCmd.Parameters.AddWithValue("anchor_room_id", anchor.RoomId) |> ignore
    jobCmd.Parameters.AddWithValue("created_room_id", createdRoomId) |> ignore
    jobCmd.Parameters.AddWithValue("summary", summary) |> ignore
    jobCmd.ExecuteNonQuery() |> ignore

    txn.Commit()
    createdRoomId, summary

let parseBuildPlan (rawResponse: string) =
    let start = rawResponse.IndexOf('{')
    let stop = rawResponse.LastIndexOf('}')
    if start < 0 || stop <= start then
        Error "No JSON object found in build response."
    else
        try
            let slice = rawResponse.[start..stop]
            let parsed = JsonSerializer.Deserialize<BuildPlan>(slice, serializerOptions)
            if String.IsNullOrWhiteSpace parsed.RoomName || String.IsNullOrWhiteSpace parsed.RoomDescription then
                Error "Build response was missing room content."
            else
                Ok parsed
        with ex ->
            Error ex.Message
