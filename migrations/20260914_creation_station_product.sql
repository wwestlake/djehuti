\if :{?webhook_secret}
INSERT INTO products (
    slug,
    name,
    description,
    required_tier_id,
    active,
    github_owner,
    github_repo,
    github_webhook_secret,
    github_tag_prefix
)
VALUES (
    'creation-station',
    'Djehuti Station',
    'The Creation Suite audio creative workstation -- multi-track recording, VST3 plugin hosting, MIDI control surfaces, and FRust-powered sound design.',
    NULL,
    TRUE,
    'wwestlake',
    'CreationStation',
    :'webhook_secret',
    'creation-station-v'
)
ON CONFLICT (slug) DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    active = EXCLUDED.active,
    github_owner = EXCLUDED.github_owner,
    github_repo = EXCLUDED.github_repo,
    github_webhook_secret = EXCLUDED.github_webhook_secret,
    github_tag_prefix = EXCLUDED.github_tag_prefix;
\else
\echo 'webhook_secret is required'
\quit
\endif
