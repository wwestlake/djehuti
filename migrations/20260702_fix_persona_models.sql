-- Repair invalid persona model values and revive forum jobs that failed only because of the bad model name.

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
