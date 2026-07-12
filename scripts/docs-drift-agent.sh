#!/usr/bin/env bash
set -u

ROOT="${1:-/opt/llama-cluster}"
OK_COUNT=0
WARN_COUNT=0
FAIL_COUNT=0

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

search_fixed() {
  pattern="$1"
  shift
  if has_cmd rg; then
    rg -n --fixed-strings -- "$pattern" "$@" || true
  else
    grep -R -n -F -- "$pattern" "$@" || true
  fi
}

if [ ! -d "$ROOT" ]; then
  fail "repo path missing: $ROOT"
  printf 'summary: ok=%s warn=%s fail=%s\n' "$OK_COUNT" "$WARN_COUNT" "$FAIL_COUNT"
  exit 2
fi

cd "$ROOT" || exit 2

if [ ! -d .git ]; then
  fail "not a git repository: $ROOT"
else
  ok "git repository found"
fi

required_files="README.md AGENTS.md docs/codex-context.md docs/agent-framework.md docs/decisions.md docs/changelog.md docker-compose.yaml"
for file in $required_files; do
  if [ -f "$file" ]; then
    ok "required file exists: $file"
  else
    fail "required file missing: $file"
  fi
done

if git ls-files --error-unmatch .env >/dev/null 2>&1; then
  fail ".env is tracked by git"
else
  ok ".env is not tracked by git"
fi

if git status --short | grep -q '^?? .env$'; then
  fail ".env appears as untracked; check .gitignore before committing"
else
  ok ".env is not shown as untracked"
fi

if git status --short | grep -q .; then
  warn "working tree has changes; review before committing"
else
  ok "working tree clean"
fi

for doc in docs/*.md; do
  name="${doc#docs/}"
  if grep -Fq "[$doc]($doc)" README.md || grep -Fq "[$name](docs/$name)" README.md || grep -Fq "[$doc](docs/$name)" README.md; then
    ok "README indexes $doc"
  else
    warn "README may not index $doc"
  fi
done

if [ -f docker-compose.yaml ]; then
  services=$(awk '
    /^services:/ { inside=1; next }
    /^volumes:/ { inside=0 }
    inside && /^  [A-Za-z0-9_-]+:/ { gsub(":", "", $1); print $1 }
  ' docker-compose.yaml)
  for service in $services; do
    if grep -Fq "\`$service\`" README.md || grep -Fq "$service" docs/passport.md; then
      ok "service documented: $service"
    else
      warn "service may be undocumented: $service"
    fi
  done
else
  fail "docker-compose.yaml missing"
fi

check_value_in_docs() {
  label="$1"
  value="$2"
  shift 2
  found=0
  for file in "$@"; do
    if [ -f "$file" ] && grep -Fq "$value" "$file"; then
      found=1
    fi
  done
  if [ "$found" -eq 1 ]; then
    ok "$label documented: $value"
  else
    warn "$label may be missing from current docs: $value"
  fi
}

check_value_in_docs "architect ctx-size" "ctx-size 65000" README.md docs/passport.md docs/decisions.md
check_value_in_docs "coder ctx-size" "ctx-size 128000" README.md docs/passport.md docs/decisions.md
check_value_in_docs "architect tensor-split" "tensor-split 1.06,1" docs/passport.md docs/decisions.md docs/changelog.md
check_value_in_docs "Telegram thinking disable" "chat_template_kwargs.enable_thinking=false" docs/telegram.md docs/codex-context.md docs/changelog.md
check_value_in_docs "Stage 6 workflow" "docs drift / repo patch assistant" docs/agent-framework.md docs/codex-context.md docs/decisions.md

scan_stale_current_docs() {
  pattern="$1"
  hits=$(search_fixed "$pattern" README.md docs/*.md | grep -v 'docs/changelog.md' || true)
  if [ -n "$hits" ]; then
    warn "possible stale wording outside historical docs: $pattern"
    printf '%s\n' "$hits" | sed 's/^/INFO    /'
  else
    ok "no current-doc stale wording: $pattern"
  fi
}

scan_stale_current_docs "$(printf '%s%s' 'Telegram UI validation' ' pending')"
scan_stale_current_docs "Telegram ещё не подключён"
scan_stale_current_docs "$(printf '%s%s' 'design documented; runtime' ' not implemented')"
scan_stale_current_docs "Stage 6 | Agent framework"
scan_stale_current_docs "$(printf '%s%s' 'docs/' 'index.md')"
scan_stale_current_docs "$(printf '%s%s' 'docs/' 'agents.md')"
scan_stale_current_docs "$(printf '%s%s' '--profile' ' telegram')"
ctx_hits=$(search_fixed "ctx-size 40000" README.md docs/*.md | grep -v 'docs/changelog.md' | grep -v 'docs/decisions.md' || true)
if [ -n "$ctx_hits" ]; then
  warn "possible stale wording outside historical docs: ctx-size 40000"
  printf '%s\n' "$ctx_hits" | sed 's/^/INFO    /'
else
  ok "no current-doc stale wording: ctx-size 40000"
fi

printf 'summary: ok=%s warn=%s fail=%s\n' "$OK_COUNT" "$WARN_COUNT" "$FAIL_COUNT"

if [ "$FAIL_COUNT" -gt 0 ]; then
  exit 1
fi

exit 0
