module Djehuti.Api.AchievementEngine

open System
open Djehuti.Api.AchievementRepository

// ── Rule DSL ─────────────────────────────────────────────────────────────────

type Rule = {
    Slug      : string
    Predicate : UserMetrics -> bool
}

let private rules : Rule list = [
    { Slug = "first-post";       Predicate = fun m -> m.PostCount     >= 1   }
    { Slug = "thread-starter";   Predicate = fun m -> m.ThreadCount   >= 1   }
    { Slug = "regular";          Predicate = fun m -> m.PostCount     >= 25  }
    { Slug = "prolific";         Predicate = fun m -> m.PostCount     >= 100 }
    { Slug = "voice-of-reason";  Predicate = fun m -> m.PostCount     >= 500 }
    { Slug = "first-answer";     Predicate = fun m -> m.AnswerCount   >= 1   }
    { Slug = "top-solver";       Predicate = fun m -> m.AnswerCount   >= 10  }
    { Slug = "well-received";    Predicate = fun m -> m.VoteReceived  >= 10  }
    { Slug = "community-fave";   Predicate = fun m -> m.VoteReceived  >= 100 }
    { Slug = "emoji-collector";  Predicate = fun m -> m.ReactionCount >= 25  }
    { Slug = "heart-magnet";     Predicate = fun m -> m.ReactionCount >= 100 }
    { Slug = "week-streak";      Predicate = fun m -> m.LoginStreak   >= 7   }
    { Slug = "month-streak";     Predicate = fun m -> m.LoginStreak   >= 30  }
    { Slug = "century";          Predicate = fun m -> m.DaysActive    >= 100 }
    { Slug = "veteran";          Predicate = fun m -> m.DaysActive    >= 365 }
    { Slug = "conversationalist";Predicate = fun m -> m.ThreadCount   >= 10  }
    { Slug = "forum-architect";  Predicate = fun m -> m.ThreadCount   >= 50  }
]

// ── Djehuti-specific badge checks ────────────────────────────────────────────

let private checkDjehutiInteractions (userId: Guid) : string list =
    use conn = Djehuti.Api.Database.openConnection ()
    use cmd = new Npgsql.NpgsqlCommand("""
        WITH user_bot_threads AS (
            SELECT DISTINCT fp.thread_id
            FROM forum_posts fp
            WHERE fp.author_id = @uid AND fp.deleted_at IS NULL
              AND EXISTS (
                  SELECT 1 FROM forum_posts bp
                  JOIN users bu ON bu.id = bp.author_id
                  WHERE bp.thread_id = fp.thread_id AND bu.is_bot = true AND bp.deleted_at IS NULL
              )
        )
        SELECT
            (SELECT COUNT(*) FROM user_bot_threads) > 0,
            EXISTS (
                SELECT 1 FROM forum_posts fp
                WHERE fp.author_id = @uid AND fp.deleted_at IS NULL
                  AND fp.thread_id IN (SELECT thread_id FROM user_bot_threads)
                GROUP BY fp.thread_id HAVING COUNT(*) >= 5
            ),
            EXISTS (
                SELECT 1 FROM forum_posts bp
                JOIN users bu ON bu.id = bp.author_id
                WHERE bu.is_bot = true AND bp.deleted_at IS NULL
                  AND bp.thread_id IN (SELECT thread_id FROM user_bot_threads)
                  AND (bp.content ILIKE '%<pre%' OR bp.content ILIKE '%<code%')
                  AND bp.vote_count > 0
            ),
            EXISTS (
                SELECT 1 FROM forum_posts bp
                JOIN users bu ON bu.id = bp.author_id
                WHERE bu.is_bot = true AND bp.deleted_at IS NULL
                  AND bp.thread_id IN (SELECT thread_id FROM user_bot_threads)
                  AND LENGTH(bp.content) > 5000
            ),
            (
                SELECT COUNT(DISTINCT DATE(fp.created_at))
                FROM forum_posts fp
                WHERE fp.author_id = @uid AND fp.deleted_at IS NULL
                  AND fp.thread_id IN (SELECT thread_id FROM user_bot_threads)
            ) >= 7
    """, conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then
        [ if r.GetBoolean(0) then yield "djehuti-initiate"
          if r.GetBoolean(1) then yield "djehuti-prompt-engineer"
          if r.GetBoolean(2) then yield "djehuti-code-collab"
          if r.GetBoolean(3) then yield "djehuti-deep-diver"
          if r.GetBoolean(4) then yield "djehuti-daily-sync" ]
    else []

// ── Per-user pass ─────────────────────────────────────────────────────────────

let private processUser (userId: Guid) =
    let metrics = recomputeMetrics userId
    upsertMetrics metrics
    let existing = getUserAchievements userId |> List.map _.Slug |> Set.ofList
    for rule in rules do
        if not (Set.contains rule.Slug existing) && rule.Predicate metrics then
            match getAchievementBySlug rule.Slug with
            | Some a -> awardAchievement userId a.Id |> ignore
            | None   -> ()
    for slug in checkDjehutiInteractions userId do
        if not (Set.contains slug existing) then
            match getAchievementBySlug slug with
            | Some a -> awardAchievement userId a.Id |> ignore
            | None   -> ()

// ── Batch pass ────────────────────────────────────────────────────────────────

let runBatch () =
    use conn = Djehuti.Api.Database.openConnection ()
    use cmd = new Npgsql.NpgsqlCommand("SELECT id FROM users", conn)
    use r = cmd.ExecuteReader()
    let ids = [ while r.Read() do yield r.GetGuid(0) ]
    r.Close()
    for uid in ids do
        try processUser uid
        with ex -> printfn "[AchievementEngine] user %A: %s" uid ex.Message

// ── Notification pass ─────────────────────────────────────────────────────────

let dispatchNotifications () =
    let pending = getUnnotified ()
    for ua in pending do
        // Check user pref before sending notification
        let prefs = Djehuti.Api.PreferencesRepository.getPreferences ua.UserId
        let getBool key def =
            match Map.tryFind key prefs with
            | Some (v: obj) -> try unbox<bool> v with _ -> def
            | None -> def
        let wantsEmail  = getBool "email_notify_achievements"  false
        let wantsInApp  = getBool "inapp_notify_achievements"  true
        if wantsEmail then
            // Enqueue email via heartbeat_jobs
            use conn = Djehuti.Api.Database.openConnection ()
            use cmd = new Npgsql.NpgsqlCommand("""
                INSERT INTO heartbeat_jobs (action_type, payload)
                VALUES ('SendEmail', @payload::jsonb)
            """, conn)
            let payload = sprintf """{"to_user_id":"%s","template":"achievement","achievement_slug":"%s","achievement_name":"%s","icon":"%s"}""" (ua.UserId.ToString()) ua.Slug ua.Name ua.Icon
            cmd.Parameters.AddWithValue("payload", payload) |> ignore
            cmd.ExecuteNonQuery() |> ignore
        if wantsInApp then
            use conn2 = Djehuti.Api.Database.openConnection ()
            use cmd2 = new Npgsql.NpgsqlCommand("""
                INSERT INTO notifications (user_id, type, message, link)
                VALUES (@uid, 'achievement', @msg, '/profile/achievements')
                ON CONFLICT DO NOTHING
            """, conn2)
            cmd2.Parameters.AddWithValue("uid", ua.UserId) |> ignore
            cmd2.Parameters.AddWithValue("msg", sprintf "%s %s — %s" ua.Icon ua.Name ua.Description) |> ignore
            cmd2.ExecuteNonQuery() |> ignore
        markNotified ua.Id
