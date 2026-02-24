import http from 'node:http'
import { randomUUID } from 'node:crypto'

const port = Number(process.env.PORT || 5000)
const webOrigin = process.env.WEB_ORIGIN || 'http://localhost:5173'
const users = new Map()
const oauthStates = new Map()

const oauthConfig = {
  spotify: {
    clientId: process.env.SPOTIFY_CLIENT_ID || '',
    clientSecret: process.env.SPOTIFY_CLIENT_SECRET || '',
    callbackUrl: process.env.SPOTIFY_CALLBACK_URL || ''
  },
  soundcloud: {
    clientId: process.env.SOUNDCLOUD_CLIENT_ID || '',
    clientSecret: process.env.SOUNDCLOUD_CLIENT_SECRET || '',
    callbackUrl: process.env.SOUNDCLOUD_CALLBACK_URL || ''
  }
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
      tokens: {
        spotify: null,
        soundcloud: null
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

function providerOauthUrls(provider) {
  if (provider === 'spotify') {
    return {
      authorizeUrl: 'https://accounts.spotify.com/authorize',
      tokenUrl: 'https://accounts.spotify.com/api/token',
      scope: 'playlist-read-private playlist-modify-private playlist-modify-public'
    }
  }

  return {
    authorizeUrl: 'https://soundcloud.com/connect',
    tokenUrl: 'https://api.soundcloud.com/oauth2/token',
    scope: 'non-expiring'
  }
}

function queryString(values) {
  return new URLSearchParams(values).toString()
}


function getAccessTokenForProvider(user, provider) {
  const tokenPayload = user.tokens?.[provider]
  if (!tokenPayload || typeof tokenPayload !== 'object') {
    return null
  }

  return tokenPayload.access_token || tokenPayload.accessToken || null
}

async function fetchProviderPlaylists(provider, accessToken) {
  if (provider === 'spotify') {
    const response = await fetch('https://api.spotify.com/v1/me/playlists?limit=50', {
      headers: { Authorization: `Bearer ${accessToken}` }
    })

    if (!response.ok) {
      const details = await response.text()
      throw new Error(`Spotify playlists request failed (${response.status}): ${details}`)
    }

    const payload = await response.json()
    const items = Array.isArray(payload?.items) ? payload.items : []
    return items
      .map(item => ({ id: item?.id ?? '', name: item?.name ?? '' }))
      .filter(item => item.id && item.name)
  }

  const response = await fetch('https://api.soundcloud.com/me/playlists?limit=50', {
    headers: { Authorization: `OAuth ${accessToken}` }
  })

  if (!response.ok) {
    const details = await response.text()
    throw new Error(`SoundCloud playlists request failed (${response.status}): ${details}`)
  }

  const payload = await response.json()
  const items = Array.isArray(payload)
    ? payload
    : (Array.isArray(payload?.collection) ? payload.collection : [])

  return items
    .map(item => ({ id: String(item?.id ?? ''), name: item?.title ?? item?.name ?? '' }))
    .filter(item => item.id && item.name)
}


function isHttpsUrl(value) {
  try {
    const parsed = new URL(value)
    return parsed.protocol === 'https:'
  } catch {
    return false
  }
}

function getSafeConfig() {
  return {
    spotify: {
      clientId: oauthConfig.spotify.clientId,
      hasClientSecret: Boolean(oauthConfig.spotify.clientSecret),
      callbackUrl: oauthConfig.spotify.callbackUrl
    },
    soundcloud: {
      clientId: oauthConfig.soundcloud.clientId,
      hasClientSecret: Boolean(oauthConfig.soundcloud.clientSecret),
      callbackUrl: oauthConfig.soundcloud.callbackUrl
    }
  }
}

async function exchangeCodeForToken(provider, code) {
  const cfg = oauthConfig[provider]
  const { tokenUrl } = providerOauthUrls(provider)

  const body = queryString({
    grant_type: 'authorization_code',
    code,
    redirect_uri: cfg.callbackUrl,
    client_id: cfg.clientId,
    client_secret: cfg.clientSecret
  })

  const response = await fetch(tokenUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body
  })

  if (!response.ok) {
    const message = await response.text()
    throw new Error(message || `Token exchange failed (${response.status})`)
  }

  return response.json()
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

  if (url.pathname === '/auth/config' && req.method === 'GET') {
    return send(res, 200, getSafeConfig())
  }

  if (url.pathname === '/auth/config' && req.method === 'PUT') {
    const body = await parseBody(req)
    for (const provider of ['spotify', 'soundcloud']) {
      const next = body?.[provider]
      if (!next) continue
      oauthConfig[provider].clientId = (next.clientId || '').trim()
      oauthConfig[provider].clientSecret = (next.clientSecret || '').trim()
      oauthConfig[provider].callbackUrl = (next.callbackUrl || oauthConfig[provider].callbackUrl).trim()
      if (oauthConfig[provider].callbackUrl && !isHttpsUrl(oauthConfig[provider].callbackUrl)) {
        return send(res, 400, `${provider} callback URL must be HTTPS.`)
      }
    }
    return send(res, 200, getSafeConfig())
  }

  if (url.pathname === '/auth/connections' && req.method === 'GET') {
    const { userId, user } = getOrCreateUser(req)
    return send(res, 200, user.connections, { 'Set-Cookie': cookieHeader(userId) })
  }

  if (url.pathname.startsWith('/auth/') && url.pathname.endsWith('/start') && req.method === 'GET') {
    const provider = url.pathname.split('/')[2]
    if (!['spotify', 'soundcloud'].includes(provider)) {
      return send(res, 404, 'Unknown provider')
    }

    const cfg = oauthConfig[provider]
    if (!cfg.clientId || !cfg.clientSecret || !cfg.callbackUrl) {
      return send(res, 400, `${provider} client credentials/callback are not configured. Open Connections and save Client ID, Client Secret, and HTTPS callback URL first.`)
    }

    if (!isHttpsUrl(cfg.callbackUrl)) {
      return send(res, 400, `${provider} callback URL must be HTTPS.`)
    }

    const { userId } = getOrCreateUser(req, url.searchParams.get('userId') || '')
    const state = randomUUID().replaceAll('-', '')
    oauthStates.set(state, { provider, userId, expiresAt: Date.now() + 10 * 60 * 1000 })

    const { authorizeUrl, scope } = providerOauthUrls(provider)
    const redirectUrl = `${authorizeUrl}?${queryString({
      client_id: cfg.clientId,
      response_type: 'code',
      redirect_uri: cfg.callbackUrl,
      scope,
      state
    })}`

    return send(res, 302, '', { 'Set-Cookie': cookieHeader(userId), Location: redirectUrl })
  }

  if (url.pathname.startsWith('/auth/') && url.pathname.endsWith('/callback') && req.method === 'GET') {
    const provider = url.pathname.split('/')[2]
    if (!['spotify', 'soundcloud'].includes(provider)) {
      return send(res, 404, 'Unknown provider')
    }

    const code = url.searchParams.get('code') || ''
    const state = url.searchParams.get('state') || ''
    const stateRecord = oauthStates.get(state)

    if (!code || !stateRecord || stateRecord.provider !== provider || stateRecord.expiresAt < Date.now()) {
      return send(res, 400, `<html><body><h3>${provider} connection failed (invalid or expired state).</h3></body></html>`, { 'Content-Type': 'text/html' })
    }

    oauthStates.delete(state)

    try {
      const token = await exchangeCodeForToken(provider, code)
      const { userId, user } = getOrCreateUser(req, stateRecord.userId)
      user.tokens[provider] = token

      const expiresAt = token.expires_in ? new Date(Date.now() + (Number(token.expires_in) * 1000)).toISOString() : null
      user.connections[provider] = {
        connected: true,
        expiresAt,
        lastRefreshResult: 'connected'
      }

      const providerTitle = provider === 'spotify' ? 'Spotify' : 'SoundCloud'
      return send(res, 200, `<html><body><h3>${providerTitle} connected.</h3><script>window.close()</script></body></html>`, { 'Content-Type': 'text/html', 'Set-Cookie': cookieHeader(userId) })
    } catch (error) {
      return send(res, 502, `<html><body><h3>${provider} token exchange failed.</h3><pre>${String(error.message || error)}</pre></body></html>`, { 'Content-Type': 'text/html' })
    }
  }

  if (url.pathname === '/sync/playlists' && req.method === 'GET') {
    const provider = (url.searchParams.get('provider') || '').toLowerCase()
    if (!['spotify', 'soundcloud'].includes(provider)) {
      return send(res, 400, { error: 'Unsupported provider.' })
    }

    const { userId, user } = getOrCreateUser(req)
    const accessToken = getAccessTokenForProvider(user, provider)
    if (!accessToken) {
      return send(res, 400, { error: `Connect ${provider} first to load playlists.` }, { 'Set-Cookie': cookieHeader(userId) })
    }

    try {
      const playlists = await fetchProviderPlaylists(provider, accessToken)
      return send(res, 200, playlists, { 'Set-Cookie': cookieHeader(userId) })
    } catch (error) {
      return send(res, 502, { error: String(error.message || error) }, { 'Set-Cookie': cookieHeader(userId) })
    }
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
