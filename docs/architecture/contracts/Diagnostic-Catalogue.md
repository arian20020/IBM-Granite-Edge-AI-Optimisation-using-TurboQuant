<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Diagnostic and Error Catalogue

| Code | Category | Technical trigger | Plain-English message | Recovery action | Related requirement(s) | Test ID |
|---|---|---|---|---|---|---|
| DIAG-001 | Input | _Example: file missing_ | _Message shown to the user_ | _Action the user can take_ | F-M03; F-M06 | _Test ID_ |

## Rules

- A raw exception must not be the only user-facing message.
- Every stable code needs a deterministic trigger, user message, recovery action and test.
- Preserve technical details in controlled logs without exposing secrets or personal data.

