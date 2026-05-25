import axios from 'axios'

const client = axios.create({
  baseURL: '',
  withCredentials: true,
})

export function getAuthStatus() {
  return client.get('/auth/status').then((r) => r.data)
}

export function connectProvider(provider) {
  window.location.href = `/auth/${provider}/start`
}

export function disconnectProvider(provider) {
  return client.delete(`/auth/${provider}`)
}

export function getPlaylists(provider) {
  return client.get(`/playlists/${provider}`).then((r) => r.data)
}

export function startMigration({ sourceProvider, destinationProvider, playlistId, playlistName }) {
  return client
    .post('/migrate', {
      sourceProvider,
      destinationProvider,
      playlistId,
      playlistName,
    })
    .then((r) => r.data)
}

export function getMigrationStatus(jobId) {
  return client.get(`/migrate/${jobId}`).then((r) => r.data)
}

export function getMigrationHistory() {
  return client.get('/migrate').then((r) => r.data)
}

export function connectAnghami(cookieString) {
  return client.post('/auth/anghami/cookies', { cookieString }).then((r) => r.data)
}
