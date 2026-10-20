---
name: approval-rule-tests
description: Write or update xUnit tests for InvoiceApp approval rules, including boundary pairs, exact blocked reasons, immutable invoice snapshots, and repository Update counts. Use when adding, changing, or reviewing approval-rule tests.
---

# Approval rule tests

## When to use

- A Domain approval rule is added, changed, or needs stronger regression coverage.
- A blocked approval must be proven not to mutate invoice state.

See [AGENTS.md — Approval limits](../../../AGENTS.md#approval-limits) for the authoritative rules and [AGENTS.md — Testing expectations](../../../AGENTS.md#testing-expectations) for project-wide requirements.

## Procedure

1. Add tests in `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs`. Name each test after the rule it proves; use one test per rule.
2. For each threshold or eligibility condition, test a boundary pair: an allowed value and the adjacent blocked value.
3. For **every** blocked case, assert the exact reason, snapshot **all** invoice fields before the attempt, and assert every field is unchanged afterward. Also assert `FakeInvoiceRepository.Update` was called zero times.
4. Use the pattern in [blocked-attempt-test-template.cs](./assets/blocked-attempt-test-template.cs); include every field from the current `Invoice` model in the snapshot.
5. Cover both negative rules explicitly. For the same-user rule, test an identical username and one differing only by letter case. For each blocked attempt, prove nothing changed using step 3. After a blocked same-user second approval, assert the first approval is still recorded unchanged and that a different Director can still complete the approval.
6. Assert concrete outcomes and reasons. Never write a test that only asserts “no exception.”
7. Run `dotnet test backend/InvoiceApp.sln` and confirm the new tests pass.

## Done when

- [ ] Each rule has a named test and each boundary has allowed and blocked coverage.
- [ ] Every blocked case checks its exact reason, every invoice field, and zero repository updates.
- [ ] Both same-user cases, including different letter case, are covered.
- [ ] Tests pass and would fail if the rule were removed.
