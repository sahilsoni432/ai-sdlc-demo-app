# Tasks

## 1. Stage 1 — Single-approver limits (separate commit)

- [x] 1.1 Before changing implementation, run the documented Stryker command on the clean pre-change revision and retain its revision and mutation score for the Stage 4 comparison; verify the baseline run completes.
- [x] 1.2 Add the dedicated Domain approval-blocked exception and implement role-aware single-approver limits, passing authenticated role and username from the API; map blocked approvals to HTTP 403 with `{ "error": reason }`. Interim until Stage 2: block ANY approval attempt for an invoice above 500,000.00 with reason `Invoices above 500,000.00 require two approvers.`; return 403 without changing the invoice or calling repository `Update`. Allow Managers, Directors, and Admins to reject with a required reason and no amount limit; reject attempts by other roles with 403. Verify blocked controller results contain the reason.
- [x] 1.3 Replace the demo Approver account with `demo.manager` (Manager), add `demo.director` and `demo.director2` (Director), retain `demo.admin` (Admin), keep `ChangeMe123!`, and update the README and seeded historical approver attribution. Verify the configured account list and roles.
- [x] 1.4 Add unit tests for Manager 50,000.00 allowed / 50,000.01 blocked and Director 500,000.00 allowed; until Stage 2, test that ANY role's approval attempt above 500,000.00 is blocked with reason `Invoices above 500,000.00 require two approvers.`, returns 403, leaves the full invoice snapshot unchanged, and makes zero repository `Update` calls. Also cover Admin and other-role blocks, exact reasons, full invoice snapshots, and zero repository `Update` calls. Verify the focused unit and API mapping tests pass.
- [x] 1.5 Run `dotnet test backend/InvoiceApp.sln`; commit Stage 1 separately only after all tests pass.

## 2. Stage 2 — Two-approver rule and negative rules (separate commit)

- [x] 2.1 Add first-approval attribution to the invoice and implement the over-500,000.00 two-step transition: Manager or Director first, invoice remains PendingApproval, and only a different Director may complete it. Remove the Stage 1 interim block for invoices above 500,000.00. Verify Manager-first and Director-first completion paths.
- [x] 2.2 Replace the Stage 1 blanket-block test for approvals above 500,000.00 with tests for 500,000.00 Director single approval / 500,000.01 two approvals, Manager or Director as first approver, Director-only second approval, and the wrong-role second-approval block. Verify each test asserts the required state and recorded approver data.
- [x] 2.3 Add same-user and case-insensitive username duplicate tests; assert the exact reason, every invoice field unchanged, and no `Update` call after each blocked attempt, then verify a different Director can complete the unchanged first approval.
- [x] 2.4 Run `dotnet test backend/InvoiceApp.sln`; commit Stage 2 separately only after all tests pass.

## 3. Stage 3 — Playwright over-limit UI test

- [x] 3.1 Add page-object-based Playwright coverage for a Manager creating and submitting a 50,000.01 invoice, attempting approval, seeing the over-limit error, and observing the invoice remain pending; use accessible role/label locators. Verify the targeted test passes.
- [x] 3.2 Run the E2E suite with `npm test` from `e2e`; finish Stage 3 with all E2E tests passing.

## 4. Stage 4 — Mutation run and PR review

- [ ] 4.1 Run Stryker on the completed implementation using the project command; record the pre-change baseline and after-change scores, source revisions, and meaningful surviving mutants in the PR summary. Verify both scores are traceable to their tested revisions.
- [ ] 4.2 Request an agent review of the PR against the project review rules; the reviewer agent proposes verdicts, and a human decides the final verdict for every finding: fixed, dismissed with a reason, or deferred with a reason. Record severity-ranked findings and each human-decided verdict, then address required fixes. Verify the review disposition is complete.
- [ ] 4.3 Run `dotnet test backend/InvoiceApp.sln` and the E2E suite with `npm test` from `e2e`; finish Stage 4 with both suites passing.
