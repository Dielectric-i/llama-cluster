# slowrig AI Cluster

Домашний локальный ИИ‑кластер для разработки, анализа кода, работы с документацией, локальных ассистентов и будущих агентских сценариев.

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

* состояние Docker‑контейнеров;
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
VS Code / Copilot -> LiteLLM Gateway -> llama-coder / llama-architect
```

## Сервисы и адреса

| Сервис | Порт | Адрес | Роль |
| --- | ---: | --- | --- |
| `open-webui` | `3000` | `http://192.168.1.6:3000` | ручной WebUI через gateway |
| `litellm` | `4000` | `http://192.168.1.6:4000/v1` | LLM Gateway / Router |
| `llama-architect` | `8080` | `http://192.168.1.6:8080/v1` | 27B architect / сложные решения |
| `llama-coder` | `8081` | `http://192.168.1.6:8081/v1` | 9B coder / быстрый исполнитель |
| `memory-db` | — | Docker Compose network only | PostgreSQL + pgvector для Memory/RAG foundation |
| `memory-embed` | `4010` | `http://127.0.0.1:4010/v1` | локальный embedding runtime для RAG ingestion |
| `telegram-bot` | — | outbound Telegram Bot API | Telegram polling interface через LiteLLM |
| `ide-proxy` | `4011` | `http://192.168.1.6:4011` | IDE/Copilot **experiment** (SSE heartbeat) |

Gateway model names:

```text
slowrig/coder
slowrig/architect
```

Прямые backend‑порты `8080` и `8081` оставлены для диагностики. Обычные клиенты должны использовать LiteLLM Gateway.

## GPU mapping

| Host GPU | Подключение | Используется для |
| --- | --- | --- |
| GPU 0 | ограниченная PCIe‑линия / райзер | часть `llama-architect` |
| GPU 1 | PCIe x16 | `llama-coder` |
| GPU 2 | ограниченная PCIe‑линия / райзер | часть `llama-architect` |

## Модели

| Сервис | Модель | Контекст |
| --- | --- | ---: |
| `llama-architect` | `Qwen3.6-27B-UD-Q4_K_XL.gguf` | `ctx-size 65000` |
| `llama-coder` | `Qwen3.5-9B-UD-Q4_K_XL.gguf` | `ctx-size 128000` |
| `memory-embed` | `embeddings/Qwen3-Embedding-0.6B-Q8_0.gguf` | `ctx-size 32768` |

## Полезные директории

```text
/opt/llama-cluster/docker-compose.yaml           # главный compose
/opt/llama-cluster/models                        # GGUF‑модели (не в git)
/opt/llama-cluster/scripts                       # эксплуатационные скрипты
/opt/llama-cluster/docs                          # документация проекта
```

## Быстрые команды

… (остальные разделы без изменений) …

## Документация

Полный индекс: [docs/index.md](docs/index.md)
