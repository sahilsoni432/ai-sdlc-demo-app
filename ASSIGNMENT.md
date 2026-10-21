# AI in SDLC Assignment

## 1. Overview

I worked on approval limits by role in the invoice approval service of
[`ai-sdlc-demo-app`](https://github.com/sahilsoni432/ai-sdlc-demo-app), on
`feature/approval-limits`.

The brief rules were:

- Managers may approve invoices up to 50,000.
- Directors may approve invoices up to 500,000.
- An invoice above 500,000 needs two distinct approvers, and the second must
  be a Director.
- The same user cannot count as both approvers.
- A blocked attempt must give a clear reason and change nothing.

I used GitHub Copilot (free tier) in VS Code with the Auto model. Copilot
agents produced the code, tests, specifications, and documentation. I planned,
reviewed, made decisions, and approved each step and commit.

The project uses .NET 8 (Domain and Api projects), a static web UI, xUnit,
Playwright, Stryker.NET 4.13.0, and OpenSpec 1.14.3.

My approach was to establish context first, write the OpenSpec change, then
implement in stages with approval gates. I followed that with mutation testing
and agent review, making the final review decisions myself.

## 2. How to run

Run the app:

```text
dotnet run --project backend/src/InvoiceApp.Api
```

Then open `http://localhost:5000`.

The demo accounts use the demo-only password `ChangeMe123!`:

| Username | Role |
|---|---|
| `demo.manager` | Manager |
| `demo.director` | Director |
| `demo.director2` | Director |
| `demo.admin` | Admin |

Run the unit tests (35 tests):

```text
dotnet test backend/InvoiceApp.sln
```

Run the end-to-end test (1 test):

```text
cd e2e && npm install && npx playwright install chromium && npm test
```

Install and run the mutation tool:

```text
dotnet tool install -g dotnet-stryker --version 4.13.0
```

From `backend/tests/InvoiceApp.Tests`, run:

```text
dotnet stryker --project InvoiceApp.Domain.csproj
```

Regenerate the AI-log index:

```text
node .github/hooks/scripts/build-log-index.js
```

## 3. What was built, step by step

The table maps each brief step or hand-in to its commit and result.

| Step | Commit | Result |
|---|---|---|
| Step 8 | `4b5ad28` | Added hooks to log prompt, tool, subagent, and stop events to `ai-logs/ai-interactions.jsonl`, with a Markdown transcript per session. |
| Step 1 | `c027722` | Extended [`AGENTS.md`](AGENTS.md) with approval rules, labelled negative rules, layering, commands, test and review expectations, and working agreements. |
| Step 2 | `cf2c22e` | Added five skills. |
| Step 9 | `75f70fa` | Added seven agents: an orchestrator and six subagents. |
| Step 3 | `8592133` | Added output-format hooks and a C# formatting hook. |
| Step 4 | `7dcc7e7` | Created the OpenSpec change [`add-approval-limits`](openspec/changes/add-approval-limits/), including proposal, spec, design, and tasks. |
| Hand-in 6 | `589b3e8` | Recorded the Stryker baseline. |
| Step 5, stage 1 | `5c4359d` | Implemented single-approver limits and unit tests. |
| Step 5, stage 2 | `68867d8` | Implemented the two-approver rule and negative rules, with unit tests. |
| Step 6 | `0019b62` | Added the Playwright E2E scenario for a rejected over-limit approval. |
| Hand-in 6 | `fe3360c` | Added tests to kill surviving mutants and recorded the final Stryker score. |
| Step 7 | `437a429` | Completed agent review and recorded my verdicts. |
| Hand-in 8 | `1c6c865` | Generated the AI-log index at [`ai-logs/README.md`](ai-logs/README.md). |

The log index has the full mapping from steps to transcripts.

## 4. Context engineering (what and why)

I set up project context before asking agents to implement the feature. The
goal was to make rules and expectations available in files rather than
repeating them in each prompt.

### Project instructions

[`AGENTS.md`](AGENTS.md) is the single source of truth an agent can read
cold. It states where business rules belong (Domain only), that the role is
passed from the session, and which commands and review expectations apply.

It also describes boundary-pair testing, exact blocked reasons, full invoice
snapshots, repository update counts, review severity and verdicts, and working
agreements such as two-stage commits and not changing logs or hooks.

### Skills

The five skills live in `.github/skills/` and refer to `AGENTS.md` instead of
duplicating its rules.

| Skill | Purpose captured in its context |
|---|---|
| `add-approval-rule` | Validate before mutation and map blocked approvals to 403. |
| `approval-rule-tests` | Cover boundary pairs and both negative rules; check the snapshot and update count. |
| `playwright-e2e` | Use page objects and role/label locators for the over-limit scenario. |
| `mutation-testing` | Run Stryker 4.13.0 and record before-and-after results. |
| `pr-review-verdicts` | Review with a checklist; the agent proposes verdicts and I decide them. |

### Agents

The seven agents are defined in `.github/agents/`.

`feature-lead` is the orchestrator. It delegates work, stops for my approval
after the plan and before each commit, and leaves final review verdicts to me.
Its permitted commands are git and `dotnet build`/`dotnet test`.

The six subagents have bounded roles:

- `planner` is read-only.
- `implementer` handles implementation.
- `test-writer` handles tests.
- `e2e-tester` handles end-to-end testing.
- `mutation-analyst` handles mutation testing.
- `reviewer` is read-only and proposes verdicts.

### Hooks

The hooks live in `.github/hooks/`.

The logging hook records events to JSONL and creates a transcript per session
on Stop. It redacts and truncates content and is designed not to fail the
agent.

The output-format hooks inject each subagent's required output template at
SubagentStart. At SubagentStop, they validate required sections and send the
answer back if a section is missing.

The C# formatting hook runs `dotnet format whitespace` after the implementer
or test-writer edits a `.cs` file.

### Evidence from use

Stages 1–3 were implemented from a three-line prompt: “Implement Stage N of
the OpenSpec change… change files and AGENTS.md are the source of truth.”
When the context had gaps, I fixed the files rather than expanding the prompt.

## 5. Spec and human decisions

I chose OpenSpec because this change modifies an existing system, uses deltas,
is the lightest process for this work, and runs on Node.

I made the design decisions that the brief left open:

| Decision | My choice |
|---|---|
| Limit boundaries | Limits are inclusive: 50,000.00 and 500,000.00 are allowed. |
| Two-approver order | Above 500,000.00, the first approver may be a Manager or Director; the second must be a different Director. |
| Roles that approve | Only Manager and Director approve. Admin retains admin rights but cannot approve. I replaced the demo users. |
| Blocked approval | A blocked attempt throws a dedicated Domain exception mapped to HTTP 403 with `{"error": reason}`. |
| Rejection | Manager, Director, and Admin may reject with a required reason and no amount limit. |

I clarified ambiguities by hand in `design.md`:

- Decision 10 specifies the order of Stage 1 checks.
- Decision 11 specifies the Stage 2 flow and order of checks.
- The extension to decision 7 specifies exact E2E assertions, unique data, and
  targeting the invoice by ID.
- A note under decision 11 clarifies that existence and pending-state checks
  apply to every user first.

During Stage 1, approval above 500,000.00 was temporarily blocked with the
reason “Invoices above 500,000.00 require two approvers.” Stage 2 removed that
interim rule and replaced its tests.

The complete proposal, spec, design, and task files are in
[`openspec/changes/add-approval-limits/`](openspec/changes/add-approval-limits/).

## 6. Tests per rule

There are 35 unit tests and 1 E2E test. The test files are in
[`backend/tests/InvoiceApp.Tests/`](backend/tests/InvoiceApp.Tests/) and
[`e2e/tests/`](e2e/tests/).

The table names representative rule coverage from the test suite.

| Rule | Coverage |
|---|---|
| Manager limit: 50,000 | `Approve_ManagerMayApproveAtExactly50000`; `Approve_BlocksManagerAt50000Point01WithoutChangingInvoice`; `Approve_BlocksManagerAtExactly500000WithoutChangingInvoice`; controller test `Approve_MapsBlockedApprovalTo403WithExactErrorBodyAndNoMutation` for Manager at 50,000.01. |
| Director limit: 500,000 | `Approve_DirectorMayApproveAtExactly500000`. |
| Above 500,000 requires two approvers; second is Director | `Approve_RecordsFirstApprovalAbove500000WithoutDecidingInvoice` covers Manager or Director first; `Approve_DifferentDirectorCompletesApprovalAbove500000` covers Manager→Director and Director→Director2; `Approve_BlocksManagerAsSecondApproverWithoutChangingFirstApproval`; `Approve_ManagerFirstThenSameManagerRetryIsBlockedByDirectorOnlyRule`; controller 403 cases. |
| Negative rule 1: same user cannot be both approvers | `Approve_BlocksSameUserAsSecondApproverAndDifferentDirectorCanComplete` checks `demo.manager` and `DEMO.MANAGER`, confirms the first approval remains, and then allows a different Director to complete. Controller 403 cases cover `demo.director` and `DEMO.DIRECTOR`. |
| Negative rule 2: a blocked attempt gives a reason and changes nothing | Every blocked test checks the exact reason, an unchanged full 10-field invoice snapshot including `FirstApprovedBy` and `FirstApprovedAt`, and `UpdateCount == 0`. The controller test checks HTTP 403 and exactly `{"error": reason}`. The E2E test reloads and checks that status remains “Pending Approval.” |
| Only Manager and Director can approve | `Approve_IneligibleRoleTakesPrecedenceAbove500000AndLeavesInvoiceUnchanged` and `Approve_BlocksIneligibleRoleAtNormalAmountWithoutChangingInvoice` cover Admin and Clerk. |
| Rejection follows decision 5 | Controller tests cover `Reject_AllowsManagerDirectorAndAdminWithRequiredReasonAndNoAmountLimit`, `Reject_ForbidsOtherRolesWithoutChangingInvoice`, and `Reject_RequiresAReasonAtTheApiBoundary`; `Reject_RejectsInvoiceThatIsNotPendingWithoutChangingInvoice` covers lifecycle state. |
| E2E over-limit scenario | [`approval-limits.spec.ts`](e2e/tests/approval-limits.spec.ts) runs “Manager cannot approve an invoice above the approval limit.” `demo.manager` creates and submits 50,000.01 using a unique name, targets the invoice by ID, and attempts approval. The UI shows exactly “Manager may approve invoices up to 50,000.00.” and the invoice remains “Pending Approval” before and after reload. |

I reviewed every test before its commit. In the Stage 1 review, I found and
added coverage for three gaps: Manager at 500,000.00, Admin at a normal amount,
and the stale username.

## 7. Mutation scores

ApprovalService.cs went from 12 killed / 8 survived (60%) before the feature to 47 killed / 0 survived (100%) after it; the overall score went from 30.77% to 71.21%. No surviving mutant was in the new approval-limit logic. The remaining gap is the legacy DiscountCalculator.cs, which has no tests (17 no-coverage mutants, out of scope), plus two Invoice default-value mutants that I dismissed.

| Run (before/after) | Date | Mutation score | Killed | Survived | No coverage | Notes |
|---|---|---:|---:|---:|---:|---|
| before | 2026-10-21 | 30.77% | 12 | 10 | 17 | HEAD 7dcc7e7; backend/src/InvoiceApp.Domain/ApprovalService.cs:27 removes Submit repository Update; :39 removes Approve repository Update; :51 removes Reject's pending-state guard.  All 17 no-coverage mutants are in DiscountCalculator.cs (legacy, untested, out of scope). ApprovalService.cs alone: 12 killed / 8 survived (60%). |
| after | 2026-10-21 | 71.21% | 47 | 2 | 17 | FINAL run on 0019b62 plus the survivor-killing tests added in the commit that records this row; Stryker 4.13.0, `dotnet stryker --project InvoiceApp.Domain.csproj` from `backend/tests/InvoiceApp.Tests`; report: `backend/tests/InvoiceApp.Tests/StrykerOutput/2026-10-21.13-30-05/reports/mutation-report.html`. ApprovalService.cs alone: 47 killed / 0 survived (13 ignored). All 17 no-coverage mutants remain only in DiscountCalculator.cs. Prior after run: 63.64% (42 killed / 7 survived / 17 no coverage; ApprovalService.cs 42 killed / 5 survived). The five prior ApprovalService survivors are now killed: Submit invalid-state message (Submit_RejectsInvoiceThatIsNotADraft), Submit repository Update (Submit_MovesDraftToPendingApproval), Reject pending-state guard (Reject_RejectsInvoiceThatIsNotPendingWithoutChangingInvoice), missing-invoice message (Approve_ThrowsWhenInvoiceDoesNotExist), and pending-state message (Approve_RejectsInvoiceThatIsStillADraft and Reject_RejectsInvoiceThatIsNotPendingWithoutChangingInvoice). Final survivors, both dismissed (not equivalent mutants; untested defaults with no dependent behaviour): Invoice.cs:5 Id default changed from empty string to "Stryker was here!" and Invoice.cs:6 CustomerName default changed likewise. These in-memory model constructor defaults have no dependent behavior; asserting empty strings would not catch a real mistake. Report lists 34 covering test IDs per mutant, but neither mutation was killed. No survivors in ApprovalBlockedException.cs. |

## 8. Review findings and verdicts

| # | Severity | File:line | Finding | Proposed verdict (agent) | Final verdict (human) | Reason |
|---|---|---|---|---|---|---|
| 1 | Minor | `backend/src/InvoiceApp.Domain/ApprovalService.cs:34` | Eligibility check follows the pending-state check, so a non-pending invoice gets the lifecycle 409 before eligibility. | fixed (agent proposal) | dismissed | State checks (invoice exists, invoice is PendingApproval) intentionally run before eligibility checks and apply to every user; a non-pending invoice cannot be decided by anyone, so 409 with ‘…only pending invoices can be decided.’ is a clear reason, nothing changes, and Reject behaves the same way. The spec was ambiguous, so design.md decision 11 was clarified. |

## 9. Where I intervened (human review during development)

I reviewed the context, plans, implementations, and mutation results. The
following table records findings and my verdicts.

| Area | Finding | My verdict |
|---|---|---|
| Logging hooks | Long values were truncated silently. | Fixed by adding a marker. |
| Logging hooks | Empty or bad input was skipped silently. | Fixed by recording a `parseError` record. |
| Logging hooks | Copilot replies were not logged. | Fixed with a transcript per session and backfill of earlier sessions. |
| `AGENTS.md` | 403 was missing from the error-mapping line. | Fixed by hand. |
| `AGENTS.md` | Negative rules were not labelled. | Fixed by hand. |
| Skills | The E2E skill assumed a seeded invoice above 50,000. | Fixed to create the invoice through the UI. |
| Skills | The mutation skill would overwrite its own template. | Fixed by recording in `ASSIGNMENT.md`. |
| Skills | The test skill missed that a first approval must survive a blocked same-user attempt. | Fixed. |
| Skills | Anchor links did not match headings. | Dismissed; agents read the whole file. |
| Agents | The reviewer assigned final verdicts itself. | Fixed: the agent proposes and I decide. |
| Agents | The reviewer has the execute tool. | Dismissed; it needed the tool for `git diff` and was forbidden to edit. |
| Agents | The feature-lead wrote files through shell commands, bypassing its “delegate everything” rule. | Fixed by limiting execution to git and `dotnet build`/`dotnet test`. |
| Output-format hooks | The C# format hook ran on file reads. | Fixed. |
| Output-format hooks | The include path was wrong. | Fixed and verified on a scratch copy. |
| Output-format hooks | The timeout equalled the hook timeout. | Fixed. |
| Output-format hooks | The first live test injected nothing because SubagentStart sends `agentName`, not `agent_type`. | Found through the logging hook and fixed. |
| Output-format hooks | Copilot's runtime ignored the documented `hookSpecificOutput` shape. | Fixed after checking official docs; verified that the template was injected in the session record. |
| Spec | Stage 1 behaviour above 500,000 was unspecified. | Fixed with the interim rule. |
| Spec | Reject access was vague. | Fixed with decision 5. |
| Spec | The Manager-at-500,000.00 blocked scenario was missing. | Fixed. |
| Spec | Human verdicts were not stated in task 4.2. | Fixed. |
| Spec | A duplicate scenario was present. | Dismissed. |
| Stage 1 plan | The plan omitted the `AGENTS.md` demo accounts. | Fixed. |
| Stage 1 plan | Tests were ordered before code, which would break the build. | Fixed. |
| Stage 1 plan | The plan lacked `UpdateCount == 1` on success. | Fixed. |
| Stage 2 plan | The snapshot omitted the two new first-approval fields. | Fixed so a blocked attempt could not change the first approval undetected. |
| Stage 2 plan | The plan did not remove interim tests and missed the “first approval survives” follow-up. | Fixed. |
| Stage 3 plan | It asked who creates the invoice, skill or spec. | Resolved in the spec: the same Manager creates it. |
| Stage 1 code review | Tests missed Manager at 500,000.00, Admin at a normal amount, and still used the removed `demo.approver` username. | Fixed. |
| Code review | Role names were string literals. | Dismissed; this is a small demo and no convention requires constants. |
| Code review | The state check runs before the eligibility check. | Dismissed because state errors apply to everyone; this was later confirmed in the PR review. |
| Mutation survivors | Five survivors were in pre-existing ApprovalService code: Submit save, Reject pending guard, and three messages. | Fixed with tests. |
| Mutation survivors | Two survivors changed Invoice default values. | Dismissed because no behaviour depends on them. |

Every stage was additionally verified by a live API run or a repeated E2E run
before I approved it.

## 10. Limitations / deferred

| Item | Status |
|---|---|
| The UI does not show “first approval by …” for invoices above 500,000.00. | Deferred; the Stage 2 spec called for no UI change. |
| `DiscountCalculator.cs` has no tests and accounts for 17 no-coverage mutants. | Out of scope and deferred. |
| A live SubagentStop “send back” was not triggered. | Validation was proven by replaying payloads; subagents followed the injected template. |
| Role names are string literals. | Dismissed during review. |
| Authentication uses the demo's in-memory session store. | Usernames in config are case-sensitive at login, while the two-approver rule compares case-insensitively. |

## 11. AI usage and disclosure

GitHub Copilot (free tier, Auto model) did the building: code, tests, spec,
documentation, and commits. About 60 Copilot sessions are logged in
[`ai-logs/`](ai-logs/); the index is [`ai-logs/README.md`](ai-logs/README.md).

I also used Claude Code as a learning and second-opinion tool for read-only
checks outside these logs. At my request, it made one repository edit:
removing duplicated text in `AGENTS.md` lines 19–20. This is also noted in
[`ai-logs/README.md`](ai-logs/README.md).

The setup chat covered installing .NET, forking, and cloning. It happened
before the hooks existed, so I exported it manually to
[`ai-logs/transcripts/00-setup-chat.json`](ai-logs/transcripts/00-setup-chat.json).

## 12. Demo video

**TODO — add video link after recording.**

Planned length: 8–10 minutes.

Planned live demonstrations: unit tests, a Stryker run, and the Playwright E2E
test.
