module Djehuti.Api.LicenseKeyRepository

open System
open System.Security.Cryptography
open System.Text
open Npgsql

type LicenseKeyRecord = {
    Id:              Guid
    Name:            string
    KeyPrefix:       string
    OwnerId:         Guid
    CreatedAt:       DateTimeOffset
    LastValidatedAt: DateTimeOffset option
    Active:          bool
}

type AdminLicenseKeyRecord = {
    Record:      LicenseKeyRecord
    OwnerName:   string
    TierName:    string option
}

type LicenseValidation = {
    Valid:     bool
    TierId:    string option
    TierName:  string option
    CheckedAt: DateTimeOffset
}

let private hashKey (raw: string) =
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()

// Same shape as ApiKeyRepository.generateKey but a distinct prefix (dlic_ vs
// djk_) -- these represent a different thing (a paid desktop-app license,
// validity tied live to Patreon pledge status) with a different owner-facing
// surface (self-serve from account settings once a tier is active, not
// admin-issued).
let generateKey (name: string) (ownerId: Guid) : string * LicenseKeyRecord =
    let rawBytes = RandomNumberGenerator.GetBytes(32)
    let raw = "dlic_" + Convert.ToBase64String(rawBytes).Replace("+", "A").Replace("/", "B").Replace("=", "")
    let prefix = raw.[..13]   // "dlic_" + 9 chars
    let hash = hashKey raw
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO license_keys (name, key_hash, key_prefix, owner_id)
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
        Id              = reader.GetGuid(0)
        Name            = name
        KeyPrefix       = prefix
        OwnerId         = ownerId
        CreatedAt       = reader.GetFieldValue<DateTimeOffset>(1)
        LastValidatedAt = None
        Active          = true
    }
    (raw, record)

let listKeys (ownerId: Guid) : LicenseKeyRecord list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT id, name, key_prefix, owner_id, created_at, last_validated_at, active
        FROM license_keys
        WHERE owner_id = @owner
        ORDER BY created_at DESC
    """, conn)
    cmd.Parameters.AddWithValue("owner", ownerId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- {
            Id              = reader.GetGuid(0)
            Name            = reader.GetString(1)
            KeyPrefix       = reader.GetString(2)
            OwnerId         = reader.GetGuid(3)
            CreatedAt       = reader.GetFieldValue<DateTimeOffset>(4)
            LastValidatedAt = if reader.IsDBNull(5) then None else Some (reader.GetFieldValue<DateTimeOffset>(5))
            Active          = reader.GetBoolean(6)
        } :: results
    List.rev results

// Admin oversight view: every license key across every account, with the
// owner's display name and their current tier (so support can see "is this
// key even supposed to be valid right now" at a glance without cross-
// referencing the users table separately).
let listAll () : AdminLicenseKeyRecord list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT lk.id, lk.name, lk.key_prefix, lk.owner_id, lk.created_at, lk.last_validated_at, lk.active,
               COALESCE(up.display_name, u.display_name, 'Anonymous'), pt.tier_name
        FROM license_keys lk
        JOIN users u ON u.id = lk.owner_id
        LEFT JOIN user_profiles up ON up.user_id = u.id
        LEFT JOIN patreon_tiers pt ON pt.tier_id = u.patreon_tier_id
        ORDER BY lk.created_at DESC
    """, conn)
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        let record = {
            Id              = reader.GetGuid(0)
            Name            = reader.GetString(1)
            KeyPrefix       = reader.GetString(2)
            OwnerId         = reader.GetGuid(3)
            CreatedAt       = reader.GetFieldValue<DateTimeOffset>(4)
            LastValidatedAt = if reader.IsDBNull(5) then None else Some (reader.GetFieldValue<DateTimeOffset>(5))
            Active          = reader.GetBoolean(6)
        }
        results <- {
            Record    = record
            OwnerName = reader.GetString(7)
            TierName  = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
        } :: results
    List.rev results

let revokeKey (keyId: Guid) (ownerId: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE license_keys SET active = FALSE, revoked_at = NOW()
        WHERE id = @id AND owner_id = @owner
    """, conn)
    cmd.Parameters.AddWithValue("id",    keyId)   |> ignore
    cmd.Parameters.AddWithValue("owner", ownerId) |> ignore
    cmd.ExecuteNonQuery() > 0

// Admin can revoke any key regardless of owner (support/abuse handling) --
// same effect as the owner revoking their own, just without the owner_id
// filter.
let adminRevokeKey (keyId: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE license_keys SET active = FALSE, revoked_at = NOW()
        WHERE id = @id
    """, conn)
    cmd.Parameters.AddWithValue("id", keyId) |> ignore
    cmd.ExecuteNonQuery() > 0

// The public, desktop-app-facing check. Deliberately re-derives validity
// from LIVE Patreon tier status on every call rather than trusting a cached
// flag on the key row -- the existing webhook already nulls
// users.patreon_tier_id the moment a pledge lapses (members:pledge:delete),
// so a canceled subscription invalidates every license key for that account
// on its very next check, automatically, with no separate revocation step
// anywhere in this flow.
let validate (raw: string) : LicenseValidation =
    let now = DateTimeOffset.UtcNow
    if String.IsNullOrWhiteSpace raw then
        { Valid = false; TierId = None; TierName = None; CheckedAt = now }
    else
        let hash = hashKey raw
        use conn = Database.openConnection()
        use cmd = new NpgsqlCommand("""
            SELECT lk.id, u.patreon_tier_id, pt.tier_name
            FROM license_keys lk
            JOIN users u ON u.id = lk.owner_id
            LEFT JOIN patreon_tiers pt ON pt.tier_id = u.patreon_tier_id
            WHERE lk.key_hash = @hash AND lk.active = TRUE
        """, conn)
        cmd.Parameters.AddWithValue("hash", hash) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then
            let keyId = reader.GetGuid(0)
            let tierId = if reader.IsDBNull(1) then None else Some (reader.GetString(1))
            let tierName = if reader.IsDBNull(2) then None else Some (reader.GetString(2))
            reader.Close()
            use updateCmd = new NpgsqlCommand("UPDATE license_keys SET last_validated_at = NOW() WHERE id = @id", conn)
            updateCmd.Parameters.AddWithValue("id", keyId) |> ignore
            updateCmd.ExecuteNonQuery() |> ignore
            { Valid = tierId.IsSome; TierId = tierId; TierName = tierName; CheckedAt = now }
        else
            { Valid = false; TierId = None; TierName = None; CheckedAt = now }
