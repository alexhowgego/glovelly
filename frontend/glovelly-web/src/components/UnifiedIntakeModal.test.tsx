// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useUnifiedIntake } from '../hooks/useUnifiedIntake'
import { UnifiedIntakeModal } from './UnifiedIntakeModal'
import type { Intake, IntakeApplication } from '../types'

const fetchSession = vi.fn()
vi.mock('../api', () => ({
  buildApiUrl: (path: string) => path, fetchWithSession: (...args: unknown[]) => fetchSession(...args),
  getResponseErrorMessage: async () => 'Request failed.', handleSessionExpired: () => false,
  jsonRequestInit: (method: string, body: unknown) => ({ method, body: JSON.stringify(body) }),
}))

const saved = {
  kind: 'Receipt', expenseId: 'expense', attachmentId: 'attachment', resource: null,
  gig: { id: 'gig', title: 'Tonight at the venue', expenses: [], externalResources: [] },
} as unknown as IntakeApplication
const intake = {
  id: 'intake', sourceType: 'File', sourceName: 'receipt.pdf', sourceUrl: null, sourceText: null,
  analysisState: 'Succeeded', failureMessage: null, proposedIntent: 'Receipt', suggestedResourceType: null,
  confidence: 'High', evidence: [{ label: 'Merchant', value: 'Station Cafe' }], candidates: [{ id: 'gig', title: 'Tonight at the venue', date: '2026-01-01', type: 'Other', clientId: 'client', daysFromToday: 0 }],
  hasMoreCandidates: false, candidateContinuation: null, application: null, applicationError: null,
} as Intake

function Harness({ onApplied = vi.fn() }: { onApplied?: (value: IntakeApplication) => void }) {
  const workflow = useUnifiedIntake({ onApplied, onSessionExpired: vi.fn() })
  return <><button onClick={() => void workflow.open()}>Add</button><button onClick={workflow.reset}>End session</button><UnifiedIntakeModal workflow={workflow} clientNamesById={new Map()} />{workflow.state.phase === 'review' && <div>Saved attachment review</div>}</>
}

async function openFile() {
  fireEvent.click(screen.getByRole('button', { name: 'Add' }))
  await waitFor(() => expect(screen.getByRole('button', { name: /Photo or file/ }).hasAttribute('disabled')).toBe(false))
  expect(screen.queryByLabelText('Choose photo or file')).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: /Photo or file/ }))
}
function submitFile() {
  fireEvent.change(screen.getByLabelText('Choose photo or file'), { target: { files: [new File(['receipt'], 'receipt.pdf', { type: 'application/pdf' })] } })
}

describe('Unified intake journey', () => {
  afterEach(() => { cleanup(); vi.resetAllMocks(); sessionStorage.clear() })

  it('keeps automatic attachment inside upload and permits Done without review', async () => {
    const applied = vi.fn()
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...intake, application: saved }), { status: 201 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    render(<Harness onApplied={applied} />)
    await openFile()
    submitFile()
    await screen.findByText('Attached to Tonight at the venue')
    expect(screen.getByRole('dialog', { name: 'Add to Glovelly' })).toBeTruthy()
    expect(screen.queryByText('Saved attachment review')).toBeNull()
    expect(applied).toHaveBeenCalledOnce()
    expect(fetchSession).toHaveBeenCalledTimes(2) // no client auto-apply request
    fireEvent.click(screen.getByRole('button', { name: 'Done' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))
    await screen.findByRole('button', { name: /Photo or file/ })
    expect(screen.queryByText('Attached to Tonight at the venue')).toBeNull()
  })

  it('explicit application reaches the same attached state and review only opens on request', async () => {
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(intake), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(saved)))
    render(<Harness />)
    await openFile()
    submitFile()
    fireEvent.click(await screen.findByRole('button', { name: 'Attach receipt' }))
    await screen.findByText('Attached to Tonight at the venue')
    expect(screen.queryByText('Saved attachment review')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Review attachment' }))
    expect(screen.getByText('Saved attachment review')).toBeTruthy()
    expect(screen.queryByRole('dialog', { name: 'Add to Glovelly' })).toBeNull()
  })

  it('recovers a save after a lost response without resubmitting the source', async () => {
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockRejectedValueOnce(new Error('Network interrupted'))
      .mockResolvedValueOnce(new Response(JSON.stringify(saved)))
    render(<Harness />)
    await openFile()
    submitFile()
    await screen.findByText('Attached to Tonight at the venue')
    expect(fetchSession.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
  })

  it('retains an unapplied source for resume after close', async () => {
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(intake), { status: 201 }))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(intake)))
    render(<Harness />)
    await openFile()
    submitFile()
    await screen.findByRole('button', { name: 'Attach receipt' })
    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Resume unfinished upload' }))
    expect(screen.getByText('receipt.pdf')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Attach receipt' })).toBeTruthy()
  })

  it('ignores an in-flight save result after the authenticated session ends', async () => {
    let finish: (response: Response) => void = () => {}
    const pending = new Promise<Response>(resolve => { finish = resolve })
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 })).mockReturnValueOnce(pending)
    const applied = vi.fn()
    render(<Harness onApplied={applied} />)
    await openFile()
    submitFile()
    fireEvent.click(screen.getByRole('button', { name: 'End session' }))
    await act(async () => { finish(new Response(JSON.stringify({ ...intake, application: saved }))); await pending })
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(applied).not.toHaveBeenCalled()
    expect(fetchSession).toHaveBeenCalledTimes(2)
  })
})
