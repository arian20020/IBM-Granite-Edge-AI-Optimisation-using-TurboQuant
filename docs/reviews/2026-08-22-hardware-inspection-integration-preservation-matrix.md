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
| Gate 3 implementation head | Physical short worktree at `789e6be`; exact range after Gate 2 documentation is `05ad17a..789e6be` | PASS; command, evidence, parser, provider, real-boundary, contradictory-GPU, and review-fix slices are independently committed |
| Gate 3 foundation tests | Debug/x64 final-head authoritative TRX `TestResults/HardwareInspection/Gate3FinalFoundationReviewed/gate3-foundation-reviewed.trx` | PASS, 96/96, zero failed/skipped; the 8-test real-process class also passed three consecutive physical-worktree repetitions before the contract-only review fixes |
| Gate 3 Gate-1 regression | Release/win-x64 deterministic category, approved harmless controlled fixture, portable SDK on child `PATH`; final-head TRX `TestResults/HardwareInspection/Gate3FinalDeterministicReviewed/gate3-deterministic-reviewed.trx` | PASS, 174/174, zero failed/skipped |
| Gate 3 packaged regression | Debug/x64 app-container VSTest with the Gate 2 authoritative Hardware Inspection, handoff, and onboarding filter; final-head TRX `TestResults/HardwareInspection/Gate3FinalPackagedReviewed/gate3-packaged-reviewed.trx` | PASS, 114/114, zero failed/skipped |
| Gate 3 repository contracts | Process-scoped `PSExecutionPolicyPreference=Bypass`; Stage A separately, then Stage 0/acquisition/public-contract/theme | PASS, 12/12 and 18/18 |
| Gate 3 builds | Packaged Debug/x64 test project, then app Debug/x64 and Release/win-x64 with the ReadyToRun runtime pack restored for the declared Release setting | PASS, zero errors; packaged build reproduced 14 pre-existing `NETSDK1198`, `CS8602`, and `MSTEST0044` warnings; each app build reproduced only the known `NETSDK1198` warning |
| Gate 3 boundary/package audit | Source scan, exact commit-range inventory, recursive Debug AppX filename scan, machine-path/URL scan, and production-composition check | PASS; no candidate executable, TRX, raw capture, trusted/offline evidence, candidate URL, username, or absolute machine path was added or packaged; `UnavailableHardwareInspectionService` remains composed |
| Gate 3 independent review | Exact range `05ad17a..789e6be`, with review findings fixed test-first and independently re-reviewed | PASS; no remaining Critical, Important, or Minor findings; ready to proceed from the code-review perspective |
| Gate 4 implementation head | Physical short worktree at `62e732c`; exact implementation/design range after Gate 3 closure is `19f7f5f..62e732c` | PASS; bounded-text, Windows processor, system-volume, DXGI, provisional NPU, native-availability, and topology-invariant slices are independently committed |
| Gate 4 foundation tests | Debug/x64 final-head authoritative TRX `TestResults/HardwareInspection/Gate4FinalFoundationFinal/gate4-foundation-final.trx` | PASS, 197/197, zero failed/skipped/not-executed; fresh build had zero warnings/errors |
| Gate 4 native smoke repetition | Physical short worktree; processor, storage, and DXGI integration classes together, minimum 3 | PASS, 3/3 in each of three consecutive repetitions; the DXGI-only class also passed three consecutive repetitions at its implementation commit; no host fact was printed or persisted |
| Gate 4 Gate-1 regression | Release/win-x64 deterministic category, approved harmless controlled fixture, portable SDK on child `PATH`; final-code-head TRX `TestResults/HardwareInspection/Gate4FinalDeterministicFinal/gate4-deterministic-final.trx` | PASS, 174/174, zero failed/skipped/not-executed |
| Gate 4 packaged regression | Debug/x64 app-container VSTest with the authoritative Hardware Inspection, handoff, and onboarding filter; final-code-head TRX `TestResults/HardwareInspection/Gate4FinalPackaged/gate4-packaged-final.trx` | PASS, 114/114, zero failed/skipped/not-executed |
| Gate 4 repository contracts | Process-scoped `PSExecutionPolicyPreference=Bypass`; Stage A separately, then Stage 0/acquisition/public-contract/theme | PASS, 12/12 in 84.398s and 18/18 in 8.660s |
| Gate 4 builds | Packaged Debug/x64 test project, app Debug/x64, and app Release/win-x64 with the ReadyToRun runtime pack restored for the declared Release setting | PASS, zero errors; packaged build reproduced 14 pre-existing `NETSDK1198`, `CS8602`, and `MSTEST0044` warnings; each app build reproduced only the known `NETSDK1198` warning |
| Gate 4 boundary/package audit | Exact range inventory, source scans, recursive Debug AppX filename audit, machine-path/URL scan, package-reference diff, and production-composition check | PASS; zero forbidden tracked/AppX paths, no new package dependency or machine path/URL, and `UnavailableHardwareInspectionService` remains composed |
| Gate 4 independent review | Exact code head `62e732c`; three native availability/ownership findings and the processor-core group invariant were fixed test-first and independently re-reviewed | PASS; no remaining Critical, Important, or Minor findings; ready to proceed from the code-review perspective |
| Gate 5 implementation head | Exact code head `a9c27a59e487336a4a6b79099d35948015c34a4c`; implementation range `a351a7d9..a9c27a59` | PASS; pinned llama.cpp identity/capabilities, isolated native probe, deterministic inactive packaging, process-tree custody, and development acceptance are complete |
| Gate 5 ordinary suites | Foundation 201/201; probe unit suite 22/22; hardware/runner Python contracts 63/63 | PASS, zero failed or skipped |
| Gate 5 packaged regression | Debug/x64 AppX-container VSTest with Hardware Inspection, model-handoff, and onboarding-navigation scopes; authoritative TRX `TestResults/HardwareInspection/Gate5FinalPackaged114/gate5-packaged-114.trx` | PASS, 118/118 with zero non-passing results; the path retains the planned minimum name while actual discovery was 99 + 16 + 3 |
| Gate 5 disposable-guest acceptance | Final reviewed v10 bundle; normally installed signed x64 AppX; registered-AUMID execution; fixed three repetitions | PASS, 32/32 in each repetition, package identity present, `Developer` signature, zero failures; development-only with public trust and Smart App Control both unverified |
| Gate 5 builds | Packaged Debug/x64 test project and app Release/x64 MSIX | PASS, zero errors; only the known `NETSDK1198`, `CS8602`, and `MSTEST0044` warning families were reproduced in their applicable builds |
| Gate 5 boundary/package audit | Source/dependency scans, exact range inventory, recursive Debug/Release AppX inventory, module/process cleanup, production composition, privacy, and `git diff --check` | PASS; one real probe per package, fakes only in the test package, no committed keys/certificates/packages/TRX/raw output, and production remains unavailable |
| Gate 5 independent review | Final runner cleanup, stable process identity, child-observation handshake, custody, packaging, privacy, and nonclaims | PASS; no remaining Critical, Important, or Minor findings |
| Gate 6 implementation head | Exact code head `7990682f`; implementation/design range `63479fa3..7990682f` | PASS; fixed 19-field manifest, checked normalization, source authority, bounded freshness/consistency policy, provenance, and fail-closed canonical snapshot construction are present |
| Gate 6 ordinary suites | Foundation 201/201; probe unit suite 22/22; hardware/runner Python contracts 63/63 | PASS, zero failed or skipped; PowerShell bypass was process-scoped and Smart App Control remained enabled |
| Gate 6 packaged regression | Focused resolution/contracts 78/78 and authoritative Hardware Inspection/model-handoff/onboarding 174/174 | PASS, zero non-passing results |
| Gate 6 builds and architecture | Packaged Debug/x64 test project; app Release/x64 MSIX; evaluated x86 graph | PASS, zero build errors; known warning families only; x86 contains zero resolution Compile items and zero Hardware Inspection Foundation project references |
| Gate 6 boundary audit | Exact-range inventory, dependency/privacy/source scans, production-composition check, conflict-marker scan, and `git diff --check` | PASS; no new dependency/artifact/private-data boundary and `UnavailableHardwareInspectionService` remains composed |
| Gate 6 review | Inline adversarial review of authority, semantics, time/numeric boundaries, collection bounds, manifest, fallbacks, privacy, and activation | PASS with no findings; explicitly not independent because the approved execution choice kept review inline |

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
- Gate 3 has now added the LLM Fit command builder, infrastructure DTO, tolerant parser, validation mapping, and `LlmFitHardwareEvidence` behind this boundary. Production collection remains unavailable until the later policy/orchestrator gates.

## Gate 3 LLM Fit provider audit

- The provider accepts only exact verified `llmfit` v1.1.9 identity and the two exact manifest commands. Version and system invocations run once through the Gate 2 runner with 5-second/4 KiB and 15-second/256 KiB per-stream bounds respectively.
- The maximum-depth-16 parser rejects comments, trailing commas, duplicate root/system/GPU properties, case-changed required names, unsafe strings, invalid CPU/RAM values, and contradictory GPU shapes. Additive unknown properties remain tolerated.
- Available, invalid, and unavailable evidence states have closed invariants and diagnostics. Invalid output retains only individually validated facts; raw stdout/stderr, exit codes, paths, exception messages, and arbitrary diagnostic text are absent.
- The accepted Gate 1 Windows Intel fixture maps 31.72/18.40 GiB, 16 logical processors, and one synthetic Intel GPU. The harmless real-process fixture proves success, version mismatch, invalid JSON, non-zero exit, output overflow, timeout, cancellation-token propagation, and root-process cleanup without executing the unsigned candidate.
- The candidate remains `FunctionalPassWithPackagingConcern`. No candidate binary, acquisition, redistribution, production registration, service activation, model data, compatibility logic, or raw-output persistence was introduced.
- Independent review initially identified misleading mismatch identity and unbounded public enumerable consumption. Commit `789e6be` records the observed verified identity, stops GPU/diagnostic enumeration at closed bounds, rejects undefined diagnostics, and passed re-review with no remaining findings.
- Gate 4 now adds Windows processor/memory/OS, DXGI graphics, storage, and provisional NPU enrichment. Gate 6 later owns authority, unit normalization, tolerance, freshness, consistency, and canonical resolution.

## Gate 4 Windows and DXGI enrichment audit

- `HardwareText` is the single scalar-aware bounded validator used by LLM Fit names and Windows OS facts. It rejects invalid decoding and non-display control/format/separator categories while preserving accepted Unicode exactly.
- Processor evidence keeps registry naming, native architecture, physical cores, and all-group active logical processors distinct. The topology adapter uses a bounded two-call allocation, validates every variable record before advancing, requires exact buffer consumption, and releases its unmanaged allocation in `finally`.
- Native processor, storage, and DXGI availability failures collapse only the approved interop exception set to closed diagnostics; cancellation and programming errors remain distinct. Each `RelationProcessorCore` record must contain exactly one group affinity, matching the Windows contract.
- System-volume evidence retains only capacity and caller-available bytes. The real Windows directory and derived volume root remain inside the native adapter and do not enter evidence, diagnostics, logs, or persisted verification artifacts; deterministic tests use only a synthetic fixture root.
- DXGI enumeration uses flattened COM interfaces in native vtable order after a pre-closure integration failure exposed unsafe managed interface inheritance. `DXGI_ERROR_NOT_FOUND` alone ends successful enumeration; other HRESULTs collapse to closed status, every acquired adapter/factory is released, and a 65th adapter fails closed without retention.
- DXGI adapter names, kinds, vendor/device IDs, ordinal identities, and dedicated-video/dedicated-system/shared-system memory remain source facts only. The provider does not sum memory, select a primary adapter, identify Intel, or infer graphics absence from enumeration failure.
- NPU evidence structurally distinguishes `Present`, `NotPresent`, and `DetectionUnavailable`. The default probe has only a `TimeProvider` dependency and returns `DetectionUnavailable(EnumerationMechanismNotApproved)` after honoring cancellation.
- Independent review found processor/storage availability exceptions and DXGI COM exception ownership needed closed mapping, then found the processor-core group invariant needed to be exact. Commits through `62e732c` resolve those findings test-first; final re-review reported no Critical, Important, or Minor findings.
- No Gate 4 provider is registered into product collection, and no canonical `HardwareSnapshot`, model input, compatibility rule, shell/process/network operation, native capture, host fact, or new package dependency was introduced. Gate 5 subsequently added isolated llama.cpp capability evidence without changing that production boundary.

## Gate 5 llama.cpp capability audit

- Identity is pinned to LLamaSharp/CPU backend `0.27.0`, LLamaSharp commit `7cbbc45e421d55794d5050d126e0b96511007007`, mapped llama.cpp commit `3f7c29d318e317b63f54c558bc69803963d7d88c`, and `win-x64`. Capability output is strict, bounded JSON-v1 evidence; native logs, paths, model data, and raw output are not retained.
- LLamaSharp and its native CPU runtime exist only in the isolated child probe. Foundation, the app, and the test host have no native package reference and do not load llama/ggml modules. Production composition neither discovers nor launches the probe.
- Process execution retains the verified package and stable process objects through cleanup. A private kill-on-close Job Object owns descendants across timeout, cancellation, output overflow, and normal parent exit; the final test fixture handshake is bounded, path-scoped, and test-only.
- The Debug test AppX contains one real probe and the adverse fake fixtures. The Release app AppX contains one inactive real probe and no fake fixture. No certificate, private key, MSIX/AppX, TRX, raw result, host device label, username, private path, URL, or model datum was added by the exact Gate 5 range.
- The final v10 disposable-guest campaign ran the normally installed, developer-signed x64 test AppX through its registered AUMID three times. Every repetition passed 32/32 with package identity present and no recorded failure. Superseded v8/v9 failures led to the stable process-identity and child-observation corrections; only v10 is accepted as final evidence.
- Independent review reported no remaining Critical, Important, or Minor findings. Development acceptance passed in a disposable guest. Smart App Control and public-trust signing remain unverified.
- Production still composes `UnavailableHardwareInspectionService`. Gate 6 is next and remains the sole authority for normalization, provenance, freshness, tolerance, consistency, resolution, and the canonical snapshot; Gates 7 through 9 remain incomplete.

## Gate 6 evidence resolution audit

- The application-side resolver consumes only immutable Gate 3-5 evidence and reads its UTC clock once. It neither recollects evidence nor starts a process.
- Source authority is field-specific: Windows supplies processor architecture/topology, memory/OS, storage, and DXGI facts; LLM Fit supplies processor name/logical count with exact corroboration or bounded fallback; llama.cpp supplies only its pinned runtime identity, backends, and visible devices.
- GiB conversion is checked and uses a documented midpoint rule. Memory consistency uses closed byte tolerances, static and dynamic evidence use separate age limits, future timestamps are bounded, and the aggregate accepted capture span is bounded.
- The exact ordered manifest contains 19 unique canonical fields. Fifteen construction-critical fields must resolve; optional instruction-set, graphics, and NPU facts remain explicitly unavailable rather than fabricated.
- Diagnostics use a bounded enum and stable tokens. Result collections and manifests are copied, sorted where policy requires it, and exposed read-only; raw provider output, paths, exception text, host facts, and model data are absent.
- Resolution sources contain no shell/process launch, listener, network, filesystem, registry, compatibility, model-inspection, handoff, or product-outcome dependency. The Gate 6 range adds no package/project reference or binary evidence artifact.
- Production still composes `UnavailableHardwareInspectionService`. Gate 7 owns orchestration, outcomes, cancellation, progress, and activation; Gates 8-9 own UI integration and supported-machine end-to-end evidence.
- Inline review found no remaining Critical, Important, or Minor issue. It is not represented as independent review; that requirement remains an explicit execution exception.
