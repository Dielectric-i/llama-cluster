# slowrig AI Cluster

Домашний локальный ИИ-кластер для разработки, анализа кода, работы с документацией, локальных ассистентов и будущих агентских сценариев.

## Быстрый статус

Лёгкая частая проверка:

```bash
/opt/llama-cluster/scripts/cluster-health-lite.sh
```

Глубокая ручная диагностика:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

`cluster-health-lite.sh` не запускает LLM generation и подходит для частой дешёвой проверки.

`cluster-status.sh` показывает:

* состояние Docker-контейнеров;
* распределение GPU;
* занятость VRAM;
* доступность API;
* доступность Open WebUI;
* доступность LiteLLM Gateway;
* короткие проверки моделей через gateway;
* последние подозрительные строки логов.

Текущая основная цепочка:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

## Сервисы и адреса

| Сервис | Порт | Адрес | Роль |
| --- | ---: | --- | --- |
| `open-webui` | `3000` | `http://192.168.1.6:3000` | ручной WebUI через gateway |
| `litellm` | `4000` | `http://192.168.1.6:4000/v1` | LLM Gateway / Router |
| `llama-architect` | `8080` | `http://192.168.1.6:8080/v1` | 27B architect / сложные решения |
| `llama-coder` | `8081` | `http://192.168.1.6:8081/v1` | 9B coder / быстрый исполнитель |
| `memory-db` | нет | Docker Compose network only | PostgreSQL + pgvector для Memory / RAG foundation |
| `memory-embed` | `4010` | `http://127.0.0.1:4010/v1` | локальный embedding runtime для RAG ingestion |
| `telegram-bot` | нет | outbound Telegram Bot API | Telegram polling interface через LiteLLM |

Gateway model names:

```text
slowrig/coder
slowrig/architect
```

Прямые backend-порты `8080` и `8081` оставлены для диагностики. Обычные клиенты должны использовать LiteLLM Gateway.

## GPU mapping

| Host GPU | Подключение | Используется для |
| --- | --- | --- |
| GPU 0 | ограниченная PCIe-линия / райзер | часть `llama-architect` |
| GPU 1 | PCIe x16 | `llama-coder` |
| GPU 2 | ограниченная PCIe-линия / райзер | часть `llama-architect` |

## Модели

| Сервис | Модель | Контекст |
| --- | --- | ---: |
| `llama-architect` | `Qwen3.6-27B-UD-Q4_K_XL.gguf` | `ctx-size 60000` |
| `llama-coder` | `Qwen3.5-9B-UD-Q4_K_XL.gguf` | `ctx-size 128000` |
| `memory-embed` | `embeddings/Qwen3-Embedding-0.6B-Q8_0.gguf` | `ctx-size 32768` |

## Основные файлы и директории

```text
/opt/llama-cluster/docker-compose.yaml
/opt/llama-cluster/docker-compose.stage1-baseline.yaml
/opt/llama-cluster/README.md
/opt/llama-cluster/AGENTS.md
/opt/llama-cluster/config/litellm.config.yaml
/opt/llama-cluster/.env.example
/opt/llama-cluster/scripts/cluster-status.sh
/opt/llama-cluster/scripts/cluster-health-lite.sh
/opt/llama-cluster/scripts/backup-memory-db.sh
/opt/llama-cluster/scripts/docs-drift-agent.sh
/opt/llama-cluster/scripts/memory-ingest-docs.py
/opt/llama-cluster/config/memory/init/001-memory-foundation.sql
/opt/llama-cluster/config/cloudflare/telegram-worker.js
/opt/llama-cluster/src/telegram-bot
```

Локальный файл секретов, не хранится в git:

```text
/opt/llama-cluster/.env
```

После Stage 4.3 в `.env` должны быть реальные значения `MEMORY_POSTGRES_DB`, `MEMORY_POSTGRES_USER` и `MEMORY_POSTGRES_PASSWORD`; placeholders есть в `.env.example`.

Основные директории:

```text
/opt/llama-cluster/models   # GGUF-модели, не хранить в git
/opt/llama-cluster/cache    # runtime/cache, не хранить в git
/opt/llama-cluster/backups  # локальные DB dumps, не хранить в git
/opt/llama-cluster/docs     # документация
/opt/llama-cluster/scripts  # эксплуатационные скрипты
/opt/llama-cluster/config   # конфигурации сервисов
```

## Быстрые команды

Проверить общее состояние кластера:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
/opt/llama-cluster/scripts/cluster-health-lite.sh
```

Проверить контейнеры:

```bash
sudo docker ps
```

Проверить GPU:

```bash
nvidia-smi
```

Проверить итоговый compose:

```bash
cd /opt/llama-cluster
sudo docker compose config
```

Проверить состояние git-репозитория:

```bash
cd /opt/llama-cluster
git status --short
git diff --stat
```

Подробные команды диагностики, проверки LiteLLM, просмотра логов, перезапуска сервисов и rollback описаны в:

```text
docs/runbook.md
```

## Правила безопасности

Не делать без отдельного плана и проверки:

* увеличивать `parallel` у `llama-architect`;
* дальше увеличивать `ctx-size` без отдельного плана и проверки;
* менять GPU mapping;
* менять model files;
* включать CPU offload как штатный режим;
* включать Unified Memory как штатный режим;
* запускать третью LLM на GPU 0/2;
* открывать порты `3000`, `4000`, `8080`, `8081` наружу без VPN/auth/reverse proxy;
* публиковать `LITELLM_MASTER_KEY`;
* коммитить `.env`;
* удалять прямой доступ к `8080` и `8081`, пока gateway не стабилизирован длительно;
* удалять Docker volumes;
* обновлять образы без фиксации baseline;
* менять сразу несколько параметров compose.
* удалять `memory-db-data` без свежего dump и явного решения.

Текущий режим рассчитан на домашнюю LAN. Для внешнего доступа нужен отдельный security stage.

## Git

Этот каталог является локальным git-репозиторием для конфигов, документации и скриптов.

Модели, кэш, данные, логи и секреты исключены через `.gitignore`.

Перед экспериментами:

```bash
cd /opt/llama-cluster
git status --short
git diff --stat
```

Для изменений, подготовленных Codex, предпочтительно использовать отдельную ветку:

```bash
git checkout -b qwen/<stage-or-task-name>
```

Активная интеграционная ветка: `qwen/main`. Ветки `codex/*` заморожены и не используются для новой работы.

После успешной проверки добавлять файлы явно, по смыслу изменения:

```bash
git add README.md docs/runbook.md docs/changelog.md
git commit -m "Describe change"
```

Не использовать `git add .`, если в рабочем дереве могут быть посторонние изменения или секреты.

## Документы

| Документ | Назначение |
| --- | --- |
| [README.md](README.md) | быстрый вход в проект |
| [AGENTS.md](AGENTS.md) | правила работы Codex в репозитории |
| [docs/codex-context.md](docs/codex-context.md) | дополнительный контекст для Codex без дублирования основной документации |
| [docs/passport.md](docs/passport.md) | паспорт текущего стенда: железо, сервисы, порты, роли |
| [docs/runbook.md](docs/runbook.md) | ежедневная эксплуатация, диагностика, перезапуск, rollback, типовые проблемы |
| [docs/architecture.md](docs/architecture.md) | текущая и целевая архитектура ПО: gateway, память, агенты, Telegram |
| [docs/gateway.md](docs/gateway.md) | дизайн и baseline LiteLLM Gateway |
| [docs/memory.md](docs/memory.md) | Stage 4 Memory / RAG design, DB foundation и будущий RAG план |
| [docs/telegram.md](docs/telegram.md) | Stage 5 Telegram bot design: polling, whitelist, routing, safety |
| [docs/agents.md](docs/agents.md) | Stage 6 Agents design: лестница прав, safety boundaries, будущий implementation plan |
| [docs/monitoring.md](docs/monitoring.md) | Stage 7 Monitoring design: lightweight health checks без тяжёлого monitoring stack |
| [docs/security.md](docs/security.md) | Stage 7 Security design: LAN/VPN-first, secrets, direct port hardening plan |
| [docs/backups.md](docs/backups.md) | Stage 7 Backups design: минимальный backup scope и restore order |
| [docs/decisions.md](docs/decisions.md) | журнал архитектурных решений и компромиссов |
| [docs/changelog.md](docs/changelog.md) | фактическая история изменений, проверок и измерений |
| [docs/roadmap.md](docs/roadmap.md) | дорожная карта Stages 1-15, критический путь, текущие gaps |

Если появляется новый значимый subsystem, для него нужно создать отдельный документ в `docs/` и добавить его в эту таблицу.
