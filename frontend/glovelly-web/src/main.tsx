import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { loadAppMetadata } from './appShared'

const THEME_STORAGE_KEY = 'glovelly.theme-preference'

const applyInitialTheme = () => {
  const rawPreference = window.localStorage.getItem(THEME_STORAGE_KEY)
  const preference =
    rawPreference === 'light' || rawPreference === 'dark' || rawPreference === 'system'
      ? rawPreference
      : 'system'

  const systemTheme = window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light'
  const resolvedTheme = preference === 'system' ? systemTheme : preference

  document.documentElement.setAttribute('data-theme', resolvedTheme)
}

applyInitialTheme()

const root = createRoot(document.getElementById('root')!)

async function bootstrap() {
  const appMetadata = await loadAppMetadata()
  document.title = appMetadata.title

  root.render(
    <StrictMode>
      <App appMetadata={appMetadata} />
    </StrictMode>,
  )
}

void bootstrap()

if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js')
  })
}
