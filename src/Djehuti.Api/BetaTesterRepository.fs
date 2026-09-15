module Djehuti.Api.BetaTesterRepository

open System
open Npgsql
open Database

type BetaTesterWithUser =
    { Id:             Guid
      UserId:         Guid
      DisplayName:    string
      Status:         string
      JoinedAt:       DateTime
      LastFeedbackAt: DateTime
      DroppedAt:      DateTime option }

// Lazily expires stale active rows instead of relying on a background
// worker -- background workers are disabled in this codebase (see
// Program.fs "DISABLED: Background workers causing database bloat"), so
// this runs as a cheap indexed UPDATE from the request paths that touch
// beta_testers rather than on a schedule. effective_tier_id() already
// stops granting the Curious Mind overlay once last_feedback_at is more
// than 30 days old regardless of whether this has run yet; this just keeps
// beta_testers.status accurate for admin display.
let sweepExpired () : int =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE beta_testers
        SET status = 'dropped', dropped_at = now()
        WHERE status = 'active' AND last_feedback_at < now() - interval '30 days'
    """, conn)
    cmd.ExecuteNonQuery()

// Enroll, or re-enroll after a prior drop, as an active beta tester for one
// product. Re-signup after a drop is a full reset (fresh 30-day clock, no
// penalty) rather than a distinct code path. invitedBy is Some for an
// admin-initiated invite, None for public self-signup; an invite never
// overwrites who originally invited someone on a later self-resignup.
let upsertActive (userId: Guid) (productId: Guid) (invitedBy: Guid option) : unit =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO beta_testers (user_id, product_id, status, joined_at, last_feedback_at, invited_by)
        VALUES (@userId, @productId, 'active', now(), now(), @invitedBy)
        ON CONFLICT (user_id, product_id) DO UPDATE
        SET status = 'active', last_feedback_at = now(), dropped_at = NULL,
            invited_by = COALESCE(beta_testers.invited_by, EXCLUDED.invited_by)
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("invitedBy", (invitedBy |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.ExecuteNonQuery() |> ignore

// Resets an active tester's 30-day clock when they submit feedback for that
// product. A no-op (WHERE matches zero rows) for an anonymous submitter or
// anyone who isn't an active tester for that product.
let bumpLastFeedback (userId: Guid) (productId: Guid) : unit =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE beta_testers SET last_feedback_at = now()
        WHERE user_id = @userId AND product_id = @productId AND status = 'active'
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.ExecuteNonQuery() |> ignore

// Never selects u.email -- per AGENTS.md, email addresses are never
// displayed in the UI; the display-name fallback chain (user_profiles ->
// users -> 'Anonymous') matches PatreonService.getSupporters.
let private readWithUser (r: System.Data.Common.DbDataReader) : BetaTesterWithUser =
    { Id             = r.GetGuid(0)
      UserId         = r.GetGuid(1)
      DisplayName    = r.GetString(2)
      Status         = r.GetString(3)
      JoinedAt       = r.GetFieldValue<DateTime>(4)
      LastFeedbackAt = r.GetFieldValue<DateTime>(5)
      DroppedAt      = if r.IsDBNull(6) then None else Some (r.GetFieldValue<DateTime>(6)) }

// Same active-within-30-days rule effective_tier_id() itself enforces --
// used to decide whether a session should see the gated Downloads page at
// all, independent of which specific product(s) they're testing.
let isActiveForAnyProduct (userId: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT EXISTS (
               SELECT 1 FROM beta_testers
               WHERE user_id = @userId AND status = 'active' AND last_feedback_at > now() - interval '30 days'
           )""", conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.ExecuteScalar() :?> bool

// For the update-notification endpoint. Email, not display name, is
// correct here -- this is used to actually send mail, not to render
// anything in the UI, so it doesn't touch the no-email-in-UI rule.
let listActiveEmailsForProduct (productId: Guid) : string list =
    sweepExpired () |> ignore
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        """SELECT u.email
           FROM beta_testers bt
           JOIN users u ON u.id = bt.user_id
           WHERE bt.product_id = @productId AND bt.status = 'active'""", conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- reader.GetString(0) :: results
    List.rev results

let listForProduct (productId: Guid) : BetaTesterWithUser list =
    sweepExpired () |> ignore
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT bt.id, bt.user_id, COALESCE(up.display_name, u.display_name, 'Anonymous'), bt.status, bt.joined_at, bt.last_feedback_at, bt.dropped_at
        FROM beta_testers bt
        JOIN users u ON u.id = bt.user_id
        LEFT JOIN user_profiles up ON up.user_id = u.id
        WHERE bt.product_id = @productId
        ORDER BY bt.status ASC, bt.joined_at DESC
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readWithUser reader :: results
    List.rev results
