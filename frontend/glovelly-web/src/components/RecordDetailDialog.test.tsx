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
  it('wraps forward/reverse Tab inside the active child, excluding hidden, disabled and inert controls', async () => {
    // jsdom has no layout; make the visible test controls measurable.
    vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([new DOMRect(0, 0, 100, 20)] as unknown as DOMRectList)
    render(<RecordDetailDialog title="Parent" onClose={vi.fn()}>
      <button>Parent action</button>
      <section role="dialog" aria-label="Child">
        <input aria-label="First field" />
        <button disabled>Disabled action</button>
        <fieldset disabled><button>Disabled fieldset action</button></fieldset>
        <div hidden><button>Hidden action</button></div>
        <div inert><button>Inert action</button></div>
        <button style={{ visibility: 'hidden' }}>Invisible action</button>
        <button tabIndex={-1}>Non-tabbable action</button>
        <button>Last action</button>
      </section>
    </RecordDetailDialog>)
    const child = screen.getByRole('dialog', { name: 'Child' })
    const first = screen.getByLabelText('First field')
    const last = screen.getByText('Last action')
    await waitFor(() => expect(document.activeElement).toBe(child))
    expect(fireEvent.keyDown(child, { key: 'Tab' })).toBe(false)
    expect(document.activeElement).toBe(first)
    fireEvent.keyDown(first, { key: 'Tab', shiftKey: true })
    expect(document.activeElement).toBe(last)
    fireEvent.keyDown(last, { key: 'Tab' })
    expect(document.activeElement).toBe(first)
    // Normal interior traversal is left to the browser.
    expect(fireEvent.keyDown(first, { key: 'Tab' })).toBe(true)
  })

  it('keeps Tab on an empty child layer and recovers focus from outside it', async () => {
    render(<RecordDetailDialog title="Parent" onClose={vi.fn()}>
      <section role="dialog" aria-label="Empty child"><button disabled>Unavailable</button></section>
    </RecordDetailDialog>)
    const child = screen.getByRole('dialog', { name: 'Empty child' })
    await waitFor(() => expect(document.activeElement).toBe(child))
    fireEvent.keyDown(child, { key: 'Tab' })
    expect(document.activeElement).toBe(child)
    document.body.tabIndex = -1
    document.body.focus()
    fireEvent.keyDown(document.body, { key: 'Tab', shiftKey: true })
    expect(document.activeElement).toBe(child)
    document.body.removeAttribute('tabindex')
  })

  it('removes Tab handling when closed', () => {
    const { unmount } = render(<RecordDetailDialog title="Parent" onClose={vi.fn()}>Content</RecordDetailDialog>)
    unmount()
    expect(fireEvent.keyDown(document.body, { key: 'Tab' })).toBe(true)
  })

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
