CREATE TABLE IF NOT EXISTS sponsors (
    id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name         TEXT        NOT NULL,
    logo_url     TEXT,
    website_url  TEXT,
    tier         TEXT        NOT NULL CHECK (tier IN ('gold', 'silver', 'bronze')),
    blurb        TEXT,
    active       BOOLEAN     NOT NULL DEFAULT true,
    position     INT         NOT NULL DEFAULT 0,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
