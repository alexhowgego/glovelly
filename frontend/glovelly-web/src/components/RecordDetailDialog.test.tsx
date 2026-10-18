// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { useState } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { RecordDetailDialog } from './RecordDetailDialog'
import { useRecordDetail } from '../hooks/useRecordDetail'
import { notifications } from '../notifications'

beforeEach(() => {
  // jsdom has dialog elements but does not implement the browser top layer.
  Object.defineProperties(HTMLDialogElement.prototype, {
    showModal: { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } },
    close: { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } },
  })
})
afterEach(() => {
  cleanup()
  document.body.style.overflow = ''
  vi.restoreAllMocks()
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal')
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'close')
})

function Harness({ recordId = 'a', text = 'Complete notes', busy = false, warn = false }: { recordId?: string; text?: string; busy?: boolean; warn?: boolean }) {
  const detail = useRecordDetail<'notes'>(recordId, warn)
  const [child, setChild] = useState(false)
  return <>
    <button onClick={event => detail.open('notes', event)}>Read notes</button>
    {detail.kind && <RecordDetailDialog title="Notes" onClose={detail.close} returnFocus={detail.trigger} canClose={() => !busy}>
      <p>{text}</p>
      <button onClick={() => setChild(true)}>Edit expense</button>
      {child && <section role="dialog" aria-label="Expense editor"><input aria-label="Amount" /><button data-dialog-close onClick={() => setChild(false)}>Close child</button></section>}
    </RecordDetailDialog>}
  </>
}

describe('record detail dialogs', () => {
  it('focuses a named modal, locks scrolling, and restores the trigger without scrolling', () => {
    document.body.style.overflow = 'auto'
    render(<Harness />)
    const trigger = screen.getByRole('button', { name: 'Read notes' })
    trigger.focus()
    fireEvent.click(trigger)
    const dialog = screen.getByRole('dialog', { name: 'Notes' })
    expect(dialog.contains(document.activeElement)).toBe(true)
    expect(document.body.style.overflow).toBe('hidden')
    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
    expect(document.body.style.overflow).toBe('auto')
  })

  it('uses the same busy guard for close and Escape', () => {
    const { rerender } = render(<Harness busy />)
    fireEvent.click(screen.getByText('Read notes'))
    const dialog = screen.getByRole('dialog', { name: 'Notes' })
    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    fireEvent(dialog, new Event('cancel', { cancelable: true }))
    expect(screen.getByRole('dialog', { name: 'Notes' })).toBe(dialog)
    rerender(<Harness />)
    fireEvent(dialog, new Event('cancel', { cancelable: true }))
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('suspends parent controls while a child is active and Escape closes only the child', async () => {
    render(<Harness />)
    fireEvent.click(screen.getByText('Read notes'))
    const edit = screen.getByRole('button', { name: 'Edit expense' })
    const body = screen.getByRole('dialog', { name: 'Notes' }).querySelector<HTMLElement>('.record-detail-dialog-body')!
    body.scrollTop = 73
    edit.focus()
    fireEvent.click(edit)
    const parent = screen.getByRole('dialog', { name: 'Notes' })
    const child = screen.getByRole('dialog', { name: 'Expense editor' })
    await waitFor(() => expect(document.activeElement).toBe(child))
    body.scrollTop = 120
    expect(edit.inert).toBe(true)
    fireEvent(parent, new Event('cancel', { cancelable: true }))
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Expense editor' })).toBeNull())
    expect(screen.getByRole('dialog', { name: 'Notes' })).toBe(parent)
    expect(edit.inert).not.toBe(true)
    expect(document.activeElement).toBe(edit)
    expect(body.scrollTop).toBe(73)
  })

  it('updates authoritative content and invalidates rather than retargets on record changes', () => {
    const { rerender } = render(<Harness />)
    fireEvent.click(screen.getByText('Read notes'))
    rerender(<Harness text="Updated notes" />)
    expect(screen.getByText('Updated notes')).toBeDefined()
    rerender(<Harness recordId="b" text="Other record" />)
    expect(screen.queryByRole('dialog')).toBeNull()
    rerender(<Harness recordId="a" />)
    expect(screen.queryByRole('dialog')).toBeNull()
    fireEvent.click(screen.getByText('Read notes'))
    rerender(<Harness recordId="" />)
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('explicitly reports external context invalidation instead of silently discarding management drafts', () => {
    const notify = vi.spyOn(notifications, 'info').mockReturnValue('notice')
    const { rerender } = render(<Harness warn />)
    fireEvent.click(screen.getByText('Read notes'))
    rerender(<Harness recordId="b" warn />)
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('unsaved detail edits were discarded'))
  })

  it('reports an open management context when its workspace unmounts', () => {
    const notify = vi.spyOn(notifications, 'info').mockReturnValue('notice')
    const { unmount } = render(<Harness warn />)
    fireEvent.click(screen.getByText('Read notes'))
    unmount()
    expect(notify).toHaveBeenCalledOnce()
    expect(document.body.style.overflow).toBe('')
  })
})
