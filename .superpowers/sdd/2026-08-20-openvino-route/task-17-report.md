# Task 17 report: gated TurboQuant adapter and explicit fallback

## Outcome

Task 17's O1-owned adapter, activation policy, evidence presentation, and
explicit fallback behavior are complete locally. The implementation remains
fail closed: the experimental route is not added to central composition and
the TurboQuant worker is not added to application packaging because the
required real pinned-Granite UCL campaign, matched quality/memory/performance
evidence, and external security/license approvals have not closed.

Consequently, this task does **not** claim normal WinUI TurboQuant acceptance
or closure of `F-M21`, `F-M22`, `N-M11`, or `DR-WF-011`.

## Implemented boundary

- Added a three-state activation policy: `Unavailable`, `Unverified`, and
  `Active`. It binds the exact evidence commit, pinned Granite model ID,
  package manifest digest, model digest and byte length, CPU requested/actual
  device, audited upstream/source build identities, closure verification, and
  security/license decisions.
- `Active` additionally requires a valid typed activation event with a nonzero
  model SDPA node count, matched official baseline, the fixed quality rubric,
  deterministic smoke, memory/quality/performance, repeatability, context,
  cancellation, cleanup, corruption, streaming, and two completed turns.
- Added `openvino.turboquant` / `openvino.turboquant.cpu.tbq4` as an
  `Experimental` route adapter over the established route-neutral prompt
  lifecycle. Activation binds the approved worker build, package manifest,
  model digest, and model length before the worker can start.
- Added five bounded evidence rows: maturity, requested/actual KV cache,
  build, activation, and matched memory/quality/performance disposition.
- Added inert, single-use fallback offers. Verified official OpenVINO is
  ordered before an applicable verified GGUF route; neither runs without
  explicit user confirmation. The returned session must report the selected
  route/configuration and must not retain TurboQuant branding.
- Exposed only the path-free retained package-manifest digest on the existing
  handoff lease so the adapter can bind package identity without exposing a
  local path.

## Verification

- Focused TurboQuant Release tests: 41/41 passed, zero skipped.
- Full OpenVINO application unit suite with the verified official worker
  stage: 269/269 passed, zero skipped.
- Release x64 application build succeeded with MSBuild 18.7.8.
- The packaging target emitted `worker_manifest_valid` and
  `worker_manifest_digest_valid` for the official stage manifest digest
  `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3`.
- `git diff --check` passed before staging.

## Deliberately closed integration points

- No central route/capability registration.
- No shared prompt-page exposure.
- No `OpenVino/TurboQuant/Worker` packaging entry.
- No automatic fallback or backend/cache substitution.
- No UCL workflow dispatch and no synthetic evidence promoted to acceptance.

Those changes remain owned by the serial I0/C1 integration step and are
permitted only after all external evidence agrees on one immutable commit.
