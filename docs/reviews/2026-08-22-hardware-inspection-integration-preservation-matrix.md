# Hardware Inspection Integration Preservation Matrix

## Provenance

| Item | Verified value |
|---|---|
| Pinned mainline / integration start | `5a2608aa07ca26c2acb1931c31bc0b84b6a1c005` |
| Functional source | `f521e9eea81b59f5814fcf100e4f527391ee67d2` |
| LLM Fit Gate source | `cc2e57ceb94e73e49f34fc383d5440a9047fba21` |
| Model visual source | `ba4fd7bad5c473208248247fcba27e6f22c356ab` |
| Visual contract source | `bb50093688a1a73f898c5eee3bef4432e30381ef` |
| I1/S1 contract source | `63ce50f695cde59e76649efef2d5e3172e59b0b2` |
| C0 decision source | `5e7a74300bdd0c2fff9ffe1bcf51eebed2bf4cc2` |
| Published handoff source | `606c6cc24478d5fa87c1d1b1c40ad11fb2b901ab` |
| Package manifest SHA-256 | `A678AEE98D004E72BA7BEB5F24937B87E8B1947656E95018DD3021AEAB27F1A2` |

The package contained 86 manifest-listed non-manifest files. Before implementation, every listed byte count and SHA-256 value was verified, all text decoded as UTF-8, JSON/XML parsing succeeded, PNG signatures were valid, and the visual overview/native-approved contact sheets were inspected.

## Required preservation

| Area | Must remain true | Evidence status |
|---|---|---|
| Mainline Intel workflow | Stage A repaired shell formatting and Stage 0 behavior remain intact | PASS, post-increment contracts 24/24 |
| Model handoff | Schema v2 has exactly six canonical fields and a 512-byte maximum | Preserved by functional merge and combined-tree audit |
| Identity | Model run, model handoff, and product hardware run UUID roles remain distinct | Preserved by functional merge and combined-tree audit |
| Lifecycle | Issue, claim, bind, invalidate, retry, and reissue reject stale/replayed state | Preserved by functional merge and combined-tree audit |
| Provider isolation | Hardware providers receive no model data and do not branch on GGUF/OpenVINO | PASS, source audit including new fixture boundary |
| Production execution | Hardware service remains fail-closed until an approved production coordinator exists | PASS, only unavailable production implementation remains |
| Block 3 | Sole future interpreter of model plus hardware evidence; not activated here | Preserved by scope |
| Continue | Not production-enabled by this integration | Preserved by scope |
| Gate 1 | Deterministic assets may merge; blocked operational status is not promoted | PASS, Gate assets remain repository tooling only |
| Operations | No Stage A/B/C/D execution, candidate acquisition, runner registration, or network change | Preserved by scope |
| Visuals | Modern light family plus later native-approved corrections | Pending native QA |

## Verification log

| Checkpoint | Command / evidence | Result |
|---|---|---|
| Package integrity | Manifest and format validation described above | PASS |
| Ref integrity | Exact remote SHA checks for all pinned refs | PASS |
| Integration isolation | Dedicated ignored worktree on `integration/hardware-inspection-intel-completion-v1` | PASS |
| Tool discovery | Python 3.12.10, Visual Studio 18.7 MSBuild 18.7.8, the WinUI application development workload, and VSTest 18.7 are available. A portable SDK 10.0.301 plus .NET runtime 8.0.28 remains under ignored `.worktrees/.tooling`; Windows Developer Mode is enabled for unsigned local AppContainer tests. | PASS |
| Mainline Stage 0/A baseline | `PSExecutionPolicyPreference=Bypass` scoped to the test process; `python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v` | PASS, 24/24 in 92.758s |
| Functional merge | Merge commit `de59d95`; seven legacy Model Inspection workflows absent from pinned mainline were then removed because the repaired Stage 0/A inventory guards reject any extra workflow inventory | PASS, post-merge Python contracts 28/28 in 91.620s |
| Gate merge | Merge commit `6687a71`; the `scripts/README.md` conflict retained the newer Stage A boundary while the Gate workflow, bounded tooling, tests, evidence schema, and docs were imported behind their repository boundary | PASS, post-merge Python contracts 28/28 in 93.845s |
| Gate deterministic managed tests | Portable SDK/runtime, a physical short clone, Developer Mode, and a prepublished harmless fake process fixture | PASS, 174/174 with zero skipped and no residual fake process |
| Combined deterministic tests | Hardware/runner Python contracts 28/28; candidate-acquisition stream regressions 2/2; Gate managed tests 174/174 | PASS |
| LLM Fit Gate 1 operational evidence | Exact pinned v1.1.9 candidate; trusted Windows Intel capture; controlled-offline run; strict six-artifact report generation | `FunctionalPassWithPackagingConcern`: trusted 3/3 and offline 1/1; no candidate listener, port 8787 listener, or residual process. Gate 2 architectural work may begin; production redistribution remains blocked by the unsigned-candidate discrepancy and pending transitive dependency-license inventory. |
| Combined WinUI app build | Portable SDK 10.0.301 with .NET runtime 8.0.28, Debug/x64 and Release/x64 | PASS, zero errors; the latest packaged test-project Debug build reported 3 warnings and the latest app Release build reported 1 warning |
| Packaged WinUI test execution | Generated Debug/x64 `.build.appxrecipe`; Visual Studio WinUI application development workload; Windows Developer Mode; x64 app-container VSTest runner | PASS, focused fixture-gallery tests 5/5 and the broader Hardware Inspection, onboarding navigation, and Model Inspection contract suite 65/65 |
| Debug hardware fixture increment | Test-first 15-scenario catalogue, real-page preview gallery, onboarding entry/close path, and Release assembly token audit | PASS: fixture type is present in Debug and absent from Release; all five packaged fixture-gallery tests execute successfully in the WinUI app container |
| Post-increment Stage 0/A regression | Process-scoped `PSExecutionPolicyPreference=Bypass`; `python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v` | PASS, 24/24 in 93.297s |
| Native Debug/x64 visual/accessibility QA | Not run yet | PENDING |
| Gate 2 foundation and security boundary | Candidate-neutral Windows snapshot, retained trusted-package custody, suspended Job-assigned process launch, bounded output/timeout/cancellation, and inactive x64 app adapter | PASS at `de3e56a`; independent review found no remaining Critical, Important, or Minor findings |
| Gate 2 foundation tests | Physical short worktree, Debug/x64, authoritative TRX `TestResults/HardwareInspection/Gate2FinalFoundation/gate2-foundation.trx` | PASS, 50/50, zero failed/skipped; build 0 warnings/errors |
| Gate 2 Gate-1 regression | Release/win-x64 deterministic category with the approved harmless controlled fixture root and portable SDK on child `PATH` | PASS, 174/174, zero failed/skipped |
| Gate 2 packaged regression | Debug/x64 app-container VSTest; Hardware Inspection, model-handoff registry/codec, and onboarding scopes | PASS, 114/114, zero failed/skipped; the previously noted 131 total was not reproducible from its documented filter, so the fresh TRX count is authoritative |
| Gate 2 repository contracts | Process-scoped `PSExecutionPolicyPreference=Bypass`; Stage A separately, then Stage 0/acquisition/public-contract/theme | PASS, 12/12 and 18/18 |
| Gate 2 app builds | Portable SDK 10.0.301, Debug/x64 and Release/x64 | PASS, zero errors; one known `NETSDK1198` missing publish-profile warning per build |

## Functional merge audit

- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection` contains no GGUF or OpenVINO interpretation and refers to the model handoff only as `OpaqueModelHandoff` on the page boundary.
- The only product implementation of `IHardwareInspectionService` introduced by the functional history is `UnavailableHardwareInspectionService`; no production coordinator was invented or activated.
- The seven removed workflow files existed at the pinned functional SHA and did not exist at the pinned mainline SHA. Their reintroduction caused the intended repository-wide Stage 0/A inventory tests to fail; the focused guards passed after their exact removal.

## Gate merge audit

- The imported workflow contains one hosted `deterministic` job. It builds and tests repository code and uploads only a sanitized test-count summary; it has no self-hosted runner, candidate acquisition, hardware capture, product navigation, or Stage B/C/D job.
- Stage 0/A inventory tests retain closed exact allowlists expanded only for the one Gate workflow, three bounded Gate scripts, and one Gate runbook.
- The Stage A scripts index deliberately retains the newer mainline text and does not link the operational Gate runbook.
- No imported Gate type is referenced by `Features/HardwareInspection`, the app project, onboarding navigation, or the product service composition.

## Debug fixture increment audit

- `HARDWARE_INSPECTION_FIXTURE_GALLERY` is defined only for Debug/x64. The app and test projects remove the Hardware Inspection debug-fixture trees by default and include their exact files only in that configuration.
- The deterministic catalogue covers all nine presentation kinds and all seven active stages. It renders through `HardwareInspectionPage.Apply` and contains no service, provider, external-process, model-handoff, GGUF, or OpenVINO dependency.
- Both completed fixtures use the existing presentation factory with `hasUsableHandoff: false` and `block3RouteRegistered: false`; their Compatibility Continue actions are disabled with accessible help.
- The onboarding debug entry invalidates and detaches any active hardware journey before showing the gallery, retains the Import Model stage, and closes to a fresh Model Import page.

## Gate 2 foundation audit

- Production composition still constructs `UnavailableHardwareInspectionService`; the Windows available-memory adapter is present but inactive.
- The app-container output contains no `llmfit.exe`, Gate TRX, trusted/offline capture, or Gate evidence artifact. No such artifact was added by the Gate 2 commit range. The repository's older EP-018 documentation TRX predates and is outside this feature scope.
- The foundation contains no `Process.Start`, shell, PowerShell, command interpreter, model contract, GGUF, OpenVINO, compatibility, network, or listening-service dependency.
- Directory/member reparse state is inspected from the same retained no-follow handles used for custody and executable hashing. The verified path hierarchy and exact package members remain deny-write/delete locked through execution.
- Child inheritance is restricted with `STARTUPINFOEX` to the standard input/output/error pipe handles. The root is created suspended, assigned to and verified against the runner's private kill-on-close Job Object, then resumed.
- Independent review of the final native trust boundary reported no remaining Critical, Important, or Minor findings and marked it ready to merge from the code-review perspective.
- Gate 3 is next: add the LLM Fit command builder, infrastructure DTO, tolerant parser, validation mapping, and `LlmFitHardwareEvidence` behind this boundary. Production collection remains unavailable until the later policy/orchestrator gates.
