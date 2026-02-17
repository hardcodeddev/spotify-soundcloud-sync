import { getConnectionStatus } from '../api/client'

function ConnectionsPage() {
  const checkConnections = async () => {
    await getConnectionStatus()
  }

  return (
    <section>
      <h1>Connections</h1>
      <button onClick={checkConnections}>Check connection status</button>
    </section>
  )
}

export default ConnectionsPage
