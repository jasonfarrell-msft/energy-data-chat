#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────
# Deploy frontend to Azure App Service
# Uses the current logged-in az CLI user
# ──────────────────────────────────────────────

RESOURCE_GROUP="rg-pseg-energy-chat-eus2-mx01"
APP_NAME="app-pseg-energychat-eus2-mx01"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "==> Installing dependencies..."
npm ci

echo "==> Building frontend..."
npm run build

echo "==> Packaging dist for deployment..."
cd dist
zip -r /tmp/frontend-deploy.zip .
cd ..

echo "==> Deploying to App Service: $APP_NAME..."
az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --src-path /tmp/frontend-deploy.zip \
  --type zip \
  --async true

rm -f /tmp/frontend-deploy.zip

echo "==> Deployment complete!"
echo "    https://${APP_NAME}.azurewebsites.net"
