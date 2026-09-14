-- Make the beta-tester Curious Mind overlay self-expiring: effective_tier_id
-- now checks last_feedback_at directly instead of only trusting
-- beta_testers.status. Background workers that would otherwise flip status
-- to 'dropped' on a schedule are disabled in this codebase (see Program.fs
-- "DISABLED: Background workers causing database bloat"), so the access
-- grant itself must not depend on one running. A lazy sweep
-- (BetaTesterRepository.sweepExpired, called from request paths that touch
-- beta_testers) still flips status to 'dropped' for bookkeeping/display, but
-- effective_tier_id is correct even if that sweep hasn't run yet.
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
          WHERE bt.user_id = p_user_id
            AND bt.status = 'active'
            AND bt.last_feedback_at > now() - interval '30 days'
      );

    IF beta_order IS NOT NULL AND (real_order IS NULL OR beta_order > real_order) THEN
        RETURN 'curious-mind';
    END IF;

    RETURN real_tier;
END;
$$ LANGUAGE plpgsql STABLE;
