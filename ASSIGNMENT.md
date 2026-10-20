# AI in SDLC Assignment

## Mutation scores

| Run (before/after) | Date | Mutation score | Killed | Survived | No coverage | Notes |
|---|---|---:|---:|---:|---:|---|
| before | 2026-10-21 | 30.77% | 12 | 10 | 17 | HEAD 7dcc7e7; backend/src/InvoiceApp.Domain/ApprovalService.cs:27 removes Submit repository Update; :39 removes Approve repository Update; :51 removes Reject's pending-state guard.  All 17 no-coverage mutants are in DiscountCalculator.cs (legacy, untested, out of scope). ApprovalService.cs alone: 12 killed / 8 survived (60%). |
