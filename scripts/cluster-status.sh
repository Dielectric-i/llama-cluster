#!/usr/bin/env bash
set -u

echo "============================================================"
echo " slowrig AI Cluster status"
echo " Time: $(date)"
echo " Host: $(hostname)"
echo "============================================================"
echo

DOCKER_CMD=(docker)

if ! docker ps >/dev/null 2>&1; then
  DOCKER_CMD=(sudo docker)
fi

echo "== Docker containers =="
"${DOCKER_CMD[@]}" ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
echo

echo "== Compose services =="
cd /opt/llama-cluster || exit 1
"${DOCKER_CMD[@]}" compose ps
echo

echo "== NVIDIA GPUs =="
nvidia-smi
echo

echo "== API checks =="

check_url() {
  local name="$1"
  local url="$2"

  echo -n "$name -> $url : "

  if curl -fsS --max-time 5 "$url" >/tmp/cluster-status-response.json 2>/tmp/cluster-status-error.log; then
    echo "OK"
  else
    echo "FAIL"
    echo "  error: $(cat /tmp/cluster-status-error.log)"
  fi
}

check_litellm_models() {
  echo -n "litellm gateway -> http://127.0.0.1:4000/v1/models : "

  if [ -z "${LITELLM_MASTER_KEY:-}" ]; then
    echo "SKIP"
    echo "  reason: LITELLM_MASTER_KEY is not loaded"
    return 0
  fi

  if curl -fsS --max-time 8 \
    -H "Authorization: Bearer ${LITELLM_MASTER_KEY}" \
    "http://127.0.0.1:4000/v1/models" \
    >/tmp/cluster-status-litellm-models.json \
    2>/tmp/cluster-status-error.log; then
    echo "OK"
  else
    echo "FAIL"
    echo "  error: $(cat /tmp/cluster-status-error.log)"
  fi
}

check_litellm_chat() {
  local model="$1"
  local label="$2"

  echo -n "litellm chat ${label} (${model}) : "

  if [ -z "${LITELLM_MASTER_KEY:-}" ]; then
    echo "SKIP"
    echo "  reason: LITELLM_MASTER_KEY is not loaded"
    return 0
  fi

  if curl -fsS --max-time 30 \
    -H "Authorization: Bearer ${LITELLM_MASTER_KEY}" \
    -H "Content-Type: application/json" \
    "http://127.0.0.1:4000/v1/chat/completions" \
    -d "{
      \"model\": \"${model}\",
      \"messages\": [
        {
          \"role\": \"user\",
          \"content\": \"Ответь одним словом: OK\"
        }
      ],
      \"temperature\": 0,
      \"max_tokens\": 8
    }" \
    >/tmp/cluster-status-litellm-chat.json \
    2>/tmp/cluster-status-error.log; then
    echo "OK"
  else
    echo "FAIL"
    echo "  error: $(cat /tmp/cluster-status-error.log)"
  fi
}

check_memory_db() {
  echo -n "memory-db pg_isready : "

  if "${DOCKER_CMD[@]}" compose exec -T memory-db sh -lc 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"' >/tmp/cluster-status-memory-db.txt 2>/tmp/cluster-status-error.log; then
    echo "OK"
  else
    echo "FAIL"
    echo "  error: $(cat /tmp/cluster-status-error.log)"
    return 0
  fi

  echo -n "memory-db pgvector extension : "

  if "${DOCKER_CMD[@]}" compose exec -T memory-db sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "SELECT extname FROM pg_extension;"' >/tmp/cluster-status-memory-vector.txt 2>/tmp/cluster-status-error.log; then
    if grep -qx 'vector' /tmp/cluster-status-memory-vector.txt; then
      echo "OK"
    else
      echo "FAIL"
      echo "  error: vector extension not found"
    fi
  else
    echo "FAIL"
    echo "  error: $(cat /tmp/cluster-status-error.log)"
  fi
}

check_url "llama-architect 27B" "http://127.0.0.1:8080/v1/models"
check_url "llama-coder 9B"      "http://127.0.0.1:8081/v1/models"
check_url "memory-embed"        "http://127.0.0.1:4010/v1/models"
check_url "open-webui"          "http://127.0.0.1:3000"

if [ -f /opt/llama-cluster/.env ]; then
  set -a
  # shellcheck disable=SC1091
  source /opt/llama-cluster/.env
  set +a
fi

check_litellm_models
check_litellm_chat "slowrig/coder" "9B"
check_litellm_chat "slowrig/architect" "27B"
check_memory_db

echo

echo "== Recent suspicious log lines =="
echo

for c in llama-architect llama-coder litellm open-webui memory-db memory-embed; do
  echo "-- $c --"
  "${DOCKER_CMD[@]}" logs --tail=250 "$c" 2>&1 | grep -Ei 'error|failed|fail|oom|out of memory|cuda error|cudamalloc|exception|traceback|unhealthy|killed|segmentation fault|illegal memory|invalid device' | tail -30 || true
  echo
done

echo "============================================================"
echo " Done"
echo "============================================================"
