import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getAuthStatus, getPlaylists, startMigration } from '../services/api'

export default function SelectPlaylists() {
  const [connectedProviders, setConnectedProviders] = useState([])
  const [source, setSource] = useState('')
  const [destination, setDestination] = useState('')
  const [playlists, setPlaylists] = useState([])
  const [selectedPlaylist, setSelectedPlaylist] = useState(null)
  const [loading, setLoading] = useState(false)
  const [starting, setStarting] = useState(false)
  const navigate = useNavigate()

  useEffect(() => {
    getAuthStatus().then((s) => {
      const connected = Object.entries(s)
        .filter(([, v]) => v)
        .map(([k]) => k)
      setConnectedProviders(connected)
      if (connected.length > 0) setSource(connected[0])
      if (connected.length > 1) setDestination(connected[1])
    })
  }, [])

  useEffect(() => {
    if (!source) return
    setLoading(true)
    setSelectedPlaylist(null)
    getPlaylists(source)
      .then(setPlaylists)
      .finally(() => setLoading(false))
  }, [source])

  const handleSourceChange = (e) => {
    const newSource = e.target.value
    setSource(newSource)
    if (newSource === destination) {
      setDestination('')
    }
  }

  const handleStart = async () => {
    if (!selectedPlaylist || !destination) return
    setStarting(true)
    try {
      const { jobId } = await startMigration({
        sourceProvider: source,
        destinationProvider: destination,
        playlistId: selectedPlaylist.id,
        playlistName: selectedPlaylist.name,
      })
      navigate(`/progress/${jobId}`)
    } finally {
      setStarting(false)
    }
  }

  return (
    <div style={{ maxWidth: 560, margin: '40px auto', padding: '0 20px' }}>
      <button
        onClick={() => navigate('/')}
        style={{
          background: 'none',
          border: 'none',
          color: '#888',
          cursor: 'pointer',
          fontSize: 14,
          marginBottom: 24,
          padding: 0,
        }}
      >
        &larr; Back
      </button>

      <h2 style={{ fontSize: 22, fontWeight: 600, marginBottom: 24 }}>Choose what to migrate</h2>

      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 28 }}>
        <select
          value={source}
          onChange={handleSourceChange}
          style={{
            flex: 1,
            padding: '10px 14px',
            borderRadius: 8,
            border: '1px solid #444',
            background: '#1a1a20',
            color: '#f0f0f0',
            fontSize: 14,
          }}
        >
          {connectedProviders.map((p) => (
            <option key={p} value={p}>
              {p.charAt(0).toUpperCase() + p.slice(1)}
            </option>
          ))}
        </select>

        <span style={{ color: '#666', fontSize: 20 }}>&rarr;</span>

        <select
          value={destination}
          onChange={(e) => setDestination(e.target.value)}
          style={{
            flex: 1,
            padding: '10px 14px',
            borderRadius: 8,
            border: '1px solid #444',
            background: '#1a1a20',
            color: '#f0f0f0',
            fontSize: 14,
          }}
        >
          <option value="">Destination</option>
          {connectedProviders
            .filter((p) => p !== source)
            .map((p) => (
              <option key={p} value={p}>
                {p.charAt(0).toUpperCase() + p.slice(1)}
              </option>
            ))}
        </select>
      </div>

      <div
        style={{
          maxHeight: 360,
          overflowY: 'auto',
          marginBottom: 24,
        }}
      >
        {loading ? (
          <div style={{ color: '#666', textAlign: 'center', padding: 40 }}>Loading playlists...</div>
        ) : playlists.length === 0 ? (
          <div style={{ color: '#666', textAlign: 'center', padding: 40 }}>No playlists found.</div>
        ) : (
          playlists.map((pl) => {
            const isSelected = selectedPlaylist?.id === pl.id
            return (
              <button
                key={pl.id}
                onClick={() => setSelectedPlaylist(pl)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 14,
                  width: '100%',
                  padding: '12px 16px',
                  marginBottom: 8,
                  borderRadius: 10,
                  border: isSelected ? '2px solid #7c3aed' : '2px solid transparent',
                  background: isSelected ? '#1e1028' : '#16161d',
                  color: '#f0f0f0',
                  cursor: 'pointer',
                  textAlign: 'left',
                  fontSize: 14,
                }}
              >
                {pl.coverUrl ? (
                  <img
                    src={pl.coverUrl}
                    alt=""
                    style={{ width: 48, height: 48, borderRadius: 6, objectFit: 'cover' }}
                  />
                ) : (
                  <div
                    style={{
                      width: 48,
                      height: 48,
                      borderRadius: 6,
                      background: '#2a2a35',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      color: '#555',
                      fontSize: 20,
                    }}
                  >
                    &#9835;
                  </div>
                )}
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {pl.name}
                  </div>
                  <div style={{ color: '#888', fontSize: 12 }}>{pl.trackCount} tracks</div>
                </div>
              </button>
            )
          })
        )}
      </div>

      <button
        disabled={!selectedPlaylist || !destination || starting}
        onClick={handleStart}
        style={{
          width: '100%',
          padding: '14px 0',
          borderRadius: 10,
          border: 'none',
          fontSize: 16,
          fontWeight: 600,
          cursor: selectedPlaylist && destination && !starting ? 'pointer' : 'not-allowed',
          background: selectedPlaylist && destination ? '#7c3aed' : '#333',
          color: selectedPlaylist && destination ? '#fff' : '#888',
        }}
      >
        {starting
          ? 'Starting...'
          : selectedPlaylist
            ? `Migrate "${selectedPlaylist.name}" \u2192`
            : 'Select a playlist to migrate'}
      </button>
    </div>
  )
}
