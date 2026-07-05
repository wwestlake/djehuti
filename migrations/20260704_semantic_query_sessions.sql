CREATE TABLE IF NOT EXISTS semantic_query_sessions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id   UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS semantic_query_turns (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id              UUID NOT NULL REFERENCES semantic_query_sessions(id) ON DELETE CASCADE,
    turn_index              INT NOT NULL,
    query_text              TEXT NOT NULL,
    source_type_filter      TEXT,
    token_count             INT NOT NULL DEFAULT 0,
    hit_count               INT NOT NULL DEFAULT 0,
    source_type_diversity   INT NOT NULL DEFAULT 0,
    matched_token_total     INT NOT NULL DEFAULT 0,
    matched_weight_total    INT NOT NULL DEFAULT 0,
    top_similarity          DOUBLE PRECISION NOT NULL DEFAULT 0,
    mean_similarity         DOUBLE PRECISION NOT NULL DEFAULT 0,
    drift_from_previous     DOUBLE PRECISION,
    query_embedding         REAL[],
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (session_id, turn_index)
);

CREATE INDEX IF NOT EXISTS idx_semantic_query_sessions_admin_updated
    ON semantic_query_sessions(admin_user_id, updated_at DESC);

CREATE INDEX IF NOT EXISTS idx_semantic_query_turns_session_turn
    ON semantic_query_turns(session_id, turn_index DESC);

GRANT ALL ON TABLE semantic_query_sessions TO djehuti;
GRANT ALL ON TABLE semantic_query_turns TO djehuti;
