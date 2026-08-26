Stop-Service SentinelaAgent -Force
Start-Sleep 2

$source = "C:\Users\ti3\Documents\sentinela\src\Services\Sentinela.Agent\bin\Release\net9.0-windows"
$dest = "C:\Program Files\Sentinela\Agent"

Copy-Item "$source\Sentinela.Agent.dll" "$dest\Sentinela.Agent.dll" -Force
Copy-Item "$source\Sentinela.Agent.exe" "$dest\Sentinela.Agent.exe" -Force
Copy-Item "$source\Sentinela.Agent.deps.json" "$dest\Sentinela.Agent.deps.json" -Force
Copy-Item "$source\Sentinela.Agent.runtimeconfig.json" "$dest\Sentinela.Agent.runtimeconfig.json" -Force

Start-Service SentinelaAgent
Start-Sleep 3

Get-Service SentinelaAgent | Format-List Name, Status
Write-Host "Deploy concluido!"
