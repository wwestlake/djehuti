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
    'creation-developer',
    'Creation Developer',
    'The Creation Suite development environment for FRust projects and Suite plugin authoring.',
    NULL,
    TRUE,
    'wwestlake',
    'Creation-Developer',
    :'webhook_secret',
    'creation-developer-v'
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
