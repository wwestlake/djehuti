module Djehuti.Api.PatreonService

open System
open Npgsql

let private openConnection () = Database.openConnection()

type PatreonTier = {
    TierId:       string
    TierName:     string
    AmountCents:  int
    DisplayOrder: int
    BadgeColor:   string
    BadgeLabel:   string
    Description:  string option
}

type SupporterEntry = {
    DisplayName: string
    TierId:      string
    TierName:    string
    BadgeColor:  string
    BadgeLabel:  string
    DisplayOrder: int
}

let getAllTiers () : PatreonTier list =
    use conn = openConnection()
    use cmd = new NpgsqlCommand(
        "SELECT tier_id, tier_name, amount_cents, display_order, badge_color, badge_label, description FROM patreon_tiers ORDER BY display_order", conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do
        yield {
            TierId       = r.GetString(0)
            TierName     = r.GetString(1)
            AmountCents  = r.GetInt32(2)
            DisplayOrder = r.GetInt32(3)
            BadgeColor   = r.GetString(4)
            BadgeLabel   = r.GetString(5)
            Description  = if r.IsDBNull(6) then None else Some (r.GetString(6))
        } ]

let getSupporters () : SupporterEntry list =
    use conn = openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT COALESCE(up.display_name, u.display_name, 'Anonymous'), pt.tier_id, pt.tier_name, pt.badge_color, pt.badge_label, pt.display_order
        FROM users u
        JOIN patreon_tiers pt ON pt.tier_id = u.patreon_tier_id
        LEFT JOIN user_profiles up ON up.user_id = u.id
        WHERE u.patreon_tier_id IS NOT NULL
        ORDER BY pt.display_order DESC, u.created_at ASC
    """, conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do
        yield {
            DisplayName  = r.GetString(0)
            TierId       = r.GetString(1)
            TierName     = r.GetString(2)
            BadgeColor   = r.GetString(3)
            BadgeLabel   = r.GetString(4)
            DisplayOrder = r.GetInt32(5)
        } ]

type TierLimits = {
    tierId: string option
    tierName: string
    maxConcurrentTasks: int option
    pollingIntervalSec: int option
    archiveDays: int option
}

type UserTierInfo = {
    UserId:     Guid
    TierId:     string
    TierName:   string
    BadgeColor: string
    BadgeLabel: string
}

let getUserTiers (userIds: Guid list) : UserTierInfo list =
    if userIds.IsEmpty then []
    else
        use conn = openConnection()
        let paramNames = userIds |> List.mapi (fun i _ -> $"@u{i}") |> String.concat ","
        use cmd = new NpgsqlCommand(
            $"SELECT u.id, pt.tier_id, pt.tier_name, pt.badge_color, pt.badge_label FROM users u JOIN patreon_tiers pt ON pt.tier_id = u.patreon_tier_id WHERE u.id IN ({paramNames})", conn)
        userIds |> List.iteri (fun i id -> cmd.Parameters.AddWithValue($"u{i}", id) |> ignore)
        use r = cmd.ExecuteReader()
        [ while r.Read() do
            yield {
                UserId     = r.GetGuid(0)
                TierId     = r.GetString(1)
                TierName   = r.GetString(2)
                BadgeColor = r.GetString(3)
                BadgeLabel = r.GetString(4)
            } ]

let getTierLimits (userId: Guid) : TierLimits option =
    try
        use conn = openConnection()
        use cmd = new NpgsqlCommand("""
            SELECT pt.tier_id, pt.tier_name, pt.max_concurrent_tasks, pt.polling_interval_sec, pt.archive_days
            FROM users u
            LEFT JOIN patreon_tiers pt ON pt.tier_id = u.patreon_tier_id
            WHERE u.id = @uid
        """, conn)
        cmd.Parameters.AddWithValue("uid", userId) |> ignore
        use r = cmd.ExecuteReader()
        if r.Read() then
            let tierName = if r.IsDBNull(1) then "Free" else r.GetString(1)
            Some {
                tierId = if r.IsDBNull(0) then None else Some (r.GetString(0))
                tierName = tierName
                maxConcurrentTasks = if r.IsDBNull(2) then (if tierName = "Free" then Some 1 else None) else Some (r.GetInt32(2))
                pollingIntervalSec = if r.IsDBNull(3) then (if tierName = "Free" then Some 300 else None) else Some (r.GetInt32(3))
                archiveDays = if r.IsDBNull(4) then (if tierName = "Free" then Some 0 else None) else Some (r.GetInt32(4))
            }
        else
            None
    with _ -> None

let getActiveTasks (userId: Guid) : int =
    try
        use conn = openConnection()
        use cmd = new NpgsqlCommand("""
            SELECT COUNT(*) FROM heartbeat_jobs
            WHERE payload::jsonb->>'userId' = @uid::text
            AND status = 'Processing'
        """, conn)
        cmd.Parameters.AddWithValue("uid", userId) |> ignore
        match cmd.ExecuteScalar() with
        | :? int as count -> count
        | :? int64 as count -> int count
        | _ -> 0
    with _ -> 0

let canSubmitTask (userId: Guid) : bool =
    match getTierLimits userId with
    | None -> false // User has no tier data
    | Some tier ->
        match tier.maxConcurrentTasks with
        | None -> true // Unlimited tier
        | Some limit ->
            let activeCount = getActiveTasks userId
            activeCount < limit

let getRemainingCapacity (userId: Guid) : int option =
    match getTierLimits userId with
    | None -> None
    | Some tier ->
        match tier.maxConcurrentTasks with
        | None -> Some 999 // Unlimited, return a large number
        | Some limit ->
            let activeCount = getActiveTasks userId
            Some (max 0 (limit - activeCount))
