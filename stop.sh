#!/usr/bin/env bash
# Stops everything CodeForge-related on this droplet:
#   1. systemd services (codeforge-api, codeforge-ui)
#   2. stray dev processes (dotnet run / ng serve started by run.sh)
#   3. pyright language server processes (LSP bridge children)
#   4. leftover execution containers (named codeforge-*)
# Usage: ./stop.sh
set -uo pipefail

echo "Stopping systemd services..."
systemctl stop codeforge-api codeforge-ui 2>/dev/null \
    && echo "  services stopped" \
    || echo "  services were not running"

echo "Killing stray dev processes..."
pkill -f "CodeForge.Api" 2>/dev/null && echo "  killed dotnet (CodeForge.Api)" || true
pkill -f "ng serve" 2>/dev/null && echo "  killed ng serve" || true
pkill -f "pyright-langserver" 2>/dev/null && echo "  killed pyright-langserver" || true

echo "Removing leftover execution containers..."
CONTAINERS=$(docker ps -aq --filter "name=^codeforge-" 2>/dev/null || true)
if [ -n "$CONTAINERS" ]; then
    docker rm -f $CONTAINERS > /dev/null 2>&1
    echo "  removed: $(echo "$CONTAINERS" | wc -l) container(s)"
else
    echo "  none"
fi

echo ""
echo "Remaining CodeForge processes:"
ps aux | grep -E 'CodeForge|ng serve|pyright' | grep -v grep || echo "  (none — all clean)"
