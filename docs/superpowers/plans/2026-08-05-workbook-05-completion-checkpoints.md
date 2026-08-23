# Workbook 05 Completion Checkpoint Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use `superpowers:subagent-driven-development` task by task. Do not begin a later task until the preceding checkpoint has been reviewed and accepted.

**Goal:** Complete Workbook 05 revision 1.4 and the `GTQ-WB05-MF-v1` two-route memory-frontier campaign with controlled source/build provenance, safe execution, complete performance and quality evidence, truthful Route A/Route B claims, and a fully closed workbook.

**Architecture:** The work is divided into six checkpointed stages: finish Phase 1 source admission, perform documented builds, create the measured-run harness, execute conformance and capability tests, execute Granite frontier/formal tests, and close the evidence/workbook. Route A is the merged OpenVINO control route. Route B remains experimental and may proceed to model tests only after admission, build, activation, packed-storage, and no-fallback proof.

**Current verified starting point:** PR `#46`, branch `testing/workbook-05-source-admission`, head `0c78e810281175915bd4e057db20fe5312ddc16c`. Tasks 1–7 of `2026-08-05-workbook-05-source-admission.md` are implemented. Workbook 05 validation run `31017397577` and WinUI build/test run `31017397378` both completed successfully on this head.

## Global execution rules

- Execute one task at a time.
- Every behaviour change follows red, green, refactor.
- Every task ends in a focused commit and a fresh review.
- A task is not complete merely because code exists; its checkpoint evidence must pass.
- Preserve every existing Workbook 04 and Workbook 05 test ID.
- Use `docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv` as the cross-route execution authority.
- Route A and Route B must remain separately labelled in every record and conclusion.
- Route B model tests are forbidden until its source/build/activation gate passes.
- A blocked test remains visible with its blocker and evidence; it is never silently removed.
- A successful measured inference run is invalid without raw output, activation proof, fallback proof, separate K/V allocation, performance metrics, resource metrics, deterministic quality checks, five rubric dimensions, caps, paired baseline, and quality delta.
- Weight quantisation and KV-cache compression are separate comparison axes.
- Formal quality uses `GTQ-PROMPTS-v1`, `GTQ-QUALITY-RUBRIC-v1`, and the project material-degradation rule.
- Pilot and warm-up runs remain visible but are excluded from formal statistics.
- Large source trees, models, build outputs, executables, wheels, and archives stay outside the repository.
- No task pushes directly to `main` or auto-merges a pull request.

---

# Stage A — Finish Phase 1 source admission

## Task R1: Calculate truthful route and Phase 1 decisions

**Purpose:** Implement the decision engine that combines measurement controls, source provenance, capability findings, Route B CMake discovery, and Route A configure evidence.

**Primary files:**

- Create `scripts/testing/workbook05/source_admission_phase.py`.
- Create `tests/testing/workbook05/test_source_admission_phase.py`.
- Modify source-admission summary/template validation only where the new tests require it.

**Required work:**

1. Write failing tests for Route A admitted/Route B blocked, Route A configure failure, invalid measurement controls, unsafe evidence paths, and open blockers.
2. Require non-empty reasons and safe relative evidence paths for every claimed proof.
3. Permit Route A to be `Admitted` while Route B is `Blocked`.
4. Permit the Phase 1 checkpoint to pass when the approved campaign can continue through Route A and Route B is truthfully blocked.
5. Produce summary JSON, summary Markdown, checkpoint candidate, controlled-record references, and hash-manifest inputs.

**Checkpoint A1:** Focused tests, the complete Workbook 05 Python suite, PowerShell suites, and WinUI build/tests are green. Review confirms no source-presence finding is promoted to executable support.

## Task R2: Build the read-only Windows source-admission orchestrator

**Purpose:** Connect the already tested Phase 1 components in the exact approved order without introducing build or inference commands.

**Primary files:**

- Create `scripts/testing/workbook05/Invoke-Workbook05SourceAdmission.ps1`.
- Create static/orchestration tests under `tests/testing/workbook05/`.

**Required work:**

1. Test first for prohibited commands: `cmake --build`, install, package, model URLs, `git reset --hard`, `git clean`, recursive removal of `C:\wb05`, `Invoke-Expression`, and Route B configure execution.
2. Run in this order: preflight validation; workspace validation; measurement-control capture; Route A Runtime verification; Route A GenAI verification; Route B verification; document capture; capability inspection; Route B CMake audit; conditional Route A configure probe; route decisions; hashes.
3. Treat source blockers as scientific outcomes rather than infrastructure crashes.
4. Return non-zero only for integrity/orchestration failures.
5. Add beginner-readable comments to every logical block.

**Checkpoint A2:** Static forbidden-command tests pass; simulated passed/blocked/error paths produce truthful complete bundles; no build or model execution is possible from the script.

## Task R3: Validate source-admission artifacts as untrusted data

**Purpose:** Create an independent hosted validator that never executes captured external commands or files.

**Primary files:**

- Create `scripts/testing/workbook05/source_admission_bundle_validation.py`.
- Create `tests/testing/workbook05/test_source_admission_bundle.py`.

**Required work:**

1. Test valid and adversarial bundles first.
2. Reject hash changes, false admission, wrong origin/commit, incomplete submodules, Route B admission with a confirmed blocker, Route A admission without a valid configure cache, invalid measurement-control hashes, permissive measured-run schemas, unsafe paths, secrets, executables, libraries, archives, wheels, and model files.
3. Always write a Markdown validation report.
4. Return zero only when no issue exists.

**Checkpoint A3:** All adversarial fixtures fail for the expected reason; a valid fixture passes; the validator contains no code path that executes evidence payloads.

## Task R4: Add the dedicated two-job source-admission workflow

**Purpose:** Replace reliance on the earlier preflight workflow with a Phase 1 workflow whose artifact name, timeout, inputs, and hosted validation are specific to source admission.

**Primary files:**

- Create `.github/workflows/workbook-05-source-admission.yml`.
- Create `tests/testing/workbook05/test_source_workflow_contract.py`.

**Required work:**

1. Require `contents: read` and `actions: read` only.
2. Restrict self-hosted execution to the same-repository branch `testing/workbook-05-source-admission`, with manual execution limited to `main` or that branch.
3. Require exact Intel labels, pinned Actions, `persist-credentials: false`, and `cancel-in-progress: false`.
4. Use a 180-minute Intel timeout.
5. Run all Workbook 05 Python and PowerShell tests before evidence collection.
6. Upload `workbook-05-source-admission-${{ github.run_id }}-${{ github.run_attempt }}` even when collection reports a scientific blocker.
7. On `windows-latest`, download the exact same-attempt artifact, rerun repository tests, and invoke the untrusted validator.

**Checkpoint A4:** Workflow-contract tests pass, self-hosted and hosted jobs are green on the same head, and the artifact digest is recorded.

## Task R5: Add the repository-level Phase 1 gate

**Purpose:** Give reviewers and future automation one deterministic command that validates all Phase 1 scaffolding.

**Primary files:**

- Create `scripts/testing/Validate-Workbook05-SourceAdmission.ps1`.
- Modify `.github/workflows/workbook-05-source-admission.yml` to call it.

**Required work:**

1. Run all Workbook 05 Python tests.
2. Run all Workbook 05 PowerShell tests.
3. Run controlled-workspace, OpenVINO structure, measurement-control, schema/template, and workflow-contract validation.
4. Run `git diff --check`.
5. Print the controlled PASS lines only after every check succeeds.

**Checkpoint A5:** The single gate passes locally/on the Intel runner and the workflow cannot bypass it.

## Task R6: Execute and close live Phase 1

**Purpose:** Produce real source-admission evidence, review it, and finish PR `#46`.

**Required work:**

1. Run the final static gate and record head SHA and test counts.
2. Run the dedicated source-admission workflow while the Intel laptop is powered, plugged in, online, awake, and running the Actions service.
3. Inspect exact origins, commits, recursive submodules, captured documents, capability findings, Route B omission evidence, Route A CMake cache, measurement hashes, decisions, checkpoint, and artifact digest.
4. Confirm hosted validation used the exact artifact attempt.
5. Run WinUI build/tests on the same head.
6. Update PR `#46` with implementation details, source outcomes, workflow/artifact identifiers, defects/fixes, explicit non-claims, and the next-stage boundary.
7. Review changed files, permissions, unresolved threads, secrets, payload suffixes, and false-support language.
8. Mark ready for review only when all evidence is green; merge only after user review.

**Checkpoint A — Phase 1 complete:** PR `#46` is merged, the live artifact is independently valid, and Route A/Route B each have a truthful source-admission status.

---

# Stage B — Decide and build the admitted route(s)

## Task R7: Review the live Route B disposition

**Purpose:** Decide what Route B can scientifically do next.

**Required work:**

1. Classify Route B as `Admitted`, `Blocked — fixable source/build exposure`, or `Blocked — implementation/selectability absent`.
2. If admitted, include it in the build plan.
3. If blocked by a bounded defect such as `RB-SRC-001`, write a separate reviewed repair specification and PR before changing the fork-derived source.
4. If implementation/selectability is absent, close the affected QJL/Polar rows as blocked with evidence unless a larger implementation work package is explicitly approved.
5. Never label source presence as successful QJL/Polar execution.

**Checkpoint B0:** The user approves either the admitted-build route, a bounded Route B repair package, or the evidence-backed blocked outcome.

## Task R8: Write and approve the Phase 2 documented-build plan

**Purpose:** Turn the live Phase 1 locks into exact build steps before compilation.

**Required outputs:**

- A versioned build-stage design/specification.
- A detailed implementation plan with exact Runtime/GenAI commits, commands, working directories, environment variables, dependency paths, timeouts, artifact rules, and deviation-record format.
- Separate Route A and Route B build directories and evidence roots.

**Checkpoint B1:** The plan is self-reviewed for complete README coverage, placeholders, type consistency, security boundaries, and route separation, then approved before code or builds begin.

## Task R9: Implement build evidence contracts and workflow controls

**Purpose:** Make incomplete or undocumented builds unable to pass.

**Required work:**

1. Add schemas/templates for command manifests, deviations, dependencies, build resources, binary hashes, Runtime/GenAI compatibility, and final build decisions.
2. Add a read-only self-hosted build workflow and independent hosted artifact validator.
3. Capture every documented command separately with working directory, timestamps, exit code, stdout, stderr, environment, elapsed time, and peak build memory.
4. Reject unrecorded command changes and unapproved deviations.

**Checkpoint B2:** Synthetic build bundles pass/fail correctly, all workflow security tests pass, and no model execution is yet possible.

## Task R10: Build and verify Route A Runtime and GenAI

**Purpose:** Produce one pinned merged OpenVINO Runtime/GenAI pair that can run a standard CPU baseline and expose the expected TurboQuant controls.

**Required work:**

1. Follow the pinned OpenVINO Windows build document in order.
2. Build Release x64 Runtime with the approved CPU-only options.
3. Build compatible OpenVINO GenAI in the same controlled source environment.
4. Record all warnings, dependencies, TBB/runtime paths, outputs, hashes, elapsed time, and peak build memory.
5. Run a standard-cache diagnostic baseline.
6. Verify property visibility and the merged-route claim boundary.
7. Record every compatibility attempt and the reason it was retained or rejected.

**Checkpoint B3:** Route A Runtime and GenAI build successfully, the standard baseline completes, binaries are hashed, and the requested controls are visible without claiming codec activation yet.

## Task R11: Build Route B or close it as blocked

**Purpose:** Produce executable experimental evidence or a final transparent blocker.

**Required work when admitted/repaired:**

1. Follow the pinned Route B build instructions and approved deviations.
2. Build the experimental Runtime and compatible GenAI/test interface.
3. Build and discover the intended codec tests.
4. Verify the selectable QJL/Polar properties or explicit runnable interfaces.
5. Preserve binary hashes and build/test evidence.

**Required work when blocked:**

1. Populate every dependent execution-index row with `Blocked` or `Not applicable` as appropriate.
2. Record the exact blocker, affected codecs/tests, root-cause status, and the separate work needed to continue.
3. Continue Route A independently.

**Checkpoint B4:** Route B is either executable with evidence or formally blocked; no ambiguous middle state remains.

---

# Stage C — Freeze assets and build the measured-run harness

## Task R12: Freeze diagnostic and Granite model assets

**Purpose:** Prevent model/tokenizer changes from contaminating codec comparisons.

**Required work:**

1. Select the approved diagnostic IR model.
2. Select the approved IBM Granite 4.1 3B and 8B variants compatible with the admitted runtime(s).
3. Record repository, revision, filenames, weight precision, licences, tokenizer files, model/tokenizer SHA-256 values, declared context limit, and local paths.
4. Keep model files outside Git and upload no model binaries in evidence.
5. Create new configuration IDs whenever model or tokenizer controls change.

**Checkpoint C1:** Every future run resolves to immutable model/tokenizer records; a hash mismatch blocks execution.

## Task R13: Implement process execution, resource sampling, watchdog, cooldown, and resume

**Purpose:** Run model tests safely and reproducibly on the 15.7 GiB laptop.

**Required work:**

1. Execute inference as a child process with separate stdout/stderr and heartbeat tracking.
2. Sample process-tree working set/private bytes, available RAM, Windows commit usage, CPU utilisation, and GPU memory where applicable.
3. Terminate when available RAM remains below 1.5 GiB for 10 seconds, Windows commit usage remains above 90% for 10 seconds, or heartbeat/stage timeout expires.
4. Retry one transient failure after cleanup and cooldown.
5. Cool down until CPU remains below 10% and RAM returns to within 10% of the pre-run value for 60 seconds.
6. Write durable checkpoints and resume only after verifying campaign/source/model hashes.

**Checkpoint C2:** Synthetic child-process tests prove normal completion, timeout, memory stop, process-tree termination, cooldown, retry, and resume behaviour.

## Task R14: Implement codec activation, fallback, and packed-storage proof

**Purpose:** Ensure a requested codec result reflects what actually executed.

**Required work:**

1. Record requested and verified K/V precision and codec independently.
2. Capture dispatch/activation evidence.
3. Detect dead code, unsupported properties, silent scalar/Turbo substitution, or device fallback.
4. Measure separate K and V record bytes and full cache allocation.
5. Reconcile measured storage with the pinned implementation and model dimensions.
6. Block formal comparison while an unexplained storage discrepancy remains.

**Checkpoint C3:** Controlled positive, negative, and fallback fixtures prove that a compressed run cannot pass without activation/no-fallback/storage evidence.

## Task R15: Implement deterministic quality and perplexity evaluation

**Purpose:** Answer whether compression materially worsens Granite output quality.

**Required work:**

1. Execute P1–P6 with frozen sampling controls.
2. Preserve exact raw outputs and SHA-256 values.
3. Run deterministic checks before subjective scoring.
4. Apply critical caps without allowing a model judge to override objective failure.
5. Score all five rubric dimensions with configuration labels hidden.
6. Evaluate both pairwise presentation orders.
7. Record evaluator identity/version, disagreements, ranking reversals, and adjudication paths.
8. Calculate per-prompt, per-dimension, overall, matched-baseline delta, and material-degradation result.
9. Validate and run perplexity only when the pinned route provides a trustworthy matched procedure.

**Checkpoint C4:** Known-good, deterministic-failure, cap, judge-disagreement, and ranking-reversal fixtures produce the expected results; no average can hide a failed prompt.

## Task R16: Run a standard-baseline harness smoke test

**Purpose:** Prove the complete measured-run pipeline before codec experiments.

**Required work:**

1. Run the diagnostic model with codecs disabled.
2. Run one excluded pilot, one excluded warm-up, and at least three measured standard-cache repetitions.
3. Produce complete manifests, raw output, resource samples, performance metrics, quality records, evidence paths, and hashes.
4. Validate the bundle independently and ingest only validated evidence.

**Checkpoint C — harness ready:** A standard baseline passes end to end with no missing mandatory metric or quality field.

---

# Stage D — Prove codec correctness and execution capability

## Task R17: Execute algorithm-level conformance

**Purpose:** Resolve Workbook 05 `OVT-A01` through `OVT-A12` for every admitted experimental codec and the corresponding Route A control tests in Workbook 04.

**Required work:**

1. Round trip, packing/unpacking, norm, record size, zero/near-zero/large finite values, NaN/Inf policy, deterministic seed/rotation/projection/codebook behaviour, independent K/V dispatch, activation/fallback, allocation formula, repository tests, and bounded quality/perplexity smoke.
2. Record expected and measured bytes for each codec.
3. Block only dependent configurations when a codec fails.
4. Correct workbook storage expectations only from executable evidence.

**Checkpoint D1:** Every admitted codec has a conformance decision and verified storage; failed codecs have explicit dependent-row blockers.

## Task R18: Lock the memory execution order and run diagnostic K/V sweeps

**Purpose:** Establish which ordered K/V combinations genuinely execute and their memory rank.

**Required work:**

1. Recalculate rank from measured K bytes plus measured V bytes.
2. Apply tie-breakers: symmetric first, lower K bytes, lower V bytes, stable codec name.
3. Run Route A official ordered pairs from Workbook 04.
4. Run Workbook 05 `OVT-S01` through `OVT-S36` only for admitted codecs; mark dependent combinations blocked otherwise.
5. Require verified dispatch, normal completion, output integrity, and no unexplained fallback.
6. Update the execution index with verified bytes, memory rank, status, run ID, and evidence path.

**Checkpoint D — capability matrix locked:** Every pair is `Passed`, `Failed`, `Blocked`, or `Not applicable`; no pair remains silently unresolved.

---

# Stage E — Discover hardware frontiers and collect formal results

## Task R19: Execute the Granite 3B symmetric feasibility frontier

**Purpose:** Find the safe context boundary for each admitted symmetric configuration in true low-memory-to-high-memory order.

**Required work:**

1. Start each configuration at 512 tokens.
2. Continue through 1,024, 2,048, 4,096, 8,192, 16,384, then double while supported and stable.
3. Use the P5-derived end-marker retrieval fixture at every point.
4. Verify codec activation, fallback, output integrity, exact marker retrieval, resource safety, cleanup, and stable exit.
5. Retry the same frozen point once after cooldown when the first failure may be transient.
6. After a repeated failure, declare the preceding context the provisional frontier and mark higher points `Skipped by frontier`.

**Checkpoint E1:** Each symmetric configuration has a last stable context, first repeated failure or declared limit, complete resource curves, and no unexplained storage discrepancy.

## Task R20: Execute formal Granite 3B performance, memory, quality, and perplexity evaluation

**Purpose:** Produce the core scientific answer about compression trade-offs.

**Required work:**

1. Select one common context supported by all compared configurations.
2. Also test the standard control, lowest-memory stable candidate, strongest-quality candidate, and decision-critical configurations at their last stable context and immediately below a failure boundary.
3. Run one pilot, one excluded warm-up, and at least three measured repetitions per frozen configuration.
4. Record load time, TTFT and its boundaries, prompt tok/s, TPOT, decode tok/s, total generation time, token counts, peak working set/private bytes, RAM before/minimum/after, K/V/total allocation, CPU mean/peak, device/placement, exit state, and stability.
5. Run P1–P6, five rubric dimensions, deterministic failures, caps, paired deltas, adjudication, material-degradation classification, and perplexity/delta where valid.
6. Summarise formal metrics with median and range while preserving every repetition.

**Checkpoint E2 — core quality answer:** The standard control and every admitted symmetric codec have matched, independently validated memory/speed/quality results or an explicit blocker.

## Task R21: Execute asymmetric and cross-family tests

**Purpose:** Resolve key-only, value-only, scalar/Turbo, QJL/Polar, and selected cross-family behaviour.

**Workbook coverage:** Workbook 05 `OVT-08`, `OVT-09`, and `OVT-14` through `OVT-25`, plus the corresponding Workbook 04 Route A controls.

**Required work:**

1. Preserve the measured-memory execution order.
2. Prove which K codec controls query rotation/projection and which V codec controls output-domain correction.
3. Require output correctness, activation, no fallback, and reconciled K/V storage.
4. Mark combinations using a blocked codec as blocked rather than deleting them.

**Checkpoint E3:** Every required asymmetric/cross-family row has a truthful decision and evidence.

## Task R22: Execute the Granite 8B safety and feasibility gate

**Purpose:** Determine whether the laptop can use Granite 8B with the standard control and the most decision-relevant compressed candidates.

**Workbook coverage:** `OVT-05` through `OVT-07`, `OVT-32`, and `OVT-33`, with Route A equivalents.

**Required work:**

1. Start again at 512 tokens with the lowest-memory quality-valid candidate.
2. Promote only the standard control and candidates relevant to final memory, quality, or speed conclusions.
3. Apply the same watchdog, retry, frontier, activation, output-integrity, metric, and quality rules.
4. Do not infer full 8B quality from a discovery-only exact-marker pass.

**Checkpoint E4:** Granite 8B is classified as feasible, bounded, or blocked for each selected configuration with evidence.

## Task R23: Execute ablations, repeatability, regression, and unsupported paths

**Workbook coverage:** `OVT-26` through `OVT-28`, `OVT-31`, and `OVT-34` through `OVT-36`.

**Required work:**

1. Test norm correction OFF/ON where exposed.
2. Test fused quantisation OFF/ON where exposed.
3. Test only documented QJL/Polar controls actually present in the pinned source.
4. Run restart/repeatability/corruption checks.
5. Run GPU request, PagedAttention, prefill compression, non-SDPA, unsupported head-dimension, and other limitation gates.
6. Run regression tests after any codec/runtime repair.

**Checkpoint E — experimental execution complete:** Every executable Workbook 04/05 row is resolved and every non-executable row has a defensible blocked/not-applicable reason.

---

# Stage F — Validate, ingest, complete, and close the workbook

## Task R24: Validate and ingest every completed stage

**Purpose:** Ensure only independently validated evidence becomes formal repository evidence.

**Required work:**

1. Validate required files, schemas, hashes, run-to-metric-to-output references, activation/fallback fields, failure records, and absence of secrets/binaries/models.
2. Commit one validated batch per stage to `results/workbook-05-GTQ-WB05-MF-v1` or the approved equivalent.
3. Create/update one draft results PR containing stage status, counts, model/source locks, current frontiers, metrics, failures, limitations, evidence paths, and remaining work.
4. Preserve failed or invalid artifacts in Actions without committing them as formal evidence.

**Checkpoint F1:** Every formal result in the repository can be traced to an independently validated artifact and exact run manifest.

## Task R25: Populate Workbook 05 and the cross-route execution index

**Purpose:** Close every controlled field rather than leaving blank cells.

**Required work:**

1. Complete repository/environment, target laptop, codec inventory, build/setup, conformance, 36-pair sweep, primary tests, compatibility attempts, activation/fallback, performance/memory, quality/perplexity, cross-family correctness, failure/limitation, and final decision sections.
2. Populate all 119 execution-index records with final status, verified bytes where applicable, memory rank, frontier status, skip reason, run ID, and evidence path.
3. Update the append-only revision register, decision log, controlled workbook manifest, generated DOCX, and hashes.
4. Preserve historical results as legacy evidence rather than copying them into active result cells.

**Checkpoint F2:** Schema/structural validation proves no required controlled field or execution-index record is unresolved.

## Task R26: Produce final bounded conclusions and complete the audit

**Purpose:** Turn the evidence into a truthful final workbook decision.

**Required conclusions:**

1. Separate leaders for memory reduction, quality preservation, decode speed, prompt-processing speed, TTFT, maximum stable context, stability, and Granite 8B feasibility.
2. Pareto-nondominated configurations across memory, speed, and quality.
3. Matched-baseline quality deltas and material-degradation decisions.
4. Official merged support boundary versus experimental support boundary.
5. Integration difficulty, maintenance risk, and recommended application role.
6. Explicit limitations, blocked tests, unresolved uncertainties, and non-claims.

**Final audit:**

- Re-run all static, workflow, schema, evidence, and WinUI gates.
- Check traceability from each conclusion to run/evidence records.
- Check that failures and skipped frontier rows remain visible.
- Check no secret, model, source tree, executable, wheel, archive, or unsupported support claim entered the repository.
- Review every PR and unresolved thread.
- Merge only after user approval.

**Checkpoint F — Workbook 05 complete:** Every controlled row is resolved, every formal result is independently validated, quality and performance conclusions are matched and bounded, the final workbook/DOCX/manifests are regenerated and hashed, and the results PR is reviewed and merged.

---

# Checkpoint sequence used during execution

1. **A1–A5:** review after each remaining Phase 1 implementation task.
2. **A:** review live source-admission evidence and PR `#46` before merge.
3. **B0:** user decision on Route B admission/repair/blocking.
4. **B1–B4:** review plan, build controls, Route A build, and Route B build/block outcome.
5. **C1–C4:** review model locks, safety harness, activation/storage proof, and quality evaluator.
6. **C:** review a complete standard-baseline bundle before codecs.
7. **D1:** review conformance and verified storage.
8. **D:** review the complete K/V capability matrix and memory order.
9. **E1:** review Granite 3B frontiers.
10. **E2:** review formal 3B quality/performance results and manual-adjudication queue.
11. **E3–E4:** review asymmetric tests and Granite 8B gate.
12. **E:** review all remaining ablation, repeatability, regression, and negative-path rows.
13. **F1–F2:** review validated ingestion and workbook/index completion.
14. **F:** final scientific, security, traceability, and merge review.

At every checkpoint, stop and present: changed files, commit SHA, tests/workflows and counts, artifacts/hashes, passed/failed/blocked/skipped records, defects found, corrections made, unresolved risks, and the exact next task. No later task begins until the checkpoint is accepted.
