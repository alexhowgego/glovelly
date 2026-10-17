import type { GigExternalResourceForm, GigExternalResourcePurpose, GigExternalResourceType } from '../types'

import { resourceTypeOptions, resourcePurposeOptions } from '../gigEditing'

type Props = {
  form: GigExternalResourceForm; disabled: boolean; showUrl?: boolean; testIdPrefix?: string
  onChange: <K extends keyof GigExternalResourceForm>(key: K, value: GigExternalResourceForm[K]) => void
}
export function GigResourceFields({ form, disabled, onChange, showUrl = true, testIdPrefix }: Props) {
  const id = (suffix: string) => testIdPrefix ? `${testIdPrefix}-${suffix}` : undefined
  return <>
    <div className="compact-form-grid">
      <label><span>Type</span><select data-testid={id('type-select')} value={form.resourceType} onChange={event => onChange('resourceType', event.target.value as GigExternalResourceType)} disabled={disabled}>{resourceTypeOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
      <label><span>Purpose</span><select value={form.purpose} onChange={event => onChange('purpose', event.target.value as GigExternalResourcePurpose)} disabled={disabled}>{resourcePurposeOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
      <label><span>Title</span><input data-testid={id('title-input')} required maxLength={200} value={form.title} onChange={event => onChange('title', event.target.value)} disabled={disabled} /></label>
      {showUrl && <label><span>URL</span><input data-testid={id('url-input')} type="url" maxLength={2000} placeholder="Optional link" value={form.url} onChange={event => onChange('url', event.target.value)} disabled={disabled} /></label>}
    </div>
    <label className="intake-input"><span>Notes</span><textarea rows={3} maxLength={2000} value={form.notes} onChange={event => onChange('notes', event.target.value)} disabled={disabled} /></label>
    <label className="checkbox-field resource-primary-toggle"><input type="checkbox" checked={form.isPrimary} onChange={event => onChange('isPrimary', event.target.checked)} disabled={disabled} /><span>Make this the primary attachment for its purpose</span></label>
  </>
}
