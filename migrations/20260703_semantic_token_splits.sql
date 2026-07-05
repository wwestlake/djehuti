CREATE TABLE IF NOT EXISTS semantic_token_splits (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    token TEXT NOT NULL,
    source_type TEXT NOT NULL,
    variant_key TEXT NOT NULL UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (token, source_type)
);

CREATE INDEX IF NOT EXISTS idx_semantic_token_splits_token
    ON semantic_token_splits(token);

CREATE INDEX IF NOT EXISTS idx_semantic_token_splits_source
    ON semantic_token_splits(source_type);

GRANT ALL ON TABLE semantic_token_splits TO djehuti;
