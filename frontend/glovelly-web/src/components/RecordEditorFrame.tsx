import { useEffect, useRef } from 'react'
import type { FormEventHandler, ReactNode } from 'react'

export function RecordEditorFrame({ id, isOpen, label, onSubmit, children }: {
  id?: string
  isOpen: boolean
  label: string
  onSubmit: FormEventHandler<HTMLFormElement>
  children: ReactNode
}) {
  const slotRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!isOpen || !window.matchMedia('(max-width: 1180px)').matches) return
    const timer = window.setTimeout(() => {
      slotRef.current?.scrollIntoView({
        behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth',
        block: 'start',
      })
    }, 80)
    return () => window.clearTimeout(timer)
  }, [isOpen])

  return <div ref={slotRef} className={`editor-slot ${isOpen ? 'open' : ''}`}>
    <form id={id} aria-hidden={!isOpen} inert={!isOpen} aria-label={label}
      className="editor-panel panel" onSubmit={onSubmit}>
      {children}
    </form>
  </div>
}
