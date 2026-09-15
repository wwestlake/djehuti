-- crates.io-style "yank": hides a version from fresh dependency resolution
-- without deleting it, so builds already pinned to it keep working. Direct
-- name+version download/lookup is unaffected by this flag on purpose.
ALTER TABLE frate_pod_versions
  ADD COLUMN IF NOT EXISTS yanked    BOOLEAN NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS yanked_at TIMESTAMPTZ,
  ADD COLUMN IF NOT EXISTS yanked_by UUID REFERENCES users(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_frate_pod_versions_yanked ON frate_pod_versions (pod_id, yanked);
