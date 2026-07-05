-- Migration 60: MUD economy - per-character-per-realm currency, NPC
-- vendors with buy/sell catalogs. Currency is intentionally scoped to
-- (character_id, realm_slug), not a global wallet: a character's home
-- realm currency should not mean anything in a different realm reached
-- through a Threshold portal.

CREATE TABLE IF NOT EXISTS mud_character_currency (
    character_id UUID NOT NULL REFERENCES mud_characters(id) ON DELETE CASCADE,
    realm_slug   TEXT NOT NULL,
    balance      INT NOT NULL DEFAULT 0 CHECK (balance >= 0),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (character_id, realm_slug)
);

CREATE INDEX IF NOT EXISTS idx_mud_character_currency_character
    ON mud_character_currency(character_id);

CREATE TABLE IF NOT EXISTS mud_vendors (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    room_id    UUID NOT NULL REFERENCES mud_rooms(id) ON DELETE CASCADE,
    name       TEXT NOT NULL,
    greeting   TEXT,
    active     BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_mud_vendors_room_id ON mud_vendors(room_id);

CREATE TABLE IF NOT EXISTS mud_vendor_listings (
    id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vendor_id          UUID NOT NULL REFERENCES mud_vendors(id) ON DELETE CASCADE,
    item_name          TEXT NOT NULL,
    item_slug          TEXT NOT NULL,
    item_description   TEXT,
    item_readable_text TEXT,
    portable           BOOLEAN NOT NULL DEFAULT TRUE,
    buy_price          INT,
    sell_price         INT,
    position           INT NOT NULL DEFAULT 0,
    active             BOOLEAN NOT NULL DEFAULT TRUE,

    UNIQUE (vendor_id, item_slug),
    CHECK (buy_price IS NULL OR buy_price >= 0),
    CHECK (sell_price IS NULL OR sell_price >= 0)
);

CREATE INDEX IF NOT EXISTS idx_mud_vendor_listings_vendor_id ON mud_vendor_listings(vendor_id);

GRANT ALL ON TABLE mud_character_currency TO djehuti;
GRANT ALL ON TABLE mud_vendors TO djehuti;
GRANT ALL ON TABLE mud_vendor_listings TO djehuti;

INSERT INTO schema_migrations(version) VALUES (60) ON CONFLICT DO NOTHING;
