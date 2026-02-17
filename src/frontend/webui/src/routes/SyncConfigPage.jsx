import { useEffect, useState } from 'react'
import { getSyncProfile, saveSyncProfile, saveSyncSchedule } from '../api/client'

const EMPTY_MAPPING = { sourceProvider: 'spotify', sourcePlaylistId: '', targetProvider: 'soundcloud', targetPlaylistId: '' }

function SyncConfigPage() {
  const [direction, setDirection] = useState('OneWay')
  const [likesBehavior, setLikesBehavior] = useState('Disabled')
  const [cronExpression, setCronExpression] = useState('')
  const [playlistMappings, setPlaylistMappings] = useState([EMPTY_MAPPING])
  const [message, setMessage] = useState(null)

  useEffect(() => {
    const load = async () => {
      try {
        const profile = await getSyncProfile()
        setDirection(profile.direction)
        setLikesBehavior(profile.likesBehavior)
        setCronExpression(profile.schedule?.cronExpression ?? '')
        setPlaylistMappings(profile.playlistMappings?.length ? profile.playlistMappings : [EMPTY_MAPPING])
      } catch {
        setMessage({ type: 'error', text: 'Unable to load sync config.' })
      }
    }

    load()
  }, [])

  const updateMapping = (index, key, value) => {
    setPlaylistMappings((current) => current.map((mapping, i) => i === index ? { ...mapping, [key]: value } : mapping))
  }

  const save = async () => {
    try {
      await saveSyncProfile({
        direction,
        likesBehavior,
        playlistMappings
      })

      await saveSyncSchedule({
        cronExpression,
        timeZoneId: 'UTC'
      })

      setMessage({ type: 'success', text: 'Sync configuration saved.' })
    } catch {
      setMessage({ type: 'error', text: 'Failed to save sync configuration.' })
    }
  }

  return (
    <section>
      <h2>Sync Configuration</h2>
      {message && <div className={`banner ${message.type}`}>{message.text}</div>}

      <label>
        Sync direction
        <select value={direction} onChange={(event) => setDirection(event.target.value)}>
          <option value="OneWay">One way</option>
          <option value="TwoWay">Two way</option>
        </select>
      </label>

      <label>
        Likes sync
        <select value={likesBehavior} onChange={(event) => setLikesBehavior(event.target.value)}>
          <option value="Disabled">Disabled</option>
          <option value="SourceToTargetOnly">Source to target only</option>
          <option value="TwoWay">Two way</option>
        </select>
      </label>

      <label>
        Cron schedule
        <input
          value={cronExpression}
          onChange={(event) => setCronExpression(event.target.value)}
          placeholder="*/30 * * * *"
        />
      </label>

      <h3>Playlist mappings</h3>
      {playlistMappings.map((mapping, index) => (
        <div className="mapping-row" key={`${index}-${mapping.sourcePlaylistId}-${mapping.targetPlaylistId}`}>
          <select value={mapping.sourceProvider} onChange={(event) => updateMapping(index, 'sourceProvider', event.target.value)}>
            <option value="spotify">Spotify</option>
            <option value="soundcloud">SoundCloud</option>
          </select>
          <input
            value={mapping.sourcePlaylistId}
            onChange={(event) => updateMapping(index, 'sourcePlaylistId', event.target.value)}
            placeholder="Source playlist ID"
          />
          <select value={mapping.targetProvider} onChange={(event) => updateMapping(index, 'targetProvider', event.target.value)}>
            <option value="soundcloud">SoundCloud</option>
            <option value="spotify">Spotify</option>
          </select>
          <input
            value={mapping.targetPlaylistId}
            onChange={(event) => updateMapping(index, 'targetPlaylistId', event.target.value)}
            placeholder="Target playlist ID"
          />
        </div>
      ))}

      <button onClick={() => setPlaylistMappings((current) => [...current, EMPTY_MAPPING])}>Add mapping</button>
      <button onClick={save}>Save sync configuration</button>
    </section>
  )
}

export default SyncConfigPage
