export const invoiceFilenameTokens = [
  '{InvoiceNumber}',
  '{InvoiceId}',
  '{ClientName}',
  '{Month}',
  '{MonthName}',
  '{Year}',
  '{InvoiceDate}',
]

const previewInvoiceNumber = 'INV-2026-001'
const previewInvoiceId = '11111111-1111-1111-1111-111111111111'

function buildInvoiceTokenReplacements(
  clientName: string | null | undefined,
  invoiceDate: Date
) {
  return new Map<string, string>([
    ['{InvoiceNumber}', previewInvoiceNumber],
    ['{InvoiceId}', previewInvoiceId],
    ['{ClientName}', clientName?.trim() || 'Client Name'],
    ['{Month}', String(invoiceDate.getMonth() + 1).padStart(2, '0')],
    [
      '{MonthName}',
      new Intl.DateTimeFormat('en-GB', { month: 'long' }).format(invoiceDate),
    ],
    ['{Year}', String(invoiceDate.getFullYear())],
    ['{InvoiceDate}', invoiceDate.toISOString().slice(0, 10)],
  ])
}

function resolveInvoicePatternPreview(
  pattern: string,
  clientName: string | null | undefined,
  invoiceDate = new Date()
) {
  const replacements = buildInvoiceTokenReplacements(clientName, invoiceDate)
  const containsUnsupportedToken = Array.from(
    pattern.matchAll(/\{[^{}]+\}/g)
  ).some(([token]) => !replacements.has(token))

  let resolved = containsUnsupportedToken ? '{InvoiceNumber}' : pattern
  for (const [token, replacement] of replacements) {
    resolved = resolved.replaceAll(token, replacement)
  }

  return resolved
}

export function buildInvoiceFilenamePreview(
  pattern: string | null | undefined,
  clientName: string | null | undefined,
  invoiceDate = new Date()
) {
  const trimmedPattern = pattern?.trim() ?? ''
  const effectivePattern = trimmedPattern || '{InvoiceNumber}'
  const resolved = resolveInvoicePatternPreview(
    effectivePattern,
    clientName,
    invoiceDate
  )

  const sanitized = resolved
    // eslint-disable-next-line no-control-regex
    .replace(/[<>:"/\\|?*\u0000-\u001F]/g, '-')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/^[. ]+|[. ]+$/g, '')

  return `${sanitized || previewInvoiceNumber}.pdf`
}

export function buildInvoiceEmailSubjectPreview(
  pattern: string | null | undefined,
  clientName: string | null | undefined,
  invoiceDate = new Date()
) {
  const trimmedPattern = pattern?.trim() ?? ''
  const effectivePattern =
    trimmedPattern || 'Invoice {InvoiceNumber} from Glovelly'
  const resolved = resolveInvoicePatternPreview(
    effectivePattern,
    clientName,
    invoiceDate
  )
    .replace(/\s+/g, ' ')
    .trim()

  return resolved || 'Invoice INV-2026-001 from Glovelly'
}
