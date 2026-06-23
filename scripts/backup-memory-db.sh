#!/usr/bin/env bash
set -u

ROOT="${ROOT:-/opt/llama-cluster}"
BACKUP_DIR="${BACKUP_DIR:-backups/memory-db}"
CONTAINER="${MEMORY_DB_CONTAINER:-memory-db}"
DOCKER_CMD=(docker)
CHECK_ONLY=0

usage() {
  cat <<'EOF'
Usage: scripts/backup-memory-db.sh [--check-only] [--root PATH] [--backup-dir PATH]

Create a manual custom-format pg_dump backup for the slowrig memory-db container.

Options:
  --check-only       Check prerequisites and print the planned backup path without creating a dump.
  --root PATH        Repository root. Default: /opt/llama-cluster or ROOT env var.
  --backup-dir PATH  Backup directory relative to root or absolute. Default: backups/memory-db.
  -h, --help         Show this help.

Environment:
  ROOT                 Repository root override.
  BACKUP_DIR           Backup directory override.
  MEMORY_DB_CONTAINER  Container name override. Default: memory-db.
EOF
}

info() { printf 'INFO    %s\n' "$*"; }
ok() { printf 'OK      %s\n' "$*"; }
fail() { printf 'FAIL    %s\n' "$*" >&2; }

while [ "$#" -gt 0 ]; do
  case "$1" in
    --check-only)
      CHECK_ONLY=1
      shift
      ;;
    --root)
      if [ "$#" -lt 2 ]; then
        fail "--root requires a path"
        exit 2
      fi
      ROOT="$2"
      shift 2
      ;;
    --backup-dir)
      if [ "$#" -lt 2 ]; then
        fail "--backup-dir requires a path"
        exit 2
      fi
      BACKUP_DIR="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      fail "unknown argument: $1"
      usage >&2
      exit 2
      ;;
  esac
done

if [ ! -d "$ROOT" ]; then
  fail "repo path missing: $ROOT"
  exit 2
fi

cd "$ROOT" || exit 2

if [ ! -f docker-compose.yaml ]; then
  fail "docker-compose.yaml not found in $ROOT"
  exit 2
fi

if ! "${DOCKER_CMD[@]}" ps >/dev/null 2>&1; then
  DOCKER_CMD=(sudo docker)
fi

if ! "${DOCKER_CMD[@]}" ps >/dev/null 2>&1; then
  fail "docker daemon unavailable via docker or sudo docker"
  exit 1
fi

if ! "${DOCKER_CMD[@]}" inspect "$CONTAINER" >/dev/null 2>&1; then
  fail "container missing: $CONTAINER"
  exit 1
fi

state=$("${DOCKER_CMD[@]}" inspect -f '{{.State.Status}}' "$CONTAINER" 2>/dev/null || true)
if [ "$state" != "running" ]; then
  fail "container is not running: $CONTAINER state=$state"
  exit 1
fi

if ! "${DOCKER_CMD[@]}" compose exec -T "$CONTAINER" sh -lc 'command -v pg_dump >/dev/null && command -v pg_restore >/dev/null' >/dev/null 2>&1; then
  fail "pg_dump/pg_restore are not available inside $CONTAINER"
  exit 1
fi

case "$BACKUP_DIR" in
  /*) backup_dir_abs="$BACKUP_DIR" ;;
  *) backup_dir_abs="$ROOT/$BACKUP_DIR" ;;
esac

stamp=$(date -u +%Y%m%d-%H%M%S)
file_name="slowrig-memory-${stamp}.dump"
meta_name="slowrig-memory-${stamp}.txt"
dump_path="$backup_dir_abs/$file_name"
meta_path="$backup_dir_abs/$meta_name"
tmp_path="$backup_dir_abs/.${file_name}.tmp.$$"

git_commit="unknown"
if git rev-parse --short=12 HEAD >/dev/null 2>&1; then
  git_commit=$(git rev-parse --short=12 HEAD)
fi

ok "repo path exists: $ROOT"
ok "docker daemon reachable via ${DOCKER_CMD[*]}"
ok "container running: $CONTAINER"
ok "pg_dump and pg_restore available inside $CONTAINER"
info "backup directory: $backup_dir_abs"
info "planned dump: $dump_path"
info "planned metadata: $meta_path"

if [ "$CHECK_ONLY" -eq 1 ]; then
  ok "check-only completed; no dump created"
  exit 0
fi

mkdir -p "$backup_dir_abs" || exit 1

if ! "${DOCKER_CMD[@]}" compose exec -T "$CONTAINER" sh -lc 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$tmp_path"; then
  rm -f "$tmp_path"
  fail "pg_dump failed; temporary file removed"
  exit 1
fi

if [ ! -s "$tmp_path" ]; then
  rm -f "$tmp_path"
  fail "pg_dump created an empty file; temporary file removed"
  exit 1
fi

if ! "${DOCKER_CMD[@]}" compose exec -T "$CONTAINER" sh -lc 'pg_restore --list >/dev/null' < "$tmp_path"; then
  rm -f "$tmp_path"
  fail "pg_restore metadata check failed; temporary file removed"
  exit 1
fi

chmod 600 "$tmp_path" 2>/dev/null || true
mv "$tmp_path" "$dump_path" || exit 1

sha256="unavailable"
if command -v sha256sum >/dev/null 2>&1; then
  sha256=$(sha256sum "$dump_path" | awk '{print $1}')
fi

cat > "$meta_path" <<EOF
slowrig memory-db backup metadata
created_utc=$stamp
git_commit=$git_commit
container=$CONTAINER
dump_file=$file_name
format=pg_dump custom format (-Fc)
sha256=$sha256
secrets_included=no
restore_note=Do not restore over the current production DB without separate approval.
EOF
chmod 600 "$meta_path" 2>/dev/null || true

size=$(ls -lh "$dump_path" | awk '{print $5}')
ok "dump created: $dump_path size=$size"
ok "metadata written: $meta_path"
ok "pg_restore metadata check passed"
info "copy the dump and metadata to offline storage; do not commit backups/"
