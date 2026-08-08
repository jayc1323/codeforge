#!/usr/bin/env bash
# Builds the Angular frontend and publishes it to the Caddy web root.
# Usage: ./deploy/deploy-frontend.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_ROOT=/var/www/codeforge

echo "Building frontend (production)..."
(cd "$ROOT/frontend" && npm run build)

echo "Publishing to $WEB_ROOT..."
mkdir -p "$WEB_ROOT"
rm -rf "$WEB_ROOT"/*
cp -r "$ROOT/frontend/dist/codeforge-ui/browser/." "$WEB_ROOT/"
chown -R caddy:caddy "$WEB_ROOT"

echo "Done. Caddy serves the new build immediately (no reload needed for static files)."
