import { useCallback, useState } from 'react'
import type { CSSProperties, ReactNode } from 'react'
import { useVisualPaneEligibility } from '../hooks/useVisualPaneEligibility'
import type { MapSizeBucket } from '../hooks/useVisualPaneEligibility'

export function RecordVisualOverview({ children, visual, factsColumns = 2, layout = 'content' }: {
  children: ReactNode
  factsColumns?: 1 | 2
  layout?: 'content' | 'invoice'
  visual?: (bucket: MapSizeBucket, unavailable: () => void) => ReactNode
}) {
  const { ref, bucket, height } = useVisualPaneEligibility()
  const [failed, setFailed] = useState(false)
  const unavailable = useCallback(() => setFailed(true), [])
  const show = bucket !== null && visual !== undefined && !failed
  return <div className="overview-body" ref={ref}>
    <div className={`record-visual-layout${layout === 'invoice' ? ' invoice-visual-layout' : ''}${factsColumns === 1 ? ' single-column-facts' : ''}${show ? ' has-visual' : ''}`}
      style={{ '--record-visual-max-height': `${height}px` } as CSSProperties}>
      <div className="record-visual-facts">{children}</div>
      {show && <aside className="record-visual-rail" aria-label="Supplementary preview">{visual(bucket, unavailable)}</aside>}
    </div>
  </div>
}
