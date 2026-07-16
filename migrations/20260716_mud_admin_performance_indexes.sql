-- Add indexes to improve MUD admin console performance
-- These indexes speed up common queries in getWorld() and getMetrics()

-- Indexes for zone queries
CREATE INDEX IF NOT EXISTS idx_mud_rooms_zone_id ON mud_rooms(zone_id);
CREATE INDEX IF NOT EXISTS idx_mud_exits_from_room_id ON mud_exits(from_room_id);
CREATE INDEX IF NOT EXISTS idx_mud_exits_to_room_id ON mud_exits(to_room_id);

-- Indexes for character queries
CREATE INDEX IF NOT EXISTS idx_mud_characters_deleted_at ON mud_characters(deleted_at);
CREATE INDEX IF NOT EXISTS idx_mud_characters_realm_slug ON mud_characters(realm_slug);

-- Indexes for item queries
CREATE INDEX IF NOT EXISTS idx_mud_items_portable ON mud_items(portable);
CREATE INDEX IF NOT EXISTS idx_mud_items_readable_text ON mud_items(readable_text);

-- Indexes for companion/profile queries
CREATE INDEX IF NOT EXISTS idx_mud_companion_profiles_enabled ON mud_companion_profiles(enabled);
CREATE INDEX IF NOT EXISTS idx_mud_companion_profiles_byo_key ON mud_companion_profiles(byo_openai_key_protected);

-- Indexes for recipe queries
CREATE INDEX IF NOT EXISTS idx_mud_craft_recipes_active ON mud_craft_recipes(active);

-- Indexes for exit type analysis
CREATE INDEX IF NOT EXISTS idx_mud_exits_type ON mud_exits(exit_type);

-- Analyze tables to update query planner statistics
ANALYZE mud_zones;
ANALYZE mud_rooms;
ANALYZE mud_exits;
ANALYZE mud_characters;
ANALYZE mud_items;
ANALYZE mud_companion_profiles;
ANALYZE mud_craft_recipes;
