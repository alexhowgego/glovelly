import type { ReactNode, RefObject } from 'react'
import type {
  AppMetadata,
  AppSection,
  AuthUser,
  SellerProfile,
  ThemePreference,
} from '../types'
import { formatBuildMetadata } from '../formatters'

export type AppNavigationItem = {
  id: AppSection
  label: string
  eyebrow: string
  description: string
  disabled?: boolean
}

export type HeaderMetric = {
  value: number
  label: string
}

type AppShellProps = {
  activeSection: AppSection
  appMetadata: AppMetadata
  authUser: AuthUser | null
  children: ReactNode
  currentSection: AppNavigationItem | undefined
  currentSectionContent: ReactNode
  headerMetrics: HeaderMetric[]
  isAdmin: boolean
  isAdminLoading: boolean
  isGigLoading: boolean
  isLoading: boolean
  isProfileMenuOpen: boolean
  isQuickReceiptSaving: boolean
  isSellerProfileSaving: boolean
  isUserSettingsSaving: boolean
  navigationItems: AppNavigationItem[]
  pendingGigImportCount: number
  onOpenGigImports: () => void
  onOpenSellerProfile: () => void
  onOpenUserSettings: () => void
  onProfileMenuToggle: () => void
  onQuickReceiptFile: (file: File) => void
  onSectionChange: (section: AppSection) => void
  onSignOut: () => void
  onThemePreferenceChange: (preference: ThemePreference) => void
  profileMenuRef: RefObject<HTMLDivElement | null>
  sellerProfile: SellerProfile
  themePreference: ThemePreference
}

export function AppShell({
  activeSection,
  appMetadata,
  authUser,
  children,
  currentSection,
  currentSectionContent,
  headerMetrics,
  isAdmin,
  isAdminLoading,
  isGigLoading,
  isLoading,
  isProfileMenuOpen,
  isQuickReceiptSaving,
  isSellerProfileSaving,
  isUserSettingsSaving,
  navigationItems,
  pendingGigImportCount,
  onOpenGigImports,
  onOpenSellerProfile,
  onOpenUserSettings,
  onProfileMenuToggle,
  onQuickReceiptFile,
  onSectionChange,
  onSignOut,
  onThemePreferenceChange,
  profileMenuRef,
  sellerProfile,
  themePreference,
}: AppShellProps) {
  const profileDisplayName = authUser?.name?.trim() || authUser?.email || 'User'
  const profileImageUrl = authUser?.profileImageUrl?.trim() || ''
  const profileInitials = profileDisplayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')

  return (
    <main className="app-shell">
      <section className="hero-panel app-frame">
        <div className="content-shell">
          <div className="content-header panel">
            <div className="content-header-top">
              <div className="content-header-copy">
                <p className="eyebrow">Workspace</p>
                <h1>Your work, all in one place.</h1>
                <p className="hero-text">
                  Move between clients, gigs, invoices and admin tools with everything easy to find.
                </p>
              </div>

              <div className="header-actions">
                <label className="primary-button quick-receipt-button">
                  <span aria-hidden="true">+</span>
                  Scan receipt
                  <input
                    type="file"
                    accept="application/pdf,image/jpeg,image/png,image/webp,image/heic,image/heif"
                    disabled={isLoading || isGigLoading || isQuickReceiptSaving}
                    onChange={(event) => {
                      const file = event.target.files?.[0]
                      event.target.value = ''
                      if (file) {
                        onQuickReceiptFile(file)
                      }
                    }}
                  />
                </label>

                <div className="profile-menu" ref={profileMenuRef}>
                  <button
                    aria-expanded={isProfileMenuOpen}
                    aria-haspopup="menu"
                    aria-label="Open profile menu"
                    className={`profile-trigger ${isProfileMenuOpen ? 'open' : ''}`}
                    onClick={onProfileMenuToggle}
                    type="button"
                  >
                    {pendingGigImportCount > 0 && (
                      <span className="notification-dot profile-notification-dot" aria-hidden="true" />
                    )}
                    <span className="profile-avatar" aria-hidden="true">
                      {profileImageUrl ? (
                        <img
                          className="profile-avatar-image"
                          src={profileImageUrl}
                          alt=""
                          decoding="async"
                          referrerPolicy="no-referrer"
                        />
                      ) : (
                        profileInitials || 'U'
                      )}
                    </span>
                  </button>

                  {isProfileMenuOpen && (
                    <div className="profile-dropdown" role="menu" aria-label="Profile menu">
                      <div className="profile-summary">
                        <p className="section-label">Signed in</p>
                        <strong>{profileDisplayName}</strong>
                        <span>{authUser?.email}</span>
                      </div>
                      <div className="profile-meta">
                        <span>{isAdmin ? 'Administrator' : 'Standard access'}</span>
                      </div>
                      <div className="profile-meta">
                        <span>
                          {sellerProfile.isInvoiceReady
                            ? 'Seller profile ready'
                            : sellerProfile.isConfigured
                              ? 'Seller profile needs attention'
                              : 'Seller profile not set up'}
                        </span>
                      </div>
                      <label className="theme-field" htmlFor="theme-preference-select">
                        <span>Theme</span>
                        <select
                          id="theme-preference-select"
                          value={themePreference}
                          onChange={(event) =>
                            onThemePreferenceChange(event.target.value as ThemePreference)
                          }
                        >
                          <option value="system">System</option>
                          <option value="light">Light</option>
                          <option value="dark">Dark</option>
                        </select>
                      </label>
                      <button
                        className="ghost-button profile-settings"
                        onClick={onOpenSellerProfile}
                        role="menuitem"
                        type="button"
                        disabled={isLoading || isAdminLoading || isSellerProfileSaving}
                      >
                        Seller profile
                      </button>
                      <button
                        className="ghost-button profile-settings profile-menu-alert-item"
                        onClick={onOpenGigImports}
                        role="menuitem"
                        type="button"
                        disabled={isLoading}
                      >
                        <span>
                          Imported gigs
                          {pendingGigImportCount > 0 ? ` (${pendingGigImportCount})` : ''}
                        </span>
                        {pendingGigImportCount > 0 && (
                          <span className="notification-dot" aria-hidden="true" />
                        )}
                      </button>
                      <button
                        className="ghost-button profile-settings"
                        onClick={onOpenUserSettings}
                        role="menuitem"
                        type="button"
                        disabled={isLoading || isAdminLoading || isUserSettingsSaving}
                      >
                        Settings
                      </button>
                      <button
                        className="ghost-button profile-signout"
                        onClick={onSignOut}
                        role="menuitem"
                        type="button"
                        disabled={isLoading || isAdminLoading}
                      >
                        Sign out
                      </button>
                    </div>
                  )}
                </div>
              </div>
            </div>

            <div className="content-header-body">
              <div className="content-header-copy">
                <p className="eyebrow">{currentSection?.eyebrow ?? 'Workspace'}</p>
                <h2>{currentSection?.label ?? 'Glovelly'}</h2>
                <p className="hero-text">{currentSection?.description}</p>
              </div>

              <div className="hero-mascot header-mascot">
                <img
                  src="/gordon-192.png"
                  alt="Gordon the Glovelly mascot"
                  decoding="async"
                  loading="lazy"
                />
                <div>
                  <p className="section-label">Meet Gordon</p>
                  <strong>Mozart wig. Rubber chicken. Unreasonably good taste in admin.</strong>
                </div>
              </div>
            </div>

            <nav className="charm-bar" aria-label="Primary">
              {navigationItems.map((item) => (
                <button
                  key={item.id}
                  className={`charm-item ${activeSection === item.id ? 'selected' : ''}`}
                  data-testid={`nav-${item.id}`}
                  onClick={() => onSectionChange(item.id)}
                  type="button"
                  disabled={item.disabled}
                >
                  <span className="charm-meta">{item.eyebrow}</span>
                  <strong>{item.label}</strong>
                  <span>{item.description}</span>
                </button>
              ))}
            </nav>

            <div className="content-header-aside">
              <div className="hero-metrics">
                {headerMetrics.map((metric) => (
                  <article key={metric.label}>
                    <span>{metric.value}</span>
                    <p>{metric.label}</p>
                  </article>
                ))}
              </div>
            </div>
          </div>

          {currentSectionContent}
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
        <p className="build-meta">
          {appMetadata.deploymentName ? `${appMetadata.deploymentName} \u2022 ` : ''}
          {formatBuildMetadata(appMetadata.commitId, appMetadata.buildTimestamp)}
        </p>
      </div>

      {children}
    </main>
  )
}
