# Spec Delta

## Purpose

Defines who may approve invoices at each amount and when high-value invoices require a second approver, while making blocked attempts explicit and non-mutating.

## ADDED Requirements

### Requirement: Approval authority follows role and amount limits
The system SHALL allow only Managers and Directors to approve invoices, subject to their individual amount limits.

#### Scenario: Manager approves at the inclusive limit
- **WHEN** a Manager approves an invoice for 50,000.00
- **THEN** the invoice becomes Approved with that Manager recorded as the approver

#### Scenario: Manager is blocked above the limit
- **WHEN** a Manager attempts to approve an invoice for 50,000.01 as a single approver
- **THEN** the attempt is blocked with reason `Manager may approve invoices up to 50,000.00.`

#### Scenario: Manager cannot approve 500,000.00 as a single approver
- **WHEN** a Manager attempts to approve an invoice for 500,000.00
- **THEN** the single approval is blocked with reason `Manager may approve invoices up to 50,000.00.`

#### Scenario: Director approves at the inclusive single-approval limit
- **WHEN** a Director approves an invoice for 500,000.00
- **THEN** the invoice becomes Approved with that Director recorded as the approver

#### Scenario: Admin cannot approve
- **WHEN** an Admin attempts to approve an invoice at any amount
- **THEN** the attempt is blocked with reason `Only Manager or Director may approve invoices.`

#### Scenario: Other roles cannot approve
- **WHEN** a user whose role is neither Manager nor Director attempts to approve an invoice
- **THEN** the attempt is blocked with reason `Only Manager or Director may approve invoices.`

### Requirement: Rejection access follows role and requires a reason
The system SHALL allow Managers, Directors, and Admins to reject invoices with a required reason and no amount limit. Users with any other role SHALL receive HTTP 403 when attempting to reject an invoice.

#### Scenario: Manager, Director, or Admin rejects an invoice
- **WHEN** a Manager, Director, or Admin rejects an invoice of any amount and supplies a reason
- **THEN** the invoice is rejected and the supplied reason is recorded

#### Scenario: Rejection without a reason is not allowed
- **WHEN** a Manager, Director, or Admin attempts to reject an invoice without a reason
- **THEN** the rejection is blocked because a reason is required

#### Scenario: Other roles cannot reject
- **WHEN** a user whose role is neither Manager, Director, nor Admin attempts to reject an invoice
- **THEN** the attempt is blocked with HTTP 403

### Requirement: High-value invoices require two approvals
An invoice above 500,000.00 SHALL remain pending after an eligible first approval and become approved only after a Director gives a second approval.

#### Scenario: Invoice at the single-approval boundary needs no second approver
- **WHEN** a Director approves an invoice for 500,000.00
- **THEN** the invoice becomes Approved in that single approval

#### Scenario: Manager gives the first approval above the boundary
- **WHEN** a Manager approves an invoice for 500,000.01
- **THEN** the invoice remains PendingApproval and records the Manager as the first approver

#### Scenario: Director gives the first approval above the boundary
- **WHEN** a Director approves an invoice for more than 500,000.00
- **THEN** the invoice remains PendingApproval and records that Director as the first approver

#### Scenario: A Director completes the second approval
- **WHEN** a Director different from the first approver gives the second approval for an invoice above 500,000.00
- **THEN** the invoice becomes Approved and records the final decision

#### Scenario: Manager cannot give the second approval
- **WHEN** a Manager attempts the second approval for an invoice above 500,000.00
- **THEN** the attempt is blocked with reason `Only a Director may give the second approval.`

### Requirement: High-value approvals require distinct users
The two approvals for an invoice above 500,000.00 SHALL be given by distinct usernames, compared without regard to letter case.

#### Scenario: The first approver cannot approve a second time
- **WHEN** the same username attempts the second approval
- **THEN** the attempt is blocked with reason `A different user must give the second approval.`

#### Scenario: Usernames differing only by letter case are the same approver
- **WHEN** the second approver's username differs from the first approver's username only by letter case
- **THEN** the attempt is blocked with reason `A different user must give the second approval.`

#### Scenario: Another Director completes approval after a case-insensitive duplicate is blocked
- **WHEN** a case-insensitive duplicate second-approval attempt is blocked and a different Director subsequently approves
- **THEN** the original first approval is unchanged and the invoice becomes Approved

### Requirement: Blocked approvals return a reason without changing the invoice
Every blocked approval SHALL return its clear reason as HTTP 403 with an `{ "error": reason }` response and SHALL leave every invoice field unchanged without calling the repository update operation.

#### Scenario: Over-limit Manager attempt is atomic
- **WHEN** a Manager attempts a single approval for an invoice above 50,000.00
- **THEN** the 403 error is `Manager may approve invoices up to 50,000.00.`, every invoice field matches its pre-attempt snapshot, and the repository update count is unchanged

#### Scenario: Admin or other-role attempt is atomic
- **WHEN** an Admin or a user with any other ineligible role attempts approval
- **THEN** the 403 error is `Only Manager or Director may approve invoices.`, every invoice field matches its pre-attempt snapshot, and the repository update count is unchanged

#### Scenario: Wrong role for second approval is atomic
- **WHEN** a Manager attempts to give the second approval for an invoice above 500,000.00
- **THEN** the 403 error is `Only a Director may give the second approval.`, every invoice field matches its pre-attempt snapshot, and the repository update count is unchanged

#### Scenario: Same-user second approval is atomic
- **WHEN** the first approver attempts the second approval
- **THEN** the 403 error is `A different user must give the second approval.`, every invoice field matches its pre-attempt snapshot, and the repository update count is unchanged

#### Scenario: Case-insensitive same-user second approval is atomic
- **WHEN** the second-approval username differs from the first only by letter case
- **THEN** the 403 error is `A different user must give the second approval.`, every invoice field matches its pre-attempt snapshot, and the repository update count is unchanged
