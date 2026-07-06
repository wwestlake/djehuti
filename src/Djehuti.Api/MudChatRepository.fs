module Djehuti.Api.MudChatRepository

open System
open Npgsql
open Database

type MudChatMessageView =
    { Id: Guid
      Channel: string
      SenderName: string
      RecipientName: string option
      RoomName: string option
      Body: string
      CreatedAt: DateTime
      Self: bool }

type MudChatSyncView =
    { Messages: MudChatMessageView list
      Here: string list
      OnlineCount: int
      PartyName: string option
      ServerTime: DateTime }

type ActiveChatCharacter =
    { CharacterId: Guid
      Name: string
      RoomId: Guid
      RealmSlug: string }

let presenceMinutes = 5.0

let tryGetActiveCharacter (userId: Guid) : ActiveChatCharacter option =
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
              Name = reader.GetString(1)
              RoomId = reader.GetGuid(2)
              RealmSlug = reader.GetString(3) }
    else
        None

let touchPresence (userId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_characters c
           SET last_active_at = now()
           FROM users u
           WHERE u.id = @user_id AND c.id = u.active_mud_character_id""", conn)
    cmd.Parameters.AddWithValue("user_id", userId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let private insertMessage
    (conn: NpgsqlConnection)
    (channel: string)
    (senderCharacterId: Guid option)
    (senderName: string)
    (roomId: Guid option)
    (recipientCharacterId: Guid option)
    (recipientName: string option)
    (groupId: Guid option)
    (body: string) =
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_chat_messages
               (channel, sender_character_id, sender_name, room_id, recipient_character_id, recipient_name, group_id, body)
           VALUES (@channel, @sender_id, @sender_name, @room_id, @recipient_id, @recipient_name, @group_id, @body)""", conn)
    let orNull (value: Guid option) = value |> Option.map box |> Option.defaultValue (box DBNull.Value)
    cmd.Parameters.AddWithValue("channel", channel) |> ignore
    cmd.Parameters.AddWithValue("sender_id", orNull senderCharacterId) |> ignore
    cmd.Parameters.AddWithValue("sender_name", senderName) |> ignore
    cmd.Parameters.AddWithValue("room_id", orNull roomId) |> ignore
    cmd.Parameters.AddWithValue("recipient_id", orNull recipientCharacterId) |> ignore
    cmd.Parameters.AddWithValue("recipient_name", recipientName |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("group_id", orNull groupId) |> ignore
    cmd.Parameters.AddWithValue("body", body) |> ignore
    cmd.ExecuteNonQuery() |> ignore

/// Low-level room message insert used by the say command in MudRepository.
let recordRoomMessage (characterId: Guid) (senderName: string) (roomId: Guid) (body: string) =
    use conn = openConnection ()
    insertMessage conn "room" (Some characterId) senderName (Some roomId) None None None body

let private cleanText (text: string) =
    let trimmed = if isNull text then "" else text.Trim()
    if trimmed.Length > 500 then trimmed.Substring(0, 500) else trimmed

let postRoom (userId: Guid) (text: string) : Result<string, string> =
    let body = cleanText text
    if String.IsNullOrWhiteSpace body then Error "Say what?"
    else
        match MudConstructionRepository.tryHandleDirectorMessage userId None body with
        | Some result -> result
        | None ->
            match tryGetActiveCharacter userId with
            | None -> Error "Choose or create a character first."
            | Some actor ->
                recordRoomMessage actor.CharacterId actor.Name actor.RoomId body
                Ok $"{actor.Name} says: {body}"

let postShout (userId: Guid) (text: string) : Result<string, string> =
    let body = cleanText text
    if String.IsNullOrWhiteSpace body then Error "Shout what?"
    else
        match MudConstructionRepository.tryHandleDirectorMessage userId None body with
        | Some result -> result
        | None ->
            match tryGetActiveCharacter userId with
            | None -> Error "Choose or create a character first."
            | Some actor ->
                use conn = openConnection ()
                insertMessage conn "shout" (Some actor.CharacterId) actor.Name (Some actor.RoomId) None None None body
                Ok $"{actor.Name} shouts: {body}"

let postWhisper (userId: Guid) (targetName: string) (text: string) : Result<string, string> =
    let body = cleanText text
    let target = if isNull targetName then "" else targetName.Trim()
    if String.IsNullOrWhiteSpace target then Error "Whisper to whom?"
    elif String.IsNullOrWhiteSpace body then Error "Whisper what?"
    else
        match MudConstructionRepository.tryHandleDirectorMessage userId (Some target) body with
        | Some result -> result
        | None ->
            match tryGetActiveCharacter userId with
            | None -> Error "Choose or create a character first."
            | Some actor ->
                use conn = openConnection ()
                use cmd = new NpgsqlCommand(
                    """SELECT c.id, c.display_name
                       FROM mud_characters c
                       WHERE lower(c.display_name) = lower(@name)
                         AND c.deleted_at IS NULL
                         AND c.id <> @self
                       LIMIT 2""", conn)
                cmd.Parameters.AddWithValue("name", target) |> ignore
                cmd.Parameters.AddWithValue("self", actor.CharacterId) |> ignore
                let matches =
                    [ use reader = cmd.ExecuteReader()
                      while reader.Read() do
                          yield reader.GetGuid(0), reader.GetString(1) ]
                match matches with
                | [] -> Error $"No character named '{target}' was found."
                | [ (recipientId, recipientName) ] ->
                    insertMessage conn "whisper" (Some actor.CharacterId) actor.Name None (Some recipientId) (Some recipientName) None body
                    Ok $"You whisper to {recipientName}: {body}"
                | _ -> Error $"More than one character answers to '{target}'. Be more specific."

let tryGetParty (characterId: Guid) : (Guid * string) option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """SELECT g.id, g.name
           FROM mud_group_members gm
           JOIN mud_groups g ON g.id = gm.group_id
           WHERE gm.character_id = @character_id
           LIMIT 1""", conn)
    cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (reader.GetGuid(0), reader.GetString(1)) else None

let private removeMembership (conn: NpgsqlConnection) (characterId: Guid) =
    use cmd = new NpgsqlCommand(
        "DELETE FROM mud_group_members WHERE character_id = @character_id", conn)
    cmd.Parameters.AddWithValue("character_id", characterId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let private pruneEmptyGroups (conn: NpgsqlConnection) =
    use cmd = new NpgsqlCommand(
        """DELETE FROM mud_groups g
           WHERE NOT EXISTS (SELECT 1 FROM mud_group_members gm WHERE gm.group_id = g.id)""", conn)
    cmd.ExecuteNonQuery() |> ignore

let partyCreate (userId: Guid) (name: string) : Result<string, string> =
    let partyName = cleanText name
    if String.IsNullOrWhiteSpace partyName then Error "Name the party. Try: party create <name>."
    elif partyName.Length > 60 then Error "That party name is too long."
    else
        match tryGetActiveCharacter userId with
        | None -> Error "Choose or create a character first."
        | Some actor ->
            use conn = openConnection ()
            removeMembership conn actor.CharacterId
            use create = new NpgsqlCommand(
                """INSERT INTO mud_groups (name, owner_character_id)
                   VALUES (@name, @owner) RETURNING id""", conn)
            create.Parameters.AddWithValue("name", partyName) |> ignore
            create.Parameters.AddWithValue("owner", actor.CharacterId) |> ignore
            let groupId = create.ExecuteScalar() :?> Guid
            use join = new NpgsqlCommand(
                "INSERT INTO mud_group_members (group_id, character_id) VALUES (@group_id, @character_id)", conn)
            join.Parameters.AddWithValue("group_id", groupId) |> ignore
            join.Parameters.AddWithValue("character_id", actor.CharacterId) |> ignore
            join.ExecuteNonQuery() |> ignore
            pruneEmptyGroups conn
            Ok $"Party '{partyName}' formed. Invite others with: party invite <character>."

let partyInvite (userId: Guid) (targetName: string) : Result<string, string> =
    let target = if isNull targetName then "" else targetName.Trim()
    if String.IsNullOrWhiteSpace target then Error "Invite whom? Try: party invite <character>."
    else
        match tryGetActiveCharacter userId with
        | None -> Error "Choose or create a character first."
        | Some actor ->
            match tryGetParty actor.CharacterId with
            | None -> Error "You are not in a party. Create one with: party create <name>."
            | Some (groupId, groupName) ->
                use conn = openConnection ()
                use find = new NpgsqlCommand(
                    """SELECT c.id, c.display_name
                       FROM mud_characters c
                       WHERE lower(c.display_name) = lower(@name)
                         AND c.deleted_at IS NULL
                         AND c.id <> @self
                       LIMIT 2""", conn)
                find.Parameters.AddWithValue("name", target) |> ignore
                find.Parameters.AddWithValue("self", actor.CharacterId) |> ignore
                let matches =
                    [ use reader = find.ExecuteReader()
                      while reader.Read() do
                          yield reader.GetGuid(0), reader.GetString(1) ]
                match matches with
                | [] -> Error $"No character named '{target}' was found."
                | [ (memberId, memberName) ] ->
                    removeMembership conn memberId
                    use join = new NpgsqlCommand(
                        """INSERT INTO mud_group_members (group_id, character_id)
                           VALUES (@group_id, @character_id)
                           ON CONFLICT DO NOTHING""", conn)
                    join.Parameters.AddWithValue("group_id", groupId) |> ignore
                    join.Parameters.AddWithValue("character_id", memberId) |> ignore
                    join.ExecuteNonQuery() |> ignore
                    pruneEmptyGroups conn
                    insertMessage conn "group" (Some actor.CharacterId) actor.Name None None None (Some groupId) $"{memberName} joined the party."
                    Ok $"{memberName} joined '{groupName}'."
                | _ -> Error $"More than one character answers to '{target}'. Be more specific."

let partyLeave (userId: Guid) : Result<string, string> =
    match tryGetActiveCharacter userId with
    | None -> Error "Choose or create a character first."
    | Some actor ->
        match tryGetParty actor.CharacterId with
        | None -> Error "You are not in a party."
        | Some (groupId, groupName) ->
            use conn = openConnection ()
            insertMessage conn "group" (Some actor.CharacterId) actor.Name None None None (Some groupId) $"{actor.Name} left the party."
            removeMembership conn actor.CharacterId
            pruneEmptyGroups conn
            Ok $"You left '{groupName}'."

let partyWho (userId: Guid) : Result<string, string> =
    match tryGetActiveCharacter userId with
    | None -> Error "Choose or create a character first."
    | Some actor ->
        match tryGetParty actor.CharacterId with
        | None -> Error "You are not in a party. Create one with: party create <name>."
        | Some (groupId, groupName) ->
            use conn = openConnection ()
            use cmd = new NpgsqlCommand(
                """SELECT c.display_name
                   FROM mud_group_members gm
                   JOIN mud_characters c ON c.id = gm.character_id
                   WHERE gm.group_id = @group_id AND c.deleted_at IS NULL
                   ORDER BY c.display_name""", conn)
            cmd.Parameters.AddWithValue("group_id", groupId) |> ignore
            let names =
                [ use reader = cmd.ExecuteReader()
                  while reader.Read() do
                      yield reader.GetString(0) ]
            Ok $"""Party '{groupName}': {String.Join(", ", names)}"""

let postGroup (userId: Guid) (text: string) : Result<string, string> =
    let body = cleanText text
    if String.IsNullOrWhiteSpace body then Error "Tell the party what?"
    else
        match tryGetActiveCharacter userId with
        | None -> Error "Choose or create a character first."
        | Some actor ->
            match tryGetParty actor.CharacterId with
            | None -> Error "You are not in a party. Create one with: party create <name>."
            | Some (groupId, _) ->
                use conn = openConnection ()
                insertMessage conn "group" (Some actor.CharacterId) actor.Name None None None (Some groupId) body
                Ok $"[party] {actor.Name}: {body}"

let postAnnounce (userId: Guid) (text: string) : Result<string, string> =
    let body = cleanText text
    if String.IsNullOrWhiteSpace body then Error "Announce what?"
    else
        let senderName =
            match tryGetActiveCharacter userId with
            | Some actor -> actor.Name
            | None -> "LagDaemon"
        use conn = openConnection ()
        insertMessage conn "announce" None senderName None None None None body
        Ok $"Announcement posted: {body}"

let sync (userId: Guid) (since: DateTime option) : MudChatSyncView option =
    match tryGetActiveCharacter userId with
    | None -> None
    | Some actor ->
        let sinceUtc =
            match since with
            | Some value when value > DateTime.UtcNow.AddDays(-2.0) -> value
            | _ -> DateTime.UtcNow.AddMinutes(-15.0)

        use conn = openConnection ()

        use cmd = new NpgsqlCommand(
            """SELECT m.id, m.channel, m.sender_name, m.recipient_name, r.name, m.body, m.created_at, m.sender_character_id
               FROM mud_chat_messages m
               LEFT JOIN mud_rooms r ON r.id = m.room_id
               WHERE m.created_at > @since
                 AND (
                     (m.channel = 'room' AND m.room_id = @room_id)
                     OR (m.channel = 'shout' AND (
                            m.room_id = @room_id
                            OR m.room_id IN (SELECT e.to_room_id FROM mud_exits e WHERE e.from_room_id = @room_id)))
                     OR (m.channel = 'whisper' AND (m.recipient_character_id = @character_id OR m.sender_character_id = @character_id))
                     OR (m.channel = 'group' AND m.group_id IN (
                            SELECT gm.group_id FROM mud_group_members gm WHERE gm.character_id = @character_id))
                     OR m.channel = 'announce'
                 )
               ORDER BY m.created_at ASC
               LIMIT 100""", conn)
        cmd.Parameters.AddWithValue("since", sinceUtc) |> ignore
        cmd.Parameters.AddWithValue("room_id", actor.RoomId) |> ignore
        cmd.Parameters.AddWithValue("character_id", actor.CharacterId) |> ignore
        let messages =
            [ use reader = cmd.ExecuteReader()
              while reader.Read() do
                  let senderCharacterId =
                      if reader.IsDBNull(7) then None else Some (reader.GetGuid(7))
                  yield
                      { Id = reader.GetGuid(0)
                        Channel = reader.GetString(1)
                        SenderName = reader.GetString(2)
                        RecipientName = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
                        RoomName = if reader.IsDBNull(4) then None else Some (reader.GetString(4))
                        Body = reader.GetString(5)
                        CreatedAt = reader.GetDateTime(6)
                        Self = senderCharacterId = Some actor.CharacterId } ]

        use here = new NpgsqlCommand(
            """SELECT c.display_name
               FROM mud_characters c
               JOIN users u ON u.active_mud_character_id = c.id
               WHERE c.current_room_id = @room_id
                 AND c.id <> @character_id
                 AND c.deleted_at IS NULL
                 AND c.last_active_at > now() - make_interval(mins => @minutes)
               ORDER BY c.display_name""", conn)
        here.Parameters.AddWithValue("room_id", actor.RoomId) |> ignore
        here.Parameters.AddWithValue("character_id", actor.CharacterId) |> ignore
        here.Parameters.AddWithValue("minutes", int presenceMinutes) |> ignore
        let hereNames =
            [ use reader = here.ExecuteReader()
              while reader.Read() do
                  yield reader.GetString(0) ]

        use online = new NpgsqlCommand(
            """SELECT count(*)::int
               FROM mud_characters c
               JOIN users u ON u.active_mud_character_id = c.id
               WHERE c.deleted_at IS NULL
                 AND c.last_active_at > now() - make_interval(mins => @minutes)""", conn)
        online.Parameters.AddWithValue("minutes", int presenceMinutes) |> ignore
        let onlineCount = online.ExecuteScalar() :?> int

        Some
            { Messages = messages
              Here = hereNames
              OnlineCount = onlineCount
              PartyName = tryGetParty actor.CharacterId |> Option.map snd
              ServerTime = DateTime.UtcNow }
