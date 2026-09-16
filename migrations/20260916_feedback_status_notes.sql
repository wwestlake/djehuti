-- Lets an admin mark a piece of beta feedback resolved and attach their own
-- working notes to it, separate from the tester's own message text.
ALTER TABLE product_feedback
    ADD COLUMN status text NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'resolved')),
    ADD COLUMN admin_notes text NOT NULL DEFAULT '';
