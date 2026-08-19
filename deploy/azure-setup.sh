#!/usr/bin/env bash
# Bootstraps a fresh Ubuntu Azure VM for CodeForge.
# Installs: .NET 8 SDK, Docker, Node 22, pyright, Caddy.
# Clones the repo, builds Docker images, deploys backend + frontend.
#
# Usage (on the Azure VM, as root or with sudo):
#   sudo bash azure-setup.sh
#
# Prerequisites you must do in the Azure portal first:
#   1. NSG inbound rules: allow 22 (SSH), 80 (HTTP), 443 (HTTPS)
#   2. Azure SQL firewall: add this VM's public IP
#
# After this script completes:
#   3. Point coderunner.duckdns.org at this VM's public IP
#   4. TLS cert provisions automatically on first HTTPS request
set -euo pipefail

REPO=https://github.com/jayc1323/codeforge.git
APP_ROOT=/root/codeforge

echo "==> System packages"
apt-get update -qq
apt-get install -y -qq git curl ufw

echo "==> .NET 8 SDK"
apt-get install -y -qq dotnet-sdk-8.0

echo "==> Docker"
apt-get install -y -qq docker.io
systemctl enable --now docker

echo "==> Node 22"
curl -fsSL https://deb.nodesource.com/setup_22.x | bash - > /dev/null
apt-get install -y -qq nodejs

echo "==> pyright (Python language server)"
npm install -g pyright --silent

echo "==> Caddy"
apt-get install -y -qq debian-keyring debian-archive-keyring apt-transport-https gpg
curl -fsSL 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg --yes
curl -fsSL 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' > /etc/apt/sources.list.d/caddy-stable.list
apt-get update -qq
apt-get install -y -qq caddy

echo "==> Clone repo"
if [ -d "$APP_ROOT" ]; then
    git -C "$APP_ROOT" pull --ff-only
else
    git clone "$REPO" "$APP_ROOT"
fi

echo "==> Secrets"
mkdir -p "$APP_ROOT/.secrets"
if [ ! -f "$APP_ROOT/.secrets/env" ]; then
    echo ""
    echo "ACTION REQUIRED: create $APP_ROOT/.secrets/env with:"
    echo "  ConnectionStrings__CodeForge=<your Azure SQL connection string>"
    echo "  Jwt__SigningKey=<random 48+ char string>"
    echo "  Jwt__Issuer=codeforge"
    echo "  Jwt__Audience=codeforge"
    echo ""
    echo "Generate a JWT key with:  python3 -c 'import secrets; print(secrets.token_urlsafe(48))'"
    echo "Then re-run this script."
    exit 1
fi
chmod 600 "$APP_ROOT/.secrets/env"

echo "==> Docker images (language toolchains)"
docker pull python:3.12-slim
docker pull gcc:13
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker build -t codeforge-typescript "$APP_ROOT/docker/typescript"

echo "==> Firewall (ufw): 22/80/443 only"
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

echo "==> Deploy backend"
"$APP_ROOT/deploy/deploy-backend.sh"

echo "==> Deploy frontend"
"$APP_ROOT/deploy/deploy-frontend.sh"

echo "==> Caddy config"
cp "$APP_ROOT/deploy/Caddyfile" /etc/caddy/Caddyfile
systemctl reload caddy

echo ""
echo "Done. Verify:"
echo "  curl -s http://localhost:5045/api/languages   # backend"
echo "  curl -s http://localhost                      # frontend via Caddy"
echo ""
echo "Then point coderunner.duckdns.org at this VM's public IP."
echo "Caddy will fetch the TLS cert automatically once DNS resolves."
