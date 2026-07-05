CREATE TABLE IF NOT EXISTS semantic_admin_action_log (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id   UUID NOT NULL REFERENCES users(id),
    action          TEXT NOT NULL,
    token           TEXT,
    scope_kind      TEXT,
    scope_value     TEXT,
    variant_key     TEXT,
    created_count   INT NOT NULL DEFAULT 0,
    proposal_count  INT NOT NULL DEFAULT 0,
    details_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_semantic_admin_action_created ON semantic_admin_action_log(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_semantic_admin_action_admin   ON semantic_admin_action_log(admin_user_id);
CREATE INDEX IF NOT EXISTS idx_semantic_admin_action_action  ON semantic_admin_action_log(action);
