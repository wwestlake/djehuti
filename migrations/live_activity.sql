-- Add last_activity_at to users for "online right now" tracking
ALTER TABLE users ADD COLUMN IF NOT EXISTS last_activity_at TIMESTAMPTZ;
CREATE INDEX IF NOT EXISTS idx_users_last_activity_at ON users (last_activity_at);
