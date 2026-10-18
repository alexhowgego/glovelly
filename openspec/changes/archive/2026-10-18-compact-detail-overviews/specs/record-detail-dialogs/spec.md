## ADDED Requirements

### Requirement: Focused dialogs expose complete growing detail content
The system SHALL provide focused dialogs for independently growing gig resources and expenses/receipts, and retain existing task-specific dialogs. Standard gig notes, client billing/contact information, invoice description/workflow metadata, and Admin identity/account metadata SHALL be shown inline rather than behind permanent read-only detail dialogs. Resource and expense dialogs SHALL remain available for empty collections and SHALL preserve existing create/edit/delete, upload/download, reimbursement, receipt-analysis, primary-resource, and set-list workflows where applicable. Full inline collections SHALL be replaced rather than duplicated as parallel workflow implementations.

#### Scenario: Manage an empty resource collection
- **WHEN** a gig has no resources and the user opens Resources
- **THEN** the dialog shows the empty state and provides the existing resource creation action

#### Scenario: Existing expense operations remain available
- **WHEN** the user opens a gig's Expenses dialog
- **THEN** the complete expenses and their existing editing, receipt, reimbursement, download, and deletion operations are available under their existing business rules

#### Scenario: Successful attachment operation retains context
- **WHEN** an upload or attachment operation succeeds within a still-open detail dialog
- **THEN** the authoritative item and overview summary refresh without automatically closing the dialog or navigating away
- **AND** feedback follows the existing inline-versus-notification policy

### Requirement: Dialogs are accessible and responsive
Detail dialogs SHALL have an accessible name, initial focus, keyboard focus containment, a reachable close control, Escape dismissal subject to busy/dirty guards, and focus restoration. Long dialog content SHALL scroll within a viewport-bounded body without stranding controls. Only the top modal layer SHALL be interactive; parent/child transitions SHALL preserve context, focus, and scroll-lock correctness. Mobile SHALL expose the same content and operations in an appropriately sized dialog.

#### Scenario: Close returns to the overview
- **WHEN** the user opens a detail dialog by keyboard and dismisses it
- **THEN** focus returns to its trigger, or a sensible workspace fallback if that trigger no longer exists
- **AND** record selection and the list's scroll position are preserved

#### Scenario: Receipt analysis opens above expense management
- **WHEN** the user opens an existing receipt-analysis or expense-edit dialog from Expenses
- **THEN** only the child dialog traps focus and accepts interaction
- **AND** closing the child restores the parent with its current record, draft, and scroll context
- **AND** Escape does not simultaneously close both layers

#### Scenario: Long dialog on a phone
- **WHEN** a large resource or expense collection is opened at 390×844 CSS pixels
- **THEN** full content and actions remain reachable through dialog scrolling and keyboard navigation without horizontal document overflow

### Requirement: Dialog state is record-scoped and resilient to asynchronous updates
An open detail dialog SHALL remain bound to its originating record ID and use authoritative current data. Record selection/workspace changes and deletion SHALL invalidate that context rather than silently retarget the dialog. In-flight mutations SHALL retain original record/item IDs and SHALL NOT populate another record's dialog with stale results. Busy or dirty content SHALL use consistent operation guards or explicit discard confirmation before user-initiated dismissal. External context invalidation SHALL close stale management dialogs immediately with an explicit notice that any unsaved detail edits were discarded, without cancelling legitimate in-flight saves under their original IDs.

#### Scenario: Selection changes during an operation
- **WHEN** an operation starts for gig A and the workspace subsequently selects gig B
- **THEN** the operation retains gig A's identity and any saved result is merged through the existing workflow
- **AND** gig A's dialog is invalidated safely without showing its draft or completion feedback as gig B's content

#### Scenario: Record updates while its dialog remains open
- **WHEN** an expense or resource is saved for the originating record
- **THEN** its open dialog and overview counts/totals use the refreshed authoritative result

#### Scenario: Dismissal would discard edits
- **WHEN** user-initiated closing, pressing Escape, or clicking a dismissible backdrop would abandon unsaved work or an active operation
- **THEN** the existing busy/dirty guard or explicit discard confirmation applies consistently

#### Scenario: External context changes invalidate a management draft
- **WHEN** a record disappears, selection changes externally, or the workspace unmounts with a management dialog open
- **THEN** the stale dialog closes without retargeting another record
- **AND** a notice explicitly identifies the closure and possible discarded unsaved detail edits
