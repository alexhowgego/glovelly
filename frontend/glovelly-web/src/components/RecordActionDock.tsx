import type { ReactNode } from 'react'

export function RecordActionDock({ title, children }: { title: string, children: ReactNode }) {
  return <div className="overview-dock">
    <section className="gig-timeline-note record-action-dock" aria-label={title}>
      <p className="detail-label">{title}</p>
      {children}
    </section>
  </div>
}
