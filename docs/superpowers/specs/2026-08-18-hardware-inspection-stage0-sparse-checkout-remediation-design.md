# Hardware Inspection Stage 0 Sparse-Checkout Remediation Design

**Status:** Approved for planning on 2026-08-18
**Scope:** GitHub-hosted, repository-only Stage 0 checkout remediation
**Failed run:** `32138539513`
**Failed revision:** `0b7da6413ba20508928c2033b88dc3d3efd10143`

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

### 1. Cone-mode sparse checkout — selected

Materialize only the repository directories required by Stage 0. This follows existing repository workflow patterns, avoids unrelated Windows-incompatible paths, reduces untrusted content, and retains `actions/checkout` authentication and cleanup behavior.

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

The evaluated feature checkout will use one inert cone:

```text
docs/superpowers/specs
```

No evaluated script, project, executable, workflow step, or test is run. `rev-parse HEAD` binds the full commit identity to the approved SHA even though only Markdown design documents are materialized. `git status --porcelain --untracked-files=all` validates the resulting sparse worktree state; it is not represented as proof that every tracked file was materialized.

Both checkouts retain:

```yaml
fetch-depth: 1
persist-credentials: false
sparse-checkout-cone-mode: true
```

No explicit checkout filter or global Git configuration is added. The pinned checkout action may apply its own partial-clone optimization internally.

## Execution flow

1. GitHub creates one `windows-latest` `hosted-preflight` job after the existing owner, default-branch, first-attempt, and confirmation guards pass.
2. The control checkout materializes only the approved cones.
3. Python 3.12.10 is configured and the same 12 Stage 0 contracts run from `control`.
4. The control validator checks dispatch context and the strict approval manifest.
5. The evaluated checkout materializes only design specifications at the manifest-selected feature ref.
6. The control validator compares evaluated `HEAD` to the approved SHA, verifies sparse worktree cleanliness, and publishes the fixed safe summary.
7. No artifact is uploaded.

Any checkout, contract, manifest, identity, or cleanliness failure stops the job before later stages. A checkout fallback or runner drift that cannot honor the sparse boundary is expected to fail closed rather than authorize a laptop stage.

## Test design

The existing workflow contract is updated test-first. Before the workflow changes, the focused contract must fail because both checkouts lack sparse inputs. The production change then adds the two exact sparse blocks and updates the canonical workflow SHA-256.

The existing test identity will assert:

- exactly two `sparse-checkout` blocks;
- exactly two `sparse-checkout-cone-mode: true` settings;
- the exact ordered control and evaluated cone lists;
- no non-cone mode, explicit filter, or `core.longpaths` setting;
- both immutable checkout pins and both `persist-credentials: false` settings remain;
- control execution and evaluated identity-only ordering remain unchanged.

The complete 12-test suite, Python compilation, PowerShell 5.1 parser, canonical hashes, UTF-8/no-BOM checks, and commit-range whitespace checks must pass. A local owned temporary sparse checkout will run the 12 contracts from the control cone and verify evaluated SHA/cleanliness behavior before publication.

## Delivery and verification

The remediation is delivered in a separate fix branch and pull request based on the failed `main` revision. The PR changes only the workflow, its contract test, and this approved remediation documentation plus its implementation plan.

After review and merge, create a new manual `workflow_dispatch` run. Do not rerun failed run `32138539513`, because a rerun would have `run_attempt` greater than 1 and must remain rejected by the existing guard.

Success requires exactly one hosted job, all 12 contracts passing, both validator phases succeeding, the fixed safe summary, zero uploaded artifacts, and no self-hosted job or Intel-laptop contact.

## Non-claims and deferred work

This remediation does not validate the candidate, Intel hardware, offline behavior, compatibility, packaging, or licensing. Gate 1 remains **Blocked**, Gate 2 must not start, and Stages A through D remain subject to their separate plans and approvals.
