import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/auth': {
        target: 'http://127.0.0.1:5000',
        changeOrigin: true,
      },
      '/playlists': {
        target: 'http://127.0.0.1:5000',
        changeOrigin: true,
      },
      '/migrate': {
        target: 'http://127.0.0.1:5000',
        changeOrigin: true,
      },
    },
  },
})
