# Model Inspection cleanup Phase 1 evidence

**Evidence date:** 2026-08-08
**Phase 0 closure source:** `8337d5861812b0f4a3867b1e2fb5889bff4e76fc`
**Phase 1 source under test:** `401259594fe5bca365bb9bfd2e4619f41bfc574d`
**Permanent workflow run:** `31234116989`
**Permanent job:** `93043440880`
**Task 9 ledger-verification run:** `31234989915`

## Scope proven by this record

This record closes the Phase 1 WinUI/application-contract cleanup against the exact source tree at `401259594fe5bca365bb9bfd2e4619f41bfc574d`. That SHA is a no-content verification anchor with the same tree as the completed Task 8 source. Phase 1 changes are limited to the audited WinUI presentation/navigation boundary, focused packaged tests, and cleanup documentation/governance. No production worker, WorkerClient, transport, shared worker-protocol, or LLamaSharp feasibility source file changed between Phase 0 closure and this source SHA.

The protected Gate 2 process boundary therefore retains its existing security and protocol meaning. Worker protocol version 1, serialized JSON names, enum and diagnostic meaning, cancellation/timeout meaning, process containment, handle inheritance, environment allowlisting, evidence privacy, and production/test-fixture separation were not modified by Phase 1.

## Permanent build and warning result

Permanent Windows `Build and test` run `31234116989`, job `93043440880`, completed successfully on `401259594fe5bca365bb9bfd2e4619f41bfc574d`. Application and packaged-test Release x64 builds both passed.

The check run reported exactly two annotations. Both were the same pre-existing publish-profile warning: `A publish profile with the name 'win-x64.pubxml' was not found in the project. Set the PublishProfile property to a valid file name.` Phase 1 did not change packaging configuration. No WMC1506 annotation was reported in the final source run, closing the compiler-proven immutable `x:Bind` warning cleanup.

## Six permanent test layers

| Layer | Executed | Passed | Failed | Not executed/skipped |
|---|---:|---:|---:|---:|
| Application contract tests | 82 | 82 | 0 | 0 |
| Transport tests | 20 | 20 | 0 | 0 |
| Worker-host tests | 4 | 4 | 0 | 0 |
| WorkerClient tests | 86 | 86 | 0 | 0 |
| Real worker-process tests | 27 | 27 | 0 | 0 |
| Packaged WinUI/UI-thread tests | 217 | 217 | 0 | 0 |
| **Total** | **436** | **436** | **0** | **0** |

The packaged WinUI TRX independently reported 217 total, 217 executed, 217 passed, and zero failed/error/timeout/aborted/inconclusive/not-runnable/not-executed/warning/pending tests.

## Retained artifacts and independent integrity check

| Artifact | GitHub artifact ID | GitHub SHA-256 | Independently downloaded ZIP SHA-256 |
|---|---:|---|---|
| `unit-test-results-31234116989-1` | `9014868847` | `9df702296add3199d8befd930bc80951745d324288831cafbaa423527dab4bf0` | `9df702296add3199d8befd930bc80951745d324288831cafbaa423527dab4bf0` |
| `gate2-verification-31234116989-1` | `9014868651` | `cca1de6c589abf2b526c7f6ac6e9cb10ded07686e4e07f2b8e51afa0cf65cf53` | `cca1de6c589abf2b526c7f6ac6e9cb10ded07686e4e07f2b8e51afa0cf65cf53` |

The Gate 2 publish manifest recorded Release x64 output at `D:/g2/publish` and these retained identities:

- worker host DLL `GraniteEdgeAI.ModelInspection.Worker.dll`: `95FDD542AC7FDADB08693EC68536E2F92CEDF4FBC2E7DBF84BE962E9652BA4DD`
- resolver sentinel `GraniteEdgeAI.ModelInspection.Worker.HostSentinel.txt`: `B0E6113EC785FDD7FACF92B2398A6F5885704E3603E1323F39946370609CAC17`
- process fixture `WorkerFixture.dll`: `4ED3ECA9B744CCD520EDA9A3042F139B2ECFEFBF5393211056C26D715CE41B4B`

## Process containment and evidence privacy

The permanent run step `Check for orphaned Gate 2 processes` completed successfully. The step `Scan Gate 2 evidence for sensitive content` also completed successfully. This record does not infer additional stdout details beyond those observed successful gate conclusions.

## Cleanup inventory closure

Task 9 adds this evidence file to the permanent cleanup boundary. The generated closure source list contains **372** sorted, unique, existing paths and the review inventory contains **372** rows in the identical ordinal order. Generated `bin`/`obj` outputs and the old Onboarding selector-test path are excluded. The repository `CleanupInventoryContractTests` are required to pass before the closure commit can be created.

## Phase 1 production changes

The behavior-changing cleanup is intentionally small:

- `InspectionContentTemplateSelector` removes duplicated content extraction without changing route outcomes.
- `InspectionContentCardPresentation.Hidden` becomes a fresh snapshot and each content card owns independent mutable disclosure state.
- initial five-stage presentation derives correlated status/detail/automation values centrally while preserving approved text and geometry.
- `ModelInspectionPage` removes redundant `SelectedModelPath` state and uses validated request facts directly while retaining the exact request object.
- compiler-proven immutable `x:Bind` modes are corrected while the genuine mutable disclosure bindings remain `TwoWay`.
- ModelCard and OutcomeCard now fail fast when required XAML visual-state names drift, matching the existing ActionCard behavior.

No new MVVM layer, runtime service, classifier, worker engine, OpenVINO route, TurboQuant execution path, or packaging integration is introduced by Phase 1.

## Explicitly deferred work

- Fixed ARGB status/check brushes remain recorded theming/high-contrast debt; changing them would alter approved rendering and belongs to a dedicated UX/accessibility decision.
- The visible `runtime is support` copy error remains recorded but unchanged because Phase 1 freezes visible wording unless separately approved as a UX correction.
- The production worker still uses the controlled unavailable inspection engine. Real LLamaSharp evidence extraction, worker-to-application mapping, classification/service orchestration, ViewModel execution, and dynamic runtime UI remain later gates.
- The pre-existing missing `win-x64.pubxml` publish-profile warning is outside this WinUI/application-contract cleanup scope.

## Final closure rule

This evidence commit is not by itself the final completion claim. After the ledger/source-list/evidence closure commit is created, the identical closure tree must receive a new exact-head permanent Windows `Build and test` run. Phase 1 is only declared closed if that final run is green and the whole PR diff against base `a4138a613dd643abe12858eec5d1c3beb09e95e7` contains no unintended changes.
