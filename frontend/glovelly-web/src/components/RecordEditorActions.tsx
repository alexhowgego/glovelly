export function RecordEditorActions({ isSaving, onDiscard }: {
  isSaving: boolean
  onDiscard: () => void
}) {
  return <div className="form-actions">
    <button className="primary-button" data-close-after-save="true" type="submit" disabled={isSaving}>
      Save and close
    </button>
    <button className="ghost-button" data-close-after-save="false" type="submit" disabled={isSaving}>
      Save
    </button>
    <button className="ghost-button" onClick={onDiscard} type="button" disabled={isSaving}>
      Discard changes
    </button>
  </div>
}
