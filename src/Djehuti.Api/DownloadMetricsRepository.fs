module Djehuti.Api.DownloadMetricsRepository

open System
open Npgsql

type DownloadCount = {
    version: string
    count: int64
}

let logDownload (productId: Guid) (version: string) : unit =
    try
        use conn = Database.openConnection()
        use cmd = new NpgsqlCommand(
            "INSERT INTO download_metrics (product_id, version) VALUES (@productId, @version)",
            conn)
        cmd.Parameters.AddWithValue("productId", productId) |> ignore
        cmd.Parameters.AddWithValue("version", version) |> ignore
        cmd.ExecuteNonQuery() |> ignore
    with
    | ex ->
        printfn "Failed to log download: %s" ex.Message

let getDownloadsByVersion (productId: Guid) : DownloadCount list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        "SELECT version, COUNT(*) as count FROM download_metrics WHERE product_id = @productId GROUP BY version ORDER BY count DESC",
        conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- {
            version = reader.GetString(0)
            count = reader.GetInt64(1)
        } :: results
    List.rev results

let getTotalDownloads (productId: Guid) : int64 =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        "SELECT COUNT(*) FROM download_metrics WHERE product_id = @productId",
        conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    match cmd.ExecuteScalar() with
    | :? int64 as count -> count
    | _ -> 0L

let getRecentDownloads (productId: Guid) (days: int) : DownloadCount list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand(
        "SELECT version, COUNT(*) as count FROM download_metrics WHERE product_id = @productId AND timestamp > NOW() - INTERVAL '1 day' * @days GROUP BY version ORDER BY count DESC",
        conn)
    cmd.Parameters.AddWithValue("productId", productId) |> ignore
    cmd.Parameters.AddWithValue("days", days) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do
        results <- {
            version = reader.GetString(0)
            count = reader.GetInt64(1)
        } :: results
    List.rev results
