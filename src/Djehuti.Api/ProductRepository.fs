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
    GithubTagPrefix:    string option
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
        GithubTagPrefix     = if r.IsDBNull(10) then None else Some (r.GetString(10))
    }

let private selectColumns = "id, slug, name, description, required_tier_id, active, created_at, github_owner, github_repo, github_webhook_secret, github_tag_prefix"

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

// Multiple products can point at the same repo (one repo, several
// installers released under distinct tag prefixes) -- all of them so far
// only used for the shared-secret lookup below, and for admin-side listing
// of "who else is on this repo."
let findByRepo (owner: string) (repo: string) : ProductRecord list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        SELECT {selectColumns} FROM products
        WHERE lower(github_owner) = lower(@owner) AND lower(github_repo) = lower(@repo)
    """, conn)
    cmd.Parameters.AddWithValue("owner", owner) |> ignore
    cmd.Parameters.AddWithValue("repo", repo) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readProduct reader :: results
    List.rev results

// Which product a given release's tag belongs to, when a repo is shared.
// Matches the longest tag-prefix that's an actual prefix of tagName, so
// prefixes that happen to be prefixes of each other (unlikely, but not
// impossible) resolve to the more specific one rather than whichever the DB
// returned first. A product with no tag prefix set only matches if it's the
// sole product on that repo (single-product-per-repo, the original/common
// case, keeps working with zero configuration).
let resolveProductForTag (owner: string) (repo: string) (tagName: string) : ProductRecord option =
    let candidates = findByRepo owner repo
    match candidates with
    | [ only ] when only.GithubTagPrefix.IsNone -> Some only
    | _ ->
        candidates
        |> List.choose (fun p -> p.GithubTagPrefix |> Option.filter tagName.StartsWith |> Option.map (fun prefix -> prefix, p))
        |> List.sortByDescending (fun (prefix, _) -> prefix.Length)
        |> List.tryHead
        |> Option.map snd

// Points a product at a GitHub repo (+ optional tag prefix, required once a
// repo has more than one product on it) and sets its webhook secret. GitHub
// only signs a given repo's webhook payloads with ONE secret, so if another
// product already shares this (owner, repo), reuse its secret instead of
// generating a new one -- otherwise verification would only ever pass for
// whichever product happened to be linked first.
let setGithubRepo (id: Guid) (owner: string) (repo: string) (tagPrefix: string option) : string =
    let existingSecret =
        findByRepo owner repo
        |> List.filter (fun p -> p.Id <> id)
        |> List.tryPick (fun p -> p.GithubWebhookSecret)
    let secret =
        existingSecret
        |> Option.defaultWith (fun () -> Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant())
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE products
        SET github_owner = @owner, github_repo = @repo, github_webhook_secret = @secret, github_tag_prefix = @prefix
        WHERE id = @id
    """, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("owner", owner) |> ignore
    cmd.Parameters.AddWithValue("repo", repo) |> ignore
    cmd.Parameters.AddWithValue("secret", secret) |> ignore
    cmd.Parameters.AddWithValue("prefix", (tagPrefix |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    secret

let clearGithubRepo (id: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE products
        SET github_owner = NULL, github_repo = NULL, github_webhook_secret = NULL, github_tag_prefix = NULL
        WHERE id = @id
    """, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0
