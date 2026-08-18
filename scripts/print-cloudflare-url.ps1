# Mostra a URL pública do túnel Cloudflare.
# Rode na pasta do repo:  powershell -File scripts\print-cloudflare-url.ps1
$logNamed = docker logs sentinela-cloudflared 2>&1 | Out-String
$logQuick = docker logs sentinela-cloudflared-quick 2>&1 | Out-String
$log = $logQuick + $logNamed

$quick = [regex]::Matches($log, 'https://[a-z0-9-]+\.trycloudflare\.com')
if ($quick.Count -gt 0) {
  Write-Host $quick[$quick.Count - 1].Value
  exit 0
}

$hostMatch = [regex]::Matches($log, 'hostname\\":\\"([a-z0-9.-]+)')
if ($hostMatch.Count -gt 0) {
  $name = $hostMatch[$hostMatch.Count - 1].Groups[1].Value
  Write-Host "https://$name"
  exit 0
}

Write-Host 'Tunel nao encontrado. Suba com: docker compose up -d cloudflared-quick'
exit 1
