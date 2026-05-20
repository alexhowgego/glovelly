import { formatBuildMetadata } from '../formatters'
import type { AppMetadata } from '../types'

type SignInScreenProps = {
  appMetadata: AppMetadata
  onSignIn: () => void
  shouldCloseBrowserNotice: boolean
  status: string
}

export function SignInScreen({
  appMetadata,
  onSignIn,
  shouldCloseBrowserNotice,
  status,
}: SignInScreenProps) {
  return (
    <main className="app-shell auth-shell">
      <section className="hero-panel auth-panel">
        <div className="hero-copy">
          <p className="eyebrow">Secure Sign-In</p>
          <h1>Sign in to open Glovelly.</h1>
          <p className="hero-text">
            Use your usual account to access clients, gigs and invoices.
          </p>
        </div>

        <div className="auth-actions">
          <span className="status-pill">{status}</span>
          {shouldCloseBrowserNotice && (
            <p className="auth-note">
              You have been signed out of Glovelly. Close your browser too if you are
              using a shared device.
            </p>
          )}
          <button className="primary-button" onClick={onSignIn} type="button">
            Continue with Google
          </button>
        </div>
      </section>
      <div className="app-footer">
        <nav className="legal-links" aria-label="Legal">
          <a href="https://docs.glovelly.net/about.html" target="_blank" rel="noreferrer">
            About
          </a>
          <a href="https://docs.glovelly.net/privacy.html" target="_blank" rel="noreferrer">
            Privacy policy
          </a>
          <a href="https://docs.glovelly.net/terms.html" target="_blank" rel="noreferrer">
            Terms of Service
          </a>
        </nav>
        <p className="build-meta">{formatBuildMetadata(appMetadata.commitId, appMetadata.buildTimestamp)}</p>
      </div>
    </main>
  )
}
