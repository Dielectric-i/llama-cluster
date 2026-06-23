# slowrig AI Cluster — Backups Design v0.1

Дата: 2026-06-23
Статус: Stage 7 design; Stage 7.2 manual backup helper implemented; Stage 7.3 first manual backup completed; automation не внедрена

## 1. Назначение

Этот документ описывает первый backup design для `slowrig AI Cluster`.

Цель:

```text
определить минимальный backup scope до автоматизации и до рискованных security/runtime изменений
```

---

## 2. Первый backup scope

Минимальный scope:

* git-backed repository: docs/config/scripts/source files;
* `.env` отдельно offline, никогда не в git;
* PostgreSQL dump для `memory-db`;
* список model filenames/sources;
* notes по restore order.

Не включать в первый backup scope:

* GGUF model files целиком;
* cache directories;
* raw logs;
* Telegram private history;
* Open WebUI data, пока нет отдельного решения;
* generated embeddings, если их можно rebuild из docs corpus и model.

---

## 3. Memory DB backup

`memory-db` должен иметь dump/restore procedure перед следующими schema/stateful changes.

Базовая идея:

```text
pg_dump -> backups/ -> offline copy
```

`backups/` остаётся ignored by git.

Перед real automation нужно определить:

* filename convention;
* retention;
* encryption/offline copy;
* restore dry run;
* restore target;
* how to avoid printing passwords.

---

## 4. Restore order

Первый restore order:

1. Восстановить git repository files.
2. Восстановить `.env` из offline secret storage.
3. Проверить `docker compose config --quiet`.
4. Запустить базовые services.
5. Восстановить `memory-db` dump, если нужен stateful restore.
6. Пересобрать derived RAG embeddings при необходимости.
7. Проверить `cluster-status.sh` вручную.

---

## 5. Stage 7.2 — Backup/restore implementation plan

Статус:

```text
manual helper implemented; automation and restore are not implemented
```

### Цель

Подготовить минимальный воспроизводимый backup/restore contract для текущего stateful scope:

```text
git-backed repo + offline .env + memory-db pg_dump
```

### Backup artifacts

Планируемые artifacts:

| Artifact | Где хранится | В git? | Комментарий |
| --- | --- | --- | --- |
| Git repository | Git remote / local clone | да | docs/config/scripts/source files |
| `.env` | offline secret storage | нет | вручную, без печати secrets |
| `memory-db` dump | `backups/memory-db/` then offline copy | нет | `backups/` ignored by git |
| model inventory | docs/passport.md / README.md | да | filenames and paths only |

### Filename convention

План для DB dump filename:

```text
backups/memory-db/slowrig-memory-YYYYMMDD-HHMMSS.dump
```

Optional metadata sidecar без secrets:

```text
backups/memory-db/slowrig-memory-YYYYMMDD-HHMMSS.txt
```

Metadata может содержать:

* git commit hash;
* dump timestamp;
* container name;
* database name only if not sensitive;
* command version;
* restore notes.

### Manual backup helper

Ручной helper:

```bash
cd /opt/llama-cluster
scripts/backup-memory-db.sh --check-only
scripts/backup-memory-db.sh
```

`--check-only` проверяет prerequisites и показывает planned paths, но не создаёт dump.

Helper создаёт:

```text
backups/memory-db/slowrig-memory-YYYYMMDD-HHMMSS.dump
backups/memory-db/slowrig-memory-YYYYMMDD-HHMMSS.txt
```

Metadata sidecar не должен содержать secrets.

### Manual backup commands

Создать каталог:

```bash
cd /opt/llama-cluster
mkdir -p backups/memory-db
```

Сделать custom-format dump без печати пароля:

```bash
stamp=$(date -u +%Y%m%d-%H%M%S)
docker compose exec -T memory-db sh -lc 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "backups/memory-db/slowrig-memory-${stamp}.dump"
```

Проверить, что файл создан и не пустой:

```bash
ls -lh backups/memory-db/slowrig-memory-*.dump
```

Не коммитить `backups/`.

### Manual restore dry run plan

Restore dry run нельзя выполнять поверх текущей production DB без отдельного approval.

Безопасный future dry run должен использовать отдельный temporary DB/container или явно созданную test database.

План проверки dump metadata без restore:

```bash
docker compose exec -T memory-db sh -lc 'pg_restore --list' < backups/memory-db/<dump-file>.dump | head -40
```

Полный restore требует отдельного stage, потому что может затронуть stateful data.

### Secrets handling

* `.env` копировать только вручную в offline secret storage.
* Не печатать `.env` values.
* Не добавлять `.env` в backup tarball, который может попасть в git или чат.
* Не индексировать dump или `.env` в Memory/RAG.

### Rollback

Если backup helper окажется неверным:

```bash
git checkout -- scripts/backup-memory-db.sh README.md docs/backups.md docs/changelog.md docs/runbook.md docs/codex-context.md docs/decisions.md
```

Если был создан плохой dump file, удалить только явно выбранный файл в `backups/memory-db/` после проверки пути.

### First manual backup check

Stage 7.3 first manual backup был выполнен 2026-06-23 через:

```bash
scripts/backup-memory-db.sh
```

Результат:

```text
dump: backups/memory-db/slowrig-memory-20260623-080317.dump, 964K
metadata: backups/memory-db/slowrig-memory-20260623-080317.txt, 365 bytes
pg_restore metadata check passed
```

Файлы находятся под `backups/`, ignored by git. Restore не выполнялся. Offline copy остаётся operator task.

### Implementation boundary

Stage 7.2 добавляет manual backup helper. Cron/systemd timer, encryption automation, DB dump execution и restore dry run требуют отдельного approval.
