// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { fetchWithSession } from '../api'
import { PermissionManagement } from './PermissionManagement'

vi.mock('../api', async original => ({ ...await original<typeof import('../api')>(), fetchWithSession: vi.fn() }))
afterEach(() => { cleanup(); vi.resetAllMocks() })

describe('administrative permission management', () => {
  it('does not enable saving if assignments cannot be loaded', async () => {
    vi.mocked(fetchWithSession).mockResolvedValue(new Response('', { status: 403 }))
    render(<PermissionManagement userId="target" onSessionExpired={vi.fn()} onPermissionsChanged={vi.fn(async () => {})} />)
    await waitFor(() => expect(screen.getByRole('status').textContent).toContain('Unable to load'))
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Save permissions', hidden: true }).disabled).toBe(true)
  })

  it('saves additive assignments and shows the audit response', async () => {
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: 'target', permissionSets: ['UserAdministrator'] }])))
      .mockResolvedValueOnce(new Response('[]'))
      .mockResolvedValueOnce(new Response('{}'))
      .mockResolvedValueOnce(new Response(JSON.stringify([{
        id: 'audit', targetUserId: 'target', targetIdentity: 'target@example.test', actorIdentity: 'operator',
        changedAtUtc: '2026-01-01T00:00:00Z', previousSets: '[]', newSets: '["UserAdministrator"]', reason: null,
      }])))
    const refresh = vi.fn(async () => {})
    render(<PermissionManagement userId="target" onSessionExpired={vi.fn()} onPermissionsChanged={refresh} />)
    fireEvent.click(screen.getByText('Administrative permissions'))
    await waitFor(() => expect(screen.getByRole<HTMLInputElement>('checkbox', { name: 'User Administrator' }).checked).toBe(true))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Licence Administrator' }))
    fireEvent.click(screen.getByRole('button', { name: 'Save permissions' }))
    await waitFor(() => expect(refresh).toHaveBeenCalledOnce())
    const request = vi.mocked(fetchWithSession).mock.calls[2][1]!
    expect(JSON.parse(request.body as string).permissionSets).toEqual(['UserAdministrator', 'LicenseAdministrator'])
    fireEvent.click(screen.getByText('Permission history'))
    expect(screen.getByText('User Administrator added')).toBeTruthy()
    expect(screen.getByText(/operator ·/)).toBeTruthy()
  })

  it('keeps a last-super conflict visible without reporting success', async () => {
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: 'target', permissionSets: ['SuperAdministrator'] }])))
      .mockResolvedValueOnce(new Response('[]'))
      .mockResolvedValueOnce(new Response('', { status: 409 }))
    const refresh = vi.fn(async () => {})
    render(<PermissionManagement userId="target" onSessionExpired={vi.fn()} onPermissionsChanged={refresh} />)
    fireEvent.click(screen.getByText('Administrative permissions'))
    await waitFor(() => expect(screen.getByRole<HTMLInputElement>('checkbox', { name: 'Super Administrator' }).checked).toBe(true))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Super Administrator' }))
    fireEvent.click(screen.getByRole('button', { name: 'Save permissions' }))
    await waitFor(() => expect(screen.getByRole('status').textContent).toContain('Super Administrator must remain'))
    expect(refresh).not.toHaveBeenCalled()
  })

  it('keeps a successful save and refreshes the directory when history refresh fails', async () => {
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: 'target', permissionSets: [] }])))
      .mockResolvedValueOnce(new Response('[]'))
      .mockResolvedValueOnce(new Response('{}'))
      .mockResolvedValueOnce(new Response('', { status: 500 }))
    const refresh = vi.fn(async () => {})
    render(<PermissionManagement userId="target" onSessionExpired={vi.fn()} onPermissionsChanged={refresh} />)
    fireEvent.click(screen.getByText('Administrative permissions'))
    await waitFor(() => expect(screen.getByRole<HTMLInputElement>('checkbox', { name: 'User Administrator' }).disabled).toBe(false))
    fireEvent.click(screen.getByRole('checkbox', { name: 'User Administrator' }))
    fireEvent.click(screen.getByRole('button', { name: 'Save permissions' }))
    await waitFor(() => expect(screen.getByRole('status').textContent).toContain('Permissions saved, but'))
    expect(refresh).toHaveBeenCalledOnce()
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Save permissions' }).disabled).toBe(true)
  })

  it('shows revoked permission failure rather than reporting success', async () => {
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: 'target', permissionSets: [] }])))
      .mockResolvedValueOnce(new Response('[]'))
      .mockResolvedValueOnce(new Response('', { status: 403 }))
    const refresh = vi.fn(async () => {})
    render(<PermissionManagement userId="target" onSessionExpired={vi.fn()} onPermissionsChanged={refresh} />)
    fireEvent.click(screen.getByText('Administrative permissions'))
    await waitFor(() => expect(screen.getByRole<HTMLInputElement>('checkbox', { name: 'User Administrator' }).disabled).toBe(false))
    fireEvent.click(screen.getByRole('checkbox', { name: 'User Administrator' }))
    fireEvent.click(screen.getByRole('button', { name: 'Save permissions' }))
    await waitFor(() => expect(screen.getByRole('status').textContent).toContain('no longer have permission'))
    expect(refresh).not.toHaveBeenCalled()
  })

  it('enables save only for changed permissions and resets the draft', async () => {
    vi.mocked(fetchWithSession)
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: 'target', permissionSets: ['UserAdministrator'] }])))
      .mockResolvedValueOnce(new Response('[]'))
    render(<PermissionManagement userId="target" onSessionExpired={vi.fn()} onPermissionsChanged={vi.fn(async () => {})} />)
    fireEvent.click(screen.getByText('Administrative permissions'))
    await waitFor(() => expect(screen.getByRole<HTMLInputElement>('checkbox', { name: 'User Administrator' }).checked).toBe(true))
    const save = screen.getByRole<HTMLButtonElement>('button', { name: 'Save permissions' })
    expect(save.disabled).toBe(true)
    fireEvent.click(screen.getByRole('checkbox', { name: 'Licence Administrator' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Reason (optional)' }), { target: { value: 'Testing' } })
    expect(save.disabled).toBe(false)
    fireEvent.click(screen.getByRole('button', { name: 'Reset' }))
    expect(screen.getByRole<HTMLInputElement>('checkbox', { name: 'Licence Administrator' }).checked).toBe(false)
    expect(screen.getByRole<HTMLInputElement>('textbox', { name: 'Reason (optional)' }).value).toBe('')
    expect(save.disabled).toBe(true)
  })
})
