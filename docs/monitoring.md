# slowrig AI Cluster — Monitoring Design v0.1

Дата: 2026-06-23
Статус: Stage 7 design; Stage 7.1 `cluster-health-lite.sh` implemented

## 1. Назначение

Этот документ описывает первый monitoring design для `slowrig AI Cluster`.

Цель:

```text
дать дешёвую регулярную проверку состояния без превращения LLM-generating diagnostics в постоянный healthcheck
```

---

## 2. Non-goals

Stage 7 monitoring design не добавляет:

* Prometheus;
* Grafana;
* Loki;
* node_exporter;
* DCGM exporter;
* alerting stack;
* cron/systemd timers;
* новые Docker services;
* частые LLM-generating checks.

---

## 3. Принятый первый вариант

Разделить проверки на два уровня:

```text
cluster-health-lite.sh -> дешёвая частая проверка
cluster-status.sh      -> глубокая ручная диагностика
```

`cluster-status.sh` остаётся ручной диагностикой, потому что он может выполнять реальные LLM-запросы и читать больше состояния.

`cluster-health-lite.sh` проверяет только дешёвые признаки:

* Docker container status;
* HTTP readiness endpoints без генерации;
* disk usage;
* memory pressure;
* GPU visibility / basic `nvidia-smi` availability;
* recent restart count if cheap to inspect;
* optional LiteLLM `/v1/models` check with secret loaded without printing.

---

## 4. Safety rules

Monitoring script не должен:

* печатать secrets;
* делать долгие LLM generations;
* рестартовать services;
* менять files/config;
* делать `docker compose down`;
* очищать cache/logs;
* писать в Memory/RAG;
* требовать public inbound ports.

---

## 5. Stage 7.1 — `cluster-health-lite.sh` implementation plan

Статус:

```text
implemented
```

### Цель

`scripts/cluster-health-lite.sh` быстро отвечает на вопрос:

```text
можно ли считать кластер живым без запуска дорогих LLM generation checks?
```

### Checks v1

Дешёвые checks v1:

| Check | Что проверяет | Failure | Warning |
| --- | --- | --- | --- |
| repo path | `/opt/llama-cluster` существует | нет каталога | git dirty не failure |
| Docker daemon | `docker ps` отвечает | Docker недоступен | нет |
| containers | expected containers running | core container stopped | optional/profile container stopped |
| disk | `/` и project filesystem usage | >= 95% | >= 85% |
| memory | host memory pressure | swap активно и memory high | memory high |
| GPU visibility | `nvidia-smi` видит GPU | no GPUs visible | high VRAM pressure |
| LiteLLM readiness | `/v1/models` отвечает через gateway | no response / auth failure | slow response |
| backend readiness | `/v1/models` на `8080` и `8081` | no response | slow response |
| memory-db readiness | container health / pg_isready if cheap | DB unhealthy | health unknown |
| memory-embed readiness | `127.0.0.1:4010/v1/models` | no response if service expected | slow response |
| telegram-bot | container running when profile enabled | stopped while expected | recent polling errors |

Не делать в v1:

* chat/completions generation;
* embeddings generation;
* docker restart/down/up;
* log scraping beyond tiny bounded tail;
* DB dumps;
* writes to Memory/RAG;
* external network checks beyond already configured local endpoints.

### Output format

Предпочтительный output:

```text
OK      service/litellm models endpoint answered
WARN    disk/root usage=87%
FAIL    docker daemon unavailable
INFO    telegram-bot profile service not running
```

Последняя строка:

```text
summary: ok=<n> warn=<n> fail=<n>
```

### Exit codes

```text
0  no FAIL
1  one or more FAIL
2  script usage/config error
```

Warnings не должны давать non-zero exit code на первом этапе.

### Secrets handling

Если нужен `LITELLM_MASTER_KEY`, скрипт должен загружать `.env` без печати values:

```bash
set -a
. ./.env
set +a
```

Запрещено:

* `set -x`;
* echo secrets;
* писать `.env` values в logs;
* отправлять secrets в Memory/RAG.

### Rollback

Если будущий script окажется шумным или неверным:

```bash
git checkout -- scripts/cluster-health-lite.sh docs/monitoring.md docs/changelog.md
```

Если позже будет добавлен timer/cron, он должен иметь отдельный rollback.

### Implementation boundary

Stage 7.1 реализовал `scripts/cluster-health-lite.sh`, но не добавил cron/systemd timer, alerting или automated remediation. Любая автоматизация запуска требует отдельного approval.


---

## 6. Проверенный результат Stage 7.1

Проверено на сервере:

```bash
bash -n scripts/cluster-health-lite.sh
scripts/cluster-health-lite.sh
```

Результат:

```text
summary: ok=21 warn=2 fail=0
exit_code=0
```

Warnings на момент проверки:

* working tree had uncommitted Stage 7.1 changes;
* tiny swap usage was detected.

После commit первый warning должен исчезнуть. Swap warning не является failure.
