-- Opt-in feedback and usage-metrics collection from installed suite apps
-- (Djehuti Station first; product_id is a real FK so any other product can
-- reuse the same two tables without a new migration). Two separate tables
-- because a user can opt into sending qualitative feedback without opting
-- into background metrics collection, and vice versa -- the client decides
-- what it sends, these tables don't enforce that pairing.

CREATE TABLE IF NOT EXISTS product_feedback (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id   UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    user_id      UUID REFERENCES users(id) ON DELETE SET NULL,
    install_id   TEXT NOT NULL,
    message      TEXT NOT NULL,
    category     TEXT NOT NULL DEFAULT 'general',
    app_version  TEXT NOT NULL DEFAULT '',
    os_info      TEXT NOT NULL DEFAULT '',
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_product_feedback_product_time ON product_feedback(product_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_product_feedback_user ON product_feedback(user_id) WHERE user_id IS NOT NULL;

-- event_type is a coarse bucket (crash | feature_usage | session | performance);
-- event_name is the specific thing within that bucket (e.g. "signal_lab_opened",
-- "audio_xrun", "session_end"); payload carries whatever event-specific data
-- doesn't need its own column (stack trace, duration, counts) so new event
-- shapes don't require schema changes.
CREATE TABLE IF NOT EXISTS product_metrics_events (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id   UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    user_id      UUID REFERENCES users(id) ON DELETE SET NULL,
    install_id   TEXT NOT NULL,
    event_type   TEXT NOT NULL,
    event_name   TEXT NOT NULL,
    app_version  TEXT NOT NULL DEFAULT '',
    os_info      TEXT NOT NULL DEFAULT '',
    payload      JSONB NOT NULL DEFAULT '{}'::jsonb,
    occurred_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_product_metrics_product_time ON product_metrics_events(product_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS idx_product_metrics_type ON product_metrics_events(product_id, event_type);
CREATE INDEX IF NOT EXISTS idx_product_metrics_install ON product_metrics_events(install_id);

GRANT ALL ON TABLE product_feedback TO djehuti;
GRANT ALL ON TABLE product_metrics_events TO djehuti;
