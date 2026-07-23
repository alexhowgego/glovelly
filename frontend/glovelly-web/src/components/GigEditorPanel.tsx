import type { FormEvent } from 'react'
import type { Client, GigExpenseForm, GigForm, GigStatus, GigType } from '../types'

type GigEditorPanelProps = {
  clients: Client[]
  gigForm: GigForm
  gigMode: 'create' | 'edit'
  gigStatus: string
  isEditorOpen: boolean
  isGigLoading: boolean
  isMileageEstimating: boolean
  onCloseEditor: () => void
  onEstimateMileage: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateGigField: (
    field: keyof GigForm,
    value: string | boolean | GigExpenseForm[]
  ) => void
}

export function GigEditorPanel({
  clients,
  gigForm,
  gigMode,
  gigStatus,
  isEditorOpen,
  isGigLoading,
  isMileageEstimating,
  onCloseEditor,
  onEstimateMileage,
  onSubmit,
  onUpdateGigField,
}: GigEditorPanelProps) {
  return (
    <form
      aria-hidden={!isEditorOpen}
      className="editor-panel panel gig-editor-panel"
      data-testid="gig-form"
      onSubmit={onSubmit}
    >
      <div className="panel-heading">
        <div>
          <p className="section-label">Management Pane</p>
          <h2>{gigMode === 'create' ? 'Create gig' : 'Update gig'}</h2>
        </div>
        <span className="status-pill" data-testid="gig-status">{gigStatus}</span>
      </div>

      <div className="form-grid">
        <label>
          <span>Client</span>
          <select
            data-testid="gig-client-select"
            required
            value={gigForm.clientId}
            onChange={(event) => onUpdateGigField('clientId', event.target.value)}
          >
            <option value="">Select a client</option>
            {clients.map((client) => (
              <option key={client.id} value={client.id}>
                {client.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          <span>Type</span>
          <select
            data-testid="gig-type-select"
            required
            value={gigForm.type}
            onChange={(event) => onUpdateGigField('type', event.target.value as GigType)}
          >
            <option value="Performance">Performance</option>
            <option value="Teaching">Teaching</option>
            <option value="Rehearsal">Rehearsal</option>
            <option value="Recording">Recording</option>
            <option value="Admin">Admin</option>
            <option value="Other">Other work</option>
          </select>
        </label>

        <label className="full-width">
          <span>Title / description</span>
          <input
            data-testid="gig-title-input"
            required
            value={gigForm.title}
            onChange={(event) => onUpdateGigField('title', event.target.value)}
            placeholder="Spring product launch"
          />
        </label>

        <label className="full-width">
          <span>Location</span>
          <input
            data-testid="gig-venue-input"
            required
            value={gigForm.venue}
            onChange={(event) => onUpdateGigField('venue', event.target.value)}
            placeholder="Albert Hall, Manchester"
          />
        </label>

        <div className="gig-editor-metrics full-width">
          <label>
            <span>Date</span>
            <input
              data-testid="gig-date-input"
              required
              type="date"
              value={gigForm.date}
              onChange={(event) => onUpdateGigField('date', event.target.value)}
            />
          </label>

          <label>
            <span>Fee</span>
            <input
              data-testid="gig-fee-input"
              required
              inputMode="decimal"
              value={gigForm.fee}
              onChange={(event) => onUpdateGigField('fee', event.target.value)}
              placeholder="650"
            />
          </label>

          <label>
            <span>Status</span>
            <select
              data-testid="gig-status-select"
              value={gigForm.status}
              onChange={(event) =>
                onUpdateGigField('status', event.target.value as GigStatus)
              }
            >
              <option value="Confirmed">Planned</option>
              <option value="Completed">Completed</option>
              <option value="Cancelled">Cancelled</option>
              <option value="Draft">Draft</option>
            </select>
          </label>
        </div>

        <label className="checkbox-field full-width">
          <input
            data-testid="gig-driving-checkbox"
            type="checkbox"
            checked={gigForm.wasDriving}
            onChange={(event) => onUpdateGigField('wasDriving', event.target.checked)}
          />
          <span>I was driving for this gig</span>
        </label>

        {gigForm.wasDriving && (
          <>
            <label>
              <span>Travel miles</span>
              <input
                data-testid="gig-travel-miles-input"
                inputMode="decimal"
                value={gigForm.travelMiles}
                onChange={(event) => onUpdateGigField('travelMiles', event.target.value)}
                placeholder="24"
              />
            </label>

            <div className="mileage-estimate-action">
              <button
                className="ghost-button"
                data-testid="gig-estimate-mileage-button"
                disabled={gigMode !== 'edit' || isMileageEstimating}
                onClick={onEstimateMileage}
                type="button"
              >
                {isMileageEstimating ? 'Estimating...' : 'Estimate mileage'}
              </button>
            </div>

            <label>
              <span>Passengers</span>
              <input
                data-testid="gig-passenger-count-input"
                inputMode="numeric"
                value={gigForm.passengerCount}
                onChange={(event) => onUpdateGigField('passengerCount', event.target.value)}
                placeholder="0"
              />
            </label>
          </>
        )}

        <label className="full-width">
          <span>Notes</span>
          <textarea
            rows={5}
            value={gigForm.notes}
            onChange={(event) => onUpdateGigField('notes', event.target.value)}
            placeholder="Optional commercial or logistics notes"
          />
        </label>
      </div>

      <div className="form-actions">
        <button
          className="primary-button"
          data-close-after-save="true"
          data-testid="gig-save-close-button"
          type="submit"
          disabled={isGigLoading || clients.length === 0}
        >
          Save and close
        </button>
        <button
          className="ghost-button"
          data-close-after-save="false"
          type="submit"
          disabled={isGigLoading || clients.length === 0}
        >
          Save
        </button>
        <button className="ghost-button" onClick={onCloseEditor} type="button">
          Discard changes
        </button>
      </div>

      {clients.length === 0 && (
        <p className="auth-note">
          Add a client first so this gig can be linked to the right account.
        </p>
      )}
    </form>
  )
}
