# Azure Deployment Plan

> **Status:** Executing

Generated: 2026-05-27

---

## 1. Project Overview

**Goal:** Greenfield company intranet with a React SPA, .NET 10 Web API, and PostgreSQL database, deployed to Azure as an empty starter (health endpoints, placeholder UI, DB connectivity).

**Path:** New Project

---

## 2. Requirements

| Attribute | Value |
|-----------|-------|
| Classification | Development (POC / empty starter) |
| Scale | Small |
| Budget | Cost-Optimized |
| **Subscription** | User Azure account (run `az login`) |
| **Location** | `eastus2` |

---

## 3. Components Detected

| Component | Type | Technology | Path |
|-----------|------|------------|------|
| intranet-web | Frontend | React 19 + Vite + TypeScript | `src/web` |
| intranet-api | API | ASP.NET Core 10 Web API | `src/api` |
| intranet-db | Database | PostgreSQL 16 (Azure Flexible Server) | Azure-managed |

---

## 4. Recipe Selection

**Selected:** Bicep + Azure CLI (`az deployment sub/group create`)

**Rationale:** Empty workspace; no existing AZD project. Azure Developer CLI (`azd`) and Azure CLI are not currently on PATH in the dev environment—infra will be Bicep with a `scripts/deploy.ps1` helper. `azure.yaml` can be added later if the user installs `azd`.

---

## 5. Architecture

**Stack:** App Service (PaaS)

### Service Mapping

| Component | Azure Service | SKU |
|-----------|---------------|-----|
| React SPA (production build) | Served from API App Service `wwwroot` | Included in App Service plan |
| .NET 10 API | Azure App Service (Linux) | B1 (Basic) |
| PostgreSQL | Azure Database for PostgreSQL Flexible Server | Burstable B1ms |
| Secrets | Azure Key Vault | Standard |
| Monitoring | Log Analytics + Application Insights | Pay-as-you-go (dev tier) |

### Supporting Services

| Service | Purpose |
|---------|---------|
| Log Analytics | Centralized logging |
| Application Insights | API monitoring & APM |
| Key Vault | PostgreSQL connection string & app secrets |
| User-assigned Managed Identity | App Service → Key Vault (no secrets in app settings) |

### Design notes

- **Local dev:** Vite dev server on port 5173 proxies `/api` to the API on port 5000; PostgreSQL via Docker Compose or local install.
- **Azure:** Single App Service hosts API + built React static files. API uses EF Core + Npgsql; initial migration creates a minimal `HealthCheck` table.
- **Networking:** PostgreSQL firewall allows Azure services; App Service outbound to DB. Public HTTPS endpoints for the intranet URL (suitable for dev; production would add VNet/private endpoints later).

---

## 6. Provisioning Limit Checklist

> Quota data from [Azure subscription limits](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/azure-subscription-service-limits) (Azure CLI not available in environment at plan time). Re-validate with `az quota` before deploy if needed.

| Resource Type | Number to Deploy | Total After Deployment | Limit/Quota | Notes |
|---------------|------------------|------------------------|-------------|-------|
| Microsoft.Web/serverFarms (Linux B1) | 1 | 1 | 10+ per subscription (typical) | Fetched from: Official docs |
| Microsoft.Web/sites | 1 | 1 | 100 per App Service plan | Fetched from: Official docs |
| Microsoft.DBforPostgreSQL/flexibleServers | 1 | 1 | 50 per region (typical) | Fetched from: Official docs |
| Microsoft.KeyVault/vaults | 1 | 1 | 1000 per region | Fetched from: Official docs |
| Microsoft.OperationalInsights/workspaces | 1 | 1 | 100 per subscription | Fetched from: Official docs |
| Microsoft.Insights/components | 1 | 1 | 100 per subscription | Fetched from: Official docs |
| Microsoft.Storage/storageAccounts | 0 | 0 | N/A | Not required for this stack |

**Status:** ✅ All resources within typical subscription limits (dev scale)

---

## 7. Execution Checklist

### Phase 1: Planning
- [x] Analyze workspace (empty)
- [x] Gather requirements
- [x] Confirm subscription and location with user
- [x] Prepare resource inventory
- [x] Quota validation (documentation-based; CLI pending install)
- [x] Scan codebase (none)
- [x] Select recipe (Bicep)
- [x] Plan architecture
- [x] **User approved this plan**

### Phase 2: Execution
- [x] Scaffold React + .NET API + EF Core
- [x] Add Docker Compose for local PostgreSQL
- [x] Generate Bicep (`infra/main.bicep` + modules)
- [x] Add deploy scripts and README
- [x] Functional verification (local build — API; frontend requires Node.js install)
- [ ] Update plan status to "Ready for Validation"

### Phase 3: Validation
- [ ] Invoke azure-validate skill (after Azure CLI installed)
- [ ] Update plan status to "Validated"

### Phase 4: Deployment
- [ ] Deploy with Azure CLI to user subscription
- [ ] Report deployed URL
- [ ] Update plan status to "Deployed"

---

## 7. Validation Proof

| Check | Command Run | Result | Timestamp |
|-------|-------------|--------|-----------|
| _Pending_ | _azure-validate_ | _Pending_ | _Pending_ |

---

## 8. Files to Generate

| File | Purpose | Status |
|------|---------|--------|
| `.azure/deployment-plan.md` | This plan | ✅ |
| `src/api/` | .NET 10 Web API | ⏳ |
| `src/web/` | React Vite app | ⏳ |
| `infra/main.bicep` | Azure infrastructure | ⏳ |
| `infra/modules/*.bicep` | Modular resources | ⏳ |
| `docker-compose.yml` | Local PostgreSQL | ⏳ |
| `scripts/deploy.ps1` | Deploy helper | ⏳ |
| `README.md` | Setup & deploy instructions | ⏳ |

---

## 9. Next Steps

> Current: Awaiting user approval (subscription, region, architecture)

1. User confirms subscription, Azure region, and approves this plan.
2. Install Azure CLI if missing (`winget install Microsoft.AzureCLI`).
3. Scaffold application and Bicep; run local verification.
4. `az login` → deploy infrastructure → publish API + frontend.

**Estimated monthly cost (dev):** ~$25–45 USD (B1 App Service + B1ms PostgreSQL + monitoring).
