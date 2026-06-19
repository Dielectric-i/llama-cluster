#!/usr/bin/env bash
set -u

echo "============================================================"
echo " slowrig AI Cluster status"
echo " Time: $(date)"
echo " Host: $(hostname)"
echo "============================================================"
echo

echo "== Docker containers =="
sudo docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
echo

echo "== Compose services =="
cd /opt/llama-cluster || exit 1
sudo docker compose ps
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

check_url "llama-architect 27B" "http://127.0.0.1:8080/v1/models"
check_url "llama-coder 9B"      "http://127.0.0.1:8081/v1/models"
check_url "open-webui"          "http://127.0.0.1:3000"
echo

echo "== Recent suspicious log lines =="
echo

for c in llama-architect llama-coder open-webui; do
  echo "-- $c --"
  sudo docker logs --tail=250 "$c" 2>&1 | grep -Ei 'error|failed|fail|oom|out of memory|cuda error|cudamalloc|exception|traceback|unhealthy|killed|segmentation fault|illegal memory|invalid device' | tail -30 || true
  echo
done

echo "============================================================"
echo " Done"
echo "============================================================"