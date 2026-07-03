-- MUD chat and presence: room/vicinity/whisper/party/announcement channels,
-- and character presence tracking (last_active_at).

ALTER TABLE mud_characters
    ADD COLUMN IF NOT EXISTS last_active_at timestamp with time zone;

CREATE INDEX IF NOT EXISTS idx_mud_characters_last_active_at
    ON mud_characters (last_active_at);

CREATE TABLE IF NOT EXISTS mud_groups (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name text NOT NULL,
    owner_character_id uuid NOT NULL REFERENCES mud_characters(id) ON DELETE CASCADE,
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS mud_group_members (
    group_id uuid NOT NULL REFERENCES mud_groups(id) ON DELETE CASCADE,
    character_id uuid NOT NULL REFERENCES mud_characters(id) ON DELETE CASCADE,
    joined_at timestamp with time zone NOT NULL DEFAULT now(),
    PRIMARY KEY (group_id, character_id)
);

CREATE INDEX IF NOT EXISTS idx_mud_group_members_character
    ON mud_group_members (character_id);

CREATE TABLE IF NOT EXISTS mud_chat_messages (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    channel text NOT NULL CHECK (channel IN ('room', 'shout', 'whisper', 'group', 'announce')),
    sender_character_id uuid REFERENCES mud_characters(id) ON DELETE SET NULL,
    sender_name text NOT NULL,
    room_id uuid REFERENCES mud_rooms(id) ON DELETE SET NULL,
    recipient_character_id uuid REFERENCES mud_characters(id) ON DELETE SET NULL,
    recipient_name text,
    group_id uuid REFERENCES mud_groups(id) ON DELETE CASCADE,
    body text NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_mud_chat_room_created
    ON mud_chat_messages (room_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_mud_chat_recipient_created
    ON mud_chat_messages (recipient_character_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_mud_chat_sender_created
    ON mud_chat_messages (sender_character_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_mud_chat_group_created
    ON mud_chat_messages (group_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_mud_chat_channel_created
    ON mud_chat_messages (channel, created_at DESC);

GRANT ALL ON TABLE mud_groups TO djehuti;
GRANT ALL ON TABLE mud_group_members TO djehuti;
GRANT ALL ON TABLE mud_chat_messages TO djehuti;
