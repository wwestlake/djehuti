-- Generic file attachments for product_feedback -- screenshots pasted from
-- clipboard, dragged-in log files, project files, whatever. One feedback
-- submission can have several. Stored in the same shared S3_BUCKET other
-- general media already uses (DjeLab, assets, etc.) under a
-- feedback-attachments/ prefix -- unlike Frate pods, there's no real reason
-- for this to be a separate bucket, it's the same kind of "user-submitted
-- media" the shared bucket already holds.
CREATE TABLE IF NOT EXISTS product_feedback_attachments (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    feedback_id  UUID NOT NULL REFERENCES product_feedback(id) ON DELETE CASCADE,
    file_name    TEXT NOT NULL,
    content_type TEXT NOT NULL,
    size_bytes   BIGINT NOT NULL,
    s3_key       TEXT NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_product_feedback_attachments_feedback ON product_feedback_attachments(feedback_id);

GRANT ALL ON TABLE product_feedback_attachments TO djehuti;
