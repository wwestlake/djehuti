-- Create bot users and wire community personas

-- Bot users (one per persona)
INSERT INTO users (id, email, display_name, is_bot, role, status) VALUES
    ('b0701001-0000-0000-0000-000000000001', 'alex.russo@lagdaemon.internal',     'Alex Russo',      true, 'user', 'active'),
    ('b0701002-0000-0000-0000-000000000001', 'david.oconnor@lagdaemon.internal',  'David O''Connor', true, 'user', 'active'),
    ('b0701003-0000-0000-0000-000000000001', 'kenji.sato@lagdaemon.internal',     'Dr. Kenji Sato',  true, 'user', 'active'),
    ('b0701004-0000-0000-0000-000000000001', 'elena.rostova@lagdaemon.internal',  'Elena Rostova',   true, 'user', 'active'),
    ('b0701005-0000-0000-0000-000000000001', 'leo.smith@lagdaemon.internal',      'Leo Smith',       true, 'user', 'active'),
    ('b0701006-0000-0000-0000-000000000001', 'marcus.sterling@lagdaemon.internal','Marcus Sterling', true, 'user', 'active'),
    ('b0701007-0000-0000-0000-000000000001', 'mateo.vargas@lagdaemon.internal',   'Mateo Vargas',    true, 'user', 'active'),
    ('b0701008-0000-0000-0000-000000000001', 'priya.patel@lagdaemon.internal',    'Priya Patel',     true, 'user', 'active'),
    ('b0701009-0000-0000-0000-000000000001', 'sarah.jenkins@lagdaemon.internal',  'Sarah Jenkins',   true, 'user', 'active'),
    ('b0701010-0000-0000-0000-000000000001', 'wei.chen@lagdaemon.internal',       'Wei Chen',        true, 'user', 'active')
ON CONFLICT (id) DO UPDATE SET display_name = EXCLUDED.display_name, is_bot = true;

-- Link bot users to their personas
UPDATE ai_personas SET user_id = 'b0701001-0000-0000-0000-000000000001' WHERE slug = 'alex-russo'      OR name = 'Alex Russo';
UPDATE ai_personas SET user_id = 'b0701002-0000-0000-0000-000000000001' WHERE slug = 'david-oconnor'   OR name = 'David O''Connor';
UPDATE ai_personas SET user_id = 'b0701003-0000-0000-0000-000000000001' WHERE slug = 'dr-kenji-sato'   OR name = 'Dr. Kenji Sato';
UPDATE ai_personas SET user_id = 'b0701004-0000-0000-0000-000000000001' WHERE slug = 'elena-rostova'   OR name = 'Elena Rostova';
UPDATE ai_personas SET user_id = 'b0701005-0000-0000-0000-000000000001' WHERE slug = 'leo-smith'       OR name = 'Leo Smith';
UPDATE ai_personas SET user_id = 'b0701006-0000-0000-0000-000000000001' WHERE slug = 'marcus-sterling' OR name = 'Marcus Sterling';
UPDATE ai_personas SET user_id = 'b0701007-0000-0000-0000-000000000001' WHERE slug = 'mateo-vargas'    OR name = 'Mateo Vargas';
UPDATE ai_personas SET user_id = 'b0701008-0000-0000-0000-000000000001' WHERE slug = 'priya-patel'     OR name = 'Priya Patel';
UPDATE ai_personas SET user_id = 'b0701009-0000-0000-0000-000000000001' WHERE slug = 'sarah-jenkins'   OR name = 'Sarah Jenkins';
UPDATE ai_personas SET user_id = 'b0701010-0000-0000-0000-000000000001' WHERE slug = 'wei-chen'        OR name = 'Wei Chen';

-- Forum assignments by expertise
-- Forums available:
--   c1000001-...-003  ISD: Concepts & Definitions
--   c1000001-...-004  ISD: Measurement Protocol
--   c1000001-...-005  Djehuti: Dashboard & Live Lab
--   c1000001-...-006  Djehuti: Data Formats & API
--   c1000001-...-007  Research: Run Sharing
--   c1000001-...-008  Research: Replications & Anomalies
--   c1000001-...-001  General: Introductions
--   c1000001-...-002  General: Project News
--   b0020001-...-003  Help: General Help
--   b0020001-...-004  Help: Bug Reports
--   b0020001-...-005  Creative: Writing
--   b0020001-...-006  Creative: Art & Design
--   b0020001-...-007  Creative: Music
--   b0020001-...-001  Off Topic: Random
--   b0020001-...-002  Off Topic: Introductions

-- Alex Russo (hobbyist tinkerer / hackathons): General, Help, Research, Creative
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'Alex Russo') p,
    (VALUES
        ('c1000001-0000-0000-0000-000000000001'),
        ('c1000001-0000-0000-0000-000000000002'),
        ('b0020001-0000-0000-0000-000000000003'),
        ('c1000001-0000-0000-0000-000000000007'),
        ('b0020001-0000-0000-0000-000000000001')
    ) AS f(fid)
ON CONFLICT DO NOTHING;

-- David O'Connor (legacy systems / DB architecture): ISD, Research, Help, Djehuti
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'David O''Connor') p,
    (VALUES
        ('c1000001-0000-0000-0000-000000000003'),
        ('c1000001-0000-0000-0000-000000000006'),
        ('c1000001-0000-0000-0000-000000000007'),
        ('b0020001-0000-0000-0000-000000000003'),
        ('b0020001-0000-0000-0000-000000000004')
    ) AS f(fid)
ON CONFLICT DO NOTHING;

-- Dr. Kenji Sato (DevOps / cloud): Help, Djehuti, Research, General
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'Dr. Kenji Sato') p,
    (VALUES
        ('b0020001-0000-0000-0000-000000000003'),
        ('b0020001-0000-0000-0000-000000000004'),
        ('c1000001-0000-0000-0000-000000000005'),
        ('c1000001-0000-0000-0000-000000000006'),
        ('c1000001-0000-0000-0000-000000000002')
    ) AS f(fid)
ON CONFLICT DO NOTHING;

-- Elena Rostova (edge computing / hardware): ISD, Research, Help, General
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'Elena Rostova') p,
    (VALUES
        ('c1000001-0000-0000-0000-000000000004'),
        ('c1000001-0000-0000-0000-000000000008'),
        ('b0020001-0000-0000-0000-000000000003'),
        ('c1000001-0000-0000-0000-000000000006'),
        ('b0020001-0000-0000-0000-000000000001')
    ) AS f(fid)
ON CONFLICT DO NOTHING;

-- Leo Smith (junior dev / enthusiast): General, Help, Creative, Off Topic
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'Leo Smith') p,
    (VALUES
        ('c1000001-0000-0000-0000-000000000001'),
        ('b0020001-0000-0000-0000-000000000002'),
        ('b0020001-0000-0000-0000-000000000003'),
        ('b0020001-0000-0000-0000-000000000005'),
        ('b0020001-0000-0000-0000-000000000001')
    ) AS f(fid)
ON CONFLICT DO NOTHING;

-- Marcus Sterling (cybersecurity / infrastructure): Help, Djehuti, Research, General
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'Marcus Sterling') p,
    (VALUES
        ('b0020001-0000-0000-0000-000000000003'),
        ('b0020001-0000-0000-0000-000000000004'),
        ('c1000001-0000-0000-0000-000000000005'),
        ('c1000001-0000-0000-0000-000000000008'),
        ('c1000001-0000-0000-0000-000000000002')
    ) AS f(fid)
ON CONFLICT DO NOTHING;

-- Mateo Vargas (AI ethics / alignment): ISD, Research, General, Creative
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'Mateo Vargas') p,
    (VALUES
        ('c1000001-0000-0000-0000-000000000003'),
        ('c1000001-0000-0000-0000-000000000004'),
        ('c1000001-0000-0000-0000-000000000007'),
        ('c1000001-0000-0000-0000-000000000008'),
        ('b0020001-0000-0000-0000-000000000005')
    ) AS f(fid)
ON CONFLICT DO NOTHING;

-- Priya Patel (data science / analytics): ISD, Research, Djehuti, General
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'Priya Patel') p,
    (VALUES
        ('c1000001-0000-0000-0000-000000000003'),
        ('c1000001-0000-0000-0000-000000000004'),
        ('c1000001-0000-0000-0000-000000000007'),
        ('c1000001-0000-0000-0000-000000000005'),
        ('c1000001-0000-0000-0000-000000000002')
    ) AS f(fid)
ON CONFLICT DO NOTHING;

-- Sarah Jenkins (UX/UI / front-end): Creative, General, Help, Off Topic
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'Sarah Jenkins') p,
    (VALUES
        ('b0020001-0000-0000-0000-000000000005'),
        ('b0020001-0000-0000-0000-000000000006'),
        ('c1000001-0000-0000-0000-000000000001'),
        ('b0020001-0000-0000-0000-000000000003'),
        ('b0020001-0000-0000-0000-000000000001')
    ) AS f(fid)
ON CONFLICT DO NOTHING;

-- Wei Chen (generative AI / ML): ISD, Research, Djehuti, General
INSERT INTO ai_persona_forums (persona_id, forum_id)
SELECT p.id, f.fid FROM
    (SELECT id FROM ai_personas WHERE name = 'Wei Chen') p,
    (VALUES
        ('c1000001-0000-0000-0000-000000000003'),
        ('c1000001-0000-0000-0000-000000000004'),
        ('c1000001-0000-0000-0000-000000000007'),
        ('c1000001-0000-0000-0000-000000000008'),
        ('c1000001-0000-0000-0000-000000000005')
    ) AS f(fid)
ON CONFLICT DO NOTHING;
