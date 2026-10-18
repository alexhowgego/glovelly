/** Reveal workflow targets without scrolling the bounded desktop document. */
export function revealWorkspaceTarget(target: HTMLElement | null) {
  if (!target) return

  const behavior = window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth'
  const isBounded = target.closest('.workspace-shell')
    && window.matchMedia('(min-width: 1181px) and (min-height: 45em)').matches

  if (isBounded) {
    const pane = target.closest<HTMLElement>('.panel')
    if (pane) {
      const top = target === pane ? 0 : pane.scrollTop + target.getBoundingClientRect().top - pane.getBoundingClientRect().top
      pane.scrollTo({ top, behavior })
    }
    return
  }

  target.scrollIntoView({ behavior, block: 'start' })
}
