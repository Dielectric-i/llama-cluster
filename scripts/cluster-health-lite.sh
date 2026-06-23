#!/usr/bin/env bash
set -u

ROOT="${1:-/opt/llama-cluster}"
OK_COUNT=0
WARN_COUNT=0
FAIL_COUNT=0
DOCKER_CMD=(docker)

ok() {
  OK_COUNT=$((OK_COUNT + 1))
  printf 'OK      %s\n' "$*"
}

warn() {
  WARN_COUNT=$((WARN_COUNT + 1))
  printf 'WARN    %s\n' "$*"
}

fail() {
  FAIL_COUNT=$((FAIL_COUNT + 1))
  printf 'FAIL    %s\n' "$*"
}

info() {
  printf 'INFO    %s\n' "$*"
}

has_cmd() {
  command -v "$1" >/dev/null 2>&1
}

pct_num() {
  printf '%s' "$1" | tr -dc '0-9'
}

check_http() {
  label="$1"
  url="$2"
  max_time="${3:-4}"
  headers_file="$(mktemp)"
  body_file="$(mktemp)"
  err_file="$(mktemp)"

  start_ns=$(date +%s%N)
  if curl -fsS --max-time "$max_time" -o "$body_file" -D "$headers_file" "$url" 2>"$err_file"; then
    end_ns=$(date +%s%N)
    elapsed_ms=$(( (end_ns - start_ns) / 1000000 ))
    if [ "$elapsed_ms" -gt $(( max_time * 1000 * 80 / 100 )) ]; then
      warn "$label answered slowly elapsed_ms=$elapsed_ms url=$url"
    else
      ok "$label answered elapsed_ms=$elapsed_ms url=$url"
    fi
  else
    err=$(tr '\n' ' ' < "$err_file" | cut -c1-220)
    fail "$label failed url=$url error=$err"
  fi

  rm -f "$headers_file" "$body_file" "$err_file"
}

check_litellm_models() {
  if [ -z "${LITELLM_MASTER_KEY:-}" ]; then
    warn "litellm models skipped; LITELLM_MASTER_KEY is not loaded"
    return 0
  fi

  body_file="$(mktemp)"
  err_file="$(mktemp)"
  start_ns=$(date +%s%N)
  if curl -fsS --max-time 5 \
    -H "Authorization: Bearer ${LITELLM_MASTER_KEY}" \
    -o "$body_file" \
    http://127.0.0.1:4000/v1/models \
    2>"$err_file"; then
    end_ns=$(date +%s%N)
    elapsed_ms=$(( (end_ns - start_ns) / 1000000 ))
    if grep -q 'slowrig/coder' "$body_file" && grep -q 'slowrig/architect' "$body_file"; then
      ok "litellm models endpoint answered elapsed_ms=$elapsed_ms"
    else
      warn "litellm models endpoint answered but expected model names were not both visible elapsed_ms=$elapsed_ms"
    fi
  else
    err=$(tr '\n' ' ' < "$err_file" | cut -c1-220)
    fail "litellm models endpoint failed error=$err"
  fi
  rm -f "$body_file" "$err_file"
}

check_container_running() {
  name="$1"
  required="$2"

  if ! "${DOCKER_CMD[@]}" inspect "$name" >/dev/null 2>&1; then
    if [ "$required" = "required" ]; then
      fail "container missing: $name"
    else
      info "optional container missing: $name"
    fi
    return 0
  fi

  state=$("${DOCKER_CMD[@]}" inspect -f '{{.State.Status}}' "$name" 2>/dev/null || true)
  health=$("${DOCKER_CMD[@]}" inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$name" 2>/dev/null || true)
  restarts=$("${DOCKER_CMD[@]}" inspect -f '{{.RestartCount}}' "$name" 2>/dev/null || echo 0)

  if [ "$state" = "running" ]; then
    if [ "$health" = "unhealthy" ]; then
      fail "container unhealthy: $name health=$health restarts=$restarts"
    elif [ "$health" = "starting" ]; then
      warn "container still starting: $name health=$health restarts=$restarts"
    elif [ "$restarts" -gt 0 ]; then
      warn "container running with restarts: $name restarts=$restarts health=$health"
    else
      ok "container running: $name health=$health"
    fi
  else
    if [ "$required" = "required" ]; then
      fail "container not running: $name state=$state"
    else
      info "optional container not running: $name state=$state"
    fi
  fi
}

if [ ! -d "$ROOT" ]; then
  fail "repo path missing: $ROOT"
  printf 'summary: ok=%s warn=%s fail=%s\n' "$OK_COUNT" "$WARN_COUNT" "$FAIL_COUNT"
  exit 2
fi

cd "$ROOT" || exit 2

ok "repo path exists: $ROOT"

if [ -d .git ]; then
  if git status --short | grep -q .; then
    warn "git working tree has changes"
  else
    ok "git working tree clean"
  fi
else
  warn "git metadata not found"
fi

if ! "${DOCKER_CMD[@]}" ps >/dev/null 2>&1; then
  DOCKER_CMD=(sudo docker)
fi

if "${DOCKER_CMD[@]}" ps >/dev/null 2>&1; then
  ok "docker daemon reachable via ${DOCKER_CMD[*]}"
else
  fail "docker daemon unavailable"
fi

for name in llama-architect llama-coder litellm open-webui memory-db memory-embed; do
  check_container_running "$name" required
done
check_container_running telegram-bot optional

root_usage=$(df -P / | awk 'NR==2 {print $5}')
root_pct=$(pct_num "$root_usage")
if [ -n "$root_pct" ]; then
  if [ "$root_pct" -ge 95 ]; then
    fail "disk root usage=${root_pct}%"
  elif [ "$root_pct" -ge 85 ]; then
    warn "disk root usage=${root_pct}%"
  else
    ok "disk root usage=${root_pct}%"
  fi
else
  warn "disk root usage unknown"
fi

project_usage=$(df -P "$ROOT" | awk 'NR==2 {print $5}')
project_pct=$(pct_num "$project_usage")
if [ -n "$project_pct" ]; then
  if [ "$project_pct" -ge 95 ]; then
    fail "disk project usage=${project_pct}%"
  elif [ "$project_pct" -ge 85 ]; then
    warn "disk project usage=${project_pct}%"
  else
    ok "disk project usage=${project_pct}%"
  fi
else
  warn "disk project usage unknown"
fi

if has_cmd free; then
  mem_line=$(free | awk '/^Mem:/ {print $2, $3}')
  set -- $mem_line
  mem_total="${1:-0}"
  mem_used="${2:-0}"
  if [ "$mem_total" -gt 0 ]; then
    mem_pct=$(( mem_used * 100 / mem_total ))
    if [ "$mem_pct" -ge 95 ]; then
      fail "memory usage=${mem_pct}%"
    elif [ "$mem_pct" -ge 85 ]; then
      warn "memory usage=${mem_pct}%"
    else
      ok "memory usage=${mem_pct}%"
    fi
  else
    warn "memory usage unknown"
  fi

  swap_line=$(free | awk '/^Swap:/ {print $2, $3}')
  set -- $swap_line
  swap_total="${1:-0}"
  swap_used="${2:-0}"
  if [ "$swap_total" -gt 0 ] && [ "$swap_used" -gt 0 ]; then
    warn "swap in use used_kb=$swap_used total_kb=$swap_total"
  else
    ok "swap not in use"
  fi
else
  warn "free command not available"
fi

if has_cmd nvidia-smi; then
  gpu_count=$(nvidia-smi --query-gpu=index --format=csv,noheader 2>/dev/null | wc -l | tr -d ' ')
  if [ "${gpu_count:-0}" -ge 1 ]; then
    ok "nvidia-smi sees GPUs count=$gpu_count"
    while IFS=, read -r idx used total; do
      idx=$(printf '%s' "$idx" | xargs)
      used=$(printf '%s' "$used" | xargs)
      total=$(printf '%s' "$total" | xargs)
      if [ -n "$total" ] && [ "$total" -gt 0 ]; then
        pct=$(( used * 100 / total ))
        if [ "$pct" -ge 98 ]; then
          warn "gpu/$idx high VRAM usage=${pct}% used_mib=$used total_mib=$total"
        else
          ok "gpu/$idx VRAM usage=${pct}% used_mib=$used total_mib=$total"
        fi
      fi
    done <<EOF
$(nvidia-smi --query-gpu=index,memory.used,memory.total --format=csv,noheader,nounits 2>/dev/null)
EOF
  else
    fail "nvidia-smi sees no GPUs"
  fi
else
  fail "nvidia-smi not available"
fi

check_http "llama-architect models" "http://127.0.0.1:8080/v1/models" 4
check_http "llama-coder models" "http://127.0.0.1:8081/v1/models" 4
check_http "memory-embed models" "http://127.0.0.1:4010/v1/models" 4
check_http "open-webui root" "http://127.0.0.1:3000" 4

if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  . ./.env
  set +a
else
  warn ".env not found; litellm authenticated check may be skipped"
fi
check_litellm_models

printf 'summary: ok=%s warn=%s fail=%s\n' "$OK_COUNT" "$WARN_COUNT" "$FAIL_COUNT"

if [ "$FAIL_COUNT" -gt 0 ]; then
  exit 1
fi

exit 0
