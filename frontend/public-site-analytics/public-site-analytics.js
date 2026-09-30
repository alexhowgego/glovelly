(() => {
  const measurementId = 'G-ZTRBP9KE40'
  const consentCookie = 'glovelly_analytics_consent'
  const consentMaxAge = 60 * 60 * 24 * 365
  const productionHosts = new Set(['glovelly.net', 'docs.glovelly.net', 'handbook.glovelly.net'])

  if (window.location.protocol !== 'https:' || !productionHosts.has(window.location.hostname)) return

  const readConsent = () => {
    const prefix = `${consentCookie}=`
    const cookie = document.cookie.split('; ').find((value) => value.startsWith(prefix))
    const consent = cookie?.slice(prefix.length)
    return consent === 'accepted' || consent === 'rejected' ? consent : null
  }

  const saveConsent = (consent) => {
    document.cookie = `${consentCookie}=${consent}; Max-Age=${consentMaxAge}; Path=/; Domain=.glovelly.net; SameSite=Lax; Secure`
  }

  const removeAnalyticsCookies = () => {
    const names = ['_ga', `_ga_${measurementId.slice(2).replace(/-/g, '_')}`]
    const domains = ['', '; Domain=.glovelly.net', `; Domain=${window.location.hostname}`]

    for (const name of names) {
      for (const domain of domains) {
        document.cookie = `${name}=; Max-Age=0; Path=/${domain}; SameSite=Lax; Secure`
      }
    }
  }

  let analyticsLoaded = false
  const loadAnalytics = () => {
    if (analyticsLoaded) return

    analyticsLoaded = true
    window[`ga-disable-${measurementId}`] = false
    window.dataLayer = window.dataLayer || []
    window.gtag = function gtag() {
      window.dataLayer.push(arguments)
    }
    window.gtag('js', new Date())
    window.gtag('config', measurementId)

    const tag = document.createElement('script')
    tag.async = true
    tag.src = `https://www.googletagmanager.com/gtag/js?id=${measurementId}`
    document.head.append(tag)
  }

  const stopAnalytics = () => {
    window[`ga-disable-${measurementId}`] = true
    removeAnalyticsCookies()
  }

  const style = document.createElement('style')
  style.textContent = `
    .glovelly-analytics-banner { background: #fffaf2; border: 1px solid #d5c9b9; border-radius: 1rem; bottom: 1rem; box-shadow: 0 1rem 2.5rem rgb(23 43 58 / 22%); color: #172b3a; left: 1rem; max-width: 32rem; padding: 1.25rem; position: fixed; right: 1rem; z-index: 1000; }
    .glovelly-analytics-banner[hidden] { display: none; }
    .glovelly-analytics-banner h2 { font-size: 1.1rem; margin: 0 0 .5rem; }
    .glovelly-analytics-banner p { line-height: 1.5; margin: 0 0 1rem; }
    .glovelly-analytics-actions { display: flex; flex-wrap: wrap; gap: .6rem; }
    .glovelly-analytics-actions button, .glovelly-analytics-settings { border: 1px solid #1d5575; border-radius: 999px; cursor: pointer; font: inherit; padding: .55rem .9rem; }
    .glovelly-analytics-accept { background: #1d5575; color: #fff; }
    .glovelly-analytics-reject, .glovelly-analytics-settings { background: #fffaf2; color: #1d5575; }
    .glovelly-analytics-settings { bottom: 1rem; position: fixed; right: 1rem; z-index: 999; }
    html[data-theme='dark'] .glovelly-analytics-banner, html[data-bs-theme='dark'] .glovelly-analytics-banner, html[data-theme='dark'] .glovelly-analytics-settings, html[data-bs-theme='dark'] .glovelly-analytics-settings { background: #172b3a; border-color: #465663; color: #f7f0e5; }
    html[data-theme='dark'] .glovelly-analytics-reject, html[data-bs-theme='dark'] .glovelly-analytics-reject, html[data-theme='dark'] .glovelly-analytics-settings, html[data-bs-theme='dark'] .glovelly-analytics-settings { color: #a9dddd; }
    @media (max-width: 38rem) { .glovelly-analytics-settings { bottom: .75rem; right: .75rem; } }
  `
  document.head.append(style)

  const banner = document.createElement('section')
  banner.className = 'glovelly-analytics-banner'
  banner.id = 'glovelly-analytics-consent'
  banner.setAttribute('aria-label', 'Analytics cookies')
  banner.setAttribute('role', 'region')
  banner.innerHTML = `
    <h2>Analytics cookies</h2>
    <p>Help us understand how Glovelly's public sites are used. We only load Google Analytics if you accept. <a href="https://handbook.glovelly.net/privacy.html">Privacy policy</a></p>
    <div class="glovelly-analytics-actions">
      <button class="glovelly-analytics-accept" type="button">Accept analytics</button>
      <button class="glovelly-analytics-reject" type="button">Reject analytics</button>
    </div>
  `

  const settings = document.createElement('button')
  settings.className = 'glovelly-analytics-settings'
  settings.type = 'button'
  settings.setAttribute('aria-controls', banner.id)
  settings.textContent = 'Cookie settings'

  const showBanner = (focus = false) => {
    banner.hidden = false
    settings.hidden = true
    if (focus) banner.querySelector('.glovelly-analytics-accept').focus()
  }

  const hideBanner = (focus = false) => {
    banner.hidden = true
    settings.hidden = false
    if (focus) settings.focus()
  }

  banner.querySelector('.glovelly-analytics-accept').addEventListener('click', () => {
    saveConsent('accepted')
    loadAnalytics()
    hideBanner(true)
  })
  banner.querySelector('.glovelly-analytics-reject').addEventListener('click', () => {
    saveConsent('rejected')
    stopAnalytics()
    hideBanner(true)
  })
  settings.addEventListener('click', () => showBanner(true))

  document.body.append(banner, settings)
  const consent = readConsent()
  if (consent === 'accepted') {
    loadAnalytics()
    hideBanner()
  } else if (consent === 'rejected') {
    stopAnalytics()
    hideBanner()
  } else {
    showBanner()
  }
})()
