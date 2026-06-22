# slowrig AI Cluster — Telegram Bot Design v0.1

Дата: 2026-06-22
Статус: Stage 5 design; runtime не внедрён

## 1. Назначение

Этот документ описывает первый безопасный design для Telegram bot в `slowrig AI Cluster`.

Цель Stage 5:

```text
подготовить Telegram-интерфейс к slowrig без установки runtime-зависимостей и без изменения работающего inference baseline
```

Telegram должен быть интерфейсом к кластеру, а не agent framework, shell gateway, memory database или заменой Open WebUI.

---

## 2. Non-goals

Stage 5 design не должен:

* создавать Telegram bot runtime;
* устанавливать Python/Node packages;
* добавлять Docker service;
* менять `docker-compose.yaml`;
* менять LiteLLM routing;
* добавлять новые gateway model names;
* открывать public inbound ports;
* давать Telegram shell, Docker или filesystem access;
* индексировать Telegram history в Memory/RAG;
* хранить реальные bot tokens в git;
* менять firewall, reverse proxy или VPN.

---

## 3. Принятый первый вариант

Первый Telegram-вариант:

```text
Telegram Bot API polling + whitelist -> LiteLLM Gateway -> slowrig/coder
```

Схема:

```text
Telegram user
    |
    v
Telegram Bot API
    |
    v
slowrig Telegram bot service
    |
    v
LiteLLM Gateway
    |
    +--> slowrig/coder
    +--> slowrig/architect only by explicit command/escalation
```

Причина выбора polling:

* не нужен public inbound port;
* не нужен webhook endpoint;
* проще rollback;
* лучше соответствует LAN/VPN-first posture;
* меньше security surface на первом этапе.

Компромисс:

* polling менее элегантен, чем webhook;
* latency зависит от polling interval;
* long-running bot process всё равно должен быть monitored.

---

## 4. Модель доступа

Доступ по умолчанию:

```text
deny by default
```

Нужен whitelist Telegram user IDs.

Планируемые env names для будущего implementation stage:

```text
TELEGRAM_BOT_TOKEN
TELEGRAM_ALLOWED_USER_IDS
TELEGRAM_DEFAULT_MODEL
TELEGRAM_ARCHITECT_MODEL
LITELLM_BASE_URL
LITELLM_MASTER_KEY
```

Реальные значения должны жить только в `/opt/llama-cluster/.env` или в будущем dedicated secrets механизме. В git допустимы только placeholders в `.env.example`, когда runtime stage будет одобрен.

Минимальные правила:

* unknown user получает отказ без подробностей о внутренней архитектуре;
* allowed user может отправлять обычные chat-запросы;
* bot не печатает секреты;
* bot не имеет shell/Docker доступа;
* bot не принимает arbitrary commands;
* bot не пишет в Memory/RAG без отдельного решения.

---

## 5. Routing policy

Default route:

```text
slowrig/coder
```

`slowrig/coder` подходит для:

* коротких вопросов;
* быстрых summaries;
* обычных рабочих запросов;
* команд Telegram, где важна отзывчивость.

`slowrig/architect` использовать только:

* явной командой пользователя;
* по документированному escalation rule;
* для сложных решений, где пользователь готов ждать;
* после отдельного prompt/context preparation.

Не добавлять новые gateway aliases на первом Telegram stage:

```text
slowrig/default
slowrig/telegram
slowrig/fast
slowrig/deep
```

Если такие aliases понадобятся, это отдельное gateway/routing decision.

---

## 6. Команды первого интерфейса

Минимальная command surface для первого implementation stage:

| Команда | Назначение |
| --- | --- |
| `/start` | короткое подтверждение доступа |
| `/help` | список доступных команд без раскрытия секретов |
| `/status` | безопасный статус bot/gateway без Docker/log access |
| `/model` | показать текущую выбранную модель |
| `/coder` | переключить текущий диалог на `slowrig/coder` |
| `/architect` | явно запросить `slowrig/architect` |
| `/reset` | сбросить короткий in-memory context текущего пользователя |

Не добавлять в первый Telegram runtime:

* shell commands;
* Docker commands;
* log tail;
* file reads;
* git mutations;
* compose restart/down/up;
* Memory/RAG writes;
* admin commands без отдельного permissions design.

---

## 7. Conversation state

Первый вариант state:

```text
short in-memory per-user context
```

Не хранить Telegram history в PostgreSQL на первом implementation stage.

Причина:

* Telegram history содержит private user data;
* нужны retention/delete правила;
* нужен backup/security decision;
* Memory/RAG сейчас индексирует только проектную документацию.

Допустимо позже рассмотреть:

* хранение sanitized summaries;
* хранение user preferences;
* хранение task state;
* opt-in history;
* TTL для history;
* delete/export commands.

Это отдельная privacy/security развилка.

---

## 8. Runtime shape для будущего Stage 5.1

Рекомендуемый минимальный runtime:

```text
custom lightweight Python bot service
```

Почему:

* меньше framework overhead;
* проще audit;
* проще permissions boundary;
* легче сохранить no-shell rule;
* достаточно для polling + whitelist + LiteLLM calls.

Будущий service:

```text
telegram-bot
```

Рекомендуемый network access:

* outbound к Telegram Bot API;
* inbound public port не нужен;
* access к `litellm:4000` внутри Docker network;
* no direct access к `llama-coder` / `llama-architect` как обычный путь;
* no access к Docker socket;
* no host filesystem mount, кроме read-only config при необходимости.

Перед добавлением runtime нужно отдельно выбрать:

* Python package / library;
* container image strategy;
* dependency pinning;
* restart policy;
* logging policy;
* healthcheck;
* `.env.example` placeholders;
* rollback path.

---

## 9. Security and privacy

Секреты:

* `TELEGRAM_BOT_TOKEN` не хранить в git;
* `LITELLM_MASTER_KEY` не печатать в logs;
* `.env` не коммитить;
* не просить пользователя вставлять реальные tokens в чат.

Access:

* whitelist обязателен;
* public WebUI/Gateway exposure не меняется;
* Telegram polling не открывает inbound port;
* bot не даёт shell/Docker/filesystem доступ;
* dangerous operations остаются вне Telegram до отдельного admin/security design.

Logs:

* не логировать полный текст сообщений по умолчанию;
* логировать минимальные технические события;
* ошибки sanitizе-ить от secrets;
* не отправлять raw logs в Memory/RAG.

---

## 10. Проверки для будущего runtime stage

Когда будет одобрен implementation stage, минимальные проверки:

```bash
cd /opt/llama-cluster
docker compose config --quiet
docker compose up -d telegram-bot
docker compose ps telegram-bot
docker logs --tail=120 telegram-bot
```

Functional checks:

* unknown Telegram user получает отказ;
* allowed user получает ответ на `/start`;
* `/help` работает;
* обычный prompt идёт в `slowrig/coder`;
* `/architect` явно использует `slowrig/architect`;
* LiteLLM `/v1/models` продолжает отвечать;
* общий `/opt/llama-cluster/scripts/cluster-status.sh` остаётся OK.

Не считать runtime verified без ручного Telegram UI check.

---

## 11. Rollback для будущего runtime stage

Если будущий `telegram-bot` ломает только себя:

```bash
cd /opt/llama-cluster
docker compose stop telegram-bot
```

Если нужно откатить config/docs будущего stage:

```bash
cd /opt/llama-cluster
git checkout -- docker-compose.yaml .env.example README.md docs/telegram.md docs/runbook.md docs/architecture.md docs/decisions.md docs/changelog.md docs/codex-context.md
```

Не использовать:

```bash
docker compose down -v
git reset --hard
```

---

## 12. Открытые вопросы перед runtime

Перед Stage 5.1 нужно решить:

* какую Python Telegram library использовать;
* делать ли bot Docker image локально или использовать lightweight base image;
* нужен ли `/status` только для bot/gateway или ещё для memory;
* нужен ли `/rag` command для read-only поиска по документации;
* сколько short context хранить in-memory;
* какие limits ставить на длину входа и частоту запросов;
* какой формат логов считать безопасным.

---

## 13. Рекомендуемый следующий этап

Рекомендуемый следующий этап:

```text
Stage 5.1 — Telegram bot implementation plan
```

Цель Stage 5.1:

* выбрать библиотеку и container strategy;
* описать compose service без запуска;
* подготовить `.env.example` placeholders;
* описать checks и rollback;
* не запускать runtime до отдельного approval.
