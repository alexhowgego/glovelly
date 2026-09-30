import { readFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const frontendDirectory = dirname(fileURLToPath(import.meta.url))
const root = resolve(frontendDirectory, '..')
const read = (path) => readFile(resolve(root, path), 'utf8')
const assertIncludes = (source, expected, description) => {
  if (!source.includes(expected)) throw new Error(`${description}: expected ${expected}`)
}
const assertExcludes = (source, unexpected, description) => {
  if (source.includes(unexpected)) throw new Error(`${description}: unexpected ${unexpected}`)
}

const [loader, landing, guide, handbook] = await Promise.all([
  read('frontend/public-site-analytics/public-site-analytics.js'),
  read('frontend/glovelly-landing/src/pages/index.astro'),
  read('frontend/glovelly-guide/astro.config.mjs'),
  read('docs/templates/glovelly/layout/_master.tmpl'),
])

assertIncludes(loader, "const measurementId = 'G-ZTRBP9KE40'", 'GA4 measurement ID')
assertIncludes(loader, "'glovelly.net', 'docs.glovelly.net', 'handbook.glovelly.net'", 'production host allow-list')
assertIncludes(loader, "window.location.protocol !== 'https:'", 'HTTPS guard')
assertIncludes(loader, 'Domain=.glovelly.net; SameSite=Lax; Secure', 'shared secure consent cookie')
assertIncludes(loader, 'https://www.googletagmanager.com/gtag/js', 'consent-gated GA loader')
assertIncludes(loader, 'removeAnalyticsCookies()', 'withdrawal cookie cleanup')

for (const [name, source] of [['landing', landing], ['guide', guide], ['handbook', handbook]]) {
  assertIncludes(source, 'public-site-analytics.js', `${name} shared loader inclusion`)
  assertExcludes(source, 'googletagmanager.com', `${name} direct GA bootstrap`)
}
