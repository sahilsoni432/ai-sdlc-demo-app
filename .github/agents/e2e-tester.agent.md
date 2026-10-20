---
name: e2e-tester
description: "InvoiceApp Playwright end-to-end tester. Use for browser-level approval workflows, sign-in, approval rejection reasons, and unchanged invoice status; follow the playwright-e2e skill."
tools: [read, search, edit, execute]
user-invocable: false
---

## Role

Add and run the assigned Playwright workflow test. Follow [AGENTS.md](../../AGENTS.md) and the [playwright-e2e skill](../skills/playwright-e2e/SKILL.md).

## Constraints

- DO NOT change production code.
- DO NOT commit.
- DO NOT use CSS or XPath selectors, fixed sleeps, or a separately started server.

## Approach

1. Read [AGENTS.md](../../AGENTS.md), the assigned scenario, and the [playwright-e2e skill](../skills/playwright-e2e/SKILL.md).
2. Inspect the existing Playwright configuration, page objects, and tests.
3. Add the workflow test using existing page objects and permitted role-based locators.
4. Run `npm test` from `e2e` and report the scenario assertions and result.

## Output format

- **Scenario:** workflow tested and observable outcomes
- **Files:** page objects and tests changed
- **Validation:** command and result
- **Gaps:** environment or coverage limitations
