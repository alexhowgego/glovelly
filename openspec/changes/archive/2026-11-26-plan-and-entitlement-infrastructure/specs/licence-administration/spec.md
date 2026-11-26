## ADDED Requirements

### Requirement: Licensing reuses established administration views
The licence workspace SHALL reuse Admin's compact directory, selected-record overview, docked actions, and shared editor frame and save/discard controls. Selection SHALL first show a read-only overview; editing SHALL be an explicit action. Save and close SHALL retain the refreshed selected overview and use terminal notification feedback, while Save SHALL keep the editor open with inline success and the current assignment version. Unsaved edits SHALL be protected when discarding or changing selection. Licence-only access SHALL remain independent of user management.

#### Scenario: Review before editing
- **WHEN** a licence manager selects a user
- **THEN** their plan, usage, expiry, and audit history SHALL appear in the established overview frame without automatically opening the editor

#### Scenario: Save while remaining in the editor
- **WHEN** the manager selects Save rather than Save and close
- **THEN** the same editor SHALL remain open with inline completion feedback and refreshed assignment version, without a completion notification

#### Scenario: Responsive pane consistency
- **WHEN** the licence workspace is viewed at desktop or mobile widths
- **THEN** it SHALL use Admin's bounded desktop panes and stacked mobile editor behavior, without a separate table layout or redundant single-page paging controls

### Requirement: Licence dashboard summarizes global effective access
The licence workspace SHALL display shared dashboard cards for effective Free accounts, effective Solo/Pro accounts, and currently effective paid grants ending within 30 days (including the upper boundary). Counts SHALL include inactive accounts and remain independent of directory search/pagination. The summary SHALL require ManageLicences alone, use the authoritative effective-grant rules, and refresh after successful licence changes and workspace entry/focus. Loading or failure SHALL NOT appear as fabricated zero counts.

#### Scenario: Search does not change global cards
- **WHEN** a licence manager filters or pages the directory
- **THEN** the dashboard SHALL continue to summarize all accounts rather than only the visible rows

#### Scenario: Expired or revoked grant
- **WHEN** a grant is expired, revoked, or has not started
- **THEN** that account SHALL count as effective Free and SHALL NOT count as an expiring paid grant

#### Scenario: Shared viewport filling
- **WHEN** a desktop viewport has room for bounded panes
- **THEN** Licences SHALL fill the available workspace height using the same shared layout contract as Clients, Gigs, Invoices, and Admin
- **AND** small or short viewports SHALL retain reachable document-flow controls

### Requirement: Licensing requires its own capability and workspace
Only active authenticated users with current ManageLicences SHALL read the licence directory/audit and assign, expire, or revoke product plans. The licensing workspace SHALL be reachable without ManageUsers and SHALL NOT disclose ordinary user-management controls, Google subjects, or owned business records. Commercial grants SHALL NOT change administrative authority.

#### Scenario: Licence-only administrator
- **WHEN** a user holds ManageLicences but not ManageUsers
- **THEN** they SHALL be able to use the licence workspace without calling the protected user-management directory
- **AND** ordinary user/access-request management SHALL remain unavailable

#### Scenario: General administrator without licensing
- **WHEN** a ManageUsers-only user requests licence assignment or audit
- **THEN** the request SHALL be forbidden without subscription/audit side effects

#### Scenario: Administrative fields are attempted through account CRUD
- **WHEN** account creation/update supplies subscription or plan fields
- **THEN** it SHALL NOT bypass the dedicated licensing policy

### Requirement: Manual assignment supports expiry and versioned editing
Authorized licence managers SHALL assign Free/Solo/Pro, grant permanent or future-expiring paid access, and revoke paid access with an optional bounded reason. Catalogue editing, usage resets, and refunds SHALL NOT be provided. Current assignment versions SHALL protect updates from stale overwrites, including concurrent creation of a previously absent assignment.

#### Scenario: Expiring Pro grant
- **WHEN** a licence manager assigns Pro with a future end instant
- **THEN** it SHALL be effective immediately through that exclusive end and fall back to Free afterward

#### Scenario: Revoke paid access
- **WHEN** a licence manager cancels an active paid assignment or assigns Free
- **THEN** effective access SHALL be Free without resetting the user's current-month usage

#### Scenario: Stale editor saves
- **WHEN** two editors submit against the same assignment version
- **THEN** at most one conflicting update SHALL commit and the other SHALL receive conflict/reload feedback

### Requirement: Licence changes and audit commit atomically
Each effective manual or migration assignment change SHALL persist target and actor identity, old/new assignment state, timestamp, source, and optional reason atomically with the change. Audit SHALL survive account deletion. No-op saves SHALL NOT create misleading change entries. Actor capability SHALL be rechecked inside the protected administrative mutation boundary.

#### Scenario: Audit persistence fails
- **WHEN** a grant cannot persist its audit record
- **THEN** the assignment SHALL roll back

#### Scenario: Licensing permission is concurrently revoked
- **WHEN** capability revocation races an assignment
- **THEN** the assignment SHALL not commit using a stale capability check after revocation has taken effect in the serialized administrative order

#### Scenario: Target account is deleted
- **WHEN** an account with licence audit history is legitimately deleted
- **THEN** audit identity snapshots SHALL survive and new relationships SHALL NOT unexpectedly prevent ordinary account deletion

#### Scenario: Save without changes
- **WHEN** the same assignment state is submitted without an effective change
- **THEN** no duplicate change record or usage reset SHALL occur

### Requirement: Licensing cannot reset continuous usage
Plan grants and revocations SHALL use the same target-user serialization boundary as issuance and SHALL NOT modify usage ledger events. Licence managers MAY change their own product plan or a privileged user's product plan under the same audited policy; this SHALL NOT bypass first-issue accounting or grant administrative capabilities.

#### Scenario: Repeated paid and Free assignments
- **WHEN** a manager repeatedly changes a user's plan within one month
- **THEN** the user's accumulated usage SHALL remain unchanged and Free allowance SHALL NOT be replenished
