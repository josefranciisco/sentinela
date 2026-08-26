$ErrorActionPreference = "Stop"
$src = "C:\Users\ti3\Documents\sentinela\dist\agent-setup"
$dest = "C:\Program Files\Sentinela\Agent"
Stop-Service SentinelaAgent -Force -ErrorAction SilentlyContinue
Get-Process "Sentinela.Agent" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
robocopy $src $dest /E /XD dist /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed $LASTEXITCODE" }
Start-Service SentinelaAgent
Start-Sleep -Seconds 6
Get-CimInstance Win32_Process -Filter "Name='Sentinela.Agent.exe'" |
    Select-Object ProcessId, SessionId, CommandLine |
    Format-List |
    Out-File "C:\ProgramData\Sentinela\Agent\logs\update-deploy.txt" -Encoding utf8
