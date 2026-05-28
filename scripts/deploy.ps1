#Requires -Version 7.0
param(
    [string]$ResourceGroup = "rg-intranet-dev",
    [string]$Location = "eastus2",
    [string]$EnvironmentName = "intranet",
    [switch]$FreshStart,
    [SecureString]$PostgresPassword
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Ensure-AzCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        Write-Host "Azure CLI not found. Install with: winget install -e --id Microsoft.AzureCLI"
        exit 1
    }
}

function Get-PostgresPassword {
    if ($null -ne $PostgresPassword) {
        $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PostgresPassword)
        try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    }
    if ($env:POSTGRES_ADMIN_PASSWORD) { return $env:POSTGRES_ADMIN_PASSWORD }
    return -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
}

function Wait-ResourceGroupDeleted {
    param([string]$Name)
    $attempts = 0
    while ($attempts -lt 60) {
        az group show --name $Name --output none 2>$null
        if ($LASTEXITCODE -ne 0) { return }
        Start-Sleep -Seconds 10
        $attempts++
        Write-Host "Waiting for resource group deletion..."
    }
    throw "Timed out waiting for resource group '$Name' to be deleted."
}

function Ensure-ResourceGroup {
    param([string]$Name, [string]$PreferredLocation, [switch]$Recreate)

    if ($Recreate) {
        Write-Host "Fresh start: deleting resource group '$Name'..."
        az group delete --name $Name --yes --no-wait | Out-Null
        Wait-ResourceGroupDeleted -Name $Name
        Write-Host "Creating resource group '$Name' in '$PreferredLocation'..."
        az group create --name $Name --location $PreferredLocation --output none
        return
    }

    $existing = az group show --name $Name --query location -o tsv 2>$null
    if ($LASTEXITCODE -eq 0 -and $existing) {
        if ($existing -ne $PreferredLocation) {
            Write-Host "Resource group '$Name' exists in '$existing'; resources will deploy to '$PreferredLocation'."
            Write-Host "If deployment fails with InvalidResourceLocation, re-run with -FreshStart to recreate the group in '$PreferredLocation'."
        }
        return
    }

    Write-Host "Creating resource group '$Name' in '$PreferredLocation'..."
    az group create --name $Name --location $PreferredLocation --output none
}

Ensure-AzCli
$password = Get-PostgresPassword

Ensure-ResourceGroup -Name $ResourceGroup -PreferredLocation $Location -Recreate:$FreshStart

Write-Host "Deploying infrastructure to region $Location..."
$deployJson = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file "$root/infra/main.bicep" `
    --parameters environmentName=$EnvironmentName location=$Location postgresAdminPassword=$password `
    --output json

if ($LASTEXITCODE -ne 0) {
    Write-Error "Infrastructure deployment failed. Fix errors above before publishing the API."
}

$deployJson | Out-File "$root/.azure/last-deployment.json" -Encoding utf8
$deployment = $deployJson | ConvertFrom-Json

if ($deployment.properties.provisioningState -ne "Succeeded") {
    Write-Error "Infrastructure deployment state: $($deployment.properties.provisioningState)"
}

$webAppName = $deployment.properties.outputs.webAppName.value
$webAppUrl = $deployment.properties.outputs.webAppUrl.value

if ([string]::IsNullOrWhiteSpace($webAppName)) {
    Write-Error "Deployment succeeded but webAppName output is missing."
}

Write-Host "Publishing API..."
Push-Location "$root/src/api"
try {
    dotnet publish -c Release -o ./publish
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }
    if (Test-Path ./publish.zip) { Remove-Item ./publish.zip -Force }
    Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force
}
finally {
    Pop-Location
}

Write-Host "Deploying package to $webAppName..."
az webapp deploy `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --src-path "$root/src/api/publish.zip" `
    --type zip

if ($LASTEXITCODE -ne 0) {
    Write-Error "Web app deployment failed."
}

Write-Host ""
Write-Host "Deployment complete."
Write-Host "URL: $webAppUrl"
Write-Host "Health: $webAppUrl/health"
Write-Host "PostgreSQL password was used for this deployment. Store it securely if generated."
