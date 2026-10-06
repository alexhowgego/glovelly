import type { GigExternalResourcePurpose, GigExternalResourceType, ReceiptAnalysisResult } from './types'

export const resourceTypeOptions: { value: GigExternalResourceType; label: string }[] = [
  { value: 'GoogleSheet', label: 'Google Sheet' }, { value: 'GoogleDoc', label: 'Google Doc' }, { value: 'Url', label: 'URL' },
  { value: 'Email', label: 'Email' }, { value: 'File', label: 'File' }, { value: 'Other', label: 'Other' },
]
export const resourcePurposeOptions: { value: GigExternalResourcePurpose; label: string }[] = [
  { value: 'SetList', label: 'Set list' }, { value: 'GigPlan', label: 'Gig plan' }, { value: 'Contract', label: 'Contract' }, { value: 'Travel', label: 'Travel' }, { value: 'Other', label: 'Other' },
]
export const expenseCategories = ['Travel', 'Meals', 'Accommodation', 'Equipment', 'Other'] as const
export type ExpenseCategory = typeof expenseCategories[number]
export const normalizeExpenseCategory = (value: string | null) => expenseCategories.find(category => category.toLowerCase() === value?.toLowerCase()) ?? ''
export type ReceiptSuggestions = { merchant: string | null; totalAmount: number | null; suggestedCategory: string | null }
export function receiptSuggestions(analysis: ReceiptAnalysisResult): ReceiptSuggestions {
  return { merchant: analysis.merchant.value, totalAmount: analysis.currency.value === 'GBP' && analysis.currency.confidence === 'High' ? analysis.totalAmount.value : null, suggestedCategory: analysis.suggestedCategory.value }
}
