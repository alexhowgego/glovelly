import { formatBuildMetadata } from '../formatters'
import type { AppMetadata } from '../types'

type SessionCheckingScreenProps = {
  appMetadata: AppMetadata
  status: string
}

export function SessionCheckingScreen({
  appMetadata,
  status,
}: SessionCheckingScreenProps) {
  return (
    <main className="app-shell auth-shell">
      <section className="hero-panel auth-panel">
        <div className="hero-copy">
          <p className="eyebrow">Security</p>
          <h1>Checking your sign-in.</h1>
          <p className="hero-text">Just a moment while we open your workspace.</p>
        </div>
        <span className="status-pill">{status}</span>
      </section>
      <p className="build-meta">{formatBuildMetadata(appMetadata.commitId, appMetadata.buildTimestamp)}</p>
    </main>
  )
}
