import { useEffect, useEffectEvent, useState } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
} from '../api'
import type { ReceiptAnalysisResult, ReceiptAnalysisTarget } from '../types'
import { AiSparkleIcon } from './AiSparkleIcon'
import { ReceiptAnalysisResults } from './ReceiptAnalysisResults'

type ReceiptAnalysisModalProps = {
  onApply: (suggestions: { merchant: string | null; totalAmount: number | null; suggestedCategory: string | null }) => void
  onClose: () => void
  onSessionExpired: (message: string) => void
  target: ReceiptAnalysisTarget
}

export function ReceiptAnalysisModal({
  onApply,
  onClose,
  onSessionExpired,
  target,
}: ReceiptAnalysisModalProps) {
  const [analysis, setAnalysis] = useState<ReceiptAnalysisResult | null>(null)
  const [isAnalysing, setIsAnalysing] = useState(false)
  const [status, setStatus] = useState('')

  const endpoint = `/gigs/${target.gigId}/expenses/${target.expenseId}/attachments/${target.attachmentId}/analysis`

  const analyse = async () => {
    setIsAnalysing(true)
    setStatus('Analysing receipt...')
    try {
      const response = await fetchWithSession(buildApiUrl(endpoint), { method: 'POST' })
      if (handleSessionExpired(response, onSessionExpired, 'Your session expired. Sign in again to analyse receipts.')) {
        return
      }
      if (!response.ok) {
        throw new Error(await getResponseErrorMessage(response, 'Unable to analyse receipt.'))
      }
      const result = (await response.json()) as ReceiptAnalysisResult
      setAnalysis(result)
      setStatus(result.status === 'Succeeded' ? 'Suggestions are ready to review.' : (result.failureMessage ?? 'Receipt analysis could not be completed.'))
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Unable to analyse receipt.')
    } finally {
      setIsAnalysing(false)
    }
  }

  const analyseOnOpen = useEffectEvent(() => {
    void analyse()
  })

  useEffect(() => {
    analyseOnOpen()
  }, [target.attachmentId])

  return (
    <div className="settings-overlay receipt-analysis-overlay" role="presentation">
      <section aria-labelledby="receipt-analysis-title" className="settings-modal receipt-analysis-modal panel" role="dialog" aria-modal="true">
        <div className="panel-heading">
          <div>
            <p className="section-label">Receipt analysis</p>
            <h2 id="receipt-analysis-title">Review suggestions</h2>
          </div>
          <button data-dialog-close className="ghost-button" onClick={onClose} type="button" disabled={isAnalysing}>Close</button>
        </div>

        <div className="quick-receipt-summary">
          <strong>{target.fileName}</strong>
          <span>{status || 'Analysing receipt...'}</span>
        </div>

        {analysis && <ReceiptAnalysisResults analysis={analysis} onApply={onApply} disabled={isAnalysing} />}

        <div className="form-actions">
          <button className="ghost-button ai-button" onClick={() => void analyse()} type="button" disabled={isAnalysing}>
            <AiSparkleIcon />
            {isAnalysing ? 'Analysing...' : 'Analyse again'}
          </button>
        </div>
      </section>
    </div>
  )
}
