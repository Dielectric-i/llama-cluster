# slowrig AI Cluster — Runbook v0.2

## 1. Назначение

Этот документ описывает ежедневные эксплуатационные действия для `slowrig AI Cluster`.

Runbook отвечает на вопросы:

* как быстро проверить состояние кластера;
* как понять, что всё работает нормально;
* как посмотреть логи;
* как проверить LiteLLM Gateway;
* как безопасно перезапустить отдельный сервис;
* как отличить проблему gateway от проблемы backend-модели;
* как откатиться после неудачного изменения;
* какие действия опасны и требуют отдельного плана.

Runbook не заменяет:

* `README.md` — быстрый вход и индекс документации;
* `docs/passport.md` — паспорт текущего стенда;
* `docs/architecture.md` — архитектуру;
* `docs/gateway.md` — подробности LiteLLM Gateway;
* `docs/decisions.md` — причины архитектурных решений;
* `docs/changelog.md` — историю фактических изменений.

---

## 2. Нормальное состояние

Нормальное состояние сервисов:

| Сервис | Порт | Роль |
| --- | ---: | --- |
| `llama-architect` | `8080` | 27B architect / deep reasoning |
| `llama-coder` | `8081` | 9B coder / fast worker |
| `litellm` | `4000` | LLM Gateway / Router |
| `open-webui` | `3000` | ручной WebUI через gateway |

Нормальная цепочка запросов:

```text
Open WebUI -> LiteLLM Gateway -> llama-coder / llama-architect
```

Gateway model names:

```text
slowrig/coder
slowrig/architect
```

Нормальное распределение GPU:

| GPU | Роль |
| --- | --- |
| GPU 0 | часть `llama-architect` |
| GPU 1 | `llama-coder`, PCIe x16 |
| GPU 2 | часть `llama-architect` |

Ожидаемое потребление VRAM после запуска:

| GPU | Примерная VRAM |
| --- | ---: |
| GPU 0 | около `9121 / 10240 MiB` |
| GPU 1 | около `6269 / 10240 MiB` |
| GPU 2 | около `9701 / 10240 MiB` |

Небольшие отличия допустимы. Сильный рост VRAM без нагрузки — повод смотреть логи.

Прямые backend-порты `8080` и `8081` оставлены для диагностики. Обычные клиенты должны использовать LiteLLM Gateway на `4000`.

---

## 3. Быстрая проверка состояния

### 3.1 Полная ручная проверка

Главная команда:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

Ожидаемо:

* контейнеры запущены;
* GPU видны;
* VRAM распределена ожидаемо;
* direct backend `8080` отвечает;
* direct backend `8081` отвечает;
* Open WebUI `3000` доступен;
* LiteLLM `/v1/models` на `4000` отвечает;
* `slowrig/coder` отвечает через LiteLLM;
* `slowrig/architect` отвечает через LiteLLM.

Важно:

```text
cluster-status.sh делает реальные короткие LLM-запросы.
```

Этот скрипт подходит для ручной диагностики, но не должен использоваться как частый автоматический healthcheck.

---

### 3.2 Проверить контейнеры

```bash
sudo docker ps
```

Ожидаемо должны быть контейнеры:

* `llama-architect`;
* `llama-coder`;
* `litellm`;
* `open-webui`.

Нормально:

* статус `Up`;
* желательно `healthy`;
* порты `8080`, `8081`, `4000`, `3000` опубликованы.

Проблема:

* контейнера нет;
* статус `Restarting`;
* статус `Exited`;
* статус `unhealthy`.

---

### 3.3 Проверить Compose-состояние

```bash
cd /opt/llama-cluster
sudo docker compose ps
```

Использовать, чтобы увидеть сервисы с точки зрения `docker-compose.yaml`.

---

### 3.4 Проверить GPU

```bash
nvidia-smi
```

Ожидаемо:

* GPU 0 и GPU 2 заняты одним процессом `/app/llama-server`;
* GPU 1 занята другим процессом `/app/llama-server`.

Нормальная логика:

```text
GPU 0 + GPU 2 -> llama-architect
GPU 1         -> llama-coder
```

Проблема:

* 27B оказалась на GPU 1;
* 9B оказалась на GPU 0 или GPU 2;
* на GPU появились лишние процессы;
* VRAM растёт и не освобождается;
* температура резко выросла;
* GPU не видна.

---

### 3.5 Проверить доступность WebUI

Открыть в браузере:

```text
http://192.168.1.6:3000
```

Ожидаемо:

* WebUI открывается;
* модели доступны через LiteLLM;
* можно отправить короткий запрос.

---

## 4. Проверка API

## 4.1 Проверить direct backend 27B

```bash
curl http://127.0.0.1:8080/v1/models
```

Ожидаемо: API отвечает списком моделей или JSON-ответом от llama.cpp server.

Если `curl` зависает или получает `connection refused`, проблема ниже gateway: контейнер, порт, модель или llama.cpp backend.

---

## 4.2 Проверить direct backend 9B

```bash
curl http://127.0.0.1:8081/v1/models
```

Ожидаемо: API отвечает списком моделей или JSON-ответом от llama.cpp server.

Если `curl` зависает или получает `connection refused`, проблема ниже gateway: контейнер, порт, модель или llama.cpp backend.

---

## 4.3 Проверить LiteLLM Gateway

Перед проверкой загрузить ключ из `.env`, не печатая его:

```bash
cd /opt/llama-cluster
set -a
source .env
set +a
```

Проверить `/v1/models`:

```bash
curl -sS \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  http://127.0.0.1:4000/v1/models
```

Ожидаемо должны быть доступны:

```text
slowrig/coder
slowrig/architect
```

---

## 4.4 Проверить 9B через gateway

```bash
curl -sS http://127.0.0.1:4000/v1/chat/completions \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "slowrig/coder",
    "messages": [
      {
        "role": "user",
        "content": "Ответь одним словом: OK"
      }
    ],
    "temperature": 0,
    "max_tokens": 8
  }'
```

Ожидаемо: короткий ответ от `slowrig/coder`.

---

## 4.5 Проверить 27B через gateway

```bash
curl -sS http://127.0.0.1:4000/v1/chat/completions \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "slowrig/architect",
    "messages": [
      {
        "role": "user",
        "content": "Ответь одним словом: OK"
      }
    ],
    "temperature": 0,
    "max_tokens": 8
  }'
```

Ожидаемо: короткий ответ от `slowrig/architect`.

---

## 5. Логи

### 5.1 Логи 27B

```bash
sudo docker logs --tail=160 llama-architect
```

Смотреть на:

* загрузку модели;
* CUDA errors;
* OOM;
* ошибки split;
* сообщения о невозможности выделить память;
* падения после запроса;
* признаки CPU offload.

---

### 5.2 Логи 9B

```bash
sudo docker logs --tail=160 llama-coder
```

Смотреть на:

* успешную загрузку модели;
* CUDA errors;
* проблемы с KV cache;
* падения при длинном контексте;
* необычно долгий prompt processing;
* признаки CPU offload.

---

### 5.3 Логи LiteLLM

```bash
sudo docker logs --tail=160 litellm
```

Смотреть на:

* ошибки загрузки конфига;
* ошибки авторизации;
* ошибки подключения к backend-ам;
* проблемы model names;
* ошибки upstream API;
* проблемы с `LITELLM_MASTER_KEY`.

---

### 5.4 Логи Open WebUI

```bash
sudo docker logs --tail=160 open-webui
```

Смотреть на:

* ошибки подключения к LiteLLM;
* ошибки API;
* проблемы с переменными окружения;
* ошибки базы WebUI;
* попытки подключения к неиспользуемым backend-ам.

---

## 6. Безопасный перезапуск

### 6.1 Перезапустить только 9B

Использовать, если проблема только с `llama-coder`.

```bash
cd /opt/llama-cluster
sudo docker compose restart llama-coder
```

После этого проверить:

```bash
sudo docker ps
nvidia-smi
sudo docker logs --tail=80 llama-coder
curl http://127.0.0.1:8081/v1/models
```

---

### 6.2 Перезапустить только 27B

Использовать, если проблема только с `llama-architect`.

```bash
cd /opt/llama-cluster
sudo docker compose restart llama-architect
```

После этого проверить:

```bash
sudo docker ps
nvidia-smi
sudo docker logs --tail=80 llama-architect
curl http://127.0.0.1:8080/v1/models
```

---

### 6.3 Перезапустить только LiteLLM

Использовать, если direct backend-и `8080` и `8081` работают, но gateway `4000` не отвечает.

```bash
cd /opt/llama-cluster
sudo docker compose restart litellm
```

После этого проверить:

```bash
sudo docker logs --tail=160 litellm
```

Затем проверить gateway:

```bash
cd /opt/llama-cluster
set -a
source .env
set +a

curl -sS \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  http://127.0.0.1:4000/v1/models
```

---

### 6.4 Перезапустить только Open WebUI

Использовать, если backend-и и gateway работают, но WebUI не открывается или не видит модели.

```bash
cd /opt/llama-cluster
sudo docker compose restart open-webui
```

После этого проверить:

```bash
sudo docker ps
sudo docker logs --tail=160 open-webui
```

---

### 6.5 Применить изменения compose для одного сервиса

Если был изменён только один сервис в `docker-compose.yaml`, лучше поднимать только его:

```bash
cd /opt/llama-cluster
sudo docker compose up -d <service-name>
```

Примеры:

```bash
sudo docker compose up -d litellm
sudo docker compose up -d open-webui
sudo docker compose up -d llama-coder
sudo docker compose up -d llama-architect
```

---

### 6.6 Привести весь кластер к compose-состоянию

Использовать, если изменение затрагивает несколько сервисов или нужно привести состояние к `docker-compose.yaml`.

```bash
cd /opt/llama-cluster
sudo docker compose up -d
```

Эта команда не должна удалять данные Open WebUI, потому что используется named volume.

Перед использованием желательно выполнить:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
```

---

## 7. Остановка

### 7.1 Остановить весь кластер

```bash
cd /opt/llama-cluster
sudo docker compose stop
```

Контейнеры остановятся, но не будут удалены.

---

### 7.2 Полностью убрать контейнеры

```bash
cd /opt/llama-cluster
sudo docker compose down
```

Контейнеры будут удалены, но named volumes останутся.

Не использовать `-v`, если не нужно удалить данные volume.

Опасная команда:

```bash
sudo docker compose down -v
```

Она удалит volumes и может стереть данные Open WebUI или будущих сервисов.

---

## 8. Проверка compose-конфигурации

Проверить итоговую compose-конфигурацию:

```bash
cd /opt/llama-cluster
sudo docker compose config
```

Проверить только валидность:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
```

Эти команды ничего не запускают и не меняют.

Использовать после правки:

* `docker-compose.yaml`;
* `.env.example`;
* service environment;
* ports;
* volumes;
* healthchecks;
* GPU assignments.

---

## 9. Типовые проблемы

### 9.1 Контейнер не стартует

Проверить:

```bash
sudo docker ps -a
sudo docker logs --tail=160 <container_name>
```

Искать:

* OOM;
* CUDA error;
* model not found;
* permission denied;
* invalid argument;
* ошибка YAML;
* ошибка переменных окружения;
* проблема volume mount;
* проблема доступа к GPU.

---

### 9.2 LiteLLM не отвечает

Проверить контейнер:

```bash
sudo docker ps --filter name=litellm
```

Проверить логи:

```bash
sudo docker logs --tail=160 litellm
```

Проверить direct backend-и:

```bash
curl http://127.0.0.1:8080/v1/models
curl http://127.0.0.1:8081/v1/models
```

Интерпретация:

```text
8080/8081 работают, 4000 не работает -> проблема в LiteLLM или его конфиге.
8080/8081 не работают -> проблема ниже gateway, в llama.cpp backend-е.
4000 работает, WebUI не видит модели -> проблема в Open WebUI config.
```

---

### 9.3 Open WebUI не видит модели

Проверить gateway:

```bash
cd /opt/llama-cluster
set -a
source .env
set +a

curl -sS \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  http://127.0.0.1:4000/v1/models
```

Проверить логи:

```bash
sudo docker logs --tail=160 open-webui
sudo docker logs --tail=160 litellm
```

Если gateway отвечает, но WebUI не видит модели — смотреть переменные Open WebUI:

```text
OPENAI_API_BASE_URLS
OPENAI_API_KEYS
ENABLE_OLLAMA_API
```

Не печатать реальные значения секретов.

---

### 9.4 Модель отвечает очень медленно

Проверить:

```bash
nvidia-smi
sudo docker logs --tail=160 llama-architect
sudo docker logs --tail=160 llama-coder
```

Возможные причины:

* слишком большой входной prompt;
* модель ушла в CPU/RAM;
* упор в PCIe x1;
* перегрев;
* нехватка VRAM;
* параллельные запросы;
* WebUI отправляет слишком большой системный prompt;
* 27B получила задачу, которую лучше было отправить в 9B;
* gateway или клиент отправляет больше контекста, чем ожидалось.

---

### 9.5 CUDA OOM или allocation error

Сначала не менять сразу много параметров.

Проверить:

```bash
nvidia-smi
sudo docker logs --tail=160 llama-architect
sudo docker logs --tail=160 llama-coder
```

Возможные причины:

* слишком большой контекст;
* слишком большой prompt;
* вырос `parallel`;
* изменился split;
* добавился лишний процесс на GPU;
* модель запущена не на той GPU;
* WebUI или gateway отправляет параллельные запросы.

Без отдельного плана не включать CPU offload или Unified Memory как “быстрое решение”.

---

### 9.6 После изменения compose всё сломалось

Проверить diff:

```bash
cd /opt/llama-cluster
git diff -- docker-compose.yaml
```

Сравнить с baseline:

```bash
cd /opt/llama-cluster
diff -u docker-compose.stage1-baseline.yaml docker-compose.yaml
```

Если нужно быстро вернуться к Stage 1 compose baseline:

```bash
cd /opt/llama-cluster
cp docker-compose.stage1-baseline.yaml docker-compose.yaml
sudo docker compose up -d
```

После отката проверить:

```bash
sudo docker ps
nvidia-smi
```

Важно:

```text
docker-compose.stage1-baseline.yaml является историческим Stage 1 baseline.
Он может не содержать более поздние Stage 2/3 изменения и не должен считаться текущим stable compose.
```

Если нужно откатить только gateway/Open WebUI routing, использовать rollback из раздела 10.

---

## 10. Rollback

### 10.1 Откат Open WebUI на прямые backend-и

Если LiteLLM работает нестабильно, можно временно вернуть Open WebUI на direct backend mode.

В `docker-compose.yaml` для `open-webui` заменить gateway-mode:

```yaml
- OPENAI_API_BASE_URLS=http://litellm:4000/v1
- OPENAI_API_KEYS=${LITELLM_MASTER_KEY}
```

на direct-mode:

```yaml
- OPENAI_API_BASE_URLS=http://llama-coder:8080/v1;http://llama-architect:8080/v1
- OPENAI_API_KEYS=dummy;dummy
```

Важно: это Docker network endpoints для контейнера `open-webui`. Host diagnostic endpoint для `llama-coder` остаётся `http://127.0.0.1:8081/v1`.

Применить только WebUI:

```bash
cd /opt/llama-cluster
sudo docker compose up -d open-webui
```

Проверить:

```bash
sudo docker logs --tail=160 open-webui
```

Этот режим является rollback path, а не предпочтительной архитектурой.

---

### 10.2 Откат конкретного файла через git

Посмотреть изменения:

```bash
cd /opt/llama-cluster
git diff --stat
git diff -- <file>
```

Откатывать файл только если понятно, что изменения не нужны:

```bash
git checkout -- <file>
```

Не использовать без отдельного решения:

```bash
git reset --hard
git clean -fd
```

---

## 11. Правила изменений

### 11.1 Менять один параметр за раз

Правильно:

```text
изменить один параметр
перезапустить только затронутый сервис
проверить логи
проверить VRAM
проверить короткий запрос
проверить длинный сценарий, если изменение касается контекста
```

Неправильно:

```text
одновременно менять ctx-size, model, image, split-mode, parallel, gateway config и WebUI config
```

---

### 11.2 Не повышать `parallel` у 27B без отдельного теста

Для `llama-architect` текущее правило:

```text
--parallel 1
```

Причина: 27B работает на двух GPU через ограниченные PCIe-линии.

Увеличение `parallel` может вызвать:

* рост VRAM;
* задержки;
* падение скорости;
* CUDA allocation errors;
* нестабильность.

---

### 11.3 Не включать CPU offload как штатный режим

Модели должны работать на GPU.

CPU offload может позволить запустить более тяжёлую конфигурацию, но резко ухудшит скорость и стабильность.

---

### 11.4 Не использовать Unified Memory как решение нехватки VRAM

Unified Memory может скрыть проблему нехватки VRAM, но при этом данные начнут уходить в RAM.

Для slowrig с PCIe x1 это приведёт к очень плохой скорости.

Unified Memory допустима только как отдельный диагностический эксперимент, а не как production baseline.

---

### 11.5 Не открывать порты наружу без отдельного security stage

Не открывать наружу без VPN/auth/reverse proxy/firewall-плана:

* `3000`;
* `4000`;
* `8080`;
* `8081`.

Текущий режим рассчитан на домашнюю LAN.

---

## 12. Запрещённые действия без отдельного плана

Не делать без отдельной проверки и rollback-плана:

* увеличивать `parallel` у 27B;
* увеличивать `ctx-size`;
* менять GPU mapping;
* менять model files;
* включать Unified Memory;
* включать CPU layers/offload;
* запускать третью LLM на GPU 0/2;
* открывать WebUI наружу;
* открывать LiteLLM наружу;
* открывать direct backend ports наружу;
* публиковать `LITELLM_MASTER_KEY`;
* коммитить `.env`;
* удалять диагностические порты `8080/8081`, пока gateway не проверен длительно;
* удалять Docker volumes;
* обновлять все образы без backup baseline;
* менять порядок GPU без проверки `nvidia-smi`;
* менять несколько параметров одновременно;
* делать `docker compose down -v`;
* делать `git reset --hard`;
* делать `git clean -fd`.

---

## 13. Проверка после любого изменения

После изменения документации:

```bash
cd /opt/llama-cluster
git diff --stat
git diff -- README.md docs/
```

После изменения bash-скрипта:

```bash
cd /opt/llama-cluster
bash -n scripts/<script-name>.sh
```

После изменения compose:

```bash
cd /opt/llama-cluster
sudo docker compose config --quiet
```

После изменения работающего сервиса:

```bash
cd /opt/llama-cluster
sudo docker compose ps
sudo docker ps
nvidia-smi
```

После изменения gateway, routing или Open WebUI:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

Если изменение успешно проверено, обновить при необходимости:

* `docs/changelog.md`;
* `docs/decisions.md`;
* соответствующий subsystem doc;
* `README.md`, если появился новый документ или изменился быстрый вход.

---

## 14. Что делать при расхождении документации и реального состояния

Если документация, config и фактический вывод команд расходятся, не угадывать.

Зафиксировать расхождение:

```text
Mismatch found:
Documentation says:
Config says:
Observed output says:
Recommended source of truth:
Question for user:
```

После подтверждения пользователем обновить affected docs в том же stage.

---
