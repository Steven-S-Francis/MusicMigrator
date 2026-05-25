import { connectProvider, disconnectProvider, connectAnghami } from '../services/api'

const CONFIG = {
  spotify: { label: 'Spotify', accent: '#1DB954', background: '#191414', icon: '\uD83C\uDFB5' },
  youtube: { label: 'YouTube Music', accent: '#FF0000', background: '#1a0000', icon: '\u25B6\uFE0F' },
  anghami: { label: 'Anghami', accent: '#ED1C24', background: '#1a0002', icon: '\uD83C\uDFB6' },
}

export default function ProviderCard({ provider, connected, onStatusChange }) {
  const cfg = CONFIG[provider]

  const handleConnect = () => {
    if (provider === 'anghami') {
      connectAnghami().then(onStatusChange).catch(() => {})
    } else {
      connectProvider(provider)
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
