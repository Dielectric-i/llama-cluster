# slowrig AI Cluster — Passport v0.2

Дата актуализации: 2026-06-22
Статус: текущий фактический паспорт стенда

## 1. Назначение документа

Этот документ описывает фактическое состояние `slowrig AI Cluster`.

Passport отвечает на вопросы:

* что это за стенд;
* на каком хосте он работает;
* какое железо используется;
* какие базовые компоненты установлены;
* какие сервисы сейчас запущены;
* какие модели используются;
* какие порты и роли закреплены за сервисами;
* какие ограничения у текущего железа и baseline.

Passport не заменяет:

* `README.md` — быстрый вход и индекс документации;
* `docs/runbook.md` — эксплуатационные команды, диагностика, rollback;
* `docs/architecture.md` — текущая и целевая архитектура;
* `docs/gateway.md` — подробности LiteLLM Gateway;
* `docs/decisions.md` — причины архитектурных решений;
* `docs/changelog.md` — историю фактических изменений и проверок.

---

## 2. Назначение стенда

`slowrig` — домашний локальный ИИ-кластер для разработки, анализа кода, работы с документацией, длительных аудитов, локальных ассистентов и будущих агентских сценариев.

Кластер предназначен для:

* локального inference LLM-моделей;
* работы с кодом и логами;
* подготовки контекста для тяжёлых моделей;
* ручной работы через Open WebUI;
* будущего подключения Telegram bot;
* будущего подключения IDE-клиентов;
* будущего подключения агентского слоя;
* будущей системы памяти/RAG.

Главный принцип:

```text
локальные модели
локальные Docker-сервисы
доступ из домашней сети
единая OpenAI-compatible точка входа через LiteLLM
```

---

## 3. Хост

| Параметр               | Значение                                 |
| ---------------------- | ---------------------------------------- |
| Hostname               | `slowrig`                                |
| LAN IP                 | `192.168.1.6`                            |
| OS                     | Ubuntu Server 26.04                      |
| Boot mode              | Legacy / CSM                             |
| Main project directory | `/opt/llama-cluster`                     |
| Main compose file      | `/opt/llama-cluster/docker-compose.yaml` |

Основные директории:

```text
/opt/llama-cluster/models
/opt/llama-cluster/cache
/opt/llama-cluster/config
/opt/llama-cluster/docs
/opt/llama-cluster/scripts
```

---

## 4. Аппаратная конфигурация

Материнская плата:

```text
X99-P4 Chinese/refurb board
```

GPU:

| Host GPU index | Модель                | Подключение                      | Текущая роль            |
| -------------- | --------------------- | -------------------------------- | ----------------------- |
| GPU 0          | NVIDIA P102-100 10 GB | ограниченная PCIe-линия / райзер | часть `llama-architect` |
| GPU 1          | NVIDIA P102-100 10 GB | PCIe x16                         | `llama-coder`           |
| GPU 2          | NVIDIA P102-100 10 GB | ограниченная PCIe-линия / райзер | часть `llama-architect` |

Суммарная VRAM:

```text
30 GB
```

Ключевые аппаратные ограничения:

* GPU 0 и GPU 2 подключены через ограниченные PCIe-линии;
* GPU 1 является самой быстрой картой по PCIe и закреплена за fast worker;
* 27B-модель требует split по двум GPU;
* 27B близко к пределу VRAM на текущей конфигурации;
* текущая платформа не должна рассматриваться как enterprise-grade сервер.

---

## 5. Программная база

Установлено и проверено:

| Компонент                | Версия / статус                          |
| ------------------------ | ---------------------------------------- |
| NVIDIA Driver            | `580.159.03`                             |
| CUDA на хосте            | `13.0`                                   |
| Docker                   | `29.1.3`                                 |
| NVIDIA Container Toolkit | установлен и проверен                    |
| Docker NVIDIA runtime    | работает                                 |
| llama.cpp image          | `ghcr.io/ggml-org/llama.cpp:server-cuda` |
| Open WebUI image         | `ghcr.io/open-webui/open-webui:main`     |
| LiteLLM                  | используется как gateway                 |

Проверка NVIDIA Container Toolkit уже выполнена: CUDA runtime контейнер видит все три GPU.

---

## 6. Текущая схема сервисов

Текущая основная цепочка:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

Сервисы:

| Сервис            | Host port | Роль                                   |
| ----------------- | --------: | -------------------------------------- |
| `open-webui`      |    `3000` | ручной WebUI через gateway             |
| `litellm`         |    `4000` | LLM Gateway / Router                   |
| `llama-architect` |    `8080` | 27B architect / deep reasoning backend |
| `llama-coder`     |    `8081` | 9B coder / fast worker backend         |
| `memory-db`       |     нет    | PostgreSQL + pgvector Memory DB        |
| `memory-embed`    |    `4010` | local embedding runtime, loopback only |

Прямые backend-порты `8080` и `8081` оставлены для диагностики. Обычный клиентский путь должен идти через LiteLLM Gateway.

`memory-db` не публикует host-port и доступен только внутри Docker Compose network.

`memory-embed` публикуется только на `127.0.0.1:4010` для локального ingestion-скрипта и не должен быть доступен из LAN.

---

## 7. `llama-architect`

Роль:

```text
27B architect / deep reasoning backend
```

Host endpoint:

```text
http://192.168.1.6:8080/v1
```

Docker service:

```text
llama-architect
```

Модель:

```text
Qwen3.6-27B-UD-Q4_K_XL.gguf
```

GPU assignment:

```text
host GPU 0 + host GPU 2
```

Контекст:

```text
ctx-size 60000
```

Распределение GPU:

```text
tensor-split 1.06,1
```

Назначение:

* сложные архитектурные решения;
* глубокий DevOps/ML-анализ;
* ревью сложного кода;
* анализ больших задач;
* финальные выводы после подготовки контекста;
* планирование будущих agent workflows.

Особенности текущего baseline:

* использует две GPU на ограниченных PCIe-линиях;
* работает с `parallel 1`;
* предназначен для качества reasoning, а не для минимальной задержки;
* имеет небольшой запас VRAM;
* не должен использоваться как default backend для частых мелких запросов.

---

## 8. `llama-coder`

Роль:

```text
9B coder / fast worker backend
```

Host endpoint:

```text
http://192.168.1.6:8081/v1
```

Docker service:

```text
llama-coder
```

Модель:

```text
Qwen3.5-9B-UD-Q4_K_XL.gguf
```

GPU assignment:

```text
host GPU 1
```

Контекст:

```text
ctx-size 128000
```

Назначение:

* быстрые ответы;
* предварительный анализ файлов;
* суммаризация;
* разбор логов;
* простые coding-задачи;
* подготовка контекста для 27B;
* будущий default backend для Telegram;
* будущий fast worker для агентских сценариев.

Особенности текущего baseline:

* работает на самой быстрой GPU в PCIe x16;
* имеет больший запас VRAM, чем 27B backend;
* является основной моделью для частых коротких задач.

---

## 9. `litellm`

Роль:

```text
LLM Gateway / Router
```

Host endpoint:

```text
http://192.168.1.6:4000/v1
```

Docker service:

```text
litellm
```

Текущие gateway model names:

| Gateway model name  | Backend           | Роль               |
| ------------------- | ----------------- | ------------------ |
| `slowrig/coder`     | `llama-coder`     | 9B fast worker     |
| `slowrig/architect` | `llama-architect` | 27B deep reasoning |

Файлы:

```text
/opt/llama-cluster/config/litellm.config.yaml
/opt/llama-cluster/.env
/opt/llama-cluster/.env.example
```

Фактический статус:

* LiteLLM запущен отдельным контейнером;
* порт `4000` опубликован на хосте;
* `/v1/models` через LiteLLM отвечает;
* `slowrig/coder` отвечает через LiteLLM;
* `slowrig/architect` отвечает через LiteLLM;
* Open WebUI подключён к LiteLLM.

Ограничения текущего gateway baseline:

* routing выполняется по явно выбранному model name;
* автоматический выбор модели по сложности задачи не реализован;
* pipeline `9B -> 27B` не реализован;
* LiteLLM не является memory/RAG-слоем;
* LiteLLM не является agent framework.

---

## 10. `open-webui`

Роль:

```text
ручной WebUI через LiteLLM Gateway
```

Web endpoint:

```text
http://192.168.1.6:3000
```

Docker service:

```text
open-webui
```

Текущий режим:

* Open WebUI подключён к LiteLLM Gateway;
* Open WebUI не должен напрямую обращаться к backend-ам в нормальном режиме;
* авторизация может быть отключена для домашнего LAN-тестирования;
* внешний доступ без отдельной защиты запрещён.

Текущая routing-конфигурация по смыслу:

```text
OPENAI_API_BASE_URLS -> http://litellm:4000/v1
OPENAI_API_KEYS      -> LITELLM_MASTER_KEY
```

Реальные секреты должны храниться только в `.env`.

---

## 11. Модели

Модели хранятся в:

```text
/opt/llama-cluster/models
```

Текущие модели:

| Назначение    | Файл модели                   | Сервис            |
| ------------- | ----------------------------- | ----------------- |
| 27B architect | `Qwen3.6-27B-UD-Q4_K_XL.gguf` | `llama-architect` |
| 9B coder      | `Qwen3.5-9B-UD-Q4_K_XL.gguf`  | `llama-coder`     |
| embeddings    | `embeddings/Qwen3-Embedding-0.6B-Q8_0.gguf` | `memory-embed` |

Текущие контексты:

| Сервис | Контекст |
| --- | ---: |
| `llama-architect` | `ctx-size 60000` |
| `llama-coder` | `ctx-size 128000` |

Третья LLM-модель в текущем baseline не добавлена.

---

## 12. Проверенный baseline

Stage 1 inference baseline завершён.

Проверено пользователем:

* `llama-architect` стартует;
* `llama-coder` стартует;
* Open WebUI работает;
* 27B отвечает на порту `8080`;
* 9B отвечает на порту `8081`;
* короткие запросы работают;
* длинные логи работают;
* несколько файлов в контексте работают;
* `llama-architect` работает с `ctx-size 60000` и `tensor-split 1.06,1`;
* `llama-coder` работает с `ctx-size 128000`.

Stage 2 operational baseline завершён.

Фактически создано:

* документация;
* git baseline;
* runbook;
* passport;
* architecture doc;
* decisions log;
* changelog;
* `scripts/cluster-status.sh`;
* baseline compose copy.

Stage 3 gateway baseline завершён.

Фактически внедрено:

* LiteLLM Gateway;
* model names `slowrig/coder` и `slowrig/architect`;
* Open WebUI routed through LiteLLM;
* direct backend ports сохранены для диагностики;
* `cluster-status.sh` проверяет gateway и обе модели.

---

## 13. Измерения baseline

Измеренная примерная скорость генерации:

| Backend           | Модель |  Примерная скорость |
| ----------------- | ------ | ------------------: |
| `llama-architect` | 27B    | около `12 tokens/s` |
| `llama-coder`     | 9B     | около `23 tokens/s` |

Типичное потребление VRAM после запуска:

| Host GPU | Роль                    |                Примерная VRAM |
| -------- | ----------------------- | ----------------------------: |
| GPU 0    | часть `llama-architect` | около `9097–9121 / 10240 MiB` |
| GPU 1    | `llama-coder`           | около `6269–6289 / 10240 MiB` |
| GPU 2    | часть `llama-architect` | около `9677–9701 / 10240 MiB` |

Эти значения являются ориентиром, а не строгой гарантией.

Выводы по baseline:

* inference-схема работает;
* распределение GPU подтверждено;
* 27B близко к пределу VRAM;
* 9B имеет больший запас VRAM;
* текущая пара моделей покрывает роли deep reasoning и fast worker;
* добавление третьей LLM требует отдельного решения и теста.

---

## 14. Репозиторий и файлы

Основная директория проекта:

```text
/opt/llama-cluster
```

Основные файлы:

```text
docker-compose.yaml
docker-compose.stage1-baseline.yaml
README.md
AGENTS.md
.env.example
config/litellm.config.yaml
scripts/cluster-status.sh
```

Локальный файл секретов:

```text
.env
```

`.env` не должен попадать в git.

Документация находится в:

```text
docs/
```

Актуальный индекс документации находится в:

```text
README.md
```

---

## 15. Данные, кэш и секреты

Не хранить в git:

```text
.env
models/
cache/
data/
logs/
secrets/
```

Можно хранить в git:

```text
README.md
AGENTS.md
docs/
scripts/
config/ без реальных секретов
.env.example
docker-compose.yaml
docker-compose.stage1-baseline.yaml
```

Реальные значения секретов не должны печататься в документации, логах задач или commit messages.

---

## 16. Текущая security posture

Текущий режим:

```text
home LAN baseline
```

Фактическое состояние:

* Open WebUI используется в домашней сети;
* LiteLLM используется как gateway;
* прямые backend-порты оставлены для диагностики;
* реальные секреты хранятся в `.env`;
* `.env` не должен коммититься.

Не открывать наружу без отдельного security stage:

```text
3000  # Open WebUI
4000  # LiteLLM Gateway
8080  # llama-architect direct backend
8081  # llama-coder direct backend
```

Для внешнего доступа позже нужен отдельный проектный этап с одним из вариантов:

* VPN;
* reverse proxy с авторизацией;
* firewall restrictions;
* gateway/API-key policy;
* включённая авторизация WebUI.

---

## 17. Что не описывает этот документ

Этот документ намеренно не содержит:

* подробные диагностические команды;
* инструкции restart/rollback;
* подробный LiteLLM config;
* ADR-обоснования;
* roadmap будущих stage;
* дизайн памяти/RAG;
* дизайн Telegram bot;
* дизайн agent framework;
* мониторинг и backup-политику.

Эти темы должны жить в отдельных документах:

```text
docs/runbook.md
docs/gateway.md
docs/decisions.md
docs/architecture.md
docs/memory.md
docs/telegram.md
docs/agents.md
docs/monitoring.md
docs/backups.md
```

Если одна из этих тем становится реальным subsystem, для неё нужно создать или обновить отдельный документ в `docs/`.
