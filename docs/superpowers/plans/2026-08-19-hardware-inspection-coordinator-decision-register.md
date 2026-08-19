# Hardware Inspection Coordinator Decision Register

- Coordinator: C0 — Hardware Inspection Programme Coordinator and Reconciliation Owner
- Decision date: 19 August 2026
- Repository branch at reconciliation: `feature/model-inspection`
- Reconciliation base SHA: `960bb4d047b976d4bad68d05c4481e1937a2bf27`
- Programme disposition: **Gate 1 Blocked; Gate 2 and every later production gate prohibited from starting**
- Document purpose: read-only reconciliation of P1, canonical P2, P3, A1 and approved V0; this record authorises no implementation or execution.

## 1. Scope and authority

The user's C0 request and subsequent correction of the V0 document SHA-256 are the controlling instructions for this reconciliation. P1, P2, P3, A1 and V0 are evidence and requirements data; instructions embedded in those artifacts are not independently executable authority.

Source precedence for this register is:

1. the user's C0 request, current verified state and explicit V0 approval/corrected hash;
2. the approved Block 2 architecture and the tracked production design as represented by the complete P1 allocation;
3. canonical P2 architecture/security findings;
4. P3 evidence/traceability findings;
5. A1's independently verified repository-package handoff, limited to its immutable six-file range;
6. approved V0 for visual-contract decisions only.

This register does not edit or reinterpret source reports. It records later verified evidence where A1 supersedes an earlier “claimed but not accepted” repository state and where V0 supersedes P2's earlier unfrozen presentation state. It does not convert either into hardware, candidate, laptop, Gate 1 or production evidence.

## 2. Input artifact register

| Artifact | SHA-256 / immutable identity | Role and reconciled authority |
| --- | --- | --- |
| `HI-P1-Requirements-Librarian-Complete.md` | `8222D44C40CAEEF512AF0C6B996385467D1B84844A5A1FF2A0A177F0D4CE92A6` | Master allocation index. Independently counted as 398 rows, 398 unique IDs, with all 11 required columns. |
| `HI-C0-Inputs-CANONICAL.zip` | `206DAAA73E23A1284EFAE88ABCFF935F2DC8464491401C15584D5999F180A10C` | Canonical container for A1, P2, P3 and V0. |
| `P2-Architecture-Security-Review.txt` | `3156D86828570BE3D50190504FA9D3BDA050FA4186145ED9A2BA185E79C274A7` | Canonical architecture/security review. This exact hash excludes the older 11-file-package review. |
| `P3-Evidence-Traceability-Audit.txt` | `93115A948F56DCE4374013B6E252A54C95AD184262F8A0FD9E724EB5C1572562` | Independent evidence and traceability audit. |
| `A1-Final-Handoff.txt` | `5EB851275F6A451066F6EF0CC3E474A6F41455B9E279198BDF9EE6ED3E11A2F3` | Repository-package verification record only. Frozen Stage A head: `dc70e8e323e5e35708aecd83c654d2763300dc04`. |
| `V0-Approved-Visual-Contract.md` | `F5EAA523742A10C8D52FAEDE2DE2547CC9AF8425A0D03FAEAFE9F4896C01718A` | Approved visual contract only. Commit: `bb50093688a1a73f898c5eee3bef2e30381ef`; approval: “Approve HI-VIS-FREEZE-CANDIDATE-v1 as written.” |

The V0 hash above is the valid 64-character value explicitly confirmed by the user after the original prompt omitted its final `A`. V0 itself was not modified.

## 3. Current verified programme state

| Area | Verified state | Exact boundary |
| --- | --- | --- |
| Stage 0 | Complete | Hosted repository preflight only; no laptop, candidate, hardware or Gate 1 evidence. |
| Stage A repository package | Complete and independently verified | Six files at `dc70e8e323e5e35708aecd83c654d2763300dc04`; 12 Stage A and 12 Stage 0 contract tests passed in A1's verification. |
| Stage A operational execution | Not started | No laptop run, remote run artifact, local TRX, runner registration or target execution exists. |
| Trusted Windows/Intel execution | Not occurred | The three trusted checks have no passing target run; no trusted hardware conclusion exists. |
| Stage B | Not started and prohibited | No approved executable plan, acquisition permission, candidate or sealed receipt. |
| Stage C | Not started and prohibited | No approved fail-closed offline plan and no candidate execution. |
| Stage D | Not started and prohibited | No exact-run collection or sanitised final report. |
| Gate 1 | **Blocked** | Existing historical artifacts lack immutable source/run binding; trusted and offline routes did not execute successfully. |
| Gate 2 and later | Not started and prohibited | Gate 1 has not produced an entry-permitting disposition. |
| Hardware evidence | None | Repository tests, Intel hardware existence and designs are not hardware evidence. |
| Candidate evidence | None | The LLM Fit candidate has not been acquired or executed in the authorised path. |
| Production Hardware Inspection | Not started | No production provider, canonical snapshot, resolver, orchestrator, page or actionable `HardwareInspectionHandoff`. |
| Cross-feature route | Not implemented | No final `ModelInspectionHandoff`, live Model→Hardware route or registered Block 3 route. |
| Continue action | Visible-disabled under V0 | It remains disabled until both a usable `HardwareInspectionHandoff` and a registered Block 3 route exist. |

## 4. P1 requirement-allocation summary

The complete row-level source, locator, authority, owner, prerequisite, acceptance evidence, state and blocker mapping remains in P1 §4. This register references rather than duplicates those 398 rows. P1's source-state totals at extraction were 154 Not started, 150 Blocked, 50 Partially implemented, 30 Decision required, 13 Complete and 1 Unknown. A1 and V0 supply later scoped evidence noted below; no other row is silently promoted.

| Category and stable IDs | Count | Source locator and authority | Current reconciled status | Owners, prerequisites and acceptance evidence | Blocker/conflict and parallel safety |
| --- | ---: | --- | --- | --- | --- |
| Functional behavior, `HI-FUNC-001..028` | 28 | P1 §4.1; approved Block 2 baseline, tracked production design and direct C0 boundaries | Mostly Not started; `HI-FUNC-006` remains partial; candidate/route-dependent rows remain Blocked | C0, G2B, G3–G7, I1 and U1/U4/U7; evidence is service, architecture, navigation and presentation tests identified per row | Gate 1, missing handoffs/routes and unresolved Critical seams. Only pure inactive contracts are conditionally parallel-safe. |
| Canonical data/evidence, `HI-DATA-001..035` | 35 | P1 §4.2; approved Block 2 data model and production design | 33 Not started; one Blocked and one Decision required in P1 remain unclosed | G3–G7 and U1; requires accepted provider evidence and field-level authority policy; evidence is schema, mapping, freshness, resolver and immutability tests | P2-IMP-03/04 and Gate 1. Provider lanes are future-parallel only after Gate 2 and frozen contracts. |
| Security/privacy/trust, `HI-SEC-001..053` | 53 | P1 §4.3; direct trust boundaries, runbooks and security architecture | A1 repository-only requirements are verified at the frozen head; operational and production requirements remain Blocked/Not started/Unknown as allocated | A1, B1–D1, C0, E1, G2A/G2B, I1 and P3; evidence requires immutable run identity, negative-path tests, privacy scans and controlled artifacts | No laptop/candidate evidence; unresolved P2 Critical findings. No execution lane is parallel-safe now. |
| Lifecycle/cancellation/concurrency, `HI-LIFE-001..028` | 28 | P1 §4.4; Block 2 lifecycle and production orchestration design | Not started except source-noted blocked/decision rows | G7, I1, U1 and U7; requires stable process, result and route contracts; evidence is concurrency, cancellation, cleanup, retry and stale-event testing | Gate 2/G6/G7 dependencies and missing seam. Future isolated state models may be parallel after contract freeze. |
| Presentation/accessibility/responsiveness, `HI-UI-001..052` | 52 | P1 §4.5 plus approved V0 commit/hash | V0 resolves the visual decisions that P1 marked pending; implementation and native evidence remain Not started | P4/U0 decision authority is complete for V0 scope; U1–U7 and I1 own future isolated inputs/composition; V0's native WinUI matrix is the acceptance oracle | V0 does not authorise implementation or close Gate 8. Future U1–U6 files are conditionally parallel-safe; shared composition is serial. |
| Gate 1 operations, `HI-OPS-001..134` | 134 | P1 §4.6 and §5.1–5.7; direct C0 operational rules, Stage A package/runbook and Gate 1 plans | Stage 0 and the Stage A repository package are complete; laptop Stage A and Stages B/C/D remain Blocked/Not started | A1, B1, C1, D1, C0 and P3; requires exact run/attempt/session receipts, strict counters, process ledger, privacy-safe artifacts and independent review | External approvals, no operational Stage A run, P2-CRIT-02/03 and absent B/C/D. Entire operational chain is serial. |
| Testing/evidence/traceability, `HI-TEST-001..040` | 40 | P1 §4.7; evidence rules, runbooks and RTM sources | A1 bounded repository tests verified; Gate 1, production acceptance and traceability closure remain Blocked/Not started | C0, D1, E1, G/U owners, I1, P3 and R*; evidence is exact-head tests, manifests, native target results and bidirectional RTM | P3-IMP-01..07 and missing target evidence. Feature-local pure fixtures are future-parallel; central registries/RTM are serial. |
| Cross-feature seam, `MI-SEAM-001..028` | 28 | P1 §4.8 and §7; direct cross-feature boundary overrides lower path-bearing wording | Eligibility is partial; exact model handoff and routes remain Decision required/Blocked/Not started; V0 resolves only their presentation | C0, I1, G7 and U1; requires frozen contracts, allowlist/privacy tests, packaged navigation tests and action-gating tests | P2-CRIT-01 and P2-IMP-05. The seam is serial and not parallel-safe. |

## 5. P2 finding register

### 5.1 Critical findings

| Finding | Disposition | Owner and required closure evidence |
| --- | --- | --- |
| P2-CRIT-01 — missing path-minimised `ModelInspectionHandoff` boundary | **Open — Critical** | C0 decides; I1 is sole implementation/integration owner. Requires an approved immutable/versioned contract, exact allowlist/prohibition list, lifetime and validation rules, privacy tests and independent review. |
| P2-CRIT-02 — Stage C lacks an auditable exactly-one-candidate-execution invariant | **Open — Critical** | C0/C1. Requires an approved Stage C execution contract and evidence that one fresh session has exactly one capture-script-owned process start, with no preflight/version/help/wrapper/retry execution. |
| P2-CRIT-03 — Gate 1 artifacts are not intrinsically bound to reviewed source and actual run | **Open — Critical** | C0/B1/C1/D1, audited by P3. Requires a new Stage B receipt, sealed Stage C session/evidence and Stage D cross-binding; historical artifacts remain non-accepting. |

### 5.2 Important findings

| Finding | Disposition | Evidence or remaining blocker |
| --- | --- | --- |
| P2-IMP-01 — Stage A exact post-publication artifact-validation exit | **Resolved for repository-package scope; operational exit pending** | A1 independently verified the frozen workflow/runbook/tests and exact JSON-only artifact contract at `dc70e8…`. No actual uploaded artifact exists because Stage A has not run; operational completion remains blocked. |
| P2-IMP-02 — production-design wording could permit prohibited Gate 2 work | **Open — Important** | Current precedence is explicit: no executable Gate 2–9 work. It closes only after an entry-permitting Gate 1 disposition and explicit C0 authorisation. |
| P2-IMP-03 — no authoritative accepted LLM Fit schema/capture input | **Open — Important** | Requires accepted source-bound Gate 1 capture and frozen field/unit mapping before Gate 3 exit. |
| P2-IMP-04 — incomplete field authority/freshness/tolerance table | **Open — Important** | G6/C0 must freeze a complete field-by-field table before Gate 6 implementation. |
| P2-IMP-05 — Block 3 route registration/validation/recovery unspecified | **Open — Important** | C0/I1 must freeze route ownership, pre-navigation validation, stale rejection, failure recovery and state retention. |
| P2-IMP-06 — presentation decisions unfrozen | **Resolved for visual-contract scope** | Approved V0 F1–F10 fixes the state-level visual decisions. Implementation and native Gate 8 evidence remain Not started. |
| P2-IMP-07 — worker split lacks enforceable file exclusivity | **Resolved by this register** | Sections 20–22 assign conditionally parallel lanes, serial lanes and exclusive concrete file boundaries. No implementation is authorised. |
| P2-IMP-08 — no executable fail-closed Stage C plan | **Open — Important** | Requires UCL-approved manual isolation/restoration, observable entry/continuous isolation/cleanup proof and a fresh session for every retry. |

### 5.3 Minor findings

| Finding | Disposition |
| --- | --- |
| P2-MIN-01 — production-design status header ambiguity | Open — Minor. The source must not be edited in this task; this register treats it as tracked below the approved Block 2 baseline and not as execution authority. |
| P2-MIN-02 — “runtime paths” conflicts with no-raw-path presentation | Resolved by precedence. V0/details authority permits sanitised runtime identity/role/version only; absolute or machine-local paths are prohibited. |

No P2 finding is closed merely because P2 recommended a remedy. Closures above cite A1, V0 or a concrete C0 ownership decision in this register.

## 6. P3 evidence and traceability register

| Finding/issue | Current disposition | Owner, prerequisite and required action |
| --- | --- | --- |
| P3-IMP-01 — RTM v1.3.1 baseline versus generated v1.3 views | **Open — Important; Stale/Contradictory** | Traceability owner must first verify the v1.3.1 workbook bytes, then run the controlled generator and review all derived diffs. No generated Markdown/JSON/CSV may be hand-edited. |
| P3-IMP-02 — G-M01 conflict | **Open — Important; Stale/Contradictory** | Traceability owner resolves the authoritative workbook/generator transform, then regenerates `Requirement-Evidence-Path-Map.csv`. |
| P3-IMP-03 — historical Gate 1 record lacks immutable candidate/run bindings | **Open — Important; Partially proved** | D1/E1 under C0 must create a new record from the six exact authorised inputs after valid B/C evidence. Historical Blocked record remains immutable. |
| P3-IMP-04 — same-commit provenance of evidence-index link unknown | **Open — Important; Unknown** | C0/E1 must identify a commit containing both index and evidence record or label pre-merge branch/ref/SHA provenance explicitly. A relative link is not proof. |
| P3-IMP-05 — Stage A plan scope versus actual six-file range | **Resolved by A1 plus this C0 scope note** | The as-built range is exactly the six files listed in §7 at commits `4bb1fb3…` and `dc70e8e…`; protected Stage 0 files remained byte-identical. The older plan file map is not as-built evidence. |
| P3-IMP-06 — empty `Test-Traceability-Matrix.csv` | **Open — Important; Not started** | Traceability owner/E1 populate it only through the controlled workflow after stable Gate 4–9 test IDs/evidence exist. AC-F-M07 remains unverified. |
| P3-IMP-07 — approved v1.3.1 workbook bytes missing | **Open — Important; Unknown** | Controlled access is required to recompute declared SHA-256 `2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96` for the 120,290-byte workbook before regeneration. |
| P3-MIN-01 — F-M07 title encoding corruption | Open — Minor. Fix the authoritative source/generator and regenerate; do not patch a generated artifact directly. |
| P3-MIN-02 — runner design “current constraints” are historical | Resolved for coordinator interpretation. This register supplies the current state; the historical text is not run authority. A future Stage B plan must restate current prerequisites. |
| P3-MIN-03 — AC-F-M07 namespace undefined | Open — Minor/Unknown. Define `AC-<requirement-ID>` in the authoritative convention and regenerate validators/views. |

Generated traceability artifacts are outputs, not decision surfaces. Corrections must be made in the authoritative workbook, convention or generator and then regenerated under review.

## 7. A1 repository versus operational status

| Dimension | Repository-package status | Operational status |
| --- | --- | --- |
| Immutable identity | Verified at `dc70e8e323e5e35708aecd83c654d2763300dc04` | No run ID, attempt or laptop session exists. |
| Exact changed range | `.github/workflows/hardware-inspection-intel-runner-stage-a.yml`; `docs/superpowers/plans/2026-08-18-hardware-inspection-intel-runner-stage-a.md`; `docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md`; `scripts/hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA.ps1`; `scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA.ps1`; `tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py` | None of these facts proves that the workflow ran on the laptop. |
| Tests | Stage A: 12 passed; Stage 0: 12 passed; zero failures/errors/skips. PowerShell AST, encoding, hashes, file inventory and independent reviews passed. | No 174+3 target execution, remote artifact or local TRX exists. |
| Digests | Workflow `953167cdfcb983ae6d0ca00831d35fcdf826570001ab7721f1b835ab423c37a5`; runbook `cfef60a33c09e11fbd913a406c25852cad4ca263011fe42091f28059e9f1a51c` | No post-run bytes or uploaded artifact to validate. |
| Security scope | Repository contracts cover identity/privacy, proxy/debug/hooks/cache, token-free prompts, exact TRX parsing, fixed child state and cleanup. | External approvals, trusted operator actions, ephemeral runner lifecycle and cleanup have not occurred. |
| Gate meaning | Security-controlled prerequisite package complete. | Gate 1 remains Blocked; this does not authorise candidate execution, Stage B/C/D or Gate 2. |

## 8. V0 visual-contract dependency record

V0 is authoritative only for the visual decisions it records. It does not authorise XAML/C# implementation, route activation, hardware/candidate execution or Gate 8 closure. Its copy, layout, states, hashes and accessibility rules are immutable inputs for future workers.

| Freeze | Approved dependency |
| --- | --- |
| F1 | Direction B is the only approved 14 August visual direction; unrelated functional/provider/gate claims from that source do not inherit approval. |
| F2 | Completed and CompletedWithWarnings use the full machine-facts/support composition. |
| F3 | The canonical warning fixture has one unresolved review item plus one resolved informational note; only the unresolved item contributes to the count. |
| F4 | Clean Completed copy, facts, actions, seven rows and IT records are fixed. |
| F5 | Invalid/missing-handoff screen, bounded copy and safe recovery are fixed; no provider starts and no sensitive path/payload appears. |
| F6 | Stopping is temporary, uses the approved wording and becomes Cancelled only after cleanup confirmation. |
| F7 | Retry creates a new `InspectionId`, resets disclosures/live regions and rejects stale prior-run events; same-run presentation changes retain disclosure state. |
| F8 | Exactly seven long canonical stage names, truthful completed-stage counting and no fabricated percentage. |
| F9 | Continue to compatibility is visible-disabled until a usable hardware handoff and registered Block 3 route both exist, with the approved accessible explanation. |
| F10 | Native WinUI captures and the V0 theme/responsive/200%-text/reduced-motion/keyboard/UIA matrix are the future evidence oracle; browser HTML/PNG references are not native acceptance proof. |

## 9. Gate 1–9 status

| Gate | Owner(s) | Status | Entry/exit dependency and present blocker |
| ---: | --- | --- | --- |
| 1 — Windows Intel LLM Fit spike | C0, B1–D1 | **Blocked** | Requires exact Stage A/B/C/D evidence, 174+3 deterministic results, 3 trusted Windows checks, exactly one offline execution and a source/run-bound four-way disposition. |
| 2 — shared resource/process-security foundations | G2A/G2B; I1 integrates | **Not started; prohibited** | May start only after an entry-permitting Gate 1 disposition and explicit C0 authorisation. |
| 3 — LLM Fit provider/parser | G3 | **Not started; prohibited** | Requires Gate 2 and an accepted source-bound LLM Fit capture/schema. |
| 4 — Windows/DXGI enrichment | G4 | **Not started; prohibited** | Requires Gate 2 plus NPU/storage decisions and target evidence. |
| 5 — llama.cpp factual capability | G5 | **Not started; prohibited** | Requires Gate 2 and a pinned runtime contract; no model execution/suitability. |
| 6 — authority/resolution/normalisation | G6 | **Not started; prohibited** | Requires Gates 3–5 and a complete field authority/freshness/tolerance table. |
| 7 — orchestration/outcomes/handoff | G7 | **Not started; prohibited** | Requires Gate 6 and stable process foundations. |
| 8 — WinUI/onboarding integration | U1–U7/I1 | **Not started; prohibited** | V0 is frozen, but Gate 7, Model release/handoff and routes are absent; composition/integration is serial. |
| 9 — native Windows/release evidence | E1 | **Not started; prohibited** | Requires qualifying evidence for Gates 1–8 and reconciled RTM/test traceability. |

## 10. Open decision register

| Decision | Source references | Owner | Decision/evidence required | Blocks |
| --- | --- | --- | --- | --- |
| OD-01 — exact `ModelInspectionHandoff` schema, identity, lifetime and stale/re-entry rules | P1 DR-001; P2-CRIT-01 | C0/I1 | Approved allowlist/prohibition list, immutable versioned contract and independent privacy/architecture review | Live Model→Hardware seam and Gate 8 |
| OD-02 — Stage C sole process-creation owner and observable one-execution ledger | P2-CRIT-02; P1 `HI-OPS-099..110` | C0/C1 | Approved fail-closed procedure and fresh-session process evidence | Stage C, Gate 1, Gate 2 |
| OD-03 — Gate 1 receipt/session/source/run/artifact binding | P2-CRIT-03; P3-IMP-03/04 | C0/B1/C1/D1/E1 | Versioned receipt/manifest/report schema and exact commit provenance | Gate 1 closure |
| OD-04 — Stage A external authorisation and default-branch publication | P1 `HI-OPS-011`, `HI-SEC-026..035`; A1 | C0/UCL/operator | Written approvals and controlled availability of the reviewed package; no action in this task | Stage A laptop run |
| OD-05 — candidate evaluation, signature/licence/dependency and redistribution disposition | P1 DR-009 | C0/UCL/legal/B1 | Exact candidate observation plus written policy/authority | Stage B and release packaging |
| OD-06 — accepted LLM Fit schema/field/unit map | P2-IMP-03; P1 DR-013 | C0/G3 | Source-bound accepted capture after Gate 1 | Gate 3 |
| OD-07 — complete canonical-field authority/freshness/tolerance matrix | P2-IMP-04 | C0/G6 | Field-by-field decision table including disagreement/confidence outcome | Gates 6–7 |
| OD-08 — Block 3 route, pre-navigation validation and failed-navigation state restoration | P2-IMP-05; P1 DR-019 | C0/I1 | Registered-route contract, stale rejection and recovery specification | Continue activation/Gate 8 |
| OD-09 — NPU method and unsupported/unknown policy | P1 DR-012 | C0/G4 | Approved Windows mechanism and authority/fallback policy | Gate 4 NPU provider |
| OD-10 — pinned llama.cpp runtime/probing identity | P1 DR-014 | C0/G5 | Package/backend/probe contract | Gate 5 |
| OD-11 — storage path-like field privacy representation | P1 DR-015 | C0/G4/G6/P2 | Allowlisted canonical representation or removal | Storage DTO/Gate 6 privacy |
| OD-12 — v1.3.1 workbook bytes and controlled regeneration | P3-IMP-01/02/07 | Traceability owner | Verified workbook bytes/checksum, corrected authoritative source/generator, regenerated reviewed outputs | Reliable current traceability/Gate 9 |
| OD-13 — AC namespace and test-traceability population | P3-IMP-06; P3-MIN-03 | Traceability owner/E1 | Authoritative ID convention and later controlled mappings | AC-F-M07 and Gate 9 |
| OD-14 — evidence-index same-commit provenance | P3-IMP-04 | C0/E1 | Exact branch/ref/SHA or a merged immutable record | Baseline evidence claim |

## 11. Resolved decision register

| Decision | Resolution and authority |
| --- | --- |
| RD-01 — V0 document identity | User confirmed the authoritative SHA-256 ending `C01718A`; commit remains `bb50093688a1a73f898c5eee3bef2e30381ef`. |
| RD-02 — path-bearing Model request downstream | `ModelInspectionRequest`, full results and ViewModel objects are prohibited downstream. Only a new path-minimised handoff may cross; OD-01 remains open for its exact contract. |
| RD-03 — A1 final repository scope | Exactly the six-file range at `dc70e8…` is the as-built package; older proposed file maps are not as-built evidence. |
| RD-04 — A1 meaning | Repository verified only; it is not a laptop run, Gate 1 evidence or permission for B/C/D/Gate 2. |
| RD-05 — Stage 0 meaning | Hosted repository-only/manual-dispatch preflight is complete and candidate-free; it is not Hardware or Gate 1 evidence. |
| RD-06 — current Gate 2 rule | Current Blocked Gate 1 categorically prohibits Gate 2. A future packaging-concern disposition would still require a new explicit C0 decision. |
| RD-07 — V0 presentation authority | V0 F1–F10 resolves P2-IMP-06 for visual scope, including Direction B, all specified terminal/invalid/stopping/action/stage/Continue/native-evidence rules. |
| RD-08 — runtime-path presentation | Only sanitised runtime identity/role/version may appear; absolute/local paths are prohibited. |
| RD-09 — exact worker collision ownership | I1 exclusively owns shared Model/onboarding/App/project/route/central-fixture seams; U7 owns Hardware page/ViewModel composition; E1 owns RTM/evidence manifests; A1 owns its frozen six files if formally reopened. |
| RD-10 — inactive seam timing | Contract decision work may proceed before Model release closure only as a documentation/test design behind an inactive seam. Live action and route activation wait for Model release closure and all gate prerequisites. |
| RD-11 — Stage A operational count | Any authorised Stage A exit requires exactly 174 deterministic plus the three named Task 8 guards, with zero other/non-passing outcomes. |
| RD-12 — Gate 1 disposition names | Retain `Blocked`, `Rejected`, `FunctionalPassWithPackagingConcern` and `AcceptedForFunctionalEvaluation` unless C0 approves a versioned replacement. |
| RD-13 — action label and visual copy | V0's exact copy, including `Check hardware fit`, is authoritative visual text; it does not change the factual-only Hardware boundary. |
| RD-14 — generated-artifact correction | Generated traceability Markdown/JSON/CSV must be regenerated from corrected authoritative inputs; hand-editing is prohibited. |

## 12. Blocker-to-owner mapping

| Blocker | Owner(s) | Next acceptable evidence |
| --- | --- | --- |
| P2-CRIT-01 / OD-01 | C0/I1; P2/P3 review | Approved handoff decision and privacy/architecture acceptance |
| P2-CRIT-02 / P2-IMP-08 / OD-02 | C0/C1/UCL | Approved Stage C procedure and exact one-process ledger |
| P2-CRIT-03 / OD-03 | C0/B1/C1/D1/E1/P3 | Bound Stage B receipt, Stage C session evidence and Stage D report |
| Stage A laptop run absent / OD-04 | C0/UCL/operator | Written approvals plus one exact run satisfying §13 |
| P2-IMP-02 | C0 | Entry-permitting Gate 1 disposition plus explicit Gate 2 authorisation |
| P2-IMP-03 / OD-06 | C0/G3 | Accepted source-bound capture and schema map |
| P2-IMP-04 / OD-07 | C0/G6 | Complete canonical-field policy table |
| P2-IMP-05 / OD-08 | C0/I1 | Route/validation/recovery decision |
| P3-IMP-01/02/07 / OD-12 | Traceability owner | Verified v1.3.1 workbook and controlled regeneration |
| P3-IMP-03/04 | C0/D1/E1/P3 | New immutable Gate 1 record and same-commit provenance |
| P3-IMP-06 / OD-13 | Traceability owner/E1 | Controlled test mappings after stable production test identities |
| P3-MIN-01/03 | Traceability owner | Corrected generator/source and defined AC namespace |

## 13. Exact prerequisites for Stage A laptop execution

The A1 repository package is a met prerequisite. Every remaining prerequisite below is mandatory before one laptop run:

1. Preserve and independently reverify the exact six-file package at `dc70e8e323e5e35708aecd83c654d2763300dc04`, including workflow/runbook digests and clean tracked range.
2. Make the reviewed controls and exact approved-source manifest available through the controlled default-branch workflow path without changing their reviewed meaning; no push, merge or PR is authorised by this register.
3. Obtain written UCL approval for runner use, repository code, dependency download/transfer, evidence storage, privacy handling, writer trust, identity/group display and bounded cleanup.
4. Use a dedicated non-admin laptop account with mutually isolated ACLs; fresh fixed-local, ancestor-reparse-free runner installation, work and phase directories; no reused state.
5. On a separate trusted device, obtain the current official runner archive and displayed SHA-256; transfer only the verified archive through the approved channel and recompute the same hash on the laptop before extraction.
6. Establish one fresh non-identifying runner name and one one-time label matching `hardware-gate1-[0-9a-f]{16}`; do not reuse, derive identifying data or rename the laptop.
7. Dispatch exactly once from the controlled default branch while the runner is absent. Prove the sole queued run's workflow, run ID, ref, run commit, approved feature SHA, actor, triggering actor, attempt `1`, confirmation input and label.
8. Immediately before token request, registration and `run.cmd`, repeat sole-queue/writer-trust checks and perform UCL-approved no-echo validation of computer/group identity, proxy sources, debug/trace controls, hooks and action-cache overrides. Any unknown state is a hard stop, not a repair prompt.
9. Request only a just-in-time transient registration token, use token-free `config.cmd` arguments with the hidden prompt, keep `ACTIONS_RUNNER_INPUT_TOKEN` absent and clear transient clipboard material under the approved procedure.
10. Start exactly one interactive runner session and accept exactly one job. The job may run only the 174 deterministic tests and three named Task 8 guards; it may not locate, acquire or execute the LLM Fit candidate, trusted hardware route, offline route or network mutation.
11. Require exactly 174+3 passes, zero non-passing/unexpected identities, local-only detailed logs/TRX and one remote artifact named `hardware-inspection-stage-a-summary-<run-id>-1` containing exactly one five-property JSON object.
12. On the trusted device, validate the exact artifact name, run/attempt association, file count, bytes, schema and privacy-safe remote log. Any extra field/file/byte or identifying/path/candidate/raw data fails the run.
13. Confirm process/listener residue is zero, deregister the ephemeral runner (or use only its time-limited removal flow), and clean only the three independently revalidated exact target directories.
14. Obtain independent post-run audit. Preserve Gate 1 as Blocked and require a separate plan and authority before Stage B.

## 14. Exact prerequisites for Gate 1 closure

Gate 1 can receive a new disposition only after all of the following exist for the same reviewed source and controlled evidence chain:

1. A successful, independently audited Stage A laptop run satisfying §13; repository verification alone is insufficient.
2. A separately reviewed and authorised Stage B implementation/run with candidate/legal permission, immutable checkout, exact package/hash/PE/version/signature/licence/dependency observations and fresh 174+3 evidence.
3. Three exact TrustedWindowsIntel checks pass without candidate rerun and produce a sealed, privacy-controlled Stage B session/manifest and pre-offline receipt bound to run ID and attempt.
4. A separately approved manual Stage C procedure proves all relevant network paths isolated, the runner absent, continued isolation until cleanup and exactly one capture-script-owned candidate process execution in one fresh session.
5. Stage C records exact process-start count, command identity, input/output hashes, timestamps, exit state and zero process/listener residue locally; raw evidence never enters the repository or remote artifacts.
6. A fresh Stage D run retrieves the exact Stage B receipt by run ID/attempt, revalidates the sealed source/tree/inventory/session and reads the exact Stage C evidence without rerunning the candidate or offline route.
7. Stage D generates a sanitised final record from the exact six authorised inputs, validates its bytes/privacy/schema and publishes only approved output.
8. The final record contains the immutable bindings in §19 and truthfully chooses one of the four names in RD-12. Historical Blocked artifacts remain immutable and non-accepting.
9. P3/E1 and independent security/specification reviewers verify the complete receipt→session→evidence→report chain, privacy boundary and exact source/commit availability.
10. C0 records the disposition. Only a disposition explicitly interpreted by C0 as entry-permitting may lead to a separate Gate 2 authorisation.

## 15. Explicit prohibition list for Gate 2 and later work

Until Gate 1 and the relevant preceding gates close, nobody may:

- begin executable G2A/G2B production foundations, candidate manifests, launch paths or packaged external-tool activation;
- implement or activate production Gates 3–9, providers, canonical selection, orchestration, Hardware page/ViewModel or live routes;
- acquire, execute, redistribute or claim acceptance of the LLM Fit candidate;
- start Stage B, C or D, run a candidate offline, contact the laptop or alter network adapters;
- enable Continue, register a Block 3 route or implement compatibility calculations;
- calculate model memory, KV cache, overhead, reserves, context limits, quantisation, GPU offload, expected performance, quality or suitability in Model or Hardware Inspection;
- pass `ModelInspectionRequest`, absolute paths, full Model results or ViewModel objects downstream;
- claim that A1 repository tests, Intel hardware existence, fixtures, mocks, screenshots or plans are hardware/candidate/target acceptance evidence;
- hand-edit generated RTM/catalogue/map files or promote statuses without controlled regeneration/evidence;
- publish raw JSON/TRX/logs, stdout/stderr, commands, paths, native errors, machine/account/device/network identifiers, credentials or private UCL data;
- edit shared App/project/route/fixture/RTM seams concurrently or bypass their exclusive owner;
- use V0 as implementation authority, change its copy/layout/state/accessibility values or claim Gate 8 closure from browser references.

## 16. Model Inspection → Hardware Inspection → Block 3 seam decision

The only permitted sequence is:

`Ready or ReadyWithWarnings Model result` → `path-minimised ModelInspectionHandoff` → `Hardware Inspection carries it opaquely while collecting factual hardware only` → `usable HardwareInspectionHandoff from Completed or CompletedWithWarnings` → `Block 3 alone consumes both handoffs and calculates compatibility`.

The following are fixed:

- `ModelInspectionRequest`, absolute model paths, full result objects and ViewModels do not cross the Model boundary.
- Hardware providers receive no model or model-handoff data. Hardware collection, evidence authority, outcome and UI claims cannot vary with model fields.
- Failed or Cancelled Hardware outcomes and unusable snapshots never create an actionable `HardwareInspectionHandoff`.
- Continue remains visible-disabled unless the Hardware handoff is usable and the Block 3 route is registered. A Model handoff is also validated before navigation; route failure preserves recoverable Hardware state.
- Block 3 is the only compatibility owner. No Block 3 calculation or route is implemented or registered now.
- Exact Model handoff fields/lifetime and exact route/recovery mechanics remain OD-01/OD-08 and keep P2-CRIT-01/P2-IMP-05 open.

## 17. `ModelInspectionHandoff` requirements

| Requirement | C0 boundary | Required acceptance evidence |
| --- | --- | --- |
| Path minimisation | Use an explicit allowlist containing only data proven necessary for the later Block 3 boundary; do not serialize or transitively retain the request/result/ViewModel. Exact fields remain OD-01. | Reflection/serialization allowlist and negative dependency tests. |
| No absolute model path | No absolute/local/UNC path, path segment, URI, command, native error or raw diagnostic may appear in the handoff, navigation state, UIA, logs or screenshots. | Windows/UNC/privacy canaries and serialization/screenshot scans. |
| Explicit owner | C0 owns the decision; I1 is the sole projection, Model/onboarding/navigation/App/project/route implementation owner. Presentation never constructs it; G7 may only carry it opaquely. | File/dependency review against §22. |
| Immutability/version/identity | The contract must be framework-neutral, immutable and versioned, with one documented inspection/correlation identity rule. It must not expose provider types. | Contract tests for immutability, version rejection and identity consistency. |
| Lifetime | OD-01 must define creation, one-journey ownership, retry/re-entry replacement and disposal. A handoff becomes stale when its owning Model inspection/result is replaced or no longer eligible; no implicit reuse. | Lifecycle tests for new navigation, retry, back/re-entry and replaced Model runs. |
| Validation | Validate schema/version, identity, eligibility, allowed fields and freshness before Hardware start and again before Block 3 navigation as applicable. Validation failure starts no provider. | Packaged navigation tests asserting zero provider calls and zero new Hardware `InspectionId`. |
| Missing/malformed/stale/ineligible behavior | Render V0's bounded invalid/missing-handoff state, preserve privacy, provide approved recovery and never auto-retry or expose payload/path details. | V0 presentation fixtures plus packaged focus/live-region/navigation tests. |
| Privacy tests | Cover prohibited fields, nested/transitive references, serialization, logs, exceptions, UIA, screenshots, retry/re-entry and stale objects. | Named contract, architecture and packaged privacy suite with zero skips. |
| No Hardware provider leakage | Provider interfaces accept only hardware/run context. Varying opaque Model fields must not change calls, evidence authority, snapshot or Hardware outcome. | Interface-shape tests, zero-call invalid-entry tests and metamorphic provider-call tests. |
| No compatibility leakage | Model/Hardware assemblies contain no compatibility, memory sizing, context, offload, performance, quality or suitability calculation/claim. | Static architecture/semantic scan and dependency tests. |

## 18. Stage C single-execution requirements

1. C1 is the sole Stage C execution owner under C0 and written UCL authority; GitHub runners are stopped and absent.
2. A fresh sealed Stage B session/receipt is mandatory. A retry or any ambiguity invalidates the session and returns to a new Stage B run.
3. The capture script is the sole candidate process-creation owner. The harness, wrappers, version/help/licence checks, preflight, cleanup and reporting must be technically incapable of starting it.
4. Version, hash, PE identity, licence and package checks occur without executing the candidate during Stage C.
5. The predeclared output leaf is absent at entry; no substitute path, cached output or prior process is allowed.
6. Manual isolation must cover physical, wireless, VPN, mobile and virtual interfaces as approved, fail closed, and remain continuously true until candidate processes/listeners are gone.
7. Exactly one TrustedOffline route and exactly one candidate process start occur. Automatic retry, secondary probing, wrapper relaunch and a second direct execution are prohibited.
8. The local process ledger binds session ID, Stage B receipt digest, source SHA/tree/inventory, candidate/executable hash, fixed arguments, start/end timestamps, PID/process-tree observation, exit code, output hash and cleanup result without exporting sensitive raw data.
9. Raw output/TRX/logs remain ignored, fixed-local and outside tracked/runner trees.
10. Network restoration occurs only after candidate/process/listener cleanup is proved; runner restart occurs only after restoration is verified. Independent review is required before Stage D.

## 19. Gate 1 source/run/artifact-binding requirements

Every new Gate 1 receipt, evidence record and final report must bind or cross-bind, as applicable:

- repository URL/identity, named ref, full source SHA, default-branch run commit and evaluated tree/inventory digest;
- workflow identity/version, run ID, attempt, actor/triggering actor where allowed, approved feature SHA and one-time label through privacy-safe references;
- opaque sealed session/manifest identity and prior-stage receipt SHA-256;
- candidate tag/version, release commit, archive name/SHA-256, executable SHA-256, PE architecture/identity, reported version, licence and dependency/signature observations;
- fixed capture command/argument contract by approved identifier, never a raw path-bearing command in remote evidence;
- exact test category identities, counters and TRX/input/output hashes for 174 deterministic, three named guards, three TrustedWindowsIntel and one TrustedOffline execution;
- candidate process-start count, exit/timeout/cancellation state and process/listener cleanup result;
- hardware/candidate raw evidence hashes locally and sanitised published-artifact hash/schema remotely;
- artifact name, run/attempt association, file count, exact bytes/content, retention and privacy scan;
- Stage D's six exact input identities and the final report SHA-256;
- one RD-12 disposition and explicit non-claims for hardware requirements, redistribution and Gate 2.

Hashes without the source/run/session/receipt relationships above do not prove freshness or same-run provenance. Existing historical artifacts cannot be reinterpreted into acceptance.

## 20. Parallel-safe worker map

Because all three P2 Critical findings remain open, **no implementation worker is currently authorised or recommended**. The table records future conditional parallel safety only; each lane requires frozen inputs, distinct files, satisfied gate prerequisites and a new C0 start decision.

| Future worker | Isolated ownership | Conditions before it becomes parallel-safe |
| --- | --- | --- |
| U1 | Immutable Hardware presentation models, exact V0 copy/state tables and pure sanitisation/action-state factory | V0 fixed; stable G7 result contract; no shared page/ViewModel/App/project/registry edits |
| U2 | Hardware-owned theme/token dictionary | V0 fixed; exact §22 file only; I1 alone later registers it in App resources |
| U3 | Isolated seven-stage progress control | U1 state contract fixed; own files/tests only |
| U4 | Isolated completion/warning/failure/cancellation and recovery controls | U1 contract fixed; own files/tests only |
| U5 | Isolated technical-details and IT disclosure control | U1 contract fixed; V0 privacy/copy rules; own files/tests only |
| U6 | Isolated action control/presentation | U1 state and OD-08 route-availability contract fixed; no navigation logic |
| Feature-local fixture/contract tests for U1–U6 | Fixtures owned beside the component | No central registry/count/project edits and no executable provider/candidate path |

G3/G4/G5 and G2A/G2B are not included as currently safe: they require Gate 1/Gate 2 entry conditions and therefore cannot start now.

## 21. Serial worker map

| Serial sequence | Owner | Reason |
| --- | --- | --- |
| `ModelInspectionHandoff` decision → Model action/navigation → Hardware entry | C0 then I1 | Open Critical privacy seam and shared Model/onboarding files |
| Stage A laptop run → Stage B → Stage C → Stage D → Gate 1 decision | operator/A1 package, B1, C1, D1, C0 | Immutable receipts, one session/execution and external authority |
| Gate 6 resolver/normaliser | G6 | Sole field selection/authority owner after G3–G5 evidence |
| Gate 7 orchestration/handoff | G7 | Converges Gate 6 and process contracts |
| Hardware page/ViewModel composition | U7 | Converges U1–U6 and G7; shared state/lifecycle |
| App/Model/onboarding/project/route/central-fixture integration | I1 | Hard shared collision surface; must follow U7 and OD-01/08 |
| RTM/evidence closure | E1, then P3/R* | Generated/central evidence requires one writer and independent review |

## 22. Exact file ownership and collision boundaries

Backslashes below reflect the current Windows repository paths. Future paths are fixed here to prevent competing workers from creating alternate shared owners; creating them still requires later authorisation.

| Exact path or path family | Exclusive writer | Collision rule |
| --- | --- | --- |
| `IBM Granite with TurboQuant (Intel)\Features\HardwareInspection\HardwareInspectionPage.xaml` and `.xaml.cs` | U7 | I1 may wire navigation only after U7 handoff; no concurrent edit. |
| `IBM Granite with TurboQuant (Intel)\Features\HardwareInspection\ViewModels\HardwareInspectionViewModel.cs` | U7 | G7/U1 expose contracts; they do not edit this file. |
| `IBM Granite with TurboQuant (Intel)\Features\HardwareInspection\Presentation\HardwareInspectionPresentation*.cs` | U1 | U7 consumes; component workers do not edit the factory/state. |
| `IBM Granite with TurboQuant (Intel)\Features\HardwareInspection\Presentation\HardwareInspectionTheme.xaml` | U2 | I1 alone adds the App resource reference. |
| `IBM Granite with TurboQuant (Intel)\Features\HardwareInspection\Controls\HardwareInspectionProgress*.xaml*` | U3 | Seven-stage control only. |
| `IBM Granite with TurboQuant (Intel)\Features\HardwareInspection\Controls\HardwareInspectionOutcome*.xaml*` | U4 | Completion/recovery controls only. |
| `IBM Granite with TurboQuant (Intel)\Features\HardwareInspection\Controls\HardwareInspectionDetails*.xaml*` | U5 | Details/IT disclosure only. |
| `IBM Granite with TurboQuant (Intel)\Features\HardwareInspection\Controls\HardwareInspectionActions*.xaml*` | U6 | Presentation only; no route registration. |
| `IBM Granite with TurboQuant (Intel)\Features\ModelInspection\ModelInspectionPage.xaml` and `.xaml.cs` | I1 | No visual/Hardware worker edits shared Model page files. |
| `IBM Granite with TurboQuant (Intel)\Features\ModelInspection\ViewModels\ModelInspectionViewModel.cs` | I1 | Sole downstream projection/action owner. |
| `IBM Granite with TurboQuant (Intel)\Features\ModelInspection\Presentation\ModelInspectionPresentationFactory.cs` | I1 | V0 supplies fixed visual input; no concurrent Model visual edit. |
| `IBM Granite with TurboQuant (Intel)\Features\Onboarding\OnboardingShellPage.xaml` and `.xaml.cs` | I1 | Sole journey/route integration owner. |
| `IBM Granite with TurboQuant (Intel)\Features\Onboarding\Controls\OnboardingStageIndicator.xaml` and `.xaml.cs` | I1 | No U worker edits the shared stage indicator. |
| `IBM Granite with TurboQuant (Intel)\App.xaml`, `App.xaml.cs` | I1 | Sole App/resource/route integration owner. |
| `IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj` and `tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj` | I1 | Feature workers provide requested references; I1 applies them serially. |
| `IBM Granite with TurboQuant (Intel)\Features\Onboarding\DebugFixtures\OnboardingShellPage.FixtureGallery.cs` and central required-test/count registries | I1 | Feature workers keep fixtures/tests local until integration. |
| Stage A six-file range listed in §7 | A1, only if C0 formally reopens | Frozen at `dc70e8…`; B/C workers create separate files. |
| `docs\testing\Test-Traceability-Matrix.csv`, `docs\requirements\RTM-*`, `docs\evidence\indexes\Requirement-Evidence-Path-Map.csv` and final evidence manifests | E1/traceability owner in a scheduled window | P3 is read-only; generated outputs follow controlled regeneration and R* review. |

## 23. Recommended next worker

The recommended next worker is a **C0/I1 ModelInspectionHandoff decision worker**, limited to documentation and contract specification behind an inactive seam. It is not an implementation worker and must not edit application/source/project/route files. Its purpose is to resolve OD-01/P2-CRIT-01 before any UI, navigation or provider implementation worker opens.

If that decision worker cannot derive an exact minimal allowlist and lifetime from authoritative Model requirements without a new product choice, it must return the choice to the user/C0 rather than invent fields.

## 24. Cannot start yet

- Any production implementation worker, including otherwise future-parallel U1–U6.
- Stage A laptop execution until every item in §13 is satisfied and separately authorised.
- Stage B candidate acquisition/preparation, Stage C offline execution or Stage D collection.
- Gate 2 and all Gates 3–9.
- G2A/G2B, G3/G4/G5, G6, G7, U7, I1 live integration or E1 closure work.
- Live `ModelInspectionHandoff` projection/navigation or any use of `ModelInspectionRequest` downstream.
- Hardware page/ViewModel/provider/orchestrator activation.
- Block 3 registration, navigation or compatibility calculations.
- Enabled Continue behavior.
- Traceability status promotion or manual editing of generated artifacts.
- Any hardware, candidate, laptop, workflow dispatch, push, PR or merge action under this register.

## 25. Acceptance criteria for the next worker

The C0/I1 decision worker is accepted only when it returns one reviewable decision artifact that:

1. traces every rule to `MI-SEAM-001..028`, especially `MI-SEAM-006..015` and `MI-SEAM-027..028`, plus P2-CRIT-01;
2. defines an exact minimal field allowlist, immutable/versioned representation, correlation identity and explicit prohibited-field/type/dependency list;
3. proves no absolute/local/UNC path, `ModelInspectionRequest`, full result, ViewModel, native error, command or provider type can be retained directly or transitively;
4. assigns C0 decision ownership and I1 sole projection/integration ownership, with Hardware carrying the value opaquely and providers receiving none of it;
5. defines creation eligibility, one-journey lifetime, replacement/disposal, retry/re-entry and stale rules without implicit reuse;
6. defines validation timing before Hardware start and Block 3 navigation, and maps missing/malformed/stale/ineligible inputs to V0's exact safe presentation with zero provider calls;
7. specifies named contract, reflection/serialization, dependency, privacy-canary, UIA/screenshot, lifecycle, metamorphic provider-call and packaged-navigation tests with zero skips;
8. keeps the route inactive, Continue disabled and Block 3 unregistered;
9. changes no production source, XAML, project, App, route, fixture-registry, V0, A1 or generated traceability file;
10. receives independent architecture/security review with P2-CRIT-01 either explicitly closed by evidence or returned Open with the exact remaining decision—never silently downgraded.

Only after these criteria are met may C0 decide whether to authorise a separate implementation plan. This register itself performs no code, hardware, candidate, laptop, workflow, push or PR action.
