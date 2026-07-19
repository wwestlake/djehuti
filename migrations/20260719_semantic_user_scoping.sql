-- User-scoped semantic context system
-- Adds account types (Regular, System, Application) and user-based context isolation

-- 1. Add account_type to users table
ALTER TABLE users ADD COLUMN IF NOT EXISTS account_type TEXT NOT NULL DEFAULT 'Regular';

-- 2. Create application_accounts table (for app-specific contexts and instructions)
CREATE TABLE IF NOT EXISTS application_accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    app_name TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL,
    description TEXT,
    context_scope TEXT NOT NULL DEFAULT 'app-global',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_application_accounts_user_id ON application_accounts(user_id);
CREATE INDEX IF NOT EXISTS idx_application_accounts_app_name ON application_accounts(app_name);

GRANT ALL ON TABLE application_accounts TO djehuti;

-- 3. Add user_id to semantic_documents for context isolation
ALTER TABLE semantic_documents ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES users(id) ON DELETE CASCADE;

-- Add index for fast filtering by user_id
CREATE INDEX IF NOT EXISTS idx_semantic_documents_user_id ON semantic_documents(user_id);
CREATE INDEX IF NOT EXISTS idx_semantic_documents_user_source ON semantic_documents(user_id, source_type, source_key);

-- 4. Create semantic_context_usage table to track per-user consumption
CREATE TABLE IF NOT EXISTS semantic_context_usage (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    total_bytes_used BIGINT NOT NULL DEFAULT 0,
    quota_bytes BIGINT,
    last_updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_semantic_context_usage_user_id ON semantic_context_usage(user_id);

GRANT ALL ON TABLE semantic_context_usage TO djehuti;

-- 5. Create conversations table (for saving/organizing AI conversations)
CREATE TABLE IF NOT EXISTS conversations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    app_name TEXT NOT NULL,
    title TEXT NOT NULL,
    s3_path TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_conversations_user_id ON conversations(user_id);
CREATE INDEX IF NOT EXISTS idx_conversations_user_app ON conversations(user_id, app_name);

GRANT ALL ON TABLE conversations TO djehuti;

-- 6. Create system_user (shared account for internal bots: MUD, Learn, etc.)
INSERT INTO users (id, email, display_name, created_at, account_type)
VALUES (
    '00000000-0000-0000-0000-000000000001'::uuid,
    'system@internal.djehuti',
    'System',
    now(),
    'System'
) ON CONFLICT DO NOTHING;

-- 7. Grant permissions
GRANT ALL ON TABLE users TO djehuti;
GRANT ALL ON TABLE conversations TO djehuti;
