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
                INSERT INTO heartbeat_jobs (action_type, payload, scheduled_at)
                VALUES ('SendEmail', @payload::jsonb, now())
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
