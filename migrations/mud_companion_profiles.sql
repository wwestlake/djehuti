CREATE TABLE IF NOT EXISTS mud_companion_profiles (
    character_id UUID PRIMARY KEY REFERENCES mud_characters(id) ON DELETE CASCADE,
    enabled BOOLEAN NOT NULL DEFAULT FALSE,
    mode TEXT NOT NULL DEFAULT 'solitary',
    model TEXT NOT NULL DEFAULT 'gpt-4.1-mini',
    disclosure TEXT NOT NULL DEFAULT 'tagged',
    allow_online_concurrency BOOLEAN NOT NULL DEFAULT FALSE,
    use_byo_openai_key BOOLEAN NOT NULL DEFAULT FALSE,
    byo_openai_key_protected TEXT,
    key_last_set_at TIMESTAMPTZ,
    last_status TEXT,
    last_error TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_mud_companion_profiles_enabled
    ON mud_companion_profiles(enabled);

GRANT ALL ON TABLE mud_companion_profiles TO djehuti;
