# Task 12 Report — Atomic Conversion Publication and UI Flow

Date: 2026-08-22

## Result

Implemented the sealed dense-Granite-to-OpenVINO conversion transaction and the
shared-page conversion flow. Conversion now retains the inspected source identity,
uses a fresh sibling staging directory, runs the pinned offline converter in the
protected worker, performs official native validation and a real one-token CPU
smoke, writes strict sanitized provenance, atomically publishes, and independently
reinspects the published package.

The UI exposes explicit confirmation, bounded stage progress, cancellation,
retry, choose-another, and distinct cancellation/hardware/conversion recovery
copy. The final converter packaging identity is intentionally supplied by the
Task 18 application packaging target.

## Security and lifecycle properties

- One-time conversion offers retain the source snapshot through conversion and
  require the reinspection manifest to match the offer.
- Source/destination overlap, resolved aliases, reparse ancestry, existing final
  destinations, insufficient space, and stale staging identities fail closed.
- Publication is an atomic sibling rename. Post-publication reinspection failure
  or cancellation performs an identity-checked rollback; foreign replacements are
  never deleted.
- Converter launch uses only `python.exe -I -s -E -S -B -m converter`, an exact
  synthetic environment, offline flags, a 120-minute cap, bounded protocol I/O,
  and a caller-pinned closure manifest.
- The 23,756-file converter closure is retained read-only and SHA-256 verified
  before and after execution. Closure verification itself is cancellable.
- Provenance has an exact schema, exact reviewed dependency versions, exact fixed
  options, output length/hash inventory, and no source path, account, host,
  prompt, answer, credential, environment, or native-output field.
- Validation handoff leases are transaction-local and are disposed on every exit.

## Fresh verification

- OpenVINO unit suite with official Stage A: 216 passed, 0 failed, 0 skipped.
- Protected worker-client unit suite: 119 passed, 0 failed, 0 skipped.
- Converter isolation integration suite: 5 passed, 0 failed, 0 skipped.
- Closure-cancellation integration: 1 passed in 1.458 s; no worker launch,
  scratch directory, or staging output.
- Real protected offline conversion: 1 passed in 4m 38.368s. It preserved the
  source manifest, completed native validation and CPU smoke, published provenance,
  independently returned Ready/ReadyWithWarnings with a new inspection identity,
  and left no staging residue.
- Release x64 application build: succeeded with 0 errors. The existing
  `NETSDK1198` missing publish-profile warning remains unchanged.
- `git diff --check`: clean apart from repository line-ending notices.

## Bound evidence

- Official worker Stage A manifest SHA-256:
  `14601a19e8fbf778f15c28276e5a769f3ca9000287f1542e832d2c66aa1cf1dc`
- Converter Stage L manifest SHA-256:
  `118aab85b4910a25e83b1e32f26859fac31505a6b886e882e8ab992ba32f3e92`
- Converter Stage L:
  `C:\openvino-o1-task12-converter-stage-l`

## Cleanup

Removed the interrupted cancellation-test tree and the two earlier manual-debug
trees after resolving and checking their exact absolute paths. Also terminated
the exact abandoned Stage L `python.exe` process from the failed manual controller.
These operation-owned temporary artifacts are not recoverable.
