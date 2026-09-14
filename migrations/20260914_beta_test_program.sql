-- Beta Test program: which products are open for public signup, who's
-- enrolled and their standing, and a centralized "effective tier" function
-- so beta-granted Curious Mind access never touches a real patreon_tier_id
-- (a beta tester who's also a genuine paying patron never has their real
-- tier clobbered by a later beta drop, and a drop never needs to "restore"
-- anything since the real column was never written).

ALTER TABLE products
  ADD COLUMN IF NOT EXISTS beta_open BOOLEAN NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS beta_welcome_subject TEXT,
  ADD COLUMN IF NOT EXISTS beta_welcome_body TEXT,
  ADD COLUMN IF NOT EXISTS beta_invite_subject TEXT,
  ADD COLUMN IF NOT EXISTS beta_invite_body TEXT;

CREATE TABLE IF NOT EXISTS beta_testers (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id          UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    product_id       UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    status           TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'dropped')),
    joined_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_feedback_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    dropped_at       TIMESTAMPTZ,
    invited_by       UUID REFERENCES users(id) ON DELETE SET NULL,
    UNIQUE (user_id, product_id)
);

CREATE INDEX IF NOT EXISTS idx_beta_testers_product_status ON beta_testers(product_id, status);
CREATE INDEX IF NOT EXISTS idx_beta_testers_status_last_feedback ON beta_testers(status, last_feedback_at)
    WHERE status = 'active';

GRANT ALL ON TABLE beta_testers TO djehuti;

-- Effective tier: a user's real patreon_tier_id, upgraded to 'curious-mind'
-- if that ranks higher and they have at least one active beta_testers row
-- (for any product -- being an active beta tester anywhere grants the
-- baseline Curious Mind perk suite site-wide, not per-product). Every
-- existing tier-gating query joins patreon_tiers against
-- u.patreon_tier_id directly; each of those becomes a one-line change to
-- join against effective_tier_id(u.id) instead, rather than duplicating
-- this overlay logic at each call site.
CREATE OR REPLACE FUNCTION effective_tier_id(p_user_id UUID) RETURNS TEXT AS $$
DECLARE
    real_tier  TEXT;
    real_order INT;
    beta_order INT;
BEGIN
    SELECT u.patreon_tier_id, pt.display_order INTO real_tier, real_order
    FROM users u
    LEFT JOIN patreon_tiers pt ON pt.tier_id = u.patreon_tier_id
    WHERE u.id = p_user_id;

    SELECT pt2.display_order INTO beta_order
    FROM patreon_tiers pt2
    WHERE pt2.tier_id = 'curious-mind'
      AND EXISTS (
          SELECT 1 FROM beta_testers bt
          WHERE bt.user_id = p_user_id AND bt.status = 'active'
      );

    IF beta_order IS NOT NULL AND (real_order IS NULL OR beta_order > real_order) THEN
        RETURN 'curious-mind';
    END IF;

    RETURN real_tier;
END;
$$ LANGUAGE plpgsql STABLE;
