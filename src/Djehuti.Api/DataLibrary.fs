module Djehuti.Api.DataLibrary

open System
open System.Text.Json
open Npgsql

// ── Types ────────────────────────────────────────────────────────────────────

type DataSetCatalogItem =
    { Id: string
      Name: string
      Description: string
      SourceKind: string
      TurnCount: Nullable<int>
      Status: string
      CreatedAt: DateTimeOffset }

// ── Helpers ──────────────────────────────────────────────────────────────────

let private jsonOptions = JsonSerializerOptions(PropertyNameCaseInsensitive = true)

let private openConn () = Database.openConnection ()

// ── Catalog ──────────────────────────────────────────────────────────────────

let catalog () : DataSetCatalogItem list =
    use conn = openConn ()
    use cmd = new NpgsqlCommand(
        "SELECT id, name, COALESCE(notes,'') as description, source_kind,
                turn_count, status, created_at
         FROM datasets
         ORDER BY created_at DESC", conn)
    use reader = cmd.ExecuteReader()
    let mutable items = []
    while reader.Read() do
        items <- items @ [{
            Id          = reader.GetGuid(0).ToString()
            Name        = reader.GetString(1)
            Description = reader.GetString(2)
            SourceKind  = reader.GetString(3)
            TurnCount   = if reader.IsDBNull(4) then Nullable() else Nullable(reader.GetInt32(4))
            Status      = reader.GetString(5)
            CreatedAt   = reader.GetFieldValue<DateTimeOffset>(6)
        }]
    items

// ── Save ─────────────────────────────────────────────────────────────────────

let saveDataSet (name: string) (description: string) (sourceKind: string) (turnCount: int) (datasetJson: string) =
    try
        // Parse the JSON to extract interactions and metadata
        let doc = JsonDocument.Parse(datasetJson)
        let root = doc.RootElement

        let sourceId =
            match root.TryGetProperty("source") with
            | true, src ->
                match src.TryGetProperty("id") with
                | true, v -> v.GetString()
                | _ -> Guid.NewGuid().ToString()
            | _ -> Guid.NewGuid().ToString()

        let distanceMetric =
            match root.TryGetProperty("constants") with
            | true, c ->
                match c.TryGetProperty("distanceMetric") with
                | true, v -> v.GetString()
                | _ -> null
            | _ -> null

        let conversationType =
            match root.TryGetProperty("constants") with
            | true, c ->
                match c.TryGetProperty("conversationType") with
                | true, v -> v.GetString()
                | _ -> null
            | _ -> null

        let interactions =
            match root.TryGetProperty("interactions") with
            | true, arr -> arr.EnumerateArray() |> Seq.toList
            | _ -> []

        let actualTurnCount = if interactions.Length > 0 then interactions.Length else turnCount

        use conn = openConn ()
        use txn = conn.BeginTransaction()

        // Insert dataset row
        use dsCmd = new NpgsqlCommand(
            """INSERT INTO datasets
                   (name, source_id, source_kind, turn_count,
                    distance_metric, conversation_type, status, notes)
               VALUES (@name, @source_id, @source_kind, @turn_count,
                       @distance_metric, @conversation_type, 'complete', @notes)
               RETURNING id, created_at""", conn, txn)
        dsCmd.Parameters.AddWithValue("name", name) |> ignore
        dsCmd.Parameters.AddWithValue("source_id", sourceId) |> ignore
        dsCmd.Parameters.AddWithValue("source_kind", sourceKind) |> ignore
        dsCmd.Parameters.AddWithValue("turn_count", actualTurnCount) |> ignore
        dsCmd.Parameters.AddWithValue("distance_metric", if distanceMetric <> null then box distanceMetric else box DBNull.Value) |> ignore
        dsCmd.Parameters.AddWithValue("conversation_type", if conversationType <> null then box conversationType else box DBNull.Value) |> ignore
        dsCmd.Parameters.AddWithValue("notes", if String.IsNullOrWhiteSpace description then box DBNull.Value else box description) |> ignore

        use dsReader = dsCmd.ExecuteReader()
        dsReader.Read() |> ignore
        let datasetId = dsReader.GetGuid(0)
        let createdAt = dsReader.GetFieldValue<DateTimeOffset>(1)
        dsReader.Close()

        // Insert interactions
        for interaction in interactions do
            use intCmd = new NpgsqlCommand(
                """INSERT INTO interactions
                       (dataset_id, session_id, model_id, sequence_index, prompt, response)
                   VALUES (@dataset_id, @session_id, @model_id, @sequence_index, @prompt, @response)
                   ON CONFLICT (dataset_id, sequence_index) DO NOTHING""", conn, txn)
            let getStr (prop: string) =
                match (interaction : System.Text.Json.JsonElement).TryGetProperty(prop) with
                | true, v -> v.GetString()
                | _ -> ""
            let getInt (prop: string) =
                match (interaction : System.Text.Json.JsonElement).TryGetProperty(prop) with
                | true, v -> v.GetInt32()
                | _ -> 0
            intCmd.Parameters.AddWithValue("dataset_id", datasetId) |> ignore
            intCmd.Parameters.AddWithValue("session_id", getStr "sessionId") |> ignore
            intCmd.Parameters.AddWithValue("model_id", getStr "modelId") |> ignore
            intCmd.Parameters.AddWithValue("sequence_index", getInt "sequenceIndex") |> ignore
            intCmd.Parameters.AddWithValue("prompt", getStr "prompt") |> ignore
            intCmd.Parameters.AddWithValue("response", getStr "response") |> ignore
            intCmd.ExecuteNonQuery() |> ignore

        txn.Commit()

        Ok { Id          = datasetId.ToString()
             Name        = name
             Description = description
             SourceKind  = sourceKind
             TurnCount   = Nullable(actualTurnCount)
             Status      = "complete"
             CreatedAt   = createdAt }
    with ex ->
        Error $"Failed to save dataset: {ex.Message}"

// ── Rename ───────────────────────────────────────────────────────────────────

let renameDataSet (id: string) (name: string) (description: string) =
    try
        use conn = openConn ()
        use cmd = new NpgsqlCommand(
            """UPDATE datasets
               SET name = @name, notes = @notes
               WHERE id = @id::uuid
               RETURNING id, name, COALESCE(notes,''), source_kind, turn_count, status, created_at""", conn)
        cmd.Parameters.AddWithValue("id", id) |> ignore
        cmd.Parameters.AddWithValue("name", name.Trim()) |> ignore
        cmd.Parameters.AddWithValue("notes", if String.IsNullOrWhiteSpace description then box DBNull.Value else box (description.Trim())) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then
            Ok { Id          = reader.GetGuid(0).ToString()
                 Name        = reader.GetString(1)
                 Description = reader.GetString(2)
                 SourceKind  = reader.GetString(3)
                 TurnCount   = if reader.IsDBNull(4) then Nullable() else Nullable(reader.GetInt32(4))
                 Status      = reader.GetString(5)
                 CreatedAt   = reader.GetFieldValue<DateTimeOffset>(6) }
        else
            Error $"Dataset '{id}' was not found."
    with ex ->
        Error $"Failed to rename dataset: {ex.Message}"

// ── Delete ───────────────────────────────────────────────────────────────────

let deleteDataSet (id: string) =
    try
        use conn = openConn ()
        use cmd = new NpgsqlCommand("DELETE FROM datasets WHERE id = @id::uuid", conn)
        cmd.Parameters.AddWithValue("id", id) |> ignore
        let rows = cmd.ExecuteNonQuery()
        if rows > 0 then Ok()
        else Error $"Dataset '{id}' was not found."
    with ex ->
        Error $"Failed to delete dataset: {ex.Message}"

// ── Read ─────────────────────────────────────────────────────────────────────

let tryReadDataSet (id: string) =
    try
        use conn = openConn ()

        // Read dataset metadata
        use dsCmd = new NpgsqlCommand(
            """SELECT id, name, source_id, source_kind, distance_metric,
                      conversation_type, turn_count, status
               FROM datasets WHERE id = @id::uuid""", conn)
        dsCmd.Parameters.AddWithValue("id", id) |> ignore
        use dsReader = dsCmd.ExecuteReader()
        if not (dsReader.Read()) then
            Error $"Dataset '{id}' was not found."
        else
            let datasetId  = dsReader.GetGuid(0).ToString()
            let name       = dsReader.GetString(1)
            let sourceId   = dsReader.GetString(2)
            let sourceKind = dsReader.GetString(3)
            let metric     = if dsReader.IsDBNull(4) then "cosine" else dsReader.GetString(4)
            let convType   = if dsReader.IsDBNull(5) then null else dsReader.GetString(5)
            let turnCount  = if dsReader.IsDBNull(6) then 0 else dsReader.GetInt32(6)
            dsReader.Close()

            // Read interactions
            use intCmd = new NpgsqlCommand(
                """SELECT session_id, model_id, sequence_index, prompt, response
                   FROM interactions
                   WHERE dataset_id = @id::uuid
                   ORDER BY sequence_index""", conn)
            intCmd.Parameters.AddWithValue("id", id) |> ignore
            use intReader = intCmd.ExecuteReader()

            let mutable interactions = []
            while intReader.Read() do
                interactions <- interactions @ [
                    {| sessionId     = intReader.GetString(0)
                       modelId       = intReader.GetString(1)
                       sequenceIndex = intReader.GetInt32(2)
                       prompt        = intReader.GetString(3)
                       response      = intReader.GetString(4) |}
                ]

            // Reconstruct JSON in the format the analysis pipeline expects
            let constants =
                let d = Collections.Generic.Dictionary<string, obj>()
                d["distanceMetric"] <- metric
                if not (String.IsNullOrWhiteSpace convType) then
                    d["conversationType"] <- convType
                d["turnCount"] <- turnCount
                d

            let payload = {|
                source = {| id = sourceId; kind = sourceKind; name = name |}
                constants = constants
                interactions = interactions
            |}

            let writeOptions = JsonSerializerOptions(WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            Ok(JsonSerializer.Serialize(payload, writeOptions))
    with ex ->
        Error $"Failed to read dataset: {ex.Message}"

// ── Import from JSON files ────────────────────────────────────────────────────

let importFromFile (filePath: string) (name: string) (description: string) =
    try
        let json = System.IO.File.ReadAllText(filePath)
        let doc = JsonDocument.Parse(json)
        let turnCount =
            match doc.RootElement.TryGetProperty("interactions") with
            | true, arr -> arr.GetArrayLength()
            | _ -> 0
        let sourceKind =
            match doc.RootElement.TryGetProperty("source") with
            | true, src ->
                match src.TryGetProperty("kind") with
                | true, v -> v.GetString()
                | _ -> "replay-file"
            | _ -> "replay-file"
        saveDataSet name description sourceKind turnCount json
    with ex ->
        Error $"Failed to import '{filePath}': {ex.Message}"
