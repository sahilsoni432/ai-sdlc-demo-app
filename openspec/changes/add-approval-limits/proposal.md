# Proposal

## Why

The current approval flow lets any approver decide an invoice alone, regardless of amount. Role-based thresholds and a two-person rule for high-value invoices make approval authority explicit while preserving the existing invoice lifecycle and returning actionable explanations for blocked attempts.

## What Changes

- Replace the single-approver rule with Manager and Director approval limits: Manager through 50,000.00 and Director alone through 500,000.00.
- Require a Manager or Director as the first approver above 500,000.00 and a different Director as the second approver; keep the invoice pending and record the first approver between approvals.
- Block Admin and other roles from approving, without changing any invoice state or calling repository `Update`; return a dedicated Domain exception mapped to HTTP 403 with `{ "error": reason }`.
- Replace the `demo.approver` account with `demo.manager`, add `demo.director` and `demo.director2`, and retain `demo.admin`, all with the existing demo-only password.
- Add focused Domain unit tests, an over-limit rejection Playwright test, mutation-test score recording, and an agent review stage.

## Capabilities

### New Capabilities
- `invoice-approval`: Role- and amount-based approval eligibility, multi-approver completion for high-value invoices, and unchanged-state behavior for blocked attempts.

### Modified Capabilities

None.

## Impact

- Domain: approval service, invoice approval metadata, and a dedicated blocked-approval exception.
- API: approval controller role attributes and role forwarding; shared controller error mapping; demo-user configuration and README account list.
- Tests: approval-service unit tests and repository update-call tracking; one Playwright UI test using the relevant page objects and role-based locators.
- Validation: .NET tests, E2E test, Stryker baseline/after scores, and an agent review. No persistence changes or new dependencies are intended.
