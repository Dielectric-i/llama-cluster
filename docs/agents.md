# slowrig AI Cluster — Agents Design v0.1

Дата: 2026-06-23
Статус: Stage 6 design; Stage 6.1 docs drift workflow plan added; runtime не реализован

## 1. Назначение

Этот документ описывает первый безопасный дизайн agent layer для `slowrig AI Cluster`.

Цель Stage 6:

```text
спроектировать controlled agent workflows поверх существующих LiteLLM, Memory/RAG, Telegram/Open WebUI и git-документации без установки нового agent framework и без выдачи агентам опасных прав
```

Agent layer должен помогать с:

* анализом репозитория;
* подготовкой patches;
* проверкой документации и drift;
* планированием stages;
* безопасной диагностикой;
* будущей координацией Telegram/IDE/API workflows.

Agent layer не должен становиться shell gateway, root automation, скрытым оператором Docker или заменой человеческого approval.

---

## 2. Non-goals

Stage 6 design не должен:

* устанавливать новый agent framework;
* добавлять Docker services;
* добавлять MCP servers;
* давать Telegram bot shell/Docker/filesystem доступ;
* автоматически менять `.env`, volumes, firewall, GPU mapping, models, context size или `parallel`;
* выполнять destructive git или Docker operations;
* индексировать private Telegram history, raw logs или secrets;
* заменять `AGENTS.md` как правила работы Codex;
* заменять `docs/runbook.md` как источник operational commands.

---

## 3. Принятый первый вариант

Первый agent design:

```text
custom lightweight orchestration / Codex-driven workflow
```

Обычный LLM path:

```text
Agent workflow -> LiteLLM Gateway -> slowrig/coder или slowrig/architect
```

Контекстный path:

```text
Agent workflow -> project docs/git files -> optional Memory/RAG retrieval -> LiteLLM Gateway
```

Первый runtime, когда он будет одобрен, должен быть маленьким и проверяемым. Предпочтение:

* plain scripts или небольшой local service только после отдельного implementation plan;
* явные allowlists;
* git patches вместо прямых mutation по умолчанию;
* human approval для опасных действий;
* logs без secrets и без полного private content.

---

## 4. Лестница прав

Права агентов должны расти по ступеням, а не включаться сразу.

| Уровень | Название | Разрешено | Запрещено по умолчанию |
| ---: | --- | --- | --- |
| 0 | read/report | читать docs/config/git state, делать отчёт | менять файлы, запускать runtime commands |
| 1 | patches | готовить patches и инструкции проверки | применять risky runtime changes без approval |
| 2 | diagnostics allowlist | запускать заранее утверждённые read-only diagnostics | произвольный shell, Docker mutation, secrets access |
| 3 | approved mutations | выполнять точечно одобренные file/service изменения | destructive operations, hidden broad automation |
| 4 | sandbox autonomy | работать автономно только в sandbox/worktree | прямое управление production baseline без approval |

Текущий safe default:

```text
Level 1: read + patches + reports
```

Level 2 требует отдельного списка команд.

---

## 5. Разрешённые источники контекста

Первый agent layer может использовать:

* `README.md` как индекс документации;
* `AGENTS.md` как правила работы Codex;
* `docs/*.md` как source of truth;
* git diff/status/log;
* `config/*.yaml` и scripts как inspectable config;
* Memory/RAG docs corpus после явного retrieval design.

Не использовать как автоматический corpus без отдельного решения:

* `.env`;
* secrets;
* raw Docker logs;
* Telegram private history;
* Open WebUI data;
* database dumps;
* model/cache files.

---

## 6. Диагностика

Будущий Level 2 diagnostics allowlist должен быть отдельным stage.

Кандидаты для read-only allowlist:

```bash
git status --short
git diff --stat
git log --oneline -20
docker compose ps
docker logs --tail=160 <known-service>
curl http://127.0.0.1:4000/v1/models
curl http://127.0.0.1:8080/v1/models
curl http://127.0.0.1:8081/v1/models
/opt/llama-cluster/scripts/cluster-status.sh
```

Перед implementation нужно уточнить:

* где команды запускаются: host, container, sandbox;
* как скрываются secrets;
* какие команды требуют `sudo`;
* какие команды могут быть слишком дорогими;
* как фиксируется output в отчёте;
* какие команды запрещены даже на diagnostic уровне.

---

## 7. Mutations and approval

Опасные операции всегда требуют явного approval:

* `docker compose down`, restart all, volume delete;
* изменение `.env` или secrets;
* изменение firewall/reverse proxy/VPN;
* изменение model files, GPU mapping, `ctx-size`, `parallel`, `tensor-split`;
* package install, image pull, framework install;
* destructive git commands;
* database restore/drop/delete;
* публикация портов наружу.

Preferred mutation flow:

```text
inspect -> plan -> patch -> diff -> user review -> explicit approval -> apply -> check -> rollback known
```

Для code/docs изменений preferred output — git patch и stage report.

---

## 8. Routing policy

Default model:

```text
slowrig/coder
```

Use `slowrig/architect` only when:

* нужна сложная архитектурная развилка;
* нужно глубокое ревью плана;
* требуется итоговый synthesis после подготовки контекста;
* пользователь явно просит architect/deep reasoning.

Не добавлять новые gateway aliases для agents в Stage 6 design:

```text
slowrig/agent
slowrig/deep
slowrig/router
```

Если понадобятся agent-specific aliases, это отдельное LiteLLM/Gateway решение.

---

## 9. Memory/RAG interaction

Первый agent layer может читать Memory/RAG только как derived project-docs context.

Разрешённый первый corpus уже определён Memory stage:

```text
README.md
AGENTS.md
docs/*.md
```

Agent layer не должен записывать новые private user memory, Telegram history или raw logs в PostgreSQL без отдельного privacy/security decision.

---

## 10. Audit trail

Будущая реализация должна оставлять понятный след:

* какой stage выполнялся;
* какие files читались;
* какие commands запускались;
* какие files изменились;
* какие checks выполнены;
* какой rollback есть;
* что требовало human approval.

Не хранить secrets и полный private prompt text в logs.

---

## 11. Stage 6.1 — Docs Drift / Repo Patch Assistant plan

Выбранный первый workflow:

```text
docs drift / repo patch assistant
```

Статус:

```text
implementation plan; runtime не реализован
```

### Цель

Первый agent workflow должен помогать находить расхождения между документацией, config и git-состоянием, а затем готовить маленький patch и отчёт.

Он работает на безопасных уровнях:

```text
Level 0 read/report
Level 1 patches
```

Level 2 diagnostics allowlist в Stage 6.1 не включается.

### Входы

Разрешённые входы:

* `README.md`;
* `AGENTS.md`;
* `docs/*.md`;
* `docker-compose.yaml`;
* `config/**/*.yaml`;
* `scripts/*.sh` и небольшие project scripts;
* `git status --short`;
* `git diff --stat`;
* `git diff -- <explicit-files>`;
* `git log --oneline -20`.

Запрещённые входы без отдельного approval:

* `.env`;
* secrets;
* raw Docker logs;
* database dumps;
* Telegram private history;
* Open WebUI private data;
* model/cache files.

### Основной workflow

```text
1. Read README.md as documentation index.
2. Read AGENTS.md and docs/codex-context.md.
3. Read task-relevant docs.
4. Compare docs against config files and git state.
5. Report mismatches with source references.
6. Prepare a focused patch only for accepted docs/config drift.
7. Provide validation commands and rollback.
8. Commit only after explicit user approval or direct instruction.
```

### Типы drift, которые workflow должен искать

* ports, service names, model names и route mismatches;
* `ctx-size`, `tensor-split`, `parallel`, GPU mapping drift;
* outdated stage status;
* README documentation index gaps;
* ADR status that conflicts with current docs/config;
* changelog entries that claim unverified checks;
* runbook commands that no longer match compose/config;
* missing rollback notes for operational changes.

### Output format

Для audit-only режима:

```text
Stage:
Scope:
Files read:
Findings:
Risks:
Recommended patch:
Validation:
Rollback:
Next stage:
```

Для patch режима:

```text
Stage:
What changed:
Files changed:
Checks performed:
Checks not performed:
Rollback:
Remaining forks:
Recommended next stage:
```

### Patch rules

* предпочитать documentation patch перед runtime patch;
* не менять runtime/config по результатам audit без отдельного approval;
* не исправлять исторические stage summaries, если они корректно описывают прошлое состояние;
* явно помечать superseded ADR вместо удаления старого решения;
* не использовать `git add .`;
* не коммитить `.env` или generated/runtime data.

### Проверки Stage 6.1

Для документационных правок:

```bash
git status --short
git diff --stat
git diff -- README.md AGENTS.md docs/
git diff --check
```

Для config-aware audit без runtime mutation:

```bash
git diff -- docker-compose.yaml config/ scripts/
```

Server/runtime checks не требуются, пока workflow не меняет `docker-compose.yaml`, LiteLLM config, ports, volumes, services или scripts with operational behavior.

### Rollback

Откатить только файлы Stage 6.1:

```bash
git checkout -- docs/agents.md docs/decisions.md docs/codex-context.md docs/changelog.md
```

Если был создан отдельный audit report file в будущем stage, откатывать его отдельно.

### Non-goals Stage 6.1

Stage 6.1 не добавляет:

* agent runtime service;
* task queue;
* Telegram-to-agent escalation;
* diagnostics command execution allowlist;
* Memory/RAG retrieval API;
* autonomous shell;
* new dependencies.

---

## 12. Открытые вопросы

Открытые вопросы перед runtime implementation:

* нужен ли отдельный task queue или достаточно git branches + markdown task reports;
* точный Level 2 diagnostics allowlist;
* формат хранения agent state, если он вообще нужен;
* нужно ли подключать Memory/RAG retrieval в первом runtime или оставить docs/git read напрямую;
* когда подключать Telegram-to-agent escalation.
