# slowrig-ide-proxy

SSE heartbeat proxy для VS Code / Copilot / LLM Gateway.

## Зачем

VS Code extension host (Node.js / Undici) падает с `fetch failed` через ~300 секунд, когда upstream (LiteLLM → llama.cpp) долго молчит во время prompt processing больших контекстов (40k–60k токенов).

Прокси решает проблему, отправляя SSE heartbeat-комментарии (`: ping\n\n`) клиенту каждые N секунд, даже когда upstream молчит.

## Схема

```
VS Code / LLM Gateway
    → ide-proxy (порт 4011)
    → LiteLLM (порт 4000)
    → llama.cpp (llama-architect / llama-coder)
```

## Endpoints

| Метод | Путь | Описание |
| --- | --- | --- |
| GET | `/health` | health check |
| GET | `/debug/active` | активные запросы (без секретов) |
| GET | `/v1/models` | прокси → LiteLLM |
| POST | `/v1/chat/completions` | прокси с SSE heartbeat |

## Environment variables

| Переменная | По умолчанию | Описание |
| --- | --- | --- |
| `UPSTREAM_BASE_URL` | `http://litellm:4000/v1` | адрес LiteLLM |
| `HEARTBEAT_INTERVAL_SECONDS` | `10` | интервал heartbeat (сек) |
| `UPSTREAM_CONNECT_TIMEOUT_SECONDS` | `10` | таймаут подключения к upstream |
| `UPSTREAM_TOTAL_TIMEOUT_SECONDS` | `0` | общий таймаут (0 = нет) |
| `MAX_ACTIVE_ARCHITECT_REQUESTS` | `1` | лимит параллельных запросов к architect |
| `BUSY_MODE` | `reject` | `reject` — вернуть 429 |
| `ARCHITECT_MODEL_NAME` | `slowrig/architect` | имя модели для busy protection |

## Curl tests

```bash
# Health
curl -sS http://127.0.0.1:4011/health | jq

# Models (с ключом)
set -a; source /opt/llama-cluster/.env; set +a
curl -sS -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  http://127.0.0.1:4011/v1/models | jq

# Small streaming
curl -N --no-buffer \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $LITELLM_MASTER_KEY" \
  http://127.0.0.1:4011/v1/chat/completions \
  -d '{"model":"slowrig/coder","messages":[{"role":"user","content":"Ответь одной строкой: proxy ok"}],"stream":true,"max_tokens":32}'

# Active requests
curl -sS http://127.0.0.1:4011/debug/active | jq
```

## VS Code settings

```json
{
  "github.copilot.llm-gateway.serverUrl": "http://127.0.0.1:4011",
  "github.copilot.llm-gateway.defaultMaxTokens": 55000,
  "github.copilot.llm-gateway.defaultMaxOutputTokens": 5000,
  "github.copilot.llm-gateway.extraModelOptions": {
    "temperature": 0.2,
    "top_p": 0.9
  },
  "github.copilot.llm-gateway.verboseLogging": true
}
```

## Rollback

1. В VS Code вернуть `serverUrl`: `http://127.0.0.1:4000`
2. Остановить прокси:
   ```bash
   cd /opt/llama-cluster
   sudo docker compose stop ide-proxy
   ```
