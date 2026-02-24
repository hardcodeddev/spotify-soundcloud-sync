const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'

async function request(path, options = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers ?? {})
    },
    ...options
  })

  if (!response.ok) {
    const errorBody = await response.text()
    throw new Error(errorBody || `Request failed with status ${response.status}`)
  }

  if (response.status === 204) {
    return null
  }

  return response.json()
}

export function getConnectionStatus() {
  return request('/auth/connections')
}

export function getSyncProfile() {
  return request('/sync/profile')
}

export function saveSyncProfile(profile) {
  return request('/sync/profile', {
    method: 'PUT',
    body: JSON.stringify(profile)
  })
}

export function saveSyncSchedule(schedule) {
  return request('/sync/schedule', {
    method: 'PUT',
    body: JSON.stringify(schedule)
  })
}

export function runSyncNow() {
  return request('/sync/run-now', {
    method: 'POST'
  })
}

export function getLatestRun() {
  return request('/sync/runs/latest')
}

export function getRuns() {
  return request('/sync/runs')
}

export function getProviderStartUrl(provider) {
  return `${API_BASE_URL}/auth/${provider}/start`
}

export function getPlaylists(provider) {
  return request(`/sync/playlists?provider=${encodeURIComponent(provider)}`)
}


export function getAuthConfig() {
  return request('/auth/config')
}

export function saveAuthConfig(config) {
  return request('/auth/config', {
    method: 'PUT',
    body: JSON.stringify(config)
  })
}
