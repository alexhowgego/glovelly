## MODIFIED Requirements

### Requirement: Browser upload and quick capture journeys are automated
The UAT browser suite SHALL verify representative receipt and gig attachment upload/download/delete journeys through the browser, including quick-capture navigation to the selected target gig.

#### Scenario: User manages an expense receipt in the browser
- **WHEN** an authenticated UAT browser test uploads a small receipt file to a gig expense, downloads it, and deletes it
- **THEN** receipt metadata SHALL appear and disappear in the browser as expected, the download SHALL be non-empty, and the expense reimbursement state SHALL remain unchanged

#### Scenario: User manages a file-only gig attachment in the browser
- **WHEN** an authenticated UAT browser test creates a file-only gig attachment, uploads a small file, downloads it, and deletes the uploaded file
- **THEN** the attachment SHALL remain scoped to the selected gig, the download SHALL be non-empty, and deleting the file SHALL NOT delete the attachment shell

#### Scenario: User quick-adds a gig attachment in a mobile-sized viewport
- **WHEN** an authenticated UAT browser test uses the floating quick attachment action in a mobile-sized viewport and chooses `Go to gig` after saving the attachment
- **THEN** the target Gig overview SHALL be visible without the test manually selecting the gig after the action

#### Scenario: User quick-adds a receipt and opens its target gig
- **WHEN** an authenticated UAT browser test saves a quick receipt and chooses `Go to gig`
- **THEN** the target Gig overview SHALL be visible without the test manually selecting the gig after the action
