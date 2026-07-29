# Update-SentinelaAgent.ps1
param(
    [string]$ServerUrl = "https://sentinela.local",
    [string]$ApiKey = "",
    [string]$InstallPath = "C:\Program Files\Sentinela\Agent"
)

$serviceName = "SentinelaAgent"

# Stop service
if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName -Force
}

# Download latest agent
$agentUrl = "$ServerUrl/api/v1/agent/download/latest"
$agentPath = "$env:TEMP\SentinelaAgent.zip"
Invoke-WebRequest -Uri $agentUrl -Headers @{ "X-API-Key" = $ApiKey } -OutFile $agentPath

# Extract
Expand-Archive -Path $agentPath -DestinationPath $InstallPath -Force

# Install/Update service
if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    & "$InstallPath\Sentinela.Agent.exe" --uninstall
}
& "$InstallPath\Sentinela.Agent.exe" --install --autostart

# Start service
Start-Service $serviceName

Write-Host "✅ Agent updated successfully"
