-- Migration 61: character portrait, bio text, and archetype tracking.
-- Portrait uploads go through the existing generic media system
-- (/api/media/upload-url + /api/media/confirm, module=mud-character-portrait,
-- contextId=characterId); this column just stores the resulting URL.

ALTER TABLE mud_characters
    ADD COLUMN IF NOT EXISTS portrait_url TEXT,
    ADD COLUMN IF NOT EXISTS bio TEXT,
    ADD COLUMN IF NOT EXISTS archetype_slug TEXT;

INSERT INTO schema_migrations(version) VALUES (61) ON CONFLICT DO NOTHING;
