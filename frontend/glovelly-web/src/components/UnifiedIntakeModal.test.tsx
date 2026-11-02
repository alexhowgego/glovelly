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
  const workflow = useUnifiedIntake({ onApplied, onReviewImport: vi.fn(), onSessionExpired: vi.fn() })
  return <><button onClick={() => void workflow.open()}>Add</button><button onClick={workflow.reset}>End session</button><UnifiedIntakeModal workflow={workflow} clientNamesById={new Map()} onGoToGig={vi.fn()} />{workflow.state.phase === 'review' && <div>Saved attachment review</div>}</>
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

  it('prepares a manual gig draft without choosing or creating a gig', async () => {
    const reviewImport = vi.fn()
    function GigHarness() {
      const workflow = useUnifiedIntake({ onApplied: vi.fn(), onReviewImport: reviewImport, onSessionExpired: vi.fn() })
      return <><button onClick={() => void workflow.open()}>Add</button><UnifiedIntakeModal workflow={workflow} clientNamesById={new Map()} onGoToGig={vi.fn()} /></>
    }
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(intake), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...intake, intentOverride: 'Gigs' })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ intakeId: 'intake', batchId: 'batch' })))
    render(<GigHarness />)
    await openFile()
    submitFile()
    fireEvent.change(await screen.findByLabelText('Treat as'), { target: { value: 'Gigs' } })
    fireEvent.click(await screen.findByRole('button', { name: 'Prepare manual gig draft' }))
    await screen.findByText('Draft batch prepared')
    expect(reviewImport).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Review import' }))
    expect(reviewImport).toHaveBeenCalledWith('batch')
  })

  it('offers explicit navigation after attaching a resource without interpreting a set list', async () => {
    const goToGig = vi.fn()
    function ResourceHarness() {
      const workflow = useUnifiedIntake({ onApplied: vi.fn(), onReviewImport: vi.fn(), onSessionExpired: vi.fn() })
      return <><button onClick={() => void workflow.open()}>Add</button><UnifiedIntakeModal workflow={workflow} clientNamesById={new Map()} onGoToGig={goToGig} /></>
    }
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...intake, application: { ...saved, kind: 'Resource' } }), { status: 201 }))
    render(<ResourceHarness />)
    await openFile()
    submitFile()
    fireEvent.click(await screen.findByRole('button', { name: 'Go to gig' }))
    expect(goToGig).toHaveBeenCalledWith('gig')
    expect(fetchSession.mock.calls.every(([url]) => !String(url).includes('setlist'))).toBe(true)
  })

  it('recovers the same draft batch after a lost preparation response', async () => {
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(intake), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...intake, intentOverride: 'Gigs' })))
      .mockRejectedValueOnce(new Error('Network interrupted'))
      .mockResolvedValueOnce(new Response(JSON.stringify({ intakeId: 'intake', batchId: 'original-batch' })))
    render(<Harness />)
    await openFile()
    submitFile()
    fireEvent.change(await screen.findByLabelText('Treat as'), { target: { value: 'Gigs' } })
    fireEvent.click(await screen.findByRole('button', { name: 'Prepare manual gig draft' }))
    await screen.findByText('Draft batch prepared')
    expect(fetchSession.mock.calls.filter(([path]) => String(path).includes('gig-drafts/manual'))).toHaveLength(1)
  })

  it('keeps a PDF source visible while background analysis finishes', async () => {
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...intake, analysisState: 'Pending', analysisInBackground: true }), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...intake, analysisInBackground: false })))
    render(<Harness />)
    await openFile()
    submitFile()
    expect(await screen.findByLabelText('Upload in progress')).toBeTruthy()
    expect(screen.getByText('receipt.pdf')).toBeTruthy()
    expect(await screen.findByRole('button', { name: 'Attach receipt' }, { timeout: 3000 })).toBeTruthy()
  })

  it('offers gig import without defaulting an unknown result to receipt', async () => {
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...intake, analysisState: 'Failed', proposedIntent: 'Unknown', confidence: 'None' }), { status: 201 }))
    render(<Harness />)
    await openFile()
    submitFile()
    const select = await screen.findByLabelText('Treat as') as HTMLSelectElement
    expect(select.value).toBe('')
    expect(screen.getByRole('option', { name: 'Gig import' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Attach receipt' })).toBeNull()
  })

  it('offers gig import when a Google Doc could not be read', async () => {
    const doc = { ...intake, sourceType: 'Url', sourceName: 'Google Doc', sourceUrl: 'https://docs.google.com/document/d/example/edit', sourceText: null, analysisState: 'Failed', failureCode: 'document_connection_required', proposedIntent: 'Unknown', confidence: 'None' }
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(doc), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...doc, intentOverride: 'Gigs' })))
    render(<Harness />)
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))
    fireEvent.click(await screen.findByRole('button', { name: /^URL/ }))
    fireEvent.change(screen.getByLabelText('URL'), { target: { value: doc.sourceUrl } })
    fireEvent.click(screen.getByRole('button', { name: 'Upload and analyse' }))
    fireEvent.change(await screen.findByLabelText('Treat as'), { target: { value: 'Gigs' } })
    expect(await screen.findByText(/Read the Google Doc first/)).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Prepare gig drafts' }) as HTMLButtonElement).disabled).toBe(true)
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

  it('gives a replacement file a new identity after interrupted submission', async () => {
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockRejectedValueOnce(new Error('Lost response'))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...intake, sourceName: 'replacement.pdf' })))
    render(<Harness />)
    await openFile()
    submitFile()
    await screen.findByText('Lost response')
    fireEvent.change(screen.getByLabelText('Choose photo or file'), { target: { files: [new File(['different source'], 'replacement.pdf', { type: 'application/pdf' })] } })
    await screen.findByText('replacement.pdf')
    const first = fetchSession.mock.calls[1][1].body as FormData
    const second = fetchSession.mock.calls[4][1].body as FormData
    expect(first.get('intakeId')).not.toBe(second.get('intakeId'))
    expect((second.get('file') as File).name).toBe('replacement.pdf')
  })

  it('shows non-blocking proximity and invoice guidance without forcing review', async () => {
    fetchSession.mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...intake, application: { ...saved, hasNearbyCandidates: true, invoices: [{ invoiceNumber: 'INV-1', documentState: 'Failed' }] } })))
    render(<Harness />)
    await openFile(); submitFile()
    await screen.findByText('Attached to Tonight at the venue')
    expect(screen.getByText('Check the gig before moving on.')).toBeTruthy()
    expect(screen.getByText(/INV-1 PDF is unavailable/)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Done' }).hasAttribute('disabled')).toBe(false)
    expect(screen.queryByText('Saved attachment review')).toBeNull()
  })
})
