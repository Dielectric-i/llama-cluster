# slowrig AI Cluster — Passport v0.3

Дата актуализации: 2026‑07‑01
Статус: **фактическое состояние стенда**. Документ содержит только факты; контекст, решения и планы ищи в [README.md](../README.md).

---

## 1. Хост и железо

| Параметр | Значение |
| --- | --- |
| Hostname | `slowrig` |
| LAN IP | `192.168.1.6` |
| OS | Ubuntu Server 26.04 |
| CPU | Xeon E5‑2678 v3 (24 thr) |
| RAM | 128 GB |
| GPUs | 3 × NVIDIA P102‑100 10 GB |
| GPU split | GPU 0 + GPU 2 → 27B backend, GPU 1 → 9B backend |
| VRAM total | 30 GB |
| Project dir | `/opt/llama-cluster` |

---

## 2. Сервисы (Docker Compose)

| Service | Port | Role |
| --- | ---: | --- |
| `open-webui` | 3000 | manual WebUI via gateway |
| `litellm` | 4000 | LLM Gateway / Router |
| `llama-architect` | 8080 | 27B deep‑reasoning backend (`ctx 65000`) |
| `llama-coder` | 8081 | 9B fast backend (`ctx 128000`) |
| `memory-db` | — | PostgreSQL + pgvector (internal) |
| `memory-embed` | 4010 | local embeddings (loopback only) |
| `telegram-bot` | — | Telegram polling client via LiteLLM |
| `ide-proxy` | 4011 | **experiment** — SSE heartbeat for IDE/Copilot |

Прямые порты 8080/8081 оставлены для диагностики в LAN; обычные клиенты идут через Gateway :4000.

---

## 3. Модели

| Backend | GGUF file | ctx |
| --- | --- | ---: |
| `llama-architect` | `Qwen3.6-27B-UD-Q4_K_XL.gguf` | 65000 |
| `llama-coder` | `Qwen3.5-9B-UD-Q4_K_XL.gguf` | 128000 |
| `memory-embed` | `Qwen3-Embedding-0.6B-Q8_0.gguf` | 32768 |

---

## 4. Gateway model names

```text
slowrig/coder      -> llama-coder
slowrig/architect  -> llama-architect
```

---

## 5. Диагностические скрипты

* `scripts/cluster-health-lite.sh` — дешёвый healthcheck (без LLM generation).
* `scripts/cluster-status.sh` — глубокая ручная диагностика.

---

## 6. Ограничения

* LAN/VPN‑first; внешние порты не публикуются без security stage.
* Нет RAG retrieval API для клиентов (только ingestion).
* Agent runtime отсутствует; Telegram — read‑only interface.
* Multi‑node cluster — дальняя опция.
