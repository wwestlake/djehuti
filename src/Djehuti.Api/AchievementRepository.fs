module Djehuti.Api.AchievementRepository

open System
open Npgsql
open Djehuti.Api.Database

// ── Types ────────────────────────────────────────────────────────────────────

type Achievement = {
    Id          : Guid
    Slug        : string
    Name        : string
    Description : string
    Icon        : string
    Tier        : string
    Category    : string
    Points      : int
    Hidden      : bool
}

type UserAchievement = {
    Id            : Guid
    UserId        : Guid
    AchievementId : Guid
    Slug          : string
    Name          : string
    Description   : string
    Icon          : string
    Tier          : string
    Category      : string
    Points        : int
    AwardedAt     : DateTime
    Notified      : bool
}

type UserMetrics = {
    UserId        : Guid
    PostCount     : int
    ThreadCount   : int
    VoteReceived  : int
    AnswerCount   : int
    ReactionCount : int
    DaysActive    : int
    LoginStreak   : int
    LastActiveDay : DateOnly option
}

// ── Seed data ─────────────────────────────────────────────────────────────────

let private seedAchievements = [
    // Participation
    "first-post",       "First Post",           "Posted your first message",                  "✍️",  "bronze",    "participation", 10
    "thread-starter",   "Thread Starter",        "Created your first discussion thread",       "🧵",  "bronze",    "participation", 10
    "regular",          "Regular",               "Posted 25 times",                            "📬",  "silver",    "participation", 25
    "prolific",         "Prolific",              "Posted 100 times",                           "📚",  "gold",      "participation", 50
    "voice-of-reason",  "Voice of Reason",       "Posted 500 times",                           "🗣️", "platinum",  "participation", 100
    // Quality
    "first-answer",     "Solver",                "Had a post marked as an accepted answer",    "✅",  "bronze",    "quality",       15
    "top-solver",       "Top Solver",            "Had 10 posts marked as accepted answers",    "🏅",  "gold",      "quality",       75
    "well-received",    "Well Received",         "Received 10 upvotes across all posts",       "👍",  "silver",    "quality",       30
    "community-fave",   "Community Favourite",   "Received 100 upvotes across all posts",      "❤️", "gold",      "quality",       75
    // Reactions
    "emoji-collector",  "Emoji Collector",       "Received 25 emoji reactions",                "😄",  "bronze",    "reactions",     10
    "heart-magnet",     "Heart Magnet",          "Received 100 emoji reactions",               "💖",  "silver",    "reactions",     30
    // Longevity
    "week-streak",      "Week Streak",           "Logged in 7 days in a row",                  "🔥",  "bronze",    "longevity",     15
    "month-streak",     "Month Streak",          "Logged in 30 days in a row",                 "🌟",  "silver",    "longevity",     50
    "century",          "Century",               "Active on 100 separate days",                "💯",  "gold",      "longevity",     100
    "veteran",          "Veteran",               "Active on 365 separate days",                "🎖️", "platinum",  "longevity",     200
    // Threads
    "conversationalist","Conversationalist",     "Started 10 threads",                         "💬",  "silver",    "threads",       25
    "forum-architect",  "Forum Architect",       "Started 50 threads",                         "🏗️", "gold",      "threads",       75
    // Hidden / Easter egg
    "early-adopter",    "Early Adopter",         "Joined during the beta period",              "🌱",  "legendary", "special",       500
    "night-owl",        "Night Owl",             "Posted between 2 AM and 5 AM local time",    "🦉",  "bronze",    "special",       10
    "comeback-kid",     "Comeback Kid",          "Returned after 30+ days of inactivity",      "🔄",  "silver",    "special",       25
    // Djehuti interaction
    "djehuti-initiate",        "Initiate Contact",   "Posted in a thread where Djehuti responded",             "djehuti-svg", "bronze",    "djehuti", 20
    "djehuti-prompt-engineer", "Prompt Engineer",    "Made 5 or more posts in a single thread with Djehuti",   "djehuti-svg", "silver",    "djehuti", 50
    "djehuti-code-collab",     "Code Collaborator",  "Djehuti generated a code block in your thread that received an upvote", "djehuti-svg", "gold", "djehuti", 75
    "djehuti-deep-diver",      "Deep Diver",         "Elicited a Djehuti response exceeding 1,000 words",      "djehuti-svg", "gold",      "djehuti", 75
    "djehuti-daily-sync",      "Daily Sync",         "Interacted with Djehuti on 7 or more distinct days",     "djehuti-svg", "platinum",  "djehuti", 100
]

// ── Helpers ──────────────────────────────────────────────────────────────────

let private readAchievement (r: System.Data.Common.DbDataReader) : Achievement =
    { Id          = r.GetGuid(0)
      Slug        = r.GetString(1)
      Name        = r.GetString(2)
      Description = r.GetString(3)
      Icon        = r.GetString(4)
      Tier        = r.GetString(5)
      Category    = r.GetString(6)
      Points      = r.GetInt32(7)
      Hidden      = r.GetBoolean(8) }

// ── Seed ─────────────────────────────────────────────────────────────────────

let seedDictionary () =
    use conn = openConnection ()
    for (slug, name, desc, icon, tier, cat, pts) in seedAchievements do
        use cmd = new NpgsqlCommand("""
            INSERT INTO achievement_dictionary (slug, name, description, icon, tier, category, points, hidden)
            VALUES (@slug, @name, @desc, @icon, @tier, @cat, @pts, @hidden)
            ON CONFLICT (slug) DO UPDATE
              SET name=@name, description=@desc, icon=@icon, tier=@tier, category=@cat, points=@pts
        """, conn)
        cmd.Parameters.AddWithValue("slug",   slug)   |> ignore
        cmd.Parameters.AddWithValue("name",   name)   |> ignore
        cmd.Parameters.AddWithValue("desc",   desc)   |> ignore
        cmd.Parameters.AddWithValue("icon",   icon)   |> ignore
        cmd.Parameters.AddWithValue("tier",   tier)   |> ignore
        cmd.Parameters.AddWithValue("cat",    cat)    |> ignore
        cmd.Parameters.AddWithValue("pts",    pts)    |> ignore
        cmd.Parameters.AddWithValue("hidden", (cat = "special" && slug <> "early-adopter")) |> ignore
        cmd.ExecuteNonQuery() |> ignore

// ── Dictionary queries ────────────────────────────────────────────────────────

let getAllAchievements (includeHidden: bool) : Achievement list =
    use conn = openConnection ()
    let sql =
        if includeHidden then
            "SELECT id,slug,name,description,icon,tier,category,points,hidden FROM achievement_dictionary ORDER BY tier,category,name"
        else
            "SELECT id,slug,name,description,icon,tier,category,points,hidden FROM achievement_dictionary WHERE NOT hidden ORDER BY tier,category,name"
    use cmd = new NpgsqlCommand(sql, conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do yield readAchievement r ]

let getAchievementBySlug (slug: string) : Achievement option =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand(
        "SELECT id,slug,name,description,icon,tier,category,points,hidden FROM achievement_dictionary WHERE slug=@s", conn)
    cmd.Parameters.AddWithValue("s", slug) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then Some (readAchievement r) else None

// ── User metrics ──────────────────────────────────────────────────────────────

let getMetrics (userId: Guid) : UserMetrics =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("""
        SELECT user_id,post_count,thread_count,vote_received,answer_count,
               reaction_count,days_active,login_streak,last_active_day
        FROM user_metrics WHERE user_id=@uid
    """, conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use r = cmd.ExecuteReader()
    if r.Read() then
        { UserId        = r.GetGuid(0)
          PostCount     = r.GetInt32(1)
          ThreadCount   = r.GetInt32(2)
          VoteReceived  = r.GetInt32(3)
          AnswerCount   = r.GetInt32(4)
          ReactionCount = r.GetInt32(5)
          DaysActive    = r.GetInt32(6)
          LoginStreak   = r.GetInt32(7)
          LastActiveDay = if r.IsDBNull(8) then None else Some (DateOnly.FromDateTime(r.GetDateTime(8))) }
    else
        { UserId=userId; PostCount=0; ThreadCount=0; VoteReceived=0; AnswerCount=0
          ReactionCount=0; DaysActive=0; LoginStreak=0; LastActiveDay=None }

let upsertMetrics (m: UserMetrics) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("""
        INSERT INTO user_metrics
          (user_id,post_count,thread_count,vote_received,answer_count,reaction_count,days_active,login_streak,last_active_day,updated_at)
        VALUES (@uid,@pc,@tc,@vr,@ac,@rc,@da,@ls,@lad,now())
        ON CONFLICT (user_id) DO UPDATE SET
          post_count=@pc, thread_count=@tc, vote_received=@vr, answer_count=@ac,
          reaction_count=@rc, days_active=@da, login_streak=@ls, last_active_day=@lad, updated_at=now()
    """, conn)
    cmd.Parameters.AddWithValue("uid", m.UserId)        |> ignore
    cmd.Parameters.AddWithValue("pc",  m.PostCount)     |> ignore
    cmd.Parameters.AddWithValue("tc",  m.ThreadCount)   |> ignore
    cmd.Parameters.AddWithValue("vr",  m.VoteReceived)  |> ignore
    cmd.Parameters.AddWithValue("ac",  m.AnswerCount)   |> ignore
    cmd.Parameters.AddWithValue("rc",  m.ReactionCount) |> ignore
    cmd.Parameters.AddWithValue("da",  m.DaysActive)    |> ignore
    cmd.Parameters.AddWithValue("ls",  m.LoginStreak)   |> ignore
    match m.LastActiveDay with
    | Some d -> cmd.Parameters.AddWithValue("lad", d.ToDateTime(TimeOnly.MinValue)) |> ignore
    | None   -> cmd.Parameters.AddWithValue("lad", DBNull.Value) |> ignore
    cmd.ExecuteNonQuery() |> ignore

// ── Award queries ─────────────────────────────────────────────────────────────

let getUserAchievements (userId: Guid) : UserAchievement list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("""
        SELECT ua.id, ua.user_id, ua.achievement_id, ad.slug, ad.name, ad.description,
               ad.icon, ad.tier, ad.category, ad.points, ua.awarded_at, ua.notified
        FROM user_achievements ua
        JOIN achievement_dictionary ad ON ad.id = ua.achievement_id
        WHERE ua.user_id = @uid
        ORDER BY ua.awarded_at DESC
    """, conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use r = cmd.ExecuteReader()
    [ while r.Read() do
        yield { Id=r.GetGuid(0); UserId=r.GetGuid(1); AchievementId=r.GetGuid(2)
                Slug=r.GetString(3); Name=r.GetString(4); Description=r.GetString(5)
                Icon=r.GetString(6); Tier=r.GetString(7); Category=r.GetString(8)
                Points=r.GetInt32(9); AwardedAt=r.GetDateTime(10); Notified=r.GetBoolean(11) } ]

let awardAchievement (userId: Guid) (achievementId: Guid) : bool =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("""
        INSERT INTO user_achievements (user_id, achievement_id)
        VALUES (@uid, @aid)
        ON CONFLICT (user_id, achievement_id) DO NOTHING
    """, conn)
    cmd.Parameters.AddWithValue("uid", userId)         |> ignore
    cmd.Parameters.AddWithValue("aid", achievementId)  |> ignore
    cmd.ExecuteNonQuery() > 0

let markNotified (achievementRowId: Guid) =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("UPDATE user_achievements SET notified=true WHERE id=@id", conn)
    cmd.Parameters.AddWithValue("id", achievementRowId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let getUnnotified () : UserAchievement list =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("""
        SELECT ua.id, ua.user_id, ua.achievement_id, ad.slug, ad.name, ad.description,
               ad.icon, ad.tier, ad.category, ad.points, ua.awarded_at, ua.notified
        FROM user_achievements ua
        JOIN achievement_dictionary ad ON ad.id = ua.achievement_id
        WHERE NOT ua.notified
        ORDER BY ua.awarded_at
    """, conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do
        yield { Id=r.GetGuid(0); UserId=r.GetGuid(1); AchievementId=r.GetGuid(2)
                Slug=r.GetString(3); Name=r.GetString(4); Description=r.GetString(5)
                Icon=r.GetString(6); Tier=r.GetString(7); Category=r.GetString(8)
                Points=r.GetInt32(9); AwardedAt=r.GetDateTime(10); Notified=r.GetBoolean(11) } ]

// ── Compute metrics from live DB data ────────────────────────────────────────

let recomputeMetrics (userId: Guid) : UserMetrics =
    use conn = openConnection ()
    use cmd = new NpgsqlCommand("""
        SELECT
          (SELECT COUNT(*)::int FROM forum_posts  WHERE author_id=@uid AND NOT deleted),
          (SELECT COUNT(*)::int FROM forum_threads WHERE author_id=@uid),
          (SELECT COALESCE(SUM(vote_count),0)::int FROM forum_posts WHERE author_id=@uid AND NOT deleted),
          (SELECT COUNT(*)::int FROM forum_posts  WHERE author_id=@uid AND is_answer AND NOT deleted),
          (SELECT COUNT(*)::int FROM post_reactions pr JOIN forum_posts fp ON fp.id=pr.post_id WHERE fp.author_id=@uid)
    """, conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use r = cmd.ExecuteReader()
    r.Read() |> ignore
    let current = getMetrics userId
    { current with
        PostCount     = r.GetInt32(0)
        ThreadCount   = r.GetInt32(1)
        VoteReceived  = r.GetInt32(2)
        AnswerCount   = r.GetInt32(3)
        ReactionCount = r.GetInt32(4) }
