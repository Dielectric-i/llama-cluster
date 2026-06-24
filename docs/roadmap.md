# slowrig AI Cluster — Дорожная карта

Дата создания: 2026-06-23
Источник: `codex/main`
Активная ветка: `qwen/main`
Префикс веток: `qwen/`

---

## Целевое видение

Автономная AI-инфраструктура, где:

1. **Агент разработки** может получить план и автономно выполнять длительную итеративную разработку в песочнице, с approval для критичных действий
2. **Чат-бот** может быть настроен для выполнения различных ролей, общаться автономно в чатах, иметь несколько персонажей
3. **Система расширяема** — можно добавлять новые модели, роли, серверы, плагины

---

## Завершённые этапы

| Этап | Описание | Статус |
| --- | --- | --- |
| Stage 1 | Inference baseline (2 модели, 3 GPU) | ✅ сервер |
| Stage 2 | Operational foundation | ✅ сервер |
| Stage 3 | Gateway baseline (LiteLLM) | ✅ сервер |
| Stage 4.2 | Memory implementation plan | ✅ сервер |
| Stage 4.3 | Memory DB foundation (PostgreSQL + pgvector) | ✅ сервер |
| Stage 4.4 | Local RAG ingestion | ✅ сервер |
| Stage 5.2 | Telegram runtime (C#) | ✅ сервер |
| Stage 5.5 | Cloudflare short polling | ✅ сервер |
| Stage 5.6 | Thinking disabled | ✅ сервер |
| Stage 6 | Agents baseline (design) | ✅ сервер |
| Stage 6.1 | Docs drift plan | ✅ сервер |
| Stage 6.3 | docs-drift-agent.sh | ✅ сервер |
| Stage 7 | Monitoring/Security/Backups design | ✅ сервер |
| Stage 7.1 | cluster-health-lite plan | ✅ сервер |

---

## План: этапы до конца

### Критический путь

```
Stage 8 (RAG Retrieval) → Stage 9 (Agent Runtime) → Stage 11 (Personas)
         ↓                       ↓
    Stage 10 (MCP) → Tool Calling для агента
```

Минимальный viable продукт для целевого видения:
1. Stage 8 — RAG retrieval
2. Stage 9 — Agent runtime
3. Stage 11 — Telegram personas
4. Stage 15 — Ops hardening

---

### Stage 8 — RAG Retrieval

**Цель:** Подключить RAG retrieval к клиентам.

**Разрыв:** Ingestion есть (embeddings записаны в DB), retrieval нет. Ни Telegram, ни WebUI, ни агенты не используют RAG.

**Подэтапы:**

| Подэтап | Описание | Ветка |
| --- | --- | --- |
| 8.1 | RAG retrieval API — HTTP-эндпоинт для semantic search | `qwen/stage-8-rag-retrieval` |
| 8.2 | RAG для Telegram — retrieval + контекст в system prompt | `qwen/stage-8-rag-retrieval` |
| 8.3 | RAG для Open WebUI — hook или plugin | `qwen/stage-8-rag-retrieval` |
| 8.4 | RAG для агентов — API для agent retrieval | `qwen/stage-8-rag-retrieval` |

**Затронутые файлы:**
- `docker-compose.yaml` — новый сервис или расширение embedding-runtime
- `scripts/memory-ingest-docs.py` — добавить retrieval mode
- `src/telegram-bot/Program.cs` — RAG context injection
- `docs/memory.md` — retrieval architecture
- `docs/changelog.md`

**Риск:** Низкий. DB и ingestion уже работают.

---

### Stage 9 — Agent Runtime

**Цель:** Сервис автономного агента с песочницей.

**Разрыв:** Есть только `docs-drift-agent.sh` (bash-скрипт, работает на хосте). Нет сервиса, песочницы, tool calling, task queue.

**Подэтапы:**

| Подэтап | Описание | Ветка |
| --- | --- | --- |
| 9.1 | Agent service design — orchestrator + worker | `qwen/stage-9-agent-runtime` |
| 9.2 | Agent sandbox — Docker container / worktree, resource limits | `qwen/stage-9-agent-runtime` |
| 9.3 | Agent orchestrator — task queue, plan parser, step executor, approval gate | `qwen/stage-9-agent-runtime` |
| 9.4 | Agent tools v1 — git, file, terminal, LLM, RAG | `qwen/stage-9-agent-runtime` |
| 9.5 | Agent API — HTTP API, WebSocket/SSE, Telegram integration | `qwen/stage-9-agent-runtime` |

**Затронутые файлы:**
- `docker-compose.yaml` — agent-runtime сервис
- `src/agent-runtime/` — новый каталог
- `docs/agent-runtime.md` — новый документ
- `docs/agents.md` — обновление
- `docs/changelog.md`

**Риск:** Средний. Новый сервис, требует careful sandboxing.

---

### Stage 10 — Tool Calling & MCP

**Цель:** Tool calling и MCP для агента.

**Разрыв:** Нет MCP server, нет tool definitions, нет tool calling в моделях.

**Подэтапы:**

| Подэтап | Описание | Ветка |
| --- | --- | --- |
| 10.1 | MCP Server — protocol implementation | `qwen/stage-10-mcp` |
| 10.2 | Tool definitions — git, file, terminal, web, RAG | `qwen/stage-10-mcp` |
| 10.3 | Tool security — classification, approval flow, audit log | `qwen/stage-10-mcp` |
| 10.4 | Model tool calling — настроить llama.cpp | `qwen/stage-10-mcp` |

**Затронутые файлы:**
- `config/litellm.config.yaml` — MCP configuration
- `src/agent-runtime/` — MCP server
- `docs/mcp.md` — новый документ
- `docs/changelog.md`

**Риск:** Средний. Зависит от поддержки tool calling в моделях.

---

### Stage 11 — Telegram Personas & Auto-Chats

**Цель:** Система персонажей, ролей и автономных чатов.

**Разрыв:** Telegram бот — один монолитный файл, одна роль, только 1-on-1 диалог.

**Подэтапы:**

| Подэтап | Описание | Ветка |
| --- | --- | --- |
| 11.1 | Persona system design — структура, хранение | `qwen/stage-11-telegram-personas` |
| 11.2 | Persona config files — YAML файлы персонажей | `qwen/stage-11-telegram-personas` |
| 11.3 | Telegram persona commands — /persona, /persona create | `qwen/stage-11-telegram-personas` |
| 11.4 | Auto-chat engine — group monitoring, trigger rules | `qwen/stage-11-telegram-personas` |
| 11.5 | Multi-agent dialogue — внутренние чаты между персонажами | `qwen/stage-11-telegram-personas` |
| 11.6 | RAG для персонажей — individual RAG corpus | `qwen/stage-11-telegram-personas` |

**Затронутые файлы:**
- `src/telegram-bot/Program.cs` — рефакторинг + personas
- `config/personas/` — новый каталог
- `docs/personas.md` — новый документ
- `docs/telegram.md` — обновление
- `docs/changelog.md`

**Риск:** Низкий-средний. Расширение существующего бота.

---

### Stage 12 — Multi-Model & External API

**Цель:** Поддержка нескольких моделей и внешних API.

**Разрыв:** Только 2 локальные модели в LiteLLM, нет внешних API.

**Подэтапы:**

| Подэтап | Описание | Ветка |
| --- | --- | --- |
| 12.1 | Model registry — реестр моделей, метаданные | `qwen/stage-12-multi-model` |
| 12.2 | External API models — провайдеры в LiteLLM | `qwen/stage-12-multi-model` |
| 12.3 | Dynamic model loading — hot-swap | `qwen/stage-12-multi-model` |
| 12.4 | Smart routing — по размеру, сложности, доступности | `qwen/stage-12-multi-model` |

**Затронутые файлы:**
- `config/litellm.config.yaml` — новые модели
- `docker-compose.yaml` — новые llama.cpp сервисы
- `docs/gateway.md` — обновление
- `docs/changelog.md`

**Риск:** Низкий. LiteLLM уже поддерживает.

---

### Stage 13 — Plugin System

**Цель:** Система плагинов для расширения функционала.

**Разрыв:** Нет системы плагинов.

**Подэтапы:**

| Подэтап | Описание | Ветка |
| --- | --- | --- |
| 13.1 | Plugin architecture — manifest, lifecycle, sandboxing | `qwen/stage-13-plugin-system` |
| 13.2 | Plugin types — tools, personas, channels, RAG sources | `qwen/stage-13-plugin-system` |
| 13.3 | Plugin registry — config/plugins/ | `qwen/stage-13-plugin-system` |
| 13.4 | Plugin API — HTTP/gRPC, event system, security | `qwen/stage-13-plugin-system` |

**Затронутые файлы:**
- `src/plugin-host/` — новый каталог
- `config/plugins/` — новый каталог
- `docs/plugins.md` — новый документ
- `docs/changelog.md`

**Риск:** Средний. Новая архитектура.

---

### Stage 14 — Cluster Orchestration

**Цель:** Поддержка нескольких серверов.

**Разрыв:** Один сервер, нет оркестрации.

**Подэтапы:**

| Подэтап | Описание | Ветка |
| --- | --- | --- |
| 14.1 | Multi-node design — leader/follower, discovery, LB | `qwen/stage-14-cluster` |
| 14.2 | LiteLLM multi-node — несколько backend-узлов | `qwen/stage-14-cluster` |
| 14.3 | Shared state — replication, config sync, secrets | `qwen/stage-14-cluster` |
| 14.4 | Node management — add/remove, GPU pool, model placement | `qwen/stage-14-cluster` |

**Затронутые файлы:**
- `docker-compose.yaml` — multi-node
- `config/` — cluster config
- `docs/clustering.md` — новый документ
- `docs/changelog.md`

**Риск:** Высокий. Значительная архитектурная переделка.

---

### Stage 15 — Ops Hardening

**Цель:** Monitoring, security, backup runtime.

**Разрыв:** Дизайн готов, runtime не реализован.

**Подэтапы:**

| Подэтап | Описание | Ветка |
| --- | --- | --- |
| 15.1 | cluster-health-lite.sh — реализовать и запустить | `qwen/stage-15-ops-hardening` |
| 15.2 | Security hardening — auth, firewall, VPN | `qwen/stage-15-ops-hardening` |
| 15.3 | Backup automation — pg_dump, retention, restore test | `qwen/stage-15-ops-hardening` |
| 15.4 | Monitoring dashboard — GPU, services, alerts | `qwen/stage-15-ops-hardening` |

**Затронутые файлы:**
- `scripts/cluster-health-lite.sh` — новый файл
- `docker-compose.yaml` — monitoring сервисы
- `docs/monitoring.md` — обновление
- `docs/security.md` — обновление
- `docs/backups.md` — обновление
- `docs/changelog.md`

**Риск:** Низкий. Дизайн готов.

---

## Несоответствия с текущим состоянием

| Проблема | Где | Решение |
| --- | --- | --- |
| RAG ingestion работает, retrieval нет | `memory-ingest-docs.py` | Stage 8.1 |
| Telegram бот — монолит | `Program.cs` ~500 строк | Stage 11 |
| LiteLLM config минимальный | 2 модели, нет tool calling | Stage 10 |
| Нет agent state storage | Только docs chunks в DB | Stage 9.3 |
| Нет approval workflow | Агенты не запрашивают approval | Stage 9.3 |
| Нет sandbox isolation | `docs-drift-agent.sh` на хосте | Stage 9.2 |
| Backup только ручной | 6 dump-файлов без retention | Stage 15.3 |
| Security только на бумаге | Порты открыты, auth выключен | Stage 15.2 |
| Нет health monitoring | Только ручной `cluster-status.sh` | Stage 15.1 |

---

## Веточная стратегия

```
codex/main ──────────────────────────────┐
                                          ├── qwen/main ← активная интеграционная ветка
                                          │   │
                                          │   ├── qwen/stage-8-rag-retrieval
                                          │   ├── qwen/stage-9-agent-runtime
                                          │   ├── qwen/stage-10-mcp
                                          │   ├── qwen/stage-11-telegram-personas
                                          │   ├── qwen/stage-12-multi-model
                                          │   ├── qwen/stage-13-plugin-system
                                          │   ├── qwen/stage-14-cluster
                                          │   └── qwen/stage-15-ops-hardening
                                          │
codex/* ветки — заморожены, не используются для новой работы
master — не используется
```

Порядок работы:
1. создать `qwen/stage-X` от `qwen/main`
2. реализовать и проверить
3. merged в `qwen/main` после approval
4. следующий stage от обновлённого `qwen/main`

---

## Рекомендованный порядок

1. **Stage 8** — RAG Retrieval (неделя) → сразу полезно для Telegram и WebUI
2. **Stage 15.1** — cluster-health-lite.sh (день) → операционная стабильность
3. **Stage 9** — Agent Runtime (2-3 недели) → основа для автономной разработки
4. **Stage 11** — Telegram Personas (неделя) → роли, персонажи, авто-чаты
5. **Stage 10** — Tool Calling & MCP (неделя) → инструменты для агента
6. **Stage 12** — Multi-Model & External API (неделя) → масштабирование
7. **Stage 13** — Plugin System (2 недели) → расширяемость
8. **Stage 14** — Cluster Orchestration (3-4 недели) → несколько серверов
9. **Stage 15** — Ops Hardening (неделя) → финальная стабилизация
