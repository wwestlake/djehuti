ALTER TABLE users ADD COLUMN IF NOT EXISTS active_mud_character_id UUID;
ALTER TABLE users ADD COLUMN IF NOT EXISTS mud_bonus_character_slots INT NOT NULL DEFAULT 0;

ALTER TABLE mud_characters DROP CONSTRAINT IF EXISTS mud_characters_user_id_key;

ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS realm_slug TEXT;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS name TEXT;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;

ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_presence INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_wit INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_resolve INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_lore INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_craft INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_guile INT NOT NULL DEFAULT 1;

ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_searching INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_crafting INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_navigation INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_lorekeeping INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_negotiation INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_devices INT NOT NULL DEFAULT 1;
ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_survival INT NOT NULL DEFAULT 1;

UPDATE mud_characters
SET name = COALESCE(NULLIF(name, ''), display_name)
WHERE name IS NULL OR btrim(name) = '';

UPDATE mud_characters
SET realm_slug = CASE
    WHEN current_room_id IN (
        SELECT r.id
        FROM mud_rooms r
        JOIN mud_zones z ON z.id = r.zone_id
        WHERE z.slug = 'star-reach'
    ) THEN 'sci-fi'
    ELSE 'medieval'
END
WHERE realm_slug IS NULL OR btrim(realm_slug) = '';

ALTER TABLE mud_characters
    ALTER COLUMN name SET NOT NULL;

ALTER TABLE mud_characters
    ALTER COLUMN realm_slug SET NOT NULL;

CREATE INDEX IF NOT EXISTS idx_mud_characters_user_id ON mud_characters(user_id);
CREATE INDEX IF NOT EXISTS idx_mud_characters_realm_slug ON mud_characters(realm_slug);
CREATE INDEX IF NOT EXISTS idx_mud_characters_deleted_at ON mud_characters(deleted_at);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE constraint_name = 'fk_users_active_mud_character_id'
          AND table_name = 'users'
    ) THEN
        ALTER TABLE users
            ADD CONSTRAINT fk_users_active_mud_character_id
            FOREIGN KEY (active_mud_character_id)
            REFERENCES mud_characters(id)
            ON DELETE SET NULL;
    END IF;
END $$;

GRANT ALL ON TABLE mud_characters TO djehuti;
