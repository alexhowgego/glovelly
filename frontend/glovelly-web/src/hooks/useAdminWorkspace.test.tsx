// @vitest-environment jsdom
import { act, renderHook, cleanup } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useAdminWorkspace } from './useAdminWorkspace'
import { fetchWithSession } from '../api'

vi.mock('../api', async original => ({ ...await original<typeof import('../api')>(), fetchWithSession: vi.fn() }))
vi.mock('../notifications', () => ({ notifications: { success: vi.fn(), error: vi.fn() } }))
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.resetAllMocks() })

describe('user access actions', () => {
  const user = { id: 'other', email: 'other@example.com', displayName: 'Other', googleSubject: 'subject', role: 'Admin', isActive: true,
    isEnrolled: true, createdUtc: '', lastLoginUtc: null, mileageRate: 0.5, passengerMileageRate: 0.06 }
  async function setup(currentUserId = 'me') {
    vi.mocked(fetchWithSession).mockResolvedValueOnce(new Response(JSON.stringify([user])))
    const saved = vi.fn()
    const hook = renderHook(() => useAdminWorkspace({ currentUserId, onAdminUserSaved: saved, onSessionExpired: vi.fn() }))
    await act(() => hook.result.current.loadAdminUsers())
    return { ...hook, saved }
  }
  it('does not request a prohibited self-deactivation or self-demotion', async () => {
    const hook = await setup(user.id)
    const confirm = vi.spyOn(window, 'confirm')
    await act(() => hook.result.current.changeAdminAccess('Admin', false))
    await act(() => hook.result.current.changeAdminAccess('User', true))
    expect(confirm).not.toHaveBeenCalled()
    expect(fetchWithSession).toHaveBeenCalledOnce()
  })
  it('cancelling confirmation leaves access unchanged', async () => {
    const hook = await setup()
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    await act(() => hook.result.current.changeAdminAccess('Admin', false))
    expect(fetchWithSession).toHaveBeenCalledOnce()
    expect(hook.result.current.selectedAdminUser?.isActive).toBe(true)
  })
  it('confirms updates, preserves unrelated fields and applies authoritative results', async () => {
    const hook = await setup()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    vi.mocked(fetchWithSession).mockResolvedValueOnce(new Response(JSON.stringify({ ...user, isActive: false })))
    await act(() => hook.result.current.changeAdminAccess('Admin', false))
    const request = vi.mocked(fetchWithSession).mock.calls[1][1]!
    expect(JSON.parse(request.body as string)).toMatchObject({ isActive: false, role: 'Admin', mileageRate: 0.5, passengerMileageRate: 0.06 })
    expect(hook.result.current.selectedAdminUser?.isActive).toBe(false)
    expect(hook.saved).toHaveBeenCalledOnce()
  })
  it('does not overwrite an open editor', async () => {
    const hook = await setup()
    act(() => hook.result.current.startAdminEdit())
    await act(() => hook.result.current.changeAdminAccess('Admin', false))
    expect(fetchWithSession).toHaveBeenCalledOnce()
  })
})
