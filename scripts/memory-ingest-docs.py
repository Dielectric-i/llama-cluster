#!/usr/bin/env python3
"""Index the approved slowrig documentation corpus into memory-db."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


CORPUS_NAME = "slowrig-docs-v1"
MODEL_NAME = "Qwen3-Embedding-0.6B-Q8_0.gguf"
MODEL_PROVIDER = "local-llama.cpp"
EMBEDDING_URL = "http://127.0.0.1:4010/v1/embeddings"
MAX_CHARS = 3200
OVERLAP_LINES = 4


def sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def sql_literal(value: str | None) -> str:
    if value is None:
        return "NULL"
    return "'" + value.replace("'", "''") + "'"


def sql_json(value: object) -> str:
    return sql_literal(json.dumps(value, ensure_ascii=False, sort_keys=True)) + "::jsonb"


def vector_literal(values: list[float]) -> str:
    return sql_literal("[" + ",".join(f"{v:.8g}" for v in values) + "]")


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def approved_files(root: Path) -> list[Path]:
    files = [root / "README.md", root / "AGENTS.md"]
    docs_dir = root / "docs"
    if docs_dir.is_dir():
        files.extend(sorted(docs_dir.glob("*.md")))
    return [path for path in files if path.is_file()]


def first_heading(lines: list[str], fallback: str) -> str:
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("#"):
            return stripped.lstrip("#").strip() or fallback
    return fallback


def chunk_markdown(text: str) -> list[dict[str, object]]:
    lines = text.splitlines()
    chunks: list[dict[str, object]] = []
    start = 0
    current: list[str] = []

    for idx, line in enumerate(lines):
        if current and line.startswith("#") and sum(len(item) + 1 for item in current) >= 900:
            chunks.append(
                {
                    "content": "\n".join(current).strip(),
                    "start_line": start + 1,
                    "end_line": idx,
                }
            )
            overlap = current[-OVERLAP_LINES:] if OVERLAP_LINES else []
            start = max(idx - len(overlap), 0)
            current = overlap[:]

        current.append(line)

        if sum(len(item) + 1 for item in current) >= MAX_CHARS:
            chunks.append(
                {
                    "content": "\n".join(current).strip(),
                    "start_line": start + 1,
                    "end_line": idx + 1,
                }
            )
            overlap = current[-OVERLAP_LINES:] if OVERLAP_LINES else []
            start = max(idx + 1 - len(overlap), 0)
            current = overlap[:]

    if current:
        content = "\n".join(current).strip()
        if content:
            chunks.append(
                {
                    "content": content,
                    "start_line": start + 1,
                    "end_line": len(lines),
                }
            )

    return chunks


def embedding(text: str, timeout: int) -> list[float]:
    payload = json.dumps({"model": MODEL_NAME, "input": text}).encode("utf-8")
    request = urllib.request.Request(
        EMBEDDING_URL,
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        data = json.loads(response.read().decode("utf-8"))
    values = data["data"][0]["embedding"]
    if not isinstance(values, list) or not values:
        raise RuntimeError("embedding response did not contain a non-empty vector")
    return [float(value) for value in values]


def run_psql(sql: str) -> None:
    command = [
        "docker",
        "compose",
        "exec",
        "-T",
        "memory-db",
        "sh",
        "-lc",
        'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1',
    ]
    completed = subprocess.run(
        command,
        input=sql,
        text=True,
        encoding="utf-8",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        if completed.stdout:
            print(completed.stdout, file=sys.stderr)
        if completed.stderr:
            print(completed.stderr, file=sys.stderr)
        raise RuntimeError("psql failed")


def start_run(metadata: dict[str, object]) -> int:
    sql = f"""
INSERT INTO memory.ingestion_runs (corpus_name, status, metadata)
VALUES ({sql_literal(CORPUS_NAME)}, 'running', {sql_json(metadata)})
RETURNING id;
"""
    command = [
        "docker",
        "compose",
        "exec",
        "-T",
        "memory-db",
        "sh",
        "-lc",
        'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -tA',
    ]
    completed = subprocess.run(
        command,
        input=sql,
        text=True,
        encoding="utf-8",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(completed.stderr.strip() or "failed to create ingestion run")
    for line in completed.stdout.splitlines():
        stripped = line.strip()
        if stripped.isdigit():
            return int(stripped)
    raise RuntimeError(f"failed to parse ingestion run id: {completed.stdout.strip()}")


def finish_run(run_id: int, status: str, metadata: dict[str, object]) -> None:
    sql = f"""
UPDATE memory.ingestion_runs
SET status = {sql_literal(status)},
    finished_at = now(),
    metadata = metadata || {sql_json(metadata)}
WHERE id = {run_id};
"""
    run_psql(sql)


def build_sql(root: Path, run_id: int, timeout: int) -> tuple[str, dict[str, object]]:
    statements = [
        "BEGIN;",
        f"""
INSERT INTO memory.embedding_models (model_name, provider, dimensions, metadata)
VALUES ({sql_literal(MODEL_NAME)}, {sql_literal(MODEL_PROVIDER)}, 1024, {sql_json({"runtime": "llama.cpp", "service": "memory-embed"})})
ON CONFLICT (model_name, dimensions) DO UPDATE
SET metadata = EXCLUDED.metadata
RETURNING id;
""",
    ]
    total_chunks = 0
    total_documents = 0
    dimensions: int | None = None

    for path in approved_files(root):
        rel_path = path.relative_to(root).as_posix()
        text = path.read_text(encoding="utf-8")
        lines = text.splitlines()
        doc_sha = sha256_text(text)
        title = first_heading(lines, rel_path)
        chunks = chunk_markdown(text)
        total_documents += 1

        doc_metadata = {
            "corpus": CORPUS_NAME,
            "ingestion_run_id": run_id,
            "source": "git-worktree",
            "line_count": len(lines),
        }
        statements.append(
            f"""
INSERT INTO memory.documents (source_path, source_type, source_sha256, title, metadata, updated_at)
VALUES ({sql_literal(rel_path)}, 'markdown', {sql_literal(doc_sha)}, {sql_literal(title)}, {sql_json(doc_metadata)}, now())
ON CONFLICT (source_path) DO UPDATE
SET source_sha256 = EXCLUDED.source_sha256,
    title = EXCLUDED.title,
    metadata = EXCLUDED.metadata,
    updated_at = now();
DELETE FROM memory.document_chunks
WHERE document_id = (SELECT id FROM memory.documents WHERE source_path = {sql_literal(rel_path)});
"""
        )

        for index, chunk in enumerate(chunks):
            content = str(chunk["content"])
            if not content:
                continue
            vector = embedding(content, timeout=timeout)
            if dimensions is None:
                dimensions = len(vector)
            if len(vector) != dimensions:
                raise RuntimeError(f"embedding dimension changed for {rel_path}")

            chunk_metadata = {
                "corpus": CORPUS_NAME,
                "ingestion_run_id": run_id,
                "source_sha256": doc_sha,
                "chunk_sha256": sha256_text(content),
                "script": "scripts/memory-ingest-docs.py",
            }
            start_line = int(chunk["start_line"])
            end_line = int(chunk["end_line"])
            statements.append(
                f"""
WITH inserted_chunk AS (
  INSERT INTO memory.document_chunks (
    document_id,
    chunk_index,
    content,
    content_sha256,
    start_line,
    end_line,
    metadata
  )
  VALUES (
    (SELECT id FROM memory.documents WHERE source_path = {sql_literal(rel_path)}),
    {index},
    {sql_literal(content)},
    {sql_literal(sha256_text(content))},
    {start_line},
    {end_line},
    {sql_json(chunk_metadata)}
  )
  RETURNING id
)
INSERT INTO memory.embeddings (chunk_id, embedding_model_id, embedding, metadata)
VALUES (
  (SELECT id FROM inserted_chunk),
  (SELECT id FROM memory.embedding_models WHERE model_name = {sql_literal(MODEL_NAME)} AND dimensions = 1024),
  {vector_literal(vector)}::vector,
  {sql_json({"corpus": CORPUS_NAME, "ingestion_run_id": run_id, "source_path": rel_path})}
);
"""
            )
            total_chunks += 1
            print(f"embedded {rel_path} chunk {index + 1}/{len(chunks)}")

    statements.append("COMMIT;")
    summary = {
        "documents": total_documents,
        "chunks": total_chunks,
        "dimensions": dimensions,
    }
    return "\n".join(statements), summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--timeout", type=int, default=120, help="embedding HTTP timeout in seconds")
    args = parser.parse_args()

    root = repo_root()
    metadata = {
        "script": "scripts/memory-ingest-docs.py",
        "corpus": CORPUS_NAME,
        "model": MODEL_NAME,
        "started_unix": int(time.time()),
    }
    run_id = start_run(metadata)
    try:
        sql, summary = build_sql(root, run_id=run_id, timeout=args.timeout)
        if summary["dimensions"] != 1024:
            raise RuntimeError(f"expected 1024 dimensions, got {summary['dimensions']}")
        run_psql(sql)
        finish_run(run_id, "completed", summary)
        print(json.dumps({"run_id": run_id, **summary}, ensure_ascii=False, sort_keys=True))
        return 0
    except (RuntimeError, urllib.error.URLError, subprocess.SubprocessError) as exc:
        finish_run(run_id, "failed", {"error": str(exc)})
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
