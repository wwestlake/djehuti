CREATE TABLE IF NOT EXISTS frate_pods (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name         TEXT NOT NULL UNIQUE,
    description  TEXT,
    owner_id     UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_frate_pods_name_lower ON frate_pods (lower(name));

CREATE TABLE IF NOT EXISTS frate_pod_versions (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    pod_id            UUID NOT NULL REFERENCES frate_pods(id) ON DELETE CASCADE,
    version           TEXT NOT NULL,
    description       TEXT,
    exports_json      TEXT NOT NULL DEFAULT '[]',
    dependencies_json TEXT NOT NULL DEFAULT '[]',
    license           TEXT NOT NULL,
    s3_key            TEXT NOT NULL,
    size_bytes        BIGINT NOT NULL,
    publisher_id      UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (pod_id, version)
);
CREATE INDEX IF NOT EXISTS idx_frate_pod_versions_pod ON frate_pod_versions (pod_id, created_at DESC);

GRANT ALL ON TABLE frate_pods TO djehuti;
GRANT ALL ON TABLE frate_pod_versions TO djehuti;
