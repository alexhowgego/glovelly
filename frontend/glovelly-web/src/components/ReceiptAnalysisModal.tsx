import { useEffect, useEffectEvent, useState } from 'react'
import {
  buildApiUrl,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
} from '../api'
import type { ReceiptAnalysisResult, ReceiptAnalysisTarget } from '../types'
import { AiSparkleIcon } from './AiSparkleIcon'

type ReceiptAnalysisModalProps = {
  onApply: (suggestions: { merchant: string | null; totalAmount: number | null }) => void
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

  const canApply = analysis?.status === 'Succeeded' && (analysis.merchant.value || analysis.totalAmount.value !== null)

  return (
    <div className="settings-overlay receipt-analysis-overlay" role="presentation">
      <section aria-labelledby="receipt-analysis-title" className="settings-modal receipt-analysis-modal panel" role="dialog" aria-modal="true">
        <div className="panel-heading">
          <div>
            <p className="section-label">Receipt analysis</p>
            <h2 id="receipt-analysis-title">Review suggestions</h2>
          </div>
          <button className="ghost-button" onClick={onClose} type="button" disabled={isAnalysing}>Close</button>
        </div>

        <div className="quick-receipt-summary">
          <strong>{target.fileName}</strong>
          <span>{status || 'Analysing receipt...'}</span>
        </div>

        {analysis?.status === 'Succeeded' ? (
          <div className="receipt-analysis-results">
            <Suggestion label="Merchant" field={analysis.merchant} applyable />
            <Suggestion label="Total" field={analysis.totalAmount} applyable />
            <Suggestion label="Transaction date" field={analysis.transactionDate} />
            <Suggestion label="Currency" field={analysis.currency} />
            <Suggestion label="Suggested category" field={analysis.suggestedCategory} />
            <p className="receipt-analysis-note">Date, currency, and category are review-only suggestions. They do not change the expense record.</p>
            {analysis.warnings.length > 0 ? (
              <div className="quick-receipt-warning">
                <strong>Check these details</strong>
                <span>{analysis.warnings.join(' ')}</span>
              </div>
            ) : null}
          </div>
        ) : null}

        <div className="form-actions">
          <button className="ghost-button ai-button" onClick={() => void analyse()} type="button" disabled={isAnalysing}>
            <AiSparkleIcon />
            {isAnalysing ? 'Analysing...' : 'Analyse again'}
          </button>
          {canApply ? (
            <button className="ghost-button" onClick={() => onApply({ merchant: analysis!.merchant.value, totalAmount: analysis!.totalAmount.value })} type="button" disabled={isAnalysing}>
              Use merchant and total
            </button>
          ) : null}
        </div>
      </section>
    </div>
  )
}

function Suggestion<T>({ label, field, applyable = false }: { label: string; field: { value: T | null; confidence: string }; applyable?: boolean }) {
  if (field.value === null || field.value === '') return null
  return (
    <div className="receipt-analysis-suggestion">
      <span>{label}{applyable ? ' (can use)' : ' (review only)'}</span>
      <strong>{String(field.value)}</strong>
      <small>{field.confidence.toLowerCase()} confidence</small>
    </div>
  )
}
