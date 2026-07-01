module Djehuti.Api.MudRepository

open System
open System.Data.Common
open System.Text.Json
open Npgsql
open Database

type MudExitView =
    { Direction: string
      Label: string option
      TargetRoomId: Guid
      TargetRoomName: string }

type MudItemView =
    { Name: string
      Slug: string
      Description: string option
      Portable: bool
      Readable: bool }

type MudRoomState =
    { CharacterId: Guid
      CharacterName: string
      RoomId: Guid
      RoomName: string
      RoomDescription: string option
      ZoneName: string
      MudTierName: string
      VisibleItems: MudItemView list
      InventoryItems: MudItemView list
      Exits: MudExitView list }

type MudCommandResult =
    { Success: bool
      Command: string
      Message: string
      State: MudRoomState option }

type private MudCharacterRow =
    { Id: Guid
      UserId: Guid
      DisplayName: string
      CurrentRoomId: Guid
      CreatedAt: DateTime
      UpdatedAt: DateTime }

let private readCharacter (r: DbDataReader) =
    { Id = r.GetGuid(0)
      UserId = r.GetGuid(1)
      DisplayName = r.GetString(2)
      CurrentRoomId = r.GetGuid(3)
      CreatedAt = r.GetFieldValue<DateTime>(4)
      UpdatedAt = r.GetFieldValue<DateTime>(5) }

let private readStateBase (r: DbDataReader) =
    { CharacterId = r.GetGuid(0)
      CharacterName = r.GetString(1)
      RoomId = r.GetGuid(2)
      RoomName = r.GetString(3)
      RoomDescription = if r.IsDBNull(4) then None else Some (r.GetString(4))
      ZoneName = r.GetString(5)
      MudTierName = "Wanderer"
      VisibleItems = []
      InventoryItems = []
      Exits = [] }

let private readItemView (r: DbDataReader) =
    { Name = r.GetString(0)
      Slug = r.GetString(1)
      Description = if r.IsDBNull(2) then None else Some (r.GetString(2))
      Portable = r.GetBoolean(3)
      Readable = not (r.IsDBNull(4)) }

let private nonBlank (value: string option) =
    value |> Option.bind (fun s -> if String.IsNullOrWhiteSpace(s) then None else Some s)

let private payloadOf (pairs: (string * string) list) =
    let rendered =
        pairs
        |> List.map (fun (key, value) -> $"\"{key}\":{JsonSerializer.Serialize(value)}")
        |> String.concat ","
    "{" + rendered + "}"

let private starterRoomId () =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("SELECT id FROM mud_rooms WHERE slug = 'atrium' LIMIT 1", conn)
    let scalar = cmd.ExecuteScalar()
    if isNull scalar || scalar = box DBNull.Value then None else Some (scalar :?> Guid)

let private loadMudTierName (userId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT COALESCE(mtl.mud_name, pt.tier_name, 'Wanderer')
           FROM users u
           LEFT JOIN patreon_tiers pt ON pt.tier_id = u.patreon_tier_id
           LEFT JOIN mud_tier_labels mtl ON mtl.patreon_tier_id = pt.tier_id
           WHERE u.id = @uid""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    let scalar = cmd.ExecuteScalar()
    if isNull scalar || scalar = box DBNull.Value then "Wanderer" else scalar :?> string

let private loadState (conn: NpgsqlConnection) (userId: Guid) : MudRoomState option =
    use cmd = new NpgsqlCommand(
        """SELECT c.id,
                  c.display_name,
                  r.id,
                  r.name,
                  r.description,
                  z.name
           FROM mud_characters c
           JOIN mud_rooms r ON r.id = c.current_room_id
           JOIN mud_zones z ON z.id = r.zone_id
           WHERE c.user_id = @uid""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if not (reader.Read()) then
        None
    else
        let baseState = readStateBase reader
        reader.Close()
        use exitCmd = new NpgsqlCommand(
            """SELECT e.direction, e.label, e.to_room_id, r.name
               FROM mud_exits e
               JOIN mud_rooms r ON r.id = e.to_room_id
               WHERE e.from_room_id = @room_id
               ORDER BY e.direction""", conn)
        exitCmd.Parameters.AddWithValue("room_id", baseState.RoomId) |> ignore
        use exitReader = exitCmd.ExecuteReader()
        let exits =
            [ while exitReader.Read() do
                yield { Direction = exitReader.GetString(0)
                        Label = if exitReader.IsDBNull(1) then None else Some (exitReader.GetString(1))
                        TargetRoomId = exitReader.GetGuid(2)
                        TargetRoomName = exitReader.GetString(3) } ]
        exitReader.Close()
        use roomItemsCmd = new NpgsqlCommand(
            """SELECT name, slug, description, portable, readable_text
               FROM mud_items
               WHERE room_id = @room_id
                 AND owner_character_id IS NULL
               ORDER BY position, name""", conn)
        roomItemsCmd.Parameters.AddWithValue("room_id", baseState.RoomId) |> ignore
        use roomItemsReader = roomItemsCmd.ExecuteReader()
        let visibleItems =
            [ while roomItemsReader.Read() do
                yield readItemView roomItemsReader ]
        roomItemsReader.Close()
        use invCmd = new NpgsqlCommand(
            """SELECT name, slug, description, portable, readable_text
               FROM mud_items
               WHERE owner_character_id = @character_id
               ORDER BY position, name""", conn)
        invCmd.Parameters.AddWithValue("character_id", baseState.CharacterId) |> ignore
        use invReader = invCmd.ExecuteReader()
        let inventoryItems =
            [ while invReader.Read() do
                yield readItemView invReader ]
        let mudTierName = loadMudTierName userId
        Some { baseState with Exits = exits; VisibleItems = visibleItems; InventoryItems = inventoryItems; MudTierName = mudTierName }

let private loadCharacter (conn: NpgsqlConnection) (userId: Guid) : MudCharacterRow option =
    use cmd = new NpgsqlCommand(
        """SELECT id, user_id, display_name, current_room_id, created_at, updated_at
           FROM mud_characters
           WHERE user_id = @uid""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readCharacter reader) else None

let private ensureCharacter (conn: NpgsqlConnection) (userId: Guid) (displayName: string option) : MudCharacterRow option =
    match loadCharacter conn userId with
    | Some character -> Some character
    | None ->
        match starterRoomId () with
        | None -> None
        | Some roomId ->
            let name =
                displayName
                |> nonBlank
                |> Option.defaultValue "Adventurer"
            use insertCmd = new NpgsqlCommand(
                """INSERT INTO mud_characters (user_id, display_name, current_room_id)
                   VALUES (@uid, @display_name, @room_id)
                   ON CONFLICT (user_id) DO UPDATE
                     SET updated_at = now()
                   RETURNING id, user_id, display_name, current_room_id, created_at, updated_at""", conn)
            insertCmd.Parameters.AddWithValue("uid", userId) |> ignore
            insertCmd.Parameters.AddWithValue("display_name", name) |> ignore
            insertCmd.Parameters.AddWithValue("room_id", roomId) |> ignore
            use insertReader = insertCmd.ExecuteReader()
            if insertReader.Read() then Some (readCharacter insertReader) else None

let private logEvent
    (conn: NpgsqlConnection)
    (actorUserId: Guid)
    (actorCharacterId: Guid)
    (roomId: Guid)
    (eventType: string)
    (command: string option)
    (message: string)
    (payload: string) =
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_events (actor_type, actor_user_id, actor_character_id, room_id, event_type, command, message, payload)
           VALUES ('user', @actor_user_id, @actor_character_id, @room_id, @event_type, @command, @message, @payload::jsonb)""", conn)
    cmd.Parameters.AddWithValue("actor_user_id", actorUserId) |> ignore
    cmd.Parameters.AddWithValue("actor_character_id", actorCharacterId) |> ignore
    cmd.Parameters.AddWithValue("room_id", roomId) |> ignore
    cmd.Parameters.AddWithValue("event_type", eventType) |> ignore
    cmd.Parameters.AddWithValue("command", command |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("message", message) |> ignore
    cmd.Parameters.AddWithValue("payload", payload) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let private describeState (state: MudRoomState) =
    let exitsText =
        match state.Exits with
        | [] -> "No exits are visible."
        | exits ->
            exits
            |> List.map (fun e ->
                match e.Label with
                | Some label when not (String.IsNullOrWhiteSpace label) -> $"{e.Direction} ({label})"
                | _ -> e.Direction)
            |> String.concat ", "
            |> fun s -> $"Exits: {s}"

    let description =
        state.RoomDescription
        |> Option.defaultValue "The room has no description yet."

    let itemsText =
        match state.VisibleItems with
        | [] -> "Visible items: none."
        | items ->
            items
            |> List.map _.Name
            |> String.concat ", "
            |> fun s -> $"Visible items: {s}"

    $"{state.RoomName}\n\n{description}\n\n{exitsText}\n{itemsText}"

let private tryFindItem (items: MudItemView list) (query: string) =
    let normalized = query.Trim().ToLowerInvariant()
    items
    |> List.tryFind (fun item ->
        item.Name.ToLowerInvariant() = normalized
        || item.Slug.ToLowerInvariant() = normalized
        || item.Name.ToLowerInvariant().Contains(normalized))

let private describeItem (item: MudItemView) =
    let body = item.Description |> Option.defaultValue "It has no description yet."
    let portability = if item.Portable then "It looks portable." else "It looks fixed in place."
    let readable = if item.Readable then "It can be read." else "There is nothing readable on it."
    $"{item.Name}\n\n{body}\n\n{portability} {readable}"

let private loadReadableText (conn: NpgsqlConnection) (state: MudRoomState) (query: string) =
    use cmd = new NpgsqlCommand(
        """SELECT readable_text
           FROM mud_items
           WHERE readable_text IS NOT NULL
             AND (
                 room_id = @room_id
                 OR owner_character_id = @character_id
             )
             AND (
                 lower(name) = lower(@query)
                 OR lower(slug) = lower(@query)
             )
           LIMIT 1""", conn)
    cmd.Parameters.AddWithValue("room_id", state.RoomId) |> ignore
    cmd.Parameters.AddWithValue("character_id", state.CharacterId) |> ignore
    cmd.Parameters.AddWithValue("query", query.Trim()) |> ignore
    let scalar = cmd.ExecuteScalar()
    if isNull scalar || scalar = box DBNull.Value then None else Some (scalar :?> string)

let private withState (userId: Guid) (displayName: string option) (action: MudRoomState -> MudCommandResult) : MudCommandResult =
    use conn = openConnection ()
    match ensureCharacter conn userId displayName with
    | None ->
        { Success = false
          Command = ""
          Message = "The MUD world is not ready yet."
          State = None }
    | Some character ->
        match loadState conn userId with
        | None ->
            { Success = false
              Command = ""
              Message = "You are not placed in a room yet."
              State = None }
        | Some state ->
            action { state with CharacterId = character.Id; CharacterName = character.DisplayName }

let getState (userId: Guid) : MudRoomState option =
    use conn = openConnection ()
    loadState conn userId

let look (userId: Guid) (displayName: string option) : MudCommandResult =
    withState userId displayName (fun state ->
        let message = describeState state
        use conn = openConnection ()
        logEvent conn userId state.CharacterId state.RoomId "look" (Some "look") message (payloadOf [ "action", "look" ])
        { Success = true
          Command = "look"
          Message = message
          State = Some state })

let private findExit (conn: NpgsqlConnection) (roomId: Guid) (direction: string) =
    use cmd = new NpgsqlCommand(
        """SELECT e.direction, e.label, e.to_room_id, r.name
           FROM mud_exits e
           JOIN mud_rooms r ON r.id = e.to_room_id
           WHERE e.from_room_id = @room_id
             AND lower(e.direction) = lower(@direction)
           LIMIT 1""", conn)
    cmd.Parameters.AddWithValue("room_id", roomId) |> ignore
    cmd.Parameters.AddWithValue("direction", direction) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some { Direction = reader.GetString(0)
               Label = if reader.IsDBNull(1) then None else Some (reader.GetString(1))
               TargetRoomId = reader.GetGuid(2)
               TargetRoomName = reader.GetString(3) }
    else
        None

let private moveInternal (userId: Guid) (direction: string) (displayName: string option) : MudCommandResult =
    use conn = openConnection ()
    match ensureCharacter conn userId displayName with
    | None ->
        { Success = false
          Command = $"move {direction}"
          Message = "The MUD world is not ready yet."
          State = None }
    | Some character ->
        match loadState conn userId with
        | None ->
            { Success = false
              Command = $"move {direction}"
              Message = "You are not placed in a room yet."
              State = None }
        | Some state ->
            match findExit conn state.RoomId direction with
            | None ->
                logEvent conn userId character.Id state.RoomId "move-failed" (Some direction) $"No exit in direction '{direction}'." (payloadOf [ "direction", direction ])
                { Success = false
                  Command = $"move {direction}"
                  Message = $"There is no exit to the {direction}."
                  State = Some state }
            | Some exitView ->
                use moveCmd = new NpgsqlCommand(
                    """UPDATE mud_characters
                       SET current_room_id = @room_id,
                           updated_at = now()
                       WHERE id = @id""", conn)
                moveCmd.Parameters.AddWithValue("room_id", exitView.TargetRoomId) |> ignore
                moveCmd.Parameters.AddWithValue("id", character.Id) |> ignore
                moveCmd.ExecuteNonQuery() |> ignore
                let nextState =
                    match loadState conn userId with
                    | Some s -> s
                    | None -> state
                logEvent conn userId character.Id nextState.RoomId "move" (Some direction) $"Moved to {nextState.RoomName}." (payloadOf [ "direction", direction; "to_room", exitView.TargetRoomName ])
                { Success = true
                  Command = $"move {direction}"
                  Message = $"You move {direction} to {exitView.TargetRoomName}."
                  State = Some nextState }

let move (userId: Guid) (displayName: string option) (direction: string) =
    moveInternal userId direction displayName

let examine (userId: Guid) (displayName: string option) (query: string) : MudCommandResult =
    withState userId displayName (fun state ->
        let trimmed = query.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "examine"
              Message = "Examine what?"
              State = Some state }
        else
            let lower = trimmed.ToLowerInvariant()
            let roomMatch =
                lower = "room"
                || lower = "here"
                || lower = state.RoomName.ToLowerInvariant()
            let item =
                tryFindItem state.VisibleItems trimmed
                |> Option.orElseWith (fun () -> tryFindItem state.InventoryItems trimmed)
            let exitView =
                state.Exits
                |> List.tryFind (fun exit ->
                    exit.Direction.Equals(trimmed, StringComparison.OrdinalIgnoreCase)
                    || exit.TargetRoomName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)

            let message =
                if roomMatch then
                    describeState state
                else
                    match item, exitView with
                    | Some foundItem, _ -> describeItem foundItem
                    | None, Some exit -> $"Exit {exit.Direction}\n\nIt leads toward {exit.TargetRoomName}."
                    | None, None when lower = "self" || lower = "me" ->
                        let inventoryCount = state.InventoryItems.Length
                        $"{state.CharacterName}\n\nRank: {state.MudTierName}\nInventory items: {inventoryCount}"
                    | None, None -> $"You do not see '{trimmed}' here."

            use conn = openConnection ()
            logEvent conn userId state.CharacterId state.RoomId "examine" (Some trimmed) message (payloadOf [ "target", trimmed ])
            { Success = item.IsSome || exitView.IsSome || roomMatch || lower = "self" || lower = "me"
              Command = $"examine {trimmed}"
              Message = message
              State = Some state })

let read (userId: Guid) (displayName: string option) (query: string) : MudCommandResult =
    withState userId displayName (fun state ->
        let trimmed = query.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "read"
              Message = "Read what?"
              State = Some state }
        else
            use conn = openConnection ()
            let message =
                match loadReadableText conn state trimmed with
                | Some text -> text
                | None -> $"There is nothing readable on '{trimmed}'."
            logEvent conn userId state.CharacterId state.RoomId "read" (Some trimmed) message (payloadOf [ "target", trimmed ])
            { Success = message <> $"There is nothing readable on '{trimmed}'."
              Command = $"read {trimmed}"
              Message = message
              State = Some state })

let inventory (userId: Guid) (displayName: string option) : MudCommandResult =
    withState userId displayName (fun state ->
        let message =
            match state.InventoryItems with
            | [] -> "You are carrying nothing."
            | items ->
                items
                |> List.map _.Name
                |> String.concat ", "
                |> fun s -> $"You are carrying: {s}"
        use conn = openConnection ()
        logEvent conn userId state.CharacterId state.RoomId "inventory" (Some "inventory") message (payloadOf [ "count", string state.InventoryItems.Length ])
        { Success = true
          Command = "inventory"
          Message = message
          State = Some state })

let say (userId: Guid) (displayName: string option) (text: string) : MudCommandResult =
    withState userId displayName (fun state ->
        let trimmed = text.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "say"
              Message = "Say what?"
              State = Some state }
        else
            use conn = openConnection ()
            logEvent conn userId state.CharacterId state.RoomId "say" (Some trimmed) $"{state.CharacterName} says: {trimmed}" (payloadOf [ "text", trimmed ])
            { Success = true
              Command = "say"
              Message = $"{state.CharacterName} says: {trimmed}"
              State = Some state })

let emote (userId: Guid) (displayName: string option) (text: string) : MudCommandResult =
    withState userId displayName (fun state ->
        let trimmed = text.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            { Success = false
              Command = "emote"
              Message = "Emote what?"
              State = Some state }
        else
            let message = $"{state.CharacterName} {trimmed}"
            use conn = openConnection ()
            logEvent conn userId state.CharacterId state.RoomId "emote" (Some trimmed) message (payloadOf [ "text", trimmed ])
            { Success = true
              Command = "emote"
              Message = message
              State = Some state })

let handleCommand (userId: Guid) (displayName: string option) (commandText: string) : MudCommandResult =
    let trimmed = if isNull commandText then "" else commandText.Trim()
    if String.IsNullOrWhiteSpace(trimmed) then
        { Success = false
          Command = ""
          Message = "Type look, examine <thing>, read <thing>, inventory, move <direction>, say <message>, or emote <action>."
          State = getState userId }
    else
        let parts = trimmed.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
        let verb = parts.[0].ToLowerInvariant()
        match verb with
        | "look" | "l" -> look userId displayName
        | "examine" | "exam" | "x" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            examine userId displayName query
        | "read" ->
            let query = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            read userId displayName query
        | "inventory" | "inv" | "i" -> inventory userId displayName
        | "say" ->
            let message = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            say userId displayName message
        | "emote" | "me" ->
            let message = if parts.Length > 1 then String.Join(" ", parts.[1..]) else ""
            emote userId displayName message
        | "move" | "go" ->
            let direction = if parts.Length > 1 then parts.[1] else ""
            if String.IsNullOrWhiteSpace(direction) then
                { Success = false
                  Command = trimmed
                  Message = "Move where?"
                  State = getState userId }
            else
                move userId displayName direction
        | "north" | "south" | "east" | "west" | "up" | "down" | "in" | "out" ->
            move userId displayName verb
        | _ ->
            { Success = false
              Command = trimmed
              Message = "Unknown command. Try look, examine <thing>, read <thing>, inventory, move <direction>, say <message>, or emote <action>."
              State = getState userId }
