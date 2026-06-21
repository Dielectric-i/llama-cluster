# slowrig AI Cluster — Gateway Design and Baseline v0.2

Дата актуализации: 2026-06-19
Статус: LiteLLM Gateway внедрён и проверен.

## 1. Назначение

Этот документ описывает Gateway / Router слой для `slowrig AI Cluster`.

Gateway нужен, чтобы все клиенты обращались не напрямую к моделям, а через единую контролируемую точку входа.

Текущая схема:

```text
Open WebUI
    |
    v
LiteLLM Gateway / port 4000
    |
    +--> slowrig/coder      -> llama-coder / 9B / 8081
    |
    +--> slowrig/architect  -> llama-architect / 27B / 8080
```

---

## 2. Почему gateway нужен

Раньше Open WebUI напрямую знал адреса обоих llama.cpp backend-ов.

Сейчас Open WebUI переключён на LiteLLM Gateway. Это лучше масштабируется, когда появятся:

* Telegram bot;
* CrewAI;
* OpenClaw;
* IDE-ассистенты;
* repo-auditor;
* память;
* RAG;
* внешние клиенты;
* разные политики доступа.

Gateway должен стать центральной точкой, которая отвечает за:

* единый OpenAI-compatible API;
* маршрутизацию между 9B и 27B;
* будущую авторизацию;
* API keys;
* логирование запросов;
* лимиты;
* очереди;
* fallback;
* подключение памяти;
* подключение агентских сценариев;
* сокрытие внутренних портов `8080` и `8081`.

---

## 3. Что gateway не должен делать

Gateway не должен становиться “всем сразу”.

Он не должен:

* хранить всю долгосрочную память;
* быть полноценным агентским фреймворком;
* выполнять shell-команды;
* редактировать файлы;
* заменять Open WebUI;
* заменять llama.cpp;
* напрямую управлять GPU;
* решать все задачи RAG на первом этапе.

Правильная роль gateway:

```text
принять запрос -> выбрать backend -> отправить запрос -> вернуть ответ -> записать минимальные логи
```

---

## 4. Текущие backend-и

### 4.1 `llama-coder`

Адрес внутри Docker-сети:

```text
http://llama-coder:8080/v1
```

Адрес с хоста:

```text
http://127.0.0.1:8081/v1
```

Роль:

* быстрые ответы;
* Telegram;
* summaries;
* подготовка контекста;
* простые coding-задачи;
* первичный анализ файлов и логов.

Рабочее имя в gateway:

```text
slowrig/coder
```

---

### 4.2 `llama-architect`

Адрес внутри Docker-сети:

```text
http://llama-architect:8080/v1
```

Адрес с хоста:

```text
http://127.0.0.1:8080/v1
```

Роль:

* сложное reasoning;
* архитектура;
* DevOps/ML-решения;
* ревью сложного кода;
* финальные выводы после подготовки контекста;
* планирование задач для агентов.

Рабочее имя в gateway:

```text
slowrig/architect
```

---

## 5. Кандидаты gateway

### 5.1 LiteLLM Proxy

Плюсы:

* уже является готовым LLM gateway;
* даёт OpenAI-compatible API;
* умеет работать с несколькими backend-ами;
* может подключаться к OpenAI-compatible endpoints;
* может стать единой точкой для Open WebUI, Telegram, IDE и агентов;
* проще, чем писать свой router сразу;
* можно потом добавить API keys, логи, fallback и внешние модели.

Минусы:

* ещё один контейнер;
* ещё один конфиг;
* нужно внимательно проверить совместимость с llama.cpp server;
* добавляет слой диагностики;
* не решает сам по себе долгосрочную память и RAG.

Статус:

```text
выбран, внедрён и проверен как первый gateway
```

---

### 5.2 Собственный FastAPI router

Плюсы:

* полный контроль;
* можно сразу встроить свои правила;
* можно сделать очень простой и понятный код;
* удобно для Telegram и будущей памяти.

Минусы:

* нужно писать и поддерживать самому;
* нужно реализовывать OpenAI-compatible API или адаптер;
* больше риска ошибок;
* появится собственный код до того, как стабилизирована общая архитектура.

Предварительный статус:

```text
хороший вариант позже, если LiteLLM окажется слишком тяжёлым или неудобным
```

---

### 5.3 Gateway внутри OpenClaw / CrewAI

Плюсы:

* ближе к агентской логике;
* может хорошо лечь на agent workflow;
* можно сразу учитывать роли агентов.

Минусы:

* слишком рано привязывает инфраструктуру к конкретному agent framework;
* сложнее использовать из Open WebUI, Telegram и IDE;
* harder to debug;
* может смешать слои: gateway, agents, memory и tools.

Предварительный статус:

```text
не использовать как первый gateway
```

---

### 5.4 Оставить ручную маршрутизацию через Open WebUI

Плюсы:

* ничего не добавлять;
* всё уже работает;
* минимальная сложность.

Минусы:

* не подходит для Telegram;
* не подходит для агентов;
* нет единой точки логирования;
* нет нормального routing policy;
* клиенты должны знать внутренние backend-и.

Предварительный статус:

```text
допустимо только временно
```

---

## 6. Предварительное решение

Для Stage 3 выбрать:

```text
LiteLLM Proxy как первый gateway
```

Причина:

* он подходит под роль центральной OpenAI-compatible точки входа;
* позволяет подключить обе локальные llama.cpp модели;
* Open WebUI можно будет подключить уже к LiteLLM, а не напрямую к моделям;
* позже к нему можно подключить Telegram, IDE и агентские системы;
* при неудаче его можно убрать, не ломая Stage 1 baseline.

Это решение реализовано как Stage 3 baseline.

---

## 7. Целевая Stage 3 схема

```text
Open WebUI
    |
    v
LiteLLM Gateway / port 4000
    |
    +--> slowrig/coder      -> llama-coder      -> 9B  -> GPU 1
    |
    +--> slowrig/architect  -> llama-architect  -> 27B -> GPU 0 + GPU 2
```

Порты после добавления gateway:

| Компонент         |   Порт | Назначение                           |
| ----------------- | -----: | ------------------------------------ |
| `open-webui`      | `3000` | ручной интерфейс                     |
| `litellm`         | `4000` | единая OpenAI-compatible точка входа |
| `llama-architect` | `8080` | внутренний 27B backend               |
| `llama-coder`     | `8081` | внутренний 9B backend                |

На первом этапе порты `8080` и `8081` можно оставить открытыми для диагностики.

Позже, когда gateway стабилизируется, можно будет закрыть прямой доступ к backend-ам и оставить внешним клиентам только gateway.

---

## 8. Модельные имена

В gateway использовать понятные имена:

```text
slowrig/coder
slowrig/architect
```

Не использовать имена GGUF-файлов как публичные имена моделей.

Причина:

* GGUF-файл может измениться;
* роль модели важнее имени файла;
* клиентам удобнее использовать стабильные имена;
* позже можно заменить backend без изменения клиентов.

---

## 9. Предварительные routing policy

### 9.1 Ручной выбор модели

На первом этапе gateway не обязан сам угадывать сложность задачи.

Клиент явно выбирает:

```text
model: slowrig/coder
```

или:

```text
model: slowrig/architect
```

Это проще и безопаснее.

### 9.2 Будущий автоматический routing

Позже можно добавить alias:

```text
slowrig/fast
slowrig/deep
slowrig/default
```

Предварительная логика:

| Alias                 | Backend            |
| --------------------- | ------------------ |
| `slowrig/fast`        | 9B                 |
| `slowrig/deep`        | 27B                |
| `slowrig/default`     | 9B                 |
| `slowrig/code-review` | 27B                |
| `slowrig/telegram`    | 9B                 |
| `slowrig/repo-audit`  | 9B -> 27B pipeline |

На первом этапе pipeline `9B -> 27B` не реализовывать внутри gateway. Это задача будущего agent layer.

---

## 10. Безопасность Stage 3

На первом этапе gateway будет доступен только в LAN.

Не открывать наружу:

* `3000`;
* `4000`;
* `8080`;
* `8081`.

Для gateway нужен master key или API key.

Секреты не хранить прямо в `docker-compose.yaml`.

Использовать:

```text
.env
```

Но `.env` должен быть исключён из git.

---

## 11. Что должно быть в git

Можно хранить в git:

* пример конфига gateway без секретов;
* `litellm.config.yaml`, если в нём нет реальных ключей;
* compose-сервис без секретов;
* документацию;
* инструкции.

Нельзя хранить в git:

* реальные API keys;
* Telegram bot token;
* внешние provider keys;
* пользовательские токены;
* приватные данные.

---

## 12. Минимальная проверка после установки gateway

После добавления gateway нужно проверить:

1. `docker ps` показывает `litellm`.
2. `litellm` healthy или стабильно `Up`.
3. `curl http://127.0.0.1:4000/v1/models` отвечает.
4. Запрос к `slowrig/coder` идёт на 9B.
5. Запрос к `slowrig/architect` идёт на 27B.
6. Open WebUI может работать через gateway.
7. Прямые backend-и `8080` и `8081` по-прежнему работают для диагностики.
8. `cluster-status.sh` обновлён и проверяет порт `4000`.

---

## 13. Риски

### 13.1 Дополнительный слой диагностики

Если ответ не пришёл, нужно будет понимать, где проблема:

```text
Open WebUI -> LiteLLM -> llama.cpp -> GPU
```

Поэтому нельзя сразу менять всё.

Правильный порядок:

1. Добавить LiteLLM.
2. Проверить LiteLLM напрямую через curl.
3. Только потом переключить Open WebUI на LiteLLM.
4. Сохранить возможность прямой проверки 8080/8081.

---

### 13.2 Несовместимость endpoint-ов

llama.cpp server даёт OpenAI-compatible API, но не обязательно поддерживает все новые OpenAI endpoint-ы.

На первом этапе использовать только базовые chat completions.

Не строить Stage 3 вокруг advanced endpoint-ов, пока они не проверены.

---

### 13.3 Ошибки авторизации

Gateway будет использовать API key.

Нужно отдельно проверить:

* что ключ работает;
* что Open WebUI передаёт ключ;
* что curl-запросы с ключом проходят;
* что без ключа доступ запрещён, если так задумано.

---

### 13.4 Перегрузка 27B

Gateway не должен отправлять частые мелкие запросы на 27B.

27B — дорогой ресурс.

Правило:

```text
default -> 9B
deep/manual -> 27B
```

---

## 14. Stage 3 план

* Stage 3.1 `docs/gateway.md` — done;
* Stage 3.2 `.env` and config — done;
* Stage 3.3 LiteLLM service — done;
* Stage 3.4 `cluster-status.sh` update — done;
* Stage 3.5 documentation update — done;
* Stage 3.6 Open WebUI routed through LiteLLM — done.

---

## 15. Текущее решение

На момент актуализации документа:

```text
LiteLLM Gateway внедрён
Open WebUI подключён к http://litellm:4000/v1
Прямые backend-порты 8080/8081 сохранены для диагностики
```

---

## 16. Фактически внедрённая конфигурация

Сервис:

```text
litellm
```

Порт:

```text
4000
```

Конфиг:

```text
config/litellm.config.yaml
```

Секреты:

```text
.env
```

Шаблон секретов:

```text
.env.example
```

Model names:

```text
slowrig/coder
slowrig/architect
```

Open WebUI использует:

```text
OPENAI_API_BASE_URLS=http://litellm:4000/v1
OPENAI_API_KEYS=${LITELLM_MASTER_KEY}
```

---

## 17. Rollback: Open WebUI direct backend mode

Если LiteLLM нужно временно обойти, Open WebUI можно вернуть на прямые backend-и.

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

---

## 18. Known limitations

* прямые порты `8080/8081` пока открыты для диагностики;
* автоматический routing по сложности задачи пока не реализован;
* LiteLLM не является memory/RAG-слоем;
* Telegram ещё не подключён;
* CrewAI/OpenClaw ещё не подключены;
* `cluster-status.sh` делает реальные короткие LLM-запросы, поэтому не использовать его как частый автоматический healthcheck.
