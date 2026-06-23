# slowrig AI Cluster — Monitoring Design v0.1

Дата: 2026-06-23
Статус: Stage 7 design; runtime не внедрён

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

Будущий `cluster-health-lite.sh` должен проверять только дешёвые признаки:

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

## 5. Future implementation plan

Рекомендуемый следующий monitoring stage:

```text
Stage 7.1 — cluster-health-lite.sh implementation plan
```

Он должен определить:

* точный список checks;
* expected output format;
* exit codes;
* что считается warning vs failure;
* как загружать `.env` без вывода secrets;
* нужен ли cron/systemd timer позже;
* rollback.

Runtime script не добавлять до отдельного implementation approval.
