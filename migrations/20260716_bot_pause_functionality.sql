-- Add pause functionality to bot/agent tables
-- Allows bots to be paused until a specific datetime

-- MUD: Builder agents (construction crews)
ALTER TABLE mud_builder_agents
ADD COLUMN IF NOT EXISTS paused_until TIMESTAMPTZ DEFAULT NULL;

CREATE INDEX IF NOT EXISTS idx_mud_builder_agents_paused
    ON mud_builder_agents(paused_until, active);

-- MUD: Companion profiles (companions/pets)
ALTER TABLE mud_companion_profiles
ADD COLUMN IF NOT EXISTS paused_until TIMESTAMPTZ DEFAULT NULL;

CREATE INDEX IF NOT EXISTS idx_mud_companion_profiles_paused
    ON mud_companion_profiles(paused_until, enabled);

-- FORUM: AI Personas (forum bots)
ALTER TABLE ai_personas
ADD COLUMN IF NOT EXISTS paused_until TIMESTAMPTZ DEFAULT NULL;

CREATE INDEX IF NOT EXISTS idx_ai_personas_paused
    ON ai_personas(paused_until, active);

-- Helper function to check if a bot is actually active (not paused)
CREATE OR REPLACE FUNCTION is_bot_active(paused_until TIMESTAMPTZ, enabled BOOLEAN)
RETURNS BOOLEAN AS $$
BEGIN
    RETURN enabled AND (paused_until IS NULL OR paused_until < now());
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- Comments for documentation
COMMENT ON COLUMN mud_builder_agents.paused_until IS 'Bot is paused until this datetime; if NULL, never paused';
COMMENT ON COLUMN mud_companion_profiles.paused_until IS 'Bot is paused until this datetime; if NULL, never paused';
COMMENT ON COLUMN ai_personas.paused_until IS 'Bot is paused until this datetime; if NULL, never paused';
