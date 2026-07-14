module Djehuti.Api.ContentRepository

open System
open Npgsql

type ContentItem = {
    Id:             Guid
    ProductId:      Guid
    Name:           string
    ItemType:       string
    Version:        string
    Description:    string option
    Tags:           string list
    RequiredTierId: string option
    MinAppVersion:  string option
    FileType:       string
    FileName:       string option
    SizeBytes:      int64
    Active:         bool
    CreatedAt:      DateTimeOffset
    UpdatedAt:      DateTimeOffset
    HasFile:        bool
}

type AccessState = Available | Locked

type ContentItemWithAccess = {
    Item:        ContentItem
    AccessState: AccessState
}

let private accessStateJson = function Available -> "available" | Locked -> "locked"

let private readItem (r: System.Data.Common.DbDataReader) : ContentItem =
    {
        Id             = r.GetGuid(0)
        ProductId      = r.GetGuid(1)
        Name           = r.GetString(2)
        ItemType       = r.GetString(3)
        Version        = r.GetString(4)
        Description    = if r.IsDBNull(5) then None else Some (r.GetString(5))
        Tags           = if r.IsDBNull(6) then [] else r.GetFieldValue<string[]>(6) |> Array.toList
        RequiredTierId = if r.IsDBNull(7) then None else Some (r.GetString(7))
        MinAppVersion  = if r.IsDBNull(8) then None else Some (r.GetString(8))
        FileType       = r.GetString(9)
        FileName       = if r.IsDBNull(10) then None else Some (r.GetString(10))
        SizeBytes      = r.GetInt64(11)
        Active         = r.GetBoolean(12)
        CreatedAt      = r.GetFieldValue<DateTimeOffset>(13)
        UpdatedAt      = r.GetFieldValue<DateTimeOffset>(14)
        HasFile        = not (r.IsDBNull(15))
    }

let private selectColumns =
    "id, product_id, name, item_type, version, description, tags, required_tier_id, min_app_version, \
     file_type, file_name, size_bytes, active, created_at, updated_at, file_data"

let private selectColumnsAliased =
    "ci.id, ci.product_id, ci.name, ci.item_type, ci.version, ci.description, ci.tags, ci.required_tier_id, ci.min_app_version, \
     ci.file_type, ci.file_name, ci.size_bytes, ci.active, ci.created_at, ci.updated_at, ci.file_data"

// display_order comparison, same ranking logic as ProductRepository.getEntitlements --
// a required_tier_id of NULL is free/always available; otherwise the user's own
// current tier must rank at or above the item's required tier.
let private resolveAccessState (requiredDisplayOrder: int option) (ownDisplayOrder: int option) =
    match requiredDisplayOrder with
    | None -> Available
    | Some req ->
        match ownDisplayOrder with
        | Some own when own >= req -> Available
        | _ -> Locked

let listLibrary (productId: Guid) (userId: Guid) : ContentItemWithAccess list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        SELECT {selectColumnsAliased},
            req.display_order, own.display_order
        FROM content_items ci
        LEFT JOIN patreon_tiers req ON req.tier_id = ci.required_tier_id
        LEFT JOIN users u ON u.id = @userId
        LEFT JOIN patreon_tiers own ON own.tier_id = u.patreon_tier_id
        WHERE ci.product_id = @productId AND ci.active = TRUE
        ORDER BY ci.created_at DESC
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        let item = readItem reader
        let reqOrder = if reader.IsDBNull(16) then None else Some (reader.GetInt32(16))
        let ownOrder = if reader.IsDBNull(17) then None else Some (reader.GetInt32(17))
        results <- { Item = item; AccessState = resolveAccessState reqOrder ownOrder } :: results
    List.rev results

let tryGetWithAccess (id: Guid) (userId: Guid) : ContentItemWithAccess option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        SELECT {selectColumnsAliased},
            req.display_order, own.display_order
        FROM content_items ci
        LEFT JOIN patreon_tiers req ON req.tier_id = ci.required_tier_id
        LEFT JOIN users u ON u.id = @userId
        LEFT JOIN patreon_tiers own ON own.tier_id = u.patreon_tier_id
        WHERE ci.id = @id AND ci.active = TRUE
    """, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        let item = readItem reader
        let reqOrder = if reader.IsDBNull(16) then None else Some (reader.GetInt32(16))
        let ownOrder = if reader.IsDBNull(17) then None else Some (reader.GetInt32(17))
        Some { Item = item; AccessState = resolveAccessState reqOrder ownOrder }
    else None

// ── Admin: metadata CRUD (no file bytes touched here) ──────────────────────

let listAllForProduct (productId: Guid) : ContentItem list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectColumns} FROM content_items WHERE product_id = @productId ORDER BY created_at DESC", conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readItem reader :: results
    List.rev results

let tryGetById (id: Guid) : ContentItem option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectColumns} FROM content_items WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readItem reader) else None

let create (productId: Guid) (name: string) (itemType: string) (version: string) (description: string option)
           (tags: string list) (requiredTierId: string option) (minAppVersion: string option) (fileType: string) : ContentItem =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        INSERT INTO content_items (product_id, name, item_type, version, description, tags, required_tier_id, min_app_version, file_type)
        VALUES (@productId, @name, @itemType, @version, @description, @tags, @tier, @minVersion, @fileType)
        RETURNING {selectColumns}
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("itemType", itemType) |> ignore
    cmd.Parameters.AddWithValue("version", version) |> ignore
    cmd.Parameters.AddWithValue("description", (description |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("tags", tags |> List.toArray) |> ignore
    cmd.Parameters.AddWithValue("tier", (requiredTierId |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("minVersion", (minAppVersion |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("fileType", fileType) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    readItem reader

let update (id: Guid) (name: string) (itemType: string) (version: string) (description: string option)
           (tags: string list) (requiredTierId: string option) (minAppVersion: string option) (fileType: string) (active: bool) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE content_items
        SET name = @name, item_type = @itemType, version = @version, description = @description,
            tags = @tags, required_tier_id = @tier, min_app_version = @minVersion, file_type = @fileType,
            active = @active, updated_at = now()
        WHERE id = @id
    """, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("itemType", itemType) |> ignore
    cmd.Parameters.AddWithValue("version", version) |> ignore
    cmd.Parameters.AddWithValue("description", (description |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("tags", tags |> List.toArray) |> ignore
    cmd.Parameters.AddWithValue("tier", (requiredTierId |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("minVersion", (minAppVersion |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("fileType", fileType) |> ignore
    cmd.Parameters.AddWithValue("active", active) |> ignore
    cmd.ExecuteNonQuery() > 0

let delete (id: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("DELETE FROM content_items WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.ExecuteNonQuery() > 0

// ── File bytes (uploaded/downloaded separately from metadata) ──────────────

let setFile (id: Guid) (fileName: string) (data: byte[]) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        UPDATE content_items
        SET file_data = @data, file_name = @fileName, size_bytes = @size, updated_at = now()
        WHERE id = @id
    """, conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("data", data) |> ignore
    cmd.Parameters.AddWithValue("fileName", fileName) |> ignore
    cmd.Parameters.AddWithValue("size", int64 data.Length) |> ignore
    cmd.ExecuteNonQuery() > 0

// (bytes, fileName, fileType) -- the actual download payload, fetched only
// after the caller has already confirmed AccessState = Available via
// tryGetWithAccess. This function itself does not re-check entitlement --
// it trusts the caller, same as every other "fetch the bytes" helper in
// this codebase (e.g. DjeLabFilesRepository).
let tryGetFileData (id: Guid) : (byte[] * string * string) option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("SELECT file_data, file_name, file_type FROM content_items WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() && not (reader.IsDBNull(0)) then
        let data = reader.GetFieldValue<byte[]>(0)
        let fileName = if reader.IsDBNull(1) then "download" else reader.GetString(1)
        let fileType = reader.GetString(2)
        Some (data, fileName, fileType)
    else None
