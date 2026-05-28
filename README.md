# Company Intranet

Starter intranet with **React** (Vite), **.NET 10 Web API**, and **PostgreSQL**, deployable to **Azure App Service** + **Azure Database for PostgreSQL**.

## Project layout

| Path | Description |
|------|-------------|
| `src/web` | React SPA (Vite + TypeScript) |
| `src/api` | ASP.NET Core 10 API + EF Core |
| `src/api/MultifamilyLbp` | Multifamily lead inspection API (jobs, uploads, normalization, reports) |
| `src/web/src/multifamily-lbp` | Lead inspection React workflow (SharePoint-launched) |
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

| URL | What |
|-----|------|
| http://localhost:5173 | React dev server (proxies `/api` → API) |
| http://localhost:5260 | API directly (`dotnet run` from `src/api`) |
| http://localhost:5260/swagger | **Swagger UI** — browse and try multifamily + intranet endpoints |
| http://localhost:5260/health | Health check (no auth) |
| https://localhost:7055 | API with HTTPS profile |

After `dotnet run`, the **http** profile opens Swagger automatically. Use **Authorize** in Swagger and paste a Bearer token from the SPA (or Entra) to call protected endpoints.

### Multifamily lead inspection (from SharePoint)

The full workflow from `multifamily-lbp` design docs is hosted in this app:

| Route | Purpose |
|-------|---------|
| `/lead-inspection` | Job lookup and recent jobs |
| `/jobs/{jobId}/multifamily-lbp` | Entity dashboard (SharePoint deep link) |
| `/jobs/{jobId}/multifamily-lbp/grid` | Data grid, uploads, normalization, reports |

SPFx web parts in the **multifamily-lbp** repo should set `processingAppBaseUrl` to this intranet URL.

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
| `GET /api/status` | Service + database status (requires Entra token) |
| `GET /api/messages` | Sample messages from PostgreSQL (requires Entra token) |
| `GET /api/me` | Signed-in user claims from Entra token |

## SharePoint SSO with Entra ID

The intranet now expects Microsoft Entra ID authentication for API access.

1. Create two App Registrations in the same tenant as SharePoint:
   - **API app registration** (for `src/api`)
   - **SPA app registration** (for `src/web`)
2. In the API app registration, expose a scope (for example `access_as_user`), producing a scope URI like:
   - `api://<api-client-id>/access_as_user`
3. Grant the SPA app permission to that API scope and grant tenant admin consent.
4. Configure API settings (`src/api/appsettings.Development.json`):
   - `AzureAd:TenantId`
   - `AzureAd:ClientId` (API app client ID)
   - `AzureAd:Audience` (for example `api://<api-client-id>`)
5. Configure SPA settings via environment variables:
   - `VITE_ENTRA_TENANT_ID=<tenant-guid>`
   - `VITE_ENTRA_CLIENT_ID=<spa-client-id>`
   - `VITE_API_SCOPE=api://<api-client-id>/access_as_user`

When users navigate from SharePoint to this intranet in the same tenant, Entra SSO will identify them and `/api/me` returns their user context.

## Azure resources

- Linux App Service Plan (B1) + Web App (.NET 10)
- PostgreSQL Flexible Server 16 (Burstable B1ms)
- Key Vault (connection string secret)

Deployment plan and status: [.azure/deployment-plan.md](.azure/deployment-plan.md).
