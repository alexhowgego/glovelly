// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchWithSession } from '../api'
import { notifications } from '../notifications'
import { LicencesSection } from './LicencesSection'

vi.mock('../api', async importOriginal => ({
  ...await importOriginal<typeof import('../api')>(), fetchWithSession: vi.fn(),
}))
vi.mock('../notifications', () => ({ notifications: { success: vi.fn() } }))

const directory = {
  users: [{
    id: 'user', email: 'musician@example.test', displayName: 'Musician', isActive: true, isEnrolled: true,
    version: 'version-one', entitlements: {
      effectivePlan: { code: 'free', displayName: 'Free' }, assignedPlan: null,
      assignmentStatus: 'Default', endsAtUtc: null, features: { paidIncomeSummary: false },
      invoiceIssuance: { used: 2, limit: 2, remaining: 0, resetsAtUtc: '2026-12-01T00:00:00Z' },
    },
  }], total: 1, page: 1, pageSize: 25,
}
const ok = (body: unknown) => new Response(JSON.stringify(body), { headers: { 'content-type': 'application/json' } })
const expired = vi.fn()
const savedDirectory = {
  ...directory,
  users: [{
    ...directory.users[0], version: 'version-two',
    entitlements: {
      ...directory.users[0].entitlements,
      assignedPlan: { code: 'pro', displayName: 'Pro' },
      effectivePlan: { code: 'pro', displayName: 'Pro' },
      assignmentStatus: 'Active',
      invoiceIssuance: { ...directory.users[0].entitlements.invoiceIssuance, limit: null, remaining: null },
    },
  }],
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false })))
  vi.stubGlobal('confirm', vi.fn(() => true))
})
afterEach(() => { cleanup(); vi.unstubAllGlobals() })

async function openEditor() {
  fireEvent.click(await screen.findByRole('button', { name: 'Musician' }))
  const edit = screen.getByRole('button', { name: 'Edit licence' })
  await waitFor(() => expect((edit as HTMLButtonElement).disabled).toBe(false))
  fireEvent.click(edit)
  return screen.getByRole('form', { name: 'Update licence' })
}

describe('LicencesSection', () => {
  it('loads its own directory without ManageUsers endpoints and shows usage', async () => {
    vi.mocked(fetchWithSession).mockResolvedValue(ok(directory))
    render(<LicencesSection onSessionExpired={expired} />)
    const row = await screen.findByRole('button', { name: 'Musician' })
    expect(row.classList.contains('compact-record-row')).toBe(true)
    expect(screen.getByText('2 / 2')).toBeTruthy()
    expect(screen.getByText('Select a user to review their licence.')).toBeTruthy()
    expect(screen.queryByRole('table')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Previous' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Next' })).toBeNull()
    expect(fetchWithSession).toHaveBeenCalledWith(expect.stringContaining('/admin/licences/users?'))
    expect(fetchWithSession).not.toHaveBeenCalledWith(expect.stringContaining('/admin/users'))
  })

  it('selects an overview first and opens the shared editor only through the docked action', async () => {
    vi.mocked(fetchWithSession).mockResolvedValueOnce(ok(directory)).mockResolvedValueOnce(ok([]))
    const view = render(<LicencesSection onSessionExpired={expired} />)
    fireEvent.click(await screen.findByRole('button', { name: 'Musician' }))
    const overview = view.container.querySelector('.record-overview')!
    expect(overview.querySelector('.overview-body')).toBeTruthy()
    expect(overview.querySelector('.overview-facts')).toBeTruthy()
    expect(screen.queryByRole('form', { name: 'Update licence' })).toBeNull()
    const edit = within(overview as HTMLElement).getByRole('button', { name: 'Edit licence' })
    expect(edit.closest('.overview-dock')).toBeTruthy()
    await waitFor(() => expect((edit as HTMLButtonElement).disabled).toBe(false))
    fireEvent.click(edit)
    expect(view.container.querySelector('.editor-slot.open > .editor-panel.panel')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Save and close' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Save' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Discard changes' })).toBeTruthy()
    const editor = screen.getByRole('form', { name: 'Update licence' })
    const plan = within(editor).getByRole('combobox', { name: 'Plan' }) as HTMLSelectElement
    expect(within(editor).getByLabelText('Plan', { exact: true })).toBe(plan)
    expect(plan.labels?.[0].htmlFor).toBe(plan.id)
    expect(document.getElementById(plan.getAttribute('aria-labelledby')!)?.textContent).toBe('Plan')
  })

  it('saves the current version and retains the refreshed overview when the shared editor closes', async () => {
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(ok(directory)).mockResolvedValueOnce(ok([]))
      .mockResolvedValueOnce(ok({ result: 'Changed' })).mockResolvedValueOnce(ok(savedDirectory))
      .mockResolvedValueOnce(ok([]))
    render(<LicencesSection onSessionExpired={expired} />)
    await openEditor()
    fireEvent.change(screen.getByLabelText('Plan'), { target: { value: 'pro' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save and close' }))
    await waitFor(() => expect(notifications.success).toHaveBeenCalledTimes(1))
    const update = vi.mocked(fetchWithSession).mock.calls.find(([, init]) => init?.method === 'PUT')!
    expect(JSON.parse(update[1]!.body as string)).toMatchObject({ planCode: 'pro', version: 'version-one', endsAtUtc: null })
    expect(screen.queryByRole('form', { name: 'Update licence' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Musician' }).getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByText('Effective plan').nextElementSibling?.textContent).toBe('Pro')
  })

  it('keeps conflict feedback inline with the editor and does not notify success', async () => {
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(ok(directory)).mockResolvedValueOnce(ok([]))
      .mockResolvedValueOnce(new Response('{}', { status: 409 }))
    render(<LicencesSection onSessionExpired={expired} />)
    await openEditor()
    fireEvent.click(screen.getByRole('button', { name: 'Save and close' }))
    expect(await screen.findByRole('alert')).toHaveProperty('textContent', expect.stringContaining('Reload'))
    expect(screen.getByRole('form', { name: 'Update licence' })).toBeTruthy()
    expect(notifications.success).not.toHaveBeenCalled()
  })

  it('accepts an existing UTC-offset expiry without requiring the user to retype it', async () => {
    const grantDirectory = {
      ...savedDirectory,
      users: [{ ...savedDirectory.users[0], entitlements: {
        ...savedDirectory.users[0].entitlements, endsAtUtc: '2030-12-31T23:00:00+00:00',
      } }],
    }
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(ok(grantDirectory)).mockResolvedValueOnce(ok([]))
      .mockResolvedValueOnce(new Response('{}', { status: 409 }))
    render(<LicencesSection onSessionExpired={expired} />)
    await openEditor()
    expect((screen.getByLabelText('Expiry (UTC, optional)') as HTMLInputElement).value).toBe('2030-12-31T23:00:00Z')
    fireEvent.click(screen.getByRole('button', { name: 'Save and close' }))
    await screen.findByRole('alert')
    const update = vi.mocked(fetchWithSession).mock.calls.find(([, init]) => init?.method === 'PUT')!
    expect(JSON.parse(update[1]!.body as string).endsAtUtc).toBe('2030-12-31T23:00:00.000Z')
  })

  it('supports Save without closing, shows inline success and uses the refreshed assignment version next time', async () => {
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(ok(directory)).mockResolvedValueOnce(ok([]))
      .mockResolvedValueOnce(ok({ result: 'Changed' })).mockResolvedValueOnce(ok(savedDirectory))
      .mockResolvedValueOnce(ok([]))
      .mockResolvedValueOnce(new Response('{}', { status: 409 }))
    render(<LicencesSection onSessionExpired={expired} />)
    await openEditor()
    fireEvent.change(screen.getByLabelText('Plan'), { target: { value: 'pro' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByRole('status')).toHaveProperty('textContent', expect.stringContaining('Licence saved'))
    expect(screen.getByRole('form', { name: 'Update licence' })).toBeTruthy()
    expect(notifications.success).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText('Plan'), { target: { value: 'solo' } })
    expect(screen.queryByRole('status')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    await screen.findByRole('alert')
    const updates = vi.mocked(fetchWithSession).mock.calls.filter(([, init]) => init?.method === 'PUT')
    expect(JSON.parse(updates[1][1]!.body as string)).toMatchObject({ planCode: 'solo', version: 'version-two' })
  })

  it('protects unsaved changes and restores the selected assignment on reopening', async () => {
    vi.mocked(fetchWithSession).mockResolvedValueOnce(ok(directory)).mockResolvedValueOnce(ok([]))
    render(<LicencesSection onSessionExpired={expired} />)
    await openEditor()
    fireEvent.change(screen.getByLabelText('Plan'), { target: { value: 'pro' } })
    vi.mocked(window.confirm).mockReturnValueOnce(false)
    fireEvent.click(screen.getByRole('button', { name: 'Discard changes' }))
    expect(screen.getByRole('form', { name: 'Update licence' })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Discard changes' }))
    expect(screen.queryByRole('form', { name: 'Update licence' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Edit licence' }))
    expect((screen.getByLabelText('Plan') as HTMLSelectElement).value).toBe('free')
    expect(fetchWithSession).toHaveBeenCalledTimes(2)
  })

  it('retains the saved reason if the post-save history refresh fails', async () => {
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(ok(directory)).mockResolvedValueOnce(ok([]))
      .mockResolvedValueOnce(ok({ result: 'Changed' })).mockResolvedValueOnce(ok(savedDirectory))
      .mockResolvedValueOnce(new Response('', { status: 503 }))
    render(<LicencesSection onSessionExpired={expired} />)
    await openEditor()
    fireEvent.change(screen.getByLabelText('Reason (optional)'), { target: { value: 'Tester' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    await screen.findByRole('status')
    expect((screen.getByLabelText('Reason (optional)') as HTMLInputElement).value).toBe('Tester')
    expect(screen.getByRole('alert').textContent).toContain('Unable to load licence history')
    expect(notifications.success).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Discard changes' }))
    fireEvent.click(screen.getByRole('button', { name: 'Edit licence' }))
    expect((screen.getByLabelText('Reason (optional)') as HTMLInputElement).value).toBe('Tester')
  })

  it('protects dirty selection changes and keeps the shared editor open for a newly selected user', async () => {
    const otherUser = { ...directory.users[0], id: 'other', displayName: 'Second musician', email: 'second@example.test' }
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(ok({ ...directory, users: [...directory.users, otherUser], total: 2 }))
      .mockResolvedValueOnce(ok([])).mockResolvedValueOnce(ok([]))
    render(<LicencesSection onSessionExpired={expired} />)
    await openEditor()
    fireEvent.change(screen.getByLabelText('Plan'), { target: { value: 'pro' } })
    vi.mocked(window.confirm).mockReturnValueOnce(false)
    fireEvent.click(screen.getByRole('button', { name: 'Second musician' }))
    expect((screen.getByLabelText('Plan') as HTMLSelectElement).value).toBe('pro')
    expect(fetchWithSession).toHaveBeenCalledTimes(2)
    fireEvent.click(screen.getByRole('button', { name: 'Second musician' }))
    await waitFor(() => expect((screen.getByRole('button', { name: 'Save' }) as HTMLButtonElement).disabled).toBe(false))
    expect(screen.getByRole('form', { name: 'Update licence' })).toBeTruthy()
    expect((screen.getByLabelText('Plan') as HTMLSelectElement).value).toBe('free')
    expect(screen.getByRole('button', { name: 'Second musician' }).getAttribute('aria-pressed')).toBe('true')
  })
})
