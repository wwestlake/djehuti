module Djehuti.Api.ProductReleaseRepository

open System
open Npgsql

type ReleaseAsset = {
    Name:       string
    Url:        string
    SizeBytes:  int64
}

type ReleaseRecord = {
    Id:          Guid
    ProductId:   Guid
    TagName:     string
    Name:        string option
    Body:        string option
    Prerelease:  bool
    Assets:      ReleaseAsset list
    PublishedAt: DateTimeOffset option
    CreatedAt:   DateTimeOffset
    TarballUrl:  string option
    ZipballUrl:  string option
}

let private readRelease (r: System.Data.Common.DbDataReader) : ReleaseRecord =
    {
        Id          = r.GetGuid(0)
        ProductId   = r.GetGuid(1)
        TagName     = r.GetString(2)
        Name        = if r.IsDBNull(3) then None else Some (r.GetString(3))
        Body        = if r.IsDBNull(4) then None else Some (r.GetString(4))
        Prerelease  = r.GetBoolean(5)
        Assets      =
            try System.Text.Json.JsonSerializer.Deserialize<ReleaseAsset list>(r.GetString(6))
            with _ -> []
        PublishedAt = if r.IsDBNull(7) then None else Some (r.GetFieldValue<DateTimeOffset>(7))
        CreatedAt   = r.GetFieldValue<DateTimeOffset>(8)
        TarballUrl  = if r.IsDBNull(9) then None else Some (r.GetString(9))
        ZipballUrl  = if r.IsDBNull(10) then None else Some (r.GetString(10))
    }

let private selectColumns = "id, product_id, tag_name, name, body, prerelease, assets_json, published_at, created_at, tarball_url, zipball_url"

let listForProduct (productId: Guid) : ReleaseRecord list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        SELECT {selectColumns} FROM product_releases
        WHERE product_id = @productId
        ORDER BY published_at DESC NULLS LAST, created_at DESC
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readRelease reader :: results
    List.rev results

// Upsert keyed on (product_id, tag_name) -- GitHub's "release edited" and
// "release published" events both land here, so a re-published/edited
// release updates the existing row instead of duplicating it.
//
// tarballUrl/zipballUrl are GitHub's auto-generated source archive links
// (release.tarball_url / release.zipball_url) -- distinct from `assets`,
// which only holds artifacts explicitly uploaded to the release (the MSI/ZIP).
//
// One real release publish (create + upload asset 1 + upload asset 2 +
// finalize) fires a SEPARATE "release" webhook event per API mutation --
// GitHub doesn't batch these -- and webhook delivery has no ordering
// guarantee, so djehuti can receive the 1-asset snapshot AFTER the
// 2-asset one. A plain overwrite is last-write-wins and can silently
// drop an asset that was already correctly recorded. Since the asset
// list only grows during that burst (shrinking only happens via the
// explicit deleteByTag path below, for real delete/unpublish events),
// keeping whichever snapshot has the most assets -- computed inside the
// same atomic UPDATE, not a separate read-then-write -- is safe against
// arrival order without needing to call back to GitHub's API to re-fetch
// ground truth.
let upsert (productId: Guid) (tagName: string) (name: string option) (body: string option) (prerelease: bool) (assets: ReleaseAsset list) (publishedAt: DateTimeOffset option) (tarballUrl: string option) (zipballUrl: string option) : unit =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO product_releases (product_id, tag_name, name, body, prerelease, assets_json, published_at, tarball_url, zipball_url)
        VALUES (@productId, @tagName, @name, @body, @prerelease, @assets, @publishedAt, @tarballUrl, @zipballUrl)
        ON CONFLICT (product_id, tag_name) DO UPDATE SET
            name = EXCLUDED.name,
            body = EXCLUDED.body,
            prerelease = EXCLUDED.prerelease,
            assets_json = CASE
                WHEN jsonb_array_length(EXCLUDED.assets_json::jsonb) >= jsonb_array_length(product_releases.assets_json::jsonb)
                THEN EXCLUDED.assets_json
                ELSE product_releases.assets_json
            END,
            published_at = EXCLUDED.published_at,
            tarball_url = EXCLUDED.tarball_url,
            zipball_url = EXCLUDED.zipball_url
    """, conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("tagName", tagName) |> ignore
    cmd.Parameters.AddWithValue("name", (name |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("body", (body |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("prerelease", prerelease) |> ignore
    cmd.Parameters.AddWithValue("assets", System.Text.Json.JsonSerializer.Serialize(assets)) |> ignore
    cmd.Parameters.AddWithValue("publishedAt", (publishedAt |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("tarballUrl", (tarballUrl |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("zipballUrl", (zipballUrl |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let deleteByTag (productId: Guid) (tagName: string) : unit =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("DELETE FROM product_releases WHERE product_id = @productId AND tag_name = @tagName", conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("tagName", tagName) |> ignore
    cmd.ExecuteNonQuery() |> ignore
