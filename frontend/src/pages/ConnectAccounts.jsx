import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getAuthStatus } from '../services/api'
import ProviderCard from '../components/ProviderCard'

export default function ConnectAccounts() {
  const [status, setStatus] = useState({ spotify: false, youtube: false, anghami: false })
  const navigate = useNavigate()

  const fetchStatus = () => getAuthStatus().then(setStatus)

  useEffect(() => {
    fetchStatus()

    const params = new URLSearchParams(window.location.search)
    if (params.has('connected')) {
      window.history.replaceState({}, '', '/')
    }
  }, [])

  const connectedCount = Object.values(status).filter(Boolean).length
  const missing = 2 - connectedCount

  return (
    <div
      style={{
        maxWidth: 520,
        margin: '60px auto',
        padding: '0 20px',
      }}
    >
      <h1 style={{ fontSize: 28, fontWeight: 700, marginBottom: 8 }}>
        MusicMigrator
      </h1>
      <p style={{ color: '#999', fontSize: 14, marginBottom: 32, lineHeight: 1.5 }}>
        Connect at least two platforms to migrate your playlists between them.
        Select source and destination after connecting.
      </p>

      <ProviderCard provider="spotify" connected={status.spotify} onStatusChange={fetchStatus} />
      <ProviderCard provider="youtube" connected={status.youtube} onStatusChange={fetchStatus} />
      <ProviderCard provider="anghami" connected={status.anghami} onStatusChange={fetchStatus} />

      <button
        disabled={connectedCount < 2}
        onClick={() => navigate('/select')}
        style={{
          width: '100%',
          marginTop: 24,
          padding: '14px 0',
          borderRadius: 10,
          border: 'none',
          fontSize: 16,
          fontWeight: 600,
          cursor: connectedCount >= 2 ? 'pointer' : 'not-allowed',
          background: connectedCount >= 2 ? '#7c3aed' : '#333',
          color: connectedCount >= 2 ? '#fff' : '#888',
        }}
      >
        {connectedCount >= 2
          ? 'Continue'
          : `Connect ${missing} more platform${missing !== 1 ? 's' : ''} to continue`}
      </button>
    </div>
  )
}
