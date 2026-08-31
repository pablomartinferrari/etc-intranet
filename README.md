# Company Intranet

Starter intranet with **React** (Vite), **.NET 10 Web API**, and **PostgreSQL**, deployable to **Azure App Service** + **Azure Database for PostgreSQL**.

Staff can open **Opportunities** in the SPA to review CLEATUS-recommended government-contract bids (SAM.gov / SLED) without signing into CLEATUS first. **Pipeline** lists pursuits already on the board, highlights work that needs close-out, and stores win/loss/drop reasons in PostgreSQL.

## Project layout

| Path | Description |
|------|-------------|
| `src/web` | React SPA (Vite + TypeScript) |
| `src/api` | ASP.NET Core 10 API + EF Core |
| `infra/` | Azure Bicep templates |
| `scripts/deploy.ps1` | Deploy infrastructure + API to Azure |
| `docker-compose.yml` | Local PostgreSQL |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) (for frontend dev/build)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (local database)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (`winget install -e --id Microsoft.AzureCLI`)

## Local development

1. Start PostgreSQL:

   ```powershell
   docker compose up -d
   ```

2. Apply migrations and run the API:

   ```powershell
   cd src/api
   dotnet ef database update
   dotnet run
   ```

3. In another terminal, run the React dev server:

   ```powershell
   cd src/web
   npm install
   npm run dev
   ```

   If npm warns about `esbuild` install scripts, either run `npm approve-scripts esbuild` or rely on the `allowScripts` entry already in `src/web/package.json`.

Open http://localhost:5173 (Vite proxies `/api` to the API).

The API compiles and runs **without** a CLEATUS key. Home works as before. The Opportunities and Pipeline pages call CLEATUS on load and show a friendly “Add Cleat__ApiKey” message (HTTP 503) until a key is configured. Close-out writes with a missing key save the reason locally, return 503, and do not pretend CLEATUS was updated.

### CLEATUS API key (optional until you want live recommendations)

Mint a key in CLEATUS: **Settings → Integrations → API Keys**. Store it outside git.

**Local (user secrets — preferred):**

```powershell
cd src/api
dotnet user-secrets set "Cleat:ApiKey" "<your-cleatus-api-key>"
```

Equivalent environment variable: `Cleat__ApiKey`. Do not put a real key in `appsettings.json`, README examples, or source control.

**Azure App Service setting:**

```powershell
az webapp config appsettings set `
  --resource-group rg-intranet-dev `
  --name "<web-app-name>" `
  --settings Cleat__ApiKey="<your-cleatus-api-key>"
```

**Azure Key Vault** (same pattern as the PostgreSQL connection string). Create a secret (name `Cleat--ApiKey` is a valid Key Vault encoding of `Cleat:ApiKey`) and wire it as an App Setting Key Vault reference:

```powershell
az keyvault secret set `
  --vault-name "<key-vault-name>" `
  --name "Cleat--ApiKey" `
  --value "<your-cleatus-api-key>"

az webapp config appsettings set `
  --resource-group rg-intranet-dev `
  --name "<web-app-name>" `
  --settings Cleat__ApiKey="@Microsoft.KeyVault(SecretUri=https://<key-vault-name>.vault.azure.net/secrets/Cleat--ApiKey/)"
```

The App Service system-assigned identity already has Key Vault Secrets User. Restart the web app after adding the secret. `Cleat:BaseUrl` defaults to `https://api.cleat.ai` and does not need to be set.

## Build for production

```powershell
cd src/web
npm install
npm run build
```

This writes static files to `src/api/wwwroot`. Then publish the API:

```powershell
cd src/api
dotnet publish -c Release
```

## Deploy to Azure (eastus2)

1. Sign in:

   ```powershell
   az login
   az account set --subscription "<your-subscription>"
   ```

2. Set a strong PostgreSQL password (or let the script generate one):

   ```powershell
   $env:POSTGRES_ADMIN_PASSWORD = "YourSecurePassword123!"
   ```

3. Deploy:

   ```powershell
   ./scripts/deploy.ps1 -ResourceGroup rg-intranet-dev -Location eastus2
   ```

The script creates the resource group (if needed), deploys Bicep (App Service, PostgreSQL, Key Vault), publishes the API, and prints the site URL.

### Partial deploy scripts

Use these when only part of the system changed:

- **Infra / DB server changes (Bicep):**
  ```powershell
  ./scripts/deploy-infra.ps1 -ResourceGroup rg-intranet-dev -Location eastus2
  ```
- **API-only changes:**
  ```powershell
  ./scripts/deploy-app.ps1 -ResourceGroup rg-intranet-dev
  ```
- **UI changes (and API package deploy):**
  ```powershell
  ./scripts/deploy-app.ps1 -ResourceGroup rg-intranet-dev -BuildFrontend
  ```

For EF Core schema changes in code-first migrations, run the API deploy script (`deploy-app.ps1`) so the app starts and runs `Database.Migrate()` on startup.

If a previous deploy left resources in `eastus` and you need `eastus2` for PostgreSQL, do a clean redeploy:

```powershell
./scripts/deploy.ps1 -ResourceGroup rg-intranet-dev -Location eastus2 -FreshStart
```

`-FreshStart` deletes the resource group and recreates it in `eastus2` so names and regions stay consistent.

### Estimated dev cost

Roughly **$20–35/month** (B1 App Service + B1ms PostgreSQL, no App Insights/Log Analytics).

## API endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /health` | Health check (includes DB) |
| `GET /api/status` | Service + database status |
| `GET /api/messages` | Sample messages from PostgreSQL |
| `GET /api/cleat/recommendations` | Proxy of CLEATUS `GET /v1/recommendations` (optional `minScore`, default 80). Missing `Cleat__ApiKey` returns **503** JSON `{ error: "cleat_api_key_missing", message: "Add Cleat__ApiKey ..." }`. |
| `GET /api/cleat/opportunities/{id}` | Proxy of CLEATUS `GET /v1/opportunities/{opportunity_id}`. Same missing-key 503. |
| `GET /api/cleat/pipeline` | Page-load pipeline dashboard: `GET /v1/pipeline/search` (active, won, archived) joined with opportunity deadlines when missing, plus local close-out reasons. Same missing-key 503. |
| `POST /api/cleat/pursuits/{id}/close-out` | Persist a win/loss/drop reason in PostgreSQL, then `PATCH /v1/pursuits/{id}` (`column_id` Won/Lost, or `archived: true` for no longer pursuing). If the CLEATUS write fails, the local reason is kept and the response is 503/502 with `cleatusUpdated: false`. |

Recommendations are not stored in Postgres. **Close-out reasons are** (table `PursuitCloseouts`) because CLEATUS has no documented win/loss-reason field. Do not put reasons in git or in CLEATUS tags.

### Pipeline close-out rules

A pursuit **needs close-out** when it is not won, lost, or archived, and any of:

1. Opportunity response/deadline is in the past
2. Phase is triage / preparing / submitted and last-activity is 21+ days old, **if** CLEATUS sends an updated-at / last-activity field
3. If there is no last-activity field (current public OpenAPI/Zapier payloads do not document one), use deadline-only, and flag **no deadline on file** so those rows still appear

No email is sent; visibility on the Pipeline page is enough.

## Azure resources

- Linux App Service Plan (B1) + Web App (.NET 10)
- PostgreSQL Flexible Server 16 (Burstable B1ms)
- Key Vault (connection string secret)

Deployment plan and status: [.azure/deployment-plan.md](.azure/deployment-plan.md).
