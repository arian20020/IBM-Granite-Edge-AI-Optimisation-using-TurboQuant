# Workbook 05 C5 Standard-Cache Baseline Smoke Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute one pilot, one warm-up, and three measured IBM Granite 4.1 3B standard-cache repetitions through the accepted C1–C4 foundations before compressed capability testing.

**Architecture:** A frozen control selects the accepted C1 Granite asset, accepted C3 `SCALAR/u8` K/V configuration, CPU, and P2. C2 supervises the C3 probe; C4 seals and deterministically checks output; Python aggregates smoke metrics; a hosted runner validates the text-only artifact.

**Tech Stack:** Python 3.12.10, Windows PowerShell 5.1, C++20 Phase 3 probe, accepted OpenVINO Runtime/GenAI builds, JSON Schema Draft 2020-12, GitHub Actions.

## Global Constraints

- Asset: `MODEL-WB05-GRANITE41-3B-INT4A-G128-R100`, repository `ibm-granite/granite-4.1-3b`.
- Configuration: `RA-SCALAR-U8-SYM`; K and V are independently verified `SCALAR/u8`; device is CPU.
- Prompt: P2 from `GTQ-PROMPTS-v1`; temperature `0.0`, top-p `1.0`, seed `42`, max output `256`, context target `512`.
- Order: pilot, warm-up, measured 1, measured 2, measured 3. All five are excluded from formal campaign statistics; only the three measured rows enter the smoke median.
- One retry is permitted only for C2-classified `InfrastructureInterrupted` after cooldown. Any other failure blocks later rows as `Skipped by frontier`.
- C2 safety thresholds and identity-bound resume remain unchanged.
- Subjective quality and perplexity remain unavailable (`QUALITY_NOT_FORMALLY_SCORED`, `PPL_NOT_IMPLEMENTED`).
- C5 emits smoke records, not the strict formal measured-run manifest.
- C5 proves only standard-cache loading and stable harness operation—not TurboQuant, comparative speed/memory, maximum context, Granite 8B, perplexity, or formal quality preservation.

## Files

```text
experiments/granite_turboquant_intel/schemas/workbook05/{smoke-repetition,smoke-summary}.schema.json
experiments/granite_turboquant_intel/manifests/templates/workbook05/{smoke-repetition,smoke-summary}-template.json
experiments/granite_turboquant_intel/configurations/workbook05/phase3-standard-smoke.json
scripts/testing/workbook05/phase3/{smoke_controls,driver_events,smoke_metrics,smoke_summary,smoke_bundle_validation,smoke_acceptance}.py
scripts/testing/workbook05/Invoke-Workbook05Phase3StandardSmoke.ps1
scripts/testing/workbook05/Accept-Workbook05Phase3StandardSmoke.ps1
.github/workflows/workbook-05-phase3-standard-smoke.yml
tests/testing/workbook05/test_phase3_smoke_*.py
tests/testing/workbook05/Invoke-Phase3StandardSmokeTests.Tests.ps1
docs/testing/workbook05/phase3-standard-smoke-{runbook,closure-template}.md
```

---

### Task 1: Smoke contracts

**Interfaces:** add `smoke-repetition` and `smoke-summary` to `validate_phase3_record`.

- [ ] Write `test_phase3_smoke_contracts.py` first. A passed summary must contain exactly pilot 0, warm-up 0, measured 1–3 in order; every row requires `included_in_formal_statistics: false`.
- [ ] Run `python -m unittest -v tests.testing.workbook05.test_phase3_smoke_contracts`; expect missing schemas/types.
- [ ] Add closed schemas/templates. A repetition requires prerequisite, asset, executable, request, process-attempt, activation, storage, event, raw-output, deterministic-quality, resource, and manifest hashes.
- [ ] Reject a scalar baseline claiming optimisation, a non-CPU actual device, wrong prompt/configuration, missing raw hash, wrong order/count, or fewer than three measured passes.
- [ ] Run the focused and shared contract suites; expect pass.
- [ ] Commit: `test: define Phase 3 smoke contracts`.

### Task 2: Frozen controls

**Interfaces:** `load_smoke_controls(repository_root: Path) -> SmokeControls`; `build_repetition_plan(controls) -> tuple[PlannedRepetition, ...]`.

- [ ] Write `test_phase3_smoke_controls.py` asserting exact asset, model, CPU, P2, K/V `SCALAR/u8`, seed 42, max 256, context 512, and five-role order.
- [ ] Verify RED.
- [ ] Create `phase3-standard-smoke.json` with `smoke_id: GTQ-WB05-PHASE3-STANDARD-SMOKE-v1` and `optimisation_requested: false`.
- [ ] Implement immutable loading with duplicate-key rejection and complete control/prompt/rubric SHA-256 values.
- [ ] Verify GREEN and commit: `feat: freeze Phase 3 smoke controls`.

### Task 3: Accepted prerequisite chain

**Interfaces:** `verify_smoke_prerequisites(...) -> SmokePrerequisiteProof`.

- [ ] Write `test_phase3_smoke_prerequisites.py` rejecting C1 asset drift, model/tokenizer drift, unsafe converted directory, wrong/failed C3 configuration, unproven K/V, fallback, unreconciled storage, probe drift, or Runtime/GenAI decision drift.
- [ ] Verify RED.
- [ ] Extend `phase3/prerequisites.py` and its PowerShell wrapper. Verification must finish before `C:\w5r` workspace creation and must never alter accepted files.
- [ ] Verify C1 and C5 prerequisite suites GREEN; commit: `feat: verify Phase 3 smoke prerequisites`.

### Task 4: Driver timeline and metrics

**Interfaces:** `parse_driver_events(path: Path) -> DriverTimeline`; `derive_smoke_metrics(timeline, genai_metrics, resources) -> SmokeMetrics`.

- [ ] Write `test_phase3_driver_events.py` for exact event order, one first-token event, monotonic timestamps, and request-hash binding.
- [ ] Write `test_phase3_smoke_metrics.py`. Definitions are:

```text
load = pipeline_constructing to pipeline_ready
TTFT = generation_started to first_token
TPOT = first_token to completed / max(output_tokens - 1, 1)
decode tok/s = 1000 / TPOT
total generation = generation_started to completed
```

- [ ] Verify RED.
- [ ] Implement strict parsing. Prompt throughput uses an accepted GenAI metric or remains null; it is never inferred from decode time. Missing values receive explicit codes.
- [ ] Verify GREEN; commit: `feat: derive Phase 3 smoke metrics`.

### Task 5: One-repetition orchestration

**Interfaces:** `Invoke-Workbook05Phase3StandardSmoke.ps1` consumes accepted controls/prerequisites, `Workbook05.Run.psm1`, the C3 probe, and C4 sealing/checking; it produces one validated repetition bundle.

- [ ] Write fixture-mode PowerShell tests for this exact order: verify → new workspace → request/validate/hash → health → supervised process → sampler stop → parse events → seal output → P2 checks → bind scalar activation/storage → classify → record → manifest → cooldown.
- [ ] Verify RED.
- [ ] Implement request materialization using executable path plus argument array. Request CPU, independent `SCALAR/u8` K/V, P2, seed 42, max 256, context 512, and explicit output/event/heartbeat paths.
- [ ] Keep `optimisation_requested` and `optimisation_activated` false; fallback false; subjective scores null.
- [ ] Verify GREEN in fixture mode with no model and no orphan; commit: `feat: orchestrate a Phase 3 smoke repetition`.

### Task 6: Sequence, frontier, retry, and resume

**Interfaces:** checkpoint order is `pilot`, `warm-up`, `measured-1`, `measured-2`, `measured-3`, `summary`, `manifest`.

- [ ] Write PowerShell order/frontier tests and `test_phase3_smoke_resume.py`.
- [ ] Verify RED.
- [ ] Enforce prior-row success and cooldown. A safety/non-transient failure marks later rows `Skipped by frontier` with the blocker ID.
- [ ] Keep logical repetition separate from physical attempts. Retain a failed infrastructure attempt and its one retry; never rewrite history.
- [ ] Resume skips passed work only when every evidence and identity hash still matches; any asset, probe, prompt, configuration, or prerequisite drift rejects resume.
- [ ] Verify GREEN with C2 checkpoint tests; commit: `feat: checkpoint Phase 3 smoke sequence`.

### Task 7: Smoke summary

**Interfaces:** `build_smoke_summary(repetitions, controls) -> dict[str, Any]`.

- [ ] Write `test_phase3_smoke_summary.py` for measured-only medians, three required measured passes, identity consistency, and null propagation.
- [ ] Verify RED.
- [ ] Report each row plus measured median for load, TTFT, prompt throughput, TPOT, decode throughput, total time, peak working set/private bytes, minimum RAM, mean/peak CPU, and separate K/V data/metadata bytes. A median is null unless all three values exist.
- [ ] Freeze claims: standard-cache execution true; Turbo, comparative performance/memory, formal quality, and maximum context false.
- [ ] Verify GREEN; commit: `feat: summarize Phase 3 smoke evidence`.

### Task 8: Untrusted bundle validation

**Interfaces:** `validate_smoke_bundle(bundle_root: Path, repository_root: Path) -> list[BundleIssue]`.

- [ ] Write adversarial fixtures/tests for wrong count/order, pilot/warm-up in median, formal-statistics true, identity drift, non-CPU device, false Turbo claim, fallback, malformed events, raw-hash drift, failed P2 with passed row, fabricated subjective score, wrong median, unsafe path, secret, manifest drift, or binary/model/archive payload.
- [ ] Verify RED.
- [ ] Implement schema, manifest, path, payload, and cross-record checks without executing artifact content.
- [ ] Verify a valid bundle passes and each mutation fails for its intended stable code.
- [ ] Commit: `test: validate Phase 3 smoke bundles`.

### Task 9: Three-boundary workflow

**Interfaces:** jobs `repository-contract`, `collect-standard-smoke`, `validate-standard-smoke`; artifact `workbook-05-phase3-standard-smoke-${{ github.run_id }}-${{ github.run_attempt }}`.

- [ ] Write `test_phase3_smoke_workflow_contract.py` requiring PR repository tests only; manual `main` collection; exact self-hosted labels; same-repository guard; read-only permissions; pinned actions; exact-head checkout; no credentials; full gate first; `cancel-in-progress: false`; same-attempt text-only artifact; hosted validation; no push/download/conversion/broad cleanup/page-file change.
- [ ] Verify RED.
- [ ] Implement workflow boundaries. The laptop trusts accepted local paths only after exact hash verification. Hosted validation always writes a Markdown report.
- [ ] Add C5 tests to `Validate-Workbook05-Phase3.ps1` before `WORKBOOK05_PHASE3_GATE_PASS`.
- [ ] Verify GREEN; commit: `ci: add Phase 3 standard smoke workflow`.

### Task 10: Independent owner acceptance

**Interfaces:** `verify_smoke_acceptance(...) -> AcceptanceDecision`; PowerShell acceptance root `C:\w5r\accepted-phase3-standard-smoke-<run>-<attempt>`.

- [ ] Write `test_phase3_smoke_acceptance.py` rejecting artifact/summary hash drift, wrong run/attempt, manifest failure, failed summary, fewer than three measured passes, overstated claims, unsafe path, or permanent-copy drift.
- [ ] Verify RED.
- [ ] Implement independent ZIP hash, temporary extraction, bundle validation, summary identity, new normal acceptance directory, permanent copy rehash, and `acceptance.json`. Delete only the tool-created temporary extraction.
- [ ] Repeat all non-claims in the acceptance record.
- [ ] Verify GREEN; commit: `feat: accept Phase 3 smoke evidence`.

### Task 11: Runbook and closure

- [ ] Write `phase3-standard-smoke-runbook.md` with exact prerequisites, five-run semantics, safety/retry/resume, outputs, hosted validation, and owner acceptance.
- [ ] Write `phase3-standard-smoke-closure-template.md` requiring repository head, workflow run/attempt, artifact/hash, summary hash, consumed decision hashes, per-row outcomes, medians, missing-data codes, claims, debt, and next gate.
- [ ] State that accepted C5 authorises only the later low-memory-first diagnostic sweep.
- [ ] Commit: `docs: add Phase 3 smoke runbook and closure`.

### Task 12: Verify, merge, execute, and close

- [ ] Run all `test_phase3_smoke_*.py` tests and `Invoke-Phase3StandardSmokeTests.Tests.ps1`.
- [ ] Run `Validate-Workbook05-Phase3.ps1`; expected final marker `WORKBOOK05_PHASE3_GATE_PASS` and no orphan process.
- [ ] Run `git diff --check`; verify no model, IR, executable, run output, or artifact is tracked.
- [ ] Open a detailed implementation PR containing RED/GREEN evidence, metric definitions, exact claim boundary, and final SHA; merge only after exact-head checks and resolved review threads.
- [ ] Dispatch the live workflow from `main`, independently verify the artifact, run owner acceptance with exact hashes, then populate and merge the closure in a separate documentation PR.
- [ ] A failed/blocked live result remains failed/blocked; it is never promoted for schedule reasons.

## C5 Acceptance Gate

C5 passes only when all five logical rows are retained, three measured rows pass, medians use only those rows, every row remains excluded from formal statistics, scalar/Turbo flags are truthful, quality/perplexity remain unavailable, hosted validation passes, owner hashes are accepted, and closure authorises only the diagnostic low-memory-first sweep.

## Textbook and Primary-Source Basis

- *AI Engineering*, Chapters 3, 4, 9: explicit evaluation and separate latency/throughput/resource metrics.
- *Systems Engineering: Principles and Practice*, Chapters 16–17: controlled integration and traceable system evaluation.
- *Code Complete*, Chapters 22, 23, 28, 29: automated testing, diagnostic records, configuration control, and smoke tests.
- *The Art of Unit Testing*, Chapters 7, 10: trustworthy tests and a multi-level test recipe.
- *Why Programs Fail*, Chapters 4, 6, 8, 9: reproduction, hypothesis, observation, and origin tracking.
- *Designing Secure Software*, Chapters 4, 10, 12: fail-secure behavior and untrusted evidence validation.
- IBM's official Granite model card supplies declared metadata; C5 separately records observed execution.
- The exact accepted OpenVINO builds supply the scalar path, pipeline, and available metrics; every observation stays bound to those identities.