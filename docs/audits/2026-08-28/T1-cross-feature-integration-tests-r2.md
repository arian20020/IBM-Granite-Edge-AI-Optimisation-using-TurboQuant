# T1 Cross-Feature Integration Test Remediation R2

Date: 2026-08-28
Worker: T1
Branch: `test/ucl-t1-cross-feature-remediation-r2`
Worktree: `C:\UCL-T1-R2`

## Source identity and scope

The worktree was created from `origin/audit/ucl-c0-audit-integration-v1` at commit `a5ef3558334e50587889140dafba194853938765`, tree `90c34ab009b744d7b00866fb93e8dbc86363f1b2`. The required frozen ancestor `4748fe04f19afdf6b27c4c12502b84db325e7294` resolves to tree `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91` and is an ancestor of the authoritative base.

T1 changed only the cross-feature test project, its fixtures/coverage map, and audit documentation. No production source, application composition, worker implementation, or non-test project was changed. `evidenceManifest` is `null` because this worker produced no separately published evidence bundle.

## Added executable coverage

- Picker and Explorer drop now exercise the real picker normalizer and real drop handler against the same path-private classifier, parameterized for GGUF files and OpenVINO package folders.
- Schema-v2 Model Inspection projection is independently checked for exact six-field encoding and byte-identical GGUF/OpenVINO handoff bytes.
- Cross-route compatibility cases cover direct Chat when the current model fits, production retention of an executable optional optimization, required optimization when only the alternative fits, and disabled execution when none fits. Route-specific configuration types and descriptors remain explicit.
- Installed memory, fresh available memory, safety reserve, and executable model budget are asserted as four distinct values using an independent 10 GiB minus 1 GiB calculation.
- The production compatibility input rejects a mutated current Hardware identity. The real journey coordinator forwards the exact selected plan to the route executor. Exact payload matching, retry authority, stale terminal-result rejection by the output registry, cancellation/failure publication, duplicate live publication prevention, restart quarantine, and exact persistent Chat/export identity are executable.
- Runtime-only OpenVINO success is excluded from model-file export. Quantizer child-environment isolation and GGUF stop/disposal lifetime behavior fixed by C0 are retained as characterization coverage.
- One onboarding shell and the absence of the onboarding stage/footer surface on Chat are characterized.

The detailed mapping is in `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/COVERAGE-MAP.md`.

## Characterization GREEN versus remediation RED

The reviewed cross-feature tree discovered 76 tests: 74 passed, 2 failed, 0 skipped. The 74 passes characterize behavior already present at the authoritative base or validate independently derived cross-feature contracts. The two failures are intentional remediation-required regressions and were not weakened:

1. `RecommendedModelDownloadActionIsFunctionallyWired`
2. `OpenVinoChatConsumesExactResultBoundConfiguration`

These RED cases correspond to two unresolved C0 findings: the visual-only download action and OpenVINO Chat using `LastPublishedDirectory` rather than the exact result-bound configuration. The unresolved package-item expression remains directly executable and RED in the affected Model Inspection contract project; T1 does not duplicate that owner regression with a weaker source-shape assertion.

Disabled-action keyboard reachability and UI Automation invoke-pattern accessibility require E1 native WinUI coverage. Download/export cancellation and retry, actual network/file bounds, existing/inaccessible destinations, post-write digest mismatch and partial-output cleanup also require E1 at this base: there is no callable download/export service seam, and the managed host does not instantiate the packaged app, picker, network transfer or destination filesystem journey. The OpenVINO Chat RED is deliberately only a source-level sentinel for the known `LastPublishedDirectory` defect; actual configuration loaded by the native runtime remains E1 acceptance. The managed suite therefore proves functional wiring is currently absent, runtime-only exclusion, exact persistent output identity, and failure publication without pretending that source tokens prove native lifecycle behavior.

## Verification receipts

All commands used the complete installed SDK entry point `C:\Program Files\dotnet\sdk\10.0.400\dotnet.dll`; the repository-pinned `10.0.301` installation is incomplete in this environment.

| Verification | Result |
|---|---:|
| Cross-feature explicit discovery | 76 discovered |
| Cross-feature complete project | 76 total, 74 passed, 2 remediation RED, 0 skipped |
| Model/Hardware Compatibility | 1,050 total, 1,050 passed |
| OpenVINO contracts | 205 total, 205 passed |
| OpenVINO worker client | 13 total, 13 passed |
| GGUF quantization contracts | 7 total, 7 passed |
| GGUF quantization worker client | 8 total, 8 passed |
| GGUF runtime contracts | 12 total, 12 passed |
| GGUF runtime worker client | 9 total, 9 passed |
| Model Inspection contracts | 357 total, 333 passed, 24 failed, 0 skipped |
| `git diff --check` | passed; line-ending notices only |
| Production-change scan | 0 paths |
| Sensitive-data scan of diff | 0 hits |
| Duplicate cross-feature test-name scan | 0 duplicates |
| Duplicate fixture/file SHA-256 scan | 0 duplicates |

The Model Inspection result reproduces the existing authoritative-base failures; T1 did not suppress them. Exact failed test names:

- `BuildWorkflowExecutesEveryGate2LayerWithoutRetainingRawResults`
- `ControlledEvidenceWorkflowIsManualPreflightOnlyAndFailClosed`
- `ControlledEvidencePreflightRejectsCampaignReferenceClassPinAndExecutionMutations`
- `Task12WorkflowGuideAndEvidenceUseAsciiPunctuation`
- `CleanupSourceListIsSortedUniqueAndContainsOnlyExistingFiles`
- `CurrentCleanupScopeIsFullyInventoried`
- `Projects_DeclareExactFailClosedDebugX64FixtureOwnership`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("debug-only-condition")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("missing-default-remove")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("wildcard-include")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("update-instead-of-include")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("none-update-leakage")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("replacement-json")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("alternate-slash")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("alternate-case")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("property-indirected-condition")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("property-indirected-item")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("metadata-indirected-item")`
- `ProjectBoundaryValidator_RejectsInMemoryMutation ("extra-fixture-item")`
- `EvaluatedProjects_ExposeExactClosureOnlyForDebugX64WithMatchingRid`
- `ModelInspectionFixtureGalleryBuildBoundary_EvaluatesOnboardingOnlyForDebugX64`
- `DurableBoardAndBlockedReferences_AreExcludedFromAppAndTestPackages`
- `VisualArtifactPrivacyScanner_AcceptsOnlySafeManifestAndApprovedPngMetadata`
- `VisualArtifactPrivacyScanner_RejectsPathsIdentityModelMetadataPngTextAndTrx`

Their causes remain missing unreceipted workflows, cleanup/fixture ownership drift, child-process `dotnet` PATH resolution, local PowerShell execution policy, and the unresolved package-item expression. The final cross-feature TRX was generated locally at `TestResults/T1-R2-final-reviewed/T1-R2-final-reviewed.trx`. To keep this managed test host reproducible under Application Control, the ten GGUF runtime contract sources are compile-linked and the unused worker-client constructor receives compile-only doubles; no production behavior is substituted in the exercised injected-session lifecycle path. `TestResults` remains ignored and is not part of the commit or evidence manifest.
