module Djehuti.Api.ApiKeyRepository

open System
open System.Security.Cryptography
open System.Text
open Npgsql

type ApiKeyRecord = {
    Id          : Guid
    Name        : string
    KeyPrefix   : string
    OwnerId     : Guid
    CreatedAt   : DateTimeOffset
    LastUsedAt  : DateTimeOffset option
    Active      : bool
}

let private hashKey (raw: string) =
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()

// Generate a new key: returns (plaintext, record) — plaintext shown once only
let generateKey (name: string) (ownerId: Guid) : string * ApiKeyRecord =
    let rawBytes = RandomNumberGenerator.GetBytes(32)
    let raw = "djk_" + Convert.ToBase64String(rawBytes).Replace("+", "A").Replace("/", "B").Replace("=", "")
    let prefix = raw.[..11]   // "djk_" + 8 chars
    let hash = hashKey raw
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO api_keys (name, key_hash, key_prefix, owner_id)
        VALUES (@name, @hash, @prefix, @owner)
        RETURNING id, created_at
    """, conn)
    cmd.Parameters.AddWithValue("name",   name)    |> ignore
    cmd.Parameters.AddWithValue("hash",   hash)    |> ignore
    cmd.Parameters.AddWithValue("prefix", prefix)  |> ignore
    cmd.Parameters.AddWithValue("owner",  ownerId) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    let record = {
        Id         = reader.GetGuid(0)
        Name       = name
        KeyPrefix  = prefix
        OwnerId    = ownerId
        CreatedAt  = reader.GetFieldValue<DateTimeOffset>(1)
        LastUsedAt = None
        Active     = true
    }
    (raw, record)

let listKeys (ownerId: Guid) : ApiKeyRecord list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT id, name, key_prefix, owner_id, created_at, last_used_at, active
        FROM api_keys
        WHERE owner_id = @owner
        ORDER BY created_at DESC
    """, conn)
    cmd.Parameters.AddWithValue("owner", ownerId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- {
            Id         = reader.GetGuid(0)
            Name       = reader.GetString(1)
            KeyPrefix  = reader.GetString(2)
            OwnerId    = reader.GetGuid(3)
            CreatedAt  = reader.GetFieldValue<DateTimeOffset>(4)
            LastUsedAt = if reader.IsDBNull(5) then None else Some (reader.GetFieldValue<DateTimeOffset>(5))
            Active     = reader.GetBoolean(6)
        } :: results
    List.rev results

let revokeKey (keyId: Guid) (ownerId: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE api_keys SET active = FALSE
        WHERE id = @id AND owner_id = @owner
    """, conn)
    cmd.Parameters.AddWithValue("id",    keyId)   |> ignore
    cmd.Parameters.AddWithValue("owner", ownerId) |> ignore
    cmd.ExecuteNonQuery() > 0

// Validate incoming X-Api-Key header — returns owner user id if valid
let validateKey (raw: string) : Guid option =
    if String.IsNullOrWhiteSpace(raw) then None
    else
        let hash = hashKey raw
        use conn = Database.openConnection()
        use cmd = new NpgsqlCommand("""
            UPDATE api_keys
            SET last_used_at = NOW()
            WHERE key_hash = @hash AND active = TRUE
            RETURNING owner_id
        """, conn)
        cmd.Parameters.AddWithValue("hash", hash) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (reader.GetGuid(0))
        else None
