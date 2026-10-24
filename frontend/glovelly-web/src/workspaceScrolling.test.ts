// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import { revealWorkspaceTarget } from './workspaceScrolling'

afterEach(() => {
  document.body.innerHTML = ''
  vi.unstubAllGlobals()
})

describe('workspace navigation scrolling', () => {
  it.each([false, true])('scrolls only the bounded pane (reduced motion: %s)', reduced => {
    vi.stubGlobal('matchMedia', vi.fn((query: string) => ({
      matches: query.includes('prefers-reduced-motion') ? reduced : true,
    })))
    document.body.innerHTML = '<main class="workspace-shell"><div class="panel" id="gig-overview"></div></main>'
    const target = document.getElementById('gig-overview')!
    target.scrollTo = vi.fn()
    target.scrollIntoView = vi.fn()
    revealWorkspaceTarget(target)
    expect(target.scrollTo).toHaveBeenCalledWith({ top: 0, behavior: reduced ? 'instant' : 'smooth' })
    expect(target.scrollIntoView).not.toHaveBeenCalled()
  })

  it('retains start-aligned navigation in natural-flow layouts', () => {
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false })))
    const target = document.createElement('div')
    target.scrollIntoView = vi.fn()
    revealWorkspaceTarget(target)
    expect(target.scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth', block: 'start' })
    expect(() => revealWorkspaceTarget(null)).not.toThrow()
  })
})
