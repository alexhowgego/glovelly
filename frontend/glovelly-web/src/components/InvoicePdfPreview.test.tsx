// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { InvoicePdfPreview } from './InvoicePdfPreview'

const mocks = vi.hoisted(() => ({ getDocument: vi.fn(), destroy: vi.fn(), getPage: vi.fn(), render: vi.fn() }))
vi.mock('pdfjs-dist', () => ({ GlobalWorkerOptions: {}, getDocument: mocks.getDocument }))
vi.mock('pdfjs-dist/build/pdf.worker.min.mjs?url', () => ({ default: '/worker.js' }))
afterEach(() => { cleanup(); vi.clearAllMocks() })

it('renders and navigates a multi-page PDF and releases it on close', async () => {
  mocks.render.mockReturnValue({ promise: Promise.resolve(), cancel: vi.fn() })
  mocks.getPage.mockResolvedValue({ getViewport: ({ scale }: { scale: number }) => ({ width: 600 * scale, height: 800 * scale }), render: mocks.render })
  mocks.destroy.mockResolvedValue(undefined)
  mocks.getDocument.mockReturnValue({ promise: Promise.resolve({ numPages: 2, getPage: mocks.getPage }), destroy: mocks.destroy })
  const view = render(<InvoicePdfPreview url="blob:invoice" invoiceNumber="INV-1" />)
  expect(await screen.findByRole('img', { name: 'Invoice INV-1, page 1' })).toBeTruthy()
  expect(screen.getByText('Page 1 of 2')).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: 'Next page' }))
  expect(await screen.findByRole('img', { name: 'Invoice INV-1, page 2' })).toBeTruthy()
  expect(mocks.getPage).toHaveBeenCalledWith(2)
  view.unmount()
  await waitFor(() => expect(mocks.destroy).toHaveBeenCalledOnce())
})

it('shows an actionable fallback if the PDF cannot be loaded', async () => {
  mocks.destroy.mockResolvedValue(undefined)
  mocks.getDocument.mockReturnValue({ promise: Promise.reject(new Error('bad PDF')), destroy: mocks.destroy })
  render(<InvoicePdfPreview url="blob:bad" invoiceNumber="INV-2" />)
  expect(await screen.findByText(/Download the PDF to review it/)).toBeTruthy()
})
