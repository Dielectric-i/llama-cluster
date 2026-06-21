# Codex Context — slowrig AI Cluster

Created: 2026-06-21  
Last updated: 2026-06-22  
Status: supplemental context for Codex

## 1. Purpose

This document provides additional project context for Codex.

It is not the primary source of truth for hardware, ports, services, commands, architecture decisions, changelog entries, or operational procedures. Those details live in the dedicated project documents.

Use this file to understand:

* how Александр wants the project to be developed;
* what assumptions are not fully captured in formal docs yet;
* which future design forks should be discussed before implementation;
* how to avoid turning `slowrig` into an unmaintainable pile of services;
* what context should guide Stage 4+ work.

This file should stay short, contextual, and non-duplicative.

---

## 2. Source-of-truth boundaries

Do not copy large factual sections from other documents into this file.

Use these documents as the authoritative sources:

| Topic | Primary source |
| --- | --- |
| Documentation index and quick entry | `README.md` |
| Codex operating rules | `AGENTS.md` |
| Current hardware/software facts | `docs/passport.md` |
| Operations, diagnostics, rollback | `docs/runbook.md` |
| Current and target architecture | `docs/architecture.md` |
| LiteLLM Gateway design/baseline | `docs/gateway.md` |
| Architectural reasons and trade-offs | `docs/decisions.md` |
| Factual change history and tests | `docs/changelog.md` |
| Completed stage summaries | `docs/stage*-summary.md` |

If this file conflicts with a dedicated document, prefer the dedicated document.

If documentation conflicts with actual config or server output, do not guess silently. Report the mismatch and ask the user which state is correct.

---

## 3. User and operator context

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

Treat Александр as the infrastructure operator, not as a passive recipient of generated files.

Preferred working style:

* step-by-step;
* conservative changes;
* explain why a change is needed;
* warn about risks before risky actions;
* provide alternatives and trade-offs for architectural decisions;
* keep changes small and reviewable;
* stop after each stage;
* wait for real server/UI checks when needed;
* do not claim something is tested unless Александр confirms it or provides output.

User-facing communication must be in Russian. Technical identifiers must keep their exact spelling.

Examples:

```text
slowrig/coder
slowrig/architect
llama-coder
llama-architect
litellm
open-webui
LITELLM_MASTER_KEY
docker-compose.yaml
```

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

The project should remain practical and maintainable.

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

## 5. Current development posture

Current baseline is already beyond simple inference:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

The next major work should not start by installing more services.

The preferred next stage is:

```text
Stage 4.1 — Memory / RAG design
```

Expected first artifact:

```text
docs/memory.md
```

Stage 4 should begin with design only.

Do not add a database, vector store, embedding service, Telegram bot, agent framework, or monitoring stack before the relevant design document exists and Александр approves the direction.

---

## 6. Stage 4 planning assumptions

Memory/RAG is the next important subsystem because future Telegram and agent workflows need durable context.

Current non-final preference:

```text
PostgreSQL + pgvector as the first serious memory layer
```

Reasoning:

* one database can store structured state and vector data;
* future agents will need task state, runs, approvals, and history;
* Telegram will need conversation metadata and access control;
* project decisions and changelog entries can be indexed later;
* one operational database is simpler than a database plus separate vector store at the beginning.

This is not a final decision.

Before implementation, Codex should compare at least:

* Markdown + Git only;
* PostgreSQL + pgvector;
* Qdrant with separate metadata storage;
* hybrid PostgreSQL + Qdrant.

The design should clearly separate:

* source of truth;
* searchable index;
* structured task state;
* conversation history;
* embeddings;
* backups;
* privacy boundaries.

---

## 7. Future subsystem forks

Codex must not decide these unilaterally.

### 7.1 Memory / RAG

Open questions:

* What should remain only in Markdown/Git?
* What should be stored in a database?
* What should be embedded?
* How should old docs, logs, chats, and decisions be indexed?
* How will backup and restore work?
* How will memory connect to future agents?

### 7.2 Telegram bot

Open questions:

* polling or webhook;
* simple chat bot or command bot;
* model selection by command or default routing;
* whitelist format;
* where conversation history is stored;
* whether Telegram should be added before or after memory.

Default assumption:

```text
Telegram should use LiteLLM Gateway and should not access shell/Docker directly.
```

### 7.3 Agent layer

Open questions:

* CrewAI;
* OpenClaw;
* custom lightweight orchestrator;
* Codex-driven workflow only.

Default assumption:

```text
Do not install a full agent framework before docs/agents.md exists.
```

Agent work must preserve human approval for risky actions.

### 7.4 Monitoring

Open questions:

* scripts only;
* lightweight cron checks;
* Prometheus/Grafana;
* Loki/log aggregation;
* GPU metrics;
* alerting.

Important distinction:

```text
scripts/cluster-status.sh       -> deep manual diagnostic
future cluster-health-lite.sh   -> cheap frequent health check
```

Do not turn deep LLM-generating checks into frequent automated health checks.

### 7.5 Backups and security

Open questions:

* how to back up docs/configs;
* how to back up future memory DB;
* whether to back up Open WebUI data;
* how to handle model backups;
* when to enable auth;
* whether remote access should use VPN, reverse proxy, or another approach.

Default assumption:

```text
LAN/VPN first, auth before external exposure, no secrets in git.
```

---

## 8. Branch and stage workflow

For non-trivial stages, use a stage branch instead of working directly on `main`.

Preferred branch naming:

```text
codex/<stage-or-task-name>
```

Examples:

```text
codex/stage-4-memory-design
codex/telegram-design
codex/gateway-policy
codex/runbook-cleanup
```

Expected workflow:

1. create or switch to a `codex/...` branch;
2. make the smallest coherent change;
3. update affected documentation;
4. provide test commands and rollback;
5. wait for Александр to run checks or review;
6. merge to `main` only after approval.

Do not merge automatically unless Александр explicitly asks.

---

## 9. Context preparation before work

Before non-trivial work, Codex should read:

```text
README.md
AGENTS.md
docs/codex-context.md
```

Then read task-specific docs.

Examples:

| Task | Read additionally |
| --- | --- |
| memory/RAG | `docs/architecture.md`, `docs/decisions.md`, future `docs/memory.md` |
| gateway/routing | `docs/gateway.md`, `docs/runbook.md`, `docs/changelog.md` |
| operational command | `docs/runbook.md` |
| new service | `docs/passport.md`, `docs/architecture.md`, `docs/runbook.md`, `docs/changelog.md` |
| architectural decision | `docs/decisions.md` |
| completed stage context | relevant `docs/stage*-summary.md` |

If a required document does not exist and the task introduces a subsystem, start by creating the design document.

---

## 10. What belongs in this file

Keep:

* user-specific working preferences;
* project-level assumptions not yet formalized elsewhere;
* future forks that require discussion;
* high-level priorities;
* reminders about stage order;
* context that helps Codex avoid bad architectural moves.

Do not keep:

* full service tables;
* port lists;
* model filenames;
* GPU mapping tables;
* detailed runbook commands;
* curl examples;
* full gateway configuration;
* ADR copies;
* changelog entries;
* secrets;
* personal contact details;
* large duplicated documentation indexes.

If information becomes important enough to operate, debug, or roll back a subsystem, move it into a dedicated document under `docs/`.

---

## 11. Privacy and data handling notes

Do not store or reproduce unnecessary personal details.

Do not add emails, tokens, private handles, exact secrets, or private credentials to documentation.

Do not ask Александр to paste `.env`.

When future memory/RAG is designed, explicitly decide:

* what user conversations may be stored;
* what should be excluded;
* how long data should be kept;
* how to delete data;
* what is safe to embed;
* what must remain only in local files;
* what must never be sent to external APIs.

---

## 12. How to update this file

Update this file only when there is new supplemental context that does not fit better elsewhere.

Before adding content, ask:

```text
Is this already covered by README, AGENTS, passport, runbook, architecture, gateway, decisions, changelog, or a stage summary?
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