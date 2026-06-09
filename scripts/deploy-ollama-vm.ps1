#Requires -Version 7.0
<#
.SYNOPSIS
  Provision an Ubuntu VM with Ollama (embed + chat) reachable from App Service.

.PARAMETER ResourceGroup
  Resource group for the VM (same as intranet by default).

.PARAMETER VmName
  Azure VM name.

.PARAMETER VmSize
  SKU. Default Standard_NC4as_T4_v3 (1× T4 GPU, 4 vCPU, 28 GB RAM).
  Requires "Standard NCASv3 T4 Family vCPUs" quota in the target region.

.PARAMETER AppServiceName
  Intranet App Service — outbound IPs are allowed on port 11434.

.EXAMPLE
  ./scripts/deploy-ollama-vm.ps1 -ResourceGroup rg-intranet-dev -AppServiceName intranet-yfjgdqq7k75by-api
#>
param(
    [string]$ResourceGroup = "rg-intranet-dev",
    [string]$Location = "eastus2",
    [string]$VmName = "etc-ollama",
    [string]$VmSize = "Standard_NC4as_T4_v3",
    [string]$AppServiceName = "",
    [switch]$SkipModelPull
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Get-AppServiceName {
    if ($AppServiceName) { return $AppServiceName }
    $last = Join-Path $root ".azure/last-deployment.json"
    if (Test-Path $last) {
        $n = (Get-Content $last -Raw | ConvertFrom-Json).properties.outputs.webAppName.value
        if ($n) { return $n }
    }
    throw "Pass -AppServiceName or run deploy.ps1 first."
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI required."
}

$appName = Get-AppServiceName

$modelPullScript = if ($SkipModelPull) {
    '      echo "Skipping model pull" >> /var/log/setup-ollama.log'
} else {
    @'
      ollama pull nomic-embed-text
      ollama pull 'llama3.1:8b'
'@
}

$cloudInit = @"
#cloud-config
package_update: true
packages:
  - curl
write_files:
  - path: /usr/local/bin/setup-ollama.sh
    permissions: '0755'
    content: |
      #!/bin/bash
      set -euo pipefail
      if ! command -v ollama >/dev/null 2>&1; then
        curl -fsSL https://ollama.com/install.sh | sh
      fi
      mkdir -p /etc/systemd/system/ollama.service.d
      cat >/etc/systemd/system/ollama.service.d/override.conf <<'EOF'
      [Service]
      Environment="OLLAMA_HOST=0.0.0.0:11434"
      EOF
      systemctl daemon-reload
      systemctl enable ollama
      systemctl restart ollama
      for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30; do
        curl -sf http://127.0.0.1:11434/api/tags && break
        sleep 2
      done
      $modelPullScript
      echo "Ollama ready on 0.0.0.0:11434" >> /var/log/setup-ollama.log
runcmd:
  - [ bash, -lc, "nohup /usr/local/bin/setup-ollama.sh >> /var/log/setup-ollama.log 2>&1 &" ]
"@

$cloudInitPath = Join-Path $env:TEMP "etc-ollama-cloud-init.yaml"
[System.IO.File]::WriteAllText($cloudInitPath, $cloudInit)

Write-Host "Creating VM $VmName ($VmSize) in $ResourceGroup..."
if ($VmSize -match "NC4as_T4|NCASv3") {
    Write-Host "GPU VM requires 'Standard NCASv3 T4 Family vCPUs' quota in $Location (request 4 in eastus if deploy fails)."
}
$existing = az vm show -g $ResourceGroup -n $VmName --query "name" -o tsv 2>$null
if ($LASTEXITCODE -eq 0 -and $existing) {
    Write-Host "VM $VmName already exists — skipping create."
}
else {
    az vm create `
        --resource-group $ResourceGroup `
        --name $VmName `
        --location $Location `
        --image Ubuntu2204 `
        --size $VmSize `
        --admin-username azureuser `
        --authentication-type ssh `
        --generate-ssh-keys `
        --public-ip-sku Standard `
        --custom-data $cloudInitPath `
        --output none
    if ($LASTEXITCODE -ne 0) { throw "VM creation failed." }
}

$publicIp = az vm show -d -g $ResourceGroup -n $VmName --query "publicIps" -o tsv
$nsgName = az network nsg list -g $ResourceGroup --query "[?contains(name, '$VmName')].name | [0]" -o tsv
if (-not $nsgName) {
    $nsgName = az network nsg list -g $ResourceGroup --query "[0].name" -o tsv
}

$outboundIps = (az webapp show -g $ResourceGroup -n $appName --query "outboundIpAddresses" -o tsv) -split ","
Write-Host "Allowing App Service outbound IPs on NSG $nsgName port 11434..."
$priority = 1000
foreach ($ip in $outboundIps) {
    $ruleName = "AllowAppService-$($ip.Replace('.', '-'))"
    az network nsg rule create `
        --resource-group $ResourceGroup `
        --nsg-name $nsgName `
        --name $ruleName `
        --priority $priority `
        --source-address-prefixes $ip `
        --destination-port-ranges 11434 `
        --access Allow `
        --protocol Tcp `
        --direction Inbound `
        --output none 2>$null
    $priority++
}

$ollamaUrl = "http://${publicIp}:11434"
Write-Host ""
Write-Host "VM public IP: $publicIp"
Write-Host "Ollama URL (for App Service): $ollamaUrl"
Write-Host "Setup log (SSH): tail -f /var/log/setup-ollama.log"
Write-Host "Model pull may take 10–20 min on first boot."
Write-Host ""
Write-Host "Wire App Service:"
Write-Host "  ./scripts/deploy-knowledge-azure.ps1 -ResourceGroup $ResourceGroup -OllamaBaseUrl `"$ollamaUrl`""

$ollamaUrl | Out-File (Join-Path $root ".azure/ollama-url.txt") -Encoding utf8 -NoNewline
