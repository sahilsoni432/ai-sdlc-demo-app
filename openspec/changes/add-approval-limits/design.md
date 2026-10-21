# Design

## Context

See [proposal.md](./proposal.md) for motivation and [specs/invoice-approval/spec.md](./specs/invoice-approval/spec.md) for the behavior contract. `ApprovalService` currently performs a single approval by changing `Status`, `DecidedBy`, and `DecidedAt` before calling `IInvoiceRepository.Update`. The authenticated session provides both username and role, but the approval controller currently passes only the username. `ApiControllerBase.Handle` maps standard exceptions, while the UI already displays an API response's `error` property. Unit tests use a same-reference fake repository whose `Update` is currently a no-op.

## Goals / Non-Goals

**Goals:**
- Keep approval eligibility and workflow transitions in Domain, with role and username supplied by the API.
- Ensure all validation completes before any invoice field is changed or `Update` is called.
- Preserve `DecidedBy` and `DecidedAt` as the final decision fields, and retain first-approval attribution for the high-value two-step path.
- Verify behavior and repository interaction at the unit-test boundary, with a focused API mapping test and one UI E2E regression.

**Non-Goals:**
- Bulk or partial approvals, UI redesign, durable storage, or database migration.
- Changing rejection policy beyond ensuring the approval route's role changes do not accidentally broaden rejection access.

## Decisions

1. **Pass the authenticated role and username into the Domain approval operation.** The controller obtains both from `HttpContext.GetSession()`. Domain receives plain values and has no dependency on HTTP or session types. Avoid putting amount checks or approval decisions in the controller.

2. **Keep approval authorization inside the approve operation rather than relying on the role filter.** The existing `RequireRoleAttribute` returns an empty 403 and cannot include the required reason. The approve action must reach Domain for any authenticated role so ineligible roles receive the dedicated approval-blocked exception and the standard `{ "error": reason }` response. Rejection access is separate: Managers, Directors, and Admins may reject with a required reason and no amount limit; users with any other role receive 403.

3. **Treat the high-value first approval as a distinct transition.** For amounts above 500,000.00, a Manager or Director may record the first approver and first-approval time while leaving status `PendingApproval`; the final decision fields remain unset until a different Director completes the approval. For amounts at or below 500,000.00, a Director completes the decision in one step. A Manager's 50,000.00 limit applies to single-approver decisions; the explicitly authorized first-approval path above 500,000.00 is still available to a Manager.

4. **Add first-approval attribution to the in-memory invoice model.** Store the first approver username and timestamp separately from final-decision fields, retaining both after completion for attribution. This is an in-memory model change only; no persistence format or migration exists. Do not expose a first-step approval as an `Approved` decision.

5. **Use a dedicated Domain exception and map it before general operation conflicts.** Each eligibility failure raises the dedicated exception with a stable, clear reason. `ApiControllerBase.Handle` maps that exception to 403 and `{ "error": reason }`; existing UI error handling then shows the message without a redesign. Keep reason text consistent between the spec and tests.

6. **Prove blocked attempts are atomic at the service boundary.** Validate role, amount, approval stage, and distinct username before mutating. Extend `FakeInvoiceRepository` with an `Update` call count and snapshot every `Invoice` property in each blocked-attempt test. Also test the controller mapping using the API assembly so a Domain block is observed as HTTP 403 with the error body. The current test project references Domain only, so it will need an API project reference or an equivalent focused API test seam.

7. **Exercise the existing UI path for the E2E case.** Use the UI to sign in as `demo.manager`, create a 50,000.01 invoice, submit it, and attempt approval. Assert the visible rejection reason and that the invoice remains pending. Follow the existing project convention of page objects and accessible role/label locators; do not add UI changes unless the test reveals a necessary display defect. Assert the exact text "Manager may approve invoices up to 50,000.00." and that the invoice's status still reads "Pending Approval". Use a unique customer name per run (for example with a timestamp) and target that invoice by its own ID (from its row), never by position such as first(), so repeated runs against a reused app instance stay reliable.


8. **Keep the demo account list synchronized.** Replace the Approver demo login with `demo.manager` (Manager), add `demo.director` and `demo.director2` (Director), retain `demo.admin` (Admin), and keep the demo-only password unchanged. Update both API configuration and the README table, and update any seeded historical attribution that references the removed demo username.

9. **Make the high-value approval rule an explicit staged change.** Interim Stage 1 behavior blocks ANY approval attempt for an invoice above 500,000.00 with reason `Invoices above 500,000.00 require two approvers.`, returning 403 without changing the invoice or calling repository `Update`. Stage 2 removes this interim block and replaces its blanket-block test with tests for the two-approver workflow.

10. **Order of approval checks.** All checks run before any invoice field changes:
    (1) role is not Manager or Director → "Only Manager or Director may approve invoices.";
    (2) Stage 1 only: amount above 500,000.00 → "Invoices above 500,000.00 require two approvers.";
    (3) Manager and amount above 50,000.00 → "Manager may approve invoices up to 50,000.00.".
    Only when all pass is the invoice changed and Update called once.

11. **Stage 2 approval flow and order of checks** (replaces check (2) of decision 10). All checks run before any invoice field changes:
    (1) role is not Manager or Director → "Only Manager or Director may approve invoices.";
    (2) amount at or below 500,000.00 → single-approver rules of decision 10 (Manager limit), approve in one step;
    (3) amount above 500,000.00 and no first approval yet → record FirstApprovedBy and FirstApprovedAt, status stays PendingApproval, DecidedBy/DecidedAt stay empty, Update called once (the Manager 50,000.00 limit does not apply to this first approval);
    (4) amount above 500,000.00 with a first approval: role is not Director → "Only a Director may give the second approval.";
    (5) same username as the first approver, compared case-insensitively → "A different user must give the second approval.";
    otherwise set Approved, DecidedBy, DecidedAt, keep FirstApprovedBy/FirstApprovedAt, Update called once.
    A blocked attempt never records, clears or changes the first approval. No UI changes in Stage 2. Before these checks the invoice must exist (404 "Invoice … was not found.") and be PendingApproval (409 "… only pending invoices can be decided."); these state checks apply to every user, so an ineligible role approving a non-pending invoice receives the 409 state reason.


## Risks / Trade-offs

- [Role checks in a filter could hide the required rejection reason] → Route authenticated approval attempts through Domain; use a dedicated exception mapping for eligibility blocks.
- [Mutating the same invoice reference before validation would make rejected attempts observable even without a repository write] → Complete all checks before touching any invoice property; snapshot every model field and assert no `Update` call in tests.
- [The Manager limit and Manager-first high-value path could be misread as conflicting] → Keep them distinct in the service decision order and tests: Manager cannot decide an invoice alone above 50,000.00, but may be the recorded first approver above 500,000.00.
- [Changing the controller's class-level role filter could broaden rejection access] → Add explicit role restriction at the reject action and cover the desired behavior while updating approval access.
- [A new API project reference for mapping tests may couple the Domain-focused unit test project to the host] → Keep the mapping assertion narrowly scoped to the controller result; introduce a separate API test project only if the existing test structure cannot support that seam cleanly.

## Migration Plan

This demo uses an in-memory repository, so no data migration or persistence rollback is needed. Deploy the application and configuration together; replace the old demo approver login in documentation and seeded attribution. Roll back by reverting the feature commits and restoring the old demo account configuration.
