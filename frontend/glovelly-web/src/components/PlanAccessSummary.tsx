import type { EntitlementSnapshot } from '../types'

export function PlanAccessSummary({ entitlements, error }: {
  entitlements?: EntitlementSnapshot | null; error?: string
}) {
  if (error) return <p className="settings-note">{error} Your plan will still be checked before issuing.</p>
  if (!entitlements) return null
  const usage = entitlements.invoiceIssuance
  return <p className="settings-note">
    <strong>{entitlements.effectivePlan.displayName}</strong> · {usage.used} first-issued this month
    {usage.limit === null ? ' · Unlimited issuance' : ` · ${usage.remaining}/${usage.limit} remaining`}
    {' · '}Resets {new Date(usage.resetsAtUtc).toLocaleString('en-GB', { timeZone: 'Europe/London' })} (UK time).
    {entitlements.assignedPlan && entitlements.assignedPlan.code !== entitlements.effectivePlan.code &&
      <> Assigned grant: {entitlements.assignedPlan.displayName} ({entitlements.assignmentStatus.toLowerCase()}).</>}
    {entitlements.endsAtUtc && <> Grant {entitlements.assignmentStatus === 'Expired' ? 'ended' : 'ends'} {new Date(entitlements.endsAtUtc).toLocaleString()}.</>}
    {entitlements.assignmentStatus === 'Expired' || entitlements.assignmentStatus === 'Cancelled'
      ? ' Paid access has fallen back to Free; usage continues and all records remain available.' : ''}
    {' '}Contact a licence administrator for plan changes.
  </p>
}
