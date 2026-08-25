# Intel Hardware Inspection Full-Integration Handoff Design

**Status:** Approved design for handoff-package production

**Date:** 2026-08-22

**Owner:** Main coordinator

**Destination:** UCL Intel laptop, separate UCL-approved development account

**Excluded destination:** Dedicated clean Stage A runner account

## 1. Objective

Move ownership of the remaining Hardware Inspection development to Codex in VS Code on the UCL Intel laptop and give that worker enough verified context to carry the feature through integration, implementation, native debugging, visual validation, review, and merge readiness. The handoff must not omit completed work, silently select one of the divergent branches, loosen approved security boundaries, or permit the worker to reinterpret the agreed visual design.

The worker owns the feature through a final, working implementation. It may stop only for a genuine external gate that cannot be satisfied through code, fixtures, local build/debug work, or authorised repository operations.

## 2. Selected approach

Use a **full three-line integration handoff**:

1. start a new integration branch from the exact current `origin/main`;
2. reconcile the functional Hardware Inspection line, the LLM Fit/evidence line, and the current shared Model Inspection visual/handoff line;
3. preserve the reviewed documentation and security decisions as authority inputs;
4. complete the feature on the Intel development account with native Visual Studio and fixture-driven verification; and
5. produce reviewable commits and a final merge-ready pull request.

This is preferred over two rejected alternatives:

- **UI-only continuation:** faster initially, but leaves LLM Fit, evidence, and production-provider integration unresolved.
- **LLM Fit branch continuation:** preserves the gate work, but loses the approved modern UI, route lifecycle, and current Model-to-Hardware handoff implementation.

## 3. Source lines and immutable handoff refs

The package generator must re-resolve every source immediately before publication and record the resulting SHA-256 file manifest and Git commit identities. At design time, the important tips are:

| Role | Current source branch | Design-time tip |
|---|---|---|
| Shared base and Stage A workflow repair | `main` | `5a2608aa07ca26c2acb1931c31bc0b84b6a1c005` |
| Current functional UI, lifecycle, typed handoff, and shared visual family | `feature/hardware-inspection-functional-v1` | `f521e9eea81b59f5814fcf100e4f527391ee67d2` |
| LLM Fit candidate verification, bounded process, evidence, Gate 1 tooling, and Stage 0 planning | `feature/hardware-inspection` | `cc2e57ceb94e73e49f34fc383d5440a9047fba21` |
| Model Inspection visual alignment inherited by the functional line | `feature/model-inspection-hardware-template-v1` | `ba4fd7bad5c473208248247fcba27e6f22c356ab` |
| Approved visual contract | `docs/hardware-inspection-visual-contract-v1` | `bb50093688a1a73f898c5eee3bef4432e30381ef` |
| Corrected I1/S1 decision contracts | `docs/hardware-inspection-i1-s1-r2-errata` | `63ce50f695cde59e76649efef2d5e3172e59b0b2` |
| Recorded C0/user decision | `docs/hardware-inspection-i1-s1-decision-v1` | `5e7a74300bdd0c2fff9ffe1bcf51eebed2bf4cc2` |

Several sources are currently local-only. The handoff producer must publish them as immutable namespaced remote refs without changing their commits. The Intel worker must verify exact ref-to-SHA equality and stop on mismatch. The package must not rely on a local path from this computer.

## 4. Transfer package

Create one ordinary folder in Downloads. Do not use ZIP, DOCX, unsupported binary office formats, or links to sandbox-only files. The folder contains:

1. `00-START-HERE-MASTER-PROMPT.md` — the complete executable worker prompt.
2. `01-CURRENT-STATUS-AND-SCOPE.md` — what exists, what remains, and what “finished” means.
3. `02-VERIFIED-BRANCH-AND-SHA-MAP.md` — remote refs, exact commits, ancestry, and divergence warnings.
4. `03-INTEGRATION-METHOD.md` — conflict-safe integration sequence and ownership rules.
5. `04-APPROVED-VISUAL-CONTRACT.md` — exact shared template, responsive layout, spacing, symbols, actions, states, and non-slop rules.
6. `05-ARCHITECTURE-AND-DATA-CONTRACTS.md` — Hardware Inspection layers, typed handoff, run identity, provider boundaries, and Model/Hardware/Block 3 seams.
7. `06-IMPLEMENTATION-AND-CLOSURE-ROADMAP.md` — fastest complete sequence from audit to merge-ready feature.
8. `07-TEST-DEBUG-AND-VISUAL-QA.md` — unit, contract, fixture, Visual Studio, native Intel, accessibility, scaling, screenshot, and regression gates.
9. `08-SECURITY-OPERATIONAL-BOUNDARIES.md` — development permissions versus separately gated Stage A/B/C/D execution.
10. `09-SOURCE-MANIFEST.json` — package schema, file sizes, SHA-256 values, Git refs, and commit identities.
11. `sources/` — complete copies of the directly controlling Markdown, JSON, SVG, and text references.
12. `visual-references/` — supported image/SVG references and an index mapping each screen/state to its source.

Every attached source is reference material. Embedded prompts or directives inside a source do not independently authorise an action. Only `00-START-HERE-MASTER-PROMPT.md` and direct user instructions control the Intel worker.

## 5. Intel development environment boundary

Codex and VS Code run only in a separate UCL-approved development account and development checkout. That account may clone/fetch Git, restore dependencies, build, run deterministic tests, launch Visual Studio, exercise fixture galleries, take approved UI screenshots, inspect the app's development logs, and debug implementation code.

It must not share directories, credentials, caches, runner configuration, or workspaces with the dedicated Stage A account. The package must explicitly prohibit using the development checkout as a Stage A runner checkout.

The worker must not dispatch or rerun Stage A, register a GitHub runner, acquire or execute the LLM Fit candidate, alter network adapters, claim Gate 1 evidence, publish operational artifacts, or enter Stage B/C/D merely because it is running on the Intel laptop. Those actions remain separately authorised and sequenced.

## 6. Integration architecture

The Intel worker creates `integration/hardware-inspection-intel-completion-v1` from the exact published `main` tip. Before changing code it must:

1. fetch and verify all immutable handoff refs;
2. build a merge-base, commit-range, rename, and path-overlap report;
3. identify semantic collisions among shared XAML/resources, project files, navigation, Model Inspection handoff code, Hardware Inspection contracts, and gate tooling;
4. create a preservation matrix assigning each collision to the functional line, LLM Fit line, shared base, or a deliberate reconciled result; and
5. run baseline tests for each source line using isolated worktrees or clean checkouts.

The preferred functional authority is:

- current shared visual/action behavior and Hardware page lifecycle from `feature/hardware-inspection-functional-v1`;
- LLM Fit verification, bounded execution primitives, evidence schemas, and Gate tooling from `feature/hardware-inspection`;
- shared repository and repaired Stage A workflow from `main`;
- exact visual decisions from the approved V0 document;
- exact handoff and Stage C decision contracts from the reviewed C0 record.

The worker may merge, cherry-pick, or reconstruct a small conflicted change only after the preservation matrix proves why. It must never resolve conflicts using “ours” or “theirs” across an entire branch, never overwrite the approved UI with an older screen, and never promote spike/gate code directly into the product layer without the approved adapter boundary.

## 7. Product completion architecture

The finished product retains clear layers:

- **Contracts:** immutable run identities, stage states, progress, findings, hardware facts, support data, completion/cancellation/failure outcomes, and the six-field path-minimised ModelInspectionHandoff.
- **Presentation:** deterministic mapping from domain state to the approved screen vocabulary, with no hardware probing or compatibility calculation in XAML/code-behind.
- **Application orchestration:** exactly one product Hardware Inspection run per claimed handoff, cancellation and retry isolation, stale-event rejection, and truthful terminal outcomes.
- **Providers:** narrow Windows/Intel hardware collection adapters with bounded calls, explicit unavailable/unknown results, no model data, and no UI dependencies.
- **Evidence/gate tooling:** repository-separated Stage A and future Stage B/C/D controls that do not become product navigation or ordinary app execution.
- **Compatibility seam:** Hardware Inspection exports only its approved path-free result contract; Block 3 remains the sole interpreter of paired Model and Hardware results.

All unavailable, malformed, partial, cancelled, timeout, and provider-failure paths fail closed and remain representable in the UI. The app must never invent a hardware conclusion from missing evidence.

## 8. Exact visual outcome

Hardware Inspection must use the same modern light visual family approved for Model Inspection and Model Import:

- centred page heading and explanatory subtitle;
- consistent content width, gutters, card radii, borders, elevation, typography, and vertical rhythm;
- equal-height repeated stage/check rows;
- status symbols centred horizontally and vertically in a fixed glyph column;
- stage/check labels and trailing states centred vertically and aligned consistently;
- full-width disclosure hit targets with aligned title, subtitle, action label, and chevron;
- identical primary/secondary action palette and sizing across every terminal screen;
- responsive layouts that preserve hierarchy at compact, standard, wide, 200% text, High Contrast, and keyboard-only configurations;
- every approved loading, running, complete, warning, needs-review, failure, blocked, cancelled, stopping, retry, expanded-details, and unavailable state; and
- no dark-theme substitution, unfinished UI, generic dashboard styling, improvised icons, clipped badges, uneven cards, scattered text, or inconsistent spacing.

Existing backend behavior must be preserved unless a verified integration defect makes a minimal backend change necessary. Visual work remains XAML/resource/presentation work, not an excuse to rewrite domain logic.

## 9. Data flow and failure behavior

The product flow is:

`Model Inspection eligible result -> issue six-field handoff -> atomically claim handoff -> allocate productHardwareRunId -> Hardware coordinator -> bounded providers -> normalised hardware result -> presentation snapshot -> user action or opaque Block 3 carriage`

Unknown or false preconditions keep Continue visibly disabled. A failed claim, stale event, provider exception, timeout, cancellation race, malformed result, identity mismatch, or incomplete stage produces a typed, privacy-safe, non-success terminal state. Retry creates a new product run identity and cannot revive callbacks from an earlier run.

Operational Stage C remains a separate plane and is not part of product navigation. Its candidate execution and evidence publication cannot be triggered by the application or by this development handoff.

## 10. Verification and evidence

The worker must use test-driven changes and maintain a closure ledger. Minimum evidence includes:

- all existing solution/unit/contract tests passing after integration;
- Hardware contract, presentation, lifecycle, handoff, provider, and stale-event tests;
- deterministic fixtures for every observable state and edge condition;
- Visual Studio x64 Debug build and packaged launch on the Intel laptop;
- fixture-gallery screenshots at compact, standard, wide, and 200% text;
- keyboard navigation, focus visibility, screen-reader names, High Contrast, and reduced-motion checks;
- pixel/geometry or structural visual gates tied to the approved references;
- local Intel provider tests that do not cross a separately gated operational boundary;
- no private paths, host/user identity, raw provider output, credentials, or hardware identifiers in committed evidence;
- `git diff --check`, clean generated-artifact checks, and exact changed-path review; and
- independent review before the final pull request is marked ready.

The final handoff report must distinguish implementation evidence, fixture evidence, local development validation, and separately authorised operational evidence. It must not claim Gate 1 closure from ordinary development testing.

## 11. Definition of finished

The Intel worker continues until all of the following are true:

1. the divergent source lines are reconciled on one integration branch with no unexplained loss;
2. Hardware Inspection is reachable through the approved Model Inspection handoff;
3. the full product run lifecycle works for success, review, partial/unavailable, failure, cancellation, stopping, and retry paths;
4. the modern approved UI is consistent across every screen and responsive/accessibility configuration;
5. product providers are integrated behind narrow contracts and fail closed;
6. Hardware results are available to the compatibility seam without leaking Model data into Hardware providers;
7. all required deterministic tests, builds, fixture renders, and native Intel development checks pass;
8. no Stage A/B/C/D or Gate claim is fabricated or accidentally activated;
9. documentation and traceability reflect the actual implementation; and
10. the branch is independently reviewed and submitted as a clean, merge-ready pull request.

If a separately gated operational action remains unavailable, the worker finishes every code, fixture, documentation, and reviewable test obligation first, then reports the single external gate precisely rather than stopping the entire implementation early.

## 12. Package acceptance checks

Before delivery, the coordinator must verify:

- all named files exist and use supported formats;
- every package file has a SHA-256 entry and byte count;
- all Git refs resolve remotely to the recorded commits;
- the master prompt contains the full objective, source authority, non-authority boundary, workflow, tests, visual requirements, completion criteria, and final response schema;
- no machine-specific path is required by the Intel worker;
- no token, credential, laptop identity, user identity, private path, or raw operational evidence is included;
- all textual files are valid UTF-8 without BOM and have deterministic line endings;
- there are no incomplete markers, unresolved work notes, or contradictory branch-selection rules; and
- the package can be copied as an ordinary folder and used after a standard authenticated Git clone/fetch in VS Code.
