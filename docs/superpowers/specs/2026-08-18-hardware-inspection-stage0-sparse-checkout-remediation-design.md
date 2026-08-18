# Hardware Inspection Stage 0 Sparse-Checkout Remediation Design

**Status:** Approved for planning on 2026-08-18
**Scope:** GitHub-hosted, repository-only Stage 0 checkout remediation
**Failed run:** `32138539513`
**Failed revision:** `0b7da6413ba20508928c2033b88dc3d3efd10143`
**Git-resolution follow-up failed run:** `32155463873` (attempt `1`)
**Git-resolution follow-up failed revision:** `b985ec7d9fe11aedd83afa9ba657699ed115a19f`

## Verification erratum — 2026-08-18

Task 2's local sparse-checkout proof showed that Git cone mode's parent-directory semantics materialize repository-root files even when the configured cone is `docs/superpowers/specs`. The observed evaluated checkout included `Initialize-Repository-Structure.ps1` and `IBM Granite with TurboQuant (Intel).slnx`, so the evaluated boundary was not limited to inert specifications.

The correction is evaluated-only. The control checkout remains the exact six-directory cone with `sparse-checkout-cone-mode: true`. The evaluated checkout now uses one root-anchored non-cone directory pattern:

```yaml
          sparse-checkout: |
            /docs/superpowers/specs/
          sparse-checkout-cone-mode: false
```

That pattern is bounded to the 22 inert Markdown specifications under `docs/superpowers/specs`; no evaluated code, project, executable, workflow step, or test runs. The corrected canonical workflow SHA-256 is `db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3`.

## Security verification erratum — 2026-08-18

Follow-up verification found that the pinned `actions/checkout` action can fall back to a full REST archive when Git is missing or too old to support sparse checkout. The fixed first executable step now performs a privacy-safe Git capability precheck before either checkout, accepts only a single strict version line at Git `2.28.0` or newer, and fails with a fixed message without exposing command output or paths. This prevents REST fallback from materializing a broad worktree before the sparse boundary is established.

The evaluated checkout now resolves the immutable `steps.approval.outputs.approved_sha` value, not the movable `steps.approval.outputs.source_ref`. `source_ref` remains validator output and summary provenance only. The precheck and immutable SHA pin therefore prevent both broad REST fallback materialization and movable-ref pre-materialization. The corrected canonical workflow SHA-256 is `db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3`.

## Git command-resolution verification erratum — 2026-08-18

Hosted attempt-1 run `32155463873` reached the first Git capability gate and failed with the fixed privacy-safe error. The official Windows image inventory lists `git version 2.55.0.windows.3`, which satisfies the strict capability expression. The failure instead came from treating every `Application` returned by `Get-Command git` as one invocation target when the hosted `PATH` exposes multiple Git applications, including `bin` and `cmd`.

The precheck now resolves the first PATH-ordered `Application` deterministically with `Select-Object -First 1`. This matches the pinned checkout implementation, whose bundled `which('git', true)` lookup selects `matches[0]`, as well as a plain `git` invocation. The existing contract identity proves that a valid first application is used even when a second result is invalid, that an invalid first application is rejected even when a second result is valid, and that every failure remains the same fixed message without exposing either path or command output. Exactly 12 test identities remain. The resulting canonical workflow SHA-256 is `db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3`.

## Problem

The first manual Stage 0 dispatch failed in the initial `actions/checkout` step on GitHub's `windows-latest` runner. The reviewed `main` revision was fetched successfully, but a full worktree checkout tried to materialize unrelated repository paths whose absolute Windows paths were 260 to 268 characters long. Git exited with code 128 before Python setup, contracts, approval-manifest validation, evaluated-source comparison, or summary publication.

This was a repository checkout failure, not a Hardware Inspection or LLM Fit result. The UCL Intel laptop, self-hosted runners, candidate executable, hardware capture, network controls, and evidence paths were not contacted or executed.

## Constraints

- Preserve the existing Hardware Inspection implementation, UI, candidate logic, evidence contracts, Gate 1 disposition, and Gate 2 prohibition.
- Keep Stage 0 manual, owner-only, first-attempt-only, GitHub-hosted, and repository-only.
- Continue using the existing immutable `actions/checkout` and `actions/setup-python` pins.
- Execute validators and tests only from the default-branch control checkout.
- Treat the evaluated feature checkout as identity-only data and never execute it.
- Preserve the full workflow and Hardware Inspection script inventory checks.
- Keep LFS and submodules disabled, checkout credentials unpersisted, and artifact upload absent.
- Keep exactly 12 Stage 0 contract-test identities.

## Considered approaches

### 1. Cone-mode control and non-cone evaluated sparse checkout — selected

Materialize only the repository directories required by Stage 0. The control checkout uses cone mode, while the evaluated checkout uses one root-anchored non-cone directory pattern. This follows existing repository workflow patterns, avoids unrelated Windows-incompatible paths, reduces untrusted content, and retains `actions/checkout` authentication and cleanup behavior.

### 2. Enable `core.longpaths` before full checkout — rejected

This is smaller textually but still materializes both complete trees. It unnecessarily exposes Stage 0 to unrelated repository content and remains vulnerable to future Windows-invalid names, reserved names, and case collisions.

### 3. Replace `actions/checkout` with custom Git commands — rejected

This would duplicate authentication, token masking, safe-directory handling, immutable-ref fetching, cleanup, and partial-clone behavior. It would expand the security surface without improving the Stage 0 boundary.

## Selected checkout design

The default-branch control checkout will use cone-mode sparse checkout for exactly these directory cones:

```text
.github/hardware-inspection
.github/workflows
docs/superpowers/specs
docs/testing/runbooks
scripts/hardware-inspection
tests/testing/hardware_inspection
```

Cone-mode parent semantics also materialize `scripts/README.md`, which the contract suite validates. The selected directories deliberately include:

- all workflows, so an alternative Stage workflow cannot be hidden by sparsity;
- all Hardware Inspection scripts, so an operational or adapter-control script cannot be hidden;
- all testing runbooks, so a copied Gate 1 operational runbook remains detectable;
- the approval manifest, canonical workflow, validator, design, runbook, and contract module.

The evaluated feature checkout will use this exact root-anchored non-cone pattern and mode:

```yaml
          sparse-checkout: |
            /docs/superpowers/specs/
          sparse-checkout-cone-mode: false
```

The root-anchored non-cone directory pattern is bounded to the 22 inert Markdown specifications. No evaluated script, project, executable, workflow step, or test is run. `rev-parse HEAD` binds the full commit identity to the approved SHA even though only Markdown design documents are materialized. `git status --porcelain --untracked-files=all` validates the resulting sparse worktree state; it is not represented as proof that every tracked file was materialized.

Both checkouts retain:

```yaml
fetch-depth: 1
persist-credentials: false
```

The control checkout retains `sparse-checkout-cone-mode: true`; the evaluated checkout uses the exact non-cone block above.

Before either checkout, the first executable step selects the first PATH-ordered Git application and requires Git `2.28.0` or newer using the fixed privacy-safe precheck; the evaluated `actions/checkout` step consumes `steps.approval.outputs.approved_sha`, while `source_ref` remains provenance metadata emitted by the validator.

No explicit checkout filter or global Git configuration is added. The pinned checkout action may apply its own partial-clone optimization internally.

## Execution flow

1. GitHub creates one `windows-latest` `hosted-preflight` job after the existing owner, default-branch, first-attempt, and confirmation guards pass.
2. The first executable step performs the fixed Git `2.28.0+` capability precheck and fails closed before any checkout if the requirement is unavailable.
3. The control checkout materializes only the approved cones.
4. Python 3.12.10 is configured and the same 12 Stage 0 contracts run from `control`.
5. The control validator checks dispatch context and the strict approval manifest, emitting `source_ref` as provenance and `approved_sha` as the immutable checkout value.
6. The evaluated checkout materializes only the 22 inert design specifications at `approved_sha` using the root-anchored non-cone pattern.
7. The control validator compares evaluated `HEAD` to the approved SHA, verifies sparse worktree cleanliness, and publishes the fixed safe summary.
8. No artifact is uploaded.

Any checkout, contract, manifest, identity, or cleanliness failure stops the job before later stages. A checkout fallback or runner drift that cannot honor the sparse boundary is expected to fail closed rather than authorize a laptop stage.

## Test design

The existing workflow contract is updated test-first. Before the security correction, focused existing identities must fail because the first Git precheck is absent and the evaluated checkout still uses the movable `source_ref`; the current pre-security digest remains in place during RED. The production change then adds the exact first precheck, switches evaluated materialization to `approved_sha`, and updates the canonical workflow SHA-256.

The existing test identity will assert:

- exactly two `sparse-checkout` blocks;
- exactly one `sparse-checkout-cone-mode: true` setting and one `sparse-checkout-cone-mode: false` setting;
- the exact ordered control cone list and root-anchored evaluated directory pattern;
- the exact first Git precheck step, including deterministic first-application resolution, its strict version matrix, fixed failure output, no path/network content, and pre-checkout placement;
- evaluated `approved_sha` materialization with `source_ref` retained only as provenance metadata;
- no explicit filter or `core.longpaths` setting;
- both immutable checkout pins and both `persist-credentials: false` settings remain;
- control execution and evaluated identity-only ordering remain unchanged.

The complete 12-test suite, PowerShell 5.1 Git-precheck semantic matrix (including ordered duplicate application results), Python compilation, PowerShell 5.1 parser, canonical hashes, UTF-8/no-BOM checks, and commit-range whitespace checks must pass. A local owned temporary sparse checkout will run the 12 contracts from the control cone and verify evaluated SHA/cleanliness behavior after the Git precheck prerequisite and immutable SHA materialization are confirmed.

## Delivery and verification

The remediation is delivered in a separate fix branch and pull request based on the failed `main` revision. The PR changes only the workflow, its contract test, and this approved remediation documentation plus its implementation plan.

After review and merge, create a new manual `workflow_dispatch` run. Do not rerun failed runs `32138539513` or `32155463873`, because a rerun would have `run_attempt` greater than 1 and must remain rejected by the existing guard.

Success requires exactly one hosted job, all 12 contracts passing, both validator phases succeeding, the fixed safe summary, zero uploaded artifacts, and no self-hosted job or Intel-laptop contact.

## Non-claims and deferred work

This remediation does not validate the candidate, Intel hardware, offline behavior, compatibility, packaging, or licensing. Gate 1 remains **Blocked**, Gate 2 must not start, and Stages A through D remain subject to their separate plans and approvals.
