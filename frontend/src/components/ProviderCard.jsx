import { useState } from 'react'
import { connectProvider, disconnectProvider, connectAnghami } from '../services/api'

const CONFIG = {
  spotify: { label: 'Spotify', accent: '#1DB954', background: '#191414', icon: '\uD83C\uDFB5' },
  youtube: { label: 'YouTube Music', accent: '#FF0000', background: '#1a0000', icon: '\u25B6\uFE0F' },
  anghami: { label: 'Anghami', accent: '#ED1C24', background: '#1a0002', icon: '\uD83C\uDFB6' },
}

export default function ProviderCard({ provider, connected, onStatusChange }) {
  const cfg = CONFIG[provider]
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const handleConnect = () => connectProvider(provider)

  const handleAnghamiConnect = async () => {
    setLoading(true)
    setError('')
    try {
      await connectAnghami(email, password)
      onStatusChange()
    } catch {
      setError('Login failed. Check your credentials.')
    } finally {
      setLoading(false)
    }
  }

  const handleDisconnect = async () => {
    await disconnectProvider(provider)
    onStatusChange()
  }

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '16px 20px',
        borderRadius: 12,
        background: cfg.background,
        border: connected ? `2px solid ${cfg.accent}` : '2px solid transparent',
        marginBottom: 12,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <span style={{ fontSize: 24 }}>{cfg.icon}</span>
        <div>
          <div style={{ fontWeight: 600, fontSize: 16 }}>{cfg.label}</div>
          <div style={{ fontSize: 13, color: connected ? cfg.accent : '#888' }}>
            {connected ? 'Connected' : 'Not connected'}
          </div>
        </div>
      </div>

      {connected ? (
        <button
          onClick={handleDisconnect}
          style={{
            background: 'transparent',
            border: `1px solid ${cfg.accent}`,
            color: cfg.accent,
            padding: '8px 18px',
            borderRadius: 8,
            cursor: 'pointer',
            fontSize: 14,
            fontWeight: 500,
          }}
        >
          Disconnect
        </button>
      ) : provider === 'anghami' ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, alignItems: 'flex-end' }}>
          <input
            type="email"
            placeholder="Email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            disabled={loading}
            style={{
              padding: '6px 10px',
              borderRadius: 6,
              border: '1px solid #555',
              background: '#222',
              color: '#f0f0f0',
              fontSize: 13,
              width: 180,
            }}
          />
          <input
            type="password"
            placeholder="Password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={loading}
            style={{
              padding: '6px 10px',
              borderRadius: 6,
              border: '1px solid #555',
              background: '#222',
              color: '#f0f0f0',
              fontSize: 13,
              width: 180,
            }}
          />
          {error && <div style={{ color: '#ff4d4d', fontSize: 11 }}>{error}</div>}
          <button
            onClick={handleAnghamiConnect}
            disabled={loading || !email || !password}
            style={{
              background: cfg.accent,
              border: 'none',
              color: '#fff',
              padding: '6px 18px',
              borderRadius: 8,
              cursor: loading || !email || !password ? 'not-allowed' : 'pointer',
              fontSize: 14,
              fontWeight: 600,
              opacity: loading ? 0.7 : 1,
            }}
          >
            {loading ? 'Connecting...' : 'Connect'}
          </button>
        </div>
      ) : (
        <button
          onClick={handleConnect}
          style={{
            background: cfg.accent,
            border: 'none',
            color: '#fff',
            padding: '8px 18px',
            borderRadius: 8,
            cursor: 'pointer',
            fontSize: 14,
            fontWeight: 600,
          }}
        >
          Connect
        </button>
      )}
    </div>
  )
}
