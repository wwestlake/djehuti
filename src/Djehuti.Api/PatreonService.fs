module Djehuti.Api.PatreonService

open System
open Npgsql

let private openConnection () = Database.openConnection()

type TierLimits = {
    tierId: string option
    tierName: string
    maxConcurrentTasks: int option
    pollingIntervalSec: int option
    archiveDays: int option
}

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
