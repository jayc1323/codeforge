#!/usr/bin/env bash
# Deploys the backend as a published binary running under systemd.
#   1. dotnet publish (Release) -> staging dir
#   2. install /etc/systemd/system/codeforge-api.service (from this repo)
#   3. swap the published output into /opt/codeforge-api and restart
#   4. health-check until the API answers (or fail loudly)
# Usage: ./deploy/deploy-backend.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_DIR=/opt/codeforge-api
STAGING="$APP_DIR.new"
UNIT_SRC="$ROOT/deploy/codeforge-api.service"
UNIT_DST=/etc/systemd/system/codeforge-api.service
export PATH="/usr/local/dotnet:$PATH"

if [ ! -f "$ROOT/.secrets/env" ]; then
    echo "ERROR: $ROOT/.secrets/env is missing (DB connection string + JWT key)." >&2
    exit 1
fi

echo "Publishing backend (Release)..."
dotnet publish "$ROOT/backend/src/CodeForge.Api" -c Release -o "$STAGING" --nologo

echo "Installing systemd unit..."
cp "$UNIT_SRC" "$UNIT_DST"
systemctl daemon-reload
systemctl enable codeforge-api > /dev/null 2>&1

echo "Swapping $APP_DIR..."
if systemctl is-active --quiet codeforge-api; then
    systemctl stop codeforge-api
fi
rm -rf "$APP_DIR.old"
[ -d "$APP_DIR" ] && mv "$APP_DIR" "$APP_DIR.old"
mv "$STAGING" "$APP_DIR"

echo "Starting codeforge-api..."
systemctl start codeforge-api

echo "Waiting for the API to answer on :5045..."
for i in $(seq 1 30); do
    if curl -sf -o /dev/null http://localhost:5045/api/languages; then
        echo "Healthy after ${i}s. Rolling back leftover dir."
        rm -rf "$APP_DIR.old"
        systemctl is-active codeforge-api
        exit 0
    fi
    sleep 1
done

echo "ERROR: API did not become healthy in 30s." >&2
echo "Recent logs:" >&2
journalctl -u codeforge-api -n 30 --no-pager >&2
exit 1
