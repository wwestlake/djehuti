-- Migration 57: pgvector-backed semantic chunk embeddings
-- Requires the postgresql-<version>-pgvector package on the database host.
-- Safe on hosts without pgvector: falls through with a NOTICE and the
-- application keeps using the in-memory REAL[] cosine fallback.

DO $pgvector$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_available_extensions WHERE name = 'vector') THEN
        BEGIN
            CREATE EXTENSION IF NOT EXISTS vector;
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'pgvector: unable to create extension: %', SQLERRM;
        END;
    END IF;

    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector') THEN
        EXECUTE 'ALTER TABLE semantic_chunks ADD COLUMN IF NOT EXISTS embedding vector(384)';
        EXECUTE 'UPDATE semantic_chunks
                    SET embedding = embedding_values::vector(384)
                  WHERE embedding IS NULL
                    AND embedding_values IS NOT NULL
                    AND cardinality(embedding_values) = 384';
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_semantic_chunks_embedding_hnsw
                     ON semantic_chunks USING hnsw (embedding vector_cosine_ops)';
    ELSE
        RAISE NOTICE 'pgvector not installed; semantic vector retrieval will use in-memory fallback';
    END IF;
END $pgvector$;

INSERT INTO schema_migrations(version) VALUES (57) ON CONFLICT DO NOTHING;
