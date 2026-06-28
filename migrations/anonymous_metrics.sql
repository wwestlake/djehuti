-- Anonymous visitor tracking

CREATE TABLE IF NOT EXISTS anonymous_page_views (
    id          BIGSERIAL PRIMARY KEY,
    ip_hash     TEXT NOT NULL,          -- SHA-256 of IP, never store raw IP
    path        TEXT NOT NULL,
    referrer    TEXT NOT NULL DEFAULT '',
    viewed_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_anon_views_viewed_at ON anonymous_page_views(viewed_at);
CREATE INDEX IF NOT EXISTS idx_anon_views_ip_hash   ON anonymous_page_views(ip_hash);
CREATE INDEX IF NOT EXISTS idx_anon_views_path      ON anonymous_page_views(path);

-- Track when an anonymous visitor registers (for conversion rate)
CREATE TABLE IF NOT EXISTS anonymous_conversions (
    id          BIGSERIAL PRIMARY KEY,
    ip_hash     TEXT NOT NULL,
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    converted_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_anon_conv_converted_at ON anonymous_conversions(converted_at);
