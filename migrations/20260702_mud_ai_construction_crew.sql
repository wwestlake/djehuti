ALTER TABLE mud_zones
    ADD COLUMN IF NOT EXISTS realm_slug TEXT;

ALTER TABLE mud_zones
    ALTER COLUMN realm_slug SET DEFAULT 'medieval';

UPDATE mud_zones
SET realm_slug = CASE slug
    WHEN 'star-reach' THEN 'sci-fi'
    WHEN 'realm-threshold' THEN 'neutral'
    ELSE 'medieval'
END
WHERE realm_slug IS NULL OR btrim(realm_slug) = '';

ALTER TABLE mud_zones
    ALTER COLUMN realm_slug SET NOT NULL;

CREATE INDEX IF NOT EXISTS idx_mud_zones_realm_slug ON mud_zones(realm_slug);

CREATE TABLE IF NOT EXISTS mud_director_directives (
    id                        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    realm_slug                TEXT NOT NULL,
    director_slug             TEXT NOT NULL,
    raw_command               TEXT NOT NULL,
    normalized_instruction    TEXT NOT NULL,
    requested_by_user_id      UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    requested_by_character_id UUID NOT NULL REFERENCES mud_characters(id) ON DELETE CASCADE,
    active                    BOOLEAN NOT NULL DEFAULT TRUE,
    superseded_at             TIMESTAMPTZ,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_mud_director_directives_realm_active
    ON mud_director_directives(realm_slug, active, created_at DESC);

CREATE TABLE IF NOT EXISTS mud_builder_agents (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug            TEXT NOT NULL UNIQUE,
    realm_slug      TEXT NOT NULL,
    director_slug   TEXT NOT NULL,
    display_name    TEXT NOT NULL,
    specialty       TEXT NOT NULL,
    model           TEXT NOT NULL DEFAULT 'gpt-4o-mini',
    build_hour_utc  INT NOT NULL,
    active          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_mud_builder_agents_realm_active
    ON mud_builder_agents(realm_slug, active, build_hour_utc);

CREATE TABLE IF NOT EXISTS mud_build_jobs (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    builder_agent_id UUID NOT NULL REFERENCES mud_builder_agents(id) ON DELETE CASCADE,
    realm_slug       TEXT NOT NULL,
    directive_id     UUID REFERENCES mud_director_directives(id) ON DELETE SET NULL,
    build_date       DATE NOT NULL,
    scheduled_for    TIMESTAMPTZ NOT NULL,
    status           TEXT NOT NULL DEFAULT 'Pending',
    retry_count      INT NOT NULL DEFAULT 0,
    anchor_room_id   UUID REFERENCES mud_rooms(id) ON DELETE SET NULL,
    created_room_id  UUID REFERENCES mud_rooms(id) ON DELETE SET NULL,
    payload          JSONB,
    result_summary   TEXT,
    error            TEXT,
    started_at       TIMESTAMPTZ,
    completed_at     TIMESTAMPTZ,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT mud_build_jobs_status_check CHECK (status IN ('Pending', 'Processing', 'Completed', 'Failed')),
    CONSTRAINT mud_build_jobs_one_per_builder_per_day UNIQUE (builder_agent_id, build_date)
);

CREATE INDEX IF NOT EXISTS idx_mud_build_jobs_status_schedule
    ON mud_build_jobs(status, scheduled_for, created_at);

GRANT ALL ON TABLE mud_zones TO djehuti;
GRANT ALL ON TABLE mud_director_directives TO djehuti;
GRANT ALL ON TABLE mud_builder_agents TO djehuti;
GRANT ALL ON TABLE mud_build_jobs TO djehuti;
