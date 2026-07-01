CREATE TABLE IF NOT EXISTS mud_items (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    room_id             UUID REFERENCES mud_rooms(id) ON DELETE SET NULL,
    owner_character_id  UUID REFERENCES mud_characters(id) ON DELETE SET NULL,
    name                TEXT NOT NULL,
    slug                TEXT NOT NULL,
    description         TEXT,
    readable_text       TEXT,
    portable            BOOLEAN NOT NULL DEFAULT false,
    position            INT NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_mud_items_room_id ON mud_items(room_id);
CREATE INDEX IF NOT EXISTS idx_mud_items_owner_character_id ON mud_items(owner_character_id);

GRANT ALL ON TABLE mud_items TO djehuti;
