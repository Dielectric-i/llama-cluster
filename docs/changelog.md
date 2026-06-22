# slowrig AI Cluster — Changelog v0.2

Дата актуализации: 2026-06-22
Статус: журнал фактических изменений, проверок и измерений

## 1. Назначение

Этот документ фиксирует фактические изменения, проверки и измерения по `slowrig AI Cluster`.

`changelog.md` отвечает на вопросы:

* что реально изменилось;
* когда это изменилось;
* что было проверено;
* какой результат получен;
* какие есть замечания;
* какие ограничения остались.

`changelog.md` отличается от `docs/decisions.md`:

* `docs/decisions.md` объясняет, почему выбрано архитектурное решение;
* `changelog.md` фиксирует, что реально было сделано и проверено.

Этот документ не заменяет:

* `README.md` — быстрый вход и индекс документации;
* `docs/passport.md` — фактический паспорт стенда;
* `docs/runbook.md` — эксплуатационные команды и rollback;
* `docs/architecture.md` — текущую и целевую архитектуру;
* `docs/gateway.md` — подробности LiteLLM Gateway;
* `docs/decisions.md` — архитектурные решения и компромиссы.

---

## 2. Формат записей

Новые записи добавляются сверху, в обратной хронологии.

Рекомендуемый формат:

```text
## YYYY-MM-DD — Краткое название изменения

### Изменено

### Проверено

### Результат

### Замечания
```

Правила:

* фиксировать только фактические изменения;
* не записывать планы как уже выполненные действия;
* не указывать реальные секреты;
* не утверждать, что проверка прошла, если пользователь не подтвердил её или не предоставил вывод;
* если изменение только документационное, явно указывать, что server/runtime checks не требовались;
* если изменение затрагивает архитектурное решение, также обновлять `docs/decisions.md`;
* если изменение затрагивает эксплуатационные команды, также обновлять `docs/runbook.md`.

---

## 2026-06-22 — Stage 4.3 Memory DB foundation

### Изменено

Добавлен `memory-db` в `docker-compose.yaml`:

* image: `pgvector/pgvector:0.8.3-pg17`;
* container name: `memory-db`;
* host-port не публикуется;
* volume: `memory-db-data`;
* init SQL mount: `config/memory/init`;
* healthcheck через `pg_isready`.

Добавлен bootstrap SQL:

```text
config/memory/init/001-memory-foundation.sql
```

Он создаёт `vector`, schema `memory` и минимальные таблицы для documents, chunks, embedding metadata, embeddings, ingestion runs, tasks, access rules и audit log.

Обновлены:

* `AGENTS.md` — активная интеграционная ветка изменена на `codex/main`, `master` помечен как legacy branch для новых stage, добавлено правило про SSH-доступ и предпочтение Docker/Docker Compose вместо host installs;
* `.env.example` — добавлены placeholders `MEMORY_POSTGRES_DB`, `MEMORY_POSTGRES_USER`, `MEMORY_POSTGRES_PASSWORD`;
* `.gitignore` — добавлен `backups/`;
* `scripts/cluster-status.sh` — добавлена проверка `memory-db` и `pgvector`;
* `README.md`, `docs/passport.md`, `docs/runbook.md`, `docs/architecture.md`, `docs/memory.md`, `docs/decisions.md`, `docs/codex-context.md`.

Добавлен `ADR-022`:

```text
Внедрить memory-db без host-port как Memory DB foundation
```

### Проверено

Repository-side проверки:

```text
git status --short
git diff --stat
git diff --check
```

`git diff --check` не нашёл whitespace errors.

Серверные проверки через `ssh discover@slowrig`:

```text
git checkout -B codex/main origin/codex/main
docker compose config --quiet
docker compose up -d memory-db
docker compose ps memory-db
docker logs --tail=120 memory-db
pg_isready
SELECT extname FROM pg_extension;
\dt memory.*
```

Результат:

* серверный worktree переключён на `codex/main`;
* `docker compose config --quiet` прошёл;
* `memory-db` запущен и показывает `healthy`;
* PostgreSQL принимает соединения;
* extension `vector` создан;
* schema `memory` содержит 9 bootstrap tables.

Частично проверено:

```text
/opt/llama-cluster/scripts/cluster-status.sh
```

Результат:

* direct backend `8080` отвечает;
* direct backend `8081` отвечает;
* Open WebUI `3000` отвечает;
* LiteLLM `/v1/models` отвечает;
* `slowrig/coder` отвечает через LiteLLM;
* `slowrig/architect` отвечает через LiteLLM;
* GPU mapping и VRAM соответствуют baseline.

Ограничение: при запуске через noninteractive SSH от Codex команды `sudo` внутри `cluster-status.sh` не прошли, поэтому Docker-секции и встроенная `memory-db` проверка в этом запуске не являются валидными. Прямые `memory-db` проверки выше выполнены отдельными Docker-командами.

Не выполнены локально в Windows-среде Codex:

```text
docker compose config --quiet
bash -n scripts/cluster-status.sh
```

Причина: в локальной Windows-среде Codex нет Docker CLI, а доступный `bash.exe` является WSL-заглушкой без установленного Linux environment.

Server/runtime checks выполнялись на `slowrig` через SSH, как описано выше.

Осталось проверить отдельно:

```text
Memory DB backup/restore dry run
```

Причина: после первичных серверных проверок noninteractive `sudo -S` перестал принимать предоставленный пароль. Пароль не сохранялся в файлах и не записывался в документацию.

Позднее пользователь добавил `discover` в группу `docker` и перелогинился по SSH. После этого Docker стал доступен без `sudo`.

Дополнительно проверено:

```text
docker ps
pg_dump --format=custom
createdb slowrig_memory_restore_check
pg_restore
\dt memory.*
dropdb slowrig_memory_restore_check
```

Результат:

* `memory-db` остаётся `healthy`;
* создан свежий dump `backups/memory-db-20260622-071845.dump`;
* restore dry run во временную DB прошёл;
* в restored DB видны 9 таблиц schema `memory`;
* временная DB `slowrig_memory_restore_check` удалена после проверки;
* нулевые dump-файлы от ранних quoting-сбоев удалены.

Замечание для automation: если `docker compose exec` запускается из shell-скрипта, который сам передан через stdin, командам без собственного stdin нужно добавлять `</dev/null`. Иначе `docker compose exec` может съесть остаток скрипта. Для `pg_restore` stdin intentionally используется для dump-файла.

После появления Docker group access для `discover` обновлён `scripts/cluster-status.sh`: теперь он использует `docker` без `sudo`, если это доступно, и сохраняет fallback на `sudo docker` для ручных запусков от пользователя без Docker group access.

После обновления `scripts/cluster-status.sh` на сервере проверено:

```text
bash -n scripts/cluster-status.sh
/opt/llama-cluster/scripts/cluster-status.sh
```

Результат:

* Docker containers и Compose services выводятся без `sudo`;
* все LLM/API checks — `OK`;
* `memory-db pg_isready` — `OK`;
* `memory-db pgvector extension` — `OK`.

Финальный запуск `/opt/llama-cluster/scripts/cluster-status.sh` после backup/restore dry run подтвердил:

* `memory-db` — `Up` и `healthy`;
* `litellm`, `open-webui`, `llama-coder`, `llama-architect` остаются запущены;
* direct backend `8080` и `8081` отвечают;
* Open WebUI `3000` отвечает;
* LiteLLM `/v1/models` отвечает;
* `slowrig/coder` и `slowrig/architect` отвечают через LiteLLM;
* GPU VRAM соответствует baseline;
* `memory-db pg_isready` — `OK`;
* `memory-db pgvector extension` — `OK`.

### Результат

Stage 4.3 добавил config-level foundation для PostgreSQL + pgvector без изменения LLM routing, GPU mapping, model files, host ports `3000/4000/8080/8081`, LiteLLM config или Open WebUI config.

### Замечания

Перед запуском на сервере оператор должен добавить реальные значения Memory DB в `/opt/llama-cluster/.env`.

Следующий этап после проверки Stage 4.3:

```text
Stage 4.4 — Local RAG ingestion
```

---

## 2026-06-22 — Stage 4.2 Memory implementation plan

### Изменено

Обновлён `docs/memory.md`:

* Stage 4.2 оформлен как documentation-only implementation plan;
* будущий DB service зафиксирован как `memory-db`;
* будущий runtime stack зафиксирован как PostgreSQL + pgvector;
* host-port для DB не публикуется по умолчанию;
* будущий volume зафиксирован как `memory-db-data`;
* env names зафиксированы как `MEMORY_POSTGRES_DB`, `MEMORY_POSTGRES_USER`, `MEMORY_POSTGRES_PASSWORD`;
* описаны минимальные schema areas, backup/restore contract, проверки и rollback для будущего Stage 4.3.

Обновлены `docs/architecture.md`, `docs/decisions.md` и `docs/codex-context.md`:

* Stage 4.1 больше не указан как следующий этап;
* ADR-013 помечен как `superseded by ADR-020`;
* ADR-020 дополнен начальными implementation defaults;
* оставшиеся Memory/RAG вопросы сужены до image/tag, backup path, migration mechanism, chunking/provenance и future Qdrant path.

### Проверено

Проверка документационная. Runtime/server checks не требовались.

### Результат

Stage 4.2 подготовил безопасный план для будущего внедрения `memory-db`, не меняя `docker-compose.yaml`, LiteLLM config, ports, volumes или running services.

### Замечания

Следующий этап после отдельного approval:

```text
Stage 4.3 — Memory DB foundation
```

---

## 2026-06-22 — Roadmap forks resolved and Codex context updated

### Изменено

Обновлён `docs/codex-context.md`:

* устаревшие open questions заменены на принятые roadmap defaults;
* зафиксированы решения по Memory/RAG, embeddings, RAG corpus, Telegram, agents, monitoring, access и backups;
* оставлены только будущие развилки, которые действительно нужно обсуждать позже;
* документ сохранён как compact supplemental context, а не второй README.

Обновлён `AGENTS.md`:

* на архитектурных развилках Codex должен подробно объяснять варианты;
* для каждого варианта нужно описывать влияние на конечный результат и процесс внедрения/эксплуатации.

Обновлены `docs/memory.md` и `docs/decisions.md`:

* первый RAG-корпус ограничен `README.md`, `AGENTS.md`, `docs/*.md`;
* local embeddings вынесены в отдельный будущий подэтап;
* добавлен `ADR-021` с roadmap defaults.

### Проверено

Проверка документационная. Runtime/server checks не требовались.

### Результат

Проект получил согласованный план развития до основных Stage 7 направлений без добавления runtime dependencies.

### Замечания

Следующий этап остаётся:

```text
Stage 4.2 — Memory implementation plan
```

---

## 2026-06-22 — Stage 4.1 planning rules and memory stack confirmation

### Изменено

Обновлён `AGENTS.md`:

* пользовательские отчёты должны использовать русские заголовки;
* Codex не должен останавливаться после каждого небольшого шага;
* останавливаться нужно только на архитектурных развилках, проблемах, необходимости пользовательского вмешательства или явном запросе пользователя;
* долговременные инструкции пользователя о поведении Codex должны фиксироваться в `AGENTS.md`.

Зафиксировано архитектурное решение:

```text
Stage 4.2 implementation plan будет готовиться вокруг PostgreSQL + pgvector.
```

Добавлен `ADR-020`.

### Проверено

Проверка документационная. Runtime/server checks не требовались.

### Результат

Правила работы Codex уточнены, а Stage 4.2 получил подтверждённый целевой memory stack для планирования.

### Замечания

Это не внедрение PostgreSQL, pgvector или новых Docker services.

---

## 2026-06-22 — Stage 4.1 Memory / RAG design

### Изменено

Создан design-документ:

```text
docs/memory.md
```

Документ фиксирует:

* цели и non-goals Memory / RAG слоя;
* правило `Markdown + Git` как source of truth;
* границы derived data и будущего индекса;
* data policy для документов, логов, чатов и секретов;
* сравнение вариантов `Markdown + Git only`, `PostgreSQL + pgvector`, `Qdrant`, `PostgreSQL + Qdrant hybrid`;
* предварительную рекомендацию рассматривать `PostgreSQL + pgvector` как первый runtime-кандидат только после отдельного approval;
* security/privacy, backup/restore и rollback-вопросы для будущего implementation stage.

Обновлены ссылки и статус Stage 4.1 в:

* `README.md`;
* `docs/architecture.md`;
* `docs/codex-context.md`;
* `docs/decisions.md`;
* `docs/changelog.md`.

### Проверено

Проверка была документационной.

Runtime/server checks не требовались, потому что:

* `docker-compose.yaml` не менялся;
* LiteLLM config не менялся;
* новые Docker services не добавлялись;
* новые runtime dependencies не устанавливались.

### Результат

Stage 4.1 оформлен как design-only этап.

Следующий возможный этап:

```text
Stage 4.2 — Memory implementation plan
```

### Замечания

До Stage 4.2 не устанавливать PostgreSQL, pgvector, Qdrant, embedding service, Telegram bot или agent framework.

---

## 2026-06-22 — Stage 3.1 documentation consistency audit

### Изменено

Проведён безопасный документационный аудит согласованности после Stage 1, Stage 2 и Stage 3.

Уточнены:

* дата завершения и дата актуализации `docs/stage3-summary.md`;
* формулировка следующего конкретного этапа как `Stage 4.1 — Memory / RAG design`;
* различие между Docker network port `8080` и host diagnostic port `8081` для `llama-coder`;
* статус `docker-compose.stage1-baseline.yaml` как исторического Stage 1 baseline, а не текущего stable compose.

### Проверено

Проверка была документационной:

* сверены README, AGENTS, основные документы `docs/`, compose, LiteLLM config, status script, `.env.example` и `.gitignore`;
* подтверждено, что основная цепочка `Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect` согласована с config;
* подтверждено, что `.env` не отслеживается git, а `.env.example` содержит только placeholders.

Runtime/server checks не требовались, так как изменения затрагивают только Markdown-документацию.

### Результат

Мелкие неоднозначности в документации устранены без изменения runtime/config поведения.

### Замечания

Следующий рекомендуемый этап остаётся:

```text
Stage 4.1 — Memory / RAG design
```

Первый артефакт — `docs/memory.md`. Новые БД, vector store, Telegram bot или agent framework на этом этапе не устанавливать.

---

## 2026-06-22 — Documentation cleanup and source-of-truth alignment

### Изменено

Проведена чистка и нормализация документации.

Обновлены или подготовлены к обновлению следующие документы:

* `README.md`;
* `AGENTS.md`;
* `docs/codex-context.md`;
* `docs/runbook.md`;
* `docs/passport.md`;
* `docs/gateway.md`;
* `docs/decisions.md`;
* `docs/changelog.md`.

Главная цель изменений:

```text
убрать дублирование
развести зоны ответственности документов
сделать README единственным индексом документации
оставить каждый документ в своей роли
```

### Детали

`README.md`:

* список документов оставлен только в одном месте — в разделе `Документы`;
* сервисы и адреса сведены в компактную таблицу;
* длинные диагностические инструкции заменены ссылкой на `docs/runbook.md`;
* `git add .` заменён на рекомендацию явно добавлять нужные файлы;
* добавлены `AGENTS.md`, `docs/codex-context.md` и `docs/stage3-summary.md` в документационный индекс.

`AGENTS.md`:

* оставлен как управляющий файл для Codex;
* убрано лишнее дублирование паспортных и runbook-данных;
* добавлен stage/branch workflow с ветками `codex/...`;
* уточнены правила безопасности, документации, drift handling и done definition.

`docs/codex-context.md`:

* переписан как supplemental context для Codex;
* убраны дубли из `README.md`, `AGENTS.md`, `passport`, `runbook`, `architecture`, `gateway`, `decisions`;
* оставлены пользовательские рабочие предпочтения, stage-order assumptions и будущие архитектурные развилки.

`docs/runbook.md`:

* исправлена структура и нумерация разделов;
* убраны дублирующиеся и устаревшие блоки;
* добавлены отдельные разделы для LiteLLM, Open WebUI, rollback и проверки после изменений;
* уточнено, что `cluster-status.sh` делает реальные короткие LLM-запросы и не должен быть частым автоматическим healthcheck.

`docs/passport.md`:

* приведён к роли фактического паспорта стенда;
* убраны runbook-команды, будущие планы памяти и агентского слоя;
* оставлены факты о хосте, железе, сервисах, моделях, baseline-измерениях и security posture.

`docs/gateway.md`:

* переписан как актуальный subsystem-документ для LiteLLM Gateway;
* убраны устаревшие “кандидаты gateway” и “предварительное решение” из основного тела документа;
* зафиксированы текущая схема, model names, routing policy, Open WebUI routing, rollback и known limitations.

`docs/decisions.md`:

* приведён к более строгому ADR-формату;
* добавлен индекс решений;
* обновлены статусы решений;
* исправлено security-решение с учётом порта `4000`;
* добавлены решения про стабильные gateway model names, direct backend-порты для диагностики и design-first подход для новых subsystem-ов.

`docs/changelog.md`:

* приведён к обратной хронологии;
* объединены дублирующие записи Stage 3;
* убран устаревший блок `Current stable state`;
* добавлен единый формат записей.

### Проверено

Проверка была документационной:

* проверено, что документы не дублируют друг друга без необходимости;
* проверено, что README остаётся единственным индексом документации;
* проверено, что `passport.md` содержит фактический паспорт, а не runbook;
* проверено, что `gateway.md` описывает текущий LiteLLM baseline, а не процесс выбора gateway;
* проверено, что `decisions.md` содержит причины решений, а не changelog;
* проверено, что `changelog.md` фиксирует факты, а не планы.

Runtime/server checks не требовались, так как изменения затрагивают только Markdown-документацию.

### Результат

Документация стала чище:

* меньше дублирования;
* понятнее зоны ответственности документов;
* проще сопровождать README;
* легче обновлять отдельные subsystem-документы;
* Codex получает более чёткие правила работы;
* будущие Stage 4+ изменения можно делать в более контролируемом формате.

### Замечания

После применения правок желательно проверить diff:

```bash
cd /opt/llama-cluster
git diff --stat
git diff -- README.md AGENTS.md docs/
```

Если изменения устраивают, добавить файлы явно:

```bash
git add README.md AGENTS.md docs/codex-context.md docs/runbook.md docs/passport.md docs/gateway.md docs/decisions.md docs/changelog.md
```

Возможный commit message:

```text
docs: clean up project documentation structure
```

---

## 2026-06-19 — Stage 3 gateway baseline completed

### Изменено

Добавлен и проверен LiteLLM Gateway как единая OpenAI-compatible точка входа.

Новая основная цепочка:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

Добавлен сервис:

```text
litellm
```

Host port:

```text
4000
```

Созданы или обновлены файлы:

```text
.env
.env.example
config/litellm.config.yaml
docker-compose.yaml
scripts/cluster-status.sh
docs/gateway.md
docs/changelog.md
docs/decisions.md
```

`.env` содержит реальные секреты и не хранится в git.

`.env.example` хранится в git как безопасный шаблон.

### Конфигурация

LiteLLM использует model names:

| Gateway model name  | Backend           |
| ------------------- | ----------------- |
| `slowrig/coder`     | `llama-coder`     |
| `slowrig/architect` | `llama-architect` |

Backend endpoints внутри Docker network:

| Backend           | Docker network endpoint          |
| ----------------- | -------------------------------- |
| `llama-coder`     | `http://llama-coder:8080/v1`     |
| `llama-architect` | `http://llama-architect:8080/v1` |

Open WebUI переключён на gateway mode:

```text
OPENAI_API_BASE_URLS=http://litellm:4000/v1
OPENAI_API_KEYS=${LITELLM_MASTER_KEY}
```

Прямые backend-порты сохранены для диагностики:

```text
8080 -> llama-architect
8081 -> llama-coder
```

### Проверено

Проверено напрямую через LiteLLM:

* `/v1/models` на порту `4000`;
* chat request к `slowrig/coder`;
* chat request к `slowrig/architect`.

Проверено через Open WebUI:

* Open WebUI работает через LiteLLM;
* модели доступны через gateway;
* прямое подключение Open WebUI к backend-ам больше не является нормальным режимом.

Обновлён `scripts/cluster-status.sh`.

Теперь он проверяет:

* direct backend `8080`;
* direct backend `8081`;
* Open WebUI `3000`;
* LiteLLM `/v1/models` на `4000`;
* chat-запрос через LiteLLM к `slowrig/coder`;
* chat-запрос через LiteLLM к `slowrig/architect`.

### Результат

Stage 3 gateway baseline считается рабочим.

Текущий baseline:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

### Замечания

LiteLLM пока выполняет routing по явно выбранному model name.

Не реализовано:

* автоматический выбор модели по сложности задачи;
* aliases `slowrig/default`, `slowrig/fast`, `slowrig/deep`;
* pipeline `9B -> 27B`;
* memory/RAG;
* Telegram bot;
* agent framework.

`cluster-status.sh` делает реальные короткие LLM-запросы через gateway. Это подходит для ручной диагностики, но не должно использоваться как частый автоматический healthcheck.

---

## 2026-06-19 — Stage 2 operational baseline completed

### Изменено

Создана эксплуатационная основа вокруг уже работающего inference baseline.

Проект получил:

* локальный git-репозиторий;
* `.gitignore`;
* baseline-копию compose-файла;
* README;
* паспорт стенда;
* runbook;
* architecture doc;
* decisions log;
* changelog;
* status script.

Созданы или обновлены документы:

| Файл                     | Назначение                    |
| ------------------------ | ----------------------------- |
| `README.md`              | быстрый вход в проект         |
| `docs/passport.md`       | описание текущего стенда      |
| `docs/runbook.md`        | эксплуатационные инструкции   |
| `docs/architecture.md`   | целевая архитектура           |
| `docs/decisions.md`      | журнал архитектурных решений  |
| `docs/changelog.md`      | фактическая история изменений |
| `docs/stage2-summary.md` | итог Stage 2                  |

Создан скрипт:

```text
scripts/cluster-status.sh
```

Создана baseline-копия:

```text
docker-compose.stage1-baseline.yaml
```

### Git baseline

В `/opt/llama-cluster` создан локальный git-репозиторий.

Добавлен `.gitignore`, исключающий:

* модели;
* кэш;
* runtime data;
* логи;
* секреты;
* временные файлы;
* рабочие директории агентов.

### Status script

`cluster-status.sh` показывает:

* состояние Docker-контейнеров;
* состояние Compose-сервисов;
* GPU и VRAM;
* доступность API;
* доступность Open WebUI;
* последние подозрительные строки логов.

На момент Stage 2 проверялись:

* `llama-architect`;
* `llama-coder`;
* `open-webui`;
* direct backend `8080`;
* direct backend `8081`;
* Open WebUI `3000`.

### Проверено

Проверено, что:

* конфигурации, документация и скрипты версионируются;
* модели и runtime data не попадают в git;
* `scripts/cluster-status.sh` выполняется;
* direct backend-и отвечают;
* Open WebUI работает;
* документационная структура создана.

### Результат

Stage 2 operational baseline завершён.

После Stage 2 проект перестал быть просто набором Docker-контейнеров и получил эксплуатационную основу:

```text
documentation
baseline config
git history
runbook
status script
decisions
changelog
architecture direction
```

### Замечания

На этапе Stage 2 Open WebUI пытался обращаться к Ollama на `host.docker.internal:11434`, хотя Ollama не используется.

Решение:

```text
ENABLE_OLLAMA_API=False
```

Фильтр подозрительных логов в `cluster-status.sh` был уточнён, чтобы не ловить лишний шум.

---

## 2026-06-19 — Stage 1 inference baseline completed

### Изменено

Создана и проверена базовая inference-инфраструктура:

* `llama-architect`;
* `llama-coder`;
* `open-webui`.

### Сервисы

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

Проверено пользователем:

* `llama-architect` стартует;
* `llama-coder` стартует;
* Open WebUI стартует;
* контейнеры работают стабильно;
* `llama-architect` отвечает на порту `8080`;
* `llama-coder` отвечает на порту `8081`;
* Open WebUI работает на порту `3000`;
* короткие запросы работают;
* длинные логи работают;
* несколько файлов в контексте работают;
* обе модели работают с `ctx-size 40000`;
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

Stage 1 inference baseline завершён.

Базовая inference-схема работает и пригодна для дальнейшего построения эксплуатационного слоя.

### Замечания

* 27B близко к пределу VRAM.
* Увеличивать контекст 27B выше `40000` не рекомендуется без отдельного теста.
* 9B имеет запас VRAM на GPU 1.
* Третью LLM-модель на этом этапе не добавлять.
* `llama-architect` может оставаться без aggressive auto-restart на этапе отладки, чтобы не уходить в циклическую перезагрузку при ошибке.

---
