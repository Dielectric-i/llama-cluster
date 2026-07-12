# Codex Context — slowrig AI Cluster

Создано: 2026-06-21
Обновлено: 2026-07-12
Статус: краткий дополнительный контекст для Codex

## 1. Назначение

Этот документ помогает Codex быстро вспомнить рабочие предпочтения, принятые defaults и открытые развилки проекта.

Он не является источником истины для hardware, ports, services, commands, architecture decisions, changelog или operational procedures. За этими фактами идти в dedicated docs, перечисленные в `README.md`.

Если этот файл конфликтует с dedicated document, считать dedicated document главным.

---

## 2. Источники истины

| Тема | Главный источник |
| --- | --- |
| Главный вход и индекс | `README.md` |
| Правила Codex | `AGENTS.md` |
| Факты стенда | `docs/passport.md` |
| Эксплуатация и rollback | `docs/runbook.md` |
| Архитектура | `docs/architecture.md` |
| LiteLLM Gateway | `docs/gateway.md` |
| Memory/RAG | `docs/memory.md` |
| Telegram bot | `docs/telegram.md` |
| Agent layer | `docs/agent-framework.md` |
| Monitoring/Security/Backups | `docs/monitoring.md`, `docs/security.md`, `docs/backups.md` |
| ADR | `docs/decisions.md` |
| История изменений | `docs/changelog.md` |
| План будущих stages | `docs/roadmap.md` |

При расхождении docs, config и server output не угадывать. Нужно явно описать mismatch и спросить Александра, какое состояние считать правильным.

---

## 3. Рабочие предпочтения

Основной оператор — Александр.

Codex может работать с реальным сервером через:

```text
ssh discover@slowrig
```

Рискованные действия остаются за оператором или требуют явного approval:

* Docker Compose mutations, restarts, `down`;
* actions с secrets и `.env`;
* GPU/model/context/parallel changes;
* firewall, reverse proxy, VPN;
* destructive git/database/volume operations;
* log inspection, если логи могут содержать sensitive data.

Пользовательское общение — на русском. Технические identifiers не переводить.

Рабочий стиль:

```text
stability > number of features
clarity > clever automation
small stages > large rewrites
manual verification > assumed success
documented behavior > hidden state
rollback path > one-way migration
```

---

## 4. Текущий baseline

Основной path:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

Дополнительные interfaces:

```text
Telegram -> telegram-bot -> LiteLLM Gateway
VS Code / Copilot -> ide-proxy -> LiteLLM Gateway
```

Имена моделей Gateway:

```text
slowrig/coder
slowrig/architect
```

Direct backend ports `8080` и `8081` остаются LAN diagnostics/rollback path до отдельного security decision.

---

## 5. Завершённые основные stages

```text
Stage 4.3 — Memory DB foundation внедрён и проверен
Stage 4.4 — Local RAG ingestion внедрён и проверен
Stage 5   — Telegram bot runtime внедрён
Stage 5.5 — Cloudflare short polling transport внедрён
Stage 5.6 — Telegram thinking disabled через request params
Stage 6   — Agent baseline завершён; docs drift helper внедрён
Stage 7   — Monitoring/Security/Backups design завершён
Stage 7.1 — cluster-health-lite.sh внедрён без timer/automation
Stage 7.2 — manual memory-db backup helper внедрён
Stage 7.5 — restore dry run отложен по решению оператора
Stage 8   — docs alignment: текущий stage
```

Новые runtime dependencies не добавлять без отдельного design/approval stage.

---

## 6. Memory/RAG defaults

Принятое направление:

```text
PostgreSQL + pgvector
```

Текущий статус:

* `memory-db` внедрён как internal PostgreSQL + pgvector service без host port;
* `memory-embed` внедрён как локальный loopback embedding runtime на `127.0.0.1:4010`;
* первый corpus: `README.md`, `AGENTS.md`, `docs/*.md`;
* Markdown + Git остаются source of truth;
* PostgreSQL хранит rebuildable derived chunks/embeddings и будущий structured state.

Не индексировать без отдельного privacy/security decision:

* `.env`, secrets;
* raw logs;
* Telegram history;
* Open WebUI history;
* произвольные пользовательские чаты.

Ближайший Memory/RAG fork: read-only retrieval API или module поверх `memory-db`, сначала для Telegram, затем reusable для agents.

---

## 7. Telegram defaults

Принятый shape:

```text
Telegram Bot API polling + whitelist -> LiteLLM Gateway
```

Текущий runtime:

* C#/.NET service `src/telegram-bot`;
* Compose service `telegram-bot` без `profile`;
* default model: `slowrig/coder`;
* `slowrig/architect` только по явной команде или documented escalation;
* no shell, no Docker socket, no host filesystem access;
* Telegram history не пишется в Memory/RAG.

Если server не может reach `api.telegram.org:443`, использовать `TELEGRAM_API_BASE_URL` для narrow reverse proxy mode или `TELEGRAM_PROXY_URL` для HTTP(S) proxy. `tg://proxy?...` не подходит для Bot API HTTP polling.

Thinking отключён через:

```text
chat_template_kwargs.enable_thinking=false
```

---

## 8. Agent defaults

Принятое направление:

```text
custom lightweight orchestration / Codex-driven workflow
```

Текущий внедрённый helper:

```text
scripts/docs-drift-agent.sh
```

Он read-only: проверяет docs/config drift, не читает `.env`, не запускает Docker, не вызывает LLM generation, не пишет в Memory/RAG и не меняет файлы.

Permission ladder:

```text
read/report -> patches -> diagnostics allowlist -> approved mutations -> sandbox autonomy
```

Не устанавливать CrewAI, OpenClaw или другой agent framework без отдельного implementation stage и approval.

---

## 9. Monitoring, security, backups

Monitoring:

```text
scripts/cluster-health-lite.sh -> дешёвый read-only healthcheck
scripts/cluster-status.sh      -> глубокая ручная диагностика с LLM checks
```

Security:

```text
LAN/VPN first
no public WebUI/Gateway exposure before hardening
direct ports 8080/8081 remain for diagnostics
```

Backups:

* docs/config/scripts/source files — через git;
* `.env` — offline secret storage, не в git;
* `memory-db` — manual `scripts/backup-memory-db.sh`;
* restore dry run отложен до отдельного stage;
* Open WebUI data пока не включён в backup scope.

---

## 10. Открытые развилки

Обсуждать только когда они становятся relevant для следующего stage:

* shape read-only retrieval поверх `memory-db`;
* prompt/context assembly для Telegram и agents;
* migration mechanism для PostgreSQL schema;
* backup encryption и restore rehearsal;
* Telegram command surface для RAG/agent escalation;
* exact diagnostics allowlist для agents;
* когда закрывать direct backend ports;
* нужен ли Qdrant позже;
* включать ли Open WebUI data в backup/index scope.

При каждой развилке Codex должен объяснить options и consequences до запроса решения.

---

## 11. Как обновлять этот файл

Обновлять только краткий supplemental context, который не лучше хранить в dedicated docs.

Перед добавлением спросить:

```text
Это уже покрыто README, AGENTS, passport, runbook, architecture, gateway, memory, decisions, changelog или roadmap?
```

Если да — не дублировать.
