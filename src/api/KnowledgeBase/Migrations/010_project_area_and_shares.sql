-- Idempotent Knowledge DB patch: project Area + Entra shares.
-- Applied on API startup after etc-kg SQL migrations.

DO $$
BEGIN
  IF to_regclass('public.kb_projects') IS NOT NULL THEN
    ALTER TABLE kb_projects ADD COLUMN IF NOT EXISTS area varchar(80);
  END IF;
END $$;

CREATE TABLE IF NOT EXISTS kb_project_shares (
    id uuid PRIMARY KEY,
    project_id uuid NOT NULL REFERENCES kb_projects(id) ON DELETE CASCADE,
    principal_type varchar(16) NOT NULL,
    principal_oid varchar(64) NOT NULL,
    principal_display_name varchar(256) NOT NULL,
    principal_email varchar(256),
    role varchar(16) NOT NULL,
    created_at timestamptz NOT NULL,
    created_by_oid varchar(64) NOT NULL,
    CONSTRAINT ux_kb_project_shares_principal UNIQUE (project_id, principal_type, principal_oid)
);

CREATE INDEX IF NOT EXISTS ix_kb_project_shares_principal
    ON kb_project_shares (principal_type, principal_oid);
