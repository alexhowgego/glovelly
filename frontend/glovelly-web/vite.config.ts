import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
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
      '/swagger': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
    },
  },
})
