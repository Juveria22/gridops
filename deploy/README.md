# Deploying to Azure (free tier)

> `azure-setup.sh` hasn't been run against a real subscription yet. Run it step by step the first time.

| | Service | Tier |
|---|---|---|
| API + Angular | App Service (Linux) | F1 free. 60 CPU min/day, sleeps after ~20 min idle |
| Database | Azure SQL Database | free offer. 100k vCore-seconds + 32 GB/month, auto-pauses at the limit instead of billing |
| CI/CD | GitHub Actions | free for public repos |

Cold start after idle: first request can take 30-60s (app wake + SQL resume). Open the site a minute before showing it.

## One-time setup

1. **Sign in**
   ```bash
   az login
   ```

2. **Create resources**
   ```bash
   ./deploy/azure-setup.sh
   ```
   Resource group, SQL server + free database, F1 plan + web app, app settings, and a GitHub OIDC identity scoped to the resource group. Writes the values GitHub needs to `deploy/azure.env` (gitignored).

3. **Add to GitHub** (repo -> Settings -> Secrets and variables -> Actions)
   - secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_SQL_CONNECTION_STRING`
   - variables: `AZURE_RESOURCE_GROUP`, `AZURE_SQL_SERVER`, `AZURE_WEBAPP_NAME`, `AZURE_DEPLOY_ENABLED=true`

   Then delete `deploy/azure.env`.

4. **Deploy**: push to `main` (or Actions -> CI/CD -> Run workflow). Pipeline: tests -> build -> migrate -> deploy -> health check.

5. **Seed demo data** (once). Seeder is Development-only, so run it from your machine with your IP allowed:
   ```bash
   ip=$(curl -s https://api.ipify.org)
   az sql server firewall-rule create -g rg-gridops -s <sql-server> -n seed --start-ip-address $ip --end-ip-address $ip
   dotnet run --project api -- seed "--ConnectionStrings:GridOps=<AZURE_SQL_CONNECTION_STRING>"
   az sql server firewall-rule delete -g rg-gridops -s <sql-server> -n seed
   ```

6. **Budget alert**: Portal -> Cost Management -> Budgets -> $1/month with an email alert. Free, catches anything unexpected.

## Remove everything

```bash
az group delete -n rg-gridops --yes
az ad app delete --id <AZURE_CLIENT_ID>
```

## How the pipeline deploys

`.github/workflows/ci-cd.yml`

- `api`: build + 53 tests (SQL Server via Testcontainers)
- `client`: `ng build --configuration production`
- `package`: `dotnet publish` + Angular build into `wwwroot`, EF migrations bundle
- `deploy` (only when `AZURE_DEPLOY_ENABLED=true`): OIDC login, open SQL firewall to the runner, apply migrations, close firewall, deploy, `/health` check

Migrations run in the pipeline, not at app startup: one controlled step, a failed migration stops the deploy, and multiple instances can't race.
