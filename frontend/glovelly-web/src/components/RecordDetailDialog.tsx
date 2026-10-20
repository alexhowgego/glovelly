import { useEffect, useEffectEvent, useId, useRef } from 'react'
import type { ReactNode } from 'react'
import { createPortal } from 'react-dom'

type Props = {
  title: string
  children: ReactNode
  onClose: () => void
  canClose?: () => boolean
  returnFocus?: HTMLElement | null
}

/** Native modal containment, with existing domain dialogs as its top layer. */
export function RecordDetailDialog({ title, children, onClose, canClose, returnFocus }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const headingRef = useRef<HTMLHeadingElement>(null)
  const titleId = useId()
  const requestClose = () => {
    if (!canClose || canClose()) onClose()
  }
  const cancelFromKeyboard = useEffectEvent(requestClose)

  useEffect(() => {
    const dialog = dialogRef.current!
    const trigger = returnFocus ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null)
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    dialog.showModal()
    headingRef.current?.focus()

    let activeLayer: HTMLElement = dialog
    let childTrigger: HTMLElement | null = null
    const body = dialog.querySelector<HTMLElement>('.record-detail-dialog-body')!
    let parentScrollTop = body.scrollTop
    let restoreInert: (() => void)[] = []
    const resetInert = () => {
      restoreInert.forEach(restore => restore())
      restoreInert = []
    }
    const updateLayer = () => {
      const layers = dialog.querySelectorAll<HTMLElement>('[role="dialog"]')
      const top = layers.item(layers.length - 1) ?? dialog
      if (top === activeLayer) return
      resetInert()
      if (top !== dialog) {
        if (activeLayer === dialog) parentScrollTop = body.scrollTop
        childTrigger = document.activeElement instanceof HTMLElement ? document.activeElement : null
        // Inert sibling branches, never an ancestor of the active child.
        let branch: HTMLElement = top
        while (branch !== dialog && branch.parentElement) {
          for (const sibling of branch.parentElement.children) {
            if (sibling !== branch && sibling instanceof HTMLElement) {
              const wasInert = sibling.inert
              sibling.inert = true
              restoreInert.push(() => { sibling.inert = wasInert })
            }
          }
          branch = branch.parentElement
        }
        top.tabIndex = -1
        top.focus()
      } else if (childTrigger?.isConnected) {
        childTrigger.focus({ preventScroll: true })
      } else {
        headingRef.current?.focus()
      }
      if (top === dialog) body.scrollTop = parentScrollTop
      activeLayer = top
    }
    const observer = new MutationObserver(updateLayer)
    observer.observe(dialog, { childList: true, subtree: true })
    updateLayer()

    const trapTab = (event: KeyboardEvent) => {
      if (event.key !== 'Tab' || event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return
      updateLayer()
      // The native outer dialog does not wrap focus inside a nested role=dialog.
      // Recompute each time: controls can become hidden, disabled or inert.
      const controls = [...activeLayer.querySelectorAll<HTMLElement>(
        'a[href], area[href], button, input, select, textarea, iframe, [tabindex], [contenteditable="true"], audio[controls], video[controls], summary',
      )].filter(element => {
        if (element.tabIndex < 0 || element.matches(':disabled') || element.closest('[hidden], [inert]') || !element.getClientRects().length) return false
        for (let ancestor: HTMLElement | null = element; ancestor; ancestor = ancestor.parentElement) {
          if (ancestor.inert) return false
        }
        const style = getComputedStyle(element)
        return style.visibility !== 'hidden' && style.visibility !== 'collapse'
      }).sort((left, right) => (left.tabIndex || Number.MAX_SAFE_INTEGER) - (right.tabIndex || Number.MAX_SAFE_INTEGER))
      const first = controls[0]
      const last = controls[controls.length - 1]
      if (!first) {
        event.preventDefault()
        activeLayer.focus()
      } else if (!controls.includes(document.activeElement as HTMLElement)
        || (event.shiftKey ? document.activeElement === first : document.activeElement === last)) {
        event.preventDefault()
        const target = event.shiftKey ? last : first
        target.focus()
      }
    }
    document.addEventListener('keydown', trapTab, true)

    const cancel = (event: Event) => {
      event.preventDefault()
      if (activeLayer !== dialog) {
        activeLayer.querySelector<HTMLButtonElement>('[data-dialog-close]')?.click()
      } else {
        cancelFromKeyboard()
      }
    }
    dialog.addEventListener('cancel', cancel)
    return () => {
      observer.disconnect()
      document.removeEventListener('keydown', trapTab, true)
      resetInert()
      dialog.removeEventListener('cancel', cancel)
      dialog.close()
      document.body.style.overflow = previousOverflow
      if (trigger?.isConnected && !trigger.closest('[inert]')) {
        trigger.focus({ preventScroll: true })
      } else {
        document.querySelector<HTMLElement>('.record-overview h2, .compact-list-controls input')?.focus({ preventScroll: true })
      }
    }
  }, [returnFocus])

  return createPortal(
    <dialog ref={dialogRef} aria-labelledby={titleId} className="record-detail-dialog panel">
      <div className="panel-heading">
        <h2 id={titleId} ref={headingRef} tabIndex={-1}>{title}</h2>
        <button type="button" className="ghost-button" onClick={() => requestClose()}>Close</button>
      </div>
      <div className="record-detail-dialog-body">{children}</div>
    </dialog>,
    document.body,
  )
}
