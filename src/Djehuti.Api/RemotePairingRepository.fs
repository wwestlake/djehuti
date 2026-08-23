module Djehuti.Api.RemotePairingRepository

open System
open System.Security.Cryptography
open Npgsql

// ── Records ──────────────────────────────────────────────────────────────────

type HostSessionRecord = {
    Id                     : Guid
    UserId                 : Guid
    ProductSlug            : string
    AppId                  : string
    AppVersion             : string
    DeviceId               : string
    DeviceName             : string option
    AgentAvailable         : bool
    ControlPanelAvailable  : bool
    CapabilitiesJson       : string
    PresenceState          : string
    LastHeartbeatAt        : DateTimeOffset
}

type PairingRecord = {
    Id                : Guid
    HostSessionId     : Guid
    UserId            : Guid
    RemoteDeviceName  : string
    RemoteDeviceType  : string
    Status            : string
    PairingCode       : string
    CreatedAt         : DateTimeOffset
    ExpiresAt         : DateTimeOffset
}

type ConnectionGrantRecord = {
    Id             : Guid
    HostSessionId  : Guid
    PairingId      : Guid
    GrantToken     : string
    GrantType      : string
    ExpiresAt      : DateTimeOffset
}

// ── Codes / tokens ───────────────────────────────────────────────────────────

// Crockford-ish alphabet, ambiguous characters (0/O, 1/I/L) removed -- this
// code may need to be typed manually as a fallback if QR scanning fails.
let private codeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"

let private generatePairingCode () : string =
    let bytes = RandomNumberGenerator.GetBytes(8)
    String(bytes |> Array.map (fun b -> codeAlphabet.[int b % codeAlphabet.Length]))

let private generateGrantToken () : string =
    let bytes = RandomNumberGenerator.GetBytes(32)
    "crg_" + Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "")

// ── Host sessions ────────────────────────────────────────────────────────────

// One row per (user, product, device) -- a receiver re-checking-in updates
// its existing row (heartbeat, capabilities) rather than accumulating a new
// row every check-in interval.
let checkInHostSession
        (userId: Guid) (productSlug: string) (appId: string) (appVersion: string)
        (deviceId: string) (deviceName: string option)
        (agentAvailable: bool) (controlPanelAvailable: bool) (capabilitiesJson: string)
        (projectsJson: string)
        : HostSessionRecord =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO remote_host_sessions
            (user_id, product_slug, app_id, app_version, device_id, device_name,
             agent_available, control_panel_available, capabilities_json, projects_json,
             presence_state, last_heartbeat_at, updated_at, revoked_at)
        VALUES
            (@userId, @productSlug, @appId, @appVersion, @deviceId, @deviceName,
             @agentAvailable, @controlPanelAvailable, @capabilitiesJson::jsonb, @projectsJson::jsonb,
             'available', NOW(), NOW(), NULL)
        ON CONFLICT (user_id, product_slug, device_id)
            WHERE revoked_at IS NULL
            DO NOTHING
        RETURNING id, user_id, product_slug, app_id, app_version, device_id, device_name,
                  agent_available, control_panel_available, capabilities_json::text,
                  presence_state, last_heartbeat_at
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("productSlug", productSlug) |> ignore
    cmd.Parameters.AddWithValue("appId", appId) |> ignore
    cmd.Parameters.AddWithValue("appVersion", appVersion) |> ignore
    cmd.Parameters.AddWithValue("deviceId", deviceId) |> ignore
    cmd.Parameters.AddWithValue("deviceName", (deviceName |> Option.defaultValue null : string)) |> ignore
    cmd.Parameters.AddWithValue("agentAvailable", agentAvailable) |> ignore
    cmd.Parameters.AddWithValue("controlPanelAvailable", controlPanelAvailable) |> ignore
    cmd.Parameters.AddWithValue("capabilitiesJson", capabilitiesJson) |> ignore
    cmd.Parameters.AddWithValue("projectsJson", projectsJson) |> ignore

    let readRecord (reader: System.Data.Common.DbDataReader) : HostSessionRecord = {
        Id = reader.GetGuid(0)
        UserId = reader.GetGuid(1)
        ProductSlug = reader.GetString(2)
        AppId = reader.GetString(3)
        AppVersion = reader.GetString(4)
        DeviceId = reader.GetString(5)
        DeviceName = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
        AgentAvailable = reader.GetBoolean(7)
        ControlPanelAvailable = reader.GetBoolean(8)
        CapabilitiesJson = reader.GetString(9)
        PresenceState = reader.GetString(10)
        LastHeartbeatAt = reader.GetFieldValue<DateTimeOffset>(11)
    }

    use reader = cmd.ExecuteReader()
    if reader.Read() then
        readRecord reader
    else
        // ON CONFLICT ... DO NOTHING returned no row -- an existing session
        // for this (user, product, device) is still active; update it instead.
        reader.Close()
        use updateCmd = new NpgsqlCommand("""
            UPDATE remote_host_sessions
            SET app_version = @appVersion,
                device_name = @deviceName,
                agent_available = @agentAvailable,
                control_panel_available = @controlPanelAvailable,
                capabilities_json = @capabilitiesJson::jsonb,
                projects_json = @projectsJson::jsonb,
                presence_state = 'available',
                last_heartbeat_at = NOW(),
                updated_at = NOW()
            WHERE user_id = @userId AND product_slug = @productSlug AND device_id = @deviceId
                  AND revoked_at IS NULL
            RETURNING id, user_id, product_slug, app_id, app_version, device_id, device_name,
                      agent_available, control_panel_available, capabilities_json::text,
                      presence_state, last_heartbeat_at
        """, conn)
        updateCmd.Parameters.AddWithValue("userId", userId) |> ignore
        updateCmd.Parameters.AddWithValue("productSlug", productSlug) |> ignore
        updateCmd.Parameters.AddWithValue("deviceId", deviceId) |> ignore
        updateCmd.Parameters.AddWithValue("appVersion", appVersion) |> ignore
        updateCmd.Parameters.AddWithValue("deviceName", (deviceName |> Option.defaultValue null : string)) |> ignore
        updateCmd.Parameters.AddWithValue("agentAvailable", agentAvailable) |> ignore
        updateCmd.Parameters.AddWithValue("controlPanelAvailable", controlPanelAvailable) |> ignore
        updateCmd.Parameters.AddWithValue("capabilitiesJson", capabilitiesJson) |> ignore
        updateCmd.Parameters.AddWithValue("projectsJson", projectsJson) |> ignore
        use updateReader = updateCmd.ExecuteReader()
        updateReader.Read() |> ignore
        readRecord updateReader

// ── Pairings ─────────────────────────────────────────────────────────────────

let createPairing (userId: Guid) (hostSessionId: Guid) : PairingRecord option =
    use conn = Database.openConnection()
    // Confirm the caller owns this host session before issuing a code for it.
    use ownerCheck = new NpgsqlCommand("""
        SELECT 1 FROM remote_host_sessions WHERE id = @hostSessionId AND user_id = @userId AND revoked_at IS NULL
    """, conn)
    ownerCheck.Parameters.AddWithValue("hostSessionId", hostSessionId) |> ignore
    ownerCheck.Parameters.AddWithValue("userId", userId) |> ignore
    use ownerReader = ownerCheck.ExecuteReader()
    let owns = ownerReader.Read()
    ownerReader.Close()
    if not owns then None
    else
        // Collision on the partial-unique (pairing_code WHERE status='pending')
        // index is astronomically unlikely at 8 chars from a 32-symbol alphabet,
        // but retry once rather than fail outright.
        let rec tryInsert attemptsLeft =
            let code = generatePairingCode ()
            use cmd = new NpgsqlCommand("""
                INSERT INTO remote_pairings
                    (host_session_id, user_id, remote_device_name, remote_device_type,
                     status, pairing_code, expires_at)
                VALUES (@hostSessionId, @userId, '', '', 'pending', @code, NOW() + INTERVAL '10 minutes')
                RETURNING id, host_session_id, user_id, remote_device_name, remote_device_type,
                          status, pairing_code, created_at, expires_at
            """, conn)
            cmd.Parameters.AddWithValue("hostSessionId", hostSessionId) |> ignore
            cmd.Parameters.AddWithValue("userId", userId) |> ignore
            cmd.Parameters.AddWithValue("code", code) |> ignore
            try
                use reader = cmd.ExecuteReader()
                reader.Read() |> ignore
                Some {
                    Id = reader.GetGuid(0)
                    HostSessionId = reader.GetGuid(1)
                    UserId = reader.GetGuid(2)
                    RemoteDeviceName = reader.GetString(3)
                    RemoteDeviceType = reader.GetString(4)
                    Status = reader.GetString(5)
                    PairingCode = reader.GetString(6)
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(7)
                    ExpiresAt = reader.GetFieldValue<DateTimeOffset>(8)
                }
            with :? PostgresException as ex when ex.SqlState = "23505" && attemptsLeft > 0 ->
                tryInsert (attemptsLeft - 1)
        tryInsert 3

// Called by the phone: submits the code it scanned/typed. Verifies the
// approving account matches the pairing's account (defense in depth beyond
// the short-lived random code itself), then issues a connection grant.
let approvePairing
        (approvingUserId: Guid) (pairingCode: string)
        (remoteDeviceName: string) (remoteDeviceType: string)
        : Result<ConnectionGrantRecord, string> =
    use conn = Database.openConnection()
    use findCmd = new NpgsqlCommand("""
        SELECT id, host_session_id, user_id, status, expires_at
        FROM remote_pairings
        WHERE pairing_code = @code AND status = 'pending'
    """, conn)
    findCmd.Parameters.AddWithValue("code", pairingCode) |> ignore
    use findReader = findCmd.ExecuteReader()
    if not (findReader.Read()) then
        findReader.Close()
        Error "Pairing code not found or already used"
    else
        let pairingId = findReader.GetGuid(0)
        let hostSessionId = findReader.GetGuid(1)
        let ownerUserId = findReader.GetGuid(2)
        let expiresAt = findReader.GetFieldValue<DateTimeOffset>(4)
        findReader.Close()
        if expiresAt < DateTimeOffset.UtcNow then
            Error "Pairing code expired"
        elif ownerUserId <> approvingUserId then
            Error "Pairing code does not belong to this account"
        else
            use approveCmd = new NpgsqlCommand("""
                UPDATE remote_pairings
                SET status = 'approved', approved_at = NOW(), last_used_at = NOW(),
                    remote_device_name = @deviceName, remote_device_type = @deviceType
                WHERE id = @pairingId
            """, conn)
            approveCmd.Parameters.AddWithValue("pairingId", pairingId) |> ignore
            approveCmd.Parameters.AddWithValue("deviceName", remoteDeviceName) |> ignore
            approveCmd.Parameters.AddWithValue("deviceType", remoteDeviceType) |> ignore
            approveCmd.ExecuteNonQuery() |> ignore

            let grantToken = generateGrantToken ()
            use grantCmd = new NpgsqlCommand("""
                INSERT INTO remote_connection_grants
                    (host_session_id, pairing_id, grant_token, grant_type, expires_at)
                VALUES (@hostSessionId, @pairingId, @grantToken, 'session', NOW() + INTERVAL '30 days')
                RETURNING id, host_session_id, pairing_id, grant_token, grant_type, expires_at
            """, conn)
            grantCmd.Parameters.AddWithValue("hostSessionId", hostSessionId) |> ignore
            grantCmd.Parameters.AddWithValue("pairingId", pairingId) |> ignore
            grantCmd.Parameters.AddWithValue("grantToken", grantToken) |> ignore
            use grantReader = grantCmd.ExecuteReader()
            grantReader.Read() |> ignore
            Ok {
                Id = grantReader.GetGuid(0)
                HostSessionId = grantReader.GetGuid(1)
                PairingId = grantReader.GetGuid(2)
                GrantToken = grantReader.GetString(3)
                GrantType = grantReader.GetString(4)
                ExpiresAt = grantReader.GetFieldValue<DateTimeOffset>(5)
            }

// Polled by the receiver while a pairing code is on screen, to learn when
// the phone has approved it. Returns None if the pairing itself vanished
// (shouldn't happen -- rows aren't deleted), Some status otherwise.
let getPairingStatus (userId: Guid) (pairingId: Guid) : string option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT status FROM remote_pairings WHERE id = @id AND user_id = @userId
    """, conn)
    cmd.Parameters.AddWithValue("id", pairingId) |> ignore
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (reader.GetString(0)) else None

// The phone's project picker: what does this specific paired host session
// currently report? Gated on an active, non-consumed connection grant for
// the caller -- "paired" is exactly "holds a live grant for this host
// session," not just "same account" (an account can own the host session
// without any phone having paired to it yet).
let getHostSessionProjects (callerId: Guid) (hostSessionId: Guid) : string option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT hs.projects_json::text
        FROM remote_host_sessions hs
        WHERE hs.id = @hostSessionId
          AND EXISTS (
              SELECT 1 FROM remote_connection_grants g
              JOIN remote_pairings p ON p.id = g.pairing_id
              WHERE g.host_session_id = hs.id
                AND p.user_id = @callerId
                AND g.consumed_at IS NULL
                AND g.expires_at > NOW()
          )
    """, conn)
    cmd.Parameters.AddWithValue("hostSessionId", hostSessionId) |> ignore
    cmd.Parameters.AddWithValue("callerId", callerId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (reader.GetString(0)) else None

// ── Connection grants (device list) ─────────────────────────────────────────

// The phone's "paired devices" switchable list: every non-revoked grant for
// this account, joined back to the host session it grants access to.
type PairedDeviceRecord = {
    HostSessionId  : Guid
    GrantToken     : string
    DeviceName     : string option
    ProductSlug    : string
    PresenceState  : string
    LastHeartbeatAt: DateTimeOffset
    GrantExpiresAt : DateTimeOffset
}

let listPairedDevices (userId: Guid) : PairedDeviceRecord list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT hs.id, g.grant_token, hs.device_name, hs.product_slug,
               hs.presence_state, hs.last_heartbeat_at, g.expires_at
        FROM remote_connection_grants g
        JOIN remote_host_sessions hs ON hs.id = g.host_session_id
        JOIN remote_pairings p ON p.id = g.pairing_id
        WHERE p.user_id = @userId AND g.consumed_at IS NULL AND g.expires_at > NOW()
        ORDER BY hs.last_heartbeat_at DESC
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- {
            HostSessionId = reader.GetGuid(0)
            GrantToken = reader.GetString(1)
            DeviceName = if reader.IsDBNull(2) then None else Some (reader.GetString(2))
            ProductSlug = reader.GetString(3)
            PresenceState = reader.GetString(4)
            LastHeartbeatAt = reader.GetFieldValue<DateTimeOffset>(5)
            GrantExpiresAt = reader.GetFieldValue<DateTimeOffset>(6)
        } :: results
    List.rev results

let revokeGrant (userId: Guid) (grantToken: string) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE remote_connection_grants g
        SET consumed_at = NOW()
        FROM remote_pairings p
        WHERE g.pairing_id = p.id AND g.grant_token = @grantToken AND p.user_id = @userId
    """, conn)
    cmd.Parameters.AddWithValue("grantToken", grantToken) |> ignore
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.ExecuteNonQuery() > 0

// Validates a grant token presented by a phone client on a data/signaling
// call. Returns the host session it grants access to, if the grant is live.
let validateGrantToken (grantToken: string) : Guid option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT host_session_id FROM remote_connection_grants
        WHERE grant_token = @grantToken AND consumed_at IS NULL AND expires_at > NOW()
    """, conn)
    cmd.Parameters.AddWithValue("grantToken", grantToken) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (reader.GetGuid(0)) else None
