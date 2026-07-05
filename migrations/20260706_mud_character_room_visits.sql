-- Migration 59: track which rooms each MUD character has actually
-- visited, so the zone map can be fogged for non-admin players
-- (admins still see the whole zone).

CREATE TABLE IF NOT EXISTS mud_character_room_visits (
    character_id     UUID NOT NULL REFERENCES mud_characters(id) ON DELETE CASCADE,
    room_id          UUID NOT NULL REFERENCES mud_rooms(id) ON DELETE CASCADE,
    first_visited_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (character_id, room_id)
);

CREATE INDEX IF NOT EXISTS idx_mud_character_room_visits_character
    ON mud_character_room_visits(character_id);

-- Backfill: every existing character has at least their current room
-- marked visited, so nobody is stranded with a blank map.
INSERT INTO mud_character_room_visits (character_id, room_id)
SELECT id, current_room_id FROM mud_characters WHERE current_room_id IS NOT NULL
ON CONFLICT DO NOTHING;

GRANT ALL ON TABLE mud_character_room_visits TO djehuti;

INSERT INTO schema_migrations(version) VALUES (59) ON CONFLICT DO NOTHING;
