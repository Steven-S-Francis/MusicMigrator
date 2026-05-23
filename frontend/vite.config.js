import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/auth': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/playlists': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/migrate': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
