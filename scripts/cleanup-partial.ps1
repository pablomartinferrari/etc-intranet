#Requires -Version 7.0
param(
    [string]$ResourceGroup = "rg-intranet-dev"
)

$ErrorActionPreference = "Stop"

Write-Host "Deleting resource group '$ResourceGroup' and all resources..."
az group delete --name $ResourceGroup --yes

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to delete resource group."
}

Write-Host "Done. Re-deploy with:"
Write-Host "  ./deploy.ps1 -ResourceGroup $ResourceGroup -Location eastus2 -FreshStart"
