ALTER TABLE semantic_chunks
    ADD COLUMN IF NOT EXISTS embedding_values REAL[],
    ADD COLUMN IF NOT EXISTS embedding_provider TEXT,
    ADD COLUMN IF NOT EXISTS embedding_dimension INT,
    ADD COLUMN IF NOT EXISTS embedded_at TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS idx_semantic_chunks_embedded_at
    ON semantic_chunks(embedded_at DESC);
