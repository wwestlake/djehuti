module Djehuti.Api.SponsorsRepository

open System
open System.Data.Common
open Npgsql
open Database

type Sponsor = {
    Id:         Guid
    Name:       string
    LogoUrl:    string option
    WebsiteUrl: string option
    Tier:       string
    Blurb:      string option
    Active:     bool
    Position:   int
    CreatedAt:  DateTime
    UpdatedAt:  DateTime
}

let private read (r: DbDataReader) : Sponsor = {
    Id         = r.GetGuid(0)
    Name       = r.GetString(1)
    LogoUrl    = if r.IsDBNull(2) then None else Some (r.GetString(2))
    WebsiteUrl = if r.IsDBNull(3) then None else Some (r.GetString(3))
    Tier       = r.GetString(4)
    Blurb      = if r.IsDBNull(5) then None else Some (r.GetString(5))
    Active     = r.GetBoolean(6)
    Position   = r.GetInt32(7)
    CreatedAt  = r.GetFieldValue<DateTime>(8)
    UpdatedAt  = r.GetFieldValue<DateTime>(9)
}

let private cols = "id, name, logo_url, website_url, tier, blurb, active, position, created_at, updated_at"

let getActiveSponsors () : Sponsor list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"SELECT {cols} FROM sponsors WHERE active = true ORDER BY CASE tier WHEN 'gold' THEN 1 WHEN 'silver' THEN 2 ELSE 3 END, position, name", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield read r ]

let getAllSponsors () : Sponsor list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"SELECT {cols} FROM sponsors ORDER BY CASE tier WHEN 'gold' THEN 1 WHEN 'silver' THEN 2 ELSE 3 END, position, name", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield read r ]

let createSponsor (name: string) (logoUrl: string option) (websiteUrl: string option) (tier: string) (blurb: string option) (position: int) : Sponsor option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""INSERT INTO sponsors (name, logo_url, website_url, tier, blurb, position)
            VALUES (@name, @logo, @web, @tier, @blurb, @pos)
            RETURNING {cols}""", conn)
    cmd.Parameters.AddWithValue("name", name)                                                                  |> ignore
    cmd.Parameters.AddWithValue("logo", logoUrl    |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("web",  websiteUrl |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("tier", tier)                                                                  |> ignore
    cmd.Parameters.AddWithValue("blurb", blurb     |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("pos",  position)                                                              |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (read r) else None

let updateSponsor (id: Guid) (name: string) (logoUrl: string option) (websiteUrl: string option) (tier: string) (blurb: string option) (active: bool) (position: int) : Sponsor option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        $"""UPDATE sponsors
            SET name = @name, logo_url = @logo, website_url = @web, tier = @tier,
                blurb = @blurb, active = @active, position = @pos, updated_at = now()
            WHERE id = @id
            RETURNING {cols}""", conn)
    cmd.Parameters.AddWithValue("id",     id)                                                                    |> ignore
    cmd.Parameters.AddWithValue("name",   name)                                                                  |> ignore
    cmd.Parameters.AddWithValue("logo",   logoUrl    |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("web",    websiteUrl |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("tier",   tier)                                                                  |> ignore
    cmd.Parameters.AddWithValue("blurb",  blurb      |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("active", active)                                                                |> ignore
    cmd.Parameters.AddWithValue("pos",    position)                                                              |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (read r) else None

let deleteSponsor (id: Guid) : bool =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("DELETE FROM sponsors WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0
