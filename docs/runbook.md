# slowrig AI Cluster — Runbook v0.1

## 1. Назначение

Этот документ описывает ежедневные эксплуатационные действия для `slowrig AI Cluster`.

Runbook отвечает на вопросы:

* как проверить состояние кластера;
* как понять, что всё работает нормально;
* как посмотреть логи;
* как безопасно перезапустить сервис;
* как понять, какая модель использует какую GPU;
* что делать при падении контейнера;
* какие действия опасны.

---

## 2. Нормальное состояние

Нормальное состояние Stage 1:

| Сервис            |   Порт | Роль                           |
| ----------------- | -----: | ------------------------------ |
| `llama-architect` | `8080` | 27B architect / deep reasoning |
| `llama-coder`     | `8081` | 9B coder / fast worker         |
| `open-webui`      | `3000` | ручной WebUI                   |

Нормальное распределение GPU:

| GPU   | Роль                    |
| ----- | ----------------------- |
| GPU 0 | часть `llama-architect` |
| GPU 1 | `llama-coder`, PCIe x16 |
| GPU 2 | часть `llama-architect` |

Ожидаемое потребление VRAM:

| GPU   |           Примерная VRAM |
| ----- | -----------------------: |
| GPU 0 | около `9121 / 10240 MiB` |
| GPU 1 | около `6269 / 10240 MiB` |
| GPU 2 | около `9701 / 10240 MiB` |

Небольшие отличия допустимы. Сильный рост VRAM без нагрузки — повод смотреть логи.

---

## 3. Быстрая проверка состояния

### 3.1 Проверить контейнеры

```bash
sudo docker ps
```

Ожидаемо должны быть контейнеры:

* `llama-architect`;
* `llama-coder`;
* `open-webui`.

Нормально:

* статус `Up`;
* желательно `healthy`;
* порты `8080`, `8081`, `3000` опубликованы.

Проблема:

* контейнера нет;
* статус `Restarting`;
* статус `Exited`;
* статус `unhealthy`.

---

### 3.2 Проверить GPU

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

### 3.3 Проверить доступность WebUI

Открыть в браузере:

```text
http://192.168.1.6:3000
```

Ожидаемо:

* WebUI открывается;
* модели доступны;
* можно отправить короткий запрос.

---

## 4. Проверка API моделей

### 4.1 Проверить 27B architect

```bash
curl http://127.0.0.1:8080/v1/models
```

Ожидаемо: API отвечает списком моделей или JSON-ответом от llama.cpp server.

### 4.2 Проверить 9B coder

```bash
curl http://127.0.0.1:8081/v1/models
```

Ожидаемо: API отвечает списком моделей или JSON-ответом от llama.cpp server.

Если `curl` зависает или получает connection refused — контейнер или порт не работает.

---

## 5. Логи

### 5.1 Логи 27B

```bash
sudo docker logs --tail=120 llama-architect
```

Смотреть на:

* загрузку модели;
* CUDA errors;
* OOM;
* ошибки split;
* сообщения о невозможности выделить память;
* падения после запроса.

---

### 5.2 Логи 9B

```bash
sudo docker logs --tail=120 llama-coder
```

Смотреть на:

* успешную загрузку модели;
* ошибки CUDA;
* проблемы с KV cache;
* падения при длинном контексте;
* необычно долгий prompt processing.

---

### 5.3 Логи Open WebUI

```bash
sudo docker logs --tail=120 open-webui
```

Смотреть на:

* ошибки подключения к backend-ам;
* ошибки API;
* проблемы с переменными окружения;
* ошибки базы WebUI.

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
```

---

### 6.3 Перезапустить только WebUI

Использовать, если модели работают, но WebUI не открывается или не видит backend.

```bash
cd /opt/llama-cluster
sudo docker compose restart open-webui
```

После этого проверить:

```bash
sudo docker ps
sudo docker logs --tail=80 open-webui
```

---

### 6.4 Перезапустить весь кластер

Использовать только если проблема общая.

```bash
cd /opt/llama-cluster
sudo docker compose up -d
```

Эта команда приводит контейнеры к состоянию, описанному в `docker-compose.yaml`.

Она не должна удалять данные Open WebUI, потому что используется volume `open-webui`.

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

Контейнеры будут удалены, но named volume `open-webui` останется.

Не использовать `-v`, если не нужно удалить данные WebUI.

Опасная команда:

```bash
sudo docker compose down -v
```

Она удалит volume и может стереть данные Open WebUI.

---

## 8. Проверка итоговой compose-конфигурации

```bash
cd /opt/llama-cluster
sudo docker compose config
```

Команда ничего не запускает и не меняет.

Она показывает, как Docker Compose видит итоговую конфигурацию.

Использовать после правки `docker-compose.yaml`.

---

## 9. Правила изменений

### 9.1 Менять только один параметр за раз

Правильно:

```text
изменить ctx-size только у llama-coder
перезапустить только llama-coder
проверить логи
проверить VRAM
```

Неправильно:

```text
одновременно менять ctx-size, model, image, split-mode, parallel и WebUI
```

---

### 9.2 Не повышать parallel у 27B

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

### 9.3 Не включать CPU-offload как штатный режим

Модели должны работать на GPU.

CPU-offload может позволить запустить более тяжёлую конфигурацию, но резко ухудшит скорость и стабильность.

---

### 9.4 Не использовать Unified Memory как решение нехватки VRAM

Unified Memory может скрыть проблему нехватки VRAM, но при этом данные начнут уходить в RAM.

Для slowrig с PCIe x1 это приведёт к очень плохой скорости.

---

### 9.5 Не открывать порты наружу

Не открывать наружу без защиты:

* `3000`;
* `8080`;
* `8081`.

Текущий WebUI без авторизации допустим только в домашней сети.

Для удалённого доступа позже нужен VPN, reverse proxy с авторизацией или gateway.

---

## 10. Типовые проблемы

### 10.1 Контейнер не стартует

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
* ошибка YAML.

---

### 10.2 WebUI не видит модели

Проверить:

```bash
sudo docker logs --tail=120 open-webui
curl http://127.0.0.1:8080/v1/models
curl http://127.0.0.1:8081/v1/models
```

Если API моделей отвечает, а WebUI не видит их — проблема в настройках Open WebUI.

Если API не отвечает — проблема в llama.cpp контейнере.

---

### 10.3 Модель отвечает очень медленно

Проверить:

```bash
nvidia-smi
sudo docker logs --tail=120 llama-architect
sudo docker logs --tail=120 llama-coder
```

Возможные причины:

* слишком большой входной prompt;
* модель ушла в CPU/RAM;
* упор в PCIe x1;
* перегрев;
* нехватка VRAM;
* параллельные запросы;
* WebUI отправляет слишком большой системный prompt.

---

### 10.4 После изменения compose всё сломалось

Сравнить с baseline:

```bash
cd /opt/llama-cluster
diff -u docker-compose.stage1-baseline.yaml docker-compose.yaml
```

Если нужно быстро вернуться к baseline:

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

---

## 11. Запрещённые действия без отдельного плана

Не делать без отдельной проверки:

* увеличивать `parallel` у 27B;
* включать Unified Memory;
* включать CPU layers;
* запускать третью LLM на GPU 0/2;
* открывать WebUI наружу;
* удалять Docker volumes;
* обновлять все образы без backup baseline;
* менять порядок GPU без проверки `nvidia-smi`;
* менять несколько параметров одновременно.

---

## 12. Следующий этап после runbook

После фиксации runbook можно переходить к:

1. локальному git-репозиторию для конфигов и документации;
2. базовому мониторингу;
3. архитектуре gateway;
4. архитектуре памяти;
5. Telegram-интерфейсу;
6. CrewAI / OpenClaw.
