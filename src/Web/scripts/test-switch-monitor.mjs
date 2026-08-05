import * as signalR from '@microsoft/signalr'

const sessionId = process.argv[2]
if (!sessionId) {
  console.error('Usage: node test-switch-monitor.mjs <sessionId>')
  process.exit(1)
}

const baseUrl = 'http://localhost:3000'
const login = await fetch(`${baseUrl}/api/v1/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username: 'Admin', password: '4517' }),
})
const { accessToken } = await login.json()

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${baseUrl}/hubs/remote?sessionId=${sessionId}`, {
    accessTokenFactory: () => accessToken,
  })
  .build()

let frames = 0

connection.on('ScreenFrameReceived', () => {
  frames++
  if (frames % 10 === 0) console.log(`frames: ${frames}`)
})

await connection.start()
console.log('connected')

await new Promise(r => setTimeout(r, 5000))
console.log('switching to monitor 2 (index 1)...')
await connection.invoke('SwitchMonitor', sessionId, 1)
console.log('switched OK')

await new Promise(r => setTimeout(r, 5000))
console.log('switching to ALL monitors...')
await connection.invoke('SwitchMonitor', sessionId, null)
console.log('switched to all OK')

await new Promise(r => setTimeout(r, 3000))
console.log(`total frames: ${frames}`)
await connection.stop()
process.exit(frames > 10 ? 0 : 2)
