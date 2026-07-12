module Djehuti.Api.ProductRepository

open System
open Npgsql

type ProductRecord = {
    Id:                 Guid
    Slug:               string
    Name:               string
    Description:        string option
    RequiredTierId:     string option
    Active:             bool
    CreatedAt:          DateTimeOffset
    GithubOwner:        string option
    GithubRepo:         string option
    GithubWebhookSecret: string option
}

let private readProduct (r: System.Data.Common.DbDataReader) : ProductRecord =
    {
        Id                  = r.GetGuid(0)
        Slug                = r.GetString(1)
        Name                = r.GetString(2)
        Description         = if r.IsDBNull(3) then None else Some (r.GetString(3))
        RequiredTierId      = if r.IsDBNull(4) then None else Some (r.GetString(4))
        Active              = r.GetBoolean(5)
        CreatedAt           = r.GetFieldValue<DateTimeOffset>(6)
        GithubOwner         = if r.IsDBNull(7) then None else Some (r.GetString(7))
        GithubRepo          = if r.IsDBNull(8) then None else Some (r.GetString(8))
        GithubWebhookSecret = if r.IsDBNull(9) then None else Some (r.GetString(9))
    }

let private selectColumns = "id, slug, name, description, required_tier_id, active, created_at, github_owner, github_repo, github_webhook_secret"

let listAll () : ProductRecord list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectColumns} FROM products ORDER BY created_at DESC", conn)
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readProduct reader :: results
    List.rev results

let listActive () : ProductRecord list =
    listAll () |> List.filter (fun p -> p.Active)

let create (slug: string) (name: string) (description: string option) (requiredTierId: string option) : ProductRecord =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        INSERT INTO products (slug, name, description, required_tier_id)
        VALUES (@slug, @name, @description, @tier)
        RETURNING {selectColumns}
    """, conn)
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("description", (description |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("tier", (requiredTierId |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    readProduct reader

let update (id: Guid) (name: string) (description: string option) (requiredTierId: string option) (active: bool) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE products
        SET name = @name, description = @description, required_tier_id = @tier, active = @active
        WHERE id = @id
    """, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("description", (description |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("tier", (requiredTierId |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("active", active) |> ignore
    cmd.ExecuteNonQuery() > 0

let delete (id: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("DELETE FROM products WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0

// Which product slugs does this user currently qualify for? A product with
// no RequiredTierId is open to everyone (signed in or not, in principle --
// this only gets called for signed-in users today, but the rule itself
// doesn't depend on that). Otherwise the user's own current tier must be at
// or above the product's required tier, compared by display_order (the
// same tier-ranking column added in migration 66) -- "Professor" (order 4)
// satisfies a product that requires "Lab Assistant" (order 2), not the
// other way around.
let getEntitlements (userId: Guid) : string list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        SELECT p.slug, p.required_tier_id, req.display_order, own.display_order
        FROM products p
        LEFT JOIN patreon_tiers req ON req.tier_id = p.required_tier_id
        LEFT JOIN users u ON u.id = @userId
        LEFT JOIN patreon_tiers own ON own.tier_id = u.patreon_tier_id
        WHERE p.active = TRUE
    """, conn)
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        let slug = reader.GetString(0)
        let requiresTier = not (reader.IsDBNull(1))
        let qualifies =
            if not requiresTier then true
            elif reader.IsDBNull(3) then false // user has no tier at all
            else reader.GetInt32(3) >= reader.GetInt32(2)
        if qualifies then results <- slug :: results
    List.rev results

// Points a product at a GitHub repo and (re)generates its webhook secret --
// called once when the admin sets/changes the repo, and again if they want
// to rotate the secret (e.g. after accidentally pasting it somewhere). The
// secret is what GitHub signs release-webhook payloads with (X-Hub-Signature-256),
// verified in the webhook handler; one per product so rotating one repo's
// hook never invalidates another product's.
let setGithubRepo (id: Guid) (owner: string) (repo: string) : string =
    let secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE products
        SET github_owner = @owner, github_repo = @repo, github_webhook_secret = @secret
        WHERE id = @id
    """, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("owner", owner) |> ignore
    cmd.Parameters.AddWithValue("repo", repo) |> ignore
    cmd.Parameters.AddWithValue("secret", secret) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    secret

let clearGithubRepo (id: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE products
        SET github_owner = NULL, github_repo = NULL, github_webhook_secret = NULL
        WHERE id = @id
    """, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0

let tryFindByRepo (owner: string) (repo: string) : ProductRecord option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        SELECT {selectColumns} FROM products
        WHERE lower(github_owner) = lower(@owner) AND lower(github_repo) = lower(@repo)
    """, conn)
    cmd.Parameters.AddWithValue("owner", owner) |> ignore
    cmd.Parameters.AddWithValue("repo", repo) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readProduct reader) else None
