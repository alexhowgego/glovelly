// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { pathForSection, replaceWorkspacePath, sectionFromPath } from './workspaceRoutes'

describe('workspace routes', () => {
  it.each(['clients', 'gigs', 'invoices'] as const)('resolves %s and its trailing slash', (section) => {
    expect(sectionFromPath(`/workspaces/${section}`)).toBe(section)
    expect(sectionFromPath(`/workspaces/${section}/`)).toBe(section)
    expect(pathForSection(section)).toBe(`/workspaces/${section}`)
  })

  it.each(['/', '/workspaces/client', '/workspaces/clients/extra', '/workspaces/CLIENTS', '/clients', '/workspaces/clients//'])('defaults safely for %s', (path) => {
    expect(sectionFromPath(path)).toBe('gigs')
  })

  it('does not expose an admin deep link', () => expect(pathForSection('admin')).toBe('/'))

  it('replaces the current entry when changing workspace or entering admin', () => {
    window.history.replaceState({ fixture: true }, '', '/workspaces/clients')
    const replace = vi.spyOn(window.history, 'replaceState')
    const length = window.history.length
    replaceWorkspacePath('invoices')
    expect(window.location.pathname).toBe('/workspaces/invoices')
    expect(window.history.length).toBe(length)
    expect(window.history.state).toEqual({ fixture: true })
    replaceWorkspacePath('admin')
    expect(window.location.pathname).toBe('/')
    expect(replace).toHaveBeenCalledTimes(2)
    replace.mockRestore()
  })
})
