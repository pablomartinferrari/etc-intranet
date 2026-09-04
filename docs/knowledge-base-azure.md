# Knowledge Base — deploy to Azure (real-world test)

This runbook deploys the **data plane** (Postgres + pgvector + Blob) and wires your existing **intranet App Service** to a **GPU VM running Ollama** plus an **ingestion worker** for etc-kg.

## Architecture on Azure

```text
Users → App Service (intranet API + React)
           ├─ JWT / Entra (unchanged)
           ├─ KnowledgeDb → Azure PostgreSQL Flexible (pgvector)
           └─ Ollama HTTP → GPU VM (private IP)

GPU VM (same machine for pilot)
  ├─ Ollama (embed + chat)
  └─ etc-kg worker (ingest uploads / SharePoint → Blob → Postgres)

Blob Storage → raw files (574 MB+ belongs here, not on App Service disk)
```

**Important:** The intranet **upload API** shells out to Python. App Service does not ship with etc-kg. For production-like tests, run **ingestion on the GPU VM** (or a Container Apps Job) and use the intranet UI only for **chat/search** against already-indexed docs—or upload small files for quick tests.

---

## Why your 574 MB upload is slow

| Factor | Effect |
|--------|--------|
| File size | Hundreds of thousands of chunks possible |
| CPU Ollama | ~1–5+ seconds **per chunk** for embeddings |
| Synchronous upload | Browser waits until **entire** ingest finishes |
| App Service | **~230 min** max HTTP timeout; large jobs should be **async** |

**Pilot guidance:** Test with **5–50 MB** PDFs on Azure first. For 500 MB+, use the VM worker (background), not the browser upload path.

---

## Prerequisites

- Intranet already deployed: `./scripts/deploy.ps1 -ResourceGroup rg-intranet-dev -Location eastus2`
- Azure CLI logged in: `az login`
- Quota for **GPU VM** (e.g. `Standard_NC4as_T4_v3`) in your region
- Entra app registrations configured (same as local)
- For **project sharing** (users and groups), grant the API app these **Microsoft Graph application permissions** with admin consent:
  - `User.Read.All` — directory user search (`GET /api/kb/directory/search`)
  - `Group.Read.All` — directory group search
  - `GroupMember.Read.All` — `checkMemberGroups` so group-shared projects appear for members
  (`Directory.Read.All` covers the same reads if you prefer a single role.)

---

## Step 1 — Deploy Postgres + Blob (~10 min)

```powershell
cd C:\dev\etc\intranet

# Optional: fix password for knowledge DB
$env:KNOWLEDGE_POSTGRES_PASSWORD = "YourSecureKgPassword123!"

./scripts/deploy-knowledge-azure.ps1 `
  -ResourceGroup rg-intranet-dev `
  -Location eastus2 `
  -NamePrefix etc `
  -SkipAppSettings
```

Save outputs from `.azure/knowledge-connection.txt`.

### Initialize database

1. Allow your laptop IP on the flexible server (Portal → Networking, or CLI).
2. Connect with `psql` or Azure Data Studio:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

3. Apply schema:

```powershell
psql "Host=<fqdn>;Port=5432;Database=knowledge;Username=kgadmin;Password=<pwd>;SSL Mode=Require" `
  -f C:\dev\etc\etc-kg\migrations\001_initial.sql
```

The Bicep template sets `azure.extensions = VECTOR` on the server; extension create may already work after deploy.

---

## Step 2 — GPU VM for Ollama + ingestion (~30 min)

### Create VM (Portal or CLI)

| Setting | Recommendation |
|---------|----------------|
| Size | `Standard_NC4as_T4_v3` or `Standard_NV6ads_A10_v5` (GPU) |
| OS | Ubuntu 22.04 LTS |
| Disk | 128 GB+ OS (models only; data goes to Blob) |
| Networking | Same region as App Service (`eastus2`) |

### Install on the VM (SSH)

```bash
# Ollama
curl -fsSL https://ollama.com/install.sh | sh
ollama pull nomic-embed-text
ollama pull llama3.1:8b

# Python / etc-kg
sudo apt update && sudo apt install -y python3.12 python3.12-venv git
git clone <your-repo> /opt/etc-kg   # or scp C:\dev\etc\etc-kg
cd /opt/etc-kg
python3.12 -m venv .venv
.venv/bin/pip install -e .

# Config
cp config/.env.example config/.env
# Edit:
#   KNOWLEDGE_DB_CONNECTION=Host=<azure-pg-fqdn>;...
#   OLLAMA_BASE_URL=http://127.0.0.1:11434
#   STORAGE_BACKEND=azure_blob
#   AZURE_STORAGE_CONNECTION_STRING=<from deploy output>
```

Open port **11434** only to App Service (VNet private IP preferred; for a quick test, restrict NSG to App Service outbound IPs—less secure).

Note the VM **private IP** (e.g. `10.0.1.4`).

---

## Step 3 — Wire App Service (~5 min)

```powershell
./scripts/deploy-knowledge-azure.ps1 `
  -ResourceGroup rg-intranet-dev `
  -WebAppName <your-intranet-api-name> `
  -OllamaBaseUrl "http://10.0.1.4:11434"
```

Redeploy intranet if needed (includes Knowledge Base UI):

```powershell
./scripts/deploy-app.ps1 -ResourceGroup rg-intranet-dev -BuildFrontend
```

### App Service → VM connectivity

For a **real** test, use one of:

1. **VNet integration** (recommended): App Service integrated subnet can reach VM private IP.
2. **Public IP on VM** + NSG allowlist (pilot only): faster to set up, not for production.

If chat returns connection errors to Ollama, this link is broken—not Entra.

---

## Step 4 — Ingest documents on Azure

### Small files (quick test)

Upload via intranet **Knowledge assistant** UI (files &lt; ~50 MB).

### Large files / SharePoint (574 MB class)

On the **GPU VM**:

```bash
cd /opt/etc-kg
# Single large file — expect hours; run in tmux/screen
.venv/bin/python -m ingest.cli ingest local --path /data/uploads/bigfile.pdf

# Or folder
.venv/bin/python -m ingest.cli ingest local --path /data/uploads

.venv/bin/python -m ingest.cli status
```

Copy files to VM with `azcopy` from Blob or SharePoint sync.

### SharePoint (when ready)

```bash
# Install optional deps on VM (enable Windows long paths if building from Windows)
.venv/bin/pip install -e ".[sharepoint]"
export AZURE_TENANT_ID=...
export AZURE_CLIENT_ID=...
export AZURE_CLIENT_SECRET=...
export SHAREPOINT_SITE_URL=...

.venv/bin/python -m ingest.cli ingest sharepoint --site "$SHAREPOINT_SITE_URL" --folder "Documents/..."
```

---

## Step 5 — Verify end-to-end

1. Open `https://<your-intranet-app>/knowledge`
2. Sign in with Entra
3. Confirm documents show **completed** in sidebar (after VM ingest)
4. Ask a question in chat

Check App Service logs if chat fails:

```powershell
az webapp log tail --resource-group rg-intranet-dev --name <webapp-name>
```

---

## Cost rough estimate (dev test)

| Resource | ~Monthly |
|----------|----------|
| Existing App Service B1 + intranet Postgres | $20–35 |
| Knowledge Postgres B2s + 64 GB | $25–40 |
| Blob (100 GB) | $2–5 |
| GPU VM NC4as_T4_v3 (only when running) | $400–700+ |

**Save money:** Stop/deallocate GPU VM when not testing; keep Postgres + Blob.

---

## Configuration reference

| Setting | Where |
|---------|--------|
| `ConnectionStrings__KnowledgeDb` | App Service |
| `KnowledgeBase__OllamaBaseUrl` | App Service → VM |
| `KnowledgeBase__Fallback__ApiKey` | App Service or Key Vault — activates hosted chat **and Help AI** when the GPU VM is down |
| `KnowledgeBase__Fallback__BaseUrl` | Optional; default `https://api.openai.com/v1` (Azure OpenAI resource URL also works) |
| `KnowledgeBase__Fallback__Model` | Optional; default `gpt-4o-mini` |
| `KNOWLEDGE_DB_CONNECTION` | etc-kg on VM |
| `AZURE_STORAGE_CONNECTION_STRING` | etc-kg on VM |
| `STORAGE_BACKEND=azure_blob` | etc-kg on VM |

---

## Next improvements (before 1.5 TB production)

1. **Async ingest** — upload to Blob → queue job → poll status in UI (no 574 MB HTTP hang)
2. **File size limit** in UI with message “use VM/SharePoint path for large files”
3. **VNet** — App Service + VM + Postgres private endpoints
4. **Separate ingestion scale set** — multiple workers on GPU SKU

---

## Troubleshooting

| Symptom | Check |
|---------|--------|
| 401 on `/api/kb/*` | Entra + `appsettings.Development.json` / Production AzureAd (tenant GUID, API app id) |
| Chat timeout | Ollama URL from App Service; GPU loaded; smaller question |
| Upload never finishes | File too large for sync path; use VM ingest |
| `IDX40001` issuer | Wrong `AzureAd:TenantId` on API |
| Python not found on App Service | Expected — run ingest on VM |
