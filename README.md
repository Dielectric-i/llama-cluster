# slowrig AI Cluster

`slowrig` — домашний локальный AI-кластер для разработки, анализа кода, работы с документацией, локальных ассистентов и будущих agent workflows.

Документация устроена так:

```text
README.md -> главный вход и индекс
AGENTS.md -> правила работы Codex в репозитории
docs/*.md -> тематические источники истины
```

Runtime/config этим документом не меняются.

---

## Быстрый статус

Лёгкая частая проверка без LLM generation:

```bash
/opt/llama-cluster/scripts/cluster-health-lite.sh
```

Глубокая ручная диагностика:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

`cluster-health-lite.sh` проверяет дешёвые признаки доступности. `cluster-status.sh` дополнительно проверяет gateway/model paths и может запускать короткие LLM-запросы, поэтому он не предназначен для частого автоматического healthcheck.

---

## Текущий baseline

Основная цепочка:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
VS Code / Copilot -> ide-proxy -> LiteLLM Gateway -> llama-coder / llama-architect
Telegram -> telegram-bot -> LiteLLM Gateway -> llama-coder / llama-architect
```

Обычные клиенты должны идти через LiteLLM Gateway. Прямые backend-порты `8080` и `8081` оставлены для диагностики и rollback.

Имена моделей Gateway:

```text
slowrig/coder
slowrig/architect
```

---

## Сервисы

| Service | Port | Address | Роль |
| --- | ---: | --- | --- |
| `open-webui` | `3000` | `http://192.168.1.6:3000` | ручной WebUI через gateway |
| `litellm` | `4000` | `http://192.168.1.6:4000/v1` | LLM Gateway / Router |
| `llama-architect` | `8080` | `http://192.168.1.6:8080/v1` | 27B architect / сложные задачи |
| `llama-coder` | `8081` | `http://192.168.1.6:8081/v1` | 9B coder / быстрый backend |
| `memory-db` | — | Docker Compose network only | PostgreSQL + pgvector для Memory/RAG |
| `memory-embed` | `4010` | `http://127.0.0.1:4010/v1` | локальный embedding runtime |
| `telegram-bot` | — | outbound Telegram Bot API | Telegram polling interface через LiteLLM |
| `ide-proxy` | `4011` | `http://192.168.1.6:4011` | IDE/Copilot experiment с SSE heartbeat |

`telegram-bot` — обычный Compose service без `profile`.

---

## GPU и модели

| Host GPU | Подключение | Используется для |
| --- | --- | --- |
| GPU 0 | ограниченная PCIe-линия / райзер | часть `llama-architect` |
| GPU 1 | PCIe x16 | `llama-coder` |
| GPU 2 | ограниченная PCIe-линия / райзер | часть `llama-architect` |

| Service | Model | Context |
| --- | --- | ---: |
| `llama-architect` | `Qwen3.6-27B-UD-Q4_K_XL.gguf` | `ctx-size 65000` |
| `llama-coder` | `Qwen3.5-9B-UD-Q4_K_XL.gguf` | `ctx-size 128000` |
| `memory-embed` | `embeddings/Qwen3-Embedding-0.6B-Q8_0.gguf` | `ctx-size 32768` |

Фактический паспорт стенда: [docs/passport.md](docs/passport.md).

---

## Основные команды

Проверить Compose config:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
```

Посмотреть сервисы:

```bash
cd /opt/llama-cluster
sudo docker compose ps
```

Проверить LiteLLM models endpoint без печати secrets:

```bash
cd /opt/llama-cluster
set -a
source .env
set +a

curl -sS \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  http://127.0.0.1:4000/v1/models
```

Подробные команды эксплуатации и rollback: [docs/runbook.md](docs/runbook.md).

---

## Директории

```text
/opt/llama-cluster/docker-compose.yaml  # основной compose
/opt/llama-cluster/models               # GGUF-модели, не в git
/opt/llama-cluster/scripts              # эксплуатационные helpers
/opt/llama-cluster/docs                 # тематическая документация
```

---

## Документация

| Документ | Назначение |
| --- | --- |
| [AGENTS.md](AGENTS.md) | правила работы Codex в этом репозитории |
| [docs/roadmap.md](docs/roadmap.md) | будущие stages, зависимости и развилки |
| [docs/passport.md](docs/passport.md) | фактический паспорт сервера, сервисов, моделей и ограничений |
| [docs/runbook.md](docs/runbook.md) | эксплуатация, диагностика, restart и rollback |
| [docs/architecture.md](docs/architecture.md) | текущая и целевая архитектура |
| [docs/gateway.md](docs/gateway.md) | LiteLLM Gateway baseline |
| [docs/memory.md](docs/memory.md) | Memory/RAG design и текущий статус |
| [docs/telegram.md](docs/telegram.md) | Telegram bot design и runtime |
| [docs/agent-framework.md](docs/agent-framework.md) | agent layer design и docs drift workflow |
| [docs/monitoring.md](docs/monitoring.md) | monitoring design и `cluster-health-lite.sh` |
| [docs/security.md](docs/security.md) | security posture и будущий hardening |
| [docs/backups.md](docs/backups.md) | backup scope, helper и restore policy |
| [docs/ide-proxy.md](docs/ide-proxy.md) | `ide-proxy` experiment для VS Code/Copilot |
| [docs/decisions.md](docs/decisions.md) | ADR: принятые архитектурные решения |
| [docs/changelog.md](docs/changelog.md) | фактическая история изменений и проверок |
| [docs/codex-context.md](docs/codex-context.md) | краткий дополнительный контекст для Codex |

При добавлении нового документа под `docs/` нужно добавить ссылку в этот README.
