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

let getMetricTimeSeries (metric: string) : DailyActivityRow list =
    use conn = Database.openConnection ()
    let sql =
        match metric with
        | "posts" ->
            """SELECT DATE(created_at)::text, COUNT(*)::int, 0::int
               FROM forum_posts WHERE created_at >= now() - interval '30 days' AND deleted_at IS NULL
               GROUP BY 1 ORDER BY 1"""
        | "threads" ->
            """SELECT DATE(created_at)::text, COUNT(*)::int, 0::int
               FROM forum_threads WHERE created_at >= now() - interval '30 days'
               GROUP BY 1 ORDER BY 1"""
        | "members" ->
            """SELECT DATE(created_at)::text, COUNT(*)::int, 0::int
               FROM users WHERE created_at >= now() - interval '30 days' AND status != 'deleted'
               GROUP BY 1 ORDER BY 1"""
        | "articles" ->
            """SELECT DATE(created_at)::text, COUNT(*)::int, 0::int
               FROM blog_articles WHERE created_at >= now() - interval '30 days' AND status != 'deleted'
               GROUP BY 1 ORDER BY 1"""
        | "badges" ->
            """SELECT DATE(awarded_at)::text, COUNT(*)::int, 0::int
               FROM user_achievements WHERE awarded_at >= now() - interval '30 days'
               GROUP BY 1 ORDER BY 1"""
        | "upvotes" ->
            """SELECT DATE(created_at)::text, COUNT(*)::int, 0::int
               FROM forum_post_votes WHERE created_at >= now() - interval '30 days'
               GROUP BY 1 ORDER BY 1"""
        | _ -> "SELECT NULL::text, 0::int, 0::int WHERE false"
    use cmd = new NpgsqlCommand(sql, conn)
    use r = cmd.ExecuteReader()
    [ while r.Read() do
        yield { Date = r.GetString(0); PostsHuman = r.GetInt32(1); PostsAi = r.GetInt32(2) } ]

// ── Live activity tracking ────────────────────────────────────────────────────

let updateLastActivity (userId: string) =
    try
        use conn = Database.openConnection ()
        use cmd = new NpgsqlCommand("""
            UPDATE users SET last_activity_at = now()
            WHERE id = @id::uuid
              AND (last_activity_at IS NULL OR last_activity_at < now() - INTERVAL '1 minute')
        """, conn)
        cmd.Parameters.AddWithValue("id", userId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
    with _ -> ()

let getLiveMetrics () =
    use conn = Database.openConnection ()
    use loggedInCmd = new NpgsqlCommand("""
        SELECT COUNT(*)::int FROM users
        WHERE last_activity_at >= now() - INTERVAL '5 minutes'
    """, conn)
    let loggedIn = loggedInCmd.ExecuteScalar() :?> int
    use anonCmd = new NpgsqlCommand("""
        SELECT COUNT(DISTINCT ip_hash)::int FROM anonymous_page_views
        WHERE viewed_at >= now() - INTERVAL '5 minutes' AND source = 'beacon'
    """, conn)
    let anon = anonCmd.ExecuteScalar() :?> int
    {| LoggedIn = loggedIn; Anonymous = anon; Total = loggedIn + anon |}

// ── Anonymous visitor tracking ────────────────────────────────────────────────

let recordPageView (ipHash: string) (ipAddress: string) (path: string) (referrer: string) =
    try
        use conn = Database.openConnection ()
        use cmd = new NpgsqlCommand("""
            INSERT INTO anonymous_page_views (ip_hash, ip_address, path, referrer, source)
            VALUES (@ip, @ipaddr, @path, @ref, 'beacon')
        """, conn)
        cmd.Parameters.AddWithValue("ip",     ipHash)     |> ignore
        cmd.Parameters.AddWithValue("ipaddr", ipAddress)  |> ignore
        cmd.Parameters.AddWithValue("path",   path)       |> ignore
        cmd.Parameters.AddWithValue("ref",    referrer)   |> ignore
        cmd.ExecuteNonQuery() |> ignore
    with _ -> ()  // best-effort; never crash a request over analytics

let recordConversion (ipHash: string) (userId: Guid) =
    try
        use conn = Database.openConnection ()
        use cmd = new NpgsqlCommand("""
            INSERT INTO anonymous_conversions (ip_hash, user_id)
            VALUES (@ip, @uid)
            ON CONFLICT DO NOTHING
        """, conn)
        cmd.Parameters.AddWithValue("ip",  ipHash) |> ignore
        cmd.Parameters.AddWithValue("uid", userId) |> ignore
        cmd.ExecuteNonQuery() |> ignore
    with _ -> ()

[<CLIMutable>]
type TopThreadViewRow = { ThreadId: Guid; Title: string; ViewCount: int }

[<CLIMutable>]
type ReferrerRow = { Referrer: string; Visits: int }

let getAnonymousMetrics () =
    use conn = Database.openConnection ()

    // Unique anonymous visitors last 30 days
    use cmd1 = new NpgsqlCommand("""
        SELECT COUNT(DISTINCT ip_hash)::int
        FROM anonymous_page_views
        WHERE viewed_at >= now() - interval '30 days'
    """, conn)
    let uniqueVisitors30d = cmd1.ExecuteScalar() :?> int

    // Total unique visitors all time
    use cmd2 = new NpgsqlCommand("""
        SELECT COUNT(DISTINCT ip_hash)::int FROM anonymous_page_views
    """, conn)
    let uniqueVisitorsAllTime = cmd2.ExecuteScalar() :?> int

    // Top viewed thread paths last 30 days
    use cmd3 = new NpgsqlCommand("""
        SELECT
            pv.path,
            COUNT(*)::int AS views,
            ft.id,
            ft.title
        FROM anonymous_page_views pv
        LEFT JOIN forum_threads ft ON pv.path LIKE '%/threads/' || ft.id::text || '%'
        WHERE pv.viewed_at >= now() - interval '30 days'
          AND pv.path LIKE '%/threads/%'
        GROUP BY pv.path, ft.id, ft.title
        ORDER BY views DESC
        LIMIT 10
    """, conn)
    use r3 = cmd3.ExecuteReader()
    let topThreads = [
        while r3.Read() do
            let tid = if r3.IsDBNull(2) then Guid.Empty else r3.GetGuid(2)
            let title = if r3.IsDBNull(3) then r3.GetString(0) else r3.GetString(3)
            yield { ThreadId = tid; Title = title; ViewCount = r3.GetInt32(1) }
    ]
    r3.Close()

    // Referrer breakdown last 30 days (top 10, strip query strings)
    use cmd4 = new NpgsqlCommand("""
        SELECT
            CASE
                WHEN referrer = '' THEN 'Direct'
                WHEN referrer ILIKE '%quora%' THEN 'Quora'
                WHEN referrer ILIKE '%google%' THEN 'Google'
                WHEN referrer ILIKE '%reddit%' THEN 'Reddit'
                WHEN referrer ILIKE '%twitter%' OR referrer ILIKE '%t.co%' OR referrer ILIKE '%x.com%' THEN 'Twitter/X'
                WHEN referrer ILIKE '%linkedin%' THEN 'LinkedIn'
                WHEN referrer ILIKE '%github%' THEN 'GitHub'
                ELSE split_part(regexp_replace(referrer, '^https?://', ''), '/', 1)
            END AS source,
            COUNT(*)::int AS visits
        FROM anonymous_page_views
        WHERE viewed_at >= now() - interval '30 days'
        GROUP BY 1
        ORDER BY visits DESC
        LIMIT 10
    """, conn)
    use r4 = cmd4.ExecuteReader()
    let referrers = [
        while r4.Read() do
            yield { Referrer = r4.GetString(0); Visits = r4.GetInt32(1) }
    ]
    r4.Close()

    // Conversion rate last 30 days
    use cmd5 = new NpgsqlCommand("""
        SELECT
            COUNT(DISTINCT ip_hash)::int AS visitors,
            (SELECT COUNT(DISTINCT ip_hash)::int FROM anonymous_conversions
             WHERE converted_at >= now() - interval '30 days') AS conversions
        FROM anonymous_page_views
        WHERE viewed_at >= now() - interval '30 days'
    """, conn)
    use r5 = cmd5.ExecuteReader()
    let visitors30d, conversions30d =
        if r5.Read() then r5.GetInt32(0), r5.GetInt32(1) else 0, 0
    r5.Close()

    // Daily unique visitors last 30 days (for chart)
    use cmd6 = new NpgsqlCommand("""
        SELECT DATE(viewed_at)::text, COUNT(DISTINCT ip_hash)::int
        FROM anonymous_page_views
        WHERE viewed_at >= now() - interval '30 days'
        GROUP BY 1 ORDER BY 1
    """, conn)
    use r6 = cmd6.ExecuteReader()
    let dailyVisitors = [
        while r6.Read() do
            yield {| Date = r6.GetString(0); Count = r6.GetInt32(1) |}
    ]
    r6.Close()

    // Country breakdown last 30 days
    use cmd7 = new NpgsqlCommand("""
        SELECT country, COUNT(DISTINCT ip_hash)::int AS visitors
        FROM anonymous_page_views
        WHERE viewed_at >= now() - interval '30 days' AND country != ''
        GROUP BY country ORDER BY visitors DESC LIMIT 15
    """, conn)
    use r7 = cmd7.ExecuteReader()
    let countries = [
        while r7.Read() do
            yield {| Country = r7.GetString(0); Visitors = r7.GetInt32(1) |}
    ]
    r7.Close()

    // Top pages last 30 days
    use cmd8 = new NpgsqlCommand("""
        SELECT path, COUNT(*)::int AS views, COUNT(DISTINCT ip_hash)::int AS uniq
        FROM anonymous_page_views
        WHERE viewed_at >= now() - interval '30 days'
        GROUP BY path ORDER BY views DESC LIMIT 10
    """, conn)
    use r8 = cmd8.ExecuteReader()
    let topPages = [
        while r8.Read() do
            yield {| Path = r8.GetString(0); Views = r8.GetInt32(1); UniqueVisitors = r8.GetInt32(2) |}
    ]
    r8.Close()

    // Recent visitors (last 50, with geo)
    use cmd9 = new NpgsqlCommand("""
        SELECT DISTINCT ON (ip_hash)
            ip_address, country, region, city, domain, referrer, path, viewed_at::text
        FROM anonymous_page_views
        WHERE viewed_at >= now() - interval '30 days'
        ORDER BY ip_hash, viewed_at DESC
        LIMIT 50
    """, conn)
    use r9 = cmd9.ExecuteReader()
    let recentVisitors = [
        while r9.Read() do
            yield {|
                IpAddress = r9.GetString(0)
                Country   = r9.GetString(1)
                Region    = r9.GetString(2)
                City      = r9.GetString(3)
                Domain    = r9.GetString(4)
                Referrer  = r9.GetString(5)
                Path      = r9.GetString(6)
                ViewedAt  = r9.GetString(7)
            |}
    ]
    r9.Close()

    {|
        UniqueVisitors30d     = uniqueVisitors30d
        UniqueVisitorsAllTime = uniqueVisitorsAllTime
        Conversions30d        = conversions30d
        ConversionRatePct     = if visitors30d > 0 then float conversions30d / float visitors30d * 100.0 else 0.0
        TopThreads            = topThreads
        Referrers             = referrers
        DailyVisitors         = dailyVisitors
        Countries             = countries
        TopPages              = topPages
        RecentVisitors        = recentVisitors
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
