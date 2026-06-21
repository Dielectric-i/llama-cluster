# slowrig AI Cluster

Домашний локальный ИИ-кластер для разработки, анализа кода, работы с документацией, локальных ассистентов и будущих агентских сценариев.

## Быстрый статус

Главная команда проверки:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

Она показывает:

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
| `llama-architect` | `Qwen3.6-27B-UD-Q4_K_XL.gguf` | `ctx-size 40000` |
| `llama-coder` | `Qwen3.5-9B-UD-Q4_K_XL.gguf` | `ctx-size 40000` |

## Основные файлы и директории

```text
/opt/llama-cluster/docker-compose.yaml
/opt/llama-cluster/docker-compose.stage1-baseline.yaml
/opt/llama-cluster/README.md
/opt/llama-cluster/AGENTS.md
/opt/llama-cluster/config/litellm.config.yaml
/opt/llama-cluster/.env.example
/opt/llama-cluster/scripts/cluster-status.sh
```

Локальный файл секретов, не хранится в git:

```text
/opt/llama-cluster/.env
```

Основные директории:

```text
/opt/llama-cluster/models   # GGUF-модели, не хранить в git
/opt/llama-cluster/cache    # runtime/cache, не хранить в git
/opt/llama-cluster/docs     # документация
/opt/llama-cluster/scripts  # эксплуатационные скрипты
/opt/llama-cluster/config   # конфигурации сервисов
```

## Быстрые команды

Проверить общее состояние кластера:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
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
* увеличивать `ctx-size`;
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
git checkout -b codex/<stage-or-task-name>
```

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
| [docs/decisions.md](docs/decisions.md) | журнал архитектурных решений и компромиссов |
| [docs/changelog.md](docs/changelog.md) | фактическая история изменений, проверок и измерений |
| [docs/stage2-summary.md](docs/stage2-summary.md) | итог Stage 2 operational baseline |
| [docs/stage3-summary.md](docs/stage3-summary.md) | итог Stage 3 gateway baseline |

Если появляется новый значимый subsystem, для него нужно создать отдельный документ в `docs/` и добавить его в эту таблицу.