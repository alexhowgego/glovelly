import type { IntakeFields } from '../hooks/useUnifiedIntake'

type Props = { fields: IntakeFields; disabled: boolean; onChange: <K extends keyof IntakeFields>(key: K, value: IntakeFields[K]) => void }

export function IntakeResourceFields({ fields, disabled, onChange }: Props) {
  return <div className="form-grid">
    <label><span>Title</span><input value={fields.title} maxLength={200} onChange={event => onChange('title', event.target.value)} disabled={disabled} placeholder="Set list, gig plan, contract..." /></label>
    <label><span>Purpose</span><select value={fields.purpose} onChange={event => onChange('purpose', event.target.value as IntakeFields['purpose'])} disabled={disabled}>
      <option value="SetList">Set list</option><option value="GigPlan">Gig plan</option><option value="Contract">Contract</option><option value="Travel">Travel</option><option value="Other">Other</option>
    </select></label>
    <label><span>Type</span><select value={fields.resourceType} onChange={event => onChange('resourceType', event.target.value as IntakeFields['resourceType'])} disabled={disabled}>
      <option value="File">File</option><option value="Url">URL</option><option value="GoogleDoc">Google Doc</option><option value="GoogleSheet">Google Sheet</option><option value="Email">Email</option><option value="Other">Other</option>
    </select></label>
    <label><span>Notes</span><input value={fields.notes} maxLength={2000} onChange={event => onChange('notes', event.target.value)} disabled={disabled} /></label>
    <label className="checkbox-field"><input type="checkbox" checked={fields.isPrimary} onChange={event => onChange('isPrimary', event.target.checked)} disabled={disabled} /><span>Make primary for this purpose</span></label>
  </div>
}
