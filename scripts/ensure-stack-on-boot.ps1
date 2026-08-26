# Ensures Sentinela Docker stack is up after Windows reboot.
# Install once (elevated): .\scripts\ensure-stack-on-boot.ps1 -Install

param(
    [switch]$Install,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$TaskName = 'SentinelaEnsureStack'

function Wait-DockerReady {
    param([int]$TimeoutSec = 300)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            docker info 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) { return $true }
        } catch { }
        Start-Sleep -Seconds 5
    }
    return $false
}

if ($Uninstall) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Removed scheduled task '$TaskName'."
    exit 0
}

if ($Install) {
    $scriptPath = $PSCommandPath

    # Prefer scheduled task (needs elevation). Fall back to Startup folder (no admin).
    $installed = $false
    try {
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' `
            -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$scriptPath`""
        $trigger = New-ScheduledTaskTrigger -AtLogOn
        $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
            -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
        Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
            -Principal $principal -Settings $settings -Force | Out-Null
        Write-Host "Installed scheduled task '$TaskName' (runs at logon)."
        $installed = $true
    } catch {
        Write-Warning "Scheduled task denied ($($_.Exception.Message)). Using Startup folder instead."
    }

    if (-not $installed) {
        $startup = [Environment]::GetFolderPath('Startup')
        $cmdPath = Join-Path $startup 'SentinelaEnsureStack.cmd'
        @"
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "$scriptPath"
"@ | Set-Content -Path $cmdPath -Encoding ASCII
        Write-Host "Installed Startup script: $cmdPath"
    }

    Write-Host "Also ensuring Docker Desktop Service starts automatically…"
    try {
        Set-Service -Name com.docker.service -StartupType Automatic -ErrorAction Stop
        Start-Service -Name com.docker.service -ErrorAction SilentlyContinue
        Write-Host "com.docker.service set to Automatic."
    } catch {
        Write-Warning "Could not set com.docker.service (run as Admin): $($_.Exception.Message)"
        Write-Host "Manual: Docker Desktop → Settings → General → enable 'Start Docker Desktop when you log in'."
    }
    exit 0
}

Set-Location $RepoRoot
if (-not (Wait-DockerReady -TimeoutSec 300)) {
    Write-Error "Docker engine did not become ready in time."
    exit 1
}

# Give the engine a moment after first start; then bring the stack up.
Start-Sleep -Seconds 8
docker compose up -d
exit $LASTEXITCODE
