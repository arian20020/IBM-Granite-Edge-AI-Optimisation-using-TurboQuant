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
| Tool discovery | Python 3.12.10 and Visual Studio 18.7 MSBuild 18.7.8 are available. A portable SDK 10.0.301 plus .NET runtime 8.0.28 was installed under ignored `.worktrees/.tooling`; no system PATH or registry setting changed. `vstest.console.exe` was not found. | PARTIAL |
| Mainline Stage 0/A baseline | `PSExecutionPolicyPreference=Bypass` scoped to the test process; `python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v` | PASS, 24/24 in 92.758s |
| Functional merge | Merge commit `de59d95`; seven legacy Model Inspection workflows absent from pinned mainline were then removed because the repaired Stage 0/A inventory guards reject any extra workflow inventory | PASS, post-merge Python contracts 28/28 in 91.620s |
| Gate merge | Merge commit `6687a71`; the `scripts/README.md` conflict retained the newer Stage A boundary while the Gate workflow, bounded tooling, tests, evidence schema, and docs were imported behind their repository boundary | PASS, post-merge Python contracts 28/28 in 93.845s |
| Gate deterministic managed tests | Portable SDK/runtime with short ignored artifacts and a prepublished harmless fake process fixture | PARTIAL, 169/174 passed; four reparse tests require unavailable symbolic-link privilege and one real-boundary fake publish is blocked by this checkout's long path. No candidate was acquired or executed. |
| Combined deterministic tests | Hardware/runner Python contracts 28/28; Gate managed tests 169/174 with the environment blockers above | PARTIAL |
| Combined WinUI app build | Portable SDK 10.0.301 with .NET runtime 8.0.28, Debug/x64 and Release/x64 | PASS, zero errors; the latest packaged test-project Debug build reported 3 warnings and the latest app Release build reported 1 warning |
| Packaged WinUI test execution | Generated Debug/x64 `.build.appxrecipe`; Visual Studio WinUI application development workload; Windows Developer Mode; x64 app-container VSTest runner | PASS, focused fixture-gallery tests 5/5 and the broader Hardware Inspection, onboarding navigation, and Model Inspection contract suite 65/65 |
| Debug hardware fixture increment | Test-first 15-scenario catalogue, real-page preview gallery, onboarding entry/close path, and Release assembly token audit | PASS: fixture type is present in Debug and absent from Release; all five packaged fixture-gallery tests execute successfully in the WinUI app container |
| Post-increment Stage 0/A regression | Process-scoped `PSExecutionPolicyPreference=Bypass`; `python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v` | PASS, 24/24 in 93.297s |
| Native Debug/x64 visual/accessibility QA | Not run yet | PENDING |

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
