# slowrig AI Cluster — Roadmap v0.4

Дата актуализации: 2026-07-12
Статус: главный план будущих stages

## 1. Назначение

`docs/roadmap.md` фиксирует порядок будущих stages, зависимости и открытые развилки. Завершённые изменения живут в `docs/changelog.md`, причины решений — в `docs/decisions.md`, фактическое состояние стенда — в `docs/passport.md`.

Roadmap не заменяет `README.md`: полный индекс документации находится в `README.md`.

---

## 2. Источники истины

| Тема | Главный источник |
| --- | --- |
| Индекс документации | `README.md` |
| Фактическое состояние стенда | `docs/passport.md` |
| Эксплуатация и rollback | `docs/runbook.md` |
| Текущая и целевая архитектура | `docs/architecture.md` |
| Gateway baseline | `docs/gateway.md` |
| Memory/RAG | `docs/memory.md` |
| Telegram bot | `docs/telegram.md` |
| Agent layer | `docs/agent-framework.md` |
| Monitoring | `docs/monitoring.md` |
| Security | `docs/security.md` |
| Backups | `docs/backups.md` |
| IDE proxy | `docs/ide-proxy.md` |
| ADR | `docs/decisions.md` |
| История изменений | `docs/changelog.md` |

---

## 3. Текущий baseline

Текущее состояние на начало Stage 8:

* `slowrig` — single-node home server под Docker Compose.
* Основной LLM path:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

* IDE path:

```text
VS Code / Copilot -> ide-proxy -> LiteLLM Gateway -> llama-coder / llama-architect
```

* Telegram path:

```text
Telegram -> telegram-bot -> LiteLLM Gateway -> slowrig/coder by default
```

* Compose services: `llama-architect`, `llama-coder`, `litellm`, `open-webui`, `memory-db`, `memory-embed`, `telegram-bot`, `ide-proxy`.
* `telegram-bot` запускается как обычный Compose service без `profile`.
* `ide-proxy` — экспериментальный SSE heartbeat proxy для VS Code/Copilot, не центральный архитектурный слой.
* Memory DB и local docs ingestion внедрены; retrieval API для клиентов ещё нет.
* Direct backend ports `8080` и `8081` остаются LAN diagnostics/rollback path.

---

## 4. Стратегическая цель

Цель ближайших stages — controlled local AI system:

```text
Gateway + Memory/RAG + Telegram/IDE interfaces + safe agent workflows
```

Долгосрочная цель — контролируемый development agent, который умеет готовить patches, отчёты и ограниченные действия через явно разрешённые tools, audit trail и rollback.

Приоритеты:

```text
стабильность > количество features
понятность > clever automation
human approval > silent autonomy
rollback > one-way migration
```

---

## 5. Критический путь

```text
Stage 8  Docs alignment
Stage 9  RAG Retrieval v1 for Telegram
Stage 10 Agent Runtime design
Stage 11 Agent Runtime MVP
Stage 12 Tool permissions / approval v1
Stage 13 MCP decision
Stage 14 Telegram -> Agent escalation
Stage 15 Ops hardening continuation
```

---

## 6. Текущий stage

### Stage 8 — Docs alignment

| Поле | Значение |
| --- | --- |
| Цель | Сделать `README.md` главным индексом, удалить лишние docs, убрать drift и русифицировать документацию. |
| Scope | Markdown docs и read-only docs drift helper. |
| Не-цели | Runtime changes, новые services, migrations, restarts, image pulls. |
| Артефакты | `README.md`, актуальные `docs/*.md`, `AGENTS.md`, `scripts/docs-drift-agent.sh`; удалены отдельный docs index и дублирующий local README для `ide-proxy`. |
| Проверка | `git diff --check`, локальный link check, drift search, `bash -n scripts/docs-drift-agent.sh` на Linux. |
| Откат | Вернуть affected docs через git; runtime state не затрагивается. |

---

## 7. Ближайшие stages

### Stage 9 — RAG Retrieval v1 для Telegram

Цель: добавить read-only retrieval поверх `memory-db`, сначала доступный Telegram bot, но спроектированный как reusable слой для будущих agents.

Зависит от: Stage 8 docs alignment.

Не-цели:

* RAG hook для Open WebUI;
* chat history ingestion;
* запись Memory из Telegram;
* agent runtime;
* новые внешние APIs.

Ожидаемые артефакты:

* update `docs/memory.md`;
* update `docs/telegram.md`;
* retrieval code/tests;
* validation и rollback в `docs/runbook.md`, если появятся новые команды.

### Stage 10 — Agent Runtime design

Цель: описать shape первого agent runtime до implementation.

Решить:

* task state;
* sandbox/worktree strategy;
* approval flow;
* audit trail;
* связь с Memory/RAG retrieval;
* нужен ли отдельный service или достаточно lightweight workflow.

Код на Stage 10 не добавлять.

### Stage 11 — Agent Runtime MVP

Цель: реализовать минимальный agent workflow поверх уже проверенной идеи docs drift assistant.

Важно: это не повтор `scripts/docs-drift-agent.sh`. MVP должен добавить runtime/workflow boundary: isolated worktree или equivalent sandbox, patch output, human approval gate и audit trail.

Не-цели:

* production shell autonomy;
* Docker mutations без approval;
* Telegram escalation;
* broad tools framework.

### Stage 12 — Tool permissions / approval v1

Цель: формализовать permissions для будущих tools.

Ожидаемые решения:

* diagnostics allowlist;
* dangerous action classes;
* approval format;
* audit log schema;
* rollback contract.

### Stage 13 — MCP decision

Цель: решить, нужен ли MCP layer.

Вопросы:

* MCP server или adapter?
* какие tools безопасно expose-ить?
* где проходят approvals?
* как не дать MCP обойти `AGENTS.md` и runbook rules?

### Stage 14 — Telegram -> Agent escalation

Цель: дать Telegram безопасный способ инициировать agent task.

Граница безопасности:

```text
Telegram command -> request/approval -> agent task -> patch/report
```

Telegram не получает shell, Docker socket или filesystem access напрямую.

### Stage 15 — Ops hardening continuation

Цель: продолжить operational hardening после RAG/agent foundations.

Кандидаты:

* restore dry run для `memory-db`;
* решение по automation для `cluster-health-lite.sh`;
* Open WebUI auth;
* direct backend ports policy;
* backup retention/encryption;
* monitoring stack decision.

---

## 8. Отложенные направления

* Multi-node cluster.
* Полноценный plugin system.
* Расширенные UI integrations beyond Telegram, Open WebUI и VS Code/Copilot.
* Qdrant или hybrid vector stack, если pgvector станет недостаточным.

---

## 9. Открытые развилки

* **RAG retrieval format** — DB query layer или HTTP service, формат provenance.
* **Agent runtime** — process model, sandbox, task state.
* **MCP** — нужен ли вообще и где провести security boundary.
* **Security hardening** — VPN vs reverse proxy, direct ports, auth policy.
* **Backups** — restore rehearsal, retention, encryption, Open WebUI data scope.

---

## 10. Правила изменения roadmap

1. Roadmap менять после решения или явного approval.
2. Завершённые stages переносить в `docs/changelog.md`; roadmap хранит будущее и текущий active stage.
3. Не добавлять runtime dependencies через roadmap без отдельного design stage.
4. Крупная развилка требует ADR или subsystem design doc до implementation.
