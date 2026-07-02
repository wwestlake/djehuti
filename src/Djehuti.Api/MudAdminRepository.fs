module Djehuti.Api.MudAdminRepository

open System
open System.Data.Common
open System.Text.RegularExpressions
open Npgsql
open Database

type MudZone =
    { Id: Guid
      Name: string
      Slug: string
      Description: string option
      Position: int
      CreatedAt: DateTime }

type MudRoom =
    { Id: Guid
      ZoneId: Guid
      ZoneName: string
      ZoneSlug: string
      Name: string
      Slug: string
      Description: string option
      Position: int
      CreatedAt: DateTime }

type MudExit =
    { Id: Guid
      FromRoomId: Guid
      FromRoomName: string
      FromRoomSlug: string
      ToRoomId: Guid
      ToRoomName: string
      ToRoomSlug: string
      Direction: string
      ExitType: string
      Label: string option
      CreatedAt: DateTime }

type MudWorld =
    { Zones: MudZone list
      Rooms: MudRoom list
      Exits: MudExit list }

let private readZone (r: DbDataReader) =
    { Id = r.GetGuid(0)
      Name = r.GetString(1)
      Slug = r.GetString(2)
      Description = if r.IsDBNull(3) then None else Some (r.GetString(3))
      Position = r.GetInt32(4)
      CreatedAt = r.GetFieldValue<DateTime>(5) }

let private readRoom (r: DbDataReader) =
    { Id = r.GetGuid(0)
      ZoneId = r.GetGuid(1)
      ZoneName = r.GetString(2)
      ZoneSlug = r.GetString(3)
      Name = r.GetString(4)
      Slug = r.GetString(5)
      Description = if r.IsDBNull(6) then None else Some (r.GetString(6))
      Position = r.GetInt32(7)
      CreatedAt = r.GetFieldValue<DateTime>(8) }

let private readExit (r: DbDataReader) =
    { Id = r.GetGuid(0)
      FromRoomId = r.GetGuid(1)
      FromRoomName = r.GetString(2)
      FromRoomSlug = r.GetString(3)
      ToRoomId = r.GetGuid(4)
      ToRoomName = r.GetString(5)
      ToRoomSlug = r.GetString(6)
      Direction = r.GetString(7)
      ExitType = r.GetString(8)
      Label = if r.IsDBNull(9) then None else Some (r.GetString(9))
      CreatedAt = r.GetFieldValue<DateTime>(10) }

let private slugify (value: string) =
    let normalized =
        if isNull value then ""
        else value.Trim().ToLowerInvariant()
    let replaced =
        Regex.Replace(normalized, "[^a-z0-9]+", "-")
    replaced.Trim('-')

let private cleanSlug (fallback: string) (value: string) =
    let slug = slugify value
    if String.IsNullOrWhiteSpace slug then slugify fallback else slug

let private nonBlank (value: string) =
    if String.IsNullOrWhiteSpace value then None else Some (value.Trim())

let getWorld () =
    use conn = openConnection ()
    use zonesCmd = new NpgsqlCommand("SELECT id, name, slug, description, position, created_at FROM mud_zones ORDER BY position, name", conn)
    use zonesReader = zonesCmd.ExecuteReader()
    let zones =
        [ while zonesReader.Read() do
            yield readZone zonesReader ]
    zonesReader.Close()

    use roomsCmd = new NpgsqlCommand(
        """SELECT r.id, r.zone_id, z.name, z.slug, r.name, r.slug, r.description, r.position, r.created_at
           FROM mud_rooms r
           JOIN mud_zones z ON z.id = r.zone_id
           ORDER BY z.position, r.position, r.name""", conn)
    use roomsReader = roomsCmd.ExecuteReader()
    let rooms =
        [ while roomsReader.Read() do
            yield readRoom roomsReader ]
    roomsReader.Close()

    use exitsCmd = new NpgsqlCommand(
        """SELECT e.id,
                  e.from_room_id,
                  rf.name,
                  rf.slug,
                  e.to_room_id,
                  rt.name,
                  rt.slug,
                  e.direction,
                  e.exit_type,
                  e.label,
                  e.created_at
           FROM mud_exits e
           JOIN mud_rooms rf ON rf.id = e.from_room_id
           JOIN mud_rooms rt ON rt.id = e.to_room_id
           ORDER BY rf.name, e.direction""", conn)
    use exitsReader = exitsCmd.ExecuteReader()
    let exits =
        [ while exitsReader.Read() do
            yield readExit exitsReader ]

    { Zones = zones; Rooms = rooms; Exits = exits }

let createZone (name: string) (slug: string) (description: string option) (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_zones (name, slug, description, position)
           VALUES (@name, @slug, @description, @position)
           RETURNING id, name, slug, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readZone reader) else None
    with _ ->
        None

let updateZone (zoneId: Guid) (name: string) (slug: string) (description: string option) (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_zones
           SET name = @name,
               slug = @slug,
               description = @description,
               position = @position
           WHERE id = @id
           RETURNING id, name, slug, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("id", zoneId) |> ignore
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readZone reader) else None
    with _ ->
        None

let createRoom (zoneId: Guid) (name: string) (slug: string) (description: string option) (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_rooms (zone_id, name, slug, description, position)
           VALUES (@zone_id, @name, @slug, @description, @position)
           RETURNING id, zone_id, (SELECT name FROM mud_zones WHERE id = @zone_id), (SELECT slug FROM mud_zones WHERE id = @zone_id), name, slug, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("zone_id", zoneId) |> ignore
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readRoom reader) else None
    with _ ->
        None

let updateRoom (roomId: Guid) (zoneId: Guid) (name: string) (slug: string) (description: string option) (position: int) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """UPDATE mud_rooms
           SET zone_id = @zone_id,
               name = @name,
               slug = @slug,
               description = @description,
               position = @position
           WHERE id = @id
           RETURNING id, zone_id,
                     (SELECT name FROM mud_zones WHERE id = @zone_id),
                     (SELECT slug FROM mud_zones WHERE id = @zone_id),
                     name, slug, description, position, created_at""", conn)
    cmd.Parameters.AddWithValue("id", roomId) |> ignore
    cmd.Parameters.AddWithValue("zone_id", zoneId) |> ignore
    cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
    cmd.Parameters.AddWithValue("slug", cleanSlug name slug) |> ignore
    cmd.Parameters.AddWithValue("description", description |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("position", position) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readRoom reader) else None
    with _ ->
        None

let createExit (fromRoomId: Guid) (toRoomId: Guid) (direction: string) (exitType: string) (label: string option) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        """INSERT INTO mud_exits (from_room_id, to_room_id, direction, exit_type, label)
           VALUES (@from_room_id, @to_room_id, @direction, @exit_type, @label)
           RETURNING id, from_room_id,
                     (SELECT name FROM mud_rooms WHERE id = @from_room_id),
                     (SELECT slug FROM mud_rooms WHERE id = @from_room_id),
                     to_room_id,
                     (SELECT name FROM mud_rooms WHERE id = @to_room_id),
                     (SELECT slug FROM mud_rooms WHERE id = @to_room_id),
                     direction, exit_type, label, created_at""", conn)
    cmd.Parameters.AddWithValue("from_room_id", fromRoomId) |> ignore
    cmd.Parameters.AddWithValue("to_room_id", toRoomId) |> ignore
    cmd.Parameters.AddWithValue("direction", direction.Trim().ToLowerInvariant()) |> ignore
    cmd.Parameters.AddWithValue("exit_type", exitType.Trim().ToLowerInvariant()) |> ignore
    cmd.Parameters.AddWithValue("label", label |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    try
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (readExit reader) else None
    with _ ->
        None

let deleteExit (exitId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM mud_exits WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", exitId) |> ignore
    cmd.ExecuteNonQuery() > 0
