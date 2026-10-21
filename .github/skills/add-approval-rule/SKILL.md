---
name: add-approval-rule
description: Add or change invoice approval rules in InvoiceApp.Domain. Use when implementing approval limits, approver roles, or approval eligibility; keep business rules out of API controllers and preserve the single source of truth in AGENTS.md.
---

# Add an approval rule

## When to use

- A request adds or changes who may approve an invoice, or under what conditions.
- An existing approval decision needs a new validation rule.

For the authoritative limits and approval constraints, see [AGENTS.md — Approval limits](../../../AGENTS.md#approval-limits). Do not duplicate those rules here.

## Procedure

1. Read [AGENTS.md — Approval limits](../../../AGENTS.md#approval-limits) and inspect `ApprovalService` in `backend/src/InvoiceApp.Domain`.
2. Keep the rule in Domain. Ensure the API obtains the approver's role from the authenticated session and passes it into Domain; Domain must not read HTTP or session state.
3. Validate **all** applicable conditions before changing any invoice field. Blocked attempts must leave the invoice unchanged and must not call repository `Update`.
4. For a blocked approval, throw the dedicated Domain exception with a clear reason. Confirm `ApiControllerBase` maps that exception to HTTP 403.
5. Add or update tests for the rule, including blocked behavior and unchanged invoice state. Follow [AGENTS.md — Testing expectations](../../../AGENTS.md#testing-expectations).
6. When adding roles or demo users, update the Demo accounts section in `AGENTS.md` and `Users` in `backend/src/InvoiceApp.Api/appsettings.json`.
7. Run `dotnet build backend/InvoiceApp.sln` and `dotnet test backend/InvoiceApp.sln`.

## Done when

- [ ] The approval rule is implemented in Domain and uses the role passed from the API session.
- [ ] All validation precedes mutation; blocked attempts give a clear Domain exception and map to 403.
- [ ] Related tests and any required demo-account updates are complete.
- [ ] The solution build and test suite pass.
