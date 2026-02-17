const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'

export async function getConnectionStatus() {
  const response = await fetch(`${API_BASE_URL}/health`)
  return response.json()
}

export async function runSync() {
  const response = await fetch(`${API_BASE_URL}/api/sync/run`, {
    method: 'POST'
  })

  return response.ok
}
