CREATE EXTENSION IF NOT EXISTS vector;

CREATE SCHEMA IF NOT EXISTS memory;

CREATE TABLE IF NOT EXISTS memory.documents (
  id bigserial PRIMARY KEY,
  source_path text NOT NULL UNIQUE,
  source_type text NOT NULL DEFAULT 'markdown',
  source_sha256 text,
  title text,
  metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS memory.document_chunks (
  id bigserial PRIMARY KEY,
  document_id bigint NOT NULL REFERENCES memory.documents(id) ON DELETE CASCADE,
  chunk_index integer NOT NULL,
  content text NOT NULL,
  content_sha256 text,
  start_line integer,
  end_line integer,
  metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (document_id, chunk_index)
);

CREATE TABLE IF NOT EXISTS memory.embedding_models (
  id bigserial PRIMARY KEY,
  model_name text NOT NULL,
  provider text NOT NULL DEFAULT 'local',
  dimensions integer NOT NULL CHECK (dimensions > 0),
  metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (model_name, dimensions)
);

CREATE TABLE IF NOT EXISTS memory.embeddings (
  id bigserial PRIMARY KEY,
  chunk_id bigint NOT NULL REFERENCES memory.document_chunks(id) ON DELETE CASCADE,
  embedding_model_id bigint NOT NULL REFERENCES memory.embedding_models(id) ON DELETE RESTRICT,
  embedding vector,
  metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (chunk_id, embedding_model_id)
);

CREATE TABLE IF NOT EXISTS memory.ingestion_runs (
  id bigserial PRIMARY KEY,
  corpus_name text NOT NULL,
  status text NOT NULL CHECK (status IN ('pending', 'running', 'completed', 'failed')),
  started_at timestamptz NOT NULL DEFAULT now(),
  finished_at timestamptz,
  metadata jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS memory.memory_tasks (
  id bigserial PRIMARY KEY,
  title text NOT NULL,
  status text NOT NULL DEFAULT 'open',
  metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS memory.task_events (
  id bigserial PRIMARY KEY,
  task_id bigint NOT NULL REFERENCES memory.memory_tasks(id) ON DELETE CASCADE,
  event_type text NOT NULL,
  payload jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS memory.access_rules (
  id bigserial PRIMARY KEY,
  subject text NOT NULL,
  scope text NOT NULL,
  rule jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (subject, scope)
);

CREATE TABLE IF NOT EXISTS memory.audit_log (
  id bigserial PRIMARY KEY,
  actor text NOT NULL,
  action text NOT NULL,
  target text,
  payload jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_document_chunks_document_id
  ON memory.document_chunks(document_id);

CREATE INDEX IF NOT EXISTS idx_embeddings_chunk_model
  ON memory.embeddings(chunk_id, embedding_model_id);

CREATE INDEX IF NOT EXISTS idx_ingestion_runs_status
  ON memory.ingestion_runs(status);

CREATE INDEX IF NOT EXISTS idx_task_events_task_id
  ON memory.task_events(task_id);

CREATE INDEX IF NOT EXISTS idx_audit_log_created_at
  ON memory.audit_log(created_at);
