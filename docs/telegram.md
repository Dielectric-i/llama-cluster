# slowrig AI Cluster — Telegram Bot Design v0.1

Дата: 2026-06-23
Статус: runtime внедрён; Cloudflare short polling и LiteLLM path проверены; no shell/admin access

## 1. Назначение

Этот документ описывает первый безопасный design для Telegram bot в `slowrig AI Cluster`.

Цель Stage 5:

```text
подготовить Telegram-интерфейс к slowrig без установки runtime-зависимостей и без изменения работающего inference baseline
```

Telegram должен быть интерфейсом к кластеру, а не agent framework, shell gateway, memory database или заменой Open WebUI.

---

## 2. Не-цели

Stage 5.2 не должен:

* устанавливать host packages;
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

Env names текущего runtime:

```text
TELEGRAM_BOT_TOKEN
TELEGRAM_ALLOWED_USER_IDS
TELEGRAM_DEFAULT_MODEL
TELEGRAM_ARCHITECT_MODEL
TELEGRAM_API_BASE_URL
TELEGRAM_PROXY_URL
LITELLM_BASE_URL
LITELLM_MASTER_KEY
```

Реальные значения должны жить только в `/opt/llama-cluster/.env` или в будущем dedicated secrets механизме. В git допустимы только placeholders в `.env.example`, для документирования runtime placeholders.

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

## 8. Runtime shape

Минимальный runtime:

```text
custom lightweight C#/.NET bot service
```

Почему:

* сильная типизация и один self-contained application layer;
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

Runtime добавлен как source under `src/telegram-bot` и собирается Docker multi-stage build-ом.

Поведение thinking для текущего Qwen/llama.cpp runtime:

* обычные Telegram chat requests передают в LiteLLM `chat_template_kwargs.enable_thinking=false`;
* текстовый `/no_think` проверен и недостаточен для текущего runtime path: модель воспринимает его как prompt text и возвращает `reasoning_content` при пустом `content`;
* bot logs показывают `thinking_enabled=false`, `finish_reason`, `content_chars` и `reasoning_chars`, но не печатают текст пользовательского prompt;
* если позже понадобится deep reasoning в Telegram, нужно увеличить `max_tokens` и определить отдельную команду или режим, а не включать reasoning молча для обычного chat.

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

## 10. Проверки runtime

Проверки текущего runtime:

```bash
cd /opt/llama-cluster
TELEGRAM_BOT_TOKEN=dummy TELEGRAM_ALLOWED_USER_IDS=123 docker compose config --quiet
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

## 11. Откат

Если `telegram-bot` ломает только себя:

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

## 12. Закрытые runtime-развилки

Stage 5.1/5.2 закрыли следующие развилки:

* Telegram library: не добавлять отдельную Telegram library, используется C# `HttpClient` + Telegram Bot API HTTP polling;
* container strategy: отдельный `telegram-bot` Docker service, local Docker build, .NET 8 runtime image;
* `/status`: только bot + LiteLLM reachability, без Docker/log access;
* `/rag`: не добавлен в первый runtime, оставлен для отдельного read-only RAG stage;
* short context: in-memory per-user context с жёстким лимитом;
* logs: технические события без полного текста сообщений и без secrets.

Открытые вопросы:

* точные input/context limits после первого ручного теста;
* нужен ли read-only `/rag` command после появления Telegram baseline;
* нужна ли opt-in history с retention/delete/export rules.

---

## 13. Runtime implementation

Статус:

```text
runtime внедрён; Cloudflare short polling и LiteLLM path проверены; no shell/admin access
```

Реализованы:

```text
src/telegram-bot/Program.cs
src/telegram-bot/Slowrig.TelegramBot.csproj
src/telegram-bot/Dockerfile
docker-compose.yaml service telegram-bot
config/cloudflare/telegram-worker.js
```

`telegram-bot` — обычный сервис в docker-compose.yaml (без отдельного profile).

Запуск:

```bash
cd /opt/llama-cluster
docker compose up -d telegram-bot
```

Runtime использует:

```text
C#/.NET 8
HttpClient
Telegram Bot API polling
LiteLLM /v1/chat/completions
```

Не добавлены:

* external Telegram library;
* webhook;
* public inbound port;
* shell/Docker/filesystem access;
* Telegram history persistence;
* Memory/RAG writes.

Перед запуском в `/opt/llama-cluster/.env` должны быть реальные значения:

```text
TELEGRAM_BOT_TOKEN
TELEGRAM_ALLOWED_USER_IDS
TELEGRAM_DEFAULT_MODEL=slowrig/coder
TELEGRAM_ARCHITECT_MODEL=slowrig/architect
TELEGRAM_API_BASE_URL=
TELEGRAM_PROXY_URL=
```

Если сервер не может подключиться к `api.telegram.org:443` напрямую, есть два режима обхода.

Reverse proxy mode, например Cloudflare Worker, задаётся как Telegram API base URL:

```text
TELEGRAM_API_BASE_URL=https://example.workers.dev
```

Ожидаемое поведение такого endpoint: запрос `GET /bot000:dummy/getMe` должен доходить до Telegram Bot API и возвращать Telegram JSON, например `401 Unauthorized` на dummy token.

Рекомендуемый Cloudflare Worker source хранится в:

```text
config/cloudflare/telegram-worker.js
```

Worker должен проксировать только Bot API paths вида `/bot.../...`; корень `/` и посторонние `/file`, `/css`, `/js` paths не должны уходить в Telegram.

HTTP(S) proxy mode задаётся как optional proxy:

```text
TELEGRAM_PROXY_URL=http://proxy-host:proxy-port
```

Поддерживаются только HTTP(S) proxy URL вида `http://host:port` или `https://host:port`.
Ссылки Telegram-клиентов вида `tg://proxy?...` являются MTProto proxy и не подходят для Bot API HTTP polling.

Proxy применяется только к Telegram Bot API. Запросы к LiteLLM остаются прямыми внутри Docker Compose network.

Проверить compose config без реальных секретов можно временными значениями:

```bash
cd /opt/llama-cluster
TELEGRAM_BOT_TOKEN=dummy TELEGRAM_ALLOWED_USER_IDS=123 docker compose config --quiet
```

Проверить syntax:

```bash
cd /opt/llama-cluster/src/telegram-bot
dotnet build
```

Проверить Docker build:

```bash
cd /opt/llama-cluster
docker compose build telegram-bot
```

Manual Telegram checks после запуска:

* unknown user получает отказ;
* allowed user получает ответ на `/start`;
* `/help` показывает команды;
* `/status` показывает bot/gateway status;
* обычный prompt отвечает через `slowrig/coder`;
* `/architect` переключает следующий запрос на `slowrig/architect`;
* `/reset` сбрасывает short in-memory context.

Transport hardening defaults после Stage 5.4:

* Cloudflare reverse proxy mode использует short polling: `getUpdates timeout=0`;
* Telegram polling HTTP timeout: `8s`;
* idle delay after empty poll: `1200ms`;
* обычные Telegram API calls, включая `sendMessage`: `25s`;
* `sendMessage` retry attempts: `3`;
* logs показывают method, attempt, elapsed time, update checkpoint, LLM request/response timing, `finish_reason`, `content_chars` и `reasoning_chars`, но не печатают message text или secrets;
* обычные Telegram prompts отправляются в LiteLLM с `chat_template_kwargs.enable_thinking=false`, чтобы короткие ответы возвращались в `content`, а не уходили целиком в `reasoning_content`.

Будущая опция: увеличить `max_tokens`, если понадобится отдельный Telegram режим с включённым reasoning и финальным ответом. Это отдельный tuning step, не текущий default.

Откат:

```bash
cd /opt/llama-cluster
docker compose stop telegram-bot
```

---

## 14. Возможные будущие работы

* отдельный deep reasoning режим с большим `max_tokens`;
* уточнение command policy;
* Telegram-to-agent escalation после agent safety stage;
* rate limits и abuse handling;
* не добавлять admin commands, RAG writes или history persistence без отдельного design/approval.
