# H1 R4 hardware and compatibility closure

## Decision

H1 is complete at implementation subject `99294b544f1f2f53c8b1d19456ded6fac0f2b3bb`, tree `e96ce08218d96d3f430b9267c3fb98e2636bf3ae`, based on immutable C0 candidate `3be52f0b0763cd0ba164b13914d8b0b4f37433e8`.

The Hardware Inspection flow now distinguishes a genuinely absent optional `llmfit` package from an unsafe or invalid package. When it is genuinely absent, Windows and packaged llama.cpp evidence still run, available hardware facts are retained in a display-only snapshot, and the UI completes with bounded warning copy. No actionable compatibility handoff is created. Invalid hashes, inaccessible paths, reparse paths, malformed manifests, and required packaged-probe failures remain fail-closed.

## Implementation

- Added typed `ToolNotAvailable` evidence and `DisplayOnly` snapshot usability without renumbering existing enum members.
- Allowed a verified llama.cpp probe lease when only optional `llmfit` is absent.
- Continued Windows, DXGI, NPU, storage, and llama.cpp collection in that degraded case.
- Preserved the snapshot for display/reporting while withholding the compatibility handoff.
- Required `DisplayOnly` to pair with `CompletedWithWarnings`; rejected undefined and `NotUsable` completed snapshots.
- Added truthful degraded-state copy and regression contracts for absence, unsafe paths, custody, resolution, terminal publication, and compatibility gating.

## Verification

| Gate | Result |
|---|---:|
| Hardware Inspection foundation | 234/234 passed |
| Packaged llama.cpp probe contracts | 22/22 passed |
| Model Hardware Compatibility | 1062/1062 passed |
| Hardware packaging/trust Pester | 10/10 passed |
| Debug x64 managed UnitTests build | passed; 0 errors, 13 inherited warnings |
| Static package matrix | 4/4 passed |
| Packaged probe manifest | passed |
| Native llama.cpp identity/capabilities | 2/2 passed; zero residual processes |
| Independent review | no remaining Critical or Important findings |

Non-overlapping executed test total: 1328 discovered, 1328 executed, 1328 passed, 0 failed, 0 skipped.

## Bounded limitations and non-claims

The approved `llmfit.exe` was present and its SHA-256 matched `db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19`, but Windows Application Control blocked process creation. Therefore native `llmfit` functional acceptance is not claimed. The unpackaged UnitTests assembly lacks WinUI package context, while the packaged VSTest recipe deployed but did not establish its runner handshake; no zero-test result is counted as behavioral evidence. App launch and screenshot capture were consequently blocked, so no visual acceptance is claimed.

No signed-package, performance, release, or broad native acceptance is claimed. The native disposition is mixed: the packaged llama.cpp probe passed, and exact `llmfit` execution was blocked by host policy.

## Review

The independent reviewer initially identified three Important findings: invalid usability combinations, over-specific degraded copy, and unsafe path classification. Commit `99294b54` closed all three. Re-review reported no remaining Critical or Important findings.

The implementation branch is `validation/ucl-h1-intel-candidate-r4`. Because the R4 receipt schema permits only `audit`, `test`, or `integration` transport refs, the identical schema-compliant remote alias is `audit/ucl-h1-intel-candidate-r4`; both refs identify the same final commit.
