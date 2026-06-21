# slowrig AI Cluster — Stage 3 Summary

Дата: 2026-06-21
Статус: Stage 3 Gateway baseline завершён.

## 1. Что завершено

Stage 3 добавил в `slowrig AI Cluster` отдельный LLM Gateway / Router на базе LiteLLM Proxy.

До Stage 3 Open WebUI был подключён напрямую к двум llama.cpp backend-ам:

```text
Open WebUI -> llama-coder / llama-architect
```

После Stage 3 текущая рабочая схема стала такой:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

Gateway стал центральной OpenAI-compatible точкой входа для текущих и будущих клиентов.

## 2. Текущие сервисы

| Сервис            |   Порт | Роль                           |
| ----------------- | -----: | ------------------------------ |
| `llama-architect` | `8080` | 27B architect / deep reasoning |
| `llama-coder`     | `8081` | 9B coder / fast worker         |
| `litellm`         | `4000` | LLM Gateway / Router           |
| `open-webui`      | `3000` | ручной WebUI через LiteLLM     |

## 3. Текущая схема

```text
Open WebUI
    |
    v
LiteLLM Gateway / port 4000
    |
    +--> slowrig/coder      -> llama-coder / 9B / port 8081 / GPU 1 x16
    |
    +--> slowrig/architect  -> llama-architect / 27B / port 8080 / GPU 0 + GPU 2
```

## 4. Gateway model names

LiteLLM предоставляет стабильные имена моделей:

| Gateway model name  | Backend           | Роль               |
| ------------------- | ----------------- | ------------------ |
| `slowrig/coder`     | `llama-coder`     | быстрая 9B-модель  |
| `slowrig/architect` | `llama-architect` | тяжёлая 27B-модель |

Клиенты должны использовать именно gateway model names, а не имена GGUF-файлов.

Причины:

* GGUF-файл может измениться;
* роль модели важнее имени файла;
* клиентам удобнее использовать стабильные имена;
* backend можно заменить без изменения клиентских настроек.

## 5. Backend-и

### 5.1 `llama-coder`

Модель:

```text
Qwen3.5-9B-UD-Q4_K_XL.gguf
```

Порт на хосте:

```text
8081
```

Роль:

* быстрые ответы;
* предварительный анализ;
* суммаризация;
* разбор логов;
* простые кодовые задачи;
* будущий Telegram worker;
* будущий worker для агентских сценариев.

GPU:

```text
host GPU 1 / PCIe x16
```

Контекст:

```text
ctx-size 40000
```

### 5.2 `llama-architect`

Модель:

```text
Qwen3.6-27B-UD-Q4_K_XL.gguf
```

Порт на хосте:

```text
8080
```

Роль:

* сложное reasoning;
* архитектурные решения;
* DevOps/ML-анализ;
* ревью сложного кода;
* планирование задач;
* финальные выводы после подготовки контекста.

GPU:

```text
host GPU 0 + host GPU 2
```

Контекст:

```text
ctx-size 40000
```

Особенности:

* модель работает на двух GPU с ограниченными PCIe-линиями;
* `parallel 1` должен оставаться базовым безопасным режимом;
* не использовать CPU offload как штатный режим;
* не повышать контекст или параллельность без отдельного теста.

## 6. Файлы gateway

Конфигурация LiteLLM:

```text
config/litellm.config.yaml
```

Локальные секреты:

```text
.env
```

Шаблон секретов:

```text
.env.example
```

Важно:

* `.env` содержит реальные ключи и не хранится в git;
* `.env.example` можно хранить в git;
* `config/litellm.config.yaml` можно хранить в git, пока в нём нет реальных внешних API-ключей;
* реальные значения `LITELLM_MASTER_KEY` и `LITELLM_SALT_KEY` нельзя публиковать, копировать в документацию или коммитить.

## 7. Open WebUI после Stage 3

Open WebUI теперь использует LiteLLM Gateway.

Текущая конфигурационная логика:

```text
OPENAI_API_BASE_URLS=http://litellm:4000/v1
OPENAI_API_KEYS=${LITELLM_MASTER_KEY}
```

Open WebUI больше не должен использовать прямое подключение к backend-ам как штатный режим.

Direct backend mode оставлен только как rollback-вариант.

## 8. Диагностические backend-порты

Прямые backend-порты пока оставлены доступными:

```text
8080 -> llama-architect
8081 -> llama-coder
```

Причина:

* быстро проверить, жив ли llama.cpp backend;
* отличить проблему LiteLLM от проблемы модели;
* иметь простой rollback-путь;
* не ломать Stage 1 inference baseline при проблемах gateway.

Позже, после длительной проверки gateway, можно будет ограничить прямой доступ к backend-ам и оставить их только для локальной диагностики.

## 9. Проверки Stage 3

Проверено:

* контейнер `litellm` запускается;
* порт `4000` опубликован на хосте;
* `/v1/models` через LiteLLM отвечает;
* `slowrig/coder` отвечает через LiteLLM;
* `slowrig/architect` отвечает через LiteLLM;
* Open WebUI работает через LiteLLM;
* прямые backend-и `8080` и `8081` продолжают работать для диагностики;
* `scripts/cluster-status.sh` проверяет gateway и оба gateway model names.

Основная команда проверки:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

Ожидаемые gateway-строки:

```text
litellm gateway -> http://127.0.0.1:4000/v1/models : OK
litellm chat 9B (slowrig/coder) : OK
litellm chat 27B (slowrig/architect) : OK
```

## 10. Что изменилось в документации

В Stage 3 обновлены документы:

```text
README.md
docs/passport.md
docs/runbook.md
docs/architecture.md
docs/gateway.md
docs/decisions.md
docs/changelog.md
docs/stage2-summary.md
```

Основные зафиксированные изменения:

* LiteLLM добавлен как рабочий сервис;
* текущая архитектура обновлена до `Open WebUI -> LiteLLM -> llama.cpp backend`;
* добавлены gateway model names `slowrig/coder` и `slowrig/architect`;
* добавлены команды диагностики LiteLLM;
* добавлен rollback Open WebUI на direct backend mode;
* добавлены ADR для LiteLLM Gateway и маршрутизации Open WebUI через LiteLLM;
* Stage 2 summary оставлен историческим документом и дополнен ссылкой на Stage 3.

## 11. Архитектурные решения Stage 3

В Stage 3 приняты и внедрены решения:

```text
ADR-015 — Использовать LiteLLM Proxy как первый gateway
ADR-016 — Маршрутизировать Open WebUI через LiteLLM
```

Старое решение о будущем gateway закрыто:

```text
ADR-012 — superseded by ADR-015
```

Итоговая позиция:

```text
LiteLLM Proxy выбран как первый LLM Gateway / Router для slowrig.
Open WebUI теперь использует LiteLLM как основную точку входа.
```

## 12. Что намеренно не реализовано

Stage 3 не реализует:

* автоматическое определение сложности задачи;
* автоматический pipeline `9B -> 27B`;
* memory/RAG;
* Telegram bot;
* CrewAI / OpenClaw;
* полноценные access policies;
* полноценное request logging;
* rate limits;
* очереди запросов;
* fallback между backend-ами;
* внешний доступ;
* monitoring stack.

LiteLLM сейчас выполняет routing по явно выбранному имени модели:

```text
slowrig/coder
slowrig/architect
```

Автоматическая маршрутизация по типу задачи — задача будущих этапов.

## 13. Ограничения текущего gateway baseline

Текущие ограничения:

* LiteLLM добавляет дополнительный слой диагностики;
* Open WebUI теперь зависит от LiteLLM;
* если gateway сломается, WebUI может перестать видеть модели, даже если backend-и работают;
* прямые backend-порты `8080/8081` пока открыты для диагностики;
* `cluster-status.sh` делает реальные короткие LLM-запросы;
* `cluster-status.sh` не стоит использовать как частый автоматический healthcheck;
* LiteLLM не является memory/RAG-слоем;
* LiteLLM не является агентским фреймворком;
* LiteLLM не должен выполнять shell-команды или редактировать файлы.

## 14. Rollback

Если LiteLLM временно нужно обойти, Open WebUI можно вернуть на прямые backend-и.

Direct backend config:

```yaml
- OPENAI_API_BASE_URLS=http://llama-coder:8080/v1;http://llama-architect:8080/v1
- OPENAI_API_KEYS=dummy;dummy
```

После изменения:

```bash
cd /opt/llama-cluster
sudo docker compose up -d open-webui
```

После rollback нужно проверить:

```bash
sudo docker logs --tail=160 open-webui
curl http://127.0.0.1:8080/v1/models
curl http://127.0.0.1:8081/v1/models
```

## 15. Безопасность

Не открывать наружу без отдельного плана защиты:

```text
3000 -> Open WebUI
4000 -> LiteLLM Gateway
8080 -> llama-architect
8081 -> llama-coder
```

Текущий режим допустим только в домашней LAN.

Перед любым внешним доступом нужен отдельный дизайн:

* VPN;
* reverse proxy;
* auth;
* firewall;
* whitelist;
* отдельная security-документация;
* rollback-план.

Секреты:

* не коммитить `.env`;
* не публиковать `LITELLM_MASTER_KEY`;
* не публиковать `LITELLM_SALT_KEY`;
* не вставлять реальные ключи в Markdown;
* не копировать полный вывод `docker compose config`, если в нём могут быть раскрыты секреты.

## 16. Итог Stage 3

Stage 3 Gateway baseline завершён.

Теперь `slowrig` имеет центральную OpenAI-compatible точку входа:

```text
http://192.168.1.6:4000/v1
```

Эта точка уже используется Open WebUI и может быть использована будущими клиентами:

* Telegram bot;
* IDE assistant;
* CrewAI;
* OpenClaw;
* custom scripts;
* memory/RAG layer;
* documentation worker;
* repo auditor;
* agent framework.

Текущий baseline:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

## 17. Следующий этап

Следующий логичный этап:

```text
Stage 4 — Memory / RAG design
```

Цель Stage 4:

* определить, какие типы памяти нужны;
* выбрать memory stack;
* спроектировать хранение истории решений;
* спроектировать RAG по документации и репозиториям;
* определить, как память будет связана с LiteLLM Gateway;
* определить, как 9B и 27B будут использовать память;
* решить, использовать PostgreSQL + pgvector, Qdrant или гибридный вариант.

Предварительная рекомендация:

```text
сначала создать docs/memory.md как design-документ,
не устанавливая новые контейнеры.
```

Наиболее вероятный базовый вариант для slowrig:

```text
PostgreSQL + pgvector
```

Причина: для проекта нужна не только векторная база, но и обычные структурированные данные:

* история задач;
* решения;
* статусы агентов;
* Telegram-диалоги;
* метаданные документов;
* связи между проектами, файлами и выводами.

Qdrant можно рассмотреть как специализированный vector store, если RAG по документам станет главным приоритетом.
