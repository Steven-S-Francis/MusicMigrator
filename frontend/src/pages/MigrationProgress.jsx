import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { getMigrationStatus } from '../services/api'
import TrackStatusRow from '../components/TrackStatusRow'

const STATUS_COLORS = {
  Pending: '#888',
  Running: '#7c3aed',
  Completed: '#1DB954',
  Failed: '#ff4d4d',
}

export default function MigrationProgress() {
  const { jobId } = useParams()
  const navigate = useNavigate()
  const [job, setJob] = useState(null)

  const fetchStatus = () => {
    getMigrationStatus(jobId).then(setJob)
  }

  useEffect(() => {
    fetchStatus()

    const id = setInterval(() => {
      if (job && (job.status === 'Completed' || job.status === 'Failed')) {
        clearInterval(id)
        return
      }
      fetchStatus()
    }, 2000)

    return () => clearInterval(id)
  }, [jobId])

  if (!job) {
    return (
      <div style={{ maxWidth: 700, margin: '60px auto', padding: '0 20px', color: '#666' }}>
        Loading...
      </div>
    )
  }

  const isRunning = job.status === 'Running'
  const isCompleted = job.status === 'Completed'
  const isFailed = job.status === 'Failed'
  const isDone = isCompleted || isFailed
  const pct = job.totalTracks > 0 ? Math.round((job.processedTracks / job.totalTracks) * 100) : 0

  const matchStats = (job.results || []).reduce(
    (acc, r) => {
      acc[r.status] = (acc[r.status] || 0) + 1
      return acc
    },
    { Matched: 0, PartialMatch: 0, NotFound: 0 }
  )

  return (
    <div style={{ maxWidth: 700, margin: '40px auto', padding: '0 20px' }}>
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

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
        <h2 style={{ fontSize: 20, fontWeight: 600 }}>{job.sourcePlaylistName}</h2>
        <span
          style={{
            padding: '4px 12px',
            borderRadius: 12,
            fontSize: 12,
            fontWeight: 600,
            color: '#fff',
            background: STATUS_COLORS[job.status] || '#888',
          }}
        >
          {job.status}
        </span>
      </div>
      <div style={{ color: '#888', fontSize: 13, marginBottom: 20 }}>
        {job.sourceProvider} &rarr; {job.destinationProvider}
      </div>

      {isRunning && (
        <div style={{ marginBottom: 24 }}>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              fontSize: 13,
              color: '#aaa',
              marginBottom: 6,
            }}
          >
            <span>
              {job.processedTracks} / {job.totalTracks} tracks
            </span>
            <span>{pct}%</span>
          </div>
          <div
            style={{
              width: '100%',
              height: 8,
              borderRadius: 4,
              background: '#2a2a35',
              overflow: 'hidden',
            }}
          >
            <div
              style={{
                width: `${pct}%`,
                height: '100%',
                background: '#7c3aed',
                borderRadius: 4,
                transition: 'width 0.4s ease',
              }}
            />
          </div>
        </div>
      )}

      {(job.results || []).length > 0 && (
        <div style={{ display: 'flex', gap: 12, marginBottom: 24 }}>
          <div
            style={{
              flex: 1,
              padding: '14px 12px',
              borderRadius: 10,
              background: '#0d1f12',
              textAlign: 'center',
            }}
          >
            <div style={{ fontSize: 22, fontWeight: 700, color: '#1DB954' }}>
              {matchStats.Matched || 0}
            </div>
            <div style={{ fontSize: 12, color: '#888' }}>Matched</div>
          </div>
          <div
            style={{
              flex: 1,
              padding: '14px 12px',
              borderRadius: 10,
              background: '#1f1a0a',
              textAlign: 'center',
            }}
          >
            <div style={{ fontSize: 22, fontWeight: 700, color: '#f0a500' }}>
              {matchStats.PartialMatch || 0}
            </div>
            <div style={{ fontSize: 12, color: '#888' }}>Partial</div>
          </div>
          <div
            style={{
              flex: 1,
              padding: '14px 12px',
              borderRadius: 10,
              background: '#1f0d0d',
              textAlign: 'center',
            }}
          >
            <div style={{ fontSize: 22, fontWeight: 700, color: '#ff4d4d' }}>
              {matchStats.NotFound || 0}
            </div>
            <div style={{ fontSize: 12, color: '#888' }}>Not found</div>
          </div>
        </div>
      )}

      {job.errorMessage && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: 8,
            background: '#2a1010',
            border: '1px solid #ff4d4d44',
            color: '#ff6b6b',
            fontSize: 14,
            marginBottom: 24,
          }}
        >
          {job.errorMessage}
        </div>
      )}

      {isCompleted && job.destinationPlaylistId && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: 8,
            background: '#0d1f12',
            border: '1px solid #1db95444',
            color: '#5cdb7a',
            fontSize: 14,
            marginBottom: 24,
          }}
        >
          Playlist created on {job.destinationProvider} with ID: {job.destinationPlaylistId}
        </div>
      )}

      {(job.results || []).length > 0 && (
        <div>
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: '1fr 1fr 100px',
              gap: 12,
              padding: '10px 0',
              borderBottom: '1px solid #333',
              fontSize: 12,
              color: '#888',
              fontWeight: 600,
              textTransform: 'uppercase',
            }}
          >
            <div>Source Track</div>
            <div>Matched Track</div>
            <div>Status</div>
          </div>
          {job.results.map((r, i) => (
            <TrackStatusRow key={i} result={r} />
          ))}
        </div>
      )}
    </div>
  )
}
