import * as signalR from '@microsoft/signalr'

const [sessionId] = process.argv.slice(2)
if (!sessionId) {
  console.error('Usage: node test-remote-stream.mjs <sessionId>')
  process.exit(1)
}

const baseUrl = 'http://localhost:3000'

const login = await fetch(`${baseUrl}/api/v1/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username: 'Admin', password: '4517' }),
})
const { accessToken } = await login.json()
console.log('token ok')

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${baseUrl}/hubs/remote?sessionId=${sessionId}`, {
    accessTokenFactory: () => accessToken,
  })
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .build()

let frames = 0
let lastSize = 0

connection.on('ScreenFrameReceived', (payload) => {
  frames++
  const raw = Array.isArray(payload.frameData)
    ? String.fromCharCode(...new Uint8Array(payload.frameData))
    : payload.frameData
  lastSize = (raw?.length ?? 0)
  if (frames === 1 || frames % 10 === 0) {
    console.log(`frame #${payload.frameNumber ?? frames} size=${lastSize}`)
  }
})

connection.on('SessionEnded', () => {
  console.log('SESSION ENDED')
  process.exit(0)
})

connection.onclose(() => {
  console.error('CONNECTION CLOSED')
  process.exit(1)
})

try {
  await connection.start()
  console.log('connected to remote hub')
} catch (err) {
  console.error('start failed:', err)
  process.exit(1)
}

setTimeout(() => {
  console.log(`timeout: received ${frames} frames`)
  process.exit(frames > 0 ? 0 : 2)
}, 20000)
