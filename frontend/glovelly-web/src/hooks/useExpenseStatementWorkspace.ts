import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  buildApiUrl,
  createBlobObjectUrl,
  downloadResponseBlob,
  fetchWithSession,
  getResponseErrorMessage,
  handleSessionExpired,
  jsonRequestInit,
} from '../api'
import type { Gig } from '../types'

type UseExpenseStatementWorkspaceOptions = {
  clientNamesById: ReadonlyMap<string, string>
  gigs: Gig[]
  selectedGig: Gig | null
  selectedGigs: Gig[]
  onSessionExpired: (message: string) => void
  setGigStatus: (status: string) => void
}

export function useExpenseStatementWorkspace({
  clientNamesById,
  gigs,
  selectedGig,
  selectedGigs,
  onSessionExpired,
  setGigStatus,
}: UseExpenseStatementWorkspaceOptions) {
  const [isExpenseStatementOpen, setIsExpenseStatementOpen] = useState(false)
  const [expenseStatementGigIds, setExpenseStatementGigIds] = useState<string[]>([])
  const [expenseStatementExpenseIds, setExpenseStatementExpenseIds] = useState<string[]>([])
  const [includeStatementReceiptAttachments, setIncludeStatementReceiptAttachments] =
    useState(true)
  const [includeStatementReceiptAppendix, setIncludeStatementReceiptAppendix] =
    useState(true)
  const [expenseStatementStatus, setExpenseStatementStatus] = useState('')
  const [expenseStatementPreviewUrl, setExpenseStatementPreviewUrl] =
    useState<string | null>(null)
  const [isExpenseStatementLoading, setIsExpenseStatementLoading] = useState(false)

  const clearExpenseStatementPreviewUrl = useCallback(() => {
    setExpenseStatementPreviewUrl((current) => {
      if (current) {
        window.URL.revokeObjectURL(current)
      }

      return null
    })
  }, [])

  useEffect(() => clearExpenseStatementPreviewUrl, [clearExpenseStatementPreviewUrl])

  const expenseStatementGigs = useMemo(() => {
    const statementGigIdSet = new Set(expenseStatementGigIds)

    return gigs
      .filter((gig) => statementGigIdSet.has(gig.id))
      .sort((left, right) => left.date.localeCompare(right.date))
  }, [expenseStatementGigIds, gigs])

  const expenseStatementSelectedExpenses = useMemo(() => {
    const selectedExpenseIdSet = new Set(expenseStatementExpenseIds)
    return expenseStatementGigs.flatMap((gig) =>
      gig.expenses.filter((expense) => selectedExpenseIdSet.has(expense.id))
    )
  }, [expenseStatementExpenseIds, expenseStatementGigs])

  const expenseStatementTotal = expenseStatementSelectedExpenses.reduce(
    (total, expense) => total + expense.amount,
    0
  )

  const expenseStatementReceiptCount = expenseStatementSelectedExpenses.reduce(
    (count, expense) => count + expense.attachments.length,
    0
  )

  const resetExpenseStatementWorkspace = useCallback(() => {
    setIsExpenseStatementOpen(false)
    setExpenseStatementGigIds([])
    setExpenseStatementExpenseIds([])
    setExpenseStatementStatus('')
    clearExpenseStatementPreviewUrl()
    setIsExpenseStatementLoading(false)
  }, [clearExpenseStatementPreviewUrl])

  const openExpenseStatement = async () => {
    const targetGigs = selectedGigs.length > 0 ? selectedGigs : selectedGig ? [selectedGig] : []
    const clientIds = new Set(targetGigs.map((gig) => gig.clientId))

    if (targetGigs.length === 0) {
      setGigStatus('Select a gig before generating an expense statement.')
      return
    }

    if (clientIds.size > 1) {
      setGigStatus('Expense statements can only include gigs for one client.')
      return
    }

    if (!targetGigs.some((gig) => gig.expenses.length > 0)) {
      setGigStatus('Add expenses before generating an expense statement.')
      return
    }

    const defaultExpenseIds = targetGigs.flatMap((gig) =>
      gig.expenses
        .filter((expense) => expense.reimbursementStatus === 'Unreimbursed')
        .map((expense) => expense.id)
    )

    setExpenseStatementGigIds(targetGigs.map((gig) => gig.id))
    setExpenseStatementExpenseIds(defaultExpenseIds)
    setIncludeStatementReceiptAttachments(true)
    setIncludeStatementReceiptAppendix(true)
    clearExpenseStatementPreviewUrl()
    setExpenseStatementStatus(
      defaultExpenseIds.length > 0
        ? 'Review expenses before downloading the PDF.'
        : 'All expenses are reimbursed or not claimable. Include at least one to download a statement.'
    )
    setIsExpenseStatementOpen(true)
  }

  const closeExpenseStatement = () => {
    setIsExpenseStatementOpen(false)
    setExpenseStatementStatus('')
    clearExpenseStatementPreviewUrl()
  }

  const toggleExpenseStatementExpense = (expenseId: string) => {
    setExpenseStatementExpenseIds((current) =>
      current.includes(expenseId)
        ? current.filter((value) => value !== expenseId)
        : [...current, expenseId]
    )
    clearExpenseStatementPreviewUrl()
  }

  const updateIncludeStatementReceiptAttachments = (value: boolean) => {
    setIncludeStatementReceiptAttachments(value)
    if (!value) {
      setIncludeStatementReceiptAppendix(false)
    }
    clearExpenseStatementPreviewUrl()
  }

  const updateIncludeStatementReceiptAppendix = (value: boolean) => {
    setIncludeStatementReceiptAppendix(value)
    clearExpenseStatementPreviewUrl()
  }

  const buildExpenseStatementPayload = () => {
    const clientId = expenseStatementGigs[0]?.clientId ?? ''

    return {
      clientId,
      gigIds: expenseStatementGigIds,
      expenseIds: expenseStatementExpenseIds,
      includeReceiptAttachments: includeStatementReceiptAttachments,
      includeReceiptAppendix:
        includeStatementReceiptAttachments && includeStatementReceiptAppendix,
      includeReimbursedExpenses: true,
    }
  }

  const previewExpenseStatement = async () => {
    if (expenseStatementGigs.length === 0 || expenseStatementExpenseIds.length === 0) {
      clearExpenseStatementPreviewUrl()
      setExpenseStatementStatus('Select at least one expense to preview the statement.')
      return null
    }

    setIsExpenseStatementLoading(true)
    setExpenseStatementStatus('Preparing PDF preview...')

    try {
      const response = await fetchWithSession(
        buildApiUrl('/expense-statements/pdf'),
        jsonRequestInit('POST', buildExpenseStatementPayload())
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to keep managing gigs.'
        )
      ) {
        return null
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(
            response,
            'Unable to preview the expense statement PDF.'
          )
        )
      }

      const previewUrl = await createBlobObjectUrl(response)
      setExpenseStatementPreviewUrl((current) => {
        if (current) {
          window.URL.revokeObjectURL(current)
        }

        return previewUrl
      })
      setExpenseStatementStatus('PDF preview ready.')
      return previewUrl
    } catch (error) {
      clearExpenseStatementPreviewUrl()
      setExpenseStatementStatus(
        error instanceof Error ? error.message : 'Unable to preview the expense statement PDF.'
      )
      return null
    } finally {
      setIsExpenseStatementLoading(false)
    }
  }

  const downloadExpenseStatementPdf = async () => {
    if (expenseStatementGigs.length === 0 || expenseStatementExpenseIds.length === 0) {
      setExpenseStatementStatus('Select at least one expense to download a statement.')
      return
    }

    const clientName =
      clientNamesById.get(expenseStatementGigs[0].clientId) ?? 'Client'
    const fallbackFilename = `Expense-Statement-${clientName}.pdf`
    setIsExpenseStatementLoading(true)
    setExpenseStatementStatus('Preparing expense statement PDF...')

    try {
      const response = await fetchWithSession(
        buildApiUrl('/expense-statements/pdf'),
        jsonRequestInit('POST', buildExpenseStatementPayload())
      )

      if (
        handleSessionExpired(
          response,
          onSessionExpired,
          'Your session expired. Sign in again to keep managing gigs.'
        )
      ) {
        return
      }

      if (!response.ok) {
        throw new Error(
          await getResponseErrorMessage(
            response,
            'Unable to download the expense statement PDF.'
          )
        )
      }

      const filename = await downloadResponseBlob(response, fallbackFilename)
      setExpenseStatementStatus(`Downloaded ${filename}.`)
      setGigStatus(`Downloaded ${filename}.`)
    } catch (error) {
      setExpenseStatementStatus(
        error instanceof Error
          ? error.message
          : 'Unable to download the expense statement PDF.'
      )
    } finally {
      setIsExpenseStatementLoading(false)
    }
  }

  return {
    closeExpenseStatement,
    downloadExpenseStatementPdf,
    expenseStatementExpenseIds,
    expenseStatementGigs,
    expenseStatementPreviewUrl,
    expenseStatementReceiptCount,
    expenseStatementStatus,
    expenseStatementTotal,
    includeStatementReceiptAppendix,
    includeStatementReceiptAttachments,
    isExpenseStatementLoading,
    isExpenseStatementOpen,
    openExpenseStatement,
    previewExpenseStatement,
    resetExpenseStatementWorkspace,
    setIncludeStatementReceiptAppendix: updateIncludeStatementReceiptAppendix,
    setIncludeStatementReceiptAttachments: updateIncludeStatementReceiptAttachments,
    toggleExpenseStatementExpense,
  }
}
