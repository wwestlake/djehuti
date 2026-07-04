ALTER TABLE semantic_token_splits
    ADD COLUMN IF NOT EXISTS scope_kind TEXT,
    ADD COLUMN IF NOT EXISTS scope_value TEXT;

UPDATE semantic_token_splits
SET scope_kind = COALESCE(scope_kind, 'source-type'),
    scope_value = COALESCE(scope_value, source_type)
WHERE scope_kind IS NULL OR scope_value IS NULL;

ALTER TABLE semantic_token_splits
    ALTER COLUMN scope_kind SET NOT NULL,
    ALTER COLUMN scope_value SET NOT NULL;

CREATE INDEX IF NOT EXISTS idx_semantic_token_splits_scope
    ON semantic_token_splits(scope_kind, scope_value);
