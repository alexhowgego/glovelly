// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { RecordEditorFrame } from './RecordEditorFrame'
import { RecordEditorActions } from './RecordEditorActions'

beforeEach(() => { vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false }))) })
afterEach(() => { cleanup(); vi.unstubAllGlobals() })

it('uses the established editor frame and keeps the closed form inert and inaccessible', () => {
  const view = render(<RecordEditorFrame isOpen={false} label="Update record" onSubmit={vi.fn()}>
    <label>Name<input /></label>
  </RecordEditorFrame>)
  expect(screen.queryByRole('form', { name: 'Update record' })).toBeNull()
  expect(view.container.querySelector('.editor-slot > .editor-panel.panel')?.hasAttribute('inert')).toBe(true)
  view.rerender(<RecordEditorFrame isOpen label="Update record" onSubmit={vi.fn()}>
    <label>Name<input /></label>
  </RecordEditorFrame>)
  expect(screen.getByRole('form', { name: 'Update record' })).toBeTruthy()
  expect(view.container.querySelector('.editor-slot.open > .editor-panel.panel')?.hasAttribute('inert')).toBe(false)
})

it('preserves the same save-and-close/save submitter controls used by Admin', () => {
  const choices: string[] = []
  const discard = vi.fn()
  render(<RecordEditorFrame isOpen label="Update record" onSubmit={event => {
    event.preventDefault()
    choices.push(((event.nativeEvent as SubmitEvent).submitter as HTMLElement).dataset.closeAfterSave!)
  }}>
    <RecordEditorActions isSaving={false} onDiscard={discard} />
  </RecordEditorFrame>)
  fireEvent.click(screen.getByRole('button', { name: 'Save and close' }))
  fireEvent.click(screen.getByRole('button', { name: 'Save' }))
  fireEvent.click(screen.getByRole('button', { name: 'Discard changes' }))
  expect(choices).toEqual(['true', 'false'])
  expect(discard).toHaveBeenCalledOnce()
})
