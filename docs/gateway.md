# slowrig AI Cluster — Gateway

Дата актуализации: 2026-06-22
Статус: LiteLLM Gateway внедрён и используется как текущий gateway baseline

## 1. Назначение документа

Этот документ описывает Gateway / Router слой в `slowrig AI Cluster`.

Gateway отвечает за единую OpenAI-compatible точку входа к локальным LLM backend-ам.

Документ фиксирует:

* зачем нужен gateway;
* какая схема внедрена сейчас;
* какие model names использовать клиентам;
* как Open WebUI подключён к gateway;
* какие backend-и доступны за gateway;
* какие ограничения есть у текущего baseline;
* какие правила безопасности действуют;
* как временно откатить Open WebUI на direct backend mode.

Этот документ не заменяет:

* `README.md` — быстрый вход и индекс документации;
* `docs/passport.md` — фактический паспорт стенда;
* `docs/runbook.md` — команды диагностики, restart и rollback;
* `docs/architecture.md` — общую архитектуру системы;
* `docs/decisions.md` — причины архитектурных решений;
* `docs/changelog.md` — фактическую историю изменений.

---

## 2. Текущая gateway-схема

Текущая основная цепочка:

```text
Open WebUI
    |
    v
LiteLLM Gateway / port 4000
    |
    +--> slowrig/coder      -> llama-coder / 9B / host port 8081
    |
    +--> slowrig/architect  -> llama-architect / 27B / host port 8080
```

Сервис gateway:

```text
litellm
```

Host endpoint:

```text
http://192.168.1.6:4000/v1
```

Docker network endpoint:

```text
http://litellm:4000/v1
```

Gateway model names:

```text
slowrig/coder
slowrig/architect
```

Обычные клиенты должны использовать gateway model names, а не имена GGUF-файлов и не прямые backend-порты.

---

## 3. Зачем нужен gateway

Раньше Open WebUI мог обращаться напрямую к llama.cpp backend-ам.

Текущий baseline использует LiteLLM Gateway, потому что будущие клиенты не должны знать внутреннюю схему моделей и портов.

Gateway нужен для:

* единой OpenAI-compatible точки входа;
* стабильных публичных model names;
* маршрутизации между 9B и 27B backend-ами;
* подключения Open WebUI, Telegram, IDE и будущих агентов через один слой;
* будущей авторизации и API keys;
* будущих access policies;
* будущих routing policies;
* будущих логов запросов;
* будущих лимитов и очередей;
* возможности заменить backend без изменения клиентов.

Gateway также снижает связанность клиентов с конкретными внутренними сервисами.

Клиент должен знать:

```text
model: slowrig/coder
```

или:

```text
model: slowrig/architect
```

Клиент не должен зависеть от:

```text
Qwen3.5-9B-UD-Q4_K_XL.gguf
Qwen3.6-27B-UD-Q4_K_XL.gguf
http://llama-coder:8080/v1
http://llama-architect:8080/v1
```

---

## 4. Что gateway не делает

LiteLLM Gateway не должен становиться “всем сразу”.

Gateway не является:

* memory/RAG-слоем;
* базой данных;
* агентским фреймворком;
* системой выполнения shell-команд;
* системой редактирования файлов;
* заменой Open WebUI;
* заменой llama.cpp;
* мониторингом GPU;
* владельцем долгосрочного project state.

Правильная роль gateway в текущем baseline:

```text
принять OpenAI-compatible запрос
проверить model name
передать запрос нужному backend-у
вернуть ответ клиенту
```

Память, RAG, Telegram, агенты, мониторинг и backup должны проектироваться как отдельные subsystem-ы.

---

## 5. Backend-и за gateway

### 5.1 `slowrig/coder`

Gateway model name:

```text
slowrig/coder
```

Backend service:

```text
llama-coder
```

Backend role:

```text
9B fast worker / coder backend
```

Docker network endpoint:

```text
http://llama-coder:8080/v1
```

Host diagnostic endpoint:

```text
http://127.0.0.1:8081/v1
```

Это не противоречие: внутри Docker network `llama-coder` слушает порт `8080`, а на host он опубликован как diagnostic port `8081`.

Назначение:

* быстрые ответы;
* простые coding-задачи;
* первичный анализ файлов и логов;
* summaries;
* подготовка контекста;
* будущий default backend для Telegram;
* будущий fast worker для agent workflows.

Ожидаемое использование:

```text
default / fast / frequent tasks
```

---

### 5.2 `slowrig/architect`

Gateway model name:

```text
slowrig/architect
```

Backend service:

```text
llama-architect
```

Backend role:

```text
27B architect / deep reasoning backend
```

Docker network endpoint:

```text
http://llama-architect:8080/v1
```

Host diagnostic endpoint:

```text
http://127.0.0.1:8080/v1
```

Назначение:

* глубокое reasoning;
* архитектурные решения;
* сложный DevOps/ML-анализ;
* ревью сложного кода;
* финальные выводы после подготовки контекста;
* планирование agent workflows.

Ожидаемое использование:

```text
deep / manual / expensive reasoning tasks
```

27B backend не должен использоваться как default для частых мелких запросов.

---

## 6. Текущая routing policy

Текущий baseline использует ручной выбор модели.

Клиент явно выбирает одну из моделей:

```text
slowrig/coder
slowrig/architect
```

Текущий gateway не выполняет автоматическую классификацию сложности задачи.

Текущий gateway не реализует pipeline:

```text
9B -> 27B
```

Текущий gateway не реализует автоматическую стратегию:

```text
сначала summary через 9B
затем финальное решение через 27B
```

Такая логика относится к будущему agent layer или отдельному router/orchestrator слою.

---

## 7. Будущие routing policy

В будущем можно добавить стабильные aliases:

```text
slowrig/default
slowrig/fast
slowrig/deep
slowrig/telegram
slowrig/code-review
slowrig/repo-audit
```

Предварительная логика:

| Alias                 | Предполагаемый backend |
| --------------------- | ---------------------- |
| `slowrig/default`     | 9B                     |
| `slowrig/fast`        | 9B                     |
| `slowrig/deep`        | 27B                    |
| `slowrig/telegram`    | 9B                     |
| `slowrig/code-review` | 27B                    |
| `slowrig/repo-audit`  | future pipeline        |

Эти aliases пока не являются текущим baseline.

Добавлять их нужно только после отдельного решения и обновления:

* `config/litellm.config.yaml`;
* `docs/gateway.md`;
* `docs/runbook.md`;
* `docs/changelog.md`;
* при необходимости `docs/decisions.md`.

---

## 8. Open WebUI routing

Open WebUI сейчас подключён к LiteLLM Gateway.

Текущая логика подключения:

```text
Open WebUI -> http://litellm:4000/v1
```

Смысловая конфигурация:

```text
OPENAI_API_BASE_URLS=http://litellm:4000/v1
OPENAI_API_KEYS=${LITELLM_MASTER_KEY}
```

Фактические значения секретов должны храниться только в:

```text
.env
```

Open WebUI не должен в нормальном режиме обращаться напрямую к:

```text
http://llama-coder:8080/v1
http://llama-architect:8080/v1
```

Direct backend mode допустим только как rollback path.

---

## 9. Конфигурационные файлы

Gateway-related files:

```text
config/litellm.config.yaml
.env
.env.example
docker-compose.yaml
```

Назначение:

| Файл                         | Назначение                             |
| ---------------------------- | -------------------------------------- |
| `config/litellm.config.yaml` | LiteLLM model list and backend mapping |
| `.env`                       | реальные секреты и runtime values      |
| `.env.example`               | безопасный шаблон переменных           |
| `docker-compose.yaml`        | сервис `litellm`, порты, env, mounts   |

`.env` не должен попадать в git.

`.env.example` должен содержать только имена переменных и безопасные placeholders.

В документации можно указывать имена переменных:

```text
LITELLM_MASTER_KEY
LITELLM_SALT_KEY
```

Нельзя указывать реальные значения.

---

## 10. Проверенный Stage 3 baseline

Stage 3 Gateway baseline считается внедрённым.

Фактически внедрено:

* добавлен сервис `litellm`;
* опубликован host port `4000`;
* добавлен `config/litellm.config.yaml`;
* добавлен `.env.example`;
* реальные секреты вынесены в `.env`;
* добавлены gateway model names;
* Open WebUI переключён на LiteLLM Gateway;
* прямые backend-порты `8080` и `8081` сохранены для диагностики;
* `scripts/cluster-status.sh` проверяет LiteLLM и обе модели через gateway.

Проверенные gateway model names:

```text
slowrig/coder
slowrig/architect
```

---

## 11. Минимальная диагностика

Подробные команды диагностики живут в:

```text
docs/runbook.md
```

Минимальная логика проверки:

1. Проверить, что direct backend-и живы.
2. Проверить, что LiteLLM отвечает на `/v1/models`.
3. Проверить короткий запрос к `slowrig/coder`.
4. Проверить короткий запрос к `slowrig/architect`.
5. Проверить, что Open WebUI видит модели через gateway.

Главная команда ручной проверки:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

Важно:

```text
cluster-status.sh делает реальные короткие LLM-запросы.
```

Этот скрипт подходит для ручной диагностики, но не должен использоваться как частый автоматический healthcheck.

---

## 12. Как отличать проблемы gateway от проблем backend

Базовая логика:

```text
8080/8081 работают, 4000 не работает
=> проблема в LiteLLM или его конфиге

8080/8081 не работают
=> проблема ниже gateway, в llama.cpp backend-е

4000 работает, Open WebUI не видит модели
=> проблема в Open WebUI config или API key

4000 работает, но только одна модель отвечает
=> проблема в конкретном backend mapping или конкретном backend-е
```

При диагностике не менять сразу несколько слоёв.

Правильный порядок:

```text
direct backend -> LiteLLM -> Open WebUI -> client behavior
```

---

## 13. Rollback: Open WebUI direct backend mode

Если LiteLLM нужно временно обойти, Open WebUI можно вернуть на прямые backend-и.

Direct backend config:

```yaml
- OPENAI_API_BASE_URLS=http://llama-coder:8080/v1;http://llama-architect:8080/v1
- OPENAI_API_KEYS=dummy;dummy
```

Этот rollback-фрагмент используется из контейнера `open-webui`, поэтому в нём указаны Docker network endpoints. Для host-диагностики `llama-coder` остаётся доступен на `127.0.0.1:8081`.

После изменения применить только Open WebUI:

```bash
cd /opt/llama-cluster
sudo docker compose up -d open-webui
```

Этот режим является rollback path.

Он не является предпочтительной архитектурой.

После восстановления LiteLLM нужно вернуть Open WebUI к gateway mode:

```yaml
- OPENAI_API_BASE_URLS=http://litellm:4000/v1
- OPENAI_API_KEYS=${LITELLM_MASTER_KEY}
```

---

## 14. Security baseline

Текущий gateway baseline рассчитан на домашнюю LAN.

Не открывать наружу без отдельного security stage:

```text
3000  # Open WebUI
4000  # LiteLLM Gateway
8080  # llama-architect direct backend
8081  # llama-coder direct backend
```

Для внешнего доступа позже нужен отдельный план:

* VPN;
* reverse proxy с авторизацией;
* firewall restrictions;
* gateway API key policy;
* включённая авторизация Open WebUI;
* whitelist для будущих клиентов.

Секреты:

* реальные ключи хранить в `.env`;
* не печатать `.env`;
* не просить пользователя присылать `.env`;
* не коммитить `.env`;
* не писать реальные ключи в документацию;
* не писать реальные ключи в commit messages.

---

## 15. Known limitations

Текущие ограничения:

* routing выполняется по явно выбранному model name;
* автоматический выбор модели по сложности задачи не реализован;
* aliases `slowrig/default`, `slowrig/fast`, `slowrig/deep` пока не реализованы;
* pipeline `9B -> 27B` не реализован;
* LiteLLM не является memory/RAG-слоем;
* LiteLLM не является agent framework;
* Telegram ещё не подключён;
* IDE-клиенты ещё не подключены;
* CrewAI/OpenClaw ещё не подключены;
* прямые порты `8080` и `8081` пока оставлены для диагностики;
* частый автоматический healthcheck через реальные LLM-запросы не настроен и не должен использовать `cluster-status.sh` без упрощения.

---

## 16. Когда обновлять этот документ

Обновлять `docs/gateway.md`, если меняется:

* gateway service;
* port `4000`;
* model names;
* backend mapping;
* LiteLLM config path;
* Open WebUI routing;
* authentication/API key policy;
* gateway aliases;
* routing policy;
* direct backend policy;
* rollback path;
* gateway-related security posture.

Если изменение является архитектурным решением, также обновить:

```text
docs/decisions.md
```

Если изменение фактически применено и проверено, также обновить:

```text
docs/changelog.md
```

Если изменение влияет на команды эксплуатации, также обновить:

```text
docs/runbook.md
```

Если появляется новый клиент через gateway, например Telegram или IDE, создать или обновить соответствующий subsystem document.

---
