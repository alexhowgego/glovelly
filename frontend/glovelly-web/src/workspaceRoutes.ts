import type { AppSection } from './types'

const paths = {
  clients: '/workspaces/clients',
  gigs: '/workspaces/gigs',
  invoices: '/workspaces/invoices',
} as const

export function sectionFromPath(pathname: string): AppSection {
  const match = Object.entries(paths).find(([, path]) => pathname === path || pathname === `${path}/`)
  return (match?.[0] as AppSection | undefined) ?? 'gigs'
}

export function pathForSection(section: AppSection): string {
  return section === 'admin' ? '/' : paths[section]
}

export function replaceWorkspacePath(section: AppSection) {
  const path = pathForSection(section)
  if (window.location.pathname !== path || window.location.search || window.location.hash) {
    window.history.replaceState(window.history.state, '', path)
  }
}
