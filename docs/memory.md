# slowrig AI Cluster — Memory / RAG Design v0.1

Дата: 2026-06-22
Статус: Stage 4.1 design принят для планирования; runtime не внедрён

## 1. Назначение

Этот документ описывает design stage для будущего Memory / RAG слоя `slowrig AI Cluster`.

Документ отвечает на вопросы:

* зачем нужен Memory / RAG слой;
* что остаётся source of truth;
* какие данные можно индексировать;
* какие данные нельзя хранить или embedding-ить;
* какие варианты stack-а рассматриваются;
* какой путь внедрения выглядит наиболее безопасным;
* какие проверки и rollback нужны перед implementation stage.

Этот документ не является инструкцией по установке БД или vector store.

На Stage 4.1 не добавляются:

* новые Docker services;
* PostgreSQL;
* pgvector;
* Qdrant;
* embedding service;
* agent framework;
* Telegram bot;
* новые runtime dependencies.

---

## 2. Цель

Цель Memory / RAG слоя:

```text
дать будущим интерфейсам и агентам управляемый доступ к долговременному проектному контексту
```

Memory / RAG должен помогать:

* находить релевантные фрагменты документации;
* использовать решения и changelog как проектную память;
* подготавливать контекст для `slowrig/coder` и `slowrig/architect`;
* поддержать будущие Telegram и agent workflows;
* хранить структурированное состояние задач после отдельного approval;
* не превращать LiteLLM, Open WebUI или Telegram bot в память.

---

## 3. Non-goals

Stage 4.1 не должен:

* устанавливать БД или vector store;
* менять `docker-compose.yaml`;
* менять LiteLLM routing;
* добавлять новые model names;
* запускать embedding model;
* импортировать историю чатов;
* индексировать `.env`, секреты, приватные данные или сырые логи;
* давать агентам shell-доступ;
* менять backup/security posture.

---

## 4. Current baseline

Текущий baseline:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

Текущие model names:

```text
slowrig/coder
slowrig/architect
```

Текущее состояние Memory / RAG:

```text
not implemented
```

Текущий source of truth:

```text
Markdown + Git
```

Ключевые документы:

* `README.md` — индекс документации;
* `docs/passport.md` — фактическое состояние стенда;
* `docs/runbook.md` — эксплуатация и rollback;
* `docs/architecture.md` — архитектурные слои;
* `docs/gateway.md` — LiteLLM Gateway;
* `docs/decisions.md` — ADR;
* `docs/changelog.md` — фактическая история изменений.

---

## 5. Source of truth and derived data

Главное правило:

```text
Markdown + Git остаются source of truth для документации, решений и операционных процедур.
```

Memory / RAG не должен заменять git-документацию.

Разделение ролей:

| Тип данных | Роль | Где должен жить |
| --- | --- | --- |
| Документация проекта | source of truth | Markdown + Git |
| ADR и changelog | source of truth | Markdown + Git |
| Индекс поиска | derived data | будущий DB/vector store |
| Embeddings | derived data | будущий vector index |
| Task state | structured state | будущая DB после approval |
| Telegram history | optional state | только после Telegram design |
| Agent runs | structured state | только после Agents design |
| Секреты | не хранить | `.env`, вне git и вне memory |

Если индекс повреждён, его должно быть можно пересоздать из Markdown + Git.

---

## 6. Data policy

Можно рассматривать для будущей индексации:

* `README.md`;
* `AGENTS.md`;
* `docs/*.md`;
* короткие summaries проверенных логов;
* summaries внешних репозиториев, если пользователь разрешил;
* результаты аудитов, оформленные как Markdown;
* будущие task summaries.

Не индексировать и не embedding-ить:

* `.env`;
* `secrets/`;
* реальные API keys и tokens;
* приватные Telegram handles, emails и credentials;
* сырые Docker logs без ручной очистки;
* модели и cache;
* временные agent workdirs;
* произвольные пользовательские чаты без отдельного решения.

Перед индексацией логов или чатов нужен отдельный privacy/security decision.

---

## 7. Proposed architecture

Целевая логика:

```text
Client / Agent / Telegram
        |
        v
Memory / RAG access layer
        |
        +--> metadata / task state
        +--> document chunks
        +--> vector search
        |
        v
Prompt context
        |
        v
LiteLLM Gateway
        |
        +--> slowrig/coder
        +--> slowrig/architect
```

Memory / RAG layer должен:

* читать только явно разрешённые источники;
* возвращать релевантный контекст;
* хранить provenance: из какого файла и строки взят фрагмент;
* отделять source document от derived chunk/index;
* позволять пересоздать индекс;
* не выполнять shell-команды;
* не менять файлы без отдельного workflow;
* не обходить LiteLLM для LLM-запросов.

LiteLLM остаётся inference gateway, а не memory layer.

---

## 8. Candidate stacks

## 8.1 Markdown + Git only

Описание:

```text
вся память хранится только в Markdown-документах и git history
```

Плюсы:

* нет новых сервисов;
* простой backup;
* легко ревьюить;
* прозрачно для оператора;
* отлично подходит для ADR, changelog и runbook.

Минусы:

* нет быстрых semantic queries;
* нет structured task state;
* неудобно для Telegram history и agent runs;
* поиск зависит от manual grep/rg и качества summaries.

Подходит для:

* Stage 4.1;
* ранних design docs;
* решений и changelog.

Не подходит как единственный долгосрочный вариант для agent workflows.

---

## 8.2 PostgreSQL + pgvector

Описание:

```text
одна БД хранит structured state, metadata, chunks и vector embeddings
```

Плюсы:

* одна основная зависимость вместо DB + отдельный vector store;
* подходит для task state, Telegram metadata, agent runs и audit records;
* pgvector закрывает базовый semantic search;
* проще backup/restore, чем гибрид нескольких storage;
* хорошо подходит как первая серьёзная memory layer.

Минусы:

* новая runtime-зависимость;
* нужно проектировать backup;
* нужно следить за размером embeddings;
* vector search может быть менее специализированным, чем Qdrant.

Предварительная рекомендация:

```text
лучший кандидат для первого implementation stage после approval
```

Это не финальное решение Stage 4.1.

---

## 8.3 Qdrant + separate metadata storage

Описание:

```text
Qdrant хранит vector index, отдельное хранилище хранит metadata/state
```

Плюсы:

* специализированный vector search;
* хорош для крупных RAG-индексов;
* удобен, если vector retrieval станет главным bottleneck.

Минусы:

* нужна отдельная metadata DB или строгая external source;
* больше сервисов;
* сложнее backup/restore;
* рано для текущего масштаба `slowrig`.

Подходит, если:

* RAG по большим корпусам станет главным сценарием;
* pgvector окажется недостаточным;
* будет понятная стратегия metadata и backup.

---

## 8.4 PostgreSQL + Qdrant hybrid

Описание:

```text
PostgreSQL хранит state/metadata, Qdrant хранит vector index
```

Плюсы:

* сильное разделение structured state и vector search;
* лучше масштабируется для сложного RAG;
* можно оптимизировать каждый слой отдельно.

Минусы:

* две runtime-зависимости;
* сложнее диагностика;
* сложнее backup/restore;
* рано для первого memory stage.

Подходит позже, если:

* появится много документов;
* потребуется высокий recall/latency для RAG;
* PostgreSQL + pgvector перестанет быть достаточным.

---

## 9. Принятое направление

Решение для дальнейшего планирования `slowrig`:

```text
Stage 4.1: design only
Stage 4.2: prepare minimal PostgreSQL + pgvector implementation plan
Stage 4.3: implement only after backup/security plan is clear
```

Пользователь подтвердил `PostgreSQL + pgvector` как целевой runtime-кандидат для Stage 4.2 implementation plan. Это не означает установку БД на Stage 4.1.

Почему PostgreSQL + pgvector выбран для планирования:

* future agents need structured task state;
* Telegram will need conversation metadata and access rules;
* RAG needs chunks, metadata and embeddings;
* one database is easier to operate than DB + separate vector store;
* Markdown + Git remain source of truth and can rebuild the index.

Что остаётся открытым:

* какую embedding model использовать;
* где запускать embeddings;
* какой chunking policy выбрать;
* как хранить provenance;
* какие retention rules применить к chats/logs;
* как делать encrypted/offline backup;
* когда нужен Qdrant.

Принятые ограничения для первого RAG-корпуса:

* индексировать только `README.md`, `AGENTS.md` и `docs/*.md`;
* не индексировать `.env`, `secrets/`, сырые логи, чаты, Open WebUI history или Telegram history;
* embeddings должны быть локальными, но embedding runtime проектируется отдельным подэтапом после DB foundation.

---

## 10. Future minimal schema areas

Это не финальная SQL-схема, а список областей, которые нужно спроектировать перед implementation:

* documents — известные source documents;
* document_chunks — фрагменты документов;
* embeddings — derived vector data;
* ingestion_runs — история индексации;
* tasks — будущие пользовательские/agent tasks;
* task_events — события по задачам;
* conversations — будущая metadata разговоров;
* access_rules — будущие allowlists/policies;
* audit_log — действия memory/agent layer.

На Stage 4.1 SQL schema не создаётся.

---

## 11. Security and privacy

Memory / RAG увеличивает риск случайного сохранения чувствительных данных.

Правила по умолчанию:

* deny by default для источников данных;
* индексировать только разрешённые paths;
* не читать `.env`;
* не читать `secrets/`;
* не индексировать сырые логи без очистки;
* не хранить реальные API keys;
* сохранять provenance;
* иметь delete/reindex path;
* не отправлять данные во внешние APIs без отдельного решения.

Если позже появится внешняя embedding API, это должен быть отдельный security decision.

---

## 12. Backup and restore

Минимальный backup/restore contract для будущего DB stage:

* source docs/config/scripts живут в git;
* `.env` и реальные секреты хранятся offline отдельно от git;
* PostgreSQL state после внедрения backup-ится через logical dump;
* derived embeddings/index считаются rebuildable, пока в них нет уникального пользовательского state;
* перед destructive DB-операциями нужен свежий dump;
* restore должен быть проверяемым dry run, а не только теоретической командой.

Минимальный принцип:

```text
source docs backup -> git
derived index backup -> optional, rebuildable
structured task state backup -> mandatory after implementation
```

Рекомендуемый путь для dump-файлов должен быть вне git. Если позже будет выбран путь внутри `/opt/llama-cluster`, например `backups/`, его нужно добавить в `.gitignore` до первого dump.

На Stage 4.2 реальные dump/restore команды не выполняются, потому что PostgreSQL ещё не внедрён.

---

## 13. Stage 4.2 implementation plan

Статус:

```text
documentation-only plan; runtime not implemented
```

Цель Stage 4.2:

* подготовить минимальный план внедрения PostgreSQL + pgvector;
* описать будущий Docker service, volume, env names и network exposure;
* определить минимальные schema areas без написания финальной SQL-схемы;
* зафиксировать backup/restore contract до появления stateful DB;
* подготовить rollback для будущего Stage 4.3;
* оставить embeddings runtime для отдельного Stage 4.4.

Non-goals Stage 4.2:

* не менять `docker-compose.yaml`;
* не добавлять PostgreSQL service;
* не скачивать Docker images;
* не создавать volume;
* не менять LiteLLM или Open WebUI routing;
* не добавлять embedding service;
* не индексировать документы;
* не подключать Telegram или agents.

Планируемый DB service для Stage 4.3:

| Параметр | План |
| --- | --- |
| Compose service | `memory-db` |
| Container name | `memory-db` |
| Runtime | PostgreSQL + pgvector |
| Image | pinned pgvector-enabled PostgreSQL image; точный tag выбрать и проверить перед Stage 4.3 |
| Network | только Docker Compose network |
| Host port | не публиковать по умолчанию |
| Volume | named volume `memory-db-data` |
| Secrets | только через `.env`, без реальных значений в git |

Планируемые `.env` names:

```text
MEMORY_POSTGRES_DB
MEMORY_POSTGRES_USER
MEMORY_POSTGRES_PASSWORD
```

В `docker-compose.yaml` будущий service должен маппить эти значения в стандартные переменные PostgreSQL:

```text
POSTGRES_DB
POSTGRES_USER
POSTGRES_PASSWORD
```

DB не должна получать host-port на первом runtime stage. Доступ для диагностики должен идти через:

```bash
sudo docker compose exec memory-db psql
```

Если позже понадобится host-port для администрирования, это отдельная security-развилка.

---

## 14. Minimal schema areas for Stage 4.3

Stage 4.3 должен начинаться с минимальной структуры, а не с универсальной платформы памяти.

Минимальные области:

| Area | Назначение |
| --- | --- |
| `documents` | известные source documents и их identity |
| `document_chunks` | фрагменты документов с provenance |
| `embedding_models` | metadata локальных embedding models |
| `embeddings` | derived vector data для chunks |
| `ingestion_runs` | история rebuild/index операций |
| `memory_tasks` | будущая task/state область для agents |
| `task_events` | события по задачам, если task state включён позже |
| `access_rules` | будущие allowlist/policy records |
| `audit_log` | действия memory/agent layer |

Не включать в первую schema без отдельного решения:

* Open WebUI conversations;
* Telegram history;
* raw logs;
* secrets;
* shell command output с чувствительными данными.

Первый индексируемый corpus остаётся:

```text
README.md
AGENTS.md
docs/*.md
```

---

## 15. Manual checks for future implementation

Для Stage 4.2 достаточно документационных проверок:

```bash
git status --short
git diff --stat
git diff -- AGENTS.md docs/codex-context.md docs/memory.md docs/architecture.md docs/decisions.md docs/changelog.md
git diff --check
```

Перед будущим Stage 4.3:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
```

После добавления `memory-db` в отдельном будущем stage:

```bash
sudo docker compose up -d memory-db
sudo docker compose ps
sudo docker logs --tail=160 memory-db
```

Будущая проверка pgvector extension:

```bash
sudo docker compose exec memory-db sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

Внутри `psql`:

```sql
SELECT extname FROM pg_extension WHERE extname = 'vector';
```

Для Stage 4.2 server/runtime checks не требуются, потому что runtime не меняется.

---

## 16. Rollback

Для Stage 4.2 rollback — только документационный:

```bash
git checkout -- docs/memory.md docs/architecture.md docs/decisions.md docs/changelog.md docs/codex-context.md
```

Для будущего Stage 4.3 rollback должен быть отдельным и включать:

* свежий dump перед destructive actions, если DB уже содержит state;
* остановку `memory-db`;
* сохранение или удаление `memory-db-data` только после approval;
* возврат compose/config;
* проверку `cluster-status.sh`;
* запись в changelog.

---

## 17. Открытые вопросы

Перед Stage 4.3 нужно решить:

* нужна ли отдельная embedding model;
* какой exact Docker image/tag использовать для PostgreSQL + pgvector;
* где хранить DB dumps и нужен ли offline/encrypted backup сразу;
* какой migration mechanism использовать для schema;
* какой chunking/provenance формат принять для `docs/*.md`;
* когда добавлять Open WebUI conversations, Telegram history и raw logs;
* когда Qdrant нужен как future upgrade path.

---

## 18. Рекомендуемый следующий этап

Рекомендуемый следующий этап:

```text
Stage 4.3 — Memory DB foundation
```

Цель Stage 4.3:

* добавить `memory-db` только после отдельного approval;
* обновить `docker-compose.yaml`, `.env.example`, `.gitignore` при необходимости и docs;
* проверить Compose config, container logs и pgvector extension;
* выполнить backup/restore dry run;
* не добавлять embeddings runtime и ingestion до Stage 4.4.

До approval на Stage 4.3 не устанавливать новые runtime dependencies.
