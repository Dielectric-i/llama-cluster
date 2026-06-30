# slowrig AI Cluster — Roadmap v0.3

Дата актуализации: 2026‑07‑01
Статус: **главный план действий** (docs‑as‑source‑of‑truth). Этот файл описывает _следующие_ этапы и критический путь. Завершённые стадии, подробные факты стенда, история изменений и ADR живут в других документах (см. §2).

---

## 1. Назначение документа

`docs/roadmap.md` фиксирует **порядок будущих этапов**, их цели, зависимости и открытые развилки. Он **не** заменяет паспорт стенда, runbook, changelog, audit‑reports или branch workflow.

---

## 2. Source‑of‑truth boundaries

| Тема | Главный источник |
| --- | --- |
| Индекс документации | `README.md` |
| Фактическое состояние стенда | `docs/passport.md` |
| Эксплуатация и rollback | `docs/runbook.md` |
| Текущая и целевая архитектура | `docs/architecture.md` |
| Gateway baseline | `docs/gateway.md` |
| Memory / RAG | `docs/memory.md` |
| Telegram bot | `docs/telegram.md` |
| Agent framework | `docs/agent-framework.md` |
| Monitoring | `docs/monitoring.md` |
| Security | `docs/security.md` |
| Backups | `docs/backups.md` |
| ADR / причины решений | `docs/decisions.md` |
| История изменений | `docs/changelog.md` |
| План действий | `docs/roadmap.md` (этот файл) |

---

## 3. Текущий baseline (2026‑07‑01)

* Home server `slowrig`, Docker Compose‑based.
* Логическая цепочка:
  ```text
  Open WebUI (manual UI) → LiteLLM Gateway :4000 → llama‑coder (9B/fast, ctx 128k) :8081
                                               ↘ llama‑architect (27B/deep, ctx 65000) :8080
  ```
* Сервисы в compose: `llama‑architect`, `llama‑coder`, `litellm`, `open-webui`, `memory-db`, `memory-embed`, `telegram-bot`, `ide-proxy` (IDE SSE heartbeat experiment).
* Memory DB (`postgresql+pgvector`) и локальный embedding service (`memory-embed`) существуют; индексирован только проектный docs‑корпус.
* Telegram bot — отдельный контейнер, всегда запускается, **без** profile.
* Direct backend порт 8080/8081 оставлены для диагностики в LAN.
* Multi‑node кластер — дальняя опция, не план ближайших стадий.

---

## 4. Стратегическая цель

> **Autonomous Development Agent** — контролируемый агент, способный готовить патчи, отчёты и выполнять согласованные действия, используя Gateway, Memory/RAG и безопасные tools с audit trail и rollback.

Open WebUI остаётся ручным интерфейсом, Telegram — отдельным пользовательским интерфейсом, `ide-proxy` — эксперимент для VS Code/Copilot, **не** осевая архитектура. Никаких новых runtime‑зависимостей без отдельного design/approval stage.

---

## 5. Критический путь

```text
Stage 8  Docs alignment  →  Stage 9  RAG Retrieval v1 (Telegram)  →
Stage 10 Agent Runtime design  →  Stage 11 Agent Runtime MVP  →
Stage 12 Tool permissions/Approval v1  →  Stage 13 MCP decision  →
Stage 14 Telegram→Agent escalation  →  Stage 15 Ops hardening continuation
```

---

## 6. Текущий docs‑only stage

### Stage 8 — Roadmap / documentation alignment  *(docs‑only)*

| Поле | Значение |
| --- | --- |
| **Цель** | Сделать этот файл главным планом, удалить устаревшие ссылки, зафиксировать `ctx‑size 65000` для llama‑architect, указать telegram‑bot как always‑on сервис, отметить `ide-proxy` как эксперимент. |
| **Scope** | Только правки Markdown / docs. |
| **Non‑goals** | Нет runtime изменений, новых сервисов, миграций или рестартов. |
| **Artifacts** | Обновлённые `docs/roadmap.md`, README, ссылки в docs, удалены мёртвые ссылки `docs/stage*-summary.md`, `docs/agents.md`. |
| **Validation** | `git diff --check`, `scripts/docs-drift-agent.sh`, ручной просмотр. |
| **Rollback** | `git checkout -- docs/roadmap.md README.md docs/`. |

---

## 7. Ближайшие этапы

### Stage 9 — RAG Retrieval v1 для Telegram *(runtime)*

* **Goal:** read‑only Retrieval API или модуль поверх `memory-db`, доступный Telegram‑боту (`/rag` или auto‑context).
* **Depends on:** Stage 8 docs alignment.
* **Non‑goals:** RAG hook для Open WebUI, chat history ingestion, Memory writes.
* **Artifacts:** update `docs/memory.md`, `docs/telegram.md`, new retrieval code/tests.

### Stage 10 — Agent Runtime design *(docs‑only)*

* Shape & sandbox model, task state, approval flow, audit trail.
* Decide if lightweight script/service is enough; no code.

### Stage 11 — Agent Runtime minimal implementation *(runtime)*

* One safe workflow: docs drift / patch assistant.
* Isolated worktree, patch output, human approval gate.

### Stage 12 — Tool permissions / approval v1 *(docs + optional small script)*

* Diagnostics allowlist, dangerous action classes, approval format, audit log schema.

### Stage 13 — MCP design decision *(docs‑only)*

* Do we need MCP? boundaries, adapter vs server, interaction with tools.

### Stage 14 — Telegram→Agent escalation *(runtime)*

* Command or flow to trigger agent task from Telegram, keeping no‑shell boundary.

### Stage 15 — Ops hardening continuation *(runtime + docs)*

* Health‑lite automation decision (cron/timer), backup restore dry‑run, auth/policy hardening, direct ports decision, monitoring stack plan.

---

## 8. Long‑range options (отложены)

* Multi‑node cluster & orchestrator.
* Plugin System / extensible tools registry.
* External UI integrations beyond Telegram & WebUI.

---

## 9. Открытые архитектурные развилки

* **Agent Runtime** – runtime shape, task state storage, sandbox strategy.
* **MCP** – нужен ли, если да – форма и безопасность.
* **Plugin System** – когда понадобятся расширяемые tools / personas / RAG sources.
* **RAG retrieval format** – DB query vs HTTP layer, provenance delivery.
* **Security hardening** – VPN vs reverse proxy, direct backend ports, auth for WebUI.

---

## 10. Правила изменения roadmap

1. Изменяй roadmap только после решения/approval.
2. Завершённые стадии переносятся в `docs/changelog.md`; roadmap хранит **только будущее**.
3. Не добавляй новые runtime‑зависимости здесь без отдельного design‑stage.
4. Крупный форк? — сначала ADR + design doc, потом обновление roadmap.
