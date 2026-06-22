# slowrig AI Cluster — Architecture v0.2

Дата актуализации: 2026-06-22
Статус: текущая и целевая архитектура проекта

## 1. Назначение документа

Этот документ описывает программную архитектуру `slowrig AI Cluster`.

`architecture.md` отвечает на вопросы:

* какие архитектурные слои уже есть;
* какие слои должны появиться позже;
* как должны взаимодействовать WebUI, gateway, модели, память, Telegram, IDE и агенты;
* какие компоненты нельзя смешивать в один слой;
* какие направления развития являются следующими;
* какие subsystem-ы требуют отдельного design stage перед внедрением.

Этот документ не заменяет:

* `README.md` — быстрый вход и индекс документации;
* `docs/passport.md` — фактический паспорт стенда;
* `docs/runbook.md` — эксплуатационные команды, диагностика и rollback;
* `docs/gateway.md` — подробности LiteLLM Gateway;
* `docs/decisions.md` — архитектурные решения и причины;
* `docs/changelog.md` — фактическую историю изменений и проверок.

---

## 2. Текущий архитектурный статус

Текущий baseline:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

Статус этапов:

| Stage   | Название                        | Статус                 |
| ------- | ------------------------------- | ---------------------- |
| Stage 1 | Inference baseline              | завершён               |
| Stage 2 | Operational foundation          | завершён               |
| Stage 3 | Gateway baseline                | завершён               |
| Stage 4 | Memory / RAG                    | Stage 4.4 Local RAG ingestion внедрён и проверен |
| Stage 5 | Telegram bot                    | runtime code/config добавлены; Telegram UI validation pending |
| Stage 6 | Agent framework                 | запланировано          |
| Stage 7 | Monitoring / Security / Backups | запланировано          |

Ключевое текущее состояние:

* локальные llama.cpp backend-и работают;
* LiteLLM внедрён как gateway;
* Open WebUI подключён через LiteLLM;
* прямые backend-порты сохранены для диагностики;
* memory/RAG foundation внедрён, local docs ingestion добавлен;
* Telegram bot runtime code/config добавлены, запуск ждёт real secrets и ручную Telegram UI проверку;
* agent framework ещё не внедрён;
* полноценный monitoring/security/backups stage ещё не внедрён.

---

## 3. Главный архитектурный принцип

`slowrig` не должен быть просто набором контейнеров с моделями.

Целевая архитектура должна разделять роли:

```text
User Interfaces
        |
        v
Gateway / Router
        |
        v
Inference Backends
        |
        +--> Memory / RAG
        +--> Tools
        +--> Agent State
        +--> Logs / Monitoring
```

Каждый слой должен иметь понятную ответственность.

Правило:

```text
один слой — одна основная роль
```

Нельзя превращать gateway, WebUI, Telegram bot или agent framework в “комбайн”, который одновременно отвечает за routing, память, shell-доступ, хранение истории, мониторинг и безопасность.

---

## 4. Текущая реализованная схема

```text
                        +----------------+
                        |   Open WebUI   |
                        |   port 3000    |
                        +--------+-------+
                                 |
                                 v
                        +--------+-------+
                        | LiteLLM Gateway|
                        |   port 4000    |
                        +--------+-------+
                                 |
              +------------------+------------------+
              |                                     |
              v                                     v
      +-------+--------+                    +-------+----------+
      | llama-coder    |                    | llama-architect  |
      | 9B / fast      |                    | 27B / deep       |
      | host port 8081 |                    | host port 8080   |
      +----------------+                    +------------------+
```

Текущие gateway model names:

```text
slowrig/coder
slowrig/architect
```

Обычный клиентский путь:

```text
Client -> LiteLLM Gateway -> llama.cpp backend
```

Диагностический путь:

```text
Client -> direct backend port 8080/8081
```

Прямой доступ к `8080` и `8081` нужен для диагностики, но не должен становиться нормальным клиентским путём.

---

## 5. Архитектурные слои

## 5.1 User Interface layer

Назначение:

* дать пользователю удобный способ взаимодействия с моделями;
* не хранить критическую архитектурную логику;
* не знать внутренние детали backend-ов без необходимости.

Текущий реализованный интерфейс:

```text
Open WebUI
```

Будущие интерфейсы:

```text
Telegram bot
IDE / coding assistant
custom scripts
agent control UI
```

Правило:

```text
User-facing clients should use LiteLLM Gateway.
```

Исключение:

```text
direct backend access is allowed for diagnostics and rollback only.
```

---

## 5.2 Gateway / Router layer

Текущая реализация:

```text
LiteLLM Proxy
```

Назначение gateway:

* единая OpenAI-compatible точка входа;
* стабильные public model names;
* routing к `llama-coder` и `llama-architect`;
* подготовка к Telegram, IDE и агентам;
* будущие API keys, access policies, logs, limits и queues.

Текущий routing:

```text
manual model selection by model name
```

Реализованные model names:

```text
slowrig/coder
slowrig/architect
```

Не реализовано:

```text
automatic task classification
9B -> 27B pipeline
slowrig/default alias
slowrig/fast alias
slowrig/deep alias
gateway-level memory
agent orchestration
```

Подробности gateway baseline находятся в:

```text
docs/gateway.md
```

---

## 5.3 Inference backend layer

Текущий backend engine:

```text
llama.cpp server
```

Текущие backend-и:

| Backend           | Роль                       | Нормальный доступ         |
| ----------------- | -------------------------- | ------------------------- |
| `llama-coder`     | fast worker / coder        | через `slowrig/coder`     |
| `llama-architect` | deep reasoning / architect | через `slowrig/architect` |

Архитектурная роль `llama-coder`:

* быстрые ответы;
* короткие и средние задачи;
* первичный анализ;
* summaries;
* подготовка контекста;
* будущий default backend для Telegram;
* future fast worker для агентов.

Архитектурная роль `llama-architect`:

* сложные решения;
* deep reasoning;
* архитектура;
* ревью сложного кода;
* анализ больших задач;
* финальные выводы после подготовки контекста.

Точные модели, GPU mapping, VRAM и host details фиксируются в:

```text
docs/passport.md
```

Эксплуатационные проверки и диагностика фиксируются в:

```text
docs/runbook.md
```

---

## 5.4 Memory / RAG layer

Статус:

```text
DB foundation implemented; local docs ingestion added in Stage 4.4
```

Текущий stage:

```text
Stage 4.4 — Local RAG ingestion
```

Основной документ:

```text
docs/memory.md
```

Memory/RAG слой должен быть отдельным subsystem-ом, а не побочным эффектом Open WebUI, LiteLLM или Telegram bot.

Будущая память должна уметь работать с разными типами данных:

* проектная документация;
* решения и changelog;
* summaries репозиториев;
* результаты аудитов;
* history задач;
* agent state;
* полезные фрагменты логов;
* пользовательские заметки;
* searchable context для RAG.

Принято для планирования:

```text
PostgreSQL + pgvector
```

Stage 4.3 добавляет минимальный DB foundation:

```text
service: memory-db
image: pgvector/pgvector:0.8.3-pg17
network: Docker Compose network only
host port: none by default
volume: memory-db-data
first corpus: README.md, AGENTS.md, docs/*.md
```

Stage 4.4 добавляет локальный embedding service и ingestion pipeline:

```text
service: memory-embed
runtime: llama.cpp server, CPU-only
host binding: 127.0.0.1:4010 only
model: Qwen3-Embedding-0.6B-Q8_0.gguf
script: scripts/memory-ingest-docs.py
corpus: README.md, AGENTS.md, docs/*.md
```

На Stage 4.4 не добавляются retrieval API, Telegram bot, agent framework, индексация чатов, raw logs или secrets.

---

## 5.5 Tools layer

Статус:

```text
not implemented as separate subsystem
```

Tools layer нужен, чтобы будущие агенты и интерфейсы могли безопасно выполнять ограниченные действия.

Потенциальные tools:

* чтение файлов проекта;
* подготовка summaries;
* запуск безопасных diagnostic commands;
* чтение логов;
* проверка состояния Docker;
* чтение git diff;
* создание patch-файлов;
* генерация changelog/report.

Опасные tools не должны быть доступны без подтверждения пользователя:

* shell commands;
* Docker restart/down;
* изменение compose;
* изменение firewall;
* удаление volume/cache;
* изменение `.env`;
* изменение GPU/model config;
* установка пакетов;
* pull новых образов.

Правило:

```text
dangerous actions require explicit human approval
```

---

## 5.6 Agent layer

Статус:

```text
runtime code/config added; Telegram UI validation pending
```

Будущий agent layer должен использовать gateway, memory и tools, а не обращаться хаотично к backend-ам напрямую.

Правильная схема:

```text
Agent
  |
  v
LiteLLM Gateway
  |
  +--> slowrig/coder
  +--> slowrig/architect
  |
  +--> Memory / RAG
  +--> Tools
  +--> Task State
```

Возможные варианты:

* CrewAI;
* OpenClaw;
* custom lightweight orchestrator;
* repo-auditor;
* documentation worker;
* code-review worker;
* ops diagnostic worker.

До внедрения нужен отдельный документ:

```text
docs/agents.md
```

Agent design должен определить:

* какие агенты нужны;
* какие модели они используют;
* какие tools им доступны;
* где хранится task state;
* как фиксируются результаты;
* какие действия требуют подтверждения;
* как выполняется rollback;
* как предотвращается uncontrolled shell autonomy.

---

## 5.7 Telegram layer

Статус:

```text
design documented; runtime not implemented
```

Telegram bot должен быть отдельным интерфейсом, а не заменой Open WebUI и не agent framework.

Базовая целевая схема:

```text
Telegram
   |
   v
Telegram Bot Service
   |
   v
LiteLLM Gateway
   |
   +--> slowrig/coder by default
   +--> slowrig/architect for deep tasks
```

Правило по умолчанию:

```text
Telegram -> slowrig/coder
```

`slowrig/architect` использовать только для:

* явно запрошенного глубокого анализа;
* сложных задач;
* архитектурных решений;
* сценариев, где 9B подготовила summary и нужна финальная проверка 27B.

Telegram bot не должен иметь произвольный shell-доступ.

До внедрения нужен отдельный документ:

```text
docs/telegram.md
```

Stage 5 design принимает первый вариант:

```text
Telegram Bot API polling + whitelist -> LiteLLM Gateway -> slowrig/coder
```

`slowrig/architect` использовать только по явной команде или документированному escalation rule. Telegram history не хранить в Memory/RAG до отдельного privacy/security decision.

---

## 5.8 Monitoring layer

Статус:

```text
partially manual, not a full monitoring stack
```

Текущий ручной уровень:

```text
scripts/cluster-status.sh
docker ps
docker compose ps
nvidia-smi
docker logs
curl checks
```

Важно:

```text
cluster-status.sh делает реальные короткие LLM-запросы.
```

Он подходит для ручной диагностики, но не должен использоваться как частый автоматический healthcheck.

Будущий monitoring stage может включать:

* lightweight health script;
* Prometheus;
* Grafana;
* Loki;
* node_exporter;
* NVIDIA/DCGM exporter;
* alerting;
* log rotation;
* retention policy.

До внедрения нужен отдельный документ:

```text
docs/monitoring.md
```

---

## 5.9 Security layer

Статус:

```text
LAN baseline
```

Текущий baseline рассчитан на домашнюю сеть.

Не открывать наружу без отдельного security stage:

```text
3000  # Open WebUI
4000  # LiteLLM Gateway
8080  # llama-architect direct backend
8081  # llama-coder direct backend
```

Будущий security hardening может включать:

* VPN;
* reverse proxy;
* auth для Open WebUI;
* gateway API key policy;
* firewall restrictions;
* закрытие direct backend-портов;
* Telegram user whitelist;
* secrets rotation;
* audit logs;
* backup encryption.

До внедрения нужен отдельный документ:

```text
docs/security.md
```

---

## 5.10 Backup layer

Статус:

```text
not implemented as a documented subsystem
```

Backup должен быть отдельным stage, потому что разные данные требуют разной стратегии.

Что нужно учитывать:

* compose/config/docs/scripts;
* `.env` и секреты;
* GGUF-модели;
* Open WebUI data;
* memory DB;
* future agent state;
* logs;
* changelog and decisions.

До внедрения нужен отдельный документ:

```text
docs/backups.md
```

---

## 6. Целевая архитектура

Целевая схема после появления memory, Telegram и agents:

```text
                         +-------------------+
                         |     User / IDE    |
                         +---------+---------+
                                   |
              +--------------------+--------------------+
              |                    |                    |
              v                    v                    v
        +-----+------+       +-----+------+       +-----+------+
        | Open WebUI |       | Telegram   |       | Agents     |
        +-----+------+       +-----+------+       +-----+------+
              |                    |                    |
              +--------------------+--------------------+
                                   |
                                   v
                         +---------+---------+
                         |  LiteLLM Gateway  |
                         |  routing / keys   |
                         +---------+---------+
                                   |
              +--------------------+--------------------+
              |                                         |
              v                                         v
       +------+-------+                         +-------+------+
       | llama-coder  |                         | llama-architect |
       | 9B / fast    |                         | 27B / deep      |
       +------+-------+                         +-------+------+
              |                                         |
              +--------------------+--------------------+
                                   |
                   +---------------+---------------+
                   |                               |
                   v                               v
            +------+-------+               +-------+------+
            | Memory / RAG |               | Tools / State |
            +--------------+               +--------------+
```

Архитектурная идея:

```text
interfaces are replaceable
gateway is the stable entry point
models are backend resources
memory is separate from chat UI
agents use controlled tools
dangerous operations require approval
```

---

## 7. Сценарии использования

### 7.1 Open WebUI

Статус:

```text
implemented
```

Схема:

```text
User -> Open WebUI -> LiteLLM -> slowrig/coder or slowrig/architect
```

Назначение:

* ручное тестирование;
* интерактивная работа;
* сравнение 9B и 27B;
* проверка gateway baseline.

---

### 7.2 Direct backend diagnostics

Статус:

```text
implemented for diagnostics
```

Схема:

```text
User/admin -> 8080 -> llama-architect
User/admin -> 8081 -> llama-coder
```

Назначение:

* отличить проблему LiteLLM от проблемы backend-а;
* проверить llama.cpp backend напрямую;
* выполнить rollback/diagnostic сценарии.

Не использовать как нормальный client path.

---

### 7.3 Telegram

Статус:

```text
design documented; runtime not implemented
```

Схема:

```text
Telegram -> Bot -> LiteLLM -> slowrig/coder / slowrig/architect
```

Default:

```text
Telegram -> slowrig/coder
```

Требуется отдельный design stage.

---

### 7.4 IDE assistant

Статус:

```text
planned
```

Схема:

```text
IDE -> LiteLLM -> slowrig/coder / slowrig/architect
```

Возможные сценарии:

* code completion-like tasks;
* code review;
* summaries;
* local project Q&A;
* file-aware assistant через future memory/RAG.

Требуется отдельное решение по клиенту и security/access policy.

---

### 7.5 Agent workflow

Статус:

```text
planned
```

Схема:

```text
User task
  -> Agent layer
  -> LiteLLM Gateway
  -> 9B / 27B
  -> Memory / Tools
  -> Report / Patch / Approval
```

Агенты не должны выполнять опасные действия без подтверждения пользователя.

---

## 8. Что нельзя смешивать

## 8.1 Gateway не должен становиться memory layer

LiteLLM должен маршрутизировать запросы и быть точкой входа.

Долгосрочная память, RAG, task state и embeddings должны проектироваться отдельно.

---

## 8.2 Telegram bot не должен становиться agent framework

Telegram — это интерфейс.

Он может запускать ограниченные команды или отправлять запросы, но не должен самостоятельно владеть всей логикой агентов, memory и shell-доступа.

---

## 8.3 Open WebUI не должен быть центром архитектуры

Open WebUI — удобный ручной интерфейс.

Архитектура должна сохраняться даже если Open WebUI заменить, отключить или использовать только для тестирования.

---

## 8.4 Agent layer не должен обходить gateway

Агенты должны использовать LiteLLM Gateway, чтобы сохранять единый routing, access policy и model naming.

Исключение — диагностика или явно задокументированный rollback.

---

## 8.5 Documentation не должна быть “после”

Документация является частью архитектуры.

Если меняется поведение, routing, service topology, security posture или operational workflow, должны обновляться соответствующие документы.

---

## 9. Порядок развития

## Stage 1 — Inference baseline

Статус:

```text
completed
```

Результат:

* `llama-architect` работает;
* `llama-coder` работает;
* Open WebUI работает;
* обе модели работают с большим контекстом;
* GPU mapping подтверждён;
* baseline измерения зафиксированы.

---

## Stage 2 — Operational foundation

Статус:

```text
completed
```

Результат:

* создан README;
* создан passport;
* создан runbook;
* создан architecture doc;
* создан decisions log;
* создан changelog;
* создан status script;
* создан git baseline;
* зафиксирован Stage 1 compose baseline.

---

## Stage 3 — Gateway baseline

Статус:

```text
completed
```

Результат:

* LiteLLM Gateway внедрён;
* Open WebUI подключён через LiteLLM;
* добавлены model names `slowrig/coder` и `slowrig/architect`;
* direct backend-порты сохранены для диагностики;
* `cluster-status.sh` проверяет gateway и обе модели.

---

## Stage 4 — Memory / RAG

Статус:

```text
Stage 4.4 Local RAG ingestion implemented and server-validated
```

Принятый порядок:

```text
Stage 4.1 — Memory / RAG design
Stage 4.2 — Memory implementation plan
Stage 4.3 — Memory DB foundation
Stage 4.4 — Local RAG ingestion
docs/memory.md
```

Текущее решение:

```text
PostgreSQL + pgvector is the first Memory DB stack.
Markdown + Git remain the source of truth.
PostgreSQL stores structured state, metadata, chunks and derived vector data.
```

Stage 4.3 добавил `memory-db` и bootstrap schema. Stage 4.4 добавляет `memory-embed` и ingestion script для первого документационного корпуса.

Stage 4 зафиксирован в:

```text
docs/memory.md
```

---

## Stage 5 — Telegram bot

Статус:

```text
planned
```

Первый шаг:

```text
docs/telegram.md
```

Цель:

* безопасный Telegram-интерфейс;
* whitelist пользователей;
* routing через LiteLLM;
* default route на `slowrig/coder`;
* controlled escalation к `slowrig/architect`;
* отсутствие произвольного shell-доступа.

Принятые defaults:

```text
polling, whitelist, LiteLLM Gateway, default slowrig/coder, explicit slowrig/architect, no shell
```

Stage 5.1 implementation plan выбирал минимальный runtime без отдельной Telegram framework/library. Stage 5.2 по запросу пользователя переделан на C#/.NET runtime с `HttpClient`.

Stage 5.2 добавляет `src/telegram-bot` и Compose service `telegram-bot` в profile `telegram`. Обычный `docker compose up -d` не стартует bot.

---

## Stage 6 — Agent framework

Статус:

```text
planned
```

Первый шаг:

```text
docs/agents.md
```

Цель:

* controlled agent workflows;
* planner/coder/reviewer/documentation roles;
* использование LiteLLM Gateway;
* использование future memory/RAG;
* tool permissions;
* human approval for risky actions.

---

## Stage 7 — Monitoring / Security / Backups

Статус:

```text
planned
```

Возможные design docs:

```text
docs/monitoring.md
docs/security.md
docs/backups.md
```

Цель:

* lightweight health checks;
* metrics/logging;
* backup/restore;
* external access strategy;
* auth and firewall rules;
* secrets handling;
* safe operational baseline.

---

## 10. Архитектурные приоритеты

Текущие приоритеты:

```text
стабильность > количество компонентов
понятность > автоматизация
наблюдаемость > скорость изменений
контроль > автономность
rollback > one-way migration
docs as source of truth > hidden chat context
```

На текущем этапе не добавлять новые тяжёлые сервисы без design stage.

Правильный порядок для нового subsystem-а:

```text
design doc
review
approval
small implementation
manual checks
docs update
changelog
```

---

## 11. Открытые архитектурные вопросы

## 11.1 Memory / RAG

Открытые вопросы:

* нужен ли encrypted/offline backup сразу?
* какой migration mechanism использовать для schema?
* нужен ли отдельный retrieval API поверх `memory-db`?
* какой prompt/context assembly формат нужен будущим клиентам?
* как удалять данные?
* как memory будет использоваться агентами?
* когда Qdrant понадобится как future upgrade path?

---

## 11.2 Gateway routing

Открытые вопросы:

* добавлять ли `slowrig/default`;
* добавлять ли `slowrig/fast`;
* добавлять ли `slowrig/deep`;
* где реализовывать routing по сложности задачи;
* должен ли pipeline `9B -> 27B` жить в gateway или agent layer.

Текущая позиция:

```text
routing по сложности лучше отложить до agent/memory design
```

---

## 11.3 Telegram

Открытые вопросы:

* точный pinned .NET base image для будущего runtime;
* нужен ли `/status` только для bot/gateway или ещё для memory;
* нужен ли `/rag` command для read-only поиска по документации;
* сколько short context хранить in-memory;
* какие limits ставить на длину входа и частоту запросов.

---

## 11.4 Agents

Открытые вопросы:

* CrewAI, OpenClaw или custom orchestrator;
* какие agent roles нужны;
* где хранить task state;
* какие tools разрешить;
* как оформлять approvals;
* где хранить результаты работы.

---

## 11.5 Security and external access

Открытые вопросы:

* VPN или reverse proxy;
* когда включать auth в Open WebUI;
* когда закрывать direct backend-порты;
* как управлять API keys;
* как делать secrets rotation;
* как аудитить внешние запросы.

---

## 12. Когда обновлять этот документ

Обновлять `docs/architecture.md`, если меняется:

* схема взаимодействия сервисов;
* основной client path;
* gateway role;
* model role;
* stage roadmap;
* subsystem boundaries;
* planned architecture;
* security posture на уровне архитектуры;
* решение о memory, Telegram, agents, monitoring или backups.

Если изменение является архитектурным решением, также обновить:

```text
docs/decisions.md
```

Если изменение фактически применено и проверено, также обновить:

```text
docs/changelog.md
```

Если изменение требует новых команд эксплуатации, также обновить:

```text
docs/runbook.md
```

Если появляется новый subsystem, создать отдельный документ в `docs/` и добавить его в индекс `README.md`.

---
