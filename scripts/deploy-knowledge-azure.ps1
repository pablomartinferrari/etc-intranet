#Requires -Version 7.0
<#
.SYNOPSIS
  Deploy Azure resources for the knowledge base (Postgres + pgvector + Blob) and wire App Service settings.

.PARAMETER ResourceGroup
  Existing intranet resource group (same as deploy.ps1).

.PARAMETER WebAppName
  Intranet App Service name. If omitted, reads .azure/last-deployment.json from deploy.ps1.

.PARAMETER OllamaBaseUrl
  Ollama URL reachable from App Service, e.g. http://10.0.1.4:11434 after GPU VM is on the VNet.

.PARAMETER SkipAppSettings
  Only deploy infra; do not update App Service configuration.

.EXAMPLE
  ./scripts/deploy-knowledge-azure.ps1 -ResourceGroup rg-intranet-dev -OllamaBaseUrl "http://10.0.1.4:11434"
#>
param(
    [string]$ResourceGroup = "rg-intranet-dev",
    [string]$Location = "eastus2",
    [string]$NamePrefix = "etc",
    [string]$KnowledgeAdminLogin = "kgadmin",
    [SecureString]$KnowledgePostgresPassword,
    [string]$WebAppName = "",
    [string]$OllamaBaseUrl = "",
    [switch]$SkipAppSettings
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Get-KnowledgePassword {
    if ($null -ne $KnowledgePostgresPassword) {
        $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($KnowledgePostgresPassword)
        try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    }
    if ($env:KNOWLEDGE_POSTGRES_PASSWORD) { return $env:KNOWLEDGE_POSTGRES_PASSWORD }
    return -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
}

function Get-WebAppName {
    param([string]$Name)
    if ($Name) { return $Name }
    $last = Join-Path $root ".azure/last-deployment.json"
    if (Test-Path $last) {
        $deployment = Get-Content $last -Raw | ConvertFrom-Json
        $n = $deployment.properties.outputs.webAppName.value
        if ($n) { return $n }
    }
    throw "WebAppName not provided and .azure/last-deployment.json missing. Run ./scripts/deploy.ps1 first or pass -WebAppName."
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI required. Install: winget install -e --id Microsoft.AzureCLI"
}

$kgPassword = Get-KnowledgePassword
$appName = Get-WebAppName -Name $WebAppName

Write-Host "Deploying knowledge-base infrastructure to $ResourceGroup ($Location)..."
$deployJson = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file "$root/infra/modules/knowledge-base.bicep" `
    --parameters namePrefix=$NamePrefix location=$Location postgresAdminLogin=$KnowledgeAdminLogin postgresAdminPassword=$kgPassword `
    --output json

if ($LASTEXITCODE -ne 0) { throw "Knowledge infrastructure deployment failed." }

$deployment = $deployJson | ConvertFrom-Json
$kgConn = $deployment.properties.outputs.knowledgePostgresConnectionString.value
$blobConn = $deployment.properties.outputs.knowledgeStorageConnectionString.value
$pgFqdn = $deployment.properties.outputs.knowledgePostgresFqdn.value

$kgConn | Out-File "$root/.azure/knowledge-connection.txt" -Encoding utf8 -NoNewline
$blobConn | Out-File "$root/.azure/knowledge-blob-connection.txt" -Encoding utf8 -NoNewline
Write-Host "Saved connection string to .azure/knowledge-connection.txt (do not commit)"
Write-Host "Saved blob connection string to .azure/knowledge-blob-connection.txt (do not commit)"

Write-Host ""
Write-Host "=== Postgres ($pgFqdn) ==="
Write-Host "1. Allow your IP in firewall if connecting from laptop:"
Write-Host "   az postgres flexible-server firewall-rule create -g $ResourceGroup -n <server-name> -r AllowMyIp --start-ip-address <your-ip> --end-ip-address <your-ip>"
Write-Host "2. Connect and run:"
Write-Host "   CREATE EXTENSION IF NOT EXISTS vector;"
Write-Host "3. Apply schema:"
Write-Host "   psql `"<connection-string>`" -f C:\dev\etc\etc-kg\migrations\001_initial.sql"

if (-not $SkipAppSettings) {
    if (-not $OllamaBaseUrl) {
        Write-Warning "OllamaBaseUrl not set. Configure after GPU VM is ready (see docs/knowledge-base-azure.md)."
        $OllamaBaseUrl = "http://REPLACE-WITH-VM-IP:11434"
    }

    Write-Host ""
    Write-Host "Updating App Service settings on $appName ..."
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $appName `
        --settings `
            "ConnectionStrings__KnowledgeDb=$kgConn" `
            "KnowledgeBase__ConnectionString=$kgConn" `
            "KnowledgeBase__OllamaBaseUrl=$OllamaBaseUrl" `
            "KnowledgeBase__AzureStorageConnectionString=$blobConn" `
            "KnowledgeBase__AzureStorageContainer=knowledge-raw" `
            "KnowledgeBase__MigrationSqlPath=migrations" `
        --output none

    Write-Host "Note: Run the ingest worker on the GPU VM (see scripts/setup-knowledge-worker-vm.sh)."
}

Write-Host ""
Write-Host "Knowledge Azure deploy complete."
Write-Host "Full runbook: docs/knowledge-base-azure.md"
