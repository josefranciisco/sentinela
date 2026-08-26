@echo off
echo ==========================================
echo   Sentinela Agent - Deploy Completo
echo ==========================================
echo.

echo [1/5] Parando servico SentinelaAgent...
sc stop SentinelaAgent >nul 2>&1
timeout /t 5 /nobreak >nul
sc query SentinelaAgent
echo.

echo [2/5] Compilando agente...
cd /d C:\Users\ti3\Documents\sentinela
dotnet publish src\Services\Sentinela.Agent\Sentinela.Agent.csproj -c Release -r win-x64 --self-contained true -o C:\SentinelaAgentDeploy
echo.

echo [3/5] Parando servico definitivamente...
sc stop SentinelaAgent >nul 2>&1
timeout /t 2 /nobreak >nul
taskkill /F /IM Sentinela.Agent.exe >nul 2>&1
timeout /t 2 /nobreak >nul

echo [4/5] Copiando binarios para C:\Program Files\Sentinela\Agent\...
set "SRC=C:\SentinelaAgentDeploy"
set "DST=C:\Program Files\Sentinela\Agent"

copy /Y "%SRC%\Sentinela.Agent.exe" "%DST%\Sentinela.Agent.exe"
copy /Y "%SRC%\Sentinela.Agent.dll" "%DST%\Sentinela.Agent.dll"
copy /Y "%SRC%\Sentinela.Agent.deps.json" "%DST%\Sentinela.Agent.deps.json"
copy /Y "%SRC%\Sentinela.Agent.runtimeconfig.json" "%DST%\Sentinela.Agent.runtimeconfig.json"

for %%f in ("%SRC%\*.dll") do copy /Y "%%f" "%DST%\" >nul
for %%f in ("%SRC%\*.json") do copy /Y "%%f" "%DST%\" >nul
echo Copiado!

echo.
echo [5/5] Iniciando servico...
sc start SentinelaAgent
timeout /t 10 /nobreak >nul
echo.
echo === Status Final ===
sc query SentinelaAgent
echo.
echo === Deploy concluido! ===
pause
