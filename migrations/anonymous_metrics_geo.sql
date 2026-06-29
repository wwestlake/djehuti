-- Add geo and identity columns to anonymous_page_views

ALTER TABLE anonymous_page_views
  ADD COLUMN IF NOT EXISTS ip_address TEXT NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS country    TEXT NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS region     TEXT NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS city       TEXT NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS domain     TEXT NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS user_agent TEXT NOT NULL DEFAULT '',
  ADD COLUMN IF NOT EXISTS source     TEXT NOT NULL DEFAULT 'beacon'; -- 'beacon' | 'nginx_log'

CREATE UNIQUE INDEX IF NOT EXISTS idx_anon_views_dedup
  ON anonymous_page_views (ip_address, path, DATE(viewed_at))
  WHERE ip_address != '';
