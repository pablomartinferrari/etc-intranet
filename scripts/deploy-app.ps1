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

$zipPath = "$root/src/api/publish.zip"
$zipSizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "Deploying package to $resolvedWebApp ($zipSizeMb MB)..."
if ($zipSizeMb -gt 100) {
    Write-Warning "Package is large; upload may time out. Consider excluding .pdb files from publish."
}

function Invoke-ZipDeploy {
    param([string]$Method)

    if ($Method -eq "onedeploy") {
        # az often prints track-status lines as WARNING on stderr even when deploy succeeds.
        $output = az webapp deploy `
            --resource-group $ResourceGroup `
            --name $resolvedWebApp `
            --src-path $zipPath `
            --type zip `
            --timeout 1800000 `
            --track-status $true 2>&1 | Out-String

        if ($output) {
            Write-Host $output.TrimEnd()
        }

        if ($LASTEXITCODE -eq 0) {
            return 0
        }

        if ($output -match 'Site started successfully|RuntimeSuccessful') {
            Write-Warning "az webapp deploy exited with code $LASTEXITCODE but reported success; treating deploy as successful."
            return 0
        }

        return $LASTEXITCODE
    }

    # Fallback: Zip Deploy API (often more reliable when OneDeploy resets the connection).
    az webapp deployment source config-zip `
        --resource-group $ResourceGroup `
        --name $resolvedWebApp `
        --src $zipPath
    return $LASTEXITCODE
}

$deployed = $false
foreach ($attempt in 1..3) {
    Write-Host "Deploy attempt $attempt/3 (OneDeploy)..."
    if ((Invoke-ZipDeploy -Method "onedeploy") -eq 0) {
        $deployed = $true
        break
    }
    if ($attempt -lt 3) {
        Write-Warning "Deploy failed (often transient). Retrying in 15s..."
        Start-Sleep -Seconds 15
    }
}

if (-not $deployed) {
    Write-Warning "OneDeploy failed after 3 attempts. Trying Zip Deploy API..."
    if ((Invoke-ZipDeploy -Method "config-zip") -ne 0) {
        throw @"
Web app deployment failed.
Try manually:
  az webapp deploy -g $ResourceGroup -n $resolvedWebApp --src-path $zipPath --type zip --timeout 1800000
Or check SCM: https://$resolvedWebApp.scm.azurewebsites.net
If on VPN, disconnect and retry.
"@
    }
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
