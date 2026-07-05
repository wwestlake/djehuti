-- Migration 58: pgvector twin for semantic query turn embeddings
-- Enables SQL-side nearest-neighbor search over the admin query history
-- (similar past queries across sessions). Safe on hosts without pgvector.

DO $pgvector_turns$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector') THEN
        EXECUTE 'ALTER TABLE semantic_query_turns ADD COLUMN IF NOT EXISTS query_vector vector(384)';
        EXECUTE 'UPDATE semantic_query_turns
                    SET query_vector = query_embedding::vector(384)
                  WHERE query_vector IS NULL
                    AND query_embedding IS NOT NULL
                    AND cardinality(query_embedding) = 384';
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_semantic_query_turns_vector_hnsw
                     ON semantic_query_turns USING hnsw (query_vector vector_cosine_ops)';
    ELSE
        RAISE NOTICE 'pgvector not installed; query-turn vector search unavailable';
    END IF;
END $pgvector_turns$;

INSERT INTO schema_migrations(version) VALUES (58) ON CONFLICT DO NOTHING;
