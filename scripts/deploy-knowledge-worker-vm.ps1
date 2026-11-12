#Requires -Version 7.0
<#
.SYNOPSIS
  Copy etc-kg + worker setup script to the GPU VM and push config/.env (no nano).

.EXAMPLE
  ./scripts/deploy-knowledge-worker-vm.ps1 -VmHost 20.127.81.213
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$VmHost,
    [string]$VmUser = "azureuser",
    [string]$EtcKgLocal = "C:\dev\etc\etc-kg"
)

$ErrorActionPreference = "Stop"
$intranetRoot = Split-Path -Parent $PSScriptRoot
$setupScript = Join-Path $PSScriptRoot "setup-knowledge-worker-vm.sh"

if (-not (Test-Path $EtcKgLocal)) {
    throw "etc-kg not found at $EtcKgLocal"
}
if (-not (Test-Path $setupScript)) {
    throw "Missing $setupScript"
}

$archive = Join-Path $env:TEMP "etc-kg-deploy.tgz"
if (Test-Path $archive) { Remove-Item $archive -Force }

Write-Host "Packing etc-kg (excluding .venv, data, .git) ..."
Push-Location $EtcKgLocal
try {
    tar -czf $archive `
        --exclude=.venv `
        --exclude=data `
        --exclude=.git `
        --exclude=__pycache__ `
        --exclude="*.pyc" `
        .
}
finally {
    Pop-Location
}

$remote = "${VmUser}@${VmHost}"
$lfSetupScript = Join-Path $env:TEMP "setup-knowledge-worker-vm.sh"
$setupContent = [System.IO.File]::ReadAllText($setupScript) -replace "`r`n", "`n" -replace "`r", "`n"
[System.IO.File]::WriteAllText($lfSetupScript, $setupContent, [System.Text.UTF8Encoding]::new($false))

Write-Host "Uploading to $remote ..."
scp $archive "${remote}:~/etc-kg-deploy.tgz"
scp $lfSetupScript "${remote}:~/setup-knowledge-worker-vm.sh"

Write-Host "Extracting on VM ..."
ssh $remote @"
set -e
mkdir -p ~/etc-kg
tar -xzf ~/etc-kg-deploy.tgz -C ~/etc-kg
rm ~/etc-kg-deploy.tgz
sed -i 's/\r$//' ~/setup-knowledge-worker-vm.sh
chmod +x ~/setup-knowledge-worker-vm.sh
"@

& (Join-Path $PSScriptRoot "push-knowledge-worker-env.ps1") -VmHost $VmHost -VmUser $VmUser

Write-Host ""
Write-Host "Next, SSH in and start the worker:"
Write-Host "  ssh $remote"
Write-Host "  bash ~/setup-knowledge-worker-vm.sh"
Write-Host "  journalctl -u etc-kg-worker -f"
