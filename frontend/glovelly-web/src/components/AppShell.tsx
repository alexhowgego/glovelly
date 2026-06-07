import { useEffect, useState } from 'react'
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

export type DashboardGigSummary = {
  title: string
  clientName: string
  dateLabel: string
  venue: string
}

export type DashboardInvoiceCandidate = DashboardGigSummary & {
  feeLabel: string
}

export type DashboardSummary = {
  outstandingBalanceLabel: string
  outstandingInvoiceCount: number
  nextGig: DashboardGigSummary | null
  invoiceCandidate: DashboardInvoiceCandidate | null
}

type AppShellProps = {
  activeSection: AppSection
  appMetadata: AppMetadata
  authUser: AuthUser | null
  children: ReactNode
  currentSection: AppNavigationItem | undefined
  currentSectionContent: ReactNode
  dashboardSummary: DashboardSummary
  isAdmin: boolean
  isAdminLoading: boolean
  isGigLoading: boolean
  isInvoiceLoading: boolean
  isLoading: boolean
  isProfileMenuOpen: boolean
  isQuickReceiptSaving: boolean
  isSellerProfileSaving: boolean
  isUserSettingsSaving: boolean
  navigationItems: AppNavigationItem[]
  pendingGigImportCount: number
  onOpenGigImports: () => void
  onOpenNextGig: () => void
  onOpenSellerProfile: () => void
  onGenerateDashboardInvoice: () => void
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
  dashboardSummary,
  isAdmin,
  isAdminLoading,
  isGigLoading,
  isInvoiceLoading,
  isLoading,
  isProfileMenuOpen,
  isQuickReceiptSaving,
  isSellerProfileSaving,
  isUserSettingsSaving,
  navigationItems,
  pendingGigImportCount,
  onOpenGigImports,
  onOpenNextGig,
  onOpenSellerProfile,
  onGenerateDashboardInvoice,
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
  const [isReturnToTopVisible, setIsReturnToTopVisible] = useState(false)
  const profileDisplayName = authUser?.name?.trim() || authUser?.email || 'User'
  const profileImageUrl = authUser?.profileImageUrl?.trim() || ''
  const profileInitials = profileDisplayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
  const returnToTop = () => {
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches

    window.scrollTo({
      top: 0,
      behavior: prefersReducedMotion ? 'auto' : 'smooth',
    })
  }

  useEffect(() => {
    const updateReturnToTopVisibility = () => {
      const isPhoneLayout = window.matchMedia('(max-width: 760px)').matches
      setIsReturnToTopVisible(isPhoneLayout && window.scrollY >= window.innerHeight / 2)
    }

    updateReturnToTopVisibility()
    window.addEventListener('scroll', updateReturnToTopVisibility, { passive: true })
    window.addEventListener('resize', updateReturnToTopVisibility)

    return () => {
      window.removeEventListener('scroll', updateReturnToTopVisibility)
      window.removeEventListener('resize', updateReturnToTopVisibility)
    }
  }, [])

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
              <div className="dashboard-summary" aria-label="Dashboard summary">
                <article className="dashboard-card outstanding-card" data-testid="dashboard-outstanding-balance">
                  <p className="section-label">Outstanding balance</p>
                  <strong>{dashboardSummary.outstandingBalanceLabel}</strong>
                  <span>
                    {dashboardSummary.outstandingInvoiceCount === 1
                      ? '1 invoice needs attention'
                      : `${dashboardSummary.outstandingInvoiceCount} invoices need attention`}
                  </span>
                </article>

                <article className="dashboard-card" data-testid="dashboard-next-gig">
                  <p className="section-label">Next gig</p>
                  {dashboardSummary.nextGig ? (
                    <>
                      <strong>{dashboardSummary.nextGig.title}</strong>
                      <span>
                        {dashboardSummary.nextGig.dateLabel} · {dashboardSummary.nextGig.clientName}
                      </span>
                      <span>{dashboardSummary.nextGig.venue}</span>
                      <button
                        className="ghost-button compact-action"
                        data-testid="dashboard-open-next-gig-button"
                        onClick={onOpenNextGig}
                        type="button"
                      >
                        Open gig
                      </button>
                    </>
                  ) : (
                    <span>No upcoming gigs on the books.</span>
                  )}
                </article>

                <article className="dashboard-card invoice-action-card" data-testid="dashboard-invoice-prompt">
                  <p className="section-label">Invoice prompt</p>
                  {dashboardSummary.invoiceCandidate ? (
                    <>
                      <strong>{dashboardSummary.invoiceCandidate.title}</strong>
                      <span>
                        {dashboardSummary.invoiceCandidate.dateLabel} ·{' '}
                        {dashboardSummary.invoiceCandidate.clientName}
                      </span>
                      <span>{dashboardSummary.invoiceCandidate.feeLabel}</span>
                      <button
                        className="primary-button compact-action"
                        data-testid="dashboard-generate-invoice-button"
                        disabled={isLoading || isInvoiceLoading}
                        onClick={onGenerateDashboardInvoice}
                        type="button"
                      >
                        Generate invoice
                      </button>
                    </>
                  ) : (
                    <span>No recent uninvoiced gigs ready for a draft.</span>
                  )}
                </article>
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

      <button
        aria-label="Return to top"
        aria-hidden={!isReturnToTopVisible}
        className={`primary-button return-to-top-button ${isReturnToTopVisible ? 'visible' : ''}`}
        onClick={returnToTop}
        tabIndex={isReturnToTopVisible ? 0 : -1}
        type="button"
      >
        Return to top
      </button>

      {children}
    </main>
  )
}
