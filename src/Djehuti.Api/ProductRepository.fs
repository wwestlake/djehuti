module Djehuti.Api.ProductRepository

open System
open Npgsql

type ProductRecord = {
    Id:             Guid
    Slug:           string
    Name:           string
    Description:    string option
    RequiredTierId: string option
    Active:         bool
    CreatedAt:      DateTimeOffset
}

let private readProduct (r: System.Data.Common.DbDataReader) : ProductRecord =
    {
        Id             = r.GetGuid(0)
        Slug           = r.GetString(1)
        Name           = r.GetString(2)
        Description    = if r.IsDBNull(3) then None else Some (r.GetString(3))
        RequiredTierId = if r.IsDBNull(4) then None else Some (r.GetString(4))
        Active         = r.GetBoolean(5)
        CreatedAt      = r.GetFieldValue<DateTimeOffset>(6)
    }

let private selectColumns = "id, slug, name, description, required_tier_id, active, created_at"

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
