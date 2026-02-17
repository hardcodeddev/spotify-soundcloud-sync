import { Link, Route, Routes } from 'react-router-dom'
import HomePage from './routes/HomePage'
import ConnectionsPage from './routes/ConnectionsPage'
import SyncPage from './routes/SyncPage'

function App() {
  return (
    <>
      <nav>
        <ul>
          <li><Link to="/">Home</Link></li>
          <li><Link to="/connections">Connections</Link></li>
          <li><Link to="/sync">Sync</Link></li>
        </ul>
      </nav>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/connections" element={<ConnectionsPage />} />
        <Route path="/sync" element={<SyncPage />} />
      </Routes>
    </>
  )
}

export default App
