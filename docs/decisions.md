# slowrig AI Cluster — Architecture Decisions v0.2

Дата актуализации: 2026-06-23
Статус: актуальный журнал архитектурных решений

## 1. Назначение

Этот документ фиксирует ключевые архитектурные решения по `slowrig AI Cluster`.

Он отвечает на вопросы:

* какое решение принято;
* почему оно принято;
* какой компромисс выбран;
* когда решение можно пересмотреть;
* какие решения уже устарели или были заменены.

Документ нужен, чтобы будущие изменения не ломали уже проверенную архитектуру и чтобы новые участники/Codex понимали не только “что сделано”, но и “почему сделано именно так”.

Этот документ не заменяет:

* `README.md` — быстрый вход и индекс документации;
* `docs/passport.md` — фактический паспорт стенда;
* `docs/runbook.md` — эксплуатационные команды и rollback;
* `docs/architecture.md` — текущую и целевую архитектуру;
* `docs/gateway.md` — подробности LiteLLM Gateway;
* `docs/changelog.md` — фактическую историю изменений и проверок.

---

## 2. Формат ADR

Каждое решение оформляется как ADR:

```text
ADR-XXX — Название решения
Дата:
Статус:
Связанные документы:

Контекст:
Решение:
Причина:
Компромисс:
Когда пересмотреть:
```

Статусы:

| Статус               | Значение                                          |
| -------------------- | ------------------------------------------------- |
| `принято`            | решение действует                                 |
| `принято и внедрено` | решение действует и уже реализовано               |
| `временно принято`   | решение допустимо временно, но требует пересмотра |
| `открыто`            | решение ещё не принято                            |
| `superseded`         | решение заменено более новым ADR                  |
| `отклонено`          | вариант рассмотрен и не принят                    |

Старые ADR не удаляются, если они объясняют историю проекта. Если решение заменено, оно помечается как `superseded`, а новый ADR указывает, что именно он заменяет.

---

## 3. Индекс решений

| ADR     | Решение                                                 | Статус                |
| ------- | ------------------------------------------------------- | --------------------- |
| ADR-001 | Docker Compose как основной способ управления сервисами | принято               |
| ADR-002 | llama.cpp server как inference backend                  | принято               |
| ADR-003 | Разделить роли моделей на `architect` и `coder`         | принято               |
| ADR-004 | Запускать 27B на GPU 0 и GPU 2                          | принято               |
| ADR-005 | Запускать 9B на GPU 1 в PCIe x16                        | принято               |
| ADR-006 | Использовать `ctx-size 40000` для обеих моделей         | superseded by ADR-027 |
| ADR-007 | Не запускать третью LLM-модель на текущем этапе         | принято               |
| ADR-008 | Оставить `parallel 1` для 27B                           | принято               |
| ADR-009 | Временно оставить Open WebUI без авторизации            | временно принято      |
| ADR-010 | Не хранить модели, кэш, данные и секреты в git          | принято               |
| ADR-011 | Сначала документация и runbook, затем новые subsystem-ы | принято               |
| ADR-012 | Gateway нужен, но не добавлять его до Stage 2           | superseded by ADR-015 |
| ADR-013 | Memory stack пока не выбран                             | superseded by ADR-020 |
| ADR-014 | Не открывать внутренние порты наружу                    | принято               |
| ADR-015 | Использовать LiteLLM Proxy как первый gateway           | принято и внедрено    |
| ADR-016 | Маршрутизировать Open WebUI через LiteLLM               | принято и внедрено    |
| ADR-017 | Использовать стабильные gateway model names             | принято и внедрено    |
| ADR-018 | Оставить direct backend-порты для диагностики           | принято               |
| ADR-019 | Проектировать новые subsystem-ы до внедрения            | принято               |
| ADR-020 | Планировать PostgreSQL + pgvector как первый Memory stack | принято; реализовано в ADR-022 |
| ADR-021 | Зафиксировать roadmap defaults после Stage 4.1          | принято               |
| ADR-022 | Внедрить `memory-db` без host-port как Memory DB foundation | принято; config добавлен |
| ADR-023 | Использовать локальный `llama.cpp` embedding service для Stage 4.4 | принято и проверено |
| ADR-024 | Проектировать первый Telegram bot как polling + whitelist клиент LiteLLM | принято; design добавлен |
| ADR-025 | Реализовывать первый Telegram runtime без отдельной Telegram library | superseded by ADR-026 |
| ADR-026 | Реализовывать первый Telegram runtime на C#/.NET | принято; runtime validated |
| ADR-027 | Использовать разные context sizes для `architect` и `coder` | принято и внедрено |
| ADR-028 | Проектировать agent layer как custom lightweight orchestration | принято; design добавлен |
| ADR-029 | Начать agents с docs drift / repo patch assistant workflow | принято; plan добавлен |
| ADR-030 | Зафиксировать Stage 7 monitoring/security/backups defaults | принято; design добавлен |
| ADR-031 | Реализовать `cluster-health-lite.sh` как дешёвый health check | принято и внедрено |
| ADR-032 | Завершить Stage 6 read-only docs drift helper | принято и внедрено |

---

## ADR-001 — Использовать Docker Compose как основной способ управления сервисами

Дата: 2026-06-19
Статус: принято
Связанные документы: `README.md`, `docs/runbook.md`, `docs/passport.md`

### Контекст

`slowrig` — домашний локальный AI-кластер на одном сервере. На текущем этапе не требуется Kubernetes, Nomad или другой полноценный orchestrator.

### Решение

Все основные сервисы кластера запускать через Docker Compose.

### Причина

Docker Compose даёт:

* воспроизводимость;
* изоляцию сервисов;
* понятное описание инфраструктуры в YAML;
* возможность версионировать конфигурацию через git;
* простой запуск и остановку отдельных сервисов;
* понятное добавление будущих сервисов: gateway, memory, Telegram bot, monitoring.

### Компромисс

Docker добавляет слой абстракции. Для GPU-сервисов нужно внимательно следить за:

* NVIDIA Container Toolkit;
* Docker runtime;
* пробросом GPU;
* volume mounts;
* переменными окружения;
* фактическим соответствием compose-файла и реального состояния.

### Когда пересмотреть

Если появится несколько серверов, необходимость scheduler-а, HA, очередей задач на уровне инфраструктуры или другой production-like сценарий.

На текущем домашнем этапе Docker Compose остаётся правильным выбором.

---

## ADR-002 — Использовать llama.cpp server как inference backend

Дата: 2026-06-19
Статус: принято
Связанные документы: `docs/passport.md`, `docs/architecture.md`

### Контекст

Кластер использует локальные GGUF-модели на бюджетном нестандартном железе с тремя NVIDIA P102-100.

### Решение

Inference моделей выполнять через:

```text
ghcr.io/ggml-org/llama.cpp:server-cuda
```

### Причина

`llama.cpp` подходит для текущего стенда, потому что:

* поддерживает GGUF;
* достаточно лёгкий;
* умеет CUDA;
* даёт OpenAI-compatible API;
* позволяет явно управлять GPU offload;
* позволяет задавать multi-GPU split;
* хорошо подходит для ручного контроля на нестандартном железе.

### Компромисс

`llama.cpp` — это inference backend, а не полноценная платформа для:

* памяти;
* агентов;
* очередей;
* маршрутизации;
* access policies;
* long-term state;
* сложного RAG.

Эти функции должны быть вынесены в отдельные слои.

### Когда пересмотреть

Если появится железо с большим объёмом VRAM и потребуется:

* высокая параллельность;
* production-like serving;
* vLLM;
* SGLang;
* TensorRT-LLM;
* другой backend под конкретные модели.

---

## ADR-003 — Разделить роли моделей на `architect` и `coder`

Дата: 2026-06-19
Статус: принято
Связанные документы: `docs/passport.md`, `docs/architecture.md`, `docs/gateway.md`

### Контекст

На стенде доступны две основные модели разных размеров и разной скорости.

### Решение

Использовать две основные LLM-роли:

* `llama-architect` — тяжёлая 27B-модель для сложных решений;
* `llama-coder` — быстрая 9B-модель для частых рабочих задач.

### Причина

У разных задач разные приоритеты:

* для простых запросов важна скорость;
* для Telegram важна отзывчивость;
* для подготовки контекста важна стабильность;
* для сложных решений важно качество reasoning;
* для аудита кода и архитектуры нужна более сильная модель.

### Компромисс

Две модели усложняют маршрутизацию.

Нужен gateway/router, чтобы клиенты не работали напрямую с внутренними backend-ами и могли выбирать модель через стабильные model names.

### Когда пересмотреть

Если появится одна модель, которая одновременно достаточно быстрая и достаточно качественная для всех сценариев, либо если будет добавлен более мощный GPU.

---

## ADR-004 — Запускать 27B на GPU 0 и GPU 2

Дата: 2026-06-19
Статус: принято
Связанные документы: `docs/passport.md`, `docs/architecture.md`, `docs/runbook.md`

### Контекст

27B-модель не помещается на одну P102-100 10 GB.

GPU 0 и GPU 2 доступны для split-запуска, но подключены через ограниченные PCIe-линии/райзеры.

### Решение

`llama-architect` использует:

```text
host GPU 0 + host GPU 2
```

### Причина

Две карты дают около 20 GB VRAM, что позволяет запустить 27B-модель.

GPU 0 и GPU 2 хуже подходят для быстрой интерактивной модели из-за ограниченных PCIe-линий, но подходят для тяжёлой модели, где приоритет — качество reasoning, а не минимальная задержка.

### Компромисс

Медленные PCIe-линии ограничивают скорость и делают опасным интенсивный меж-GPU обмен.

Поэтому для 27B baseline важно сохранять:

```text
split-mode layer
parallel 1
```

### Когда пересмотреть

Если:

* изменится физическое расположение GPU;
* появится карта с большим объёмом VRAM;
* появится возможность держать 27B на одной GPU;
* будет доказана стабильность другого split-режима;
* пользователь решит пересобрать hardware layout.

---

## ADR-005 — Запускать 9B на GPU 1 в PCIe x16

Дата: 2026-06-19
Статус: принято
Связанные документы: `docs/passport.md`, `docs/architecture.md`

### Контекст

GPU 1 установлена в PCIe x16 и является самой подходящей картой для быстрой интерактивной модели.

### Решение

`llama-coder` использует:

```text
host GPU 1
```

### Причина

9B-модель должна быть самой быстрой и отзывчивой.

Она используется или будет использоваться для:

* коротких запросов;
* Telegram;
* предварительного анализа;
* суммаризации;
* подготовки данных для 27B;
* частых worker-задач;
* быстрых coding-задач.

Для этих задач важны latency и стабильность, поэтому используется самая быстрая PCIe-линия.

### Компромисс

GPU 1 остаётся занята 9B-моделью и не используется как часть 27B.

Это снижает суммарную VRAM для architect-модели, но делает архитектуру более гибкой и отзывчивой.

### Когда пересмотреть

Если:

* понадобится запускать 27B на трёх GPU;
* появится отдельная карта для fast worker;
* будет доказано, что 9B достаточно хорошо работает на ограниченной PCIe-линии;
* появится новая модель или новая роль, важнее текущей 9B.

---

## ADR-006 — Использовать `ctx-size 40000` для обеих моделей

Дата: 2026-06-19
Статус: superseded by ADR-027
Связанные документы: `docs/passport.md`, `docs/runbook.md`

### Контекст

Кластер предназначен не только для короткого чата, но и для больших задач с длинным контекстом.

### Решение

Обе модели работают с:

```text
ctx-size 40000
```

### Причина

Большой контекст нужен для:

* длинных логов;
* нескольких файлов;
* анализа документации;
* подготовки контекста;
* работы с репозиториями;
* сложных DevOps/ML-задач.

Проверено пользователем, что обе модели стартуют и работают с таким контекстом.

### Компромисс

Большой контекст увеличивает:

* потребление VRAM;
* время prompt processing;
* риск OOM на длинных задачах;
* задержку до первого токена.

Особенно близко к пределу находится 27B backend.

### Когда пересмотреть

Если:

* появятся OOM;
* prompt processing станет слишком долгим;
* понадобится отдельный быстрый профиль для Telegram;
* будет добавлена память/RAG, уменьшающая необходимость держать большой raw-context;
* появится задача с другим latency/throughput профилем.

---

## ADR-027 — Использовать разные context sizes для `architect` и `coder`

Дата: 2026-06-23
Статус: принято и внедрено
Заменяет: ADR-006
Связанные документы: `docker-compose.yaml`, `README.md`, `docs/passport.md`, `docs/changelog.md`

### Контекст

После Stage 5 runtime-проверок текущая конфигурация моделей показала стабильную работу с увеличенными context sizes и уточнённым распределением 27B по двум GPU.

### Решение

Текущий baseline:

```text
llama-architect: ctx-size 60000, tensor-split 1.06,1, parallel 1
llama-coder:     ctx-size 128000, parallel 1
```

### Причина

`llama-coder` используется как быстрый рабочий backend для Telegram, Open WebUI и обычных задач, где большой рабочий контекст полезен для логов, документации и кода.

`llama-architect` остаётся тяжёлым backend для сложных задач. Умеренное увеличение контекста до `60000` принято вместе с `tensor-split 1.06,1`, так как текущая серверная конфигурация работает стабильно с такими параметрами.

### Компромисс

Новый baseline потребляет больше VRAM и может увеличивать время prompt processing. Для дальнейших увеличений context size, изменения `tensor-split`, `parallel`, GPU mapping или model files нужен отдельный test plan.

### Когда пересмотреть

Если появятся:

* OOM или нестабильные рестарты;
* заметная деградация latency;
* ошибки на длинных prompts;
* новая модель или отдельный Telegram/agent профиль;
* решение закрыть direct backend ports или изменить GPU mapping.

---

## ADR-007 — Не запускать третью LLM-модель на текущем этапе

Дата: 2026-06-19
Статус: принято
Связанные документы: `docs/passport.md`, `docs/architecture.md`

### Контекст

Текущая связка уже использует все три GPU:

* 27B — GPU 0 + GPU 2;
* 9B — GPU 1.

На GPU 1 есть некоторый запас VRAM, но он важен для стабильности 9B и длинного контекста.

### Решение

Не добавлять третью LLM-модель в текущий baseline.

### Причина

Текущая пара уже покрывает основные роли:

* 9B — быстрый worker;
* 27B — тяжёлый architect.

Добавление третьей LLM увеличит сложность диагностики, расход VRAM и риск нестабильности.

### Компромисс

Не используется весь потенциальный остаток VRAM, но кластер остаётся проще, стабильнее и легче диагностируется.

### Когда пересмотреть

Если появится конкретная роль:

* embedding model;
* reranker;
* маленькая ultra-fast Telegram-модель;
* классификатор задач;
* STT/TTS;
* vision-модель;
* отдельная summary-модель.

Такое добавление должно быть отдельным stage с тестом VRAM, логов и rollback.

---

## ADR-008 — Оставить `parallel 1` для 27B

Дата: 2026-06-19
Статус: принято
Связанные документы: `docs/runbook.md`, `docs/passport.md`

### Контекст

27B использует две GPU через ограниченные PCIe-линии.

### Решение

`llama-architect` работает с:

```text
parallel 1
```

### Причина

Повышение параллельности может увеличить:

* расход VRAM;
* нагрузку на KV cache;
* меж-GPU обмен;
* задержки;
* риск CUDA allocation errors;
* риск нестабильности.

### Компромисс

Нельзя эффективно обслуживать несколько параллельных тяжёлых запросов к 27B.

### Когда пересмотреть

Если:

* будет доказана стабильность при `parallel 2`;
* появится gateway с очередями;
* будет больше VRAM;
* 27B переедет на более быстрые GPU;
* появится потребность в параллельной обработке именно на 27B.

---

## ADR-009 — Временно оставить Open WebUI без авторизации

Дата: 2026-06-19
Статус: временно принято
Связанные документы: `docs/runbook.md`, `docs/passport.md`, `docs/gateway.md`

### Контекст

Стенд находится в домашней LAN и используется для быстрой настройки и тестирования.

### Решение

Open WebUI может временно работать с отключённой авторизацией только в домашней сети.

### Причина

На этапе настройки это ускоряет ручное тестирование и снижает количество переменных при диагностике.

### Компромисс

Это небезопасно для внешнего доступа.

Порты нельзя открывать в интернет без отдельного security stage:

```text
3000  # Open WebUI
4000  # LiteLLM Gateway
8080  # llama-architect direct backend
8081  # llama-coder direct backend
```

### Когда пересмотреть

Перед любым удалённым доступом.

Варианты:

* включить авторизацию Open WebUI;
* использовать VPN;
* поставить reverse proxy с auth;
* ограничить доступ firewall-ом;
* использовать gateway/API-key policy;
* закрыть direct backend-порты снаружи.

---

## ADR-010 — Не хранить модели, кэш, данные и секреты в git

Дата: 2026-06-19
Статус: принято
Связанные документы: `README.md`, `AGENTS.md`, `docs/passport.md`

### Контекст

Проект использует локальный git-репозиторий для конфигов, документации и скриптов.

### Решение

В git хранить:

* compose-файлы;
* документацию;
* скрипты;
* конфигурации без секретов;
* safe-шаблоны вроде `.env.example`.

В git не хранить:

* GGUF-модели;
* cache;
* runtime data;
* логи;
* секреты;
* volume-данные;
* `.env`.

### Причина

Модели слишком большие, кэш и данные изменчивые, секреты нельзя хранить в репозитории.

### Компромисс

Git не является полным backup всего стенда.

Для моделей, Open WebUI data, memory DB и agent state нужен отдельный backup-план.

### Когда пересмотреть

Не пересматривать для моделей и секретов.

Для некоторых статичных конфигураций будущих сервисов можно добавлять отдельные safe-файлы, если в них нет приватных данных.

---

## ADR-011 — Сначала документация и runbook, затем новые subsystem-ы

Дата: 2026-06-19
Статус: принято
Связанные документы: `README.md`, `AGENTS.md`, `docs/runbook.md`, `docs/changelog.md`

### Контекст

После Stage 1 уже работала inference-база. Дальнейшее усложнение без документации могло сделать проект трудноуправляемым.

### Решение

После Stage 1 сначала создать эксплуатационную базу:

* `README.md`;
* `docs/passport.md`;
* `docs/runbook.md`;
* `docs/architecture.md`;
* `docs/decisions.md`;
* `docs/changelog.md`;
* git baseline;
* `scripts/cluster-status.sh`.

И только потом добавлять gateway, memory, Telegram и agents.

### Причина

Без документации и baseline любое усложнение превращает стенд в набор случайных контейнеров.

### Компромисс

На старте меньше автоматизации и меньше новых функций.

### Когда пересмотреть

Решение уже выполнено для Stage 2.

Принцип остаётся актуальным для будущих subsystem-ов: сначала design doc и rollback, потом внедрение.

---

## ADR-012 — Gateway должен стать центральной точкой входа, но не добавляется сразу

Дата: 2026-06-19
Статус: superseded by ADR-015
Связанные документы: `docs/gateway.md`, `docs/changelog.md`

### Контекст

После Stage 1 клиенты могли обращаться напрямую к llama.cpp backend-ам. Gateway был нужен, но его раннее добавление усложнило бы диагностику.

### Решение

На момент Stage 2 не добавлять gateway до завершения базовой документации и эксплуатационного слоя.

### Причина

Gateway понадобится для:

* маршрутизации 9B/27B;
* подключения Telegram;
* подключения IDE;
* подключения CrewAI/OpenClaw;
* логирования;
* ключей доступа;
* будущей памяти;
* ограничения прямого доступа к backend-ам.

Но добавление gateway до стабилизации базовой схемы усложнило бы диагностику.

### Компромисс

До Stage 3 маршрутизация могла выполняться вручную через Open WebUI или прямые API.

### Когда пересмотреть

Решение закрыто внедрением LiteLLM Proxy в ADR-015.

---

## ADR-013 — Memory stack пока не выбран

Дата: 2026-06-19
Статус: superseded by ADR-020
Связанные документы: `docs/architecture.md`, `docs/memory.md`

### Контекст

Кластеру нужна долгосрочная память/RAG, но перед выбором stack нужно понять типы данных, сценарии агентов, Telegram, backups и privacy boundaries.

### Решение

Пока не выбирать окончательно:

* PostgreSQL + pgvector;
* Qdrant;
* Chroma;
* LanceDB;
* hybrid stack.

На текущем этапе источником правды для проектных решений остаются Markdown + Git.

### Причина

Перед выбором memory stack нужно понять:

* какие данные будут храниться;
* что должно оставаться в Markdown;
* что должно быть структурированной БД;
* что должно быть embedded;
* где будет жить история задач;
* как память будет связана с gateway;
* какие агенты будут использовать память;
* как делать backup/restore.

### Компромисс

Пока нет полноценной автоматической долгосрочной памяти и RAG.

### Когда пересмотреть

Решение пересмотрено на Stage 4.1/4.2.

Текущий выбор для планирования зафиксирован в ADR-020:

```text
PostgreSQL + pgvector
```

Stage 4.2 остаётся documentation-only plan. Runtime implementation требует отдельного approval на Stage 4.3.

---

## ADR-014 — Не открывать внутренние порты наружу

Дата: 2026-06-19
Статус: принято
Связанные документы: `docs/runbook.md`, `docs/gateway.md`, `docs/passport.md`

### Контекст

Текущий режим рассчитан на домашнюю LAN.

Open WebUI может быть без авторизации, LiteLLM использует ключи, а direct backend-и не должны быть публичными endpoint-ами.

### Решение

Не открывать наружу без отдельного security stage:

```text
3000  # Open WebUI
4000  # LiteLLM Gateway
8080  # llama-architect direct backend
8081  # llama-coder direct backend
```

### Причина

Риски:

* WebUI без авторизации небезопасен в интернете;
* LiteLLM Gateway требует продуманной key/access policy;
* llama.cpp API не должны быть публичными напрямую;
* direct backend-и не имеют достаточного security layer;
* будущие Telegram/agent сервисы потребуют whitelist и access control.

### Компромисс

Удалённый доступ пока требует нахождения в LAN или отдельного безопасного канала.

### Когда пересмотреть

Когда будет выбран вариант удалённого доступа:

* VPN;
* reverse proxy;
* auth;
* firewall;
* gateway access policy;
* отдельный security hardening stage.

---

## ADR-015 — Использовать LiteLLM Proxy как первый gateway

Дата: 2026-06-19
Статус: принято и внедрено
Связанные документы: `docs/gateway.md`, `docs/changelog.md`

### Контекст

После Stage 2 нужен был gateway/router, чтобы клиенты не обращались напрямую к `llama-coder` и `llama-architect`.

Рассматривались варианты:

* LiteLLM Proxy;
* собственный FastAPI router;
* gateway внутри agent framework;
* оставить ручную маршрутизацию через Open WebUI.

### Решение

Использовать LiteLLM Proxy как первый LLM Gateway / Router для `slowrig`.

### Причина

LiteLLM:

* даёт OpenAI-compatible endpoint;
* может маршрутизировать запросы к локальным llama.cpp backend-ам;
* подходит как единая точка входа для Open WebUI;
* подходит для будущих Telegram, IDE и agent clients;
* проще, чем писать собственный router сразу;
* может быть заменён позже без уничтожения inference baseline.

### Компромисс

Появился дополнительный слой диагностики:

```text
Client -> LiteLLM -> llama.cpp -> GPU
```

Если запрос не проходит, нужно отдельно проверять:

* клиента;
* gateway;
* backend mapping;
* llama.cpp backend;
* GPU/VRAM.

### Когда пересмотреть

Если LiteLLM окажется:

* слишком тяжёлым;
* нестабильным;
* неудобным для будущей памяти/агентов;
* недостаточно гибким для нужных routing policies.

В этом случае можно рассмотреть собственный FastAPI router.

---

## ADR-016 — Маршрутизировать Open WebUI через LiteLLM

Дата: 2026-06-19
Статус: принято и внедрено
Связанные документы: `docs/gateway.md`, `docs/runbook.md`, `docs/changelog.md`

### Контекст

До Stage 3 Open WebUI мог обращаться напрямую к backend-ам.

Целевая архитектура требует, чтобы ordinary clients использовали gateway.

### Решение

Open WebUI подключается к LiteLLM Gateway:

```text
http://litellm:4000/v1
```

Вместо прямого подключения к:

```text
http://llama-coder:8080/v1
http://llama-architect:8080/v1
```

### Причина

Это приближает архитектуру к целевой схеме, где все клиенты используют единую точку входа:

```text
Client -> LiteLLM Gateway -> llama.cpp backend
```

### Компромисс

Open WebUI теперь зависит от LiteLLM.

Если gateway сломается, WebUI перестанет видеть модели, даже если прямые backend-и работают.

### Механизм отката

В `docker-compose.yaml` можно вернуть direct backend mode:

```yaml
- OPENAI_API_BASE_URLS=http://llama-coder:8080/v1;http://llama-architect:8080/v1
- OPENAI_API_KEYS=dummy;dummy
```

Этот режим является rollback path, а не предпочтительной архитектурой.

### Когда пересмотреть

Если LiteLLM будет нестабилен или появится другой gateway.

---

## ADR-017 — Использовать стабильные gateway model names

Дата: 2026-06-22
Статус: принято и внедрено
Связанные документы: `docs/gateway.md`, `docs/passport.md`, `README.md`

### Контекст

GGUF model filenames могут измениться. Клиенты не должны зависеть от конкретных имён файлов моделей.

### Решение

Использовать стабильные gateway model names:

```text
slowrig/coder
slowrig/architect
```

Не использовать GGUF filenames как публичные model names.

### Причина

Стабильные имена удобнее для клиентов:

* Open WebUI;
* Telegram;
* IDE;
* future agents;
* scripts;
* future memory/RAG services.

Они отражают роль backend-а, а не конкретный файл модели.

Это позволяет позже заменить модель без изменения клиентских сценариев.

### Компромисс

Нужно поддерживать отдельный mapping в LiteLLM config.

Если backend меняется, нужно следить, чтобы role-based name всё ещё соответствовал фактической роли.

### Когда пересмотреть

Если появится новая модельная схема:

* aliases `slowrig/default`, `slowrig/fast`, `slowrig/deep`;
* routing по задачам;
* несколько coder-моделей;
* несколько architect-моделей;
* отдельный embedding/reranker backend.

---

## ADR-018 — Оставить direct backend-порты для диагностики

Дата: 2026-06-22
Статус: принято
Связанные документы: `docs/gateway.md`, `docs/runbook.md`, `docs/passport.md`

### Контекст

После внедрения LiteLLM обычный клиентский путь идёт через gateway.

Но для диагностики важно отличать проблему gateway от проблемы backend-а.

### Решение

Оставить direct backend-порты доступными в LAN для диагностики:

```text
8080 -> llama-architect
8081 -> llama-coder
```

Обычный клиентский путь при этом остаётся:

```text
Client -> LiteLLM Gateway -> backend
```

### Причина

Direct backend-порты позволяют быстро понять:

```text
8080/8081 работают, 4000 не работает
=> проблема в LiteLLM или его конфиге

8080/8081 не работают
=> проблема ниже gateway, в llama.cpp backend-е
```

Это сильно упрощает диагностику на этапе развития кластера.

### Компромисс

В LAN остаются опубликованные backend-порты.

Их нельзя открывать наружу, и они не должны становиться нормальным путём для клиентов.

### Когда пересмотреть

Когда gateway стабилизируется длительно и появится отдельный security hardening stage.

Возможные будущие варианты:

* закрыть direct backend-и firewall-ом;
* оставить их доступными только с localhost;
* оставить доступ только из Docker network;
* оставить как есть в LAN, но не публиковать наружу.

---

## ADR-019 — Проектировать новые subsystem-ы до внедрения

Дата: 2026-06-22
Статус: принято
Связанные документы: `AGENTS.md`, `docs/codex-context.md`, `docs/architecture.md`

### Контекст

Будущие subsystem-ы могут сильно усложнить кластер:

* Memory / RAG;
* Telegram bot;
* agent framework;
* monitoring;
* backups;
* security hardening.

Если сразу устанавливать новые сервисы без design stage, проект быстро станет сложным и плохо откатываемым.

### Решение

Перед внедрением нового subsystem-а сначала создать или обновить design document.

Примеры:

| Subsystem          | Design doc           |
| ------------------ | -------------------- |
| Memory / RAG       | `docs/memory.md`     |
| Telegram bot       | `docs/telegram.md`   |
| Agent framework    | `docs/agents.md`     |
| Monitoring         | `docs/monitoring.md` |
| Security hardening | `docs/security.md`   |
| Backups            | `docs/backups.md`    |

Design stage должен описывать:

```text
Цель:
Не-цели:
Текущий baseline:
Предлагаемая архитектура:
Затронутые файлы/сервисы:
Влияние на безопасность:
Влияние на persistence/backup:
Ручные проверки:
Откат:
Открытые вопросы:
```

### Причина

Такой подход сохраняет:

* управляемость;
* понятный rollback;
* документацию как source of truth;
* staged workflow;
* возможность остановиться перед опасной развилкой.

### Компромисс

Новые функции появляются медленнее.

Зато кластер остаётся стабильным и понятным.

### Когда пересмотреть

Если проект станет экспериментальной веткой, где скорость важнее стабильности.

Для основной ветки `main` это правило должно сохраняться.

---

## ADR-020 — Планировать PostgreSQL + pgvector как первый Memory stack

Дата: 2026-06-22
Статус: принято для планирования
Связанные документы: `docs/memory.md`, `docs/architecture.md`, `docs/changelog.md`

### Контекст

Stage 4.1 создал design-документ `docs/memory.md` без установки новых runtime-зависимостей.

Для Stage 4.2 нужно выбрать целевой вариант, вокруг которого будет готовиться implementation plan.

Рассматривались:

* Markdown + Git only;
* PostgreSQL + pgvector;
* Qdrant;
* PostgreSQL + Qdrant hybrid.

### Решение

Готовить Stage 4.2 implementation plan вокруг:

```text
PostgreSQL + pgvector
```

Это решение не устанавливает БД и не меняет runtime baseline.

Stage 4.2 implementation plan зафиксировал начальные defaults для Stage 4.3:

* service name: `memory-db`;
* container name: `memory-db`;
* network: только Docker Compose network;
* host port: не публиковать по умолчанию;
* volume: `memory-db-data`;
* `.env` names: `MEMORY_POSTGRES_DB`, `MEMORY_POSTGRES_USER`, `MEMORY_POSTGRES_PASSWORD`;
* exact pgvector-enabled PostgreSQL image/tag выбран в ADR-022.

### Причина

PostgreSQL + pgvector лучше всего подходит как первый memory stack для `slowrig`, потому что одна БД может хранить:

* structured task state;
* metadata документов и chunks;
* vector embeddings;
* будущие Telegram metadata;
* будущие agent runs и audit records.

Это проще для первого backup/restore plan, чем сразу добавлять отдельную metadata DB и Qdrant.

### Компромисс

PostgreSQL + pgvector добавит stateful runtime dependency на будущем implementation stage.

Vector search может быть менее специализированным, чем Qdrant, но текущий масштаб `slowrig` не требует отдельного vector store с самого начала.

### Когда пересмотреть

Пересмотреть, если:

* объём RAG-корпуса резко вырастет;
* pgvector окажется недостаточным по качеству или скорости retrieval;
* потребуется отдельный специализированный vector store;
* backup/restore требования сделают PostgreSQL неподходящим;
* будущие agent workflows потребуют другой state model.

---

## ADR-021 — Зафиксировать roadmap defaults после Stage 4.1

Дата: 2026-06-22
Статус: принято
Связанные документы: `docs/codex-context.md`, `docs/memory.md`, `docs/changelog.md`

### Контекст

После Stage 4.1 были решены основные развилки, влияющие на порядок развития проекта до Telegram, agents и hardening.

### Решение

Принять следующие defaults:

* embeddings — локальная embedding model позже, отдельным подэтапом;
* первый RAG-корпус — только `README.md`, `AGENTS.md`, `docs/*.md`;
* порядок — Memory foundation -> RAG -> Telegram -> agents -> hardening;
* backup/security для первой DB — минимальный backup/restore contract уже в Stage 4.2;
* Telegram — polling + whitelist, через LiteLLM, без shell;
* agents — custom lightweight workflow с лестницей прав;
* gateway aliases — не добавлять до появления клиентской необходимости;
* direct backend ports `8080/8081` — оставить в LAN до security hardening;
* monitoring — будущий `cluster-health-lite.sh`, без Prometheus/Grafana на первом monitoring stage;
* внешний доступ — LAN/VPN first;
* backup scope — git docs/config/scripts, `.env` offline отдельно, PostgreSQL dump; модели не backup-ить в первом scope.

### Причина

Такой порядок сохраняет staged delivery:

```text
stateful foundation -> local retrieval -> user interface -> controlled autonomy -> hardening
```

Он снижает риск преждевременного усложнения и не открывает наружу незрелые сервисы.

### Компромисс

Telegram, agents и полноценный monitoring появятся позже.

Зато каждый следующий subsystem будет опираться на более понятную memory/security/backup основу.

### Когда пересмотреть

Пересмотреть, если:

* Telegram нужен раньше зрелого RAG;
* pgvector окажется недостаточным;
* понадобится публичный доступ до завершения Stage 7;
* agents потребуют другой permission model;
* появится необходимость backup-ить модели или Open WebUI data.

---

## ADR-022 — Внедрить `memory-db` без host-port как Memory DB foundation

Дата: 2026-06-22
Статус: принято и проверено на сервере
Связанные документы: `docker-compose.yaml`, `.env.example`, `config/memory/init/001-memory-foundation.sql`, `docs/memory.md`, `docs/runbook.md`, `docs/architecture.md`, `docs/changelog.md`

### Контекст

Stage 4.2 подготовил implementation plan для PostgreSQL + pgvector.

Stage 4.3 должен добавить минимальную DB foundation без внедрения embeddings runtime, ingestion pipeline, Telegram bot или agent framework.

### Решение

Добавить Docker Compose service:

```text
memory-db
```

Defaults:

* image: `pgvector/pgvector:0.8.3-pg17`;
* container name: `memory-db`;
* host-port: не публиковать;
* network exposure: только Docker Compose network;
* volume: `memory-db-data`;
* init SQL: `config/memory/init/001-memory-foundation.sql`;
* secrets: только через `/opt/llama-cluster/.env`;
* backup dumps: `backups/`, не хранить в git.

### Причина

Такой вариант даёт project-local stateful DB foundation и не открывает новый сетевой endpoint в LAN.

Pinned image tag фиксирует версию pgvector/PostgreSQL для первого внедрения и не зависит от moving tags вроде `pg17` или `latest`.

`memory-db-data` отделяет state от repo files. Logical dump становится обязательным перед destructive действиями.

### Компромисс

Появляется новая runtime dependency и новый Docker volume.

Оператор должен добавить `MEMORY_POSTGRES_DB`, `MEMORY_POSTGRES_USER`, `MEMORY_POSTGRES_PASSWORD` в real `.env` перед запуском `memory-db`.

Bootstrap SQL через `/docker-entrypoint-initdb.d` подходит для первого пустого volume, но не является полноценным migration framework для будущих schema changes.

### Когда пересмотреть

Пересмотреть, если:

* понадобится host-level DB administration port;
* понадобится encrypted/offline backup policy сразу;
* schema начнёт меняться после появления важных данных;
* pgvector окажется недостаточным;
* появится необходимость перейти на PostgreSQL + Qdrant hybrid.

---

## ADR-023 — Использовать локальный `llama.cpp` embedding service для Stage 4.4

Дата: 2026-06-22
Статус: принято и проверено на сервере
Связанные документы: `docker-compose.yaml`, `scripts/memory-ingest-docs.py`, `docs/memory.md`, `docs/runbook.md`, `docs/changelog.md`

### Контекст

После Stage 4.3 в кластере есть `memory-db` на PostgreSQL + pgvector. Для Stage 4.4 нужен первый локальный embedding runtime и ingestion pipeline для разрешённого документационного корпуса.

Рассматривались:

* локальный `llama.cpp` embedding service;
* отдельный Python embedding stack;
* внешний embedding API;
* откладывание embeddings и хранение только chunks.

Пользователь выбрал вариант A:

```text
llama.cpp embedding service, CPU-only, GGUF model
```

### Решение

Добавить service:

```text
memory-embed
```

Defaults:

* image: `ghcr.io/ggml-org/llama.cpp:server-cuda`;
* model: `/models/embeddings/Qwen3-Embedding-0.6B-Q8_0.gguf`;
* endpoint: `127.0.0.1:4010`;
* Docker internal port: `8080`;
* GPU offload: `--gpu-layers 0`;
* context: `--ctx-size 32768`;
* embedding mode: `--embedding`;
* pooling: `--pooling last`;
* ubatch: `--ubatch-size 8192`.
* parallelism: `--parallel 1`;
* prompt cache: `--cache-ram 0`.

Добавить host-side ingestion script:

```text
scripts/memory-ingest-docs.py
```

Первый corpus:

```text
README.md
AGENTS.md
docs/*.md
```

### Причина

`llama.cpp` уже является базовым inference runtime проекта, умеет GGUF и OpenAI-compatible endpoints. Это даёт один знакомый operational pattern вместо отдельного Python/ML стека.

CPU-only режим не забирает VRAM у `llama-coder` и `llama-architect`. Для маленького документационного корпуса скорость embedding ingestion менее важна, чем стабильность основных LLM.

Loopback binding `127.0.0.1:4010` позволяет host-side ingestion script обращаться к endpoint без публикации embeddings в LAN.

`Qwen3-Embedding-0.6B-Q8_0.gguf` выбран как компактная локальная embedding model с Apache 2.0 license, GGUF artifact и ожидаемой размерностью 1024.

### Компромисс

CPU-only embeddings могут быть медленнее GPU-варианта.

Loopback endpoint всё равно является host-visible endpoint, поэтому его нельзя открывать на `0.0.0.0` без отдельного security decision.

Ingestion script использует `docker compose exec -T memory-db psql` вместо отдельного PostgreSQL driver. Это проще для текущего стека и не добавляет Python dependencies, но не является полноценным application service.

### Когда пересмотреть

Пересмотреть, если:

* ingestion станет слишком медленным;
* потребуется online retrieval API с частыми embedding-запросами;
* embedding quality окажется недостаточной;
* появится необходимость GPU embedding runtime;
* понадобится отдельная memory service с PostgreSQL driver и API;
* endpoint `127.0.0.1:4010` потребуется закрыть даже от host-level clients.

---

## ADR-024 — Проектировать первый Telegram bot как polling + whitelist клиент LiteLLM

Дата: 2026-06-22
Статус: принято; design добавлен, runtime не внедрён
Связанные документы: `docs/telegram.md`, `docs/architecture.md`, `docs/codex-context.md`, `docs/changelog.md`

### Контекст

После Stage 4.4 в `slowrig` есть проверенный inference baseline, LiteLLM Gateway и локальный RAG ingestion для проектной документации. Следующий интерфейс — Telegram bot.

Telegram увеличивает security/privacy surface, потому что появляется внешний messaging API, пользовательские сообщения, bot token и потенциальный temptation добавить admin commands. Поэтому перед runtime нужен design-only stage.

### Решение

Первый Telegram bot проектировать как:

```text
Telegram Bot API polling + whitelist -> LiteLLM Gateway -> slowrig/coder
```

Defaults:

* polling вместо webhook;
* deny-by-default whitelist по Telegram user IDs;
* default model: `slowrig/coder`;
* `slowrig/architect` только по явной команде или документированному escalation rule;
* no shell;
* no Docker access;
* no arbitrary filesystem access;
* no Telegram history in Memory/RAG на первом runtime stage;
* no public inbound port.

Первый design artifact:

```text
docs/telegram.md
```

### Причина

Polling не требует public inbound endpoint и лучше соответствует текущему LAN/VPN-first posture.

Whitelist снижает риск случайного доступа к локальному кластеру через Telegram.

LiteLLM остаётся обычной точкой входа для LLM-клиентов, поэтому Telegram не должен обращаться напрямую к `llama-coder` или `llama-architect`.

`slowrig/coder` выбран default route, потому что Telegram требует отзывчивости и обычно получает короткие задачи.

### Компромисс

Polling менее production-like, чем webhook, и может иметь чуть большую latency.

Без хранения истории bot будет менее “памятливым”, но это сохраняет privacy boundary до отдельного решения о retention/delete/backup.

Отсутствие shell/admin commands ограничивает удобство удалённого управления, зато не превращает Telegram в опасный remote ops интерфейс.

### Когда пересмотреть

Пересмотреть, если:

* понадобится публичный webhook за reverse proxy/VPN;
* появится зрелый admin/security design для Telegram diagnostics;
* потребуется opt-in хранение истории;
* потребуется read-only RAG command;
* появится отдельный bot user/role model;
* `slowrig/coder` окажется слишком слабым или медленным для Telegram default.

---

## ADR-025 — Реализовывать первый Telegram runtime без отдельной Telegram library

Дата: 2026-06-22
Статус: superseded by ADR-026
Связанные документы: `docs/telegram.md`, `.env.example`, `docs/changelog.md`

### Контекст

Stage 5 design выбрал polling + whitelist + LiteLLM. Перед runtime нужно выбрать dependency strategy.

Варианты:

* использовать `python-telegram-bot`;
* использовать `pyTelegramBotAPI`;
* использовать Node.js библиотеку;
* реализовать минимальный polling через Python stdlib и Telegram Bot API HTTP.

### Решение

Для первого runtime plan выбрать:

```text
Python stdlib + Telegram Bot API HTTP polling
```

Не добавлять отдельную Telegram framework/library на первом runtime stage.

Будущий bot service проектировать как:

```text
telegram-bot
```

Планируемые env placeholders:

```text
TELEGRAM_BOT_TOKEN
TELEGRAM_ALLOWED_USER_IDS
TELEGRAM_DEFAULT_MODEL
TELEGRAM_ARCHITECT_MODEL
```

### Причина

Первый bot должен делать небольшой набор действий: polling, whitelist, commands и OpenAI-compatible запросы в LiteLLM. Для этого достаточно stdlib `urllib`.

Отказ от внешней Telegram library снижает dependency surface, упрощает audit и уменьшает риск скрытого поведения.

### Компромисс

Придётся вручную обработать Telegram Bot API детали:

* `getUpdates`;
* `sendMessage`;
* offset;
* базовые ошибки HTTP;
* command parsing;
* message length limits.

Это приемлемо для первого компактного bot. Если command surface вырастет, можно пересмотреть решение и добавить библиотеку отдельным ADR.

### Когда пересмотреть

Пересмотреть, если:

* появятся inline keyboards;
* понадобится files/media support;
* понадобится сложный command router;
* stdlib implementation станет слишком хрупкой;
* потребуется webhook;
* появится полноценный Telegram admin/security layer.

---

## ADR-026 — Реализовывать первый Telegram runtime на C#/.NET

Дата: 2026-06-22
Статус: принято; runtime validated
Заменяет: ADR-025
Связанные документы: `docker-compose.yaml`, `src/telegram-bot`, `docs/telegram.md`, `docs/runbook.md`, `docs/changelog.md`

### Контекст

После добавления первого Python runtime пользователь попросил переделать Telegram bot на C#.

Сохраняются прежние архитектурные ограничения:

* polling вместо webhook;
* whitelist;
* LiteLLM как единственная LLM-точка входа;
* no shell;
* no Docker socket;
* no filesystem access к host;
* no Telegram history persistence;
* profile `telegram`, чтобы обычный `docker compose up -d` не запускал bot.

### Решение

Реализовать Telegram runtime как C#/.NET console service:

```text
src/telegram-bot
```

Compose service:

```text
telegram-bot
```

Build strategy:

```text
local Docker multi-stage build
mcr.microsoft.com/dotnet/sdk:8.0 -> mcr.microsoft.com/dotnet/runtime:8.0
```

Не добавлять Telegram-specific NuGet package на первом runtime. Использовать `HttpClient` и Telegram Bot API HTTP endpoints напрямую.

### Причина

C# даёт типизированный компактный service без Python runtime script и лучше соответствует предпочтению пользователя для bot implementation.

Прямой `HttpClient` сохраняет небольшой dependency surface: нет отдельного Telegram framework package, нет webhook stack, нет лишних runtime services.

### Компромисс

Появляется .NET Docker build dependency и необходимость pull-ить Microsoft .NET images.

Код вручную обрабатывает Telegram Bot API details, как и Python stdlib-вариант:

* `getUpdates`;
* `sendMessage`;
* update offset;
* command parsing;
* message splitting.

### Когда пересмотреть

Пересмотреть, если:

* потребуется Telegram framework package;
* потребуется webhook;
* появятся inline keyboards/media/files;
* .NET image footprint станет нежелательным;
* bot превратится в полноценный admin/security subsystem.

---

## ADR-028 — Проектировать agent layer как custom lightweight orchestration

Дата: 2026-06-23
Статус: принято; design добавлен
Связанные документы: `docs/agents.md`, `docs/codex-context.md`, `docs/changelog.md`

### Контекст

`slowrig` должен постепенно получить controlled agent workflows, но текущий кластер остаётся домашней инфраструктурой с реальными сервисами, GPU, secrets и stateful data. Полноценный agent framework на старте увеличит surface area, права и operational complexity.

### Решение

Первый Stage 6 design выбирает:

```text
custom lightweight orchestration / Codex-driven workflow
```

Agent capabilities должны расти через permission ladder:

```text
read/report -> patches -> diagnostics allowlist -> approved mutations -> sandbox autonomy
```

### Причина

Такой подход сохраняет:

* маленький blast radius;
* понятный audit trail;
* human approval для опасных действий;
* совместимость с существующими docs/git workflows;
* routing через LiteLLM вместо прямого backend access.

### Компромисс

На первом этапе не будет rich autonomous framework, task queue, plugin ecosystem и сложной multi-agent координации. Это осознанный обмен скорости внедрения на безопасность и контролируемость.

### Когда пересмотреть

Если появится устойчивый набор повторяемых workflows, понятный diagnostics allowlist, требования к очередям задач, persistent agent state или необходимость интеграции с Telegram/IDE/API beyond simple patch/report workflows.

---

## ADR-029 — Начать agents с docs drift / repo patch assistant workflow

Дата: 2026-06-23
Статус: принято и внедрено
Связанные документы: `docs/agents.md`, `docs/codex-context.md`, `docs/changelog.md`

### Контекст

После Stage 6 design нужно выбрать первый concrete agent workflow. Рассматривались безопасный docs drift assistant, diagnostics assistant, Telegram-to-agent escalation и task queue / persistent state.

### Решение

Первый Stage 6.1 workflow:

```text
docs drift / repo patch assistant
```

Он работает только на уровнях:

```text
Level 0 read/report
Level 1 patches
```

### Причина

Этот workflow даёт практическую пользу сразу, но не требует runtime service, shell access, Telegram escalation, task queue, новых dependencies или доступа к secrets.

### Компромисс

Он не решает operational diagnostics automation и не даёт полноценной автономии. Это осознанный первый шаг, чтобы отработать audit trail, patch discipline и безопасный agent workflow на документации и config drift.

### Когда пересмотреть

После нескольких успешных docs/config drift audits можно перейти к Level 2 diagnostics allowlist или Telegram-to-agent escalation, если будет понятен список безопасных команд и формат отчётов.

---

## ADR-030 — Зафиксировать Stage 7 monitoring/security/backups defaults

Дата: 2026-06-23
Статус: принято; design добавлен
Связанные документы: `docs/monitoring.md`, `docs/security.md`, `docs/backups.md`, `docs/codex-context.md`, `docs/changelog.md`

### Контекст

После Memory, Telegram и Agents design проекту нужен safety baseline для наблюдаемости, доступа и восстановления. При этом тяжёлые monitoring stacks, public exposure и backup automation увеличивают operational complexity и требуют отдельной проверки.

### Решение

Stage 7 defaults:

```text
monitoring: cluster-health-lite.sh как будущий дешёвый health check; cluster-status.sh остаётся ручной глубокой диагностикой
security: LAN/VPN first; без public WebUI/Gateway; direct ports 8080/8081 пока оставить для diagnostics
backups: git docs/config/scripts + offline .env + PostgreSQL dumps; models не backup-ить на первом этапе
```

### Причина

Такой baseline улучшает operational safety без новых сервисов, firewall changes, cron jobs, Prometheus/Grafana stack или рискованных runtime mutations.

### Компромисс

На этом этапе нет автоматического мониторинга, alerting, backup jobs, encrypted restore rehearsal или security hardening. Они должны идти отдельными implementation stages после design review.

### Когда пересмотреть

Перед public access, firewall/reverse proxy/VPN changes, Open WebUI auth changes, backup automation, Prometheus/Grafana/Loki rollout или закрытием direct backend ports.

---

## ADR-031 — Реализовать `cluster-health-lite.sh` как дешёвый health check

Дата: 2026-06-23
Статус: принято и внедрено
Связанные документы: `scripts/cluster-health-lite.sh`, `docs/monitoring.md`, `docs/codex-context.md`, `docs/changelog.md`

### Контекст

`cluster-status.sh` полезен для ручной диагностики, но он может выполнять реальные LLM checks и поэтому не подходит как частый автоматический healthcheck.

### Решение

Stage 7.1 реализует:

```text
scripts/cluster-health-lite.sh
```

Он проверяет только дешёвые readiness/status признаки и не выполняет LLM generation.

### Причина

Такой health check можно запускать чаще и безопаснее, не расходуя GPU/LLM resources и не создавая ложных задержек из-за генерации.

### Компромисс

Lite-check не доказывает качество генерации моделей. Глубокая проверка остаётся задачей `scripts/cluster-status.sh` и ручной диагностики. Timer/cron/systemd и automated remediation не добавлены.

### Когда пересмотреть

Перед добавлением cron/systemd timer, alerting, Prometheus/Grafana или любых automated remediation actions.

---

## ADR-032 — Завершить Stage 6 read-only docs drift helper

Дата: 2026-06-23
Статус: принято и внедрено
Связанные документы: `scripts/docs-drift-agent.sh`, `docs/agents.md`, `docs/stage6-summary.md`, `docs/changelog.md`

### Контекст

Stage 6 выбрал lightweight agent direction и первый workflow `docs drift / repo patch assistant`. Чтобы считать Stage 6 завершённым, нужен практический, безопасный baseline без framework, shell autonomy и новых dependencies.

### Решение

Завершить Stage 6 через read-only helper:

```text
scripts/docs-drift-agent.sh
```

Helper работает на Level 0/1: read/report и patch-support. Он проверяет repo/docs/config drift, но не меняет файлы и не выполняет runtime operations.

### Причина

Это даёт первый воспроизводимый agent workflow, сохраняя маленький blast radius и соответствие `AGENTS.md`: documentation-first, explicit approvals, no secrets, no dangerous automation.

### Компромисс

Helper не является полноценным agent framework, не имеет task queue, persistent state, Telegram escalation или diagnostics command allowlist. Эти возможности остаются будущими stages.

### Когда пересмотреть

Если docs drift helper станет недостаточным, можно проектировать Level 2 diagnostics allowlist, Memory/RAG retrieval for agent context, Telegram-to-agent escalation или маленький local service.

---

## 4. Открытые вопросы

### Q1. Какие routing policy добавить в gateway?

Gateway выбран: LiteLLM Proxy.

Открытый вопрос теперь — какие routing policy добавлять дальше.

Варианты:

* оставить только `slowrig/coder` и `slowrig/architect`;
* добавить `slowrig/default`;
* добавить `slowrig/fast`;
* добавить `slowrig/deep`;
* добавить task-specific aliases;
* реализовать routing через будущий agent/orchestrator layer.

---

### Q2. Какую память выбрать?

Текущий выбор для планирования:

```text
PostgreSQL + pgvector
```

Markdown + Git остаются source of truth. PostgreSQL + pgvector планируется как хранилище structured state, metadata, chunks и derived vector data.

Оставшиеся вопросы:

* backup dump path и offline/encrypted policy;
* schema migration mechanism;
* chunking/provenance format;
* когда Qdrant нужен как future upgrade path.

---

### Q3. Каким будет Telegram bot?

Варианты:

* простой chat bot;
* bot с выбором модели;
* bot с памятью;
* bot как интерфейс к агентам;
* bot с ограниченным набором безопасных команд диагностики.

Ключевое ограничение:

```text
Telegram bot не должен иметь произвольный shell-доступ.
```

---

### Q4. Какой diagnostics allowlist дать агентам?

Лестница прав выбрана в ADR-028:

```text
read/report -> patches -> diagnostics allowlist -> approved mutations -> sandbox autonomy
```

Открытым остаётся точный список Level 2 diagnostics commands, формат отчёта и правила sanitization.

Ключевое ограничение:

```text
опасные действия требуют подтверждения пользователя.
```

---

### Q5. Как делать backup?

Нужно отдельно решить backup для:

* compose/config/docs/scripts;
* моделей;
* Open WebUI data;
* memory DB;
* логов;
* agent state;
* `.env` и секретов без попадания в git.

---

### Q6. Когда закрывать direct backend-порты?

Текущий baseline сохраняет `8080` и `8081` для диагностики.

Открытый вопрос:

* оставить как есть в LAN;
* закрыть снаружи firewall-ом;
* ограничить localhost;
* оставить только Docker network;
* закрыть после внедрения полноценного security stage.

---

## 5. Правило обновления решений

При изменении важного архитектурного решения:

1. Добавить новый ADR.
2. Не удалять старый ADR, если он объясняет историю.
3. Указать, если старое решение `superseded`.
4. Обновить связанные документы.
5. Добавить запись в `docs/changelog.md`, если изменение фактически применено.
6. Предложить commit message.

Пример:

```text
ADR-022 — Название нового решения
Статус: принято
Заменяет: ADR-XXX частично
```

Если решение ещё не принято, не записывать его как принятое. Использовать статус:

```text
открыто
```

или вынести вопрос в раздел “Открытые вопросы”.
