import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api/v1/auth': {
        target: 'http://localhost:5003',
        changeOrigin: true,
      },
      '/api/v1/admin': {
        target: 'http://localhost:5003',
        changeOrigin: true,
      },
      '/api': {
        target: 'http://localhost:5002',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5002',
        ws: true,
      },
    },
  },
})
