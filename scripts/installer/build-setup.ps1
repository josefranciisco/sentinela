#Requires -Version 5.1
<#
.SYNOPSIS
  Publica o agente Windows (self-contained) e gera dist\installer\Sentinela.exe

.EXAMPLE
  .\scripts\installer\build-setup.ps1
  .\scripts\installer\build-setup.ps1 -ApiUrlDefault "http://192.168.0.116:5002"
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$ApiUrlDefault = "http://192.168.0.116:5002"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$agentCsproj = Join-Path $root "src\Services\Sentinela.Agent\Sentinela.Agent.csproj"
$publishDir = Join-Path $root "dist\agent-setup"
$installerDir = Join-Path $root "dist\installer"
$iss = Join-Path $PSScriptRoot "Sentinela.iss"

function Get-AgentVersion {
    $xml = [xml](Get-Content -LiteralPath $agentCsproj -Raw)
    $version = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) { return "1.0.0" }
    return $version.Trim()
}

function Find-ISCC {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe")
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) { return $c }
    }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "Inno Setup não encontrado. Instale com: winget install --id JRSoftware.InnoSetup -e"
}

Write-Host "==> Gerando ícone e imagens do assistente"
& (Join-Path $PSScriptRoot "generate-assets.ps1")

$version = Get-AgentVersion
Write-Host "==> Publicando Sentinela.Agent $version ($Runtime, self-contained)"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet publish $agentCsproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:DebugType=none `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falhou (exit $LASTEXITCODE)"
}

$agentExe = Join-Path $publishDir "Sentinela.Agent.exe"
if (-not (Test-Path -LiteralPath $agentExe)) {
    throw "Publish não gerou Sentinela.Agent.exe em $publishDir"
}

foreach ($junk in @("dist", "createdump.exe")) {
    $path = Join-Path $publishDir $junk
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
Get-ChildItem -LiteralPath $publishDir -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Force

$iscc = Find-ISCC
Write-Host "==> Compilando instalador com $iscc"

New-Item -ItemType Directory -Path $installerDir -Force | Out-Null

# Caminhos relativos no .iss (AgentPublishDir). Só a versão entra como define.
& $iscc "/DMyAppVersion=$version" $iss
if ($LASTEXITCODE -ne 0) {
    throw "ISCC falhou (exit $LASTEXITCODE)"
}

$setup = Join-Path $installerDir "Sentinela.exe"
if (-not (Test-Path -LiteralPath $setup)) {
    throw "Instalador não foi gerado em $setup"
}

$sizeMb = [Math]::Round((Get-Item -LiteralPath $setup).Length / 1MB, 1)
Write-Host ""
Write-Host "Instalador pronto: $setup ($sizeMb MB)"
Write-Host "Interface: execute Sentinela.exe (assistente em português)"
Write-Host "Silencioso: Sentinela.exe /VERYSILENT /NORESTART /APIURL=$ApiUrlDefault"
