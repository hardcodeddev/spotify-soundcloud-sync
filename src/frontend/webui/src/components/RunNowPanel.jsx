import { useState } from 'react'
import { getLatestRun, getRuns, runSyncNow } from '../api/client'

function RunNowPanel() {
  const [runs, setRuns] = useState([])
  const [status, setStatus] = useState(null)

  const loadRuns = async () => {
    const data = await getRuns()
    setRuns(data)
  }

  const runNow = async () => {
    try {
      await runSyncNow()
      setStatus({ type: 'info', text: 'Sync started. Polling latest run...' })

      const poller = window.setInterval(async () => {
        try {
          const latest = await getLatestRun()
          setRuns((prev) => {
            const next = [latest, ...prev.filter((run) => run.id !== latest.id)]
            return next.slice(0, 10)
          })

          if (latest.status === 'Completed') {
            setStatus({ type: 'success', text: 'Latest sync run completed.' })
            window.clearInterval(poller)
          }

          if (latest.status === 'Failed') {
            setStatus({ type: 'error', text: 'Latest sync run failed.' })
            window.clearInterval(poller)
          }
        } catch {
          setStatus({ type: 'error', text: 'Failed to poll latest run.' })
          window.clearInterval(poller)
        }
      }, 2000)
    } catch {
      setStatus({ type: 'error', text: 'Unable to trigger sync run.' })
    }
  }

  return (
    <section>
      <h2>Run Sync Now</h2>
      {status && <div className={`banner ${status.type}`}>{status.text}</div>}
      <button onClick={runNow}>Run Sync Now</button>
      <button onClick={() => loadRuns().catch(() => setStatus({ type: 'error', text: 'Failed to load run history.' }))}>Refresh runs</button>

      <table>
        <thead>
          <tr>
            <th>Status</th>
            <th>Started</th>
            <th>Imported</th>
            <th>Exported</th>
            <th>Skipped</th>
          </tr>
        </thead>
        <tbody>
          {runs.map((run) => (
            <tr key={run.id}>
              <td>{run.status}</td>
              <td>{run.startedAt}</td>
              <td>{run.importedCount}</td>
              <td>{run.exportedCount}</td>
              <td>{run.skippedCount}</td>
            </tr>
          ))}
          {!runs.length && (
            <tr>
              <td colSpan="5">No sync runs yet.</td>
            </tr>
          )}
        </tbody>
      </table>
    </section>
  )
}

export default RunNowPanel
