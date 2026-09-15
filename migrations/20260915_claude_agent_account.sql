-- A real, distinct account for the Claude agent -- not the site owner's own
-- account, not a shared credential. No password (API-key auth only, see
-- ApiKeyRepository.fs); no context roles granted by default (least
-- privilege from the start -- the account can authenticate and do nothing
-- until specific roles are granted via the existing admin Roles tab, module
-- 'agent', e.g. role 'beta-feedback-reader'). Site-wide admin, when truly
-- needed, is the real users.role = 'admin' flag on THIS row, toggled
-- explicitly and separately -- never implied by any agent role.
INSERT INTO users (email, password_hash, display_name, role, status, email_verified_at)
VALUES ('claude-agent@lagdaemon.com', NULL, 'Claude', 'user', 'active', now())
ON CONFLICT (email) DO NOTHING;
