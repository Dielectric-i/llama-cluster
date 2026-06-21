# slowrig AI Cluster — Changelog

## Назначение

Этот документ фиксирует фактические изменения, проверки и измерения по `slowrig AI Cluster`.

Формат:

* дата;
* что изменено;
* что проверено;
* результат;
* замечания;
* следующий шаг.

`changelog.md` отличается от `decisions.md`:

* `decisions.md` объясняет, почему выбрано архитектурное решение;
* `changelog.md` фиксирует, что реально было сделано.

---

## 2026-06-19 — Open WebUI routed through LiteLLM Gateway

### Изменено

Open WebUI переключён с прямого подключения к llama.cpp backend-ам на LiteLLM Gateway.

Новая цепочка:

```text
Open WebUI -> LiteLLM -> llama-coder / llama-architect
```

### Конфигурация

Open WebUI использует:

```text
OPENAI_API_BASE_URLS=http://litellm:4000/v1
OPENAI_API_KEYS=${LITELLM_MASTER_KEY}
```

LiteLLM использует модели:

| Gateway model name | Backend |
| --- | --- |
| `slowrig/coder` | `llama-coder` |
| `slowrig/architect` | `llama-architect` |

### Проверено

* `litellm` запущен на порту `4000`;
* `/v1/models` через LiteLLM отвечает;
* `slowrig/coder` отвечает через LiteLLM;
* `slowrig/architect` отвечает через LiteLLM;
* Open WebUI работает через LiteLLM;
* прямые backend-и `8080` и `8081` сохранены для диагностики;
* `scripts/cluster-status.sh` проверяет LiteLLM и оба model names.

### Результат

Stage 3 gateway baseline считается рабочим.

### Замечания

LiteLLM пока выполняет routing по явно выбранному model name. Автоматический выбор модели по сложности задачи пока не реализован.

---

## 2026-06-19 — Stage 1 baseline зафиксирован

### Изменено

Создана и проверена базовая inference-инфраструктура:

* `llama-architect`;
* `llama-coder`;
* `open-webui`.

### Текущие сервисы

| Сервис            |   Порт | Роль                           |
| ----------------- | -----: | ------------------------------ |
| `llama-architect` | `8080` | 27B architect / deep reasoning |
| `llama-coder`     | `8081` | 9B coder / fast worker         |
| `open-webui`      | `3000` | ручной WebUI                   |

### Модели

`llama-architect`:

```text
Qwen3.6-27B-UD-Q4_K_XL.gguf
```

`llama-coder`:

```text
Qwen3.5-9B-UD-Q4_K_XL.gguf
```

Обе модели работают с:

```text
ctx-size 40000
```

### GPU mapping

| GPU   | Использование           |
| ----- | ----------------------- |
| GPU 0 | часть `llama-architect` |
| GPU 1 | `llama-coder`, PCIe x16 |
| GPU 2 | часть `llama-architect` |

### Проверено

* `llama-architect` отвечает на порту `8080`;
* `llama-coder` отвечает на порту `8081`;
* Open WebUI работает на порту `3000`;
* короткие запросы работают;
* длинный лог работает;
* несколько файлов в контексте работают;
* обе модели работают с `ctx-size 40000`;
* контейнеры healthy;
* API `/v1/models` отвечает на обоих backend-ах.

### Измерения

Примерная скорость генерации:

| Модель        |            Скорость |
| ------------- | ------------------: |
| 27B architect | около `12 tokens/s` |
| 9B coder      | около `23 tokens/s` |

Примерное потребление VRAM после запуска:

| GPU   |                          VRAM |
| ----- | ----------------------------: |
| GPU 0 | около `9097–9121 / 10240 MiB` |
| GPU 1 | около `6269–6289 / 10240 MiB` |
| GPU 2 | около `9677–9701 / 10240 MiB` |

### Результат

Stage 1 считается завершённым.

Базовая inference-схема работает и пригодна для дальнейшего построения эксплуатационного слоя.

### Замечания

* 27B близко к пределу VRAM, особенно на GPU 2.
* Увеличивать контекст 27B выше `40000` не рекомендуется без отдельного теста.
* 9B имеет запас VRAM на GPU 1.
* Третью LLM-модель пока не добавлять.
* `llama-architect` пока может оставаться без `restart: unless-stopped`, чтобы не уходить в циклическую перезагрузку при ошибке.

---

## 2026-06-19 — LiteLLM Gateway added

### Изменено

Добавлен gateway-сервис:

* `litellm`

Порт:

* `4000`

Назначение:

* единая OpenAI-compatible точка входа;
* маршрутизация к локальным llama.cpp backend-ам;
* подготовка к Telegram;
* подготовка к CrewAI / OpenClaw;
* подготовка к IDE-клиентам;
* будущие API keys, логи и политики доступа.

Конфигурация

Созданы файлы:

* `.env`
* `.env.example`
* `config/litellm.config.yaml`

`.env` содержит реальные секреты и не хранится в git.

`.env.example` хранится в git как шаблон.

`config/litellm.config.yaml` содержит локальные модели:

Gateway model name	Backend
slowrig/coder	http://llama-coder:8080/v1
slowrig/architect	http://llama-architect:8080/v1

### Проверено

Проверено напрямую через LiteLLM:

* `/v1/models` на порту `4000`;
* chat request к `slowrig/coder`;
* chat request к `slowrig/architect`.

Также обновлён:

* `scripts/cluster-status.sh`

Теперь он проверяет:

* прямой backend `8080`;
* прямой backend `8081`;
* Open WebUI `3000`;
* LiteLLM `/v1/models` на `4000`;
* chat-запрос через LiteLLM к `slowrig/coder`;
* chat-запрос через LiteLLM к `slowrig/architect`.

### Результат

Stage 3 gateway baseline работает.

Open WebUI пока не обязан быть переключён на gateway. Прямой доступ к `8080` и `8081` оставлен для диагностики.

### Замечания

`cluster-status.sh` теперь делает реальные короткие LLM-запросы через gateway. Это подходит для ручной диагностики, но не должно использоваться как частый автоматический healthcheck.

---

## 2026-06-19 — Stage 2 documentation started

### Изменено

Созданы базовые документы:

* `docs/passport.md`;
* `docs/runbook.md`;
* `README.md`;
* `docs/architecture.md`;
* `docs/decisions.md`.

### Результат

Проект получил базовую документационную структуру:

| Файл                   | Назначение                   |
| ---------------------- | ---------------------------- |
| `README.md`            | быстрый вход                 |
| `docs/passport.md`     | описание текущего стенда     |
| `docs/runbook.md`      | эксплуатационные инструкции  |
| `docs/architecture.md` | целевая архитектура          |
| `docs/decisions.md`    | журнал архитектурных решений |

---

## 2026-06-19 — Git baseline created

### Изменено

В `/opt/llama-cluster` создан локальный git-репозиторий.

Добавлен `.gitignore`, исключающий:

* модели;
* кэш;
* runtime data;
* логи;
* секреты;
* временные файлы;
* рабочие директории агентов.

Создана baseline-копия:

```text
docker-compose.stage1-baseline.yaml
```

### Результат

Конфигурации, документация и скрипты теперь версионируются.

Модели и runtime data не попадают в git.

---

## 2026-06-19 — Added cluster status script

### Изменено

Создан скрипт:

```text
scripts/cluster-status.sh
```

### Назначение

Скрипт показывает:

* состояние Docker-контейнеров;
* состояние Compose-сервисов;
* GPU и VRAM;
* доступность API;
* доступность Open WebUI;
* последние подозрительные строки логов.

### Проверено

Команда:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

Показывает:

* `llama-architect` — healthy;
* `llama-coder` — healthy;
* `open-webui` — healthy;
* `8080 /v1/models` — OK;
* `8081 /v1/models` — OK;
* `3000` — OK.

### Замечания

Open WebUI пытался обращаться к Ollama на `host.docker.internal:11434`, хотя Ollama не используется.

Решение:

```text
ENABLE_OLLAMA_API=False
```

Также фильтр подозрительных логов был уточнён, чтобы не ловить лишний шум.

---

## 2026-06-19 — Current stable state

### Стабильная конфигурация

| Компонент                | Статус   |
| ------------------------ | -------- |
| Docker Compose           | работает |
| NVIDIA Container Toolkit | работает |
| llama.cpp CUDA server    | работает |
| 27B architect            | работает |
| 9B coder                 | работает |
| Open WebUI               | работает |
| Git baseline             | создан   |
| Passport                 | создан   |
| Runbook                  | создан   |
| Architecture doc         | создан   |
| Decisions log            | создан   |
| Status script            | создан   |

### Следующий этап

Завершить Stage 2 operational baseline:

* добавить `changelog.md`;
* обновить README ссылкой на changelog;
* проверить `git status`;
* закоммитить документацию.

После этого перейти к выбору следующего крупного слоя:

1. Gateway.
2. Memory.
3. Telegram.
4. Agents.
5. Monitoring.
