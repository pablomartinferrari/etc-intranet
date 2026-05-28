#Requires -Version 7.0
param(
    [string]$ResourceGroup = "rg-intranet-dev",
    [string]$WebAppName = "",
    [switch]$BuildFrontend
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Ensure-AzCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI not found. Install with: winget install -e --id Microsoft.AzureCLI"
    }
}

function Resolve-WebAppName {
    param([string]$ResourceGroup, [string]$CurrentName)

    if (-not [string]::IsNullOrWhiteSpace($CurrentName)) {
        return $CurrentName
    }

    $lastDeploymentPath = "$root/.azure/last-deployment.json"
    if (Test-Path $lastDeploymentPath) {
        try {
            $deployment = Get-Content $lastDeploymentPath | ConvertFrom-Json
            $fromOutput = $deployment.properties.outputs.webAppName.value
            if (-not [string]::IsNullOrWhiteSpace($fromOutput)) {
                return $fromOutput
            }
        }
        catch {
            # fall back to Azure query
        }
    }

    $fromAzure = az webapp list --resource-group $ResourceGroup --query "[0].name" -o tsv
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($fromAzure)) {
        throw "Could not resolve Web App name. Pass -WebAppName explicitly."
    }
    return $fromAzure
}

Ensure-AzCli
$resolvedWebApp = Resolve-WebAppName -ResourceGroup $ResourceGroup -CurrentName $WebAppName

if ($BuildFrontend) {
    Write-Host "Building frontend..."
    Push-Location "$root/src/web"
    try {
        npm install
        if ($LASTEXITCODE -ne 0) { throw "npm install failed." }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build failed." }
    }
    finally {
        Pop-Location
    }
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

Write-Host "Deploying package to $resolvedWebApp..."
az webapp deploy `
    --resource-group $ResourceGroup `
    --name $resolvedWebApp `
    --src-path "$root/src/api/publish.zip" `
    --type zip

if ($LASTEXITCODE -ne 0) {
    throw "Web app deployment failed."
}

$webAppHost = az webapp show --resource-group $ResourceGroup --name $resolvedWebApp --query "defaultHostName" -o tsv
Write-Host ""
Write-Host "App deployment complete."
Write-Host "URL: https://$webAppHost"
Write-Host "Liveness: https://$webAppHost/health/live"
Write-Host "Health (includes DB): https://$webAppHost/health"
Write-Host ""
Write-Host "If startup is slow, stream logs with:"
Write-Host "  az webapp log tail --resource-group $ResourceGroup --name $resolvedWebApp"
