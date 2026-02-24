import http from 'node:http'
import { randomUUID } from 'node:crypto'

const port = Number(process.env.PORT || 5000)
const webOrigin = process.env.WEB_ORIGIN || 'http://localhost:5173'

const users = new Map()

const providerPlaylists = {
  spotify: [
    { id: 'sp-liked', name: 'Liked Songs' },
    { id: 'sp-discover-weekly', name: 'Discover Weekly' },
    { id: 'sp-release-radar', name: 'Release Radar' }
  ],
  soundcloud: [
    { id: 'sc-likes', name: 'My Likes' },
    { id: 'sc-daily-drops', name: 'Daily Drops' },
    { id: 'sc-archive', name: 'Archive' }
  ]
}


function parseCookies(cookieHeader = '') {
  return Object.fromEntries(cookieHeader.split(';').map(v => v.trim()).filter(Boolean).map(part => {
    const i = part.indexOf('=')
    return [part.slice(0, i), decodeURIComponent(part.slice(i + 1))]
  }))
}

function send(res, status, body, headers = {}) {
  const baseHeaders = {
    'Access-Control-Allow-Origin': webOrigin,
    'Access-Control-Allow-Credentials': 'true',
    ...headers
  }

  if (typeof body === 'object' && body !== null) {
    const payload = JSON.stringify(body)
    res.writeHead(status, { 'Content-Type': 'application/json', ...baseHeaders })
    return res.end(payload)
  }

  res.writeHead(status, baseHeaders)
  res.end(body ?? '')
}

function parseBody(req) {
  return new Promise(resolve => {
    let data = ''
    req.on('data', chunk => { data += chunk })
    req.on('end', () => {
      if (!data) return resolve({})
      try { resolve(JSON.parse(data)) } catch { resolve({}) }
    })
  })
}

function getOrCreateUser(req, explicitUserId) {
  const cookies = parseCookies(req.headers.cookie)
  const userId = (explicitUserId || req.headers['x-user-id'] || cookies.playlist_sync_user || `user-${randomUUID().replaceAll('-', '')}`).trim()

  if (!users.has(userId)) {
    users.set(userId, {
      connections: {
        spotify: { connected: false, expiresAt: null, lastRefreshResult: 'never' },
        soundcloud: { connected: false, expiresAt: null, lastRefreshResult: 'never' }
      },
      profile: {
        id: randomUUID(),
        direction: 'OneWay',
        likesBehavior: 'Disabled',
        updatedAt: new Date().toISOString(),
        schedule: { enabled: false, cronExpression: null, timeZoneId: 'UTC' },
        playlistMappings: []
      },
      runs: []
    })
  }

  return { userId, user: users.get(userId) }
}

function cookieHeader(userId) {
  return `playlist_sync_user=${encodeURIComponent(userId)}; Path=/; HttpOnly; SameSite=Lax; Max-Age=2592000`
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url || '/', `http://${req.headers.host}`)

  if (req.method === 'OPTIONS') {
    return send(res, 204, '', {
      'Access-Control-Allow-Methods': 'GET,POST,PUT,OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type, Idempotency-Key, X-User-Id'
    })
  }

  if (url.pathname === '/health' && req.method === 'GET') {
    return send(res, 200, { status: 'ok' })
  }

  if (url.pathname === '/auth/connections' && req.method === 'GET') {
    const { userId, user } = getOrCreateUser(req)
    return send(res, 200, user.connections, { 'Set-Cookie': cookieHeader(userId) })
  }

  if (url.pathname === '/auth/spotify/start' && req.method === 'GET') {
    const { userId } = getOrCreateUser(req, url.searchParams.get('userId') || '')
    return send(res, 302, '', { 'Set-Cookie': cookieHeader(userId), Location: `/auth/spotify/callback?code=mock&state=mock&userId=${encodeURIComponent(userId)}` })
  }

  if (url.pathname === '/auth/soundcloud/start' && req.method === 'GET') {
    const { userId } = getOrCreateUser(req, url.searchParams.get('userId') || '')
    return send(res, 302, '', { 'Set-Cookie': cookieHeader(userId), Location: `/auth/soundcloud/callback?code=mock&state=mock&userId=${encodeURIComponent(userId)}` })
  }

  if (url.pathname === '/auth/spotify/callback' && req.method === 'GET') {
    const { userId, user } = getOrCreateUser(req, url.searchParams.get('userId') || '')
    user.connections.spotify = { connected: true, expiresAt: null, lastRefreshResult: 'connected' }
    return send(res, 200, '<html><body><h3>Spotify connected.</h3><script>window.close()</script></body></html>', { 'Content-Type': 'text/html', 'Set-Cookie': cookieHeader(userId) })
  }

  if (url.pathname === '/auth/soundcloud/callback' && req.method === 'GET') {
    const { userId, user } = getOrCreateUser(req, url.searchParams.get('userId') || '')
    user.connections.soundcloud = { connected: true, expiresAt: null, lastRefreshResult: 'connected' }
    return send(res, 200, '<html><body><h3>SoundCloud connected.</h3><script>window.close()</script></body></html>', { 'Content-Type': 'text/html', 'Set-Cookie': cookieHeader(userId) })
  }


  if (url.pathname === '/sync/playlists' && req.method === 'GET') {
    const provider = (url.searchParams.get('provider') || '').toLowerCase()
    if (!providerPlaylists[provider]) {
      return send(res, 400, { error: 'Unsupported provider.' })
    }

    const playlists = providerPlaylists[provider].map(p => ({
      id: p.id,
      name: p.name
    }))

    return send(res, 200, playlists)
  }

  if (url.pathname === '/sync/profile' && req.method === 'GET') {

    const { userId, user } = getOrCreateUser(req)
    return send(res, 200, user.profile, { 'Set-Cookie': cookieHeader(userId) })
  }

  if (url.pathname === '/sync/profile' && req.method === 'PUT') {
    const body = await parseBody(req)
    const { userId, user } = getOrCreateUser(req)
    user.profile.direction = body.direction || user.profile.direction
    user.profile.likesBehavior = body.likesBehavior || user.profile.likesBehavior
    user.profile.playlistMappings = Array.isArray(body.playlistMappings) ? body.playlistMappings : []
    user.profile.updatedAt = new Date().toISOString()
    return send(res, 200, user.profile, { 'Set-Cookie': cookieHeader(userId) })
  }

  if (url.pathname === '/sync/schedule' && req.method === 'PUT') {
    const body = await parseBody(req)
    const { userId, user } = getOrCreateUser(req)
    const cronExpression = (body.cronExpression || '').trim()
    const timeZoneId = (body.timeZoneId || 'UTC').trim()
    if (!cronExpression) {
      user.profile.schedule = { enabled: false, cronExpression: null, timeZoneId: 'UTC' }
      user.profile.updatedAt = new Date().toISOString()
      return send(res, 200, { enabled: false }, { 'Set-Cookie': cookieHeader(userId) })
    }

    user.profile.schedule = { enabled: true, cronExpression, timeZoneId }
    user.profile.updatedAt = new Date().toISOString()
    return send(res, 200, { enabled: true, cronExpression, normalizedCronExpression: cronExpression, timeZoneId }, { 'Set-Cookie': cookieHeader(userId) })
  }

  if (url.pathname === '/sync/run-now' && req.method === 'POST') {
    const { userId, user } = getOrCreateUser(req)
    const now = new Date().toISOString()
    const run = {
      id: randomUUID(),
      syncJobId: randomUUID(),
      status: 'Completed',
      startedAt: now,
      endedAt: new Date(Date.now() + 500).toISOString(),
      importedCount: 0,
      exportedCount: 0,
      skippedCount: 0,
      error: null,
      idempotencyKey: req.headers['idempotency-key'] || randomUUID().replaceAll('-', '')
    }
    user.runs.unshift(run)
    user.runs = user.runs.slice(0, 25)
    return send(res, 202, run, { 'Set-Cookie': cookieHeader(userId) })
  }

  if (url.pathname === '/sync/runs/latest' && req.method === 'GET') {
    const { userId, user } = getOrCreateUser(req)
    if (!user.runs.length) return send(res, 404, 'No runs found', { 'Set-Cookie': cookieHeader(userId) })
    return send(res, 200, user.runs[0], { 'Set-Cookie': cookieHeader(userId) })
  }

  if (url.pathname === '/sync/runs' && req.method === 'GET') {
    const { userId, user } = getOrCreateUser(req)
    return send(res, 200, user.runs, { 'Set-Cookie': cookieHeader(userId) })
  }

  return send(res, 404, 'Not Found')
})

server.listen(port, '0.0.0.0', () => {
  console.log(`PlaylistSync JS API listening on http://0.0.0.0:${port}`)
})
