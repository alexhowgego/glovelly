import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const extraAllowedHosts = (process.env.VITE_ALLOWED_HOSTS ?? '')
  .split(',')
  .map((host) => host.trim())
  .filter(Boolean)

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    allowedHosts: ['localhost', ...extraAllowedHosts],
    proxy: {
      '/.well-known': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/access': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/auth': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/test-auth': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/workspace-events': {
        target: 'http://localhost:5153',
        changeOrigin: true,
        ws: true,
      },
      '/clients': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/gigs': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/gig-imports': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/expense-statements': {
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
      '/invoice-email-template': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/integrations': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/mcp': {
        target: 'http://localhost:5153',
        changeOrigin: true,
      },
      '/oauth': {
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
