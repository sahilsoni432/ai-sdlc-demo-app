---
name: pr-review-verdicts
description: Review InvoiceApp changes for approval-rule correctness, regression coverage, and scope, then record severity-ranked findings with a disposition for each. Use when reviewing a pull request or proposed code change.
---

# PR review verdicts

## When to use

- Reviewing a change that affects invoice approval, its API integration, tests, or related files.
- Recording actionable review findings and their final disposition.

## Procedure

1. Read [AGENTS.md — Approval limits](../../../AGENTS.md#approval-limits) and [AGENTS.md — Review expectations](../../../AGENTS.md#review-expectations). Treat AGENTS.md as the source of truth.
2. Check the change against this checklist:
   - [ ] Approval behavior matches AGENTS.md.
   - [ ] Both negative rules are enforced and tested.
   - [ ] All conditions are validated before invoice mutation.
   - [ ] The dedicated Domain exception maps to HTTP 403.
   - [ ] Each rule has meaningful tests, including blocked-state assertions.
   - [ ] Changes stay within scope; no unrelated refactors.
3. For each issue, record severity (`critical`, `major`, `minor`, or `trivial`), `file:line`, the finding, and a suggested fix. Use [review-findings-template.md](./assets/review-findings-template.md).
4. Give **every** finding exactly one verdict: `fixed`, `dismissed`, or `deferred`. For dismissed or deferred findings, state the reason. Do not leave a finding without a verdict.
5. Re-check any fix against the checklist and confirm the recorded verdict and reason accurately reflect the final state.

## Done when

- [ ] The full checklist has been considered against the change.
- [ ] Every finding has a severity, file:line, suggested fix, verdict, and applicable reason.
- [ ] Fixed, dismissed, and deferred outcomes are clearly distinguished.
