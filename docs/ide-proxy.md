# ide-proxy — SSE Heartbeat Proxy для VS Code

## Цель

Решить проблему таймаута VS Code / Copilot / LLM Gateway при работе с большими контекстами (40k–60k токенов).

Когда upstream (LiteLLM → llama.cpp) долго молчит во время prompt processing (4–6 минут), клиентский HTTP/SSE/fetch timeout в VS Code extension host падает через ~300 секунд с ошибкой `fetch failed`.

Прокси отправляет SSE heartbeat-комментарии (`: ping\n\n`) клиенту каждые 10 секунд, даже когда upstream молчит, тем самым удерживая соединение живым.

## Схема

```
VS Code / LLM Gateway (Remote SSH)
    → ide-proxy:4011  (C# ASP.NET Core)
    → LiteLLM:4000    (gateway)
    → llama-architect:8080 / llama-coder:8080  (llama.cpp)
```

## Endpoints

### GET /health

Health check. Возвращает:
```json
{
  "status": "ok",
  "service": "slowrig-ide-proxy",
  "upstreamBaseUrl": "http://litellm:4000/v1",
  "heartbeatIntervalSeconds": 10
}
```

### GET /debug/active

Активные запросы (без секретов, без prompt text):
```json
{
  "activeRequests": 1,
  "activeArchitectRequests": 1,
  "requests": [
    {
      "id": "abc123def456",
      "model": "slowrig/architect",
      "startedAtUtc": "2025-01-01T00:00:00Z",
      "elapsedSeconds": 123.5,
      "heartbeatsSent": 12,
      "firstUpstreamByteAfterSeconds": null,
      "state": "waiting_upstream"
    }
  ]
}
```

### GET /v1/models

Проксирует запрос к LiteLLM. Пробрасывает Authorization header.

### POST /v1/chat/completions

Основной endpoint. Логика:
1. Извлекает metadata (model, stream, messages count, tools count, max_tokens)
2. Busy protection: если slowrig/architect занят → HTTP 429
3. Streaming mode:
   - Сразу открывает SSE response клиенту
   - Отправляет initial heartbeat `: slowrig-ide-proxy connected`
   - Параллельно открывает upstream request к LiteLLM
   - Читает upstream stream byte chunks
   - Пока upstream молчит — отправляет `: ping\n\n` каждые 10 секунд
   - Прокидывает реальные upstream chunks в downstream
   - При завершении upstream корректно завершает downstream
4. Non-streaming mode: простой pass-through

## Environment variables

| Переменная | По умолчанию | Описание |
| --- | --- | --- |
| `UPSTREAM_BASE_URL` | `http://litellm:4000/v1` | адрес LiteLLM внутри Docker network |
| `HEARTBEAT_INTERVAL_SECONDS` | `10` | интервал heartbeat (сек) |
| `UPSTREAM_CONNECT_TIMEOUT_SECONDS` | `10` | таймаут TCP-подключения к upstream |
| `UPSTREAM_TOTAL_TIMEOUT_SECONDS` | `0` | общий таймаут запроса (0 = нет) |
| `MAX_ACTIVE_ARCHITECT_REQUESTS` | `1` | лимит параллельных запросов к architect |
| `BUSY_MODE` | `reject` | режим при перегрузке (`reject` → 429) |
| `ARCHITECT_MODEL_NAME` | `slowrig/architect` | имя модели для busy protection |

## Эксплуатационные правила

### Перед запуском большого запроса

1. Проверить active requests:
   ```bash
   curl -sS http://127.0.0.1:4011/debug/active | jq
   ```
2. Если `activeArchitectRequests > 0` — ждать или отменить текущий запрос

### После Stop в VS Code

1. Подождать, пока `/debug/active` не покажет `activeArchitectRequests: 0`
2. Или проверить логи:
   ```bash
   sudo docker logs ide-proxy --tail=50 | grep -E 'client_disconnect|upstream_done|downstream_done'
   ```
3. Если запрос завис — перезапустить llama-architect:
   ```bash
   sudo docker compose restart llama-architect
   ```

### Когда не запускать второй запрос

- Пока `/debug/active` показывает активный запрос на slowrig/architect
- Пока логи не показали `upstream_done` или `client_disconnect`
- Второй запрос получит HTTP 429 (busy_rejected)

## Troubleshooting

### Прокси не отвечает

```bash
# Health check
curl -sS http://127.0.0.1:4011/health | jq

# Логи
sudo docker logs ide-proxy --tail=100

# Статус контейнера
sudo docker ps | grep ide-proxy
```

### VS Code всё равно падает

1. Проверить, что serverUrl указывает на 4011, а не 4000
2. Проверить heartbeat в логах:
   ```bash
   sudo docker logs ide-proxy --tail=200 | grep heartbeat
   ```
3. Проверить, что upstream реально работает:
   ```bash
   sudo docker logs llama-architect --tail=50
   ```

### Upstream error

```bash
# Проверить логи прокси на ошибки upstream
sudo docker logs ide-proxy --tail=100 | grep -i 'upstream_error'

# Проверить LiteLLM
sudo docker logs litellm --tail=50

# Проверить llama.cpp
sudo docker logs llama-architect --tail=50
```

### Busy rejection (HTTP 429)

Это нормально — значит architect занят. Варианты:
1. Подождать завершения текущего запроса
2. Отменить текущий запрос (Stop в VS Code)
3. Перезапустить llama-architect если запрос завис

## Как смотреть логи

```bash
# Последние 100 строк
sudo docker logs ide-proxy --tail=100

# Только ошибки
sudo docker logs ide-proxy --tail=500 | grep -i 'error\|fail'

# Активные запросы в реальном времени
watch -n 2 'curl -sS http://127.0.0.1:4011/debug/active | jq'
```

## Критерии успеха

1. ✅ ide-proxy собирается и стартует в Docker
2. ✅ `/health` возвращает 200 OK
3. ✅ `/v1/models` работает через proxy
4. ✅ Small streaming request работает
5. ✅ Big streaming request через curl живёт > 300 секунд и получает heartbeat
6. ✅ Big streaming request через VS Code не падает через 300 секунд
7. ✅ При занятом architect второй request получает 429
8. ✅ Rollback простой
9. ✅ Open WebUI не затронут

## Rollback

1. В VS Code вернуть `serverUrl`:
   ```json
   "github.copilot.llm-gateway.serverUrl": "http://127.0.0.1:4000"
   ```

2. Остановить прокси:
   ```bash
   cd /opt/llama-cluster
   sudo docker compose stop ide-proxy
   ```

3. Если нужно полностью убрать:
   ```bash
   sudo docker compose rm -f ide-proxy
   sudo docker compose down ide-proxy
   ```

4. Вернуть изменения через git:
   ```bash
   git diff
   git checkout -- docker-compose.yaml docs/ scripts/
   rm -rf proxy/
   ```
