import { useEffect, useEffectEvent, useState } from 'react'
import type { MouseEvent } from 'react'
import { notifications } from '../notifications'

const notifyInvalidation = () => notifications.info('The record context changed. Its detail dialog was closed; any unsaved detail edits were discarded.')

/** Selection-bound presentation state; mutations remain in workspace hooks. */
export function useRecordDetail<Kind extends string>(recordId: string | undefined, warnOnInvalidation = false) {
  const [detail, setDetail] = useState<{ recordId: string; kind: Kind; trigger: HTMLElement } | null>(null)
  const [invalidations, setInvalidations] = useState(0)
  if (detail && detail.recordId !== recordId) {
    setDetail(null)
    setInvalidations(invalidations + 1)
  }
  useEffect(() => {
    if (invalidations > 0 && warnOnInvalidation) notifyInvalidation()
  }, [invalidations, warnOnInvalidation])
  const onUnmount = useEffectEvent(() => {
    if (detail && warnOnInvalidation) notifyInvalidation()
  })
  useEffect(() => () => onUnmount(), [])

  return {
    kind: detail?.recordId === recordId ? detail?.kind : undefined,
    trigger: detail?.trigger,
    open: (kind: Kind, event: MouseEvent<HTMLElement>) => {
      if (recordId) setDetail({ recordId, kind, trigger: event.currentTarget })
    },
    close: () => setDetail(null),
  }
}
