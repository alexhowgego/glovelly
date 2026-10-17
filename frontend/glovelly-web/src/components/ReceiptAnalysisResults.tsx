import type { ReceiptAnalysisResult } from '../types'

import { receiptSuggestions } from '../gigEditing'
import type { ReceiptSuggestions } from '../gigEditing'
export function ReceiptAnalysisResults({ analysis, onApply, disabled = false }: { analysis: ReceiptAnalysisResult; onApply?: (values: ReceiptSuggestions) => void; disabled?: boolean }) {
  if (analysis.status !== 'Succeeded') return null
  const confirmedGbp = analysis.currency.value === 'GBP' && analysis.currency.confidence === 'High'
  const values = receiptSuggestions(analysis)
  return <div className="receipt-analysis-results">
    <Suggestion label="Merchant" field={analysis.merchant} applyable />
    <Suggestion label="Total" field={analysis.totalAmount} applyable={confirmedGbp} />
    <Suggestion label="Transaction date" field={analysis.transactionDate} />
    <Suggestion label="Currency" field={analysis.currency} />
    <Suggestion label="Suggested category" field={analysis.suggestedCategory} applyable />
    <p className="receipt-analysis-note">Date and currency are review-only. Expenses are recorded in GBP; only confidently GBP totals can be copied. Enter a GBP amount explicitly for foreign or unknown currency.</p>
    {analysis.warnings.length > 0 && <div className="quick-receipt-warning"><strong>Check these details</strong><span>{analysis.warnings.join(' ')}</span></div>}
    {!confirmedGbp && analysis.totalAmount.value !== null && <div className="quick-receipt-warning"><strong>Check the currency</strong><span>The original total is retained above. Enter the appropriate GBP expense amount yourself.</span></div>}
    {onApply && (values.merchant || values.totalAmount !== null || values.suggestedCategory) && <button type="button" className="ghost-button" disabled={disabled} onClick={() => onApply(values)}>Use suggestions</button>}
  </div>
}
function Suggestion<T>({ label, field, applyable = false }: { label: string; field: { value: T | null; confidence: string }; applyable?: boolean }) {
  if (field.value === null || field.value === '') return null
  return <div className="receipt-analysis-suggestion"><span>{label}{applyable ? ' (can use)' : ' (review only)'}</span><strong>{String(field.value)}</strong><small>{field.confidence.toLowerCase()} confidence</small></div>
}
