# slowrig AI Cluster — Stage 6 Summary

Дата: 2026-06-23
Статус: Stage 6 завершён

## 1. Что завершено

Stage 6 завершил первый безопасный agent слой без установки agent framework и без выдачи опасных прав.

Итоговый выбранный подход:

```text
custom lightweight orchestration / Codex-driven workflow
```

Первый реализованный workflow:

```text
docs drift / repo patch assistant
```

Реализация:

```text
scripts/docs-drift-agent.sh
```

Скрипт выполняет read-only проверки repo/docs/config drift и печатает `OK/WARN/FAIL` отчёт.

## 2. Границы безопасности

Stage 6 не добавил:

* agent framework;
* Docker service;
* task queue;
* persistent agent state;
* Telegram-to-agent escalation;
* shell autonomy;
* diagnostics allowlist with Docker/log commands;
* новые runtime dependencies;
* доступ к `.env` или secrets.

`docs-drift-agent.sh` не читает `.env`, не запускает Docker, не вызывает LLM generation и не меняет файлы.

## 3. Что проверяет первый helper

`docs-drift-agent.sh` проверяет:

* наличие ключевых entrypoint документов;
* что `.env` не tracked и не виден как untracked;
* что README индексирует `docs/*.md`;
* что Compose services отражены в README/passport;
* что текущие `ctx-size`, `tensor-split`, Telegram thinking mode и Stage 6 workflow отражены в документации;
* что в актуальных docs не остались очевидные stale формулировки.

## 4. Проверка

Выполнить:

```bash
cd /opt/llama-cluster
bash -n scripts/docs-drift-agent.sh
scripts/docs-drift-agent.sh
```

Ожидаемый результат:

```text
summary: ok=<n> warn=<n> fail=0
```

Warnings допустимы как audit findings. Failures требуют исправления перед commit/release.

## 5. Rollback

Откатить Stage 6 completion files:

```bash
git checkout -- scripts/docs-drift-agent.sh docs/agents.md docs/codex-context.md docs/decisions.md docs/changelog.md docs/stage6-summary.md README.md
```

## 6. Что дальше

Следующий крупный блок уже начат как Stage 7:

```text
Monitoring / Security / Backups
```

Stage 7.1 подготовил plan для будущего `cluster-health-lite.sh`, но сам runtime script ещё не создан.
