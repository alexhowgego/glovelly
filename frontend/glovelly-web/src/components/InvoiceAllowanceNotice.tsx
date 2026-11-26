import type { EntitlementSnapshot, Invoice } from '../types'

export function InvoiceAllowanceNotice({ invoice, entitlements }: {
  invoice?: Invoice | null
  entitlements?: EntitlementSnapshot | null
}) {
  if (invoice && invoice.status !== 'Draft') return null
  if (invoice && (invoice.hasBeenIssued || invoice.firstIssuedUtc)) {
    return <p className="settings-note">This is a correction. Issuing it again uses no additional monthly allowance.</p>
  }
  if (!entitlements) return <p className="settings-note">Your current plan allowance will be checked before issuing.</p>
  const usage = entitlements.invoiceIssuance
  return <p className="settings-note">
    {usage.limit === null
      ? `${entitlements.effectivePlan.displayName} includes unlimited invoice issuance.`
      : usage.remaining === 0
        ? `Your ${entitlements.effectivePlan.displayName} allowance is used (${usage.used}/${usage.limit}). You can keep editing drafts.`
        : `First issuance uses one monthly allowance unit. ${usage.remaining} of ${usage.limit} remaining.`}
    {usage.limit !== null && <> Resets {new Date(usage.resetsAtUtc).toLocaleString('en-GB', { timeZone: 'Europe/London' })} (UK time). No rollover.</>}
    {' '}Plan changes are currently handled by a licence administrator.
  </p>
}
