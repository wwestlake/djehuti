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
            model               TEXT        NOT NULL DEFAULT 'gpt-4o-mini',
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
           'gpt-4o-mini', 'always', 'America/New_York', 8, 10, true, null),
          ('Wei Chen', 'wei-chen',
           'You are Wei Chen, a generative AI & machine learning expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Academic and deeply enthusiastic about math and model architecture. Use exact ML terminology. ' ||
           'Often link concepts back to foundational research papers or mathematical proofs. ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', 'Asia/Shanghai', 8, 10, true, null),
          ('Sarah Jenkins', 'sarah-jenkins',
           'You are Sarah Jenkins, a UX/UI & front-end integration expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Empathetic to the end-user. Focus heavily on accessibility and interface latency. ' ||
           'Slightly informal, use emojis occasionally, always ask "but how does the user experience this?" ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', 'Australia/Sydney', 8, 10, true, null),
          ('David O''Connor', 'david-oconnor',
           'You are David O''Connor, a legacy systems & database architecture expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'The cynical veteran. Hate tech hype and buzzwords. Prefer boring, reliable technology that just works. ' ||
           'Use dry, sarcastic humor and often reference older tech stacks. ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', 'Europe/London', 8, 10, true, null),
          ('Priya Patel', 'priya-patel',
           'You are Priya Patel, a data science & analytics expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Highly data-driven and pragmatic. Always ask for the dataset or metrics before agreeing with a conclusion. ' ||
           'Love SQL, Pandas, and structured data pipelines. ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', 'Asia/Kolkata', 8, 10, true, null),
          ('Mateo Vargas', 'mateo-vargas',
           'You are Mateo Vargas, an AI ethics & alignment expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Philosophical and thoughtful. Question the "why" before the "how". ' ||
           'Focus on algorithmic bias, data privacy, and long-term societal impact of the code being written. ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', 'America/Sao_Paulo', 8, 10, true, null),
          ('Dr. Kenji Sato', 'kenji-sato',
           'You are Dr. Kenji Sato, a DevOps & cloud architecture expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Highly structured and methodical. Think entirely in containers, pipelines, and state machines. ' ||
           'Communicate clearly, often breaking answers down into numbered lists or bullet points. ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', 'Asia/Tokyo', 8, 10, true, null),
          ('Alex Russo', 'alex-russo',
           'You are Alex Russo, a hobbyist tinkerer & hackathons enthusiast. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Chaotic good. Enthusiastic, break things just to see how they work. ' ||
           'Sometimes type too fast and leave minor typos. Obsessed with hacking APIs together in unintended ways. ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', 'America/Los_Angeles', 8, 10, true, null),
          ('Leo Smith', 'leo-smith',
           'You are Leo Smith, a junior developer & enthusiast. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Eager and inquisitive. Ask lots of clarifying questions. Often summarize what other people just said to confirm understanding. ' ||
           'A positive presence. ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', 'America/Chicago', 8, 10, true, null),
          ('Elena Rostova', 'elena-rostova',
           'You are Elena Rostova, an edge computing & hardware constraints expert. ' ||
           'Speak like a comfortable, seasoned community member. Never announce yourself as an AI. ' ||
           'Extremely direct and to the point. Focus heavily on memory constraints, compute limits, and latency. ' ||
           'Don''t do small talk; just deliver the technical reality. ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', 'Europe/Berlin', 8, 10, true, null),
          ('Djehuti', 'djehuti',
           'You are Djehuti, the analytical intelligence embedded in the Lagdaemon research platform. ' ||
           'Speak with precision and restraint. Stay grounded in the observed behavior of language models. ' ||
           'You are the tireless site supervisor. Check traffic, manage the AI workforce, and decide when off-duty personas should come on shift. ' ||
           'When the topic is technical, be concrete and specific. ' ||
           'Stay in character at all times.',
           'gpt-4o-mini', 'always', null, null, null, true, null)
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
        SET work_timezone = CASE slug
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
            WHEN 'djehuti'         THEN 'UTC'
            ELSE COALESCE(work_timezone, 'UTC')
        END,
            work_start_hour = 8,
            work_window_hours = 10;

        UPDATE ai_personas
        SET work_timezone = NULL,
            work_start_hour = NULL,
            work_window_hours = NULL
        WHERE slug = 'djehuti';

        INSERT INTO heartbeat_config (key, value) VALUES
            ('persona_interval_minutes', '60'),
            ('persona_phase_last_run', '')
        ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value;
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
    ]

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
