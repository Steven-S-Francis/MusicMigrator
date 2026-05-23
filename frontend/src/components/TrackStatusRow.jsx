const STATUS_STYLE = {
  Matched: { icon: '\u2713', color: '#1DB954', label: 'Matched' },
  PartialMatch: { icon: '~', color: '#f0a500', label: 'Partial' },
  NotFound: { icon: '\u2717', color: '#ff4d4d', label: 'Not found' },
}

export default function TrackStatusRow({ result }) {
  const st = STATUS_STYLE[result.status]

  return (
    <div
      style={{
        display: 'grid',
        gridTemplateColumns: '1fr 1fr 100px',
        gap: 12,
        padding: '10px 0',
        borderBottom: '1px solid #222',
        fontSize: 14,
        alignItems: 'center',
      }}
    >
      <div>
        <div style={{ fontWeight: 500 }}>{result.sourceTrack.title}</div>
        <div style={{ color: '#888', fontSize: 12 }}>{result.sourceTrack.artist}</div>
      </div>
      <div>
        {result.matchedTrack ? (
          <>
            <div style={{ fontWeight: 500 }}>{result.matchedTrack.title}</div>
            <div style={{ color: '#888', fontSize: 12 }}>{result.matchedTrack.artist}</div>
          </>
        ) : (
          <span style={{ color: '#555' }}>&mdash;</span>
        )}
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: st.color }}>
        <span>{st.icon}</span>
        <span style={{ fontSize: 12 }}>{st.label}</span>
        <span style={{ fontSize: 12, opacity: 0.8 }}>
          {Math.round(result.confidenceScore * 100)}%
        </span>
      </div>
    </div>
  )
}
