import { runSync } from '../api/client'

function SyncPage() {
  const triggerSync = async () => {
    await runSync()
  }

  return (
    <section>
      <h1>Sync</h1>
      <button onClick={triggerSync}>Run sync now</button>
    </section>
  )
}

export default SyncPage
