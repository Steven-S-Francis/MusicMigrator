import { BrowserRouter, Routes, Route } from 'react-router-dom'
import ConnectAccounts from './pages/ConnectAccounts'
import SelectPlaylists from './pages/SelectPlaylists'
import MigrationProgress from './pages/MigrationProgress'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<ConnectAccounts />} />
        <Route path="/select" element={<SelectPlaylists />} />
        <Route path="/progress/:jobId" element={<MigrationProgress />} />
      </Routes>
    </BrowserRouter>
  )
}
