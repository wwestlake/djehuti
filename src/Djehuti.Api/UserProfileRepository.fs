module UserProfileRepository

open System
open System.Data.Common
open Npgsql

type UserProfile = {
    UserId:      Guid
    DisplayName: string option
    Bio:         string option
    AvatarUrl:   string option
    Website:     string option
    Location:    string option
    CreatedAt:   DateTime
    UpdatedAt:   DateTime
}

let private readProfile (r: DbDataReader) : UserProfile = {
    UserId      = r.GetGuid(r.GetOrdinal("user_id"))
    DisplayName = if r.IsDBNull(r.GetOrdinal("display_name")) then None else Some(r.GetString(r.GetOrdinal("display_name")))
    Bio         = if r.IsDBNull(r.GetOrdinal("bio"))          then None else Some(r.GetString(r.GetOrdinal("bio")))
    AvatarUrl   = if r.IsDBNull(r.GetOrdinal("avatar_url"))   then None else Some(r.GetString(r.GetOrdinal("avatar_url")))
    Website     = if r.IsDBNull(r.GetOrdinal("website"))      then None else Some(r.GetString(r.GetOrdinal("website")))
    Location    = if r.IsDBNull(r.GetOrdinal("location"))     then None else Some(r.GetString(r.GetOrdinal("location")))
    CreatedAt   = r.GetDateTime(r.GetOrdinal("created_at"))
    UpdatedAt   = r.GetDateTime(r.GetOrdinal("updated_at"))
}

let getProfile (conn: NpgsqlConnection) (userId: Guid) : UserProfile option =
    use cmd = new NpgsqlCommand(
        "SELECT * FROM user_profiles WHERE user_id = @uid", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some(readProfile reader) else None

let upsertProfile
    (conn: NpgsqlConnection)
    (userId: Guid)
    (displayName: string option)
    (bio: string option)
    (avatarUrl: string option)
    (website: string option)
    (location: string option) : UserProfile =
    use cmd = new NpgsqlCommand("""
        INSERT INTO user_profiles (user_id, display_name, bio, avatar_url, website, location)
        VALUES (@uid, @dn, @bio, @av, @web, @loc)
        ON CONFLICT (user_id) DO UPDATE
          SET display_name = EXCLUDED.display_name,
              bio          = EXCLUDED.bio,
              avatar_url   = EXCLUDED.avatar_url,
              website      = EXCLUDED.website,
              location     = EXCLUDED.location,
              updated_at   = now()
        RETURNING *""", conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    cmd.Parameters.AddWithValue("dn",  displayName |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("bio", bio         |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("av",  avatarUrl   |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("web", website     |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    cmd.Parameters.AddWithValue("loc", location    |> Option.map box |> Option.defaultValue (box DBNull.Value)) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    readProfile reader
