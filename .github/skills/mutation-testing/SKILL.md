---
name: mutation-testing
description: Run Stryker mutation testing for InvoiceApp's .NET tests and interpret mutation scores, killed mutants, surviving mutants, and coverage gaps. Use to assess approval-rule test effectiveness before or after a change.
---

# Mutation testing

## When to use

- You need to measure whether tests detect changes to production behavior.
- Approval-rule tests have changed and their effectiveness needs checking.

## Procedure

1. If the global tool is not installed, install the required version: `dotnet tool install -g dotnet-stryker --version 4.13.0`.
2. From `backend/tests/InvoiceApp.Tests`, run `dotnet stryker`.
3. Read the generated report for the mutation score, killed mutants, surviving mutants, and mutants with no coverage. Inspect survivors to determine whether a test is missing or the mutant is equivalent.
4. Record each run in the **Mutation scores** table in [ASSIGNMENT.md](../../../ASSIGNMENT.md) at the repository root, marking Run as `before` or `after`. Include the date and concise notes on significant survivors or changes. If the file or table is missing, create the table from [mutation-scores-template.md](./assets/mutation-scores-template.md); never edit the template file itself.

## Done when

- [ ] Stryker completed from `backend/tests/InvoiceApp.Tests`.
- [ ] Score, killed, survived, and no-coverage counts were read from the report.
- [ ] Results and relevant observations are recorded in the table.
