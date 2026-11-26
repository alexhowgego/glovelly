import type { ReactNode } from 'react'

/** Shared list → overview → editor geometry; domain classes keep their existing row styles. */
export function RecordWorkspace({ children, className, sectionClassName, label }: {
  children: ReactNode
  className: 'workspace' | 'gig-workspace' | 'admin-workspace'
  sectionClassName?: string
  label?: string
}) {
  return <section className={`section-layout${sectionClassName ? ` ${sectionClassName}` : ''}`} aria-label={label}>
    <div className={`record-workspace ${className}`}>{children}</div>
  </section>
}
