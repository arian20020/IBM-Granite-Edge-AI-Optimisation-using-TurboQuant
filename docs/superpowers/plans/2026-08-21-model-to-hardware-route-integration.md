# Model-to-Hardware Route Integration Plan

**Goal:** Implement the user-approved `ModelInspectionHandoff` v2 contract and connect an eligible current Model Inspection result to one Hardware Inspection run without exposing model paths or model content to Hardware providers.

**Baseline:** `ad70d055` on `feature/hardware-inspection-functional-v1`.

**Authorities:** User-approved `HI-C0-I1-S1-DECISION-v1`; reviewed I1 document at commit `63ce50f695cde59e76649efef2d5e3172e59b0b2`, SHA-256 `A91672A4BC8AF08DF3DDFE2BDFBB56C708B0729FB6EFCF3E163215044DB3D836`; Hardware production design; approved visual contract F7/F9.

## Constraints

- The handoff contains exactly six fields: schema version, handoff ID, Model run ID, eligible outcome, model SHA-256, and validated model byte length.
- No path, filename, request/result object, free-form text, diagnostic, credential, host identity, provider payload, or compatibility result crosses the seam.
- Model Inspection alone projects the handoff from a current eligible terminal result.
- Hardware validates and claims the envelope, then carries it opaquely. Hardware providers receive none of its model fields.
- A handoff is one-use. Retry/reissue gets a new handoff ID; Model retry gets a new Model run ID; Hardware retry gets a new Hardware run ID.
- Continue remains disabled until a usable Hardware handoff and registered Block 3 route both exist.
- Do not change approved Model/Hardware visual geometry or copy except replacing the explicitly future-disabled Model action with the approved typed action when eligibility is proven.
- Do not implement Block 3 calculations, external tools, hardware probes, workflow dispatch, laptop contact, or network changes.
- Use strict test-first changes and preserve existing test identities.

## Task 1 — Closed handoff and projection

- Add failing tests for the exact six fields, schema version 2, UUID v4 roles, lowercase 64-hex digest, positive length, canonical encoding within 512 bytes, and rejection of every unknown/missing/malformed field.
- Add a Model-owned projector that accepts only a current `Ready`/`ReadyWithWarnings` result with integrity-preserved file evidence.
- Give each Model inspection attempt a cryptographically random run UUID separate from the existing presentation generation.
- Prove stale/replaced/cancelled/failed results cannot project a handoff.

## Task 2 — One-use lifecycle registry

- Add failing tests for `Issued -> BoundToHardwareRun -> Transferred` and terminal invalidation states.
- Implement atomic issue/claim/proved rollback/reissue/invalidate rules with no serialized mutable lifecycle state.
- Prove duplicate claim, wrong Model run, wrong Hardware run, stale reissue, and ambiguous rollback fail closed.

## Task 3 — Typed Model action

- Add a Model-owned Check Hardware command that is enabled only for a current projectable terminal result and registered Hardware route.
- Raise one typed event containing only the immutable handoff.
- Preserve Choose another/retry/cancel behavior and every approved Model visual contract.

## Task 4 — Shell and Hardware entry

- Let the onboarding shell own route registration, issue/claim sequencing, navigation confirmation, rollback, and stage-indicator transition.
- Hardware entry creates a fresh Hardware run ID, atomically claims the handoff, creates the Hardware ViewModel, and begins exactly once after navigation succeeds.
- Failed navigation must not start the service. Navigation away cancels the run and invalidates ambiguous ownership.
- Back returns safely to Model Inspection; Continue remains disabled because Block 3 is not part of this plan.

## Verification

- Run exact new contract/registry/route tests, all Model focused tests, all Hardware tests, both Hardware Python contracts, app/test builds, and the complete packaged suite.
- Require no path-bearing object or model field in Hardware provider/service signatures.
- Require `git diff --check`, exact scope review, and a clean worktree.

This plan authorizes repository implementation only. Gate 1 remains Blocked and Gate 2 remains prohibited; no operational execution is authorized.
