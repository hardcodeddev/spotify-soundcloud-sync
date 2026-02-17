import { useEffect, useMemo, useState } from 'react'
import { getConnectionStatus, getProviderStartUrl } from '../api/client'

function ConnectionsPage() {
  const [connections, setConnections] = useState(null)
  const [banner, setBanner] = useState(null)

  const loadConnections = async () => {
    const data = await getConnectionStatus()
    setConnections(data)
    return data
  }

  useEffect(() => {
    loadConnections().catch(() => {
      setBanner({ type: 'error', message: 'Failed to load connection status.' })
    })
  }, [])

  const statuses = useMemo(() => [
    { provider: 'spotify', label: 'Spotify' },
    { provider: 'soundcloud', label: 'SoundCloud' }
  ], [])

  const connectProvider = async (provider, label) => {
    const popup = window.open(getProviderStartUrl(provider), '_blank', 'width=600,height=800')
    if (!popup) {
      setBanner({ type: 'error', message: `Unable to open ${label} authentication window.` })
      return
    }

    setBanner({ type: 'info', message: `Waiting for ${label} authentication...` })
    const poll = window.setInterval(async () => {
      try {
        const latest = await loadConnections()
        if (latest?.[provider]?.connected) {
          setBanner({ type: 'success', message: `${label} connected successfully.` })
          window.clearInterval(poll)
          popup.close()
        } else if (popup.closed) {
          setBanner({ type: 'error', message: `${label} authentication failed or was cancelled.` })
          window.clearInterval(poll)
        }
      } catch {
        if (popup.closed) {
          setBanner({ type: 'error', message: `${label} authentication check failed.` })
          window.clearInterval(poll)
        }
      }
    }, 2000)
  }

  return (
    <section>
      <h1>Connections</h1>
      {banner && <div className={`banner ${banner.type}`}>{banner.message}</div>}
      <div className="provider-grid">
        {statuses.map(({ provider, label }) => {
          const isConnected = Boolean(connections?.[provider]?.connected)
          return (
            <article key={provider} className="provider-card">
              <h3>{label}</h3>
              <span className={`badge ${isConnected ? 'connected' : 'disconnected'}`}>
                {isConnected ? 'Connected' : 'Disconnected'}
              </span>
              <button onClick={() => connectProvider(provider, label)}>Connect {label}</button>
            </article>
          )
        })}
      </div>
      <button onClick={() => loadConnections().catch(() => setBanner({ type: 'error', message: 'Failed to refresh status.' }))}>Refresh status</button>
    </section>
  )
}

export default ConnectionsPage
