import { useEffect, useRef, useState } from 'react'

export type MapSizeBucket = 'small' | 'medium'

export const visualPaneHeightBudget = (height: number) => Math.max(0, Math.floor(height) - 8)

export function visualPaneBucket(width: number, height: number, previouslyEligible: boolean, previousBucket: MapSizeBucket | null = null): MapSizeBucket | null {
  const threshold = previouslyEligible ? 600 : 620
  if (width < threshold || height < (previouslyEligible ? 280 : 300)) return null
  const mediumWidth = previousBucket === 'medium' ? 820 : previousBucket === 'small' ? 860 : 840
  const mediumHeight = previousBucket === 'medium' ? 410 : previousBucket === 'small' ? 450 : 430
  return width >= mediumWidth && height >= mediumHeight ? 'medium' : 'small'
}

export function useVisualPaneEligibility() {
  const ref = useRef<HTMLDivElement>(null)
  const [pane, setPane] = useState<{ bucket: MapSizeBucket | null, height: number }>({ bucket: null, height: 0 })
  useEffect(() => {
    const node = ref.current
    if (!node || typeof ResizeObserver === 'undefined' || typeof IntersectionObserver === 'undefined') return
    let visible = false
    let width = 0
    let height = 0
    const update = () => setPane(previous => {
      const bucket = visible && !document.hidden
        ? visualPaneBucket(width, height, previous.bucket !== null, previous.bucket) : null
      const availableHeight = visualPaneHeightBudget(height)
      return bucket === previous.bucket && availableHeight === previous.height
        ? previous : { bucket, height: availableHeight }
    })
    const resize = new ResizeObserver(entries => {
      const rect = entries[0]?.contentRect
      width = rect?.width ?? 0
      height = rect?.height ?? 0
      update()
    })
    const intersection = new IntersectionObserver(entries => {
      visible = entries[0]?.isIntersecting ?? false
      update()
    })
    resize.observe(node)
    intersection.observe(node)
    document.addEventListener('visibilitychange', update)
    return () => {
      resize.disconnect()
      intersection.disconnect()
      document.removeEventListener('visibilitychange', update)
    }
  }, [])
  return { ref, bucket: pane.bucket, height: pane.height }
}
