#!/usr/bin/env bash
# One-time Azure setup for GridOps. Free tiers only:
#   App Service F1 (Linux) - API + Angular
#   Azure SQL Database free offer - pauses instead of billing when the monthly free amount runs out
# Needs: az login first. Usage: ./deploy/azure-setup.sh
# Writes secrets to deploy/azure.env (gitignored) - copy them into GitHub, then delete the file.
set -euo pipefail

LOCATION="${LOCATION:-eastus2}"
RG="${RG:-rg-gridops}"
REPO="${REPO:-Juveria22/gridops}"
SUFFIX="${SUFFIX:-$(openssl rand -hex 3)}" # server + app names must be globally unique

SQL_SERVER="gridops-sql-$SUFFIX"
SQL_DB="gridops"
SQL_ADMIN="gridopsadmin"
SQL_PASSWORD="$(openssl rand -base64 24 | tr -d '/+=')Aa1!"
PLAN="plan-gridops"
WEBAPP="gridops-$SUFFIX"
JWT_KEY="$(openssl rand -base64 48)"

step() { printf '\n== %s\n' "$*"; }

step "subscription"
az account show --query "{name:name, id:id}" -o table
SUB_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

step "register providers (new subscriptions need this once)"
az provider register -n Microsoft.Web --wait
az provider register -n Microsoft.Sql --wait

step "resource group $RG ($LOCATION) - delete this one group to remove everything"
az group create -n "$RG" -l "$LOCATION" -o none

step "SQL server $SQL_SERVER"
az sql server create -g "$RG" -n "$SQL_SERVER" -l "$LOCATION" -u "$SQL_ADMIN" -p "$SQL_PASSWORD" -o none
# 0.0.0.0 = allow Azure services (App Service -> SQL). no public IPs
az sql server firewall-rule create -g "$RG" -s "$SQL_SERVER" -n AllowAzureServices \
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 -o none

step "SQL database $SQL_DB (free offer, auto-pause on free limit)"
az sql db create -g "$RG" -s "$SQL_SERVER" -n "$SQL_DB" \
  --edition GeneralPurpose --compute-model Serverless --family Gen5 --capacity 2 \
  --use-free-limit --free-limit-exhaustion-behavior AutoPause \
  --backup-storage-redundancy Local -o none

CONN="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=$SQL_DB;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"

step "App Service plan F1 + web app $WEBAPP"
az webapp list-runtimes --os linux -o tsv | grep -q "DOTNETCORE:10.0" \
  || { echo "DOTNETCORE:10.0 not offered on Linux App Service in this region/CLI"; exit 1; }
az appservice plan create -g "$RG" -n "$PLAN" --is-linux --sku F1 -o none
az webapp create -g "$RG" -p "$PLAN" -n "$WEBAPP" --runtime "DOTNETCORE:10.0" -o none
az webapp update -g "$RG" -n "$WEBAPP" --https-only true -o none

step "app settings (become env vars -> IConfiguration, same as user secrets locally)"
az webapp config appsettings set -g "$RG" -n "$WEBAPP" -o none --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  Jwt__SigningKey="$JWT_KEY"
az webapp config connection-string set -g "$RG" -n "$WEBAPP" -t SQLAzure -o none --settings GridOps="$CONN"

step "GitHub OIDC identity (no stored password in GitHub)"
APP_ID=$(az ad app create --display-name "gh-gridops-deploy" --query appId -o tsv)
az ad sp create --id "$APP_ID" -o none
# token is only accepted from this repo's "production" environment
az ad app federated-credential create --id "$APP_ID" -o none --parameters "{
  \"name\": \"gh-production\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:$REPO:environment:production\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}"
# least privilege: this resource group only
az role assignment create --assignee "$APP_ID" --role Contributor \
  --scope "/subscriptions/$SUB_ID/resourceGroups/$RG" -o none

OUT="$(dirname "$0")/azure.env"
cat > "$OUT" <<EOF
# GitHub repo -> Settings -> Secrets and variables -> Actions
# --- secrets ---
AZURE_CLIENT_ID=$APP_ID
AZURE_TENANT_ID=$TENANT_ID
AZURE_SUBSCRIPTION_ID=$SUB_ID
AZURE_SQL_CONNECTION_STRING=$CONN
# --- variables ---
AZURE_RESOURCE_GROUP=$RG
AZURE_SQL_SERVER=$SQL_SERVER
AZURE_WEBAPP_NAME=$WEBAPP
AZURE_DEPLOY_ENABLED=true
EOF

step "done"
echo "App URL: https://$WEBAPP.azurewebsites.net"
echo "Values for GitHub written to $OUT (gitignored). Copy them over, then delete the file."
