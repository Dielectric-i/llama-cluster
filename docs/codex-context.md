# Codex Context — slowrig AI Cluster

Created: 2026-06-21
Last updated: 2026-06-22
Status: supplemental context for Codex

## 1. Purpose

This document provides compact project context for Codex.

It is not the source of truth for hardware, ports, services, commands, architecture decisions, changelog entries, or operational procedures. Those details live in the dedicated project documents.

Use this file to understand:

* how Александр wants the project to be developed;
* which defaults and trade-offs were already approved;
* which future forks still require discussion;
* how to avoid turning `slowrig` into an unmaintainable pile of services;
* what context should guide Stage 4+ work.

This file should stay short, contextual, and non-duplicative.

---

## 2. Source-of-truth boundaries

Use these documents as authoritative sources:

| Topic | Primary source |
| --- | --- |
| Documentation index and quick entry | `README.md` |
| Codex operating rules | `AGENTS.md` |
| Current hardware/software facts | `docs/passport.md` |
| Operations, diagnostics, rollback | `docs/runbook.md` |
| Current and target architecture | `docs/architecture.md` |
| LiteLLM Gateway design/baseline | `docs/gateway.md` |
| Memory / RAG design | `docs/memory.md` |
| Architectural reasons and trade-offs | `docs/decisions.md` |
| Factual change history and tests | `docs/changelog.md` |
| Completed stage summaries | `docs/stage*-summary.md` |

If this file conflicts with a dedicated document, prefer the dedicated document.

If documentation conflicts with actual config or server output, do not guess silently. Report the mismatch and ask Александр which state is correct.

---

## 3. Operator and workflow preferences

The primary operator is Александр.

Александр runs the real server commands himself:

* Docker Compose operations;
* service restarts;
* log inspection;
* browser UI checks;
* network/API checks;
* manual approval of architectural forks.

Codex prepares:

* plans;
* patches;
* documentation;
* test commands;
* expected results;
* rollback steps;
* stage reports.

User-facing communication must be in Russian. Technical identifiers must keep their exact spelling.

Important working preferences:

* explain architectural forks in detail before asking for a decision;
* for each fork, describe options and how each option affects the final result and process;
* stop only when an architectural decision, problem, real-server check, security/runtime risk, or explicit user request requires it;
* do not stop after every small progress update;
* when Александр gives a durable behavior rule, preserve it in `AGENTS.md`;
* keep changes small, staged, documented, and reversible.

---

## 4. Project intent

`slowrig` is not only a local inference box.

The intended long-term direction is a local AI system for:

* coding assistance;
* long code and log audits;
* local project analysis;
* documentation work;
* controlled agent workflows;
* Telegram/IDE/API access;
* project memory and RAG;
* repeatable operations with rollback.

Preferred engineering bias:

```text
stability > number of features
clarity > clever automation
small stages > large rewrites
manual verification > assumed success
documented behavior > hidden state
rollback path > one-way migration
```

---

## 5. Current baseline

Current baseline:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

Current gateway model names:

```text
slowrig/coder
slowrig/architect
```

Direct backend ports `8080` and `8081` remain available in LAN for diagnostics until a separate security hardening stage decides otherwise.

Future ordinary clients should use LiteLLM Gateway, not direct backend ports.

---

## 6. Approved roadmap defaults

The approved high-level order is:

```text
Stage 4.2 — Memory implementation plan (done)
Stage 4.3 — Memory DB foundation (done and server-validated)
Stage 4.4 — Local RAG ingestion (done and server-validated)
Stage 5   — Telegram bot
Stage 6   — Agents
Stage 7   — Monitoring / Security / Backups
```

No new runtime dependencies should be installed before the relevant design/implementation plan is approved.

---

## 7. Memory / RAG decisions

Memory / RAG is the next major subsystem.

Approved direction:

```text
PostgreSQL + pgvector
```

Meaning:

* Stage 4.2 prepared the implementation plan around PostgreSQL + pgvector;
* Stage 4.3 added and validated `memory-db`;
* Stage 4.4 adds local RAG ingestion after the DB foundation exists;
* embeddings are local through `memory-embed`;
* first indexed corpus is only `README.md`, `AGENTS.md`, and `docs/*.md`;
* chats, raw logs, secrets, Open WebUI history, and Telegram history are not indexed in the first RAG corpus.

Source-of-truth rule:

```text
Markdown + Git remain source of truth.
PostgreSQL + pgvector stores structured state, metadata, chunks, and derived vector data.
```

Stage 4.3 DB foundation defaults:

* Compose service: `memory-db`;
* image: `pgvector/pgvector:0.8.3-pg17`;
* volume: `memory-db-data`;
* DB network exposure: Docker Compose network only, no host port by default;
* env names: `MEMORY_POSTGRES_DB`, `MEMORY_POSTGRES_USER`, `MEMORY_POSTGRES_PASSWORD`;
* bootstrap SQL: `config/memory/init/001-memory-foundation.sql`;
* backup dumps path: `backups/`, ignored by git.

Stage 4.4 ingestion defaults:

* Compose service: `memory-embed`;
* runtime: `llama.cpp server`, CPU-only;
* model: `Qwen3-Embedding-0.6B-Q8_0.gguf`;
* model path: `/opt/llama-cluster/models/embeddings/Qwen3-Embedding-0.6B-Q8_0.gguf`;
* endpoint: `127.0.0.1:4010`, not LAN/public;
* ingestion script: `scripts/memory-ingest-docs.py`;
* chunks and embeddings are rebuildable derived data.

---

## 8. Telegram decisions

Approved first Telegram shape:

```text
Telegram bot via polling + whitelist -> LiteLLM Gateway
```

Defaults:

* no webhook in the first Telegram stage;
* no public inbound port for Telegram;
* default model is `slowrig/coder`;
* `slowrig/architect` is used only by explicit command or clearly defined escalation;
* Telegram has no shell/Docker access;
* Telegram history is not stored in Memory/RAG until a dedicated decision defines privacy, retention, deletion, and backup rules.

First artifact:

```text
docs/telegram.md
```

---

## 9. Agents decisions

Approved first agents direction:

```text
custom lightweight orchestration / Codex-driven workflow
```

Do not install CrewAI, OpenClaw, or another full agent framework before `docs/agents.md` exists and Александр approves it.

Agent permission ladder:

1. read/report;
2. patches/reports;
3. predefined diagnostics allowlist;
4. approved mutations;
5. sandbox/worktree autonomy.

Dangerous real-infrastructure actions still require approval:

* Docker restart/down;
* compose/config mutation;
* `.env` or secrets changes;
* model/GPU/context/parallel changes;
* volume/cache deletion;
* firewall/reverse proxy/VPN changes;
* package installation;
* image pulls.

First artifact:

```text
docs/agents.md
```

---

## 10. Monitoring, security, and backups decisions

Monitoring direction:

```text
future cluster-health-lite.sh -> cheap frequent health check
scripts/cluster-status.sh    -> deep manual diagnostic
```

Do not turn deep LLM-generating checks into frequent automated health checks.

Security/access direction:

```text
LAN/VPN first
no public WebUI/Gateway exposure before security hardening
direct ports 8080/8081 remain in LAN for diagnostics until a security stage decides otherwise
```

Backup scope for first stateful stages:

* git-backed docs/config/scripts;
* `.env` stored separately offline, never in git;
* PostgreSQL dumps after DB implementation;
* model files are documented by filename/source, but not backed up in the first backup scope;
* Open WebUI data is not included until a dedicated backup decision includes it.

Future artifacts:

```text
docs/monitoring.md
docs/security.md
docs/backups.md
```

---

## 11. Gateway and routing defaults

Current public model names remain:

```text
slowrig/coder
slowrig/architect
```

Do not add aliases such as `slowrig/default`, `slowrig/fast`, `slowrig/deep`, `slowrig/telegram`, or task-specific model names until a client stage needs them and the decision is documented.

Routing by task complexity and `9B -> 27B` pipelines should live in a future agent/router layer, not in LiteLLM by default.

---

## 12. Remaining forks

Remaining forks should be discussed only when they become relevant to the next implementation plan.

Known future forks:

* retrieval API shape over `memory-db`;
* prompt/context assembly format for future clients;
* PostgreSQL schema details;
* backup encryption and restore rehearsal details;
* Telegram command surface;
* agent diagnostics allowlist;
* when to restrict or close direct backend ports;
* whether Qdrant is needed later if pgvector becomes insufficient;
* whether Open WebUI data should be backed up or indexed.

On each fork, Codex must explain options and consequences before asking Александр to choose.

---

## 13. How to update this file

Update this file only when there is new supplemental context that does not fit better elsewhere.

Before adding content, ask:

```text
Is this already covered by README, AGENTS, passport, runbook, architecture, gateway, memory, decisions, changelog, or a stage summary?
```

If yes, do not duplicate it here.

If no, add it briefly.

When a new subsystem becomes real, prefer creating or updating its own document:

```text
docs/memory.md
docs/telegram.md
docs/agents.md
docs/monitoring.md
docs/security.md
docs/backups.md
```

This file should remain a compact orientation layer for Codex, not a second README.
