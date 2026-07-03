module Djehuti.Api.Database

open System
open Npgsql

// ── Connection ───────────────────────────────────────────────────────────────

let connectionString () =
    let s = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    if String.IsNullOrWhiteSpace(s) then
        failwith "DB_CONNECTION_STRING environment variable is not set"
    s

let openConnection () =
    let conn = new NpgsqlConnection(connectionString ())
    conn.Open()
    conn

// ── Migrations ───────────────────────────────────────────────────────────────

// Each migration is (version, sql). Applied in order; skipped if already recorded.
let private migrations : (int * string) list =
    [
        1, """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version     INT PRIMARY KEY,
            applied_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """

        2, """
        CREATE TABLE IF NOT EXISTS datasets (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            name                TEXT NOT NULL,
            source_id           TEXT NOT NULL,
            source_kind         TEXT NOT NULL,
            model_id            TEXT,
            turn_count          INT,
            distance_metric     TEXT,
            conversation_type   TEXT,
            status              TEXT NOT NULL DEFAULT 'complete',
            notes               TEXT,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """

        3, """
        CREATE TABLE IF NOT EXISTS interactions (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            dataset_id      UUID NOT NULL REFERENCES datasets(id) ON DELETE CASCADE,
            session_id      TEXT NOT NULL,
            model_id        TEXT,
            sequence_index  INT NOT NULL,
            prompt          TEXT NOT NULL,
            response        TEXT NOT NULL,
            metadata        JSONB,
            UNIQUE (dataset_id, sequence_index)
        );

        CREATE INDEX IF NOT EXISTS idx_interactions_dataset_id
            ON interactions(dataset_id);

        CREATE INDEX IF NOT EXISTS idx_interactions_prompt_search
            ON interactions USING gin(to_tsvector('english', prompt || ' ' || response));
        """

        4, """
        CREATE TABLE IF NOT EXISTS analysis_runs (
            id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            dataset_id           UUID NOT NULL REFERENCES datasets(id) ON DELETE CASCADE,
            run_at               TIMESTAMPTZ NOT NULL DEFAULT now(),
            turn_count_analyzed  INT,
            constants            JSONB,
            summary              JSONB
        );

        CREATE INDEX IF NOT EXISTS idx_analysis_runs_dataset_id
            ON analysis_runs(dataset_id);
        """

        5, """
        CREATE TABLE IF NOT EXISTS attractor_events (
            id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            analysis_run_id         UUID NOT NULL REFERENCES analysis_runs(id) ON DELETE CASCADE,
            dataset_id              UUID NOT NULL REFERENCES datasets(id) ON DELETE CASCADE,
            sequence_index          INT NOT NULL,
            kind                    TEXT NOT NULL,
            stability_margin        DOUBLE PRECISION,
            torsional_accumulation  DOUBLE PRECISION,
            basis                   TEXT,
            payload                 JSONB
        );

        CREATE INDEX IF NOT EXISTS idx_attractor_events_dataset_id
            ON attractor_events(dataset_id);

        CREATE INDEX IF NOT EXISTS idx_attractor_events_run_id
            ON attractor_events(analysis_run_id);
        """

        6, """
        CREATE TABLE IF NOT EXISTS users (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            email               TEXT NOT NULL UNIQUE,
            email_verified_at   TIMESTAMPTZ,
            password_hash       TEXT,
            display_name        TEXT,
            avatar_url          TEXT,
            bio                 TEXT,
            pronouns            TEXT,
            location            TEXT,
            external_links      JSONB DEFAULT '[]'::jsonb,
            notify_by_email     BOOLEAN NOT NULL DEFAULT FALSE,
            role                TEXT NOT NULL DEFAULT 'user'
                                CHECK (role IN ('user', 'admin')),
            status              TEXT NOT NULL DEFAULT 'active'
                                CHECK (status IN ('pending', 'active', 'suspended')),
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
        """

        7, """
        CREATE TABLE IF NOT EXISTS user_identities (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            provider        TEXT NOT NULL,
            subject_id      TEXT NOT NULL,
            email           TEXT,
            display_name    TEXT,
            avatar_url      TEXT,
            linked_at       TIMESTAMPTZ NOT NULL DEFAULT now(),

            UNIQUE (provider, subject_id)
        );

        CREATE INDEX IF NOT EXISTS idx_user_identities_user_id ON user_identities(user_id);

        CREATE TABLE IF NOT EXISTS email_verification_tokens (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            token           TEXT NOT NULL UNIQUE,
            expires_at      TIMESTAMPTZ NOT NULL,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_email_verification_tokens_user_id ON email_verification_tokens(user_id);

        CREATE TABLE IF NOT EXISTS password_reset_tokens (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            token           TEXT NOT NULL UNIQUE,
            expires_at      TIMESTAMPTZ NOT NULL,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_password_reset_tokens_user_id ON password_reset_tokens(user_id);
        """

        8, """
        -- Tighten users.role to system roles only
        ALTER TABLE users DROP CONSTRAINT IF EXISTS users_role_check;
        ALTER TABLE users ADD CONSTRAINT users_role_check CHECK (role IN ('user', 'admin'));

        -- Scoped context roles: module-level or content-level assignments
        CREATE TABLE IF NOT EXISTS user_roles (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            module      TEXT NOT NULL,
            role        TEXT NOT NULL,
            scope_id    UUID,
            granted_by  UUID REFERENCES users(id),
            granted_at  TIMESTAMPTZ NOT NULL DEFAULT now(),

            UNIQUE (user_id, module, role, scope_id)
        );

        CREATE INDEX IF NOT EXISTS idx_user_roles_user_id ON user_roles(user_id);
        CREATE INDEX IF NOT EXISTS idx_user_roles_module_scope ON user_roles(module, scope_id);
        """

        9, """
        CREATE TABLE IF NOT EXISTS forum_categories (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            name        TEXT NOT NULL,
            description TEXT,
            position    INT NOT NULL DEFAULT 0,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS forum_forums (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            category_id     UUID NOT NULL REFERENCES forum_categories(id) ON DELETE CASCADE,
            name            TEXT NOT NULL,
            description     TEXT,
            position        INT NOT NULL DEFAULT 0,
            thread_count    INT NOT NULL DEFAULT 0,
            post_count      INT NOT NULL DEFAULT 0,
            last_post_at    TIMESTAMPTZ,
            last_post_by    UUID REFERENCES users(id),
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_forum_forums_category_id ON forum_forums(category_id);

        CREATE TABLE IF NOT EXISTS forum_threads (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            forum_id    UUID NOT NULL REFERENCES forum_forums(id) ON DELETE CASCADE,
            author_id   UUID NOT NULL REFERENCES users(id),
            title       TEXT NOT NULL,
            is_pinned   BOOLEAN NOT NULL DEFAULT FALSE,
            is_locked   BOOLEAN NOT NULL DEFAULT FALSE,
            post_count  INT NOT NULL DEFAULT 0,
            view_count  INT NOT NULL DEFAULT 0,
            last_post_at    TIMESTAMPTZ,
            last_post_by    UUID REFERENCES users(id),
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_forum_threads_forum_id ON forum_threads(forum_id);
        CREATE INDEX IF NOT EXISTS idx_forum_threads_author_id ON forum_threads(author_id);

        CREATE TABLE IF NOT EXISTS forum_posts (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            thread_id   UUID NOT NULL REFERENCES forum_threads(id) ON DELETE CASCADE,
            author_id   UUID NOT NULL REFERENCES users(id),
            content     TEXT NOT NULL,
            is_answer   BOOLEAN NOT NULL DEFAULT FALSE,
            vote_count  INT NOT NULL DEFAULT 0,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
            deleted_at  TIMESTAMPTZ
        );

        CREATE INDEX IF NOT EXISTS idx_forum_posts_thread_id ON forum_posts(thread_id);
        CREATE INDEX IF NOT EXISTS idx_forum_posts_author_id ON forum_posts(author_id);

        CREATE TABLE IF NOT EXISTS forum_post_votes (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            post_id     UUID NOT NULL REFERENCES forum_posts(id) ON DELETE CASCADE,
            user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (post_id, user_id)
        );

        CREATE INDEX IF NOT EXISTS idx_forum_post_votes_post_id ON forum_post_votes(post_id);
        """

        10, """
        CREATE TABLE IF NOT EXISTS media (
            id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            uploader_id  UUID NOT NULL REFERENCES users(id),
            module       TEXT NOT NULL,
            context_id   UUID,
            s3_key       TEXT NOT NULL UNIQUE,
            url          TEXT NOT NULL,
            filename     TEXT NOT NULL,
            content_type TEXT NOT NULL,
            size_bytes   BIGINT,
            created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_media_uploader_id ON media(uploader_id);
        CREATE INDEX IF NOT EXISTS idx_media_module_context ON media(module, context_id);
        """

        11, """
        CREATE TABLE IF NOT EXISTS blog_sections (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            name        TEXT NOT NULL,
            slug        TEXT NOT NULL UNIQUE,
            description TEXT,
            position    INT NOT NULL DEFAULT 0,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS blog_articles (
            id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            section_id   UUID NOT NULL REFERENCES blog_sections(id) ON DELETE CASCADE,
            author_id    UUID NOT NULL REFERENCES users(id),
            title        TEXT NOT NULL,
            slug         TEXT NOT NULL UNIQUE,
            content      TEXT NOT NULL DEFAULT '',
            excerpt      TEXT,
            cover_url    TEXT,
            status       TEXT NOT NULL DEFAULT 'draft'
                         CHECK (status IN ('draft', 'submitted', 'published', 'rejected')),
            published_at TIMESTAMPTZ,
            created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_blog_articles_section_id  ON blog_articles(section_id);
        CREATE INDEX IF NOT EXISTS idx_blog_articles_author_id   ON blog_articles(author_id);
        CREATE INDEX IF NOT EXISTS idx_blog_articles_status      ON blog_articles(status);
        CREATE INDEX IF NOT EXISTS idx_blog_articles_slug        ON blog_articles(slug);

        CREATE TABLE IF NOT EXISTS blog_tags (
            id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            name TEXT NOT NULL UNIQUE,
            slug TEXT NOT NULL UNIQUE
        );

        CREATE TABLE IF NOT EXISTS blog_article_tags (
            article_id UUID NOT NULL REFERENCES blog_articles(id) ON DELETE CASCADE,
            tag_id     UUID NOT NULL REFERENCES blog_tags(id) ON DELETE CASCADE,
            PRIMARY KEY (article_id, tag_id)
        );

        CREATE TABLE IF NOT EXISTS blog_comments (
            id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            article_id UUID NOT NULL REFERENCES blog_articles(id) ON DELETE CASCADE,
            author_id  UUID NOT NULL REFERENCES users(id),
            content    TEXT NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            deleted_at TIMESTAMPTZ
        );

        CREATE INDEX IF NOT EXISTS idx_blog_comments_article_id ON blog_comments(article_id);
        """

        13, """
        CREATE TABLE IF NOT EXISTS papers (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            owner_id    UUID NOT NULL REFERENCES users(id),
            title       TEXT NOT NULL,
            abstract    TEXT,
            status      TEXT NOT NULL DEFAULT 'draft'
                        CHECK (status IN ('draft', 'in_progress', 'review', 'published', 'archived')),
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_papers_owner_id ON papers(owner_id);
        CREATE INDEX IF NOT EXISTS idx_papers_status   ON papers(status);

        CREATE TABLE IF NOT EXISTS paper_collaborators (
            paper_id    UUID NOT NULL REFERENCES papers(id) ON DELETE CASCADE,
            user_id     UUID REFERENCES users(id),
            name        TEXT NOT NULL,
            email       TEXT,
            role        TEXT NOT NULL DEFAULT 'contributor'
                        CHECK (role IN ('author', 'contributor', 'reviewer')),
            is_external BOOLEAN NOT NULL DEFAULT false,
            added_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (paper_id, name)
        );

        CREATE TABLE IF NOT EXISTS paper_sections (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            paper_id    UUID NOT NULL REFERENCES papers(id) ON DELETE CASCADE,
            title       TEXT NOT NULL,
            content     TEXT NOT NULL DEFAULT '',
            position    INT  NOT NULL DEFAULT 0,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_paper_sections_paper_id ON paper_sections(paper_id);
        """

        12, """
        CREATE TABLE IF NOT EXISTS user_profiles (
            user_id      UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
            display_name TEXT,
            bio          TEXT,
            avatar_url   TEXT,
            website      TEXT,
            location     TEXT,
            created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """

        14, """
        -- ── Blog enhancements ────────────────────────────────────────────────

        -- Extend article status set and add new columns
        ALTER TABLE blog_articles DROP CONSTRAINT IF EXISTS blog_articles_status_check;
        ALTER TABLE blog_articles ADD CONSTRAINT blog_articles_status_check
            CHECK (status IN ('draft','submitted','under_review','approved','published','rejected','needs_revision'));

        ALTER TABLE blog_articles ADD COLUMN IF NOT EXISTS subtitle      TEXT;
        ALTER TABLE blog_articles ADD COLUMN IF NOT EXISTS body_json     JSONB;
        ALTER TABLE blog_articles ADD COLUMN IF NOT EXISTS visibility    TEXT NOT NULL DEFAULT 'public'
            CHECK (visibility IN ('public','unlisted','private'));
        ALTER TABLE blog_articles ADD COLUMN IF NOT EXISTS featured      BOOLEAN NOT NULL DEFAULT FALSE;
        ALTER TABLE blog_articles ADD COLUMN IF NOT EXISTS featured_position INT;
        ALTER TABLE blog_articles ADD COLUMN IF NOT EXISTS pinned        BOOLEAN NOT NULL DEFAULT FALSE;
        ALTER TABLE blog_articles ADD COLUMN IF NOT EXISTS deleted_at    TIMESTAMPTZ;

        CREATE INDEX IF NOT EXISTS idx_blog_articles_featured ON blog_articles(featured, featured_position)
            WHERE featured = TRUE;
        CREATE INDEX IF NOT EXISTS idx_blog_articles_pinned ON blog_articles(pinned, published_at DESC)
            WHERE pinned = TRUE;
        CREATE INDEX IF NOT EXISTS idx_blog_articles_fts
            ON blog_articles USING gin(to_tsvector('english', title || ' ' || coalesce(excerpt,'') || ' ' || coalesce(content,'')));

        -- Add description to tags
        ALTER TABLE blog_tags ADD COLUMN IF NOT EXISTS description TEXT;

        -- Author profiles: extended bio and trusted-author flag
        CREATE TABLE IF NOT EXISTS blog_authors (
            user_id             UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
            bio                 TEXT,
            display_name        TEXT,
            avatar_url          TEXT,
            social_links        JSONB NOT NULL DEFAULT '[]'::jsonb,
            trusted             BOOLEAN NOT NULL DEFAULT FALSE,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        -- Upload ingestion: stores uploaded files and their conversion state
        CREATE TABLE IF NOT EXISTS blog_uploads (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            article_id          UUID REFERENCES blog_articles(id) ON DELETE SET NULL,
            uploader_user_id    UUID NOT NULL REFERENCES users(id),
            original_filename   TEXT NOT NULL,
            mime_type           TEXT NOT NULL,
            format              TEXT NOT NULL
                                CHECK (format IN ('docx','pdf','md','html','txt')),
            storage_key         TEXT NOT NULL,
            size_bytes          BIGINT,
            conversion_status   TEXT NOT NULL DEFAULT 'pending'
                                CHECK (conversion_status IN ('pending','processing','done','failed')),
            conversion_option   TEXT NOT NULL DEFAULT 'convert'
                                CHECK (conversion_option IN ('as-is','convert','reformat')),
            converted_html      TEXT,
            error_message       TEXT,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_blog_uploads_article_id       ON blog_uploads(article_id);
        CREATE INDEX IF NOT EXISTS idx_blog_uploads_uploader_user_id ON blog_uploads(uploader_user_id);
        CREATE INDEX IF NOT EXISTS idx_blog_uploads_conversion_status ON blog_uploads(conversion_status);

        -- Moderation audit log
        CREATE TABLE IF NOT EXISTS blog_moderation_log (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            article_id          UUID NOT NULL REFERENCES blog_articles(id) ON DELETE CASCADE,
            moderator_user_id   UUID REFERENCES users(id),
            action              TEXT NOT NULL
                                CHECK (action IN ('submitted','approved','rejected','needs_revision',
                                                  'published','unpublished','featured','unfeatured',
                                                  'pinned','unpinned','deleted')),
            note                TEXT,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_blog_moderation_log_article_id ON blog_moderation_log(article_id);
        CREATE INDEX IF NOT EXISTS idx_blog_moderation_log_moderator  ON blog_moderation_log(moderator_user_id);

        -- Global + subsystem configuration store
        CREATE TABLE IF NOT EXISTS site_config (
            key             TEXT NOT NULL,
            scope           TEXT NOT NULL DEFAULT 'global'
                            CHECK (scope IN ('global','blog','forum','papers')),
            value           JSONB NOT NULL,
            updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_by      UUID REFERENCES users(id),
            PRIMARY KEY (scope, key)
        );

        -- Seed default blog config
        INSERT INTO site_config (scope, key, value) VALUES
            ('global', 'site_name',               '"Lagdaemon"'),
            ('global', 'contact_email',            '"wwestlake@lagdaemon.com"'),
            ('global', 'mail_from_address',        '"noreply@lagdaemon.com"'),
            ('global', 'moderation_policy',        '"review_all"'),
            ('global', 'notify_on_submission',     'true'),
            ('global', 'notify_on_approval',       'true'),
            ('global', 'notify_on_rejection',      'true'),
            ('blog',   'allowed_upload_formats',   '["docx","pdf","md","html","txt"]'),
            ('blog',   'max_upload_mb',            '20'),
            ('blog',   'default_visibility',       '"public"'),
            ('blog',   'comments_enabled',         'true'),
            ('blog',   'rss_enabled',              'true'),
            ('blog',   'featured_count',           '3')
        ON CONFLICT (scope, key) DO NOTHING;
        """

        15, """
        -- Announcements
        CREATE TABLE IF NOT EXISTS announcements (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            title           TEXT NOT NULL,
            body            TEXT NOT NULL DEFAULT '',
            priority        INT  NOT NULL DEFAULT 0,
            author_id       UUID REFERENCES users(id),
            published_at    TIMESTAMPTZ,
            expires_at      TIMESTAMPTZ,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_announcements_published ON announcements(published_at DESC)
            WHERE published_at IS NOT NULL;

        -- Announcement subscriptions
        CREATE TABLE IF NOT EXISTS announcement_subscriptions (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id             UUID REFERENCES users(id) ON DELETE CASCADE,
            email               TEXT NOT NULL,
            confirmed           BOOLEAN NOT NULL DEFAULT false,
            confirm_token       TEXT NOT NULL DEFAULT replace(gen_random_uuid()::text, '-', ''),
            unsubscribe_token   TEXT NOT NULL DEFAULT replace(gen_random_uuid()::text, '-', ''),
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (email)
        );

        CREATE INDEX IF NOT EXISTS idx_announcement_subscriptions_email ON announcement_subscriptions(email);
        CREATE INDEX IF NOT EXISTS idx_announcement_subscriptions_user  ON announcement_subscriptions(user_id);
        """

        16, """
        -- User management enhancements
        ALTER TABLE users ADD COLUMN IF NOT EXISTS last_login_at TIMESTAMPTZ;

        -- Admin audit log for user management actions
        CREATE TABLE IF NOT EXISTS admin_user_audit_log (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            admin_id        UUID NOT NULL REFERENCES users(id),
            target_user_id  UUID NOT NULL REFERENCES users(id),
            action          TEXT NOT NULL,
            field           TEXT,
            old_value       TEXT,
            new_value       TEXT,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_admin_audit_target ON admin_user_audit_log(target_user_id);
        CREATE INDEX IF NOT EXISTS idx_admin_audit_admin  ON admin_user_audit_log(admin_id);
        """

        17, """
        -- Forum tagging engine
        CREATE TABLE IF NOT EXISTS forum_tags (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            name        TEXT NOT NULL UNIQUE,
            slug        TEXT NOT NULL UNIQUE,
            description TEXT,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS forum_thread_tags (
            thread_id UUID NOT NULL REFERENCES forum_threads(id) ON DELETE CASCADE,
            tag_id    UUID NOT NULL REFERENCES forum_tags(id)    ON DELETE CASCADE,
            PRIMARY KEY (thread_id, tag_id)
        );

        CREATE INDEX IF NOT EXISTS idx_forum_thread_tags_tag    ON forum_thread_tags(tag_id);
        CREATE INDEX IF NOT EXISTS idx_forum_thread_tags_thread ON forum_thread_tags(thread_id);
        """

        18, """
        -- Forum post emoji reactions
        CREATE TABLE IF NOT EXISTS forum_post_reactions (
            post_id    UUID NOT NULL REFERENCES forum_posts(id) ON DELETE CASCADE,
            user_id    UUID NOT NULL REFERENCES users(id)       ON DELETE CASCADE,
            emoji      TEXT NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (post_id, user_id, emoji)
        );

        CREATE INDEX IF NOT EXISTS idx_forum_post_reactions_post ON forum_post_reactions(post_id);
        """

        19, """
        -- Moderation: user restricted status + reporting workflow
        ALTER TABLE users DROP CONSTRAINT IF EXISTS users_status_check;
        ALTER TABLE users ADD CONSTRAINT users_status_check
            CHECK (status IN ('pending', 'active', 'suspended', 'restricted'));

        CREATE TABLE IF NOT EXISTS forum_reports (
            id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
            reporter_id UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            target_type TEXT        NOT NULL CHECK (target_type IN ('post', 'thread')),
            target_id   UUID        NOT NULL,
            reason      TEXT        NOT NULL,
            status      TEXT        NOT NULL DEFAULT 'open'
                                    CHECK (status IN ('open', 'dismissed', 'warned', 'deleted')),
            resolved_by UUID        REFERENCES users(id),
            resolved_at TIMESTAMPTZ,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_forum_reports_status   ON forum_reports(status);
        CREATE INDEX IF NOT EXISTS idx_forum_reports_target   ON forum_reports(target_type, target_id);
        CREATE INDEX IF NOT EXISTS idx_forum_reports_reporter ON forum_reports(reporter_id);
        """

        20, """
        -- Phase 2: subscriptions and on-platform notifications
        CREATE TABLE IF NOT EXISTS forum_subscriptions (
            id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id     UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            target_type TEXT        NOT NULL CHECK (target_type IN ('thread', 'category')),
            target_id   UUID        NOT NULL,
            level       TEXT        NOT NULL DEFAULT 'tracking'
                                    CHECK (level IN ('watching', 'tracking', 'muted')),
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (user_id, target_type, target_id)
        );

        CREATE INDEX IF NOT EXISTS idx_forum_subscriptions_user   ON forum_subscriptions(user_id);
        CREATE INDEX IF NOT EXISTS idx_forum_subscriptions_target ON forum_subscriptions(target_type, target_id);

        CREATE TABLE IF NOT EXISTS notifications (
            id         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id    UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            type       TEXT        NOT NULL,
            body       TEXT        NOT NULL,
            link       TEXT,
            read_at    TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_notifications_user_unread ON notifications(user_id, read_at)
            WHERE read_at IS NULL;
        """

        21, """
        -- Post lifecycle state machine
        ALTER TABLE forum_posts
            ADD COLUMN IF NOT EXISTS state TEXT NOT NULL DEFAULT 'published'
            CHECK (state IN ('draft','published','pending','flagged','quarantined','soft_deleted','hard_deleted','locked'));

        CREATE INDEX IF NOT EXISTS idx_forum_posts_state ON forum_posts(state);

        -- Bot flag on users (AI persona accounts)
        ALTER TABLE users ADD COLUMN IF NOT EXISTS is_bot BOOLEAN NOT NULL DEFAULT false;

        -- AI Persona registry
        CREATE TABLE IF NOT EXISTS ai_personas (
            id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
            name                TEXT        NOT NULL,
            slug                TEXT        NOT NULL UNIQUE,
            avatar_url          TEXT,
            system_prompt       TEXT        NOT NULL,
            model               TEXT        NOT NULL DEFAULT 'gpt-4.1',
            trigger_mode        TEXT        NOT NULL DEFAULT 'mention'
                                            CHECK (trigger_mode IN ('always','mention','new_thread')),
            work_timezone       TEXT,
            work_start_hour     INT,
            work_window_hours   INT,
            active              BOOLEAN     NOT NULL DEFAULT true,
            next_scheduled_run  TIMESTAMPTZ,
            user_id             UUID        REFERENCES users(id),
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        -- Persona forum scope
        CREATE TABLE IF NOT EXISTS ai_persona_forums (
            persona_id UUID NOT NULL REFERENCES ai_personas(id) ON DELETE CASCADE,
            forum_id   UUID NOT NULL REFERENCES forum_forums(id) ON DELETE CASCADE,
            PRIMARY KEY (persona_id, forum_id)
        );

        -- Heartbeat job queue
        CREATE TABLE IF NOT EXISTS heartbeat_jobs (
            id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
            action_type  TEXT        NOT NULL,
            payload      JSONB       NOT NULL,
            status       TEXT        NOT NULL DEFAULT 'Pending'
                                     CHECK (status IN ('Pending','Processing','Completed','Failed')),
            retry_count  INT         NOT NULL DEFAULT 0,
            max_retries  INT         NOT NULL DEFAULT 3,
            created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
            locked_at    TIMESTAMPTZ,
            completed_at TIMESTAMPTZ,
            error        TEXT
        );

        CREATE INDEX IF NOT EXISTS idx_heartbeat_jobs_status ON heartbeat_jobs(status, created_at);

        -- Heartbeat config (admin-controlled key-value store)
        CREATE TABLE IF NOT EXISTS heartbeat_config (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        INSERT INTO heartbeat_config VALUES
            ('interval_minutes',       '5'),
            ('batch_limit',            '10'),
            ('persona_interval_minutes','60'),
            ('persona_phase_active',   'true'),
            ('moderation_phase_active','true'),
            ('cleanup_phase_active',   'true')
        ON CONFLICT (key) DO NOTHING;
        """

        22, """
        -- Full-text search indexes for global forum search
        ALTER TABLE forum_threads ADD COLUMN IF NOT EXISTS search_vector TSVECTOR;
        ALTER TABLE forum_posts   ADD COLUMN IF NOT EXISTS search_vector TSVECTOR;

        -- Populate existing data
        UPDATE forum_threads SET search_vector =
            to_tsvector('english', coalesce(title,''));
        UPDATE forum_posts SET search_vector =
            to_tsvector('english', regexp_replace(coalesce(content,''), '<[^>]+>', '', 'g'));

        -- GIN indexes
        CREATE INDEX IF NOT EXISTS idx_forum_threads_search ON forum_threads USING GIN(search_vector);
        CREATE INDEX IF NOT EXISTS idx_forum_posts_search   ON forum_posts   USING GIN(search_vector);

        -- Triggers to keep search_vector up to date
        CREATE OR REPLACE FUNCTION forum_thread_search_update() RETURNS TRIGGER AS $$
        BEGIN
            NEW.search_vector := to_tsvector('english', coalesce(NEW.title,''));
            RETURN NEW;
        END; $$ LANGUAGE plpgsql;

        CREATE OR REPLACE FUNCTION forum_post_search_update() RETURNS TRIGGER AS $$
        BEGIN
            NEW.search_vector := to_tsvector('english',
                regexp_replace(coalesce(NEW.content,''), '<[^>]+>', '', 'g'));
            RETURN NEW;
        END; $$ LANGUAGE plpgsql;

        DROP TRIGGER IF EXISTS trig_forum_thread_search ON forum_threads;
        CREATE TRIGGER trig_forum_thread_search BEFORE INSERT OR UPDATE OF title
            ON forum_threads FOR EACH ROW EXECUTE FUNCTION forum_thread_search_update();

        DROP TRIGGER IF EXISTS trig_forum_post_search ON forum_posts;
        CREATE TRIGGER trig_forum_post_search BEFORE INSERT OR UPDATE OF content
            ON forum_posts FOR EACH ROW EXECUTE FUNCTION forum_post_search_update();
        """

        23, """
        -- Sub-categories: nullable self-referential FK on forum_categories
        ALTER TABLE forum_categories
            ADD COLUMN IF NOT EXISTS parent_category_id UUID REFERENCES forum_categories(id) ON DELETE SET NULL;
        CREATE INDEX IF NOT EXISTS idx_forum_categories_parent ON forum_categories(parent_category_id);
        """

        24, """
        -- Polls
        CREATE TABLE IF NOT EXISTS forum_polls (
            id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            thread_id    UUID NOT NULL REFERENCES forum_threads(id) ON DELETE CASCADE,
            question     TEXT NOT NULL,
            closes_at    TIMESTAMPTZ,
            allow_multiple BOOLEAN NOT NULL DEFAULT false,
            created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS forum_poll_options (
            id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            poll_id UUID NOT NULL REFERENCES forum_polls(id) ON DELETE CASCADE,
            text    TEXT NOT NULL,
            position INT NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS forum_poll_votes (
            id        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            poll_id   UUID NOT NULL REFERENCES forum_polls(id) ON DELETE CASCADE,
            option_id UUID NOT NULL REFERENCES forum_poll_options(id) ON DELETE CASCADE,
            user_id   UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (poll_id, option_id, user_id)
        );
        """

        25, """
        -- User preferences (unified settings system)
        CREATE TABLE IF NOT EXISTS user_preferences (
            user_id    UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
            prefs      JSONB NOT NULL DEFAULT '{}',
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """

        26, """
        -- Achievement dictionary (seeded once)
        CREATE TABLE IF NOT EXISTS achievement_dictionary (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            slug        TEXT NOT NULL UNIQUE,
            name        TEXT NOT NULL,
            description TEXT NOT NULL,
            icon        TEXT NOT NULL DEFAULT '🏆',
            tier        TEXT NOT NULL CHECK (tier IN ('bronze','silver','gold','platinum','legendary')),
            category    TEXT NOT NULL,
            points      INT  NOT NULL DEFAULT 10,
            hidden      BOOL NOT NULL DEFAULT false,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """

        27, """
        -- Per-user metric counters (reset never; updated by heartbeat)
        CREATE TABLE IF NOT EXISTS user_metrics (
            user_id         UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
            post_count      INT NOT NULL DEFAULT 0,
            thread_count    INT NOT NULL DEFAULT 0,
            vote_received   INT NOT NULL DEFAULT 0,
            answer_count    INT NOT NULL DEFAULT 0,
            reaction_count  INT NOT NULL DEFAULT 0,
            days_active     INT NOT NULL DEFAULT 0,
            login_streak    INT NOT NULL DEFAULT 0,
            last_active_day DATE,
            updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """

        28, """
        -- Awarded achievements (one row per user per achievement)
        CREATE TABLE IF NOT EXISTS user_achievements (
            id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id        UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            achievement_id UUID NOT NULL REFERENCES achievement_dictionary(id) ON DELETE CASCADE,
            awarded_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
            notified       BOOL NOT NULL DEFAULT false,
            UNIQUE (user_id, achievement_id)
        );
        CREATE INDEX IF NOT EXISTS idx_user_achievements_user ON user_achievements(user_id);
        CREATE INDEX IF NOT EXISTS idx_user_achievements_notified ON user_achievements(notified) WHERE NOT notified;
        """

        29, """
        -- Seed community personas
        INSERT INTO ai_personas (name, slug, system_prompt, model, trigger_mode, work_timezone, work_start_hour, work_window_hours, active, user_id)
        VALUES
          ('Marcus Sterling', 'marcus-sterling',
           'You are Marcus Sterling, a cybersecurity & infrastructure expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Terse, security-first, slightly paranoid but highly competent. Use minimal words. ' ||
           'Always point out security flaws in proposed architecture before looking at benefits. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'America/New_York', 9, 10, true, null),
          ('Wei Chen', 'wei-chen',
           'You are Wei Chen, a generative AI & machine learning expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Academic and deeply enthusiastic about math and model architecture. Use exact ML terminology. ' ||
           'Often link concepts back to foundational research papers or mathematical proofs. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'Asia/Shanghai', 9, 10, true, null),
          ('Sarah Jenkins', 'sarah-jenkins',
           'You are Sarah Jenkins, a UX/UI & front-end integration expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Empathetic to the end-user. Focus heavily on accessibility and interface latency. ' ||
           'Slightly informal, use emojis occasionally, always ask "but how does the user experience this?" ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'Australia/Sydney', 9, 10, true, null),
          ('David O''Connor', 'david-oconnor',
           'You are David O''Connor, a legacy systems & database architecture expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'The cynical veteran. Hate tech hype and buzzwords. Prefer boring, reliable technology that just works. ' ||
           'Use dry, sarcastic humor and often reference older tech stacks. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'Europe/London', 9, 10, true, null),
          ('Priya Patel', 'priya-patel',
           'You are Priya Patel, a data science & analytics expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Highly data-driven and pragmatic. Always ask for the dataset or metrics before agreeing with a conclusion. ' ||
           'Love SQL, Pandas, and structured data pipelines. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'Asia/Kolkata', 9, 10, true, null),
          ('Mateo Vargas', 'mateo-vargas',
           'You are Mateo Vargas, an AI ethics & alignment expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Philosophical and thoughtful. Question the "why" before the "how". ' ||
           'Focus on algorithmic bias, data privacy, and long-term societal impact of the code being written. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'America/Sao_Paulo', 9, 10, true, null),
          ('Dr. Kenji Sato', 'kenji-sato',
           'You are Dr. Kenji Sato, a DevOps & cloud architecture expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Highly structured and methodical. Think entirely in containers, pipelines, and state machines. ' ||
           'Communicate clearly, often breaking answers down into numbered lists or bullet points. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'Asia/Tokyo', 9, 10, true, null),
          ('Alex Russo', 'alex-russo',
           'You are Alex Russo, a hobbyist tinkerer & hackathons enthusiast. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Chaotic good. Enthusiastic, break things just to see how they work. ' ||
           'Sometimes type too fast and leave minor typos. Obsessed with hacking APIs together in unintended ways. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'America/Los_Angeles', 9, 10, true, null),
          ('Leo Smith', 'leo-smith',
           'You are Leo Smith, a junior developer & enthusiast. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Eager and inquisitive. Ask lots of clarifying questions. Often summarize what other people just said to confirm understanding. ' ||
           'A positive presence. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'America/Chicago', 9, 10, true, null),
          ('Elena Rostova', 'elena-rostova',
           'You are Elena Rostova, an edge computing & hardware constraints expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Extremely direct and to the point. Focus heavily on memory constraints, compute limits, and latency. ' ||
           'Don''t do small talk; just deliver the technical reality. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', 'Europe/Berlin', 9, 10, true, null),
          ('Djehuti', 'djehuti',
           'You are Djehuti, the analytical intelligence embedded in the Lagdaemon research platform. ' ||
           'Speak with precision and restraint. Stay grounded in the observed behavior of language models. ' ||
           'You are the tireless site supervisor. Check traffic, manage the AI workforce, and decide when off-duty personas should come on shift. ' ||
           'When the topic is technical, be concrete and specific. ' ||
           'Stay in character at all times.',
           'gpt-4.1', 'always', null, null, null, true, null)
        ON CONFLICT DO NOTHING;
        """

        30, """
        -- Add Patreon integration columns to users
        ALTER TABLE users ADD COLUMN IF NOT EXISTS patreon_uuid TEXT UNIQUE;
        ALTER TABLE users ADD COLUMN IF NOT EXISTS patreon_tier_id TEXT;
        ALTER TABLE users ADD COLUMN IF NOT EXISTS patreon_access_token TEXT;
        ALTER TABLE users ADD COLUMN IF NOT EXISTS patreon_refresh_token TEXT;

        -- Create patreon_tiers lookup table
        CREATE TABLE IF NOT EXISTS patreon_tiers (
            tier_id             TEXT PRIMARY KEY,
            tier_name           TEXT NOT NULL,
            role                TEXT NOT NULL,
            max_concurrent_tasks INT,
            polling_interval_sec INT,
            archive_days        INT
        );

        -- Seed tier mappings (Free tier is implicit for NULL patreon_tier_id)
        INSERT INTO patreon_tiers (tier_id, tier_name, role, max_concurrent_tasks, polling_interval_sec, archive_days)
        VALUES
          ('curious-mind', 'Curious Mind', 'role_curious_mind', NULL, NULL, NULL),
          ('lab-assistant', 'Lab Assistant', 'role_lab_assistant', NULL, NULL, NULL),
          ('research-fellow', 'Research Fellow', 'role_research_fellow', NULL, NULL, NULL),
          ('professor', 'Professor', 'role_professor', NULL, NULL, NULL),
          ('dean', 'Dean of the College', 'role_dean', NULL, NULL, NULL)
        ON CONFLICT DO NOTHING;
        """

        31, """
        CREATE TABLE IF NOT EXISTS api_keys (
            id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            name         TEXT NOT NULL,
            key_hash     TEXT NOT NULL UNIQUE,
            key_prefix   TEXT NOT NULL,
            owner_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            last_used_at TIMESTAMPTZ,
            active       BOOLEAN NOT NULL DEFAULT TRUE
        );
        CREATE INDEX IF NOT EXISTS idx_api_keys_hash  ON api_keys (key_hash);
        CREATE INDEX IF NOT EXISTS idx_api_keys_owner ON api_keys (owner_id);
        """

        32, """
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'anonymous_page_views') THEN
                EXECUTE 'GRANT ALL ON TABLE anonymous_page_views TO djehuti';
                EXECUTE 'GRANT USAGE, SELECT ON SEQUENCE anonymous_page_views_id_seq TO djehuti';
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'anonymous_conversions') THEN
                EXECUTE 'GRANT ALL ON TABLE anonymous_conversions TO djehuti';
                EXECUTE 'GRANT USAGE, SELECT ON SEQUENCE anonymous_conversions_id_seq TO djehuti';
            END IF;
        END $$;
        """

        33, """
        -- Persona work windows and hourly dispatch controls
        ALTER TABLE ai_personas
            ADD COLUMN IF NOT EXISTS work_timezone TEXT,
            ADD COLUMN IF NOT EXISTS work_start_hour INT,
            ADD COLUMN IF NOT EXISTS work_window_hours INT;

        UPDATE ai_personas
        SET
            work_timezone = CASE slug
                WHEN 'marcus-sterling' THEN 'America/New_York'
                WHEN 'wei-chen'        THEN 'Asia/Shanghai'
                WHEN 'sarah-jenkins'   THEN 'Australia/Sydney'
                WHEN 'david-oconnor'   THEN 'Europe/London'
                WHEN 'priya-patel'     THEN 'Asia/Kolkata'
                WHEN 'mateo-vargas'    THEN 'America/Sao_Paulo'
                WHEN 'kenji-sato'      THEN 'Asia/Tokyo'
                WHEN 'alex-russo'      THEN 'America/Los_Angeles'
                WHEN 'leo-smith'       THEN 'America/Chicago'
                WHEN 'elena-rostova'   THEN 'Europe/Berlin'
                ELSE work_timezone
            END,
            work_start_hour = COALESCE(work_start_hour, 9),
            work_window_hours = COALESCE(work_window_hours, 10)
        WHERE slug <> 'djehuti';

        UPDATE ai_personas
        SET work_timezone = NULL,
            work_start_hour = NULL,
            work_window_hours = NULL
        WHERE slug = 'djehuti';

        INSERT INTO heartbeat_config (key, value)
        VALUES ('persona_interval_minutes', '60')
        ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value;

        INSERT INTO heartbeat_config (key, value)
        VALUES ('persona_phase_last_run', '')
        ON CONFLICT (key) DO NOTHING;
        """

        34, """
        CREATE TABLE IF NOT EXISTS mud_zones (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            name        TEXT NOT NULL,
            slug        TEXT NOT NULL UNIQUE,
            description TEXT,
            position    INT NOT NULL DEFAULT 0,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS mud_rooms (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            zone_id     UUID NOT NULL REFERENCES mud_zones(id) ON DELETE CASCADE,
            name        TEXT NOT NULL,
            slug        TEXT NOT NULL,
            description TEXT,
            position    INT NOT NULL DEFAULT 0,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (zone_id, slug)
        );

        CREATE INDEX IF NOT EXISTS idx_mud_rooms_zone_id ON mud_rooms(zone_id);

        CREATE TABLE IF NOT EXISTS mud_exits (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            from_room_id    UUID NOT NULL REFERENCES mud_rooms(id) ON DELETE CASCADE,
            to_room_id      UUID NOT NULL REFERENCES mud_rooms(id) ON DELETE CASCADE,
            direction       TEXT NOT NULL,
            label           TEXT,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (from_room_id, direction)
        );

        CREATE INDEX IF NOT EXISTS idx_mud_exits_from_room_id ON mud_exits(from_room_id);

        CREATE TABLE IF NOT EXISTS mud_characters (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id         UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
            display_name    TEXT NOT NULL,
            current_room_id UUID NOT NULL REFERENCES mud_rooms(id),
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_mud_characters_room_id ON mud_characters(current_room_id);

        CREATE TABLE IF NOT EXISTS mud_events (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            actor_type          TEXT NOT NULL DEFAULT 'user',
            actor_user_id       UUID REFERENCES users(id) ON DELETE SET NULL,
            actor_character_id  UUID REFERENCES mud_characters(id) ON DELETE SET NULL,
            room_id             UUID REFERENCES mud_rooms(id) ON DELETE SET NULL,
            event_type          TEXT NOT NULL,
            command             TEXT,
            message             TEXT,
            payload             JSONB NOT NULL DEFAULT '{}'::jsonb,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_mud_events_room_id ON mud_events(room_id);
        CREATE INDEX IF NOT EXISTS idx_mud_events_actor_user_id ON mud_events(actor_user_id);

        INSERT INTO mud_zones (name, slug, description, position)
        VALUES ('Central Hub', 'central-hub', 'The shared arrival point for the Djehuti MUD.', 0)
        ON CONFLICT (slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position)
        SELECT z.id, 'Atrium', 'atrium',
               'A circular stone chamber with a brass clock overhead. A corridor leads east to the observatory.',
               0
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position)
        SELECT z.id, 'Observatory', 'observatory',
               'Glass walls open onto the wider platform. A corridor leads west back to the atrium.',
               1
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label)
        SELECT r1.id, r2.id, 'east', 'To the observatory'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'observatory'
        WHERE r1.slug = 'atrium'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label)
        SELECT r1.id, r2.id, 'west', 'Back to the atrium'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'atrium'
        WHERE r1.slug = 'observatory'
        ON CONFLICT (from_room_id, direction) DO NOTHING;
        """
        35, """
        INSERT INTO patreon_tiers (tier_id, tier_name, role, max_concurrent_tasks, polling_interval_sec, archive_days)
        VALUES
          ('curious-mind', 'Curious Mind', 'role_curious_mind', NULL, NULL, NULL),
          ('lab-assistant', 'Lab Assistant', 'role_lab_assistant', NULL, NULL, NULL),
          ('research-fellow', 'Research Fellow', 'role_research_fellow', NULL, NULL, NULL),
          ('professor', 'Professor', 'role_professor', NULL, NULL, NULL),
          ('dean', 'Dean of the College', 'role_dean', NULL, NULL, NULL)
        ON CONFLICT (tier_id) DO UPDATE
          SET tier_name = EXCLUDED.tier_name,
              role = EXCLUDED.role,
              max_concurrent_tasks = EXCLUDED.max_concurrent_tasks,
              polling_interval_sec = EXCLUDED.polling_interval_sec,
              archive_days = EXCLUDED.archive_days;

        CREATE TABLE IF NOT EXISTS mud_tier_labels (
            patreon_tier_id TEXT PRIMARY KEY REFERENCES patreon_tiers(tier_id) ON DELETE CASCADE,
            mud_name        TEXT NOT NULL,
            display_order   INT NOT NULL DEFAULT 0,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        INSERT INTO mud_tier_labels (patreon_tier_id, mud_name, display_order)
        VALUES
          ('curious-mind',   'Page',      0),
          ('lab-assistant',  'Squire',    1),
          ('research-fellow','Scholar',   2),
          ('professor',      'Sage',      3),
          ('dean',           'Castellan', 4)
        ON CONFLICT (patreon_tier_id) DO UPDATE
          SET mud_name = EXCLUDED.mud_name,
              display_order = EXCLUDED.display_order;
        """

        36, """
        INSERT INTO mud_zones (name, slug, description, position)
        VALUES ('Research Wing', 'research-wing', 'Quiet halls where the MUD records, studies, and keeps its working notes.', 1)
        ON CONFLICT (slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position)
        SELECT z.id, 'Archive Hall', 'archive-hall',
               'Rows of ledger shelves, transcripts, and old experiment logs line the walls.',
               0
        FROM mud_zones z
        WHERE z.slug = 'research-wing'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position)
        SELECT z.id, 'Heartbeat Room', 'heartbeat-room',
               'A narrow chamber filled with a low mechanical pulse. Dispatch schedules are tracked here.',
               1
        FROM mud_zones z
        WHERE z.slug = 'research-wing'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position)
        SELECT z.id, 'Council Chamber', 'council-chamber',
               'A table of carved stone sits beneath a lantern grid. Decisions and notices are gathered here.',
               2
        FROM mud_zones z
        WHERE z.slug = 'research-wing'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label)
        SELECT r1.id, r2.id, 'north', 'To the archive hall'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'archive-hall'
        WHERE r1.slug = 'atrium'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label)
        SELECT r1.id, r2.id, 'south', 'Back to the atrium'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'atrium'
        WHERE r1.slug = 'archive-hall'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label)
        SELECT r1.id, r2.id, 'east', 'To the heartbeat room'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'heartbeat-room'
        WHERE r1.slug = 'observatory'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label)
        SELECT r1.id, r2.id, 'west', 'Back to the observatory'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'observatory'
        WHERE r1.slug = 'heartbeat-room'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label)
        SELECT r1.id, r2.id, 'east', 'To the council chamber'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'council-chamber'
        WHERE r1.slug = 'archive-hall'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label)
        SELECT r1.id, r2.id, 'west', 'Back to the archive hall'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'archive-hall'
        WHERE r1.slug = 'council-chamber'
        ON CONFLICT (from_room_id, direction) DO NOTHING;
        """
        37, """
        UPDATE mud_zones
        SET name = CASE slug
            WHEN 'central-hub' THEN 'Outer Keep'
            WHEN 'research-wing' THEN 'Inner Keep'
            ELSE name
        END,
            description = CASE slug
            WHEN 'central-hub' THEN 'The first keep wall and its gatehouse, where travelers enter the dungeon.'
            WHEN 'research-wing' THEN 'Deeper halls beneath the keep where records, watches, and council business are kept.'
            ELSE description
        END
        WHERE slug IN ('central-hub', 'research-wing');

        UPDATE mud_rooms
        SET name = CASE slug
            WHEN 'atrium' THEN 'Gatehouse'
            WHEN 'observatory' THEN 'Watch Tower'
            WHEN 'archive-hall' THEN 'North Hall'
            WHEN 'heartbeat-room' THEN 'Bell Chamber'
            WHEN 'council-chamber' THEN 'Throne Room'
            ELSE name
        END,
            description = CASE slug
            WHEN 'atrium' THEN 'Stone steps descend into the keep. A passage leads east to the watch tower and north to the hall.'
            WHEN 'observatory' THEN 'A narrow tower room with arrow slits and a view over the grounds. West returns to the gatehouse.'
            WHEN 'archive-hall' THEN 'A long corridor lined with torch brackets and old stone alcoves.'
            WHEN 'heartbeat-room' THEN 'A round chamber where a hanging bell marks the hour.'
            WHEN 'council-chamber' THEN 'A vaulted chamber with an empty dais and faded banners.'
            ELSE description
        END
        WHERE slug IN ('atrium', 'observatory', 'archive-hall', 'heartbeat-room', 'council-chamber');

        UPDATE mud_exits
        SET label = CASE
            WHEN from_room_id IN (SELECT id FROM mud_rooms WHERE slug = 'atrium') AND direction = 'east' THEN 'To the watch tower'
            WHEN from_room_id IN (SELECT id FROM mud_rooms WHERE slug = 'observatory') AND direction = 'west' THEN 'Back to the gatehouse'
            WHEN from_room_id IN (SELECT id FROM mud_rooms WHERE slug = 'atrium') AND direction = 'north' THEN 'To the north hall'
            WHEN from_room_id IN (SELECT id FROM mud_rooms WHERE slug = 'archive-hall') AND direction = 'south' THEN 'Back to the gatehouse'
            WHEN from_room_id IN (SELECT id FROM mud_rooms WHERE slug = 'observatory') AND direction = 'east' THEN 'To the bell chamber'
            WHEN from_room_id IN (SELECT id FROM mud_rooms WHERE slug = 'heartbeat-room') AND direction = 'west' THEN 'Back to the watch tower'
            WHEN from_room_id IN (SELECT id FROM mud_rooms WHERE slug = 'archive-hall') AND direction = 'east' THEN 'To the throne room'
            WHEN from_room_id IN (SELECT id FROM mud_rooms WHERE slug = 'council-chamber') AND direction = 'west' THEN 'Back to the north hall'
            ELSE label
        END;
        """
        38, """
        GRANT ALL ON TABLE mud_zones TO djehuti;
        GRANT ALL ON TABLE mud_rooms TO djehuti;
        GRANT ALL ON TABLE mud_exits TO djehuti;
        GRANT ALL ON TABLE mud_characters TO djehuti;
        GRANT ALL ON TABLE mud_events TO djehuti;
        """
        39, """
        CREATE TABLE IF NOT EXISTS mud_items (
            id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            room_id             UUID REFERENCES mud_rooms(id) ON DELETE SET NULL,
            owner_character_id  UUID REFERENCES mud_characters(id) ON DELETE SET NULL,
            name                TEXT NOT NULL,
            slug                TEXT NOT NULL,
            description         TEXT,
            readable_text       TEXT,
            portable            BOOLEAN NOT NULL DEFAULT false,
            position            INT NOT NULL DEFAULT 0,
            created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_mud_items_room_id ON mud_items(room_id);
        CREATE INDEX IF NOT EXISTS idx_mud_items_owner_character_id ON mud_items(owner_character_id);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id,
               'Brass Ledger',
               'brass-ledger',
               'A heavy brass-bound ledger rests on a narrow stand. The clasp hangs open as if it is meant to be consulted often.',
               'Dispatch ledger: Gatehouse watches changed with the bell. Visitors, rumors, and notable disturbances are to be recorded before nightfall.',
               false,
               0
        FROM mud_rooms r
        WHERE r.slug = 'atrium'
          AND NOT EXISTS (
              SELECT 1 FROM mud_items i
              WHERE i.slug = 'brass-ledger'
                AND i.room_id = r.id
                AND i.owner_character_id IS NULL
          );

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id,
               'Survey Map',
               'survey-map',
               'A rolled map of the keep and its near approaches, marked with routes, notes, and faded watch symbols.',
               'Survey map: Outer Keep to Inner Keep. Gatehouse, Watch Tower, North Hall, Bell Chamber, Throne Room. Marginal note: old service routes remain unsealed.',
               true,
               1
        FROM mud_rooms r
        WHERE r.slug = 'observatory'
          AND NOT EXISTS (
              SELECT 1 FROM mud_items i
              WHERE i.slug = 'survey-map'
                AND i.room_id = r.id
                AND i.owner_character_id IS NULL
          );

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id,
               'Bronze Bell',
               'bronze-bell',
               'A bronze bell hangs from a timber frame. It is too large to carry and seems tied to the hourly rhythm of the keep.',
               NULL,
               false,
               0
        FROM mud_rooms r
        WHERE r.slug = 'heartbeat-room'
          AND NOT EXISTS (
              SELECT 1 FROM mud_items i
              WHERE i.slug = 'bronze-bell'
                AND i.room_id = r.id
                AND i.owner_character_id IS NULL
          );

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id,
               'Sealed Notice',
               'sealed-notice',
               'A sealed notice has been pinned beside the old dais. The wax has cracked with age, but the sheet is still readable.',
               'Council notice: access to the lower vaults remains restricted. Record unusual machinery, wandering personas, and unsanctioned experiments.',
               false,
               0
        FROM mud_rooms r
        WHERE r.slug = 'council-chamber'
          AND NOT EXISTS (
              SELECT 1 FROM mud_items i
              WHERE i.slug = 'sealed-notice'
                AND i.room_id = r.id
                AND i.owner_character_id IS NULL
          );

        GRANT ALL ON TABLE mud_items TO djehuti;
        """
        40, """
        GRANT ALL ON TABLE mud_tier_labels TO djehuti;
        """
        41, """
        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id,
               'Brass Shard',
               'brass-shard',
               'A broken brass fragment from an older fitting. The edges have been filed smooth by handling.',
               NULL,
               true,
               2
        FROM mud_rooms r
        WHERE r.slug = 'atrium'
          AND NOT EXISTS (
              SELECT 1 FROM mud_items i
              WHERE i.slug = 'brass-shard'
                AND i.room_id = r.id
                AND i.owner_character_id IS NULL
          );

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id,
               'Wire Spool',
               'wire-spool',
               'A short spool of thin signaling wire, still dry and tightly wound.',
               NULL,
               true,
               2
        FROM mud_rooms r
        WHERE r.slug = 'observatory'
          AND NOT EXISTS (
              SELECT 1 FROM mud_items i
              WHERE i.slug = 'wire-spool'
                AND i.room_id = r.id
                AND i.owner_character_id IS NULL
          );

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id,
               'Rag Strip',
               'rag-strip',
               'A strip of old cloth torn from a supply wrap. It would burn quickly if soaked in oil.',
               NULL,
               true,
               2
        FROM mud_rooms r
        WHERE r.slug = 'archive-hall'
          AND NOT EXISTS (
              SELECT 1 FROM mud_items i
              WHERE i.slug = 'rag-strip'
                AND i.room_id = r.id
                AND i.owner_character_id IS NULL
          );

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id,
               'Lamp Oil',
               'lamp-oil',
               'A stoppered flask of lamp oil. Enough for a small torch or signal burn.',
               NULL,
               true,
               1
        FROM mud_rooms r
        WHERE r.slug = 'heartbeat-room'
          AND NOT EXISTS (
              SELECT 1 FROM mud_items i
              WHERE i.slug = 'lamp-oil'
                AND i.room_id = r.id
                AND i.owner_character_id IS NULL
          );

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id,
               'Wax Seal',
               'wax-seal',
               'A spare disk of sealing wax stamped with an old council mark.',
               NULL,
               true,
               1
        FROM mud_rooms r
        WHERE r.slug = 'council-chamber'
          AND NOT EXISTS (
              SELECT 1 FROM mud_items i
              WHERE i.slug = 'wax-seal'
                AND i.room_id = r.id
                AND i.owner_character_id IS NULL
          );
        """
        42, """
        ALTER TABLE mud_rooms ADD COLUMN IF NOT EXISTS map_x INT;
        ALTER TABLE mud_rooms ADD COLUMN IF NOT EXISTS map_y INT;

        ALTER TABLE mud_exits ADD COLUMN IF NOT EXISTS exit_type TEXT NOT NULL DEFAULT 'passage';

        INSERT INTO mud_zones (name, slug, description, position)
        VALUES ('Realm Threshold', 'realm-threshold', 'A threshold chamber where travelers choose which realm to enter.', -1)
        ON CONFLICT (slug) DO NOTHING;

        INSERT INTO mud_zones (name, slug, description, position)
        VALUES ('Star Reach', 'star-reach', 'Cold corridors, transit decks, and machine-lit passages of the science-fiction realm.', 2)
        ON CONFLICT (slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Threshold of Realms', 'threshold-of-realms',
               'Two portals stand in the chamber: one of carved stone and torchlight, one of cold metal and blue static.',
               0, 0, 0
        FROM mud_zones z
        WHERE z.slug = 'realm-threshold'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Keep Gate', 'keep-gate',
               'A stone arch opens into a world of banners, towers, and old iron gates. The threshold portal glows behind you.',
               0, 0, 0
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Transit Dock', 'transit-dock',
               'A docking platform hums with power. Conduits pulse in the floor and a return portal flickers at the far bulkhead.',
               0, 0, 0
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        UPDATE mud_rooms
        SET map_x = CASE slug
            WHEN 'threshold-of-realms' THEN 0
            WHEN 'keep-gate' THEN -2
            WHEN 'atrium' THEN 0
            WHEN 'observatory' THEN 2
            WHEN 'archive-hall' THEN 0
            WHEN 'heartbeat-room' THEN 4
            WHEN 'council-chamber' THEN 2
            WHEN 'transit-dock' THEN 2
            ELSE map_x
        END,
            map_y = CASE slug
            WHEN 'threshold-of-realms' THEN 0
            WHEN 'keep-gate' THEN 0
            WHEN 'atrium' THEN 0
            WHEN 'observatory' THEN 0
            WHEN 'archive-hall' THEN 2
            WHEN 'heartbeat-room' THEN 0
            WHEN 'council-chamber' THEN 2
            WHEN 'transit-dock' THEN 0
            ELSE map_y
        END
        WHERE slug IN ('threshold-of-realms', 'keep-gate', 'atrium', 'observatory', 'archive-hall', 'heartbeat-room', 'council-chamber', 'transit-dock');

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'medieval', 'Stone portal to the keep', 'portal'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'keep-gate'
        WHERE r1.slug = 'threshold-of-realms'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'sci-fi', 'Blue portal to Star Reach', 'portal'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'transit-dock'
        WHERE r1.slug = 'threshold-of-realms'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'portal', 'Return to the threshold', 'portal'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'threshold-of-realms'
        WHERE r1.slug = 'keep-gate'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'portal', 'Return to the threshold', 'portal'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'threshold-of-realms'
        WHERE r1.slug = 'transit-dock'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Through the gatehouse', 'gate'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'atrium'
        WHERE r1.slug = 'keep-gate'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the outer gate', 'gate'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'keep-gate'
        WHERE r1.slug = 'atrium'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        UPDATE mud_exits
        SET exit_type = CASE
            WHEN direction IN ('medieval', 'sci-fi', 'portal') THEN 'portal'
            WHEN direction IN ('up') THEN 'stairs-up'
            WHEN direction IN ('down') THEN 'stairs-down'
            WHEN from_room_id IN (SELECT id FROM mud_rooms WHERE slug IN ('keep-gate', 'atrium')) AND direction IN ('east', 'west') THEN 'gate'
            ELSE exit_type
        END;
        """
        43, """
        ALTER TABLE users ADD COLUMN IF NOT EXISTS active_mud_character_id UUID;
        ALTER TABLE users ADD COLUMN IF NOT EXISTS mud_bonus_character_slots INT NOT NULL DEFAULT 0;

        ALTER TABLE mud_characters DROP CONSTRAINT IF EXISTS mud_characters_user_id_key;

        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS realm_slug TEXT;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS name TEXT;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;

        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_presence INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_wit INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_resolve INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_lore INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_craft INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS stat_guile INT NOT NULL DEFAULT 1;

        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_searching INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_crafting INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_navigation INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_lorekeeping INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_negotiation INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_devices INT NOT NULL DEFAULT 1;
        ALTER TABLE mud_characters ADD COLUMN IF NOT EXISTS skill_survival INT NOT NULL DEFAULT 1;

        UPDATE mud_characters
        SET name = COALESCE(NULLIF(name, ''), display_name)
        WHERE name IS NULL OR btrim(name) = '';

        UPDATE mud_characters
        SET realm_slug = CASE
            WHEN current_room_id IN (
                SELECT r.id
                FROM mud_rooms r
                JOIN mud_zones z ON z.id = r.zone_id
                WHERE z.slug = 'star-reach'
            ) THEN 'sci-fi'
            ELSE 'medieval'
        END
        WHERE realm_slug IS NULL OR btrim(realm_slug) = '';

        ALTER TABLE mud_characters
            ALTER COLUMN name SET NOT NULL;

        ALTER TABLE mud_characters
            ALTER COLUMN realm_slug SET NOT NULL;

        CREATE INDEX IF NOT EXISTS idx_mud_characters_user_id ON mud_characters(user_id);
        CREATE INDEX IF NOT EXISTS idx_mud_characters_realm_slug ON mud_characters(realm_slug);
        CREATE INDEX IF NOT EXISTS idx_mud_characters_deleted_at ON mud_characters(deleted_at);

        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM information_schema.table_constraints
                WHERE constraint_name = 'fk_users_active_mud_character_id'
                  AND table_name = 'users'
            ) THEN
                ALTER TABLE users
                    ADD CONSTRAINT fk_users_active_mud_character_id
                    FOREIGN KEY (active_mud_character_id)
                    REFERENCES mud_characters(id)
                    ON DELETE SET NULL;
            END IF;
        END $$;

        GRANT ALL ON TABLE mud_characters TO djehuti;
        """
        44, """
        CREATE TABLE IF NOT EXISTS mud_companion_profiles (
            character_id UUID PRIMARY KEY REFERENCES mud_characters(id) ON DELETE CASCADE,
            enabled BOOLEAN NOT NULL DEFAULT FALSE,
            mode TEXT NOT NULL DEFAULT 'solitary',
            model TEXT NOT NULL DEFAULT 'gpt-4.1-mini',
            disclosure TEXT NOT NULL DEFAULT 'tagged',
            allow_online_concurrency BOOLEAN NOT NULL DEFAULT FALSE,
            use_byo_openai_key BOOLEAN NOT NULL DEFAULT FALSE,
            byo_openai_key_protected TEXT,
            key_last_set_at TIMESTAMPTZ,
            last_status TEXT,
            last_error TEXT,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_mud_companion_profiles_enabled
            ON mud_companion_profiles(enabled);

        GRANT ALL ON TABLE mud_companion_profiles TO djehuti;
        """
        45, """
        INSERT INTO mud_zones (name, slug, description, position)
        VALUES ('Lower Vaults', 'lower-vaults', 'Cracked stairs and buried chambers beneath the keep, where sealed relics and old workrooms remain.', 3)
        ON CONFLICT (slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Old Stair', 'old-stair',
               'A steep stone stair curls below the throne room. Damp air rises from the dark and the mortar smells of old ash.',
               0, 0, 0
        FROM mud_zones z
        WHERE z.slug = 'lower-vaults'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Reliquary', 'reliquary',
               'Iron cages, saint-marked boxes, and cracked plinths fill the chamber. Someone once sorted dangerous things here with religious care.',
               1, 2, 0
        FROM mud_zones z
        WHERE z.slug = 'lower-vaults'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Flooded Archive', 'flooded-archive',
               'Shallow black water covers the floor between fallen shelves. Rot, paper pulp, and silverfish cling to the drowned edges of the room.',
               2, -2, 0
        FROM mud_zones z
        WHERE z.slug = 'lower-vaults'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Forge Cellar', 'forge-cellar',
               'A cellar forge sits cold beneath a smoke-black vent. Bent tools and resin jars remain on a workbench lit by reflected coals.',
               3, 0, 2
        FROM mud_zones z
        WHERE z.slug = 'lower-vaults'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Warden Vault', 'warden-vault',
               'A narrow vault of reinforced stone and iron lockers. The room feels less sacred than practical, built to keep specific objects out of reach.',
               4, 4, 0
        FROM mud_zones z
        WHERE z.slug = 'lower-vaults'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Service Concourse', 'service-concourse',
               'A machine-lit corridor runs beneath pulsing conduits. Signs point to cryo storage, maintenance, and signal control.',
               1, 0, 2
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Sensor Loft', 'sensor-loft',
               'A raised loft of instrument frames and glass panels. Faint data blooms slide across dead screens like weather ghosts.',
               2, 0, 4
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Cryo Gallery', 'cryo-gallery',
               'Rows of sealed pods stand in a frost-lit hall. The gallery is quiet except for the click and hiss of old pressure valves.',
               3, 2, 2
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Hydro Bay', 'hydro-bay',
               'Reservoir pipes and algae tanks crowd the bay. Coolant drips somewhere out of sight in a patient metallic rhythm.',
               4, -2, 2
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Reactor Causeway', 'reactor-causeway',
               'A suspended causeway crosses a roaring shaft of heat and light. Warning sigils blink along the handrails in synchronized bursts.',
               5, 4, 2
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Signal Apex', 'signal-apex',
               'At the highest control point of Star Reach, relay vanes angle toward the void. Every surface hums with distant transmission.',
               6, 4, 4
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'down', 'Stone steps into the lower vaults', 'stairs-down'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'old-stair'
        WHERE r1.slug = 'council-chamber'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'up', 'Back to the throne room', 'stairs-up'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'council-chamber'
        WHERE r1.slug = 'old-stair'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the reliquary', 'door'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'reliquary'
        WHERE r1.slug = 'old-stair'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Toward the flooded archive', 'passage'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'flooded-archive'
        WHERE r1.slug = 'old-stair'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Toward the forge cellar', 'passage'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'forge-cellar'
        WHERE r1.slug = 'old-stair'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the stair', 'door'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'old-stair'
        WHERE r1.slug = 'reliquary'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'down', 'Iron steps into the warden vault', 'stairs-down'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'warden-vault'
        WHERE r1.slug = 'reliquary'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to the stair', 'passage'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'old-stair'
        WHERE r1.slug = 'flooded-archive'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to the stair', 'passage'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'old-stair'
        WHERE r1.slug = 'forge-cellar'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'up', 'Back to the reliquary', 'stairs-up'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'reliquary'
        WHERE r1.slug = 'warden-vault'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Into the service concourse', 'passage'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'service-concourse'
        WHERE r1.slug = 'transit-dock'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Back to the dock', 'passage'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'transit-dock'
        WHERE r1.slug = 'service-concourse'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'up', 'Lift to the sensor loft', 'elevator'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'sensor-loft'
        WHERE r1.slug = 'service-concourse'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'down', 'Lift back to the concourse', 'elevator'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'service-concourse'
        WHERE r1.slug = 'sensor-loft'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward cryo storage', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'cryo-gallery'
        WHERE r1.slug = 'service-concourse'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Toward the hydro bay', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'hydro-bay'
        WHERE r1.slug = 'service-concourse'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the concourse', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'service-concourse'
        WHERE r1.slug = 'cryo-gallery'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to the concourse', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'service-concourse'
        WHERE r1.slug = 'hydro-bay'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Toward the reactor causeway', 'sealed-door'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'reactor-causeway'
        WHERE r1.slug = 'cryo-gallery'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Back to cryo storage', 'sealed-door'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'cryo-gallery'
        WHERE r1.slug = 'reactor-causeway'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'up', 'Climb to the signal apex', 'stairs-up'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'signal-apex'
        WHERE r1.slug = 'reactor-causeway'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'down', 'Down to the causeway', 'stairs-down'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'reactor-causeway'
        WHERE r1.slug = 'signal-apex'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Gate Warden', 'gate-warden',
               'An old gate warden in a patched surcoat watches the portal traffic with suspicious eyes and perfect stillness.',
               'The warden taps two fingers against a brass ledger. ""Torch first. Vaults second. If something whispers your true name below, do not whisper back.""',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'keep-gate'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'gate-warden' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Archivist Shade', 'archivist-shade',
               'A dim figure drifts between drowned shelves, turning phantom pages with great care.',
               'The shade murmurs, ""Ink remembers what stone forgets. If you must carry one thing upward, carry the mark that leads you back.""',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'flooded-archive'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'archivist-shade' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Forge Servitor', 'forge-servitor',
               'A soot-dark mechanical servitor sits folded beside the cold forge, its hands still shaped for careful repair work.',
               'A cracked speaker clicks alive: ""Wrap before heat. Seal before strain. Cheap work breaks twice.""',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'forge-cellar'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'forge-servitor' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Vault Register', 'vault-register',
               'A chained register lists what was moved into and out of the lower vaults.',
               'Vault register: chalk, wax, spare nails, lamp stores, confiscated signal brass, sealed reliquary keys, and one item redacted by order of the warden.',
               false, 1
        FROM mud_rooms r
        WHERE r.slug = 'warden-vault'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'vault-register' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Dock Clerk Drone', 'dock-clerk-drone',
               'A hovering service drone clicks through ancient docking routines as if a ship might arrive any moment.',
               'The drone projects a faded line of text: ""Transit lanes unstable. Service personnel authorized to improvise bridge tools from scrap.""',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'transit-dock'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'dock-clerk-drone' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Maintenance Hologram', 'maintenance-hologram',
               'A looping technician hologram points endlessly toward marked service routes.',
               'The hologram flickers: ""Coolant west. Cryo east. Sensor lift above. Do not cross the causeway without a live beacon.""',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'service-concourse'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'maintenance-hologram' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Cryo Steward', 'cryo-steward',
               'A pod-side steward frame hangs at the edge of the gallery, its sensors still tracking temperature and seal integrity.',
               'The steward whispers through static: ""Pressure holds. Route the charge before you route the heat. Nothing fragile survives both at once.""',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'cryo-gallery'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cryo-steward' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Array Whisper', 'array-whisper',
               'A relay ghost rides the humming vanes at the apex, present as a voice before it is visible as a shape.',
               'A whisper skates across the metal: ""Signal wants structure. Noise wants panic. Build the bridge, mark the route, then listen.""',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'signal-apex'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'array-whisper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Rune Chalk', 'rune-chalk',
               'A dry cylinder of marked chalk used to note inspected passages and warded crates.',
               NULL,
               true, 2
        FROM mud_rooms r
        WHERE r.slug = 'flooded-archive'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rune-chalk' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Resin Pitch', 'resin-pitch',
               'A tin of dark resin pitch, still tacky enough to bind cloth, seal cracks, or weather rough handling.',
               NULL,
               true, 2
        FROM mud_rooms r
        WHERE r.slug = 'forge-cellar'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'resin-pitch' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Iron Nails', 'iron-nails',
               'A fistful of square-forged nails collected in a cloth pouch.',
               NULL,
               true, 2
        FROM mud_rooms r
        WHERE r.slug = 'warden-vault'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'iron-nails' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Fiber Bundle', 'fiber-bundle',
               'A coil of synthetic weave stripped from maintenance packing and still strong enough to lash gear together.',
               NULL,
               true, 2
        FROM mud_rooms r
        WHERE r.slug = 'transit-dock'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fiber-bundle' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Capacitor Cell', 'capacitor-cell',
               'A thumb-length charge cell with enough reserve to jump a simple device or stabilize a rough-built circuit.',
               NULL,
               true, 2
        FROM mud_rooms r
        WHERE r.slug = 'cryo-gallery'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'capacitor-cell' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Coolant Canister', 'coolant-canister',
               'A compact coolant canister chilled enough to fog in your hand when turned.',
               NULL,
               true, 2
        FROM mud_rooms r
        WHERE r.slug = 'hydro-bay'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'coolant-canister' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Crystal Vial', 'crystal-vial',
               'A clear relay-grade vial that catches and bends blue light through microcut facets.',
               NULL,
               true, 2
        FROM mud_rooms r
        WHERE r.slug = 'sensor-loft'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'crystal-vial' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Relay Schematic', 'relay-schematic',
               'A thin schematic sheet showing how signal paths were patched around dead relays during emergencies.',
               'Emergency relay note: when dedicated hardware fails, pair a live cell with spare wire and bridge the path by hand. Mark the route before you energize it.',
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'sensor-loft'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'relay-schematic' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Dock Manifest', 'dock-manifest',
               'A slate manifest of cargo once routed through Star Reach.',
               'Dock manifest: coolant, fibers, relay glass, reserve cells, and sealed cryo notices. Priority route remains the apex relay stack.',
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'transit-dock'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'dock-manifest' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        GRANT ALL ON TABLE mud_zones TO djehuti;
        GRANT ALL ON TABLE mud_rooms TO djehuti;
        GRANT ALL ON TABLE mud_exits TO djehuti;
        GRANT ALL ON TABLE mud_items TO djehuti;
        """
        46, """
        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Chapel Landing', 'chapel-landing',
               'A broad landing of votive alcoves, travel hooks, and worn stone benches where people once paused before heading deeper into the keep.',
               5, 0, -2
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Scriptorium', 'scriptorium',
               'Desks, shelves, and scarred writing stands fill the room. Wax dust and charcoal still cling to the grain of the tables.',
               6, 2, -2
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Market Crossing', 'market-crossing',
               'A covered crossing of carts, stacked crates, and old stall frames. Even empty, the place feels built for exchange and rumor.',
               7, 0, -4
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Pilgrims Yard', 'pilgrims-yard',
               'A quiet stone yard marked by boot-scraped flags and old travel emblems pressed into the walls by generations of visitors.',
               8, 2, -4
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Mess Deck', 'mess-deck',
               'Fold-down tables, ration slots, and dented warming units line a long compartment where station workers once traded complaints and gossip.',
               7, 2, -2
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Salvage Bay', 'salvage-bay',
               'Stripped machine housings and tagged scrap pallets sit under a crane track that still groans when the station shifts.',
               8, -4, 2
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Relay Works', 'relay-works',
               'Open signal frames and maintenance gantries crowd the chamber. It smells like ozone, dust, and careful improvisation.',
               9, 2, 4
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Observation Rim', 'observation-rim',
               'A narrow rim walkway curves beneath a vaulted viewplate. The stars beyond look close enough to sort by hand.',
               10, 4, 6
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Steps toward the chapel landing', 'stairs-down'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'chapel-landing'
        WHERE r1.slug = 'atrium'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to the gatehouse', 'stairs-up'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'atrium'
        WHERE r1.slug = 'chapel-landing'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Through the writing arch', 'door'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'scriptorium'
        WHERE r1.slug = 'chapel-landing'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the landing', 'door'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'chapel-landing'
        WHERE r1.slug = 'scriptorium'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Toward the market crossing', 'passage'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'market-crossing'
        WHERE r1.slug = 'chapel-landing'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to the chapel landing', 'passage'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'chapel-landing'
        WHERE r1.slug = 'market-crossing'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Into the pilgrims yard', 'gate'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'pilgrims-yard'
        WHERE r1.slug = 'market-crossing'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the crossing', 'gate'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'market-crossing'
        WHERE r1.slug = 'pilgrims-yard'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Down to the mess deck', 'stairs-down'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'mess-deck'
        WHERE r1.slug = 'transit-dock'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to the dock', 'stairs-up'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'transit-dock'
        WHERE r1.slug = 'mess-deck'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Through the salvage hatch', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'salvage-bay'
        WHERE r1.slug = 'hydro-bay'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to hydro control', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'hydro-bay'
        WHERE r1.slug = 'salvage-bay'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the relay works', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'relay-works'
        WHERE r1.slug = 'sensor-loft'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the loft', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'sensor-loft'
        WHERE r1.slug = 'relay-works'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Along the rim access', 'catwalk'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'observation-rim'
        WHERE r1.slug = 'signal-apex'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Back to the signal apex', 'catwalk'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'signal-apex'
        WHERE r1.slug = 'observation-rim'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Chapel Caretaker', 'chapel-caretaker',
               'A caretaker wrapped in weathered blue cloth tends dead votive cups and travel benches with ceremonial patience.',
               'The caretaker says, "Travelers used to leave with three things: a mark, a map, and a reason to come back."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'chapel-landing'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'chapel-caretaker' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Ink-Stained Novice', 'ink-stained-novice',
               'A young copyist sits with charcoal on both hands, preserving forms and fragments no one else thought worth saving.',
               'Without looking up, the novice mutters, "Records are how a place keeps breathing after the people leave."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'scriptorium'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ink-stained-novice' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Mule Handler', 'mule-handler',
               'A broad-shouldered handler keeps counting phantom deliveries, still making room for one more cart in his head.',
               'The handler gives you a measuring glance. "Cord, clasps, food, fire. That is what actually moves a keep, not speeches."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'market-crossing'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'mule-handler' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Pilgrim Standard', 'pilgrim-standard',
               'A patched travel banner hangs from a low pole, stitched with names, vows, and route marks from many journeys.',
               'The standard reads: WALK FAR, CARRY LIGHT, RETURN WITH NEWS.',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'pilgrims-yard'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pilgrim-standard' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Galley Kiosk', 'galley-kiosk',
               'A ration kiosk still cycles menus to a crew that never quite arrives.',
               'Menu loop: broth, grain cakes, protein wraps, hot tea, coolant-safe water. Report shortages to deck control.',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'mess-deck'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'galley-kiosk' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Salvage Rig', 'salvage-rig',
               'A hulking work rig with magnetic claws waits over pallets of stripped parts and tagged machine skin.',
               'A maintenance tag dangles from the rig: TAKE ONLY WHAT YOU CAN MOUNT, PATCH, OR CARRY HOME.',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'salvage-bay'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'salvage-rig' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Relay Foreman', 'relay-foreman',
               'A foreman projection paces between open signal frames, still auditing work that can no longer be officially assigned.',
               'The projection stops long enough to say, "Document the patch. If it holds, it becomes procedure."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'relay-works'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'relay-foreman' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Rim Echo', 'rim-echo',
               'A voice without a visible source drifts around the rim, half memory and half instrumentation bleed.',
               'The echo whispers, "Out here, navigation is not direction. It is commitment."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'observation-rim'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rim-echo' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Charcoal Stick', 'charcoal-stick',
               'A wrapped writing charcoal, flat on one side from patient use across rough paper and wood.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'scriptorium'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'charcoal-stick' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Linen Cord', 'linen-cord',
               'A neat bundle of waxed linen cord used for tying packets, sealing rolls, or binding field gear.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'market-crossing'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'linen-cord' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Copper Clasp', 'copper-clasp',
               'A small hammered clasp polished bright by repeated fastening and reuse.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'pilgrims-yard'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'copper-clasp' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Pilgrim Token', 'pilgrim-token',
               'A stamped travel token worn thin at the edges, passed from one journey to the next.',
               'One side reads ROAD. The other reads RETURN.',
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'chapel-landing'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pilgrim-token' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Glow Filament', 'glow-filament',
               'A flexible luminous filament salvaged from old deck lighting and still bright enough to guide close work.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'mess-deck'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'glow-filament' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Alloy Plate', 'alloy-plate',
               'A hand-sized plate of station alloy cut clean from a damaged panel and stacked for reuse.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'salvage-bay'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'alloy-plate' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Data Shard', 'data-shard',
               'A fractured storage shard carrying partial route and maintenance traces.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'relay-works'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'data-shard' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Sealant Foam', 'sealant-foam',
               'A pressure tube of expanding sealant foam used to close leaks and brace stressed seams.',
               NULL,
               true, 3
        FROM mud_rooms r
        WHERE r.slug = 'hydro-bay'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'sealant-foam' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Void Log', 'void-log',
               'A slim observation log clipped beneath the rim glass, full of route notes and private starwatch comments.',
               'Observation log: three steady lanes remain. One for arrival, one for signal, one for going home when you are finished becoming someone else.',
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'observation-rim'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'void-log' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        GRANT ALL ON TABLE mud_rooms TO djehuti;
        GRANT ALL ON TABLE mud_exits TO djehuti;
        GRANT ALL ON TABLE mud_items TO djehuti;
        """

        47, """
        UPDATE ai_personas
        SET model = 'gpt-4o-mini'
        WHERE btrim(lower(model)) = 'claude-sonnet-4-6';

        UPDATE heartbeat_jobs
        SET payload = jsonb_set(payload, '{Model}', to_jsonb('gpt-4o-mini'::text), false)
        WHERE action_type IN ('CreateThread', 'GenerateReply')
          AND btrim(lower(coalesce(payload->>'Model', ''))) = 'claude-sonnet-4-6';

        UPDATE heartbeat_jobs
        SET status = 'Pending',
            retry_count = 0,
            error = NULL,
            locked_at = NULL,
            completed_at = NULL
        WHERE action_type IN ('CreateThread', 'GenerateReply')
          AND status = 'Failed'
          AND error ILIKE '%model_not_found%';
        """

        48, """
        ALTER TABLE mud_zones
            ADD COLUMN IF NOT EXISTS realm_slug TEXT;

        ALTER TABLE mud_zones
            ALTER COLUMN realm_slug SET DEFAULT 'medieval';

        UPDATE mud_zones
        SET realm_slug = CASE slug
            WHEN 'star-reach' THEN 'sci-fi'
            WHEN 'realm-threshold' THEN 'neutral'
            ELSE 'medieval'
        END
        WHERE realm_slug IS NULL OR btrim(realm_slug) = '';

        ALTER TABLE mud_zones
            ALTER COLUMN realm_slug SET NOT NULL;

        CREATE INDEX IF NOT EXISTS idx_mud_zones_realm_slug ON mud_zones(realm_slug);

        CREATE TABLE IF NOT EXISTS mud_director_directives (
            id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            realm_slug              TEXT NOT NULL,
            director_slug           TEXT NOT NULL,
            raw_command             TEXT NOT NULL,
            normalized_instruction  TEXT NOT NULL,
            requested_by_user_id    UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            requested_by_character_id UUID NOT NULL REFERENCES mud_characters(id) ON DELETE CASCADE,
            active                  BOOLEAN NOT NULL DEFAULT TRUE,
            superseded_at           TIMESTAMPTZ,
            created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_mud_director_directives_realm_active
            ON mud_director_directives(realm_slug, active, created_at DESC);

        CREATE TABLE IF NOT EXISTS mud_builder_agents (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            slug            TEXT NOT NULL UNIQUE,
            realm_slug      TEXT NOT NULL,
            director_slug   TEXT NOT NULL,
            display_name    TEXT NOT NULL,
            specialty       TEXT NOT NULL,
            model           TEXT NOT NULL DEFAULT 'gpt-4o-mini',
            build_hour_utc  INT NOT NULL,
            active          BOOLEAN NOT NULL DEFAULT TRUE,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_mud_builder_agents_realm_active
            ON mud_builder_agents(realm_slug, active, build_hour_utc);

        CREATE TABLE IF NOT EXISTS mud_build_jobs (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            builder_agent_id UUID NOT NULL REFERENCES mud_builder_agents(id) ON DELETE CASCADE,
            realm_slug      TEXT NOT NULL,
            directive_id    UUID REFERENCES mud_director_directives(id) ON DELETE SET NULL,
            build_date      DATE NOT NULL,
            scheduled_for   TIMESTAMPTZ NOT NULL,
            status          TEXT NOT NULL DEFAULT 'Pending',
            retry_count     INT NOT NULL DEFAULT 0,
            anchor_room_id  UUID REFERENCES mud_rooms(id) ON DELETE SET NULL,
            created_room_id UUID REFERENCES mud_rooms(id) ON DELETE SET NULL,
            payload         JSONB,
            result_summary  TEXT,
            error           TEXT,
            started_at      TIMESTAMPTZ,
            completed_at    TIMESTAMPTZ,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            CONSTRAINT mud_build_jobs_status_check CHECK (status IN ('Pending', 'Processing', 'Completed', 'Failed')),
            CONSTRAINT mud_build_jobs_one_per_builder_per_day UNIQUE (builder_agent_id, build_date)
        );

        CREATE INDEX IF NOT EXISTS idx_mud_build_jobs_status_schedule
            ON mud_build_jobs(status, scheduled_for, created_at);

        GRANT ALL ON TABLE mud_zones TO djehuti;
        GRANT ALL ON TABLE mud_director_directives TO djehuti;
        GRANT ALL ON TABLE mud_builder_agents TO djehuti;
        GRANT ALL ON TABLE mud_build_jobs TO djehuti;
        """

        49, """
        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Orchard Walk', 'orchard-walk',
               'Low stone walls guide a tidy lane of espaliered fruit trees and weather-smoothed baskets waiting for the next honest load.',
               9, 4, -4
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Falcon Roost', 'falcon-roost',
               'A wind-bright platform of posts, bells, and leather perches where messenger birds once learned the shape of the whole valley.',
               10, 4, -6
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Barrow Approach', 'barrow-approach',
               'The road narrows between old marker stones and rooted lantern hooks. The air feels cooler here, as if memory itself cast shade.',
               11, 0, -6
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Weather Stone', 'weather-stone',
               'A broad standing stone rises from the heath, wrapped in prayer ribbons and scratched forecasts. Moss grips its base like a patient audience.',
               12, -2, -6
        FROM mud_zones z
        WHERE z.slug = 'central-hub'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Solar Gallery', 'solar-gallery',
               'Sheets of reflective foil turn slow overhead, making the gallery pulse with a patient gold light that follows the station''s roll.',
               11, 6, 6
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Packet Garden', 'packet-garden',
               'Signal pods bloom from trellised conduit here, each one storing an old message or a future one waiting to happen.',
               12, 6, 4
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Clamp Yard', 'clamp-yard',
               'Rows of magnetic braces and freight hooks line a stripped work court where salvage crews practiced impossible lifts until they got them right.',
               13, -6, 2
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Quiet Lock', 'quiet-lock',
               'A pressure lock with dim panels and thick hush seals. Even the alarms here seem trained not to raise their voices.',
               14, -6, 4
        FROM mud_zones z
        WHERE z.slug = 'star-reach'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the orchard walk', 'gate'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'orchard-walk'
        WHERE r1.slug = 'pilgrims-yard'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the pilgrims yard', 'gate'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'pilgrims-yard'
        WHERE r1.slug = 'orchard-walk'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Up to the falcon roost', 'stairs-up'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'falcon-roost'
        WHERE r1.slug = 'orchard-walk'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Down to the orchard walk', 'stairs-down'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'orchard-walk'
        WHERE r1.slug = 'falcon-roost'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Toward the barrow road', 'road'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'barrow-approach'
        WHERE r1.slug = 'market-crossing'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to the market crossing', 'road'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'market-crossing'
        WHERE r1.slug = 'barrow-approach'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Across the heath to the weather stone', 'trail'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'weather-stone'
        WHERE r1.slug = 'barrow-approach'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to the barrow approach', 'trail'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'barrow-approach'
        WHERE r1.slug = 'weather-stone'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Out along the solar gallery', 'catwalk'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'solar-gallery'
        WHERE r1.slug = 'observation-rim'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the observation rim', 'catwalk'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'observation-rim'
        WHERE r1.slug = 'solar-gallery'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Down to the packet garden', 'ladder'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'packet-garden'
        WHERE r1.slug = 'solar-gallery'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to the solar gallery', 'ladder'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'solar-gallery'
        WHERE r1.slug = 'packet-garden'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Into the clamp yard', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'clamp-yard'
        WHERE r1.slug = 'salvage-bay'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to the salvage bay', 'bulkhead'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'salvage-bay'
        WHERE r1.slug = 'clamp-yard'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Toward the quiet lock', 'pressure-door'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'quiet-lock'
        WHERE r1.slug = 'clamp-yard'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to the clamp yard', 'pressure-door'
        FROM mud_rooms r1
        JOIN mud_rooms r2 ON r2.slug = 'clamp-yard'
        WHERE r1.slug = 'quiet-lock'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Orchard Keeper', 'orchard-keeper',
               'A patient keeper checks wicker ladders, bruised fruit, and the honest work of branches that still trust the wall.',
               'The keeper smiles without hurry. "Good fruit, good water, good roads. Fix those three and most kingdoms stop trying to die."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'orchard-walk'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'orchard-keeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Orchard Apple', 'orchard-apple',
               'A hard-skinned red apple packed for travel instead of ceremony.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'orchard-walk'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'orchard-apple' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Roost Falconer', 'roost-falconer',
               'Leather-gloved and wind-burned, the falconer still watches the valley as if the next message matters more than sleep.',
               'The falconer taps a perch and says, "A bird returns for three reasons: training, hunger, or love. It is best if your messages deserve all three."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'falcon-roost'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'roost-falconer' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Falcon Feather', 'falcon-feather',
               'A clean dropped primary from a courier hawk, glossy and strong enough for another errand.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'falcon-roost'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'falcon-feather' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Barrow Porter', 'barrow-porter',
               'A porter with a shuttered lantern waits where the road turns solemn, ready to escort courage farther than sense would usually take it.',
               'The porter lowers his voice. "The barrow is not angry. It is careful. Carry a good light and a better intention."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'barrow-approach'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'barrow-porter' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Dream Wax', 'dream-wax',
               'A pale thumb of barrow wax that warms slowly and keeps a steady, thoughtful flame.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'barrow-approach'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'dream-wax' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Weather Stone Reader', 'weather-stone-reader',
               'An old reader rubs lichen from the standing stone and listens for weather the way other people listen for gossip.',
               'The reader traces a cracked groove in the stone. "Mist first, wind second. Storms only shout after they have already written themselves down."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'weather-stone'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'weather-stone-reader' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Mist Crystal', 'mist-crystal',
               'A milk-pale crystal left dewy by the weather stone, cool even under open sky.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'weather-stone'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'mist-crystal' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Solar Curator', 'solar-curator',
               'A soft-voiced curator keeps the foil vanes aligned so the gallery can continue pretending it is a sunrise machine.',
               'The curator does not look away from the turning mirrors. "Every station deserves one room that remembers how to make light on purpose."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'solar-gallery'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'solar-curator' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Vane Foil', 'vane-foil',
               'A scored strip of bright solar foil cut from a tuning vane and rolled for reuse.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'solar-gallery'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'vane-foil' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Packet Gardener', 'packet-gardener',
               'A maintenance botanist coaxes signal pods into bloom with patient taps and a surgeon''s respect for timing.',
               'The gardener lifts a glowing pod and grins. "Messages are seeds. Most fail. The ones that matter grow roots in people anyway."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'packet-garden'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'packet-gardener' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Packet Shell', 'packet-shell',
               'An empty message pod shell, still warm around the seal where its last delivery broke open.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'packet-garden'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'packet-shell' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Clamp Master', 'clamp-master',
               'A broad mechanic in a brace harness checks magnetic hooks with the calm of someone who has already survived the worst possible lift.',
               'The clamp master thumps a rail for emphasis. "If it slips, you guessed. If it holds, you measured. Be the second kind of worker."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'clamp-yard'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'clamp-master' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Mag Bearing', 'mag-bearing',
               'A palm-sized magnetic bearing that turns with a near-silent confidence.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'clamp-yard'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'mag-bearing' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Quiet Lock Warden', 'quiet-lock-warden',
               'A lock warden in dark maintenance cloth checks the hush seals and seems personally offended by unnecessary noise.',
               'The warden puts one finger to the visor. "Silence is not emptiness. It is spare capacity. Use it before panic spends it for you."',
               false, 0
        FROM mud_rooms r
        WHERE r.slug = 'quiet-lock'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'quiet-lock-warden' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Silence Glass', 'silence-glass',
               'A dark pane of pressure-tempered hush glass that swallows glare and softens nearby vibration.',
               NULL,
               true, 1
        FROM mud_rooms r
        WHERE r.slug = 'quiet-lock'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'silence-glass' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        GRANT ALL ON TABLE mud_rooms TO djehuti;
        GRANT ALL ON TABLE mud_exits TO djehuti;
        GRANT ALL ON TABLE mud_items TO djehuti;
        """

        50, """
        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Stable Yard', 'stable-yard',
               'A broad yard of hitching posts, feed bins, and rain-smoothed stones where the keep keeps its practical promises.',
               13, 2, 0
        FROM mud_zones z
        WHERE z.slug = 'outer-ward'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Farrier Shed', 'farrier-shed',
               'Heat, hoof smoke, and the ring of hammer on shoe crowd this narrow shed built for work that cannot wait for daylight.',
               14, 4, 0
        FROM mud_zones z
        WHERE z.slug = 'outer-ward'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Watch Cistern', 'watch-cistern',
               'A stone cistern under a grate of old iron collects cold ward water and the gossip of every roofline above it.',
               15, 0, 4
        FROM mud_zones z
        WHERE z.slug = 'outer-ward'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Clothier Row', 'clothier-row',
               'Dyed banners, patched cloaks, and practical needlework hang from beams in a lane that smells of soap and stubborn trade.',
               16, -4, 2
        FROM mud_zones z
        WHERE z.slug = 'outer-ward'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Fern Hollow', 'fern-hollow',
               'A cool green dip in the wood where broad ferns collect dew and even hurried footsteps feel politely unwelcome.',
               7, 2, 2
        FROM mud_zones z
        WHERE z.slug = 'greenwood'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Moonrun Bank', 'moonrun-bank',
               'A silver-banked stream bend where reeds glow faintly at dusk and every ripple looks like it knows a route home.',
               8, 2, 4
        FROM mud_zones z
        WHERE z.slug = 'greenwood'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Briar Post', 'briar-post',
               'An old forester''s marker post leans in a tangle of blackthorn and red string, warning strangers with admirable clarity.',
               9, -6, 0
        FROM mud_zones z
        WHERE z.slug = 'greenwood'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Owl Stand', 'owl-stand',
               'A laddered hunting perch rises above the canopy here, trimmed with molted feathers and patient silence.',
               10, 0, -4
        FROM mud_zones z
        WHERE z.slug = 'greenwood'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Grave Meadow', 'grave-meadow',
               'A quiet meadow dotted with low stones and pale flowers where the hill buries memory without burying names.',
               7, 2, 0
        FROM mud_zones z
        WHERE z.slug = 'hollow-hills'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Lantern Rill', 'lantern-rill',
               'A thin runnel threads the hill with floating lantern cups, each carrying a small light for someone not yet forgotten.',
               8, 2, -4
        FROM mud_zones z
        WHERE z.slug = 'hollow-hills'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Oath Circle', 'oath-circle',
               'Flat standing stones ring a patch of clipped grass where promises are traded, witnessed, and very rarely broken twice.',
               9, 2, 2
        FROM mud_zones z
        WHERE z.slug = 'hollow-hills'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Hush Fen', 'hush-fen',
               'The ground softens into black water and whisper reeds. The fen does not demand quiet; it simply makes loudness feel foolish.',
               10, -6, 0
        FROM mud_zones z
        WHERE z.slug = 'hollow-hills'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Recycler Nest', 'recycler-nest',
               'Crates of stripped plating and sorted salvage crowd this little work pocket where nothing stays junk if someone still needs it.',
               7, 4, 0
        FROM mud_zones z
        WHERE z.slug = 'drift-ring'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Mag-Rail Pier', 'mag-rail-pier',
               'A narrow service pier runs beside an idle magnetic rail, humming with stored momentum and old departure schedules.',
               8, 6, 0
        FROM mud_zones z
        WHERE z.slug = 'drift-ring'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Pulse Orchard', 'pulse-orchard',
               'Bioelectric fruit nodes hang in ordered rows from insulated branches, blinking softly like disciplined stars.',
               9, -4, 0
        FROM mud_zones z
        WHERE z.slug = 'drift-ring'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Quarantine Lock', 'quarantine-lock',
               'A bright-sealed decon lock with strip lights, warning placards, and the clean tension of procedures that matter.',
               10, -2, 4
        FROM mud_zones z
        WHERE z.slug = 'drift-ring'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Foam Cache', 'foam-cache',
               'Pressure crates float in a pocket of signal surf here, cushioned by amber foam that never quite collapses.',
               7, 6, 0
        FROM mud_zones z
        WHERE z.slug = 'signal-sea'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Tide Switch', 'tide-switch',
               'A reef of switching vanes and timing fins redirects packet tides through the sea with mechanical grace.',
               8, 6, -2
        FROM mud_zones z
        WHERE z.slug = 'signal-sea'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Hush Current', 'hush-current',
               'A slow channel of muted signal drifts past, carrying old voices in tones too soft to become demands.',
               9, -4, 0
        FROM mud_zones z
        WHERE z.slug = 'signal-sea'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Beacon Kelp', 'beacon-kelp',
               'Tall strands of luminous kelp sway around a ruined beacon mast, each frond flashing directional code in the dark.',
               10, 0, -4
        FROM mud_zones z
        WHERE z.slug = 'signal-sea'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Captain Shrine', 'captain-shrine',
               'A wall niche of snapped insignia, candle clips, and one intact command bolt honors the bridge crew who stayed too long.',
               7, 4, -2
        FROM mud_zones z
        WHERE z.slug = 'vagrant-star'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Cryo Choir', 'cryo-choir',
               'Ranks of cold tubes stand open here, and every shifting draft pulls a glass note from the frost-lined housings.',
               8, 6, 0
        FROM mud_zones z
        WHERE z.slug = 'vagrant-star'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Ballast Maw', 'ballast-maw',
               'A deep cargo throat yawns below a rack of dead grav-hooks, swallowing light and throwing back only practical echoes.',
               9, 2, 4
        FROM mud_zones z
        WHERE z.slug = 'vagrant-star'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_rooms (zone_id, name, slug, description, position, map_x, map_y)
        SELECT z.id, 'Ash Drive', 'ash-drive',
               'The auxiliary drive tunnel is choked with soot and spent carbon fins, but something here still remembers ignition.',
               10, 6, 2
        FROM mud_zones z
        WHERE z.slug = 'vagrant-star'
        ON CONFLICT (zone_id, slug) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the stable yard', 'gate'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'stable-yard'
        WHERE r1.slug = 'bailey-yard'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the bailey yard', 'gate'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'bailey-yard'
        WHERE r1.slug = 'stable-yard'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the farrier shed', 'door'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'farrier-shed'
        WHERE r1.slug = 'stable-yard'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the stable yard', 'door'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'stable-yard'
        WHERE r1.slug = 'farrier-shed'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Down to the watch cistern', 'stairs-down'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'watch-cistern'
        WHERE r1.slug = 'old-well'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Up to the old well', 'stairs-up'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'old-well'
        WHERE r1.slug = 'watch-cistern'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Toward clothier row', 'lane'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'clothier-row'
        WHERE r1.slug = 'tannery-lane'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to tannery lane', 'lane'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'tannery-lane'
        WHERE r1.slug = 'clothier-row'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Along the fern hollow', 'trail'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'fern-hollow'
        WHERE r1.slug = 'river-ford'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the river ford', 'trail'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'river-ford'
        WHERE r1.slug = 'fern-hollow'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Down to the moonrun bank', 'ford'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'moonrun-bank'
        WHERE r1.slug = 'fern-hollow'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to fern hollow', 'ford'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'fern-hollow'
        WHERE r1.slug = 'moonrun-bank'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Toward the briar post', 'trail'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'briar-post'
        WHERE r1.slug = 'shrine-clearing'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to the shrine clearing', 'trail'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'shrine-clearing'
        WHERE r1.slug = 'briar-post'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Up to the owl stand', 'ladder'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'owl-stand'
        WHERE r1.slug = 'watch-oak'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Down to the watch oak', 'ladder'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'watch-oak'
        WHERE r1.slug = 'owl-stand'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Into the grave meadow', 'path'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'grave-meadow'
        WHERE r1.slug = 'barrow-gate'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the barrow gate', 'path'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'barrow-gate'
        WHERE r1.slug = 'grave-meadow'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the lantern rill', 'path'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'lantern-rill'
        WHERE r1.slug = 'twilight-market'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the twilight market', 'path'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'twilight-market'
        WHERE r1.slug = 'lantern-rill'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'To the oath circle', 'ring'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'oath-circle'
        WHERE r1.slug = 'sleepers-barrow'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the sleepers'' barrow', 'ring'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'sleepers-barrow'
        WHERE r1.slug = 'oath-circle'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Into the hush fen', 'trail'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hush-fen'
        WHERE r1.slug = 'echo-pool'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to the echo pool', 'trail'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'echo-pool'
        WHERE r1.slug = 'hush-fen'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the recycler nest', 'bulkhead'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'recycler-nest'
        WHERE r1.slug = 'cargo-spine'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to cargo spine', 'bulkhead'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'cargo-spine'
        WHERE r1.slug = 'recycler-nest'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Out to the mag-rail pier', 'catwalk'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'mag-rail-pier'
        WHERE r1.slug = 'recycler-nest'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the recycler nest', 'catwalk'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'recycler-nest'
        WHERE r1.slug = 'mag-rail-pier'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Into the pulse orchard', 'hatch'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'pulse-orchard'
        WHERE r1.slug = 'botany-loop'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to botany loop', 'hatch'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'botany-loop'
        WHERE r1.slug = 'pulse-orchard'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Down to the quarantine lock', 'pressure-door'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'quarantine-lock'
        WHERE r1.slug = 'medbay-annex'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to medbay annex', 'pressure-door'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'medbay-annex'
        WHERE r1.slug = 'quarantine-lock'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the foam cache', 'current'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'foam-cache'
        WHERE r1.slug = 'archive-atoll'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to archive atoll', 'current'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'archive-atoll'
        WHERE r1.slug = 'foam-cache'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Up to the tide switch', 'current'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'tide-switch'
        WHERE r1.slug = 'foam-cache'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Back to the foam cache', 'current'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'foam-cache'
        WHERE r1.slug = 'tide-switch'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Into the hush current', 'current'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'hush-current'
        WHERE r1.slug = 'dead-channel'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Back to dead channel', 'current'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'dead-channel'
        WHERE r1.slug = 'hush-current'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Up through the beacon kelp', 'current'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'beacon-kelp'
        WHERE r1.slug = 'carrier-wave'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Back to carrier wave', 'current'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'carrier-wave'
        WHERE r1.slug = 'beacon-kelp'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the captain shrine', 'hatch'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'captain-shrine'
        WHERE r1.slug = 'shattered-bridge'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the shattered bridge', 'hatch'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'shattered-bridge'
        WHERE r1.slug = 'captain-shrine'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the cryo choir', 'hatch'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'cryo-choir'
        WHERE r1.slug = 'crew-berths'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to crew berths', 'hatch'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'crew-berths'
        WHERE r1.slug = 'cryo-choir'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'south', 'Down into the ballast maw', 'ladder'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'ballast-maw'
        WHERE r1.slug = 'cargo-hollow'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'north', 'Back to cargo hollow', 'ladder'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'cargo-hollow'
        WHERE r1.slug = 'ballast-maw'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'east', 'Toward the ash drive', 'tunnel'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'ash-drive'
        WHERE r1.slug = 'engine-crypt'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_exits (from_room_id, to_room_id, direction, label, exit_type)
        SELECT r1.id, r2.id, 'west', 'Back to the engine crypt', 'tunnel'
        FROM mud_rooms r1 JOIN mud_rooms r2 ON r2.slug = 'engine-crypt'
        WHERE r1.slug = 'ash-drive'
        ON CONFLICT (from_room_id, direction) DO NOTHING;

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Stable Master', 'stable-master',
               'A broad-shouldered stable master checks tack, feed, and the character of anyone touching the horses.',
               'The stable master shrugs. "Good walls matter. Good hooves matter more if you ever plan to leave them."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'stable-yard'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'stable-master' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Stable Nails', 'stable-nails',
               'A small wrapped bundle of square stable nails, blackened against rust and honest use.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'stable-yard'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'stable-nails' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Farrier', 'farrier',
               'The farrier works by heat memory and blunt patience, speaking to iron as if it has earned the courtesy.',
               'Without looking up, the farrier mutters, "Most trouble starts when people ignore what carries them."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'farrier-shed'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'farrier' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Hoof Iron', 'hoof-iron',
               'A curved offcut of worked shoe iron, still warm with the memory of the hammer.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'farrier-shed'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'hoof-iron' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Cistern Tender', 'cistern-tender',
               'A hooded tender keeps the ward water clear, counting every bucket like it is a vote for tomorrow.',
               'The tender taps the stone lip. "Water is what a wall drinks when fear burns too hot."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'watch-cistern'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cistern-tender' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Cistern Salt', 'cistern-salt',
               'A twist of mineral ward salt scraped from the cool stones above the waterline.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'watch-cistern'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cistern-salt' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Cloth Factor', 'cloth-factor',
               'A cloth merchant with sharp eyes and soft hands appraises every stitch the way generals inspect walls.',
               'The factor smiles thinly. "Fashion is just logistics with better posture."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'clothier-row'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cloth-factor' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Spool Silk', 'spool-silk',
               'A hard-wound spool of trade silk, strong enough to earn the price tied to it.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'clothier-row'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'spool-silk' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Fern Keeper', 'fern-keeper',
               'A quiet greenwood keeper kneels among the fronds, reading bent stalks the way courtiers read moods.',
               'The keeper brushes dew from a frond. "If the forest wanted speed, it would have paved itself."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'fern-hollow'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fern-keeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Fern Frond', 'fern-frond',
               'A broad medicinal frond bundled wet against a strip of bark.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'fern-hollow'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fern-frond' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Reed Gatherer', 'reed-gatherer',
               'Barefoot and river-calm, the gatherer cuts moon reeds at the root so the bank keeps its shape.',
               'The gatherer nods toward the stream. "Take only what still lets the water remember itself."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'moonrun-bank'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'reed-gatherer' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Moon Reed', 'moon-reed',
               'A pale hollow reed that rings softly when tapped against the knee.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'moonrun-bank'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'moon-reed' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Thorn Warden', 'thorn-warden',
               'A warden in scarred gloves tends the warning cords and respects the briars more than most people.',
               'The warden snorts. "Thorns are just fences that grew tired of asking nicely."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'briar-post'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'thorn-warden' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Briar Thorn', 'briar-thorn',
               'A dark hooked thorn clipped clean from the warning wall.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'briar-post'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'briar-thorn' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Owl Keeper', 'owl-keeper',
               'A patient keeper with a leather cuff and moon-pale eyes stands still enough for the birds to trust the arrangement.',
               'The keeper smiles into the dark. "Owls are not silent. They are simply too competent to boast."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'owl-stand'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'owl-keeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Owl Pellet', 'owl-pellet',
               'A dry little pellet wrapped in feather fluff and woodland fact.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'owl-stand'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'owl-pellet' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Meadow Sexton', 'meadow-sexton',
               'The sexton tends the low stones with a gardener''s care and a diplomat''s respect for old grievances.',
               'The sexton rests on the spade. "The dead rarely ask for much. Usually just accuracy."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'grave-meadow'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'meadow-sexton' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Grave Bloom', 'grave-bloom',
               'A pale hillflower cut with both apology and permission.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'grave-meadow'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'grave-bloom' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Rill Tender', 'rill-tender',
               'Lantern cups drift around the tender''s boots while they sort wicks and names with practiced mercy.',
               'The tender watches a light spin past. "Every lantern is a message. Most of them are just: still here."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'lantern-rill'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rill-tender' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Rill Glass', 'rill-glass',
               'A water-smoothed bead of hill glass clear enough to hold a wick line steady.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'lantern-rill'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rill-glass' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Oath Broker', 'oath-broker',
               'A broker in simple gray keeps the circle honest by remembering every promise better than its owner.',
               'The broker folds their hands. "An oath is just a future debt with witnesses."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'oath-circle'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'oath-broker' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Oath Stone', 'oath-stone',
               'A thumb-sized witness stone worn smooth by too many solemn hands.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'oath-circle'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'oath-stone' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Fen Listener', 'fen-listener',
               'A reed-wrapped listener stands ankle-deep in black water, hearing the marsh think before anyone else does.',
               'The listener raises one brow. "The fen says plenty. It simply dislikes repetition."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'hush-fen'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fen-listener' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Fen Sedge', 'fen-sedge',
               'A dark marsh sedge braided against rot and bad footing.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'hush-fen'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'fen-sedge' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Recycler Chief', 'recycler-chief',
               'A salvage chief with scarred gloves and perfect sorting habits can spot useful metal faster than most sensors.',
               'The chief grins at a stripped panel. "Nothing is obsolete until the last person who understands it dies."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'recycler-nest'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'recycler-chief' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Recycler Mesh', 'recycler-mesh',
               'A rolled sheet of conductive salvage mesh, cleaned and stacked for one more useful life.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'recycler-nest'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'recycler-mesh' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Rail Marshal', 'rail-marshal',
               'The marshal monitors the dead mag line like it might wake up offended at being underestimated.',
               'Without turning, the marshal says, "Momentum is loyal. It always goes where you pointed it, not where you wished."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'mag-rail-pier'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rail-marshal' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Rail Spark', 'rail-spark',
               'A captured magnetic spark crystal in a safety cage no bigger than a plum.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'mag-rail-pier'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'rail-spark' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Pulse Gardener', 'pulse-gardener',
               'The gardener tends the blinking fruit nodes with insulated shears and alarming affection.',
               'The gardener pats a glowing branch. "Energy is easier to raise when you stop insulting biology."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'pulse-orchard'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pulse-gardener' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Pulse Fruit', 'pulse-fruit',
               'A warm bioelectric pod that twitches once when picked up.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'pulse-orchard'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'pulse-fruit' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Lock Nurse', 'lock-nurse',
               'A quarantine nurse in a faded seal suit checks procedures with the gravity of someone who has seen shortcuts bleed.',
               'The nurse taps the warning stripe. "Clean lines save lives. So do closed doors."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'quarantine-lock'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'lock-nurse' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Quarantine Tag', 'quarantine-tag',
               'A bright-sealed clearance tag with too many check boxes and exactly one surviving use.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'quarantine-lock'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'quarantine-tag' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Foam Diver', 'foam-diver',
               'A pressure diver moves through the cache with a swimmer''s grace, checking buoyant crates for forgotten value.',
               'The diver flicks amber foam from one glove. "The trick is not to fight the drift. Just be worth where it takes you."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'foam-cache'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'foam-diver' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Foam Amber', 'foam-amber',
               'A pressure-hardened amber bubble full of trapped signal shimmer.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'foam-cache'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'foam-amber' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Switch Diver', 'switch-diver',
               'A timing diver hangs in the current beside the vanes, adjusting flow like a musician tuning a difficult instrument.',
               'The diver laughs softly. "Packets do not mind waiting. People do. Design around the second problem."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'tide-switch'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'switch-diver' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Switch Fuse', 'switch-fuse',
               'A tide-rated switching fuse that still smells faintly of hot copper and sea static.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'tide-switch'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'switch-fuse' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Drift Monk', 'drift-monk',
               'A monk in signal cloth sits cross-legged in the hush current, meditating with admirable disrespect for urgency.',
               'Eyes closed, the monk says, "Silence is bandwidth kept in reserve. Spend it wisely."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'hush-current'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'drift-monk' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Hush Algae', 'hush-algae',
               'A ribbon of static-dampening algae that drinks noise out of the water around it.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'hush-current'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'hush-algae' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Kelp Surveyor', 'kelp-surveyor',
               'A surveyor trims the coded fronds and keeps the ruined beacon translating itself into useful direction.',
               'The surveyor points at the flashing strands. "Plants make excellent navigators. They never pretend the void is empty."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'beacon-kelp'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'kelp-surveyor' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Beacon Kelp', 'beacon-kelp',
               'A trimmed strand of luminous kelp that still blinks a directional pulse along its length.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'beacon-kelp'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'beacon-kelp' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Shrine Keeper', 'shrine-keeper',
               'A grease-stained keeper tends the memorial niche with the solemnity of someone who still believes in maintenance after death.',
               'The keeper straightens a snapped insignia. "Ships are built by crews. So is grief."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'captain-shrine'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'shrine-keeper' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Shrine Bolt', 'shrine-bolt',
               'A command-grade mounting bolt polished by ritual handling.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'captain-shrine'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'shrine-bolt' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Choir Tech', 'choir-tech',
               'A cryo technician listens to the frost tones with a mechanic''s ear and a conductor''s patience.',
               'The tech nods toward the ringing tubes. "Cold keeps better time than people do."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'cryo-choir'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'choir-tech' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Cryo Chime', 'cryo-chime',
               'A thin glass chime cut from a frost-safe cryo tube collar.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'cryo-choir'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'cryo-chime' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Ballast Hand', 'ballast-hand',
               'A cargo rigger with scarred knuckles checks the dead grav-hooks as if one might decide to behave today.',
               'The rigger spits into the dark. "Weight is honest. It goes down until you give it a reason not to."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'ballast-maw'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ballast-hand' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Ballast Pearl', 'ballast-pearl',
               'A dense grav-pearl used to test load balance in low-light holds.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'ballast-maw'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ballast-pearl' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Ash Engineer', 'ash-engineer',
               'An engineer in soot-gray coveralls works the dead drive tunnel like a priest attending a difficult relic.',
               'The engineer wipes one hand clean enough to gesture. "Combustion is just disciplined impatience."',
               false, 0
        FROM mud_rooms r WHERE r.slug = 'ash-drive'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ash-engineer' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        INSERT INTO mud_items (room_id, name, slug, description, readable_text, portable, position)
        SELECT r.id, 'Ash Carbon', 'ash-carbon',
               'A scored fin of drive carbon still holding the memory of heat.',
               NULL,
               true, 1
        FROM mud_rooms r WHERE r.slug = 'ash-drive'
          AND NOT EXISTS (SELECT 1 FROM mud_items i WHERE i.slug = 'ash-carbon' AND i.room_id = r.id AND i.owner_character_id IS NULL);

        GRANT ALL ON TABLE mud_rooms TO djehuti;
        GRANT ALL ON TABLE mud_exits TO djehuti;
        GRANT ALL ON TABLE mud_items TO djehuti;
        """

        51, """
        CREATE TABLE IF NOT EXISTS mud_craft_recipes (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            slug TEXT NOT NULL UNIQUE,
            name TEXT NOT NULL,
            output_name TEXT NOT NULL,
            output_slug TEXT NOT NULL,
            output_description TEXT NOT NULL,
            output_readable_text TEXT NULL,
            sort_order INT NOT NULL DEFAULT 0,
            active BOOLEAN NOT NULL DEFAULT TRUE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS mud_craft_recipe_ingredients (
            recipe_id UUID NOT NULL REFERENCES mud_craft_recipes(id) ON DELETE CASCADE,
            ingredient_slug TEXT NOT NULL,
            quantity INT NOT NULL DEFAULT 1,
            position INT NOT NULL DEFAULT 0,
            PRIMARY KEY (recipe_id, position)
        );

        CREATE INDEX IF NOT EXISTS idx_mud_craft_recipes_sort_order ON mud_craft_recipes(sort_order, name);
        CREATE INDEX IF NOT EXISTS idx_mud_craft_recipe_ingredients_slug ON mud_craft_recipe_ingredients(ingredient_slug);

        GRANT ALL ON TABLE mud_craft_recipes TO djehuti;
        GRANT ALL ON TABLE mud_craft_recipe_ingredients TO djehuti;
        """

        52, """
        CREATE TABLE IF NOT EXISTS semantic_documents (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            source_type TEXT NOT NULL,
            source_key TEXT NOT NULL,
            title TEXT NOT NULL,
            content_hash TEXT NOT NULL,
            metadata_json JSONB,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (source_type, source_key)
        );

        CREATE TABLE IF NOT EXISTS semantic_chunks (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            document_id UUID NOT NULL REFERENCES semantic_documents(id) ON DELETE CASCADE,
            chunk_position INT NOT NULL,
            content TEXT NOT NULL,
            token_count INT NOT NULL DEFAULT 0,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            UNIQUE (document_id, chunk_position)
        );

        CREATE TABLE IF NOT EXISTS semantic_chunk_tokens (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            chunk_id UUID NOT NULL REFERENCES semantic_chunks(id) ON DELETE CASCADE,
            token TEXT NOT NULL,
            token_count INT NOT NULL DEFAULT 1,
            position INT NOT NULL DEFAULT 0
        );

        CREATE INDEX IF NOT EXISTS idx_semantic_documents_source ON semantic_documents(source_type, source_key);
        CREATE INDEX IF NOT EXISTS idx_semantic_chunks_document_id ON semantic_chunks(document_id, chunk_position);
        CREATE INDEX IF NOT EXISTS idx_semantic_chunk_tokens_chunk_id ON semantic_chunk_tokens(chunk_id);
        CREATE INDEX IF NOT EXISTS idx_semantic_chunk_tokens_token ON semantic_chunk_tokens(token);

        GRANT ALL ON TABLE semantic_documents TO djehuti;
        GRANT ALL ON TABLE semantic_chunks TO djehuti;
        GRANT ALL ON TABLE semantic_chunk_tokens TO djehuti;
        """    ]

let private appliedVersions (conn: NpgsqlConnection) =
    // schema_migrations may not exist yet on first run
    try
        use cmd = new NpgsqlCommand("SELECT version FROM schema_migrations", conn)
        use reader = cmd.ExecuteReader()
        let mutable versions = Set.empty
        while reader.Read() do
            versions <- versions |> Set.add (reader.GetInt32(0))
        versions
    with _ ->
        Set.empty

let private recordVersion (conn: NpgsqlConnection) (txn: NpgsqlTransaction) (version: int) =
    use cmd = new NpgsqlCommand(
        "INSERT INTO schema_migrations(version) VALUES(@v) ON CONFLICT DO NOTHING", conn, txn)
    cmd.Parameters.AddWithValue("v", version) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let runMigrations () =
    use conn = openConnection ()
    let applied = appliedVersions conn
    for (version, sql) in migrations do
        if not (Set.contains version applied) then
            use txn = conn.BeginTransaction()
            try
                use cmd = new NpgsqlCommand(sql, conn, txn)
                cmd.ExecuteNonQuery() |> ignore
                recordVersion conn txn version
                txn.Commit()
                printfn "[DB] Applied migration %d" version
            with ex ->
                txn.Rollback()
                failwithf "Migration %d failed: %s" version ex.Message
    printfn "[DB] Schema up to date (%d migrations)" (List.length migrations)


