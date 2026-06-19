# slowrig AI Cluster

Домашний локальный ИИ-кластер для разработки, анализа кода, работы с документацией, локальных ассистентов и будущих агентских сценариев.

## Быстрый статус

Главная команда проверки:

```bash
/opt/llama-cluster/scripts/cluster-status.sh
```

Она показывает:

* состояние Docker-контейнеров;
* распределение GPU;
* занятость VRAM;
* доступность API;
* доступность Open WebUI;
* последние подозрительные строки логов.

## Основные сервисы

| Сервис            |   Порт | Роль                            |
| ----------------- | -----: | ------------------------------- |
| `llama-architect` | `8080` | 27B architect / сложные решения |
| `llama-coder`     | `8081` | 9B coder / быстрый исполнитель  |
| `open-webui`      | `3000` | ручной WebUI                    |

## Адреса

Open WebUI:

```text
http://192.168.1.6:3000
```

27B architect API:

```text
http://192.168.1.6:8080/v1
```

9B coder API:

```text
http://192.168.1.6:8081/v1
```

## GPU mapping

| Host GPU | Подключение                      | Используется для        |
| -------- | -------------------------------- | ----------------------- |
| GPU 0    | ограниченная PCIe-линия / райзер | часть `llama-architect` |
| GPU 1    | PCIe x16                         | `llama-coder`           |
| GPU 2    | ограниченная PCIe-линия / райзер | часть `llama-architect` |

## Модели

`llama-architect`:

```text
Qwen3.6-27B-UD-Q4_K_XL.gguf
```

`llama-coder`:

```text
Qwen3.5-9B-UD-Q4_K_XL.gguf
```

Обе модели сейчас работают с:

```text
ctx-size 40000
```

## Основные файлы

```text
/opt/llama-cluster/docker-compose.yaml
/opt/llama-cluster/docker-compose.stage1-baseline.yaml
/opt/llama-cluster/docs/passport.md
/opt/llama-cluster/docs/runbook.md
/opt/llama-cluster/scripts/cluster-status.sh
```

## Директории

```text
/opt/llama-cluster/models   # GGUF-модели, не хранить в git
/opt/llama-cluster/cache    # runtime/cache, не хранить в git
/opt/llama-cluster/docs     # документация
/opt/llama-cluster/scripts  # эксплуатационные скрипты
```

## Быстрые команды

Проверить контейнеры:

```bash
sudo docker ps
```

Проверить GPU:

```bash
nvidia-smi
```

Проверить итоговый compose:

```bash
cd /opt/llama-cluster
sudo docker compose config
```

Перезапустить только WebUI:

```bash
cd /opt/llama-cluster
sudo docker compose restart open-webui
```

Перезапустить только 9B:

```bash
cd /opt/llama-cluster
sudo docker compose restart llama-coder
```

Перезапустить только 27B:

```bash
cd /opt/llama-cluster
sudo docker compose restart llama-architect
```

## Правила безопасности

Не делать без отдельного теста:

* увеличивать `parallel` у `llama-architect`;
* включать CPU offload;
* включать Unified Memory как штатный режим;
* запускать третью LLM на GPU 0/2;
* открывать порты `3000`, `8080`, `8081` наружу;
* удалять Docker volumes;
* обновлять образы без фиксации baseline;
* менять сразу несколько параметров compose.

## Git

Этот каталог является локальным git-репозиторием для конфигов, документации и скриптов.

Модели, кэш, данные, логи и секреты исключены через `.gitignore`.

Перед экспериментами:

```bash
cd /opt/llama-cluster
git status
git diff
```

После успешного изменения:

```bash
git add .
git commit -m "Describe change"
```

## Документы

Подробное описание стенда:

```text
docs/passport.md
```

Ежедневная эксплуатация и диагностика:

```text
docs/runbook.md
```
