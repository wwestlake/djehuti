module Djehuti.Api.MetricsRepository

open System
open Npgsql

[<CLIMutable>]
type MetricsCounts = {
    Users        : int
    Posts        : int
    Threads      : int
    Articles     : int
    VotesGiven   : int
    Reactions    : int
    Achievements : int
}

[<CLIMutable>]
type ForumActivityRow = {
    ForumId      : Guid
    ForumName    : string
    PostsAll     : int
    PostsHuman   : int
    PostsAi      : int
    ThreadsAll   : int
    ThreadsHuman : int
    ThreadsAi    : int
}

[<CLIMutable>]
type DailyActivityRow = {
    Date       : string
    PostsHuman : int
    PostsAi    : int
}

[<CLIMutable>]
type TopUserRow = {
    UserId       : Guid
    DisplayName  : string
    IsBot        : bool
    Posts        : int
    Threads      : int
    VotesReceived: int
    Achievements : int
    LoginStreak  : int
    DaysActive   : int
}

[<CLIMutable>]
type UserDrilldown = {
    UserId        : Guid
    DisplayName   : string
    IsBot         : bool
    Email         : string
    PostCount     : int
    ThreadCount   : int
    VoteReceived  : int
    VoteGiven     : int
    ReactionCount : int
    AnswerCount   : int
    LoginStreak   : int
    DaysActive    : int
    LastActiveAt  : string
    Achievements  : {| Slug: string; Name: string; Icon: string; Tier: string; AwardedAt: string |} list
}

let getSiteMetrics () =
    use conn = Database.openConnection ()

    // ── Totals ────────────────────────────────────────────────────────────────
    use cmd1 = new NpgsqlCommand("""
        SELECT
            COUNT(*) FILTER (WHERE NOT is_bot)::int,
            COUNT(*) FILTER (WHERE is_bot)::int,
            COUNT(*)::int
        FROM users WHERE status != 'deleted'
    """, conn)
    use r1 = cmd1.ExecuteReader()
    let humanUsers, aiUsers, allUsers =
        if r1.Read() then r1.GetInt32(0), r1.GetInt32(1), r1.GetInt32(2)
        else 0, 0, 0
    r1.Close()

    use cmd2 = new NpgsqlCommand("""
        SELECT
            COUNT(*) FILTER (WHERE NOT u.is_bot)::int,
            COUNT(*) FILTER (WHERE u.is_bot)::int,
            COUNT(*)::int
        FROM forum_posts fp JOIN users u ON u.id = fp.author_id WHERE fp.deleted_at IS NULL
    """, conn)
    use r2 = cmd2.ExecuteReader()
    let humanPosts, aiPosts, allPosts =
        if r2.Read() then r2.GetInt32(0), r2.GetInt32(1), r2.GetInt32(2)
        else 0, 0, 0
    r2.Close()

    use cmd3 = new NpgsqlCommand("""
        SELECT
            COUNT(*) FILTER (WHERE NOT u.is_bot)::int,
            COUNT(*) FILTER (WHERE u.is_bot)::int,
            COUNT(*)::int
        FROM forum_threads ft JOIN users u ON u.id = ft.author_id
    """, conn)
    use r3 = cmd3.ExecuteReader()
    let humanThreads, aiThreads, allThreads =
        if r3.Read() then r3.GetInt32(0), r3.GetInt32(1), r3.GetInt32(2)
        else 0, 0, 0
    r3.Close()

    use cmd4 = new NpgsqlCommand("""
        SELECT COUNT(*)::int FROM blog_articles WHERE status != 'deleted'
    """, conn)
    let allArticles = cmd4.ExecuteScalar() :?> int

    use cmd5 = new NpgsqlCommand("""
        SELECT SUM(vote_count)::int FROM forum_posts WHERE deleted_at IS NULL AND vote_count > 0
    """, conn)
    let totalVotes = match cmd5.ExecuteScalar() with | :? DBNull -> 0 | v -> v :?> int

    use cmd6 = new NpgsqlCommand("""
        SELECT COUNT(*)::int FROM user_achievements
    """, conn)
    let totalAchievements = cmd6.ExecuteScalar() :?> int

    let mkCounts users posts threads articles votes ach : MetricsCounts =
        { Users = users; Posts = posts; Threads = threads; Articles = articles
          VotesGiven = votes; Reactions = 0; Achievements = ach }

    let allCounts   = mkCounts allUsers   allPosts   allThreads   allArticles totalVotes totalAchievements
    let humanCounts = mkCounts humanUsers humanPosts humanThreads allArticles 0           0
    let aiCounts    = mkCounts aiUsers    aiPosts    aiThreads    0           0           0

    // ── Forum activity ────────────────────────────────────────────────────────
    use cmd7 = new NpgsqlCommand("""
        SELECT
            f.id,
            f.name,
            COUNT(fp.id)::int                                       AS posts_all,
            COUNT(fp.id) FILTER (WHERE NOT u.is_bot)::int          AS posts_human,
            COUNT(fp.id) FILTER (WHERE u.is_bot)::int              AS posts_ai,
            COUNT(DISTINCT ft.id)::int                             AS threads_all,
            COUNT(DISTINCT ft.id) FILTER (WHERE NOT u2.is_bot)::int AS threads_human,
            COUNT(DISTINCT ft.id) FILTER (WHERE u2.is_bot)::int    AS threads_ai
        FROM forum_forums f
        LEFT JOIN forum_threads ft ON ft.forum_id = f.id
        LEFT JOIN users u2 ON u2.id = ft.author_id
        LEFT JOIN forum_posts fp ON fp.thread_id = ft.id AND fp.deleted_at IS NULL
        LEFT JOIN users u ON u.id = fp.author_id
        GROUP BY f.id, f.name
        ORDER BY posts_all DESC
    """, conn)
    use r7 = cmd7.ExecuteReader()
    let forumActivity = [
        while r7.Read() do
            yield {
                ForumId      = r7.GetGuid(0)
                ForumName    = r7.GetString(1)
                PostsAll     = r7.GetInt32(2)
                PostsHuman   = r7.GetInt32(3)
                PostsAi      = r7.GetInt32(4)
                ThreadsAll   = r7.GetInt32(5)
                ThreadsHuman = r7.GetInt32(6)
                ThreadsAi    = r7.GetInt32(7)
            }
    ]
    r7.Close()

    // ── Daily activity (last 30 days) ─────────────────────────────────────────
    use cmd8 = new NpgsqlCommand("""
        SELECT
            DATE(fp.created_at)::text AS day,
            COUNT(*) FILTER (WHERE NOT u.is_bot)::int AS human,
            COUNT(*) FILTER (WHERE u.is_bot)::int     AS ai
        FROM forum_posts fp JOIN users u ON u.id = fp.author_id
        WHERE fp.deleted_at IS NULL AND fp.created_at >= now() - interval '30 days'
        GROUP BY day ORDER BY day
    """, conn)
    use r8 = cmd8.ExecuteReader()
    let dailyActivity = [
        while r8.Read() do
            yield { Date = r8.GetString(0); PostsHuman = r8.GetInt32(1); PostsAi = r8.GetInt32(2) }
    ]
    r8.Close()

    // ── Top humans ────────────────────────────────────────────────────────────
    use cmd9 = new NpgsqlCommand("""
        SELECT u.id, COALESCE(u.display_name, 'Anonymous'), u.is_bot,
               COALESCE(m.post_count,0), COALESCE(m.thread_count,0),
               COALESCE(m.vote_received,0), COALESCE(m.login_streak,0), COALESCE(m.days_active,0),
               COUNT(ua.id)::int AS ach_count
        FROM users u
        LEFT JOIN user_metrics m ON m.user_id = u.id
        LEFT JOIN user_achievements ua ON ua.user_id = u.id
        WHERE NOT u.is_bot AND u.status != 'deleted'
        GROUP BY u.id, u.display_name, u.is_bot, m.post_count, m.thread_count, m.vote_received, m.login_streak, m.days_active
        ORDER BY COALESCE(m.post_count,0) DESC
        LIMIT 25
    """, conn)
    use r9 = cmd9.ExecuteReader()
    let topHumans = [
        while r9.Read() do
            yield {
                UserId = r9.GetGuid(0); DisplayName = r9.GetString(1); IsBot = r9.GetBoolean(2)
                Posts = r9.GetInt32(3); Threads = r9.GetInt32(4); VotesReceived = r9.GetInt32(5)
                LoginStreak = r9.GetInt32(6); DaysActive = r9.GetInt32(7); Achievements = r9.GetInt32(8)
            }
    ]
    r9.Close()

    // ── Top bots ──────────────────────────────────────────────────────────────
    use cmd10 = new NpgsqlCommand("""
        SELECT u.id, COALESCE(u.display_name, u.email), u.is_bot,
               COALESCE(m.post_count,0), COALESCE(m.thread_count,0),
               COALESCE(m.vote_received,0), COALESCE(m.login_streak,0), COALESCE(m.days_active,0),
               0::int
        FROM users u
        LEFT JOIN user_metrics m ON m.user_id = u.id
        WHERE u.is_bot AND u.status != 'deleted'
        ORDER BY COALESCE(m.post_count,0) DESC
    """, conn)
    use r10 = cmd10.ExecuteReader()
    let topBots = [
        while r10.Read() do
            yield {
                UserId = r10.GetGuid(0); DisplayName = r10.GetString(1); IsBot = r10.GetBoolean(2)
                Posts = r10.GetInt32(3); Threads = r10.GetInt32(4); VotesReceived = r10.GetInt32(5)
                LoginStreak = r10.GetInt32(6); DaysActive = r10.GetInt32(7); Achievements = r10.GetInt32(8)
            }
    ]
    r10.Close()

    {|
        Totals        = {| All = allCounts; Human = humanCounts; Ai = aiCounts |}
        ForumActivity = forumActivity
        DailyActivity = dailyActivity
        TopHumans     = topHumans
        TopBots       = topBots
    |}

let getUserDrilldown (userId: Guid) =
    use conn = Database.openConnection ()
    use cmd = new NpgsqlCommand("""
        SELECT u.id, COALESCE(u.display_name,'Anonymous'), u.is_bot, u.email,
               COALESCE(m.post_count,0), COALESCE(m.thread_count,0),
               COALESCE(m.vote_received,0), 0,
               COALESCE(m.reaction_count,0), COALESCE(m.answer_count,0),
               COALESCE(m.login_streak,0), COALESCE(m.days_active,0),
               COALESCE(m.last_active_day::text,'')
        FROM users u LEFT JOIN user_metrics m ON m.user_id = u.id
        WHERE u.id = @uid
    """, conn)
    cmd.Parameters.AddWithValue("uid", userId) |> ignore
    use r = cmd.ExecuteReader()
    if not (r.Read()) then None
    else
        let base_ = {|
            UserId       = r.GetGuid(0)
            DisplayName  = r.GetString(1)
            IsBot        = r.GetBoolean(2)
            Email        = r.GetString(3)
            PostCount    = r.GetInt32(4)
            ThreadCount  = r.GetInt32(5)
            VoteReceived = r.GetInt32(6)
            VoteGiven    = r.GetInt32(7)
            ReactionCount= r.GetInt32(8)
            AnswerCount  = r.GetInt32(9)
            LoginStreak  = r.GetInt32(10)
            DaysActive   = r.GetInt32(11)
            LastActiveAt = r.GetString(12)
        |}
        r.Close()
        use cmd2 = new NpgsqlCommand("""
            SELECT ad.slug, ad.name, ad.icon, ad.tier, ua.awarded_at::text
            FROM user_achievements ua JOIN achievement_dictionary ad ON ad.id = ua.achievement_id
            WHERE ua.user_id = @uid ORDER BY ua.awarded_at
        """, conn)
        cmd2.Parameters.AddWithValue("uid", userId) |> ignore
        use r2 = cmd2.ExecuteReader()
        let achievements = [
            while r2.Read() do
                yield {| Slug = r2.GetString(0); Name = r2.GetString(1); Icon = r2.GetString(2)
                         Tier = r2.GetString(3); AwardedAt = r2.GetString(4) |}
        ]
        Some {|
            base_ with Achievements = achievements
        |}
