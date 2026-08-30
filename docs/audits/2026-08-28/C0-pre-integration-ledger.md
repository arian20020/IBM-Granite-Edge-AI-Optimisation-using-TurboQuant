# C0 pre-integration handoff ledger

Recorded at `2026-08-28T11:31:44Z` against frozen source commit
`4748fe04f19afdf6b27c4c12502b84db325e7294` and tree
`fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.

An absent receipt is not a failed worker result. It means C0 has no authoritative
handoff to import. Candidate worktree state is read-only review context and never
substitutes for a receipt.

| Worker | Receipt validation | Transport and ref | Final tip / tree | Report identity | Evidence subject | Tests (D/E/P/F/S) | Native | C0 disposition |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A1 | Valid: schema, hashes, bytes, clean claim, Git identity, ancestry and merge base independently verified | remote; `refs/remotes/origin/audit/ucl-backend-clean-code-v1` | `294fc9674e37663da43a3adfa1fb6c978be17f38` / `822e64564f4116956357a25177ca0f95ec1a4276` | `docs/audits/2026-08-28/A1-backend-architecture-clean-code.md`; SHA-256 `3621a55df8be8a23513048b25dd4b0c3597b4fab4a799c39a724ec23f87ceefb`; 39,702 bytes | not applicable | 1708/1708/1693/4/11 | not-run | Report accepted for independent finding reconciliation; no A1 production changes exist |
| F1 | Absent: `C:\UCL-AUDIT-HANDOFFS\F1.json` not published | unavailable | unavailable | unavailable | not applicable | unavailable | unavailable | Candidate report may be reviewed, but cannot be imported |
| H1 | Absent: `C:\UCL-AUDIT-HANDOFFS\H1.json` not published | unavailable | unavailable | unavailable | required but unavailable | unavailable | unavailable | Candidate diff may be reviewed, but no H1 change may be imported |
| M1 | Absent: `C:\UCL-AUDIT-HANDOFFS\M1.json` not published | unavailable | unavailable | unavailable | required but unavailable | unavailable | unavailable | Candidate diff may be reviewed, but no M1 change may be imported |
| Q1 | Absent: `C:\UCL-AUDIT-HANDOFFS\Q1.json` not published | unavailable | unavailable | unavailable | required but unavailable | unavailable | unavailable | Candidate diff may be reviewed, but no Q1 change may be imported |
| S1 | Absent: `C:\UCL-AUDIT-HANDOFFS\S1.json` not published | unavailable | unavailable | unavailable | not applicable | unavailable | unavailable | No candidate correction may be imported |
| T1 | Valid: schema, hashes, bytes, clean claim, Git identity, ancestry and merge base independently verified | remote; `refs/remotes/origin/test/ucl-cross-feature-integration-v1` | `1a50b6cf560a1cf47bc180079f1212fd4dd4416a` / `f4e3761aec5833f601591b5e3ac3ced49343895e` | `docs/audits/2026-08-28/T1-cross-feature-integration-tests.md`; SHA-256 `c3dd2100923a7f4e39267c0383de7c1b484f55211e8faec18ec70704ad40465a`; 6,717 bytes | not applicable | 50/50/50/0/0 | not-applicable | Three test-only implementation commits accepted; report deferred to documentation checkpoint |
| E1 | Valid: schema, report hash/bytes, clean claim, Git identity, ancestry, merge base and remote tip independently verified | remote; `refs/remotes/origin/test/ucl-native-e2e-v1` | `e3e660a62cd9b247c6980b32a768ab5540d1e1a1` / `cd0a324ac06e5299675b373b411ee313beeddfe7` | `docs/audits/2026-08-28/E1-native-end-to-end-tests.md`; SHA-256 `a6706dcb0e32c95a8fe9d5d58dab5214b5d7e4308178fe46b3ee1973d9a32d82`; 9,160 bytes | not applicable | 31/31/14/0/17 | blocked | Test infrastructure accepted for selective import and C0 reconciliation; native pass not claimed |

`D/E/P/F/S` means discovered, executed, passed, failed and skipped. This ledger is
append-only in meaning: later validated receipts may replace an `Absent` row, but
the original absence and its import restriction remain part of the audit history.

## Entry-gate note

T1's test-only commits were imported after its receipt was validated and its full
diff was reviewed, before this ledger file was committed. No other specialist
implementation or report has been imported. C0 records that sequencing deviation
explicitly rather than rewriting history or fabricating an earlier ledger.
