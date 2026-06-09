#!/bin/bash
# Install and enable the etc-kg ingest worker on the Ollama GPU VM.
# Run on the VM after Ollama is working: bash setup-knowledge-worker-vm.sh
set -euo pipefail

ETC_KG_DIR="${ETC_KG_DIR:-$HOME/etc-kg}"
ENV_FILE="${ETC_KG_DIR}/config/.env"

if [[ ! -d "$ETC_KG_DIR" ]]; then
  echo "etc-kg not found at $ETC_KG_DIR. Copy the repo or set ETC_KG_DIR."
  exit 1
fi

cd "$ETC_KG_DIR"

find_python() {
  local candidate version major minor
  for candidate in python3.12 python3.11 python3; do
    if command -v "$candidate" >/dev/null 2>&1; then
      version="$("$candidate" -c 'import sys; print(f"{sys.version_info.major}.{sys.version_info.minor}")')"
      major="${version%%.*}"
      minor="${version#*.}"
      if (( major > 3 || (major == 3 && minor >= 11) )); then
        echo "$candidate"
        return 0
      fi
    fi
  done
  return 1
}

ensure_python() {
  if find_python >/dev/null; then
    return 0
  fi

  echo "Python 3.11+ not found. Installing python3.11 ..."
  sudo apt-get update -qq
  sudo apt-get install -y python3.11 python3.11-venv python3-pip
}

ensure_python
PYTHON="$(find_python)"
echo "Using $PYTHON ($($PYTHON --version))"

if [[ ! -x .venv/bin/pip ]]; then
  echo "Creating virtualenv in $ETC_KG_DIR/.venv ..."
  rm -rf .venv
  "$PYTHON" -m venv .venv
fi

.venv/bin/python -m pip install --upgrade pip
.venv/bin/pip install -e ".[azure]"

if [[ ! -f "$ENV_FILE" ]]; then
  cp config/.env.example "$ENV_FILE"
  echo "Created $ENV_FILE — edit KNOWLEDGE_DB_CONNECTION and AZURE_STORAGE_CONNECTION_STRING before starting."
  exit 1
fi

sudo tee /etc/systemd/system/etc-kg-worker.service >/dev/null <<EOF
[Unit]
Description=ETC knowledge ingest worker
After=network-online.target ollama.service
Wants=network-online.target

[Service]
Type=simple
User=$(whoami)
WorkingDirectory=${ETC_KG_DIR}
EnvironmentFile=${ENV_FILE}
ExecStart=${ETC_KG_DIR}/.venv/bin/python -m ingest.cli worker
Restart=on-failure
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable etc-kg-worker.service
sudo systemctl restart etc-kg-worker.service

echo "Worker status:"
sudo systemctl status etc-kg-worker.service --no-pager || true
echo ""
echo "Logs: journalctl -u etc-kg-worker -f"
