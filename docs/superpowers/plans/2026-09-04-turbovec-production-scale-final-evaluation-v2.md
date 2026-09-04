# TurboVec Production-Scale Final Evaluation v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce reproducible 30-, 1,000-, and 10,000-chunk Exact/TQ2/TQ3/TQ4 evidence, attempt 100,000 chunks only when safe, complete upstream and PDF verification, audit Gates A/B, and publish a final research disposition without application integration.

**Architecture:** Extend the existing experiment-only Python package with deterministic frozen datasets, phase-separated embedding artifacts, a counterbalanced benchmark engine, machine-state sampling, and fail-closed evidence validation. Raw run directories remain append-only; a separate evaluator derives statistics, gate audits, reports, and the final decision. The existing .NET PdfPig spike remains isolated from production and gains fixture-driven coverage.

**Tech Stack:** Python 3.12.10, NumPy 2.5.2, OpenVINO 2026.3.1, OpenVINO GenAI 2026.3.0.0, TurboVec 1.0.0, PowerShell 7, .NET 8/MSTest/PdfPig 0.1.15, Rust 1.89 when available.

**Spec:** `docs/superpowers/specs/2026-09-04-turbovec-production-scale-final-evaluation-v2-design.md`

## Global Constraints

- Work only on `test/turbovec-production-scale-final-evaluation-v2`, based on commit `07998fb7766887689a222dd0c8726d821fa33169` and tree `ed68db942aa26c2e66218c10012c914a0e3c361e`.
- Never edit production application, frontend, XAML, inference, model-inspection, hardware-inspection, optimisation, chat, onboarding, settings, or `main` files.
- Use only TurboVec 1.0.0 commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49` and Granite model revision `2ab6fa8ea2d674564defd37171ae19079b864b33` on OpenVINO CPU.
- Exact, TQ2, TQ3, and TQ4 consume byte-identical document/query embeddings and equivalent metadata at every scale.
- Preserve all attempts and failures; never overwrite evidence, cherry-pick favourable repetitions, weaken thresholds, disable security, or terminate unrelated processes.
- A genuine 10,000-chunk run is mandatory to reconsider `DEMONSTRATOR_ONLY`; 100,000 chunks are conditional on the safety gate.

---

### Task 1: Freeze pre-flight and baseline reconciliation

**Files:**
- Create: `experiments/manifests/turbovec/production-scale-v2/preflight.json`
- Create: `experiments/processed-results/EXP-TV-COMP-001/production-scale-v2/baseline-reconciliation.md`
- Test: `scripts/testing/tests/test_turbovec_production_manifest.py`

**Interfaces:**
- Consumes: verified Git/dependency/model identities and diagnostic run `EXP-TV-COMP-001-20260904T025427Z-090`.
- Produces: immutable base identity and a truthful 31/31-versus-39/39 reconciliation.

- [ ] Write a failing test requiring the exact branch base, TurboVec/model hashes, 39 current harness tests, 5 direct PDF tests, and diagnostic reproduction hashes.
- [ ] Run `python -m unittest scripts.testing.tests.test_turbovec_production_manifest -v` and confirm failure because the v2 manifest does not exist.
- [ ] Add the closed JSON manifest and reconciliation document, including the `dotnet test` discovery failure versus direct executable 5/5 result.
- [ ] Run the focused test and all `test_turbovec_*.py` tests; require zero failures.
- [ ] Commit as `evidence(turbovec): preserve production-scale preflight`.

### Task 2: Deterministic genuine corpus, queries, and relevance

**Files:**
- Create: `scripts/testing/turbovec/dataset.py`
- Create: `scripts/testing/turbovec/dataset_schema.py`
- Create: `scripts/testing/generate_turbovec_dataset.py`
- Create: `scripts/testing/tests/test_turbovec_dataset.py`
- Create: `experiments/protocols/turbovec/production-scale-v2.json`
- Create: `experiments/protocols/turbovec/production-scale-v2.schema.json`
- Create: `experiments/protocols/turbovec/corpus-v2-provenance.json`
- Create: `experiments/protocols/turbovec/queries-v2.json`
- Create: `experiments/protocols/turbovec/relevance-v2.json`

**Interfaces:**
- Produces: `FrozenDataset` containing ordered chunks, source/page provenance, development/final queries, relevance grades, and SHA-256 identities for scales 30/1,000/10,000/100,000.

- [ ] Write failing tests for deterministic two-run hashes, genuine unique chunks/vectors, domain/type coverage, independent relevance, split isolation, stable ordering, and exact scale sizes.
- [ ] Confirm RED with `python -m unittest scripts.testing.tests.test_turbovec_dataset -v`.
- [ ] Implement deterministic generation from redistributable/generated source templates with unique factual payloads, semantic distractors, near-duplicates, Unicode, tables, headings, education, healthcare, technical and ordinary prose.
- [ ] Generate and hash the frozen protocol artifacts; reject duplicate content hashes and final-query leakage into development metadata.
- [ ] Run focused and complete TurboVec tests, then generate twice into separate temporary roots and compare every hash.
- [ ] Commit as `test(turbovec): freeze production-scale datasets`.

### Task 3: Phase-separated embedding artifact pipeline

**Files:**
- Create: `scripts/testing/turbovec/artifacts.py`
- Modify: `scripts/testing/turbovec/embedding.py`
- Modify: `scripts/testing/run_turbovec_feasibility.py`
- Create: `scripts/testing/tests/test_turbovec_embedding_artifacts.py`

**Interfaces:**
- Consumes: `FrozenDataset`, locked `OpenVinoGraniteEmbeddingProvider`.
- Produces: immutable little-endian FP32 document/query matrices plus identity JSON with corpus/query/model/tokenizer/runtime hashes.

- [ ] Write failing tests for atomic publication, dtype/dimension/normalisation, one generation per scale, hash verification, batch bounds, interrupted writes, and reuse by all formats.
- [ ] Confirm RED.
- [ ] Implement `generate-embeddings` and `verify-embeddings` commands with staging, fsync/rename publication, manifest closure, and no inclusion of generation time in retrieval metrics.
- [ ] Verify with deterministic provider tests, then perform a one-row live OpenVINO CPU smoke using the locked model.
- [ ] Commit as `test(turbovec): freeze reusable embedding artifacts`.

### Task 4: Fair index, storage, lifecycle, and memory accounting

**Files:**
- Create: `scripts/testing/turbovec/storage.py`
- Create: `scripts/testing/turbovec/lifecycle.py`
- Create: `scripts/testing/turbovec/memory.py`
- Modify: `scripts/testing/turbovec/indexes.py`
- Create: `scripts/testing/tests/test_turbovec_accounting.py`
- Create: `scripts/testing/tests/test_turbovec_lifecycle.py`

**Interfaces:**
- Produces: equivalent component byte ledgers, baseline/peak/incremental memory samples, and lifecycle results for every format.

- [ ] Write failing tests proving equivalent metadata accounting, storage-ratio direction, fixed/variable byte separation, process-versus-system RAM labels, save/reload equality, corruption/incomplete-write rejection, cancellation, owned cleanup, and repeated-run stability.
- [ ] Confirm RED.
- [ ] Implement minimal accounting and lifecycle APIs without changing TurboVec or Exact ranking semantics.
- [ ] Run focused tests and existing index/runner regressions.
- [ ] Commit as `test(turbovec): add fair storage memory lifecycle accounting`.

### Task 5: Counterbalanced repeated benchmark engine

**Files:**
- Create: `scripts/testing/turbovec/schedule.py`
- Create: `scripts/testing/turbovec/benchmark.py`
- Modify: `scripts/testing/turbovec/runner.py`
- Create: `scripts/testing/tests/test_turbovec_schedule.py`
- Create: `scripts/testing/tests/test_turbovec_benchmark.py`

**Interfaces:**
- Consumes: verified embedding artifacts and relevance.
- Produces: five repetition records per scale with rotating Latin-square order, five warmups, 30 warm batches, matched Exact references, cold/warm latency samples, and cleanup state.

- [ ] Write failing tests for deterministic recorded seeds, balanced position counts, matched query order, no warmup contamination, five complete repetitions, full latency arrays, invalidation of contaminated blocks, and no partial-candidate decision input.
- [ ] Confirm RED.
- [ ] Implement the schedule and benchmark state machine; persist after each atomic phase and preserve invalid runs.
- [ ] Run focused tests plus all runner/metrics regressions.
- [ ] Commit as `test(turbovec): counterbalance repeated formal benchmarks`.

### Task 6: Machine readiness and recovery gate

**Files:**
- Create: `scripts/testing/turbovec/machine_state.py`
- Create: `scripts/testing/Capture-TurboVecMachineState.ps1`
- Create: `scripts/testing/tests/test_turbovec_machine_state.py`

**Interfaces:**
- Produces: 60-second readiness/post-run JSONL samples and a decision record with CPU/RAM/power/process/update/sync/disk observations.

- [ ] Write failing tests for 60-second coverage, CPU below 10%, RAM stability, 4 GiB practical and 2 GiB hard floors, AC/power mode capture, top-process capture, timezone/uptime, and fail-closed missing sensors.
- [ ] Confirm RED with synthetic samples.
- [ ] Implement read-only Windows collection using psutil, CIM and available GPU counters; never change power/security/process state.
- [ ] Verify synthetic acceptance/rejection and one live non-formal capture.
- [ ] Commit as `test(turbovec): gate formal machine readiness`.

### Task 7: PDF matrix and Windows long-path investigation

**Files:**
- Modify: `tools/TurboVec.PdfExtractionSpike.Tests/PdfExtractorTests.cs`
- Create: `tools/TurboVec.PdfExtractionSpike.Tests/PdfCampaignFixture.cs`
- Create: `scripts/testing/Invoke-TurboVecPdfCampaign.ps1`
- Create: `docs/testing/turbovec/production-scale-v2/pdf-extraction-report.md`
- Create: `docs/testing/turbovec/production-scale-v2/windows-long-path-investigation.md`

**Interfaces:**
- Produces: TRX and manifest evidence for the required PDF categories and a classified upstream long-path result.

- [ ] Add one failing MSTest per missing PDF category and preserve the first RED TRX.
- [ ] Apply only experiment-owned extractor corrections required by reproduced defects.
- [ ] Execute the generated Microsoft Testing Platform executable directly, because the pinned .NET SDK bridge currently reports zero discovery; preserve both observations.
- [ ] Reproduce the upstream Python long-path case at the pinned source commit and classify OS/path-policy versus candidate defect without weakening Windows security.
- [ ] Commit as `test(turbovec): complete pdf and long-path evidence`.

### Task 8: Upstream Python, repository, Rust and Clippy verification

**Files:**
- Create: `scripts/testing/Invoke-TurboVecUpstreamVerification.ps1`
- Create: `docs/testing/turbovec/production-scale-v2/upstream-verification-report.md`
- Create: `experiments/raw-results/turbovec/production-scale-v2/upstream/manifest.json`

**Interfaces:**
- Produces: reconciled discovered/executed/passed/failed/skipped/blocked/unexecuted counts and raw stdout/stderr/hashes.

- [ ] Add contract tests that reject arithmetic gaps or claims unsupported by raw exit codes.
- [ ] Rerun the repository 111-test baseline and pinned upstream Python suite.
- [ ] Detect Rust 1.89; if absent, install only the user-scoped official 1.89 toolchain, then run locked release tests and Clippy without modifying other toolchains.
- [ ] Preserve the Windows long-path failure separately from unrelated skips and produce the report.
- [ ] Commit as `evidence(turbovec): verify upstream toolchains`.

### Task 9: Evidence schema, orchestration, and independent validator

**Files:**
- Create: `experiments/protocols/turbovec/production-scale-v2-evidence.schema.json`
- Modify: `scripts/testing/Invoke-TurboVecFeasibility.ps1`
- Modify: `scripts/testing/Test-TurboVecEvidence.ps1`
- Create: `scripts/testing/turbovec/validate_campaign.py`
- Create: `scripts/testing/tests/test_turbovec_campaign_evidence.py`

**Interfaces:**
- Produces: append-only formal run trees and independent validation that recomputes hashes, arithmetic, metrics, schedules, statistics, cleanup, and gate inputs.

- [ ] Write failing mutation tests for every required identity, command state, hash, metric, repetition, order, readiness, exclusion, cleanup and privacy field.
- [ ] Confirm RED.
- [ ] Implement closed schemas, phase orchestration and independent recomputation; the orchestrator never creates a successful terminal record after a failed prerequisite.
- [ ] Run mutation tests, all TurboVec tests, PowerShell parser checks and `git diff --check`.
- [ ] Commit as `test(turbovec): validate production-scale evidence`.

### Task 10: Execute formal scale blocks

**Files:**
- Create by harness: `experiments/raw-results/turbovec/production-scale-v2/<run-id>/...`
- Create by evaluator: `experiments/processed-results/EXP-TV-COMP-001/production-scale-v2/<run-id>/...`

**Interfaces:**
- Consumes: frozen artifacts and passing readiness gate.
- Produces: valid five-repetition evidence at 30, 1,000 and 10,000 chunks; conditional 100,000 result or a documented safety block.

- [ ] Confirm AC power, fixed power mode, no update/download/sync/build/benchmark activity, security enabled, and five-minute idle period.
- [ ] Capture at least 60 seconds of readiness evidence and start only when all gates pass.
- [ ] Execute the 30-vector block, perform between-run recovery, validate independently, and preserve every attempt.
- [ ] Repeat for 1,000 and 10,000 genuine chunks using the same protocol.
- [ ] Estimate 100,000 resource demand from observed component accounting; execute only if the 4 GiB practical floor and 2 GiB hard floor remain safe.
- [ ] Re-run any wholly invalidated comparison block; never retain only favourable retries.
- [ ] Commit admissible raw and processed textual evidence as `evidence(turbovec): record production-scale formal campaign`.

### Task 11: Statistics, Gates A/B, and twenty deliverables

**Files:**
- Create under: `docs/testing/turbovec/production-scale-v2/`
- Create under: `experiments/processed-results/EXP-TV-COMP-001/production-scale-v2/final/`

**Interfaces:**
- Produces: final report, manifests/logs/provenance/relevance, per-scale/per-repetition tables, order/storage/memory/PDF/long-path/upstream reports, Gate A/B audits, threats, reproduction guide, decision record, and handoff receipt.

- [ ] Write failing document-contract tests requiring all 20 deliverables and cross-document identity/metric/disposition consistency.
- [ ] Compute per-format median/mean/min/max/dispersion and appropriate 95% confidence intervals from all valid repetitions.
- [ ] Audit each Gate A and Gate B condition independently at 10,000+ chunks; state that generated-answer quality was not evaluated.
- [ ] Perform the threats-to-validity review for load, thermal/power drift, memory/paging, order, storage equivalence, cold/warm mixing, duplication, leakage, relevance, cherry-picking, hidden failures, arithmetic, claims, privacy and security.
- [ ] Produce one exact disposition: `INTEGRATE_CANDIDATE`, `DEMONSTRATOR_ONLY`, `EXCLUDE`, or `BLOCKED`.
- [ ] Run document contracts and evidence validation, then commit as `docs(turbovec): publish final research disposition`.

### Task 12: Final scope, remote, and handoff verification

**Files:**
- Modify only if validation requires: experiment-owned files listed above.

**Interfaces:**
- Produces: clean, pushed branch with equal local/remote/advertised identities and no application integration.

- [ ] Diff the complete branch against `07998fb7`; fail if any production application/frontend/main-scoped file changed.
- [ ] Run all TurboVec Python tests, direct PDF tests, Rust/Python/upstream gates, evidence validators, schema validators, privacy scan and `git diff --check`.
- [ ] Verify every committed evidence file hash/byte count and recompute the final disposition independently.
- [ ] Push without force and verify local tip, remote-tracking tip and `git ls-remote` advertised tip equality.
- [ ] Verify the worktree is clean and emit the handoff receipt containing base/implementation/return commits and trees, identities, machine state, results, gates, limitations, artifact hashes, remote equality and no-integration confirmation.
