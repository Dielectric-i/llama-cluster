# slowrig AI Cluster — Memory / RAG Design v0.1

Дата: 2026-06-22
Статус: Stage 4.1 design draft; runtime не внедрён

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

## 9. Recommended direction

Рекомендация для `slowrig`:

```text
Stage 4.1: design only
Stage 4.2: approve minimal PostgreSQL + pgvector implementation plan
Stage 4.3: implement only after backup/security plan is clear
```

Почему PostgreSQL + pgvector выглядит лучшим первым implementation-кандидатом:

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

До implementation нужно определить:

* что входит в backup;
* где лежат DB volumes;
* как восстанавливать schema;
* как пересоздавать embeddings;
* нужно ли backup-ить vector index или достаточно rebuild;
* как хранить `.env` отдельно от git;
* как проверять restore.

Минимальный принцип:

```text
source docs backup -> git
derived index backup -> optional, rebuildable
structured task state backup -> mandatory after implementation
```

---

## 13. Manual checks for future implementation

Перед будущим implementation stage:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
```

После добавления DB service в отдельном будущем stage:

```bash
sudo docker compose up -d <memory-db-service>
sudo docker compose ps
sudo docker logs --tail=160 <memory-db-service>
```

Для Stage 4.1 эти команды не требуются, потому что runtime не меняется.

---

## 14. Rollback

Для Stage 4.1 rollback — только документационный:

```bash
git checkout -- docs/memory.md README.md docs/architecture.md docs/decisions.md docs/changelog.md docs/codex-context.md
```

Для будущего implementation stage rollback должен быть отдельным и включать:

* остановку нового service;
* сохранение или удаление volume только после approval;
* возврат compose/config;
* проверку `cluster-status.sh`;
* запись в changelog.

---

## 15. Open questions

Перед implementation нужно решить:

* подтверждает ли оператор PostgreSQL + pgvector как первый runtime stack;
* нужна ли отдельная embedding model;
* какие документы индексировать в первой версии;
* индексировать ли changelog и ADR целиком или summaries;
* хранить ли Open WebUI conversations;
* когда добавлять Telegram history;
* когда добавлять agent task state;
* какие backup expectations принять до появления stateful DB;
* нужен ли Qdrant сразу или только как future upgrade path.

---

## 16. Recommended next stage

Рекомендуемый следующий этап:

```text
Stage 4.2 — Memory implementation plan
```

Цель Stage 4.2:

* утвердить или отклонить PostgreSQL + pgvector как первый runtime stack;
* описать минимальный compose/service plan;
* описать backup/restore;
* определить embeddings source;
* определить initial indexed corpus;
* подготовить rollback.

До завершения Stage 4.2 не устанавливать новые runtime dependencies.
