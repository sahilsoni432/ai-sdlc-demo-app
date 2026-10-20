# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 8 Web API with a small static web UI.

## Layout

- `backend/src/InvoiceApp.Domain`: business rules, no web or storage code. `ApprovalService` owns the invoice lifecycle (Draft, PendingApproval, Approved, Rejected).
- Approval-limit rules live in Domain (`ApprovalService` or a dedicated policy class). The API gets the approver's role from the session and passes it into Domain; Domain must never read HTTP or session state.
- `backend/src/InvoiceApp.Api`: controllers, sign-in (`Auth/`), in-memory repository, and the web UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, one test class per class under test, named `<ClassUnderTest>Tests`.
- `e2e`: Playwright end-to-end tests (`tests/`) and page objects (`pages/`).
- `docs/use-cases`: short briefs for features we may build.

## Approval limits (feature on branch feature/approval-limits)

- Only Manager and Director may approve; Admin retains admin rights but cannot approve.
- Manager may approve up to and including 50,000.00. Director may approve alone up to and including 500,000.00.
- Above 500,000.00 requires two distinct approvers: the first may be a Manager or Director; the second must be a Director.
- Negative rule 1: the same user can never count as both approvers; compare usernames case-insensitively.
- Negative rule 2: a blocked attempt must return a clear reason and change nothing: no status, approver, first approval, timestamp, or repository `Update` call. Throw a dedicated Domain exception; `ApiControllerBase` maps it to 403 Forbidden.
- Amounts are `decimal`; never use `float` or `double` for money.
- Repositories return the same object reference from `GetById`, and `FakeInvoiceRepository.Update` is a no-op. Validate everything before mutating; snapshot every invoice field in blocked-attempt tests and assert none changed.

## Conventions

- Controllers live in `Controllers/`, one per resource, route `api/[controller]`. Request bodies are `record` types declared inside the controller.
- Controllers inherit `ApiControllerBase` and use `Handle(...)` so service errors map to 404, 409, 400 and 403 (blocked approval).
- Every endpoint is behind sign-in. Restrict actions with `[RequireRole("...")]`: destructive actions are Admin only; approval actions follow the role rules in Approval limits.
- Business rules go in the Domain project, never in controllers.
- Register services in `Program.cs`. Repositories are interfaces in Domain with an in-memory implementation in Api.
- Every new rule or endpoint needs tests. Use `FakeInvoiceRepository` in tests.
- Use clear names; no one-letter variables or `flag` parameters.
- End-to-end tests use page objects and role-based locators (`getByRole`, `getByLabel`), never CSS or XPath selectors.

## Commands

- Run the app: `dotnet run --project backend/src/InvoiceApp.Api` then open http://localhost:5000
- Run the tests: `dotnet test backend/InvoiceApp.sln`
- Run end-to-end tests: `cd e2e && npm install && npx playwright install chromium && npm test`
- Mutation testing: install with `dotnet tool install -g dotnet-stryker --version 4.13.0`, then run `dotnet stryker` from `backend/tests/InvoiceApp.Tests`. Record the mutation score before and after changes.

## Testing expectations

- Add unit tests for every approval rule, including both negative rules. Name each test for the rule it proves (for example, `Approve_Blocked_WhenSameUserGivesSecondApproval`).
- Positive cases: Manager at 50,000.00 allowed and at 50,000.01 blocked; Director at 500,000.00 allowed alone and at 500,000.01 requires another approver; Manager or Director may be first above 500,000.00; a different Director as second approver completes approval. Admin and other roles are blocked.
- Same-user cases: the same user attempting the second approval is blocked; a username differing only by letter case is also blocked; after a blocked attempt, the first approval remains unchanged and a different Director can complete approval.
- For every blocked case (over limit, wrong role, wrong role for second approval, or same user), assert the exact reason, every invoice field against a pre-attempt snapshot, and that repository `Update` was not called. Extend `FakeInvoiceRepository` to count `Update` calls.
- Tests must fail if the rule is removed; do not write tests that only check for “no exception.”

## Review expectations

- List findings by severity (critical/major/minor/trivial), with file:line and a suggested fix.
- Give every finding a verdict: fixed, dismissed with a reason, or deferred with a reason.
- Keep changes within task scope; do not make unrelated refactors.

## Working agreements

- Implement in small steps: single-approver limits with tests first, then the two-approver rule; make each step its own commit.
- AI interactions are logged automatically by `.github/hooks` into `ai-logs/`. Never edit or delete files in `ai-logs/` or `.github/hooks/` unless explicitly asked.
- When adding roles or demo users, update the “Demo accounts” section in the same commit.

## Demo accounts (not real credentials)

- `demo.approver` / `ChangeMe123!` (role Approver)
- `demo.admin` / `ChangeMe123!` (role Admin)

## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.
