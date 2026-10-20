---
name: playwright-e2e
description: Create and run Playwright end-to-end tests for the InvoiceApp web UI. Use for browser workflows, role-based approval scenarios, and regression coverage in e2e/.
---

# Playwright end-to-end tests

## When to use

- A user-facing workflow or API-backed UI behavior needs browser-level verification.
- An approval scenario must be tested from sign-in through its visible result.

## Procedure

1. Put reusable page objects in `e2e/pages/` and test specs in `e2e/tests/`.
2. Use role-based locators only: `getByRole` and `getByLabel`. Do not use CSS selectors, XPath, or fixed sleeps; rely on Playwright's locator assertions and auto-waiting.
3. Use the project's existing `playwright.config.ts`. Its `webServer` starts the API app for the test run; do not start a separate server manually.
4. For the rejected over-limit approval scenario:
   1. Sign in using the app's supported test account/setup and create an invoice through the UI with an amount above 50,000 (for example, 50,001).
   2. Submit the invoice for approval through the UI.
   3. Sign in as a Manager, open the submitted invoice, and record its current status.
   4. Attempt to approve it.
   5. Assert the clear rejection reason is shown and the invoice status remains unchanged.
5. Run the suite with `cd e2e && npm test`.

## Done when

- [ ] Page objects and specs are in their designated folders.
- [ ] Locators use only `getByRole` or `getByLabel`; there are no CSS/XPath selectors or sleeps.
- [ ] The rejected over-limit scenario verifies both the reason and unchanged status.
- [ ] `cd e2e && npm test` passes.
