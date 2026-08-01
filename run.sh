#!/usr/bin/env bash
# Starts the CodeForge API (port 5045) and Angular dev server (port 4200).
# Ctrl+C stops both. Usage: ./run.sh
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PATH="/usr/local/dotnet:$PATH"   # dotnet is not on the default PATH

API_LOG=/tmp/codeforge-api.log
UI_LOG=/tmp/codeforge-ui.log

echo "Starting CodeForge API..."
(cd "$ROOT/backend/src/CodeForge.Api" && dotnet run > "$API_LOG" 2>&1) &
API_PID=$!

echo "Starting Angular dev server..."
(cd "$ROOT/frontend" && npx ng serve --host 0.0.0.0 --port 4200 > "$UI_LOG" 2>&1) &
UI_PID=$!

cleanup() {
    echo ""
    echo "Shutting down..."
    kill "$API_PID" "$UI_PID" 2>/dev/null
    # dotnet run / ng serve spawn child processes; make sure they go down too
    pkill -f "CodeForge.Api" 2>/dev/null
    pkill -f "ng serve" 2>/dev/null
    wait 2>/dev/null
    echo "Stopped."
}
trap cleanup INT TERM

echo ""
echo "  API: http://localhost:5045  (log: $API_LOG)"
echo "  UI:  http://localhost:4200  (log: $UI_LOG)"
echo ""
echo "Press Ctrl+C to stop both."

wait -n 2>/dev/null || wait   # if either process dies, shut down the other
cleanup
