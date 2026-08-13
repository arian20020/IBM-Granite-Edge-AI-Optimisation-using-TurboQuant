# Workbook 05 Phase 3 and Measured-Run Foundations Roadmap

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement each package plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the approved Phase 3 design into five independently reviewable packages that establish trustworthy model assets, process supervision, codec conformance, quality evidence, and a standard-cache smoke baseline before any compressed capability sweep.

**Architecture:** Work follows a layered evidence pipeline. Each package creates a narrow, schema-validated result that becomes an immutable prerequisite for the next package. A later result cannot repair an earlier missing proof, and no package may weaken the strict formal measured-run manifest.

**Tech Stack:** Python 3.12.10, Windows PowerShell 5.1, C++20, CMake/MSVC, OpenVINO Runtime `b9a1f201c109e0bed74763934f79483cf6c4cbf4`, OpenVINO GenAI `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`, JSON Schema Draft 2020-12, GitHub Actions, IBM Granite 4.1.

## Approved design

The controlling specification is:

```text
docs/superpowers/specs/2026-08-13-workbook-05-phase3-measured-run-foundations-design.md
```

The project owner approved it on 13 August 2026. Implementation remains unstarted. This roadmap decomposes the specification because its five subsystems can fail, be reviewed, and be accepted independently.

## Global Constraints

- Campaign is exactly `GTQ-WB05-MF-v1`; route is exactly `route-a-merged-openvino`.
- Runtime install `C:\w5a\phase2-31391119557-4\i-ov` and decision `C:\w5a\accepted-route-a-runtime-31656417607-1\decision.json` are read-only.
- Runtime decision SHA-256 is `5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38`; source commit is `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- GenAI install `C:\w5a\phase2-31661571860-1\i-genai` and decision `C:\w5a\accepted-route-a-genai-31661571860-1\decision.json` are read-only.
- GenAI decision SHA-256 is `0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79`; source commit is `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`.
- `C:\w5a\` contains accepted prerequisites, `C:\w5m\` immutable model assets, `C:\w5c\` probe/trace workspaces, and `C:\w5r\` durable run workspaces/checkpoints.
- Controlled roots and children must be normal local directories, never files, UNC/device paths, links, junctions, mount points, or other reparse points. Existing run directories are never silently cleaned, overwritten, or reused.
- Available RAM below `1610612736` bytes for ten seconds, Windows commit above `90` percent for ten seconds, or heartbeat age above `900` seconds terminates the process tree.
- No workflow alters the page file, overclocks hardware, disables memory protection, or changes firmware.
- Prompt set remains `GTQ-PROMPTS-v1`; rubric remains `GTQ-QUALITY-RUBRIC-v1`; score remains 0–10; prompts remain P1–P6.
- `measured-run-manifest.schema.json` remains strict and is not relaxed for spikes, conformance, smoke, blocked records, or missing evidence.
- Live laptop execution is manual from `main` only and requires labels `self-hosted`, `Windows`, `X64`, `workbook05`, and `intel-target`.
- Workflows use `contents: read` and `actions: read`, immutable action SHAs, `persist-credentials: false`, and `cancel-in-progress: false`.
- Pull requests run repository/synthetic tests only. They do not download or execute a model or send unreviewed code to the Intel laptop.
- Self-hosted jobs never push repository changes.
- Uploaded evidence is text/data only. Executables, DLLs, libraries, object files, model weights, tokenizers, and source/model archives are forbidden.
- Route B QJL/PolarQuant remains separately blocked. Route A must reject or explicitly classify those labels and never translate them into a Route A codec.

## Binding interface resolutions from plan self-review

These decisions remove the remaining implementation ambiguity and override any broader wording in an individual package plan:

1. **C++ JSON parsing:** C3 uses `ov::genai::JsonContainer::from_json_string` from the exact accepted GenAI API. It does not add or fetch another JSON dependency and does not implement an ad-hoc parser.
2. **Bundle-policy reuse:** package validators reuse the stable issue/path/hash/payload rules in `scripts/testing/workbook05/bundle_validation.py` and package-specific cross-record checks. There is no new `bundle_policy.py` module.
3. **Schema ownership:** `scripts/testing/workbook05/phase3/contracts.py` is the single registry for all new Phase 3 record types; packages extend that registry rather than creating parallel schema loaders.
4. **Checkpoint ownership:** `scripts/testing/workbook05/phase3/checkpoint.py` owns the richer identity-bound Phase 3 checkpoint. The existing Workbook 05 checkpoint module is not weakened or silently replaced.
5. **Formal-run boundary:** C5 emits `smoke-repetition` and `smoke-summary` records only. It never manufactures a formally complete measured-run manifest with unavailable quality or perplexity fields.
6. **Diagnostic truthfulness:** a small model can be `HarnessOnly`; it becomes `PathEquivalent` only with executable evidence that it reaches the same CPU stateful SDPA/KV-cache path.
7. **Performance eligibility:** trace-only Runtime results may prove property acceptance, branch dispatch, fallback absence, and storage facts, but are always ineligible for formal performance or quality claims.

## Package dependency order

```text
C1 asset locking
  -> C2 process harness
    -> C3 activation and storage conformance
      -> C4 quality evidence
        -> C5 standard-cache smoke
          -> later low-memory-first capability sweep
```

A package begins only after the previous package's exact PR head and required acceptance decision are known. Parallel work is limited to repository-only documentation or tests that consume no unaccepted interface.

## Plan files and results

| Package | Plan | Independently testable result |
|---|---|---|
| C1 | `docs/superpowers/plans/2026-08-13-workbook-05-c1-asset-locking.md` | Immutable diagnostic and Granite 4.1 3B model/tokenizer/conversion identities; no model-execution claim |
| C2 | `docs/superpowers/plans/2026-08-13-workbook-05-c2-process-harness.md` | Synthetic-fixture-proven supervisor, sampler, watchdog, cooldown, retry, and identity-bound resume |
| C3 | `docs/superpowers/plans/2026-08-13-workbook-05-c3-activation-storage-conformance.md` | Independent K/V request, property, dispatch, fallback, and reconciled storage evidence |
| C4 | `docs/superpowers/plans/2026-08-13-workbook-05-c4-quality-evidence.md` | P1–P6 deterministic gates, caps, blinding, paired comparison, and adjudication records |
| C5 | `docs/superpowers/plans/2026-08-13-workbook-05-c5-standard-baseline-smoke.md` | One pilot, one warm-up, and three measured scalar-cache repetitions with independent validation |

## Shared file map

```text
scripts/testing/workbook05/phase3/
  contracts.py       shared Phase 3 schema registry
  prerequisites.py   accepted Runtime/GenAI and later prerequisite verification
  paths.py           controlled-root/evidence-path policy
  hashing.py         deterministic SHA-256 helpers
  checkpoint.py      identity-bound atomic Phase 3 state

scripts/testing/workbook05/
  bundle_validation.py   existing stable untrusted-bundle primitives reused by package validators

experiments/granite_turboquant_intel/schemas/workbook05/
  phase3-prerequisite-proof.schema.json
  model-asset-lock.schema.json
  model-conversion-record.schema.json
  process-attempt.schema.json
  resource-summary.schema.json
  phase3-checkpoint.schema.json
  probe-run-request.schema.json
  activation-proof.schema.json
  storage-proof.schema.json
  conformance-result.schema.json
  deterministic-check-result.schema.json
  quality-result.schema.json
  adjudication-record.schema.json
  smoke-repetition.schema.json
  smoke-summary.schema.json
```

Each module has one responsibility. Contracts validate shape but do not decide scientific admission. Prerequisites verify identity but do not run a model. Checkpoints record evidence but do not reinterpret failed results. Validators treat uploaded artifacts only as data.

## Pull-request boundaries

```text
feature/workbook-05-c1-asset-locking
feature/workbook-05-c2-process-harness
feature/workbook-05-c3-activation-storage
feature/workbook-05-c4-quality-evidence
feature/workbook-05-c5-standard-smoke
```

Every implementation PR must explain purpose, exact base/head SHAs, changed files by responsibility, observed RED, exact-head GREEN, security boundaries, newly permitted claims, still-prohibited claims, retained debt/missing-data codes, recovery, and artifact/hash identities when live evidence exists. No PR combines a later package merely because adjacent files are convenient to edit.

## Verification ladder

```text
1. focused failing test
2. focused GREEN test
3. package unit/component suite
4. complete Workbook 05 repository gate
5. git diff --check
6. independent changed-file and claim-boundary review
7. exact-head GitHub Actions verification
8. live collection only after merge when required
9. clean hosted validation
10. project-owner digest acceptance before promotion
```

A green workflow name alone is insufficient. Logs, artifact identity, validation report, and exact commit must agree.

## Scientific promotion rules

- C1 proves only asset/conversion identity.
- C2 proves only harness behavior with synthetic/controlled processes.
- C3 admits a configuration only when K and V each have request, property, dispatch, no-fallback, and reconciled-storage evidence.
- C4 proves only that evaluation is deterministic, traceable, blinded where required, and unable to override objective failure.
- C5 may prove standard-cache Granite execution and harness stability, not TurboQuant quality, memory benefit, or speed benefit.
- The later capability sweep ranks only reconciled candidates by K data + K metadata + V data + V metadata bytes, never nominal bit width alone.

## Self-review result

- Placeholder scan: no `TBD`, `TODO`, `implement later`, `fill in`, or “similar to Task N” instructions remain.
- Coverage: every approved-design success criterion maps to C1–C5 and an explicit acceptance gate.
- Type/interface consistency: shared function/module ownership is fixed above; later plans consume names produced by earlier plans.
- Scope: each package can be reviewed or rejected independently and produces a working, testable boundary.
- Implementation status: no production code, model download, model conversion, trace build, or model run is authorised by this documentation branch.

## Textbook and professional basis

- *Systems Engineering: Principles and Practice*, Chapters 3, 6, 12, 16, 17: lifecycle gates, verification, risk reduction, integration, and traceable evaluation.
- *Engineering Software Products*, Chapters 8–10: reliable programming, testing automation, DevOps, and code management.
- *Code Complete*, Chapters 3, 8, 20–23, 28, 29: prerequisites, defensive programming, developer testing, debugging, configuration management, and incremental integration.
- *Designing Secure Software*, Chapters 4, 6, 7, 10, 12, 13: secure defaults, fail-secure behavior, design review, untrusted input, security tests, and secure development.
- *The Art of Unit Testing*, Chapters 1, 7–10: trustworthy, maintainable tests and multi-level test recipes.
- *Why Programs Fail*, Chapters 4–6, 8–10: reproducibility, scientific debugging, observation, origin tracking, and executable expectations.
- *AI Engineering*, Chapters 3, 4, 9: evaluation methodology, component-level AI evaluation, and separate inference metrics.
- *Fundamentals of Software Architecture*, Chapters 3, 6, 21, 22: modular boundaries, fitness functions, explicit decisions, and risk analysis.

Implementation begins with C1 only after review of this documentation branch.