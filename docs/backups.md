# slowrig AI Cluster — Backups Design v0.1

Дата: 2026-06-23
Статус: Stage 7 design; backup automation не внедрена

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

## 5. Future implementation plan

Рекомендуемый следующий backups stage:

```text
Stage 7.2 — backup/restore implementation plan
```

Он должен определить commands, filenames, encryption/offline policy, restore dry run и rollback.

Автоматизацию backup jobs не включать без отдельного approval.
