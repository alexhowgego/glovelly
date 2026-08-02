## ADDED Requirements

### Requirement: Cloned gigs start as drafts
The system SHALL create every gig cloned through the Gigs workspace with lifecycle status `Draft`, regardless of the source gig's lifecycle status.

#### Scenario: Clone a planned gig
- **WHEN** a user clones a gig whose status is `Confirmed` (shown as Planned)
- **THEN** the newly created and opened clone has status `Draft`

#### Scenario: Clone a completed gig
- **WHEN** a user clones a gig whose status is `Completed`
- **THEN** the newly created and opened clone has status `Draft`

#### Scenario: Clone a cancelled gig
- **WHEN** a user clones a gig whose status is `Cancelled`
- **THEN** the newly created and opened clone has status `Draft`

#### Scenario: Clone a draft gig
- **WHEN** a user clones a gig whose status is `Draft`
- **THEN** the newly created and opened clone has status `Draft`

### Requirement: Cloning preserves reusable gig details without changing the source
The system SHALL preserve the existing cloning behaviour for reusable gig details and optional expenses, SHALL omit invoice linkage and receipt attachments from the clone, and SHALL NOT modify the source gig.

#### Scenario: Clone a gig with copied expenses
- **WHEN** a user chooses to include expenses while cloning a gig
- **THEN** the clone retains the source gig's reusable details and expense descriptions and amounts, has no invoice linkage or receipt attachments, and the source gig remains unchanged

#### Scenario: Clone a gig without copied expenses
- **WHEN** a user declines to include expenses while cloning a gig
- **THEN** the clone retains the source gig's reusable core details, has no expenses or invoice linkage, and the source gig remains unchanged
