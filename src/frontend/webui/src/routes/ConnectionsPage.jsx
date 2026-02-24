import { useEffect, useMemo, useState } from 'react'
import { getAuthConfig, getConnectionStatus, getProviderStartUrl, saveAuthConfig } from '../api/client'

function ConnectionsPage() {
  const [connections, setConnections] = useState(null)
  const [banner, setBanner] = useState(null)
  const [authConfig, setAuthConfig] = useState({
    spotify: { clientId: '', callbackUrl: 'https://<your-tunnel-domain>/auth/spotify/callback' },
    soundcloud: { clientId: '', callbackUrl: 'https://<your-tunnel-domain>/auth/soundcloud/callback' }
  })

  const loadConnections = async () => {
    const data = await getConnectionStatus()
    setConnections(data)
    return data
  }

  const loadAuthConfig = async () => {
    const data = await getAuthConfig()
    setAuthConfig((current) => ({
      spotify: {
        clientId: data.spotify?.clientId ?? current.spotify.clientId,
        callbackUrl: data.spotify?.callbackUrl ?? current.spotify.callbackUrl
      },
      soundcloud: {
        clientId: data.soundcloud?.clientId ?? current.soundcloud.clientId,
        callbackUrl: data.soundcloud?.callbackUrl ?? current.soundcloud.callbackUrl
      }
    }))
  }

  useEffect(() => {
    Promise.all([loadConnections(), loadAuthConfig()]).catch(() => {
      setBanner({ type: 'error', message: 'Failed to load connection settings.' })
    })
  }, [])

  const statuses = useMemo(() => [
    { provider: 'spotify', label: 'Spotify' },
    { provider: 'soundcloud', label: 'SoundCloud' }
  ], [])

  const updateProviderConfig = (provider, key, value) => {
    setAuthConfig((current) => ({
      ...current,
      [provider]: {
        ...current[provider],
        [key]: value
      }
    }))
  }

  const saveCredentials = async () => {
    try {
      await saveAuthConfig(authConfig)
      await loadAuthConfig()
      setBanner({ type: 'success', message: 'OAuth client settings saved.' })
    } catch (error) {
      setBanner({ type: 'error', message: `Failed to save OAuth settings: ${error.message}` })
    }
  }

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

      <h3>OAuth Client Settings</h3>
      <p>Set public client settings here before connecting. Client secrets are intentionally not editable in the browser and must be configured as secure server environment variables (required for safe public/GitHub Pages usage). Callback URLs must be HTTPS.</p>
      <div className="provider-grid">
        {statuses.map(({ provider, label }) => (
          <article key={`${provider}-settings`} className="provider-card">
            <h4>{label} App</h4>
            <label>
              Client ID
              <input value={authConfig[provider].clientId} onChange={(event) => updateProviderConfig(provider, 'clientId', event.target.value)} />
            </label>
            <label>
              Callback URL
              <input value={authConfig[provider].callbackUrl} onChange={(event) => updateProviderConfig(provider, 'callbackUrl', event.target.value)} />
            </label>
          </article>
        ))}
      </div>
      <button onClick={saveCredentials}>Save OAuth settings</button>

      <h3>Provider Connections</h3>
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
