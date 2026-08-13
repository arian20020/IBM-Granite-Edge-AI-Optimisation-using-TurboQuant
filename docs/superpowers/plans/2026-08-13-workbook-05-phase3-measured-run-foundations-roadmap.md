# Workbook 05 Phase 3 and Measured-Run Foundations Roadmap

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement each package plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the approved Phase 3 design into five independently reviewable implementation packages that establish trustworthy model assets, process supervision, codec conformance, quality evidence, and a standard-cache smoke baseline before any compressed capability sweep begins.

**Architecture:** The work follows a layered evidence pipeline. Each package produces a narrow, schema-validated output that becomes an immutable prerequisite for the next package. No package may silently weaken the strict formal measured-run manifest or use a later-stage result to repair an earlier missing proof.

**Tech Stack:** Python 3.12.10, Windows PowerShell 5.1, C++20, CMake/MSVC, OpenVINO Runtime at `b9a1f201c109e0bed74763934f79483cf6c4cbf4`, OpenVINO GenAI at `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`, JSON Schema Draft 2020-12, GitHub Actions, IBM Granite 4.1.

## Approved design

The controlling specification is:

```text
docs/superpowers/specs/2026-08-13-workbook-05-phase3-measured-run-foundations-design.md
```

The project owner approved that specification on 13 August 2026. This roadmap decomposes it because the approved design contains five subsystems that can fail, be reviewed, and be accepted independently.

## Global Constraints

- Campaign identity is exactly `GTQ-WB05-MF-v1`.
- Route identity is exactly `route-a-merged-openvino`.
- Accepted Runtime install is `C:\w5a\phase2-31391119557-4\i-ov` and is read-only.
- Accepted Runtime decision is `C:\w5a\accepted-route-a-runtime-31656417607-1\decision.json`.
- Accepted Runtime decision SHA-256 is `5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38`.
- Accepted Runtime source commit is `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Accepted GenAI install is `C:\w5a\phase2-31661571860-1\i-genai` and is read-only.
- Accepted GenAI decision is `C:\w5a\accepted-route-a-genai-31661571860-1\decision.json`.
- Accepted GenAI decision SHA-256 is `0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79`.
- Accepted GenAI source commit is `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`.
- `C:\w5a\` contains accepted prerequisites; `C:\w5m\` contains immutable model assets; `C:\w5c\` contains Phase 3 probe workspaces; `C:\w5r\` contains durable run workspaces and checkpoints.
- Controlled roots and run directories must be normal local directories, never files, UNC paths, device paths, links, junctions, mount points, or other reparse points.
- Existing run directories are never silently cleaned, overwritten, or reused.
- Available physical RAM below `1610612736` bytes for ten seconds terminates the process tree.
- Windows commit use above `90` percent for ten seconds terminates the process tree.
- A heartbeat older than `900` seconds terminates the process tree.
- No workflow may alter the page file, overclock hardware, disable memory protection, or change firmware settings.
- Prompt set remains `GTQ-PROMPTS-v1`; rubric remains `GTQ-QUALITY-RUBRIC-v1`; formal score remains 0–10; formal prompts remain P1–P6.
- `measured-run-manifest.schema.json` remains strict and is not relaxed for spikes, conformance attempts, smoke attempts, blocked records, or missing evidence.
- Live laptop execution is manual from `main` only and requires the exact labels `self-hosted`, `Windows`, `X64`, `workbook05`, and `intel-target`.
- Workflows use only `contents: read` and `actions: read`, immutable action SHAs, `persist-credentials: false`, and `cancel-in-progress: false`.
- Pull requests may run repository and synthetic tests but may not download a model, execute a model, or use the Intel laptop for unreviewed code.
- Self-hosted jobs never push repository changes.
- Uploaded evidence is text/data only. Executables, DLLs, libraries, object files, model weights, tokenizers, source archives, and model archives are forbidden.
- Route B QJL and PolarQuant remain separately blocked. Route A must reject or explicitly classify unsupported Route B labels; it must never translate them into a Route A codec.

## Package dependency order

```text
C1 asset locking
  -> C2 process harness
    -> C3 activation and storage conformance
      -> C4 quality evidence
        -> C5 standard-cache smoke
          -> later low-memory-first capability sweep
```

A package may be implemented only after the preceding package's exact pull-request head, retained evidence where applicable, and acceptance decision are known. Parallel work is allowed only for repository-only documentation or tests that do not assume an unaccepted interface.

## Plan files

| Package | Plan | Independently testable result |
|---|---|---|
| C1 | `docs/superpowers/plans/2026-08-13-workbook-05-c1-asset-locking.md` | Exact, immutable diagnostic and Granite 4.1 3B model/tokenizer/conversion identities with hosted validation; no model-execution claim |
| C2 | `docs/superpowers/plans/2026-08-13-workbook-05-c2-process-harness.md` | A synthetic-fixture-proven child-process supervisor, sampler, watchdog, cooldown, retry, and identity-bound resume mechanism |
| C3 | `docs/superpowers/plans/2026-08-13-workbook-05-c3-activation-storage-conformance.md` | Direct K/V request, property, dispatch, fallback, and storage evidence; only proven and reconciled configurations admitted |
| C4 | `docs/superpowers/plans/2026-08-13-workbook-05-c4-quality-evidence.md` | Deterministic P1–P6 gates, critical caps, blinded scoring, matched-baseline comparison, and adjudication records |
| C5 | `docs/superpowers/plans/2026-08-13-workbook-05-c5-standard-baseline-smoke.md` | One pilot, one warm-up, and three measured scalar-cache repetitions with complete retained evidence and independent validation |

## Shared file map

The five plans deliberately share a small set of stable boundaries:

```text
scripts/testing/workbook05/phase3/
  contracts.py                 shared schema loading and validation
  prerequisites.py             accepted Runtime/GenAI verification
  paths.py                     controlled-root and evidence-path policy
  hashing.py                   deterministic SHA-256 helpers
  checkpoint.py                identity-bound atomic Phase 3 state
  bundle_policy.py             common untrusted-bundle restrictions

experiments/granite_turboquant_intel/schemas/workbook05/
  phase3-prerequisite-proof.schema.json
  model-asset-lock.schema.json
  model-conversion-record.schema.json
  process-attempt.schema.json
  resource-summary.schema.json
  activation-proof.schema.json
  storage-proof.schema.json
  conformance-result.schema.json
  quality-result.schema.json
  smoke-summary.schema.json

experiments/granite_turboquant_intel/manifests/templates/workbook05/
  matching `*-template.json` files for every new schema

tests/testing/workbook05/
  package-specific unit, component, integration, workflow, and adversarial tests
```

Each shared module remains narrow. `contracts.py` validates records but does not decide scientific admission. `prerequisites.py` verifies accepted build identities but does not run a model. `checkpoint.py` records completed stages but does not reinterpret a failed result. `bundle_policy.py` validates evidence as untrusted data but never executes anything from the bundle.

## Pull-request boundaries

Each implementation package receives its own branch and pull request:

```text
feature/workbook-05-c1-asset-locking
feature/workbook-05-c2-process-harness
feature/workbook-05-c3-activation-storage
feature/workbook-05-c4-quality-evidence
feature/workbook-05-c5-standard-smoke
```

Every package pull request must include:

- purpose and plain-English explanation;
- exact base and head SHAs;
- files added and changed, grouped by responsibility;
- RED evidence showing the new contract failed before implementation;
- GREEN evidence from the exact final head;
- security and trust-boundary review;
- scientific claims now permitted;
- scientific claims still prohibited;
- retained technical debt and missing-data codes;
- rollback or recovery instructions;
- links to generated artifacts and their SHA-256 values when live evidence exists.

No package pull request may combine implementation of a later package merely because the files are convenient to edit together.

## Verification ladder

Every package uses the same verification order:

```text
1. one focused failing unit or contract test
2. focused GREEN test
3. package unit/component suite
4. complete Workbook 05 repository gate
5. git diff --check
6. independent review of changed files and claim boundary
7. exact-head GitHub Actions verification
8. live evidence collection only after merge, when that package requires it
9. hosted validation of live evidence
10. project-owner digest acceptance before promotion
```

A green workflow name is not sufficient evidence by itself. The retained logs, artifact identity, validation report, and exact commit must agree.

## Scientific promotion rules

- C1 may prove only asset and conversion identity.
- C2 may prove only harness behavior under synthetic and controlled process fixtures.
- C3 may admit a codec configuration only when K and V each have request, property, dispatch, no-fallback, and reconciled-storage evidence.
- C4 may prove only that the scoring procedure is deterministic, traceable, blinded where required, and unable to override objective failures.
- C5 may prove standard-cache Granite execution and harness operation. It does not prove TurboQuant quality, memory benefit, or performance benefit.
- A later compressed capability sweep ranks configurations by reconciled K data + K metadata + V data + V metadata bytes, never by the nominal `u3`, `u4`, or `u8` label alone.

## Textbook and professional basis

- *Systems Engineering: Principles and Practice*, Chapters 3, 6, 12, 16, and 17: lifecycle gates, requirements verification, risk reduction, integration hierarchy, and traceable system evaluation.
- *Engineering Software Products*, Chapters 8–10: reliable programming, automated testing, DevOps, and code-management controls.
- *Code Complete*, Chapters 3, 8, 20–23, 28, and 29: prerequisites, defensive programming, developer testing, systematic debugging, configuration management, and incremental integration.
- *Designing Secure Software*, Chapters 4, 6, 7, 10, 12, and 13: secure defaults, fail-secure behavior, design review, untrusted input, security testing, and secure development practice.
- *The Art of Unit Testing*, Chapters 1, 7–10: trustworthy, maintainable tests and a deliberate multi-level test recipe.
- *Why Programs Fail*, Chapters 4–6 and 8–10: reproducibility, simplification, scientific debugging, observation, origin tracking, and executable assertions.
- *AI Engineering*, Chapters 3, 4, and 9: evaluation methodology, component-level AI-system evaluation, and separate inference performance metrics.
- *Fundamentals of Software Architecture*, Chapters 3, 6, 21, and 22: modular boundaries, fitness functions, explicit architectural decisions, and risk analysis.

## Roadmap completion check

This roadmap is complete only when all five package plans exist, contain no unresolved placeholders, use consistent interfaces, and trace every success criterion in the approved design to at least one concrete task. Implementation begins with C1; no production code is authorised by this documentation-only roadmap.