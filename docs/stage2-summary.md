# slowrig AI Cluster — Stage 2 Summary

Дата: 2026-06-19  
Статус: Stage 2 operational baseline завершён.

## 1. Что завершено

Stage 2 создал эксплуатационную основу вокруг уже работающего inference baseline.

После Stage 2 проект больше не является просто набором Docker-контейнеров. Теперь у него есть:

- документация;
- baseline-конфигурация;
- локальная история изменений;
- runbook;
- health/status script;
- журнал решений;
- changelog;
- целевая архитектура развития.

## 2. Текущий рабочий inference baseline

Сервисы:

| Сервис | Порт | Роль |
|---|---:|---|
| `llama-architect` | `8080` | 27B architect / deep reasoning |
| `llama-coder` | `8081` | 9B coder / fast worker |
| `open-webui` | `3000` | ручной WebUI |

GPU mapping:

| GPU | Использование |
|---|---|
| GPU 0 | часть `llama-architect` |
| GPU 1 | `llama-coder`, PCIe x16 |
| GPU 2 | часть `llama-architect` |

Обе модели работают с:

```text
ctx-size 40000
```

## 10. После Stage 2

После завершения Stage 2 начат Stage 3 — Gateway / Router.

Текущий выбранный gateway: LiteLLM Proxy.

Подробности Stage 3 описываются в:

```text
docs/gateway.md
docs/changelog.md
docs/decisions.md
```
