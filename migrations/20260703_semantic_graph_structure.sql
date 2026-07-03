CREATE TABLE IF NOT EXISTS semantic_nodes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    node_key TEXT NOT NULL UNIQUE,
    node_type TEXT NOT NULL DEFAULT 'token',
    display_text TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS semantic_chunk_nodes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chunk_id UUID NOT NULL REFERENCES semantic_chunks(id) ON DELETE CASCADE,
    node_id UUID NOT NULL REFERENCES semantic_nodes(id) ON DELETE CASCADE,
    node_weight INT NOT NULL DEFAULT 1,
    first_position INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (chunk_id, node_id)
);

CREATE TABLE IF NOT EXISTS semantic_node_edges (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chunk_id UUID NOT NULL REFERENCES semantic_chunks(id) ON DELETE CASCADE,
    from_node_id UUID NOT NULL REFERENCES semantic_nodes(id) ON DELETE CASCADE,
    to_node_id UUID NOT NULL REFERENCES semantic_nodes(id) ON DELETE CASCADE,
    edge_type TEXT NOT NULL DEFAULT 'cooccurrence',
    edge_weight INT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (chunk_id, from_node_id, to_node_id, edge_type)
);

CREATE INDEX IF NOT EXISTS idx_semantic_nodes_key
    ON semantic_nodes(node_key);

CREATE INDEX IF NOT EXISTS idx_semantic_chunk_nodes_chunk
    ON semantic_chunk_nodes(chunk_id);

CREATE INDEX IF NOT EXISTS idx_semantic_chunk_nodes_node
    ON semantic_chunk_nodes(node_id);

CREATE INDEX IF NOT EXISTS idx_semantic_node_edges_chunk
    ON semantic_node_edges(chunk_id);

CREATE INDEX IF NOT EXISTS idx_semantic_node_edges_from
    ON semantic_node_edges(from_node_id);

CREATE INDEX IF NOT EXISTS idx_semantic_node_edges_to
    ON semantic_node_edges(to_node_id);

GRANT ALL ON TABLE semantic_nodes TO djehuti;
GRANT ALL ON TABLE semantic_chunk_nodes TO djehuti;
GRANT ALL ON TABLE semantic_node_edges TO djehuti;
