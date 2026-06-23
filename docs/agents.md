# slowrig AI Cluster — Agents Design v0.1

Дата: 2026-06-23
Статус: Stage 6 design; runtime не реализован

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

## 11. Минимальный implementation plan для следующего stage

Рекомендуемый следующий Stage 6.1:

```text
Agents implementation plan
```

Он должен определить:

* первый concrete workflow;
* будет ли это scripts-only, Codex procedure или маленький local service;
* Level 2 diagnostics allowlist;
* формат agent task file/report;
* как агент получает context из docs/Memory;
* как создаются patches;
* какие operations требуют approval;
* manual checks и rollback.

Runtime не внедрять до завершения Stage 6.1 и отдельного approval.

---

## 12. Открытые вопросы

Открытые вопросы перед implementation:

* какой первый workflow важнее: docs drift audit, repo patch assistant, diagnostic assistant или Telegram-to-agent escalation;
* нужен ли отдельный task queue или достаточно git branches + markdown task reports;
* точный diagnostics allowlist;
* формат хранения agent state, если он вообще нужен;
* нужно ли подключать Memory/RAG retrieval в первом runtime или оставить docs/git read напрямую.
