-- Achievements schema migration

CREATE TABLE IF NOT EXISTS achievement_dictionary (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug        TEXT NOT NULL UNIQUE,
    name        TEXT NOT NULL,
    description TEXT NOT NULL,
    icon        TEXT NOT NULL DEFAULT '',
    tier        TEXT NOT NULL DEFAULT 'bronze',  -- bronze, silver, gold, platinum, legendary
    category    TEXT NOT NULL DEFAULT 'participation',
    points      INT  NOT NULL DEFAULT 10,
    hidden      BOOLEAN NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS user_achievements (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    achievement_id  UUID NOT NULL REFERENCES achievement_dictionary(id) ON DELETE CASCADE,
    awarded_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    notified        BOOLEAN NOT NULL DEFAULT FALSE,
    UNIQUE (user_id, achievement_id)
);

CREATE INDEX IF NOT EXISTS idx_user_achievements_user ON user_achievements(user_id);
CREATE INDEX IF NOT EXISTS idx_user_achievements_notified ON user_achievements(notified) WHERE NOT notified;

CREATE TABLE IF NOT EXISTS user_metrics (
    user_id         UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    post_count      INT NOT NULL DEFAULT 0,
    thread_count    INT NOT NULL DEFAULT 0,
    vote_received   INT NOT NULL DEFAULT 0,
    answer_count    INT NOT NULL DEFAULT 0,
    reaction_count  INT NOT NULL DEFAULT 0,
    days_active     INT NOT NULL DEFAULT 0,
    login_streak    INT NOT NULL DEFAULT 0,
    last_active_day DATE,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed metrics rows for all existing users (zeroed out — recompute will fill them)
INSERT INTO user_metrics (user_id)
SELECT id FROM users
ON CONFLICT (user_id) DO NOTHING;
