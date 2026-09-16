#!/usr/bin/env bash
# Builds the Angular frontend and publishes it to the nginx web root.
# Usage: ./deploy/deploy-frontend.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_ROOT=/var/www/codeforge

echo "Installing frontend dependencies (if needed)..."
if [ ! -d "$ROOT/frontend/node_modules" ]; then
    (cd "$ROOT/frontend" && npm ci)
fi

echo "Building frontend (production)..."
(cd "$ROOT/frontend" && npm run build)

echo "Publishing to $WEB_ROOT..."
mkdir -p "$WEB_ROOT"
rm -rf "$WEB_ROOT"/*
# Angular 17+ outputs to browser/ subdirectory
if [ -d "$ROOT/frontend/dist/codeforge-ui/browser" ]; then
    cp -r "$ROOT/frontend/dist/codeforge-ui/browser/." "$WEB_ROOT/"
else
    cp -r "$ROOT/frontend/dist/codeforge-ui/." "$WEB_ROOT/"
fi
chown -R nginx:nginx "$WEB_ROOT"

echo "Done. nginx serves the new build immediately (no reload needed for static files)."
