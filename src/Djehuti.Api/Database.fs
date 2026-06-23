module Djehuti.Api.Database

open System
open Npgsql

// ── Connection ───────────────────────────────────────────────────────────────

let connectionString () =
    let s = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    if String.IsNullOrWhiteSpace(s) then
        failwith "DB_CONNECTION_STRING environment variable is not set"
    s

let openConnection () =
    let conn = new NpgsqlConnection(connectionString ())
    conn.Open()
    conn

// ── Migrations ───────────────────────────────────────────────────────────────

// Each migration is (version, sql). Applied in order; skipped if already recorded.
let private migrations : (int * string) list =
    [
        1, """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version     INT PRIMARY KEY,
            applied_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """

        2, """
        CREATE TABLE IF NOT EXISTS datasets (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            name                TEXT NOT NULL,
            source_id           TEXT NOT NULL,
            source_kind         TEXT NOT NULL,
            model_id            TEXT,
            turn_count          INT,
            distance_metric     TEXT,
            conversation_type   TEXT,
            status              TEXT NOT NULL DEFAULT 'complete',
            notes               TEXT,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """

        3, """
        CREATE TABLE IF NOT EXISTS interactions (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            dataset_id      UUID NOT NULL REFERENCES datasets(id) ON DELETE CASCADE,
            session_id      TEXT NOT NULL,
            model_id        TEXT,
            sequence_index  INT NOT NULL,
            prompt          TEXT NOT NULL,
            response        TEXT NOT NULL,
            metadata        JSONB,
            UNIQUE (dataset_id, sequence_index)
        );

        CREATE INDEX IF NOT EXISTS idx_interactions_dataset_id
            ON interactions(dataset_id);

        CREATE INDEX IF NOT EXISTS idx_interactions_prompt_search
            ON interactions USING gin(to_tsvector('english', prompt || ' ' || response));
        """

        4, """
        CREATE TABLE IF NOT EXISTS analysis_runs (
            id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            dataset_id           UUID NOT NULL REFERENCES datasets(id) ON DELETE CASCADE,
            run_at               TIMESTAMPTZ NOT NULL DEFAULT now(),
            turn_count_analyzed  INT,
            constants            JSONB,
            summary              JSONB
        );

        CREATE INDEX IF NOT EXISTS idx_analysis_runs_dataset_id
            ON analysis_runs(dataset_id);
        """

        5, """
        CREATE TABLE IF NOT EXISTS attractor_events (
            id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            analysis_run_id         UUID NOT NULL REFERENCES analysis_runs(id) ON DELETE CASCADE,
            dataset_id              UUID NOT NULL REFERENCES datasets(id) ON DELETE CASCADE,
            sequence_index          INT NOT NULL,
            kind                    TEXT NOT NULL,
            stability_margin        DOUBLE PRECISION,
            torsional_accumulation  DOUBLE PRECISION,
            basis                   TEXT,
            payload                 JSONB
        );

        CREATE INDEX IF NOT EXISTS idx_attractor_events_dataset_id
            ON attractor_events(dataset_id);

        CREATE INDEX IF NOT EXISTS idx_attractor_events_run_id
            ON attractor_events(analysis_run_id);
        """

        6, """
        CREATE TABLE IF NOT EXISTS users (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            email               TEXT NOT NULL UNIQUE,
            email_verified_at   TIMESTAMPTZ,
            password_hash       TEXT,
            display_name        TEXT,
            avatar_url          TEXT,
            bio                 TEXT,
            pronouns            TEXT,
            location            TEXT,
            external_links      JSONB DEFAULT '[]'::jsonb,
            notify_by_email     BOOLEAN NOT NULL DEFAULT FALSE,
            role                TEXT NOT NULL DEFAULT 'user'
                                CHECK (role IN ('user', 'admin')),
            status              TEXT NOT NULL DEFAULT 'active'
                                CHECK (status IN ('pending', 'active', 'suspended')),
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
        """

        7, """
        CREATE TABLE IF NOT EXISTS user_identities (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            provider        TEXT NOT NULL,
            subject_id      TEXT NOT NULL,
            email           TEXT,
            display_name    TEXT,
            avatar_url      TEXT,
            linked_at       TIMESTAMPTZ NOT NULL DEFAULT now(),

            UNIQUE (provider, subject_id)
        );

        CREATE INDEX IF NOT EXISTS idx_user_identities_user_id ON user_identities(user_id);

        CREATE TABLE IF NOT EXISTS email_verification_tokens (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            token           TEXT NOT NULL UNIQUE,
            expires_at      TIMESTAMPTZ NOT NULL,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_email_verification_tokens_user_id ON email_verification_tokens(user_id);

        CREATE TABLE IF NOT EXISTS password_reset_tokens (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            token           TEXT NOT NULL UNIQUE,
            expires_at      TIMESTAMPTZ NOT NULL,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_password_reset_tokens_user_id ON password_reset_tokens(user_id);
        """
    ]

let private appliedVersions (conn: NpgsqlConnection) =
    // schema_migrations may not exist yet on first run
    try
        use cmd = new NpgsqlCommand("SELECT version FROM schema_migrations", conn)
        use reader = cmd.ExecuteReader()
        let mutable versions = Set.empty
        while reader.Read() do
            versions <- versions |> Set.add (reader.GetInt32(0))
        versions
    with _ ->
        Set.empty

let private recordVersion (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (version: int) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO schema_migrations(version) VALUES(@v) ON CONFLICT DO NOTHING", conn, txn)
    cmd.Parameters.AddWithValue("v", version) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let runMigrations () =
    use conn = openConnection ()
    let applied = appliedVersions conn
    for (version, sql) in migrations do
        if not (Set.contains version applied) then
            use txn = conn.BeginTransaction()
            try
                use cmd = new NpgsqlCommand(sql, conn, txn)
                cmd.ExecuteNonQuery() |> ignore
                recordVersion conn txn version
                txn.Commit()
                printfn "[DB] Applied migration %d" version
            with ex ->
                txn.Rollback()
                failwithf "Migration %d failed: %s" version ex.Message
    printfn "[DB] Schema up to date (%d migrations)" (List.length migrations)
