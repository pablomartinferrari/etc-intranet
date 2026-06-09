#Requires -Version 7.0
<#
.SYNOPSIS
  Build etc-kg config/.env for the GPU VM and copy it over SSH (no nano required).

.PARAMETER VmHost
  Public IP or DNS of the GPU VM, e.g. 20.127.81.213

.PARAMETER UseAppServiceSettings
  Read Postgres + blob connection strings from App Service (same as the live API).
  Recommended — keeps VM in sync with Portal settings.

.EXAMPLE
  ./scripts/push-knowledge-worker-env.ps1 -VmHost 20.127.81.213 -UseAppServiceSettings
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$VmHost,
    [string]$VmUser = "azureuser",
    [string]$EtcKgRemotePath = "~/etc-kg",
    [string]$ResourceGroup = "rg-intranet-dev",
    [string]$WebAppName = "",
    [switch]$UseAppServiceSettings
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Convert-NpgsqlConnectionToPsycopg {
    param([string]$Conn)

    $Conn = $Conn.Trim()
    if ($Conn -match '^postgres(ql)?://') { return $Conn }

    $parts = @{}
    foreach ($segment in $Conn.Split(';')) {
        $segment = $segment.Trim()
        if (-not $segment -or $segment -notmatch '=') { continue }
        $eq = $segment.IndexOf('=')
        $key = $segment.Substring(0, $eq).Trim().ToLower()
        $value = $segment.Substring($eq + 1).Trim()

        switch ($key) {
            'host' { $parts.host = $value }
            'port' { $parts.port = $value }
            'database' { $parts.dbname = $value }
            'username' { $parts.user = $value }
            'password' { $parts.password = $value }
            'ssl mode' { $parts.sslmode = ($value -replace '\s', '').ToLower() }
            'trust server certificate' {
                if ($value -match '^(?i:true|yes|1)$') { $parts.sslmode = 'require' }
            }
        }
    }

    if (-not $parts.sslmode) { $parts.sslmode = 'require' }
    if (-not $parts.port) { $parts.port = '5432' }
    if (-not $parts.dbname) { $parts.dbname = 'knowledge' }

    if ($parts.user -and $parts.password -and $parts.host) {
        $user = [uri]::EscapeDataString($parts.user)
        $pass = [uri]::EscapeDataString($parts.password)
        $db = [uri]::EscapeDataString($parts.dbname)
        return "postgresql://${user}:${pass}@$($parts.host):$($parts.port)/${db}?sslmode=$($parts.sslmode)"
    }

    return ($parts.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ' '
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
    throw "WebAppName not provided and .azure/last-deployment.json missing."
}

function Get-AppSetting {
    param([string]$AppName, [string]$SettingName)
    $value = az webapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $AppName `
        --query "[?name=='$SettingName'].value | [0]" `
        -o tsv
    if ($value -is [string]) {
        return $value.Trim()
    }
    return $value
}

function Get-KnowledgePostgresConnection {
    param([string]$AppName)

    foreach ($name in @(
            "KnowledgeBase__ConnectionString",
            "ConnectionStrings__KnowledgeDb"
        )) {
        $value = Get-AppSetting -AppName $AppName -SettingName $name
        if ($value) {
            Write-Host "Using App Service setting $name"
            return $value
        }
    }

    throw "No Postgres connection string found on $AppName."
}

function Escape-EnvValue {
    param([string]$Value)
    if ($Value -match '[\s#"$`''\\]') {
        return '"' + ($Value -replace '\\', '\\' -replace '"', '\"') + '"'
    }
    return $Value
}

if ($UseAppServiceSettings) {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI required for -UseAppServiceSettings."
    }
    $appName = Get-WebAppName -Name $WebAppName
    Write-Host "Reading connection strings from App Service $appName ..."
    $pgRaw = Get-KnowledgePostgresConnection -AppName $appName
    $blobConn = Get-AppSetting -AppName $appName -SettingName "KnowledgeBase__AzureStorageConnectionString"
    if (-not $blobConn) { throw "KnowledgeBase__AzureStorageConnectionString not set on $appName" }
    # Keep Npgsql format — etc-kg config.py normalizes Host=... for psycopg (avoids URL password encoding bugs).
    $pgConn = $pgRaw

    $azureDir = Join-Path $root ".azure"
    New-Item -ItemType Directory -Force -Path $azureDir | Out-Null
    $pgRaw | Out-File (Join-Path $azureDir "knowledge-connection.txt") -Encoding utf8 -NoNewline
    $blobConn | Out-File (Join-Path $azureDir "knowledge-blob-connection.txt") -Encoding utf8 -NoNewline
    Write-Host "Updated .azure/knowledge-connection.txt from App Service."
}
else {
    $pgPath = Join-Path $root ".azure/knowledge-connection.txt"
    $blobPath = Join-Path $root ".azure/knowledge-blob-connection.txt"

    if (-not (Test-Path $pgPath)) {
        throw "Missing $pgPath — run with -UseAppServiceSettings or ./scripts/deploy-knowledge-azure.ps1 first."
    }
    if (-not (Test-Path $blobPath)) {
        throw "Missing $blobPath — run with -UseAppServiceSettings or ./scripts/deploy-knowledge-azure.ps1 first."
    }

    $pgConn = (Get-Content $pgPath -Raw).Trim()
    $blobConn = (Get-Content $blobPath -Raw).Trim()
}

$envContent = @"
# Generated by push-knowledge-worker-env.ps1 — GPU VM ingest worker
KNOWLEDGE_DB_CONNECTION=$(Escape-EnvValue $pgConn)

OLLAMA_BASE_URL=http://127.0.0.1:11434
OLLAMA_EMBED_MODEL=nomic-embed-text
OLLAMA_CHAT_MODEL=llama3.1:8b

STORAGE_BACKEND=azure_blob
AZURE_STORAGE_CONNECTION_STRING=$(Escape-EnvValue $blobConn)
AZURE_STORAGE_CONTAINER=knowledge-raw
"@

$localEnv = Join-Path $env:TEMP "etc-kg-worker.env"
[System.IO.File]::WriteAllText($localEnv, $envContent.TrimEnd() + "`n")

Write-Host "Built $localEnv"
Write-Host "Copying to ${VmUser}@${VmHost}:${EtcKgRemotePath}/config/.env ..."

scp $localEnv "${VmUser}@${VmHost}:${EtcKgRemotePath}/config/.env"

if ($LASTEXITCODE -ne 0) {
    throw "scp failed. Is the VM running? Try: az vm start -g rg-intranet-dev -n etc-ollama"
}

$configPy = "C:\dev\etc\etc-kg\ingest\config.py"
if (Test-Path $configPy) {
    Write-Host "Updating ingest/config.py on VM (Npgsql connection string support) ..."
    scp $configPy "${VmUser}@${VmHost}:${EtcKgRemotePath}/ingest/config.py"
}

Write-Host ""
Write-Host "Done. On the VM, restart and test:"
Write-Host "  sudo systemctl restart etc-kg-worker"
Write-Host "  cd ~/etc-kg && .venv/bin/python -c \"from ingest.config import get_settings; import psycopg; psycopg.connect(get_settings().db_connection); print('Postgres OK')\""
Write-Host "  journalctl -u etc-kg-worker -n 15 --no-pager"
