CREATE TABLE IF NOT EXISTS semantic_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_type TEXT NOT NULL,
    source_key TEXT NOT NULL,
    title TEXT NOT NULL,
    content_hash TEXT NOT NULL,
    metadata_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (source_type, source_key)
);

CREATE TABLE IF NOT EXISTS semantic_chunks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id UUID NOT NULL REFERENCES semantic_documents(id) ON DELETE CASCADE,
    chunk_position INT NOT NULL,
    content TEXT NOT NULL,
    token_count INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (document_id, chunk_position)
);

CREATE TABLE IF NOT EXISTS semantic_chunk_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chunk_id UUID NOT NULL REFERENCES semantic_chunks(id) ON DELETE CASCADE,
    token TEXT NOT NULL,
    token_count INT NOT NULL DEFAULT 1,
    position INT NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_semantic_documents_source ON semantic_documents(source_type, source_key);
CREATE INDEX IF NOT EXISTS idx_semantic_chunks_document_id ON semantic_chunks(document_id, chunk_position);
CREATE INDEX IF NOT EXISTS idx_semantic_chunk_tokens_chunk_id ON semantic_chunk_tokens(chunk_id);
CREATE INDEX IF NOT EXISTS idx_semantic_chunk_tokens_token ON semantic_chunk_tokens(token);

GRANT ALL ON TABLE semantic_documents TO djehuti;
GRANT ALL ON TABLE semantic_chunks TO djehuti;
GRANT ALL ON TABLE semantic_chunk_tokens TO djehuti;
