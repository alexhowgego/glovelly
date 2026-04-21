import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/auth': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/clients': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/gigs': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/invoices': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/invoice-lines': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/seller-profile': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/admin': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/app': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/swagger': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
    },
  },
})
