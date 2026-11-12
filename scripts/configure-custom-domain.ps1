#Requires -Version 7.0
<#
.SYNOPSIS
  Bind a custom hostname to the intranet App Service and provision a managed TLS certificate.

.EXAMPLE
  .\scripts\configure-custom-domain.ps1 `
    -CustomHostname "intranet.etcenvironmental.com" `
    -ResourceGroup "rg-intranet-dev" `
    -WebAppName "intranet-yfjgdqq7k75by-api"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$CustomHostname,

    [string]$ResourceGroup = "rg-intranet-dev",

    [string]$WebAppName = "",

    [switch]$SkipCertificate
)

$ErrorActionPreference = "Stop"

function Ensure-AzCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI not found. Install: winget install -e --id Microsoft.AzureCLI"
    }
}

function Resolve-WebAppName {
    param([string]$ResourceGroup, [string]$CurrentName)

    if (-not [string]::IsNullOrWhiteSpace($CurrentName)) {
        return $CurrentName
    }

    $lastDeploymentPath = "$PSScriptRoot\..\.azure\last-deployment.json"
    if (Test-Path $lastDeploymentPath) {
        try {
            $deployment = Get-Content $lastDeploymentPath | ConvertFrom-Json
            $fromOutput = $deployment.properties.outputs.webAppName.value
            if (-not [string]::IsNullOrWhiteSpace($fromOutput)) {
                return $fromOutput
            }
        }
        catch { }
    }

    $fromAzure = az webapp list --resource-group $ResourceGroup --query "[0].name" -o tsv
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($fromAzure)) {
        throw "Could not resolve Web App name. Pass -WebAppName."
    }
    return $fromAzure
}

Ensure-AzCli
$resolvedWebApp = Resolve-WebAppName -ResourceGroup $ResourceGroup -CurrentName $WebAppName

$defaultHost = az webapp show `
    --resource-group $ResourceGroup `
    --name $resolvedWebApp `
    --query "defaultHostName" -o tsv

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($defaultHost)) {
    throw "Could not read default hostname for $resolvedWebApp"
}

Write-Host "App Service: $resolvedWebApp"
Write-Host "Default host:  $defaultHost"
Write-Host "Custom host:   $CustomHostname"
Write-Host ""
Write-Host "Before continuing, ensure DNS has a CNAME:"
Write-Host "  $CustomHostname -> $defaultHost"
Write-Host ""

$answer = Read-Host "DNS is configured and propagated? (y/N)"
if ($answer -notmatch '^[yY]') {
    Write-Host "Aborted. Add the CNAME, wait for DNS, then re-run this script."
    exit 0
}

Write-Host "Adding custom hostname..."
az webapp config hostname add `
    --resource-group $ResourceGroup `
    --webapp-name $resolvedWebApp `
    --hostname $CustomHostname

if ($LASTEXITCODE -ne 0) {
    throw "Failed to add hostname. Check DNS and Azure Portal > Custom domains for validation errors."
}

if (-not $SkipCertificate) {
    Write-Host "Creating App Service managed certificate and binding..."
    az webapp config ssl create `
        --resource-group $ResourceGroup `
        --name $resolvedWebApp `
        --hostname $CustomHostname

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "ssl create failed; trying hostname bind with AppServiceManaged certificate..."
        az webapp config hostname bind `
            --resource-group $ResourceGroup `
            --name $resolvedWebApp `
            --hostname $CustomHostname `
            --ssl-type SNI `
            --certificate-type AppServiceManaged

        if ($LASTEXITCODE -ne 0) {
            throw "Certificate binding failed. Finish in Portal: App Service > Custom domains > Add binding."
        }
    }
}

Write-Host ""
Write-Host "Custom domain configured."
Write-Host "URL: https://$CustomHostname"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Entra SPA redirect URI: https://$CustomHostname"
Write-Host "  2. Rebuild web with VITE_ENTRA_REDIRECT_URI=https://$CustomHostname"
Write-Host "  3. Redeploy API + wwwroot"
Write-Host "  4. SPFx Processing app URL -> https://$CustomHostname"
