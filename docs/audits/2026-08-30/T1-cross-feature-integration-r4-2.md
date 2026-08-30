# T1 R4.2 cross-feature contract and evidence correction

## Disposition

**READY FOR C0 REREVIEW.** The corrected T1 proposal is an apply-built, single-catalog, non-forgeable authority design. The direct T1 runner honestly executed 113 tests: 103 passed and the same 10 declared product-gap cases failed. The independently apply-built authority suite passed 30/30. No production file changed on the T1 branch.

This is managed development-machine evidence only. It is not Intel-native, performance, package, or release acceptance.

## Immutable source and ancestry

| Identity | Commit | Tree |
| --- | --- | --- |
| Frozen historical source | `4748fe04f19afdf6b27c4c12502b84db325e7294` | `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91` |
| R4.1 immutable subject | `6f6d774681a735c67c655598e95cd8b9310cf015` | `3cfeed36bd0cdedf8e3b9274684f4c765a808cee` |
| R4.1 publication / R4.2 start | `027a9d9f9348546b0bc7d4f4f23005e9c0ec30ca` | `87c90f613eb716806d15fed6e5e67367e7f4707b` |
| R4.2 first subject commit | `133f38cc670aeccc367e59d420c922908a7a0460` | `14ba4d3c4837434003fe50bedda6384e2f2956c3` |
| R4.2 final immutable subject | `9b3bffefdd5f337803a52b938b141d8a56063f99` | `0ceb829d73d3eb1483ee4b87d491806ac9306720` |

The branch `test/ucl-t1-cross-feature-remediation-r4-2` began exactly at the required R4.1 publication. Direct parentage is `027a9d... -> 133f38... -> 9b3bff...`; the publication commit is to be directly above `9b3bff...`. The required R4.1 report matched 26,084 bytes and SHA-256 `cf6dacd10770fd4f6977173293f46fc14be385e90feb737f79c7bbc3d0d7d16d`; its receipt matched 1,090 bytes and `f54c83960d5a74e32b5ba1cbbede7f8f889f5e4f39af93e6f8d2b1cba166c3d1`. The source gate, clean state, and required ancestry through `af799e8ba62bd4528568ab9ddcf72d82da762ca1` were verified before work.

## F1 authority inspection

The proposal was applied and tested on the exact reviewed F1 tip `414ad9f97e5cc27e6b710f82d70b985aa14f3507`; its catalog blob is `110a77b5dfc53deb1787a56b6a1e4d8d7b3b7a55`. No advertised remote F1 R4.1 ref existed.

A newer local ref did exist and advanced during rereview: `audit/ucl-f1-frontend-download-remediation-r4-1` was finally observed at `f6af3666e656415d3b8fc51b7a628e237f9758e6`, tree `161d7b3d9aea4cc80eef24968bff988d3e245337`; its direct ancestors include `f64b9895d83409e5eee4e5ffe6323f037fea977e`, `24e5cd0bd24da72e92377821abc91c1ae051c560`, and `42f7c9e7c3a7fd169760777bdbf6a0f44426c1c5` above `414ad9...`. Relative to the reviewed tip it changes nine app files and seven F1 unit-test files for lifecycle/cancellation/retry hardening. Its catalog blob remains exactly `110a77...`. It was inspected, recorded, and not silently substituted as the proposal base.

Read-only inspection confirmed the five exact slider mappings, repository `ibm-granite/granite-4.0-h-micro-GGUF`, revision `51ce07a9c9cfa971ca359d9625836bf8a4a1b61f`, filenames, byte lengths, and lowercase SHA-256 pins. The proposal migrates those definitions into one shared authority; it leaves no second app-local production catalog.

## Corrections and TDD evidence

The R4.1 runner commands used VSTest-shaped options against Microsoft.Testing.Platform and did not prove execution. R4.2 inspected the generated executable's help, listed every filter before execution, enforced strict non-zero floors, and used the direct runner with TRX reporting.

The old proposal exposed caller-constructible requested metadata. TDD proved that a self-consistent arbitrary non-catalog tuple was accepted. The corrected proposal resolves a closed `OptimizationPreferenceBand` through the migrated catalog, keeps catalog-entry and observed-evidence construction inside the authority, and re-resolves the canonical entry at the trust boundary. Verification requires exact band, repository, revision, filename, byte length, lowercase SHA-256, operation, generation, publication, inspection, and lifecycle bindings. It fails closed for unknown bands, each independently mutated field, path-like names, empty authority bindings, incomplete/cancelled/retired operations, and self-consistent non-catalog tuples.

The proposed authority itself owns catalog resolution and exact completion verification. Friend access is limited to `GraniteEdgeAI.ModelDownload.Authority.Tests` and the existing F1 owner test assembly `GraniteEdgeAI.UnitTests`; the application is not a friend. A test-only internal legacy constructor preserves F1 owner-fixture compatibility and derives its band from the slider rather than accepting arbitrary authority. As `C0-INTEGRATION.md` states, C0 must still move/add the downloader, completion-evidence production, and coordinator wiring behind a narrow public operation/opaque-result seam; that future seam is a required integration action, not present proposal behavior. The separate R4.1 `IExactOptimizationResultConsumer` Chat/export seam is retained.

The patch is zero-context, no-renames, whitespace-clean, and ordinary `git apply` compatible. A fresh apply at exact F1 tip passed `git apply --check`, `git apply`, and `git diff --check`. The authority suite then discovered and passed 30/30, including positive exact-completion acceptance and adversarial rejection. On a dependency-complete, integration-equivalent source carrying the same corrected proposal content, the F1 app managed Release/x64 build and F1 unit project build both completed with 0 warnings and 0 errors. The pure `PinnedGraniteModelCatalogTests` subset passed 17/17.

A broader 64-test F1 download-owner run was attempted on that prepared source: 17 passed and 47 were host-blocked because Windows App Runtime/UITest dispatch was unavailable (`REGDB_E_CLASSNOTREG`). Those environment failures are excluded from acceptance totals; the successful unit-project compile proves the previously incompatible owner constructor call sites now compile. A later clean exact-`414ad...` standalone unit-project attempt was not dependency-complete and failed on pre-existing absent specialist namespaces/XAML types (471 errors, one warning); it is not represented as proposal failure or acceptance evidence.

## Execution ledger

| Execution | Discovered | Executed | Passed | Failed | Skipped | Exit |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| T1 full | 113 | 113 | 103 | 10 | 0 | 2 |
| Memory focused | 14 | 14 | 10 | 4 | 0 | 2 |
| Download/exact focused | 8 | 8 | 5 | 3 | 0 | 2 |
| Compile-link focused | 3 | 3 | 3 | 0 | 0 | 0 |
| Applied proposal authority | 30 | 30 | 30 | 0 | 0 | 0 |
| Prepared-source F1 pinned-catalog owner (corroboration only) | 17 | 17 | 17 | 0 | 0 | 0 |

The full invariant is `113 = 103 + 10 + 0`. All non-intentional T1 tests passed. Focused reruns overlap the full suite. The prepared-source F1 result lacks an immutable source identity and is excluded from acceptance and receipt totals. Authoritative non-overlapping totals are therefore full T1 + exact-applied authority: 143 discovered/executed, 133 passed, 10 failed, 0 skipped.

## Exact executable commands

Commands below are repository-relative and executable as written from the indicated worktree roots. The T1 commands ran from the T1 worktree:

```powershell
dotnet build .\tests\IntegrationTests\GraniteEdgeAI.CrossFeature.IntegrationTests\GraniteEdgeAI.CrossFeature.IntegrationTests.csproj -c Release -r win-x64
$runner = '.\tests\IntegrationTests\GraniteEdgeAI.CrossFeature.IntegrationTests\bin\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.CrossFeature.IntegrationTests.exe'
& $runner --help
& $runner --list-tests json
& $runner --filter "Name~ProportionalReserveIsBoundedByAvailabilityAndBudgetNeverNegative|Name=ZeroAvailableMemoryProjectsAnEstablishedZeroBudgetWithoutThrowing|Name=InstalledMemoryAloneCannotChangeAnAvailableMemoryReserve|Name=MachineMemoryRejectsIncoherentBudgets" --list-tests json
& $runner --filter "Name=DownloadedSourceFolderPublishesConversionIntentWithoutPath|Name=OpenVinoChatAndExportSourceCompositionRequiresExactResultAndLifecycleToken|Name~PickerAndExplorerDropUseTheSamePathPrivatePipeline|Name=PublishedGgufIdentityIsReusedByChatAndExportAfterRestart|Name=PublishedGgufLookupRejectsNonExactResultAuthority|Name=RecommendedModelDownloadActionIsFunctionallyWired|Name=RuntimeOnlyOpenVinoSuccessCannotMasqueradeAsDownloadableModel" --list-tests json
& $runner --filter "FullyQualifiedName~GraniteEdgeAI.CrossFeature.IntegrationTests.CompileLinkIntegrityTests" --list-tests json
& $runner --report-trx --report-trx-filename R42-full.trx --results-directory .\TestResults\R42-rereview-full --minimum-expected-tests 113 --zero-tests-policy strict
& $runner --filter "Name~ProportionalReserveIsBoundedByAvailabilityAndBudgetNeverNegative|Name=ZeroAvailableMemoryProjectsAnEstablishedZeroBudgetWithoutThrowing|Name=InstalledMemoryAloneCannotChangeAnAvailableMemoryReserve|Name=MachineMemoryRejectsIncoherentBudgets" --report-trx --report-trx-filename R42-memory.trx --results-directory .\TestResults\R42-rereview-memory --minimum-expected-tests 14 --zero-tests-policy strict
& $runner --filter "Name=DownloadedSourceFolderPublishesConversionIntentWithoutPath|Name=OpenVinoChatAndExportSourceCompositionRequiresExactResultAndLifecycleToken|Name~PickerAndExplorerDropUseTheSamePathPrivatePipeline|Name=PublishedGgufIdentityIsReusedByChatAndExportAfterRestart|Name=PublishedGgufLookupRejectsNonExactResultAuthority|Name=RecommendedModelDownloadActionIsFunctionallyWired|Name=RuntimeOnlyOpenVinoSuccessCannotMasqueradeAsDownloadableModel" --report-trx --report-trx-filename R42-download-exact.trx --results-directory .\TestResults\R42-rereview-download-exact --minimum-expected-tests 8 --zero-tests-policy strict
& $runner --filter "FullyQualifiedName~GraniteEdgeAI.CrossFeature.IntegrationTests.CompileLinkIntegrityTests" --report-trx --report-trx-filename R42-compile-link.trx --results-directory .\TestResults\R42-rereview-compile-link --minimum-expected-tests 3 --zero-tests-policy strict
```

The proposal apply and authority commands ran from the disposable exact-F1-tip worktree; only the sibling worktree names are setup-specific:

```powershell
$proposal = Resolve-Path '..\R42-T1\tests\IntegrationTests\GraniteEdgeAI.CrossFeature.IntegrationTests\C0-R4.1-CROSS-FEATURE-SEAMS.patch'
git apply --check $proposal
git apply $proposal
git diff --check
dotnet build .\tests\ContractTests\GraniteEdgeAI.ModelDownload.Authority.Tests\GraniteEdgeAI.ModelDownload.Authority.Tests.csproj -c Release
$authorityRunner = '.\tests\ContractTests\GraniteEdgeAI.ModelDownload.Authority.Tests\bin\Release\net8.0\GraniteEdgeAI.ModelDownload.Authority.Tests.exe'
& $authorityRunner --list-tests json
& $authorityRunner --report-trx --report-trx-filename R42-authority-final-rereview.trx --results-directory .\TestResults\R42-authority-final-rereview --minimum-expected-tests 30 --zero-tests-policy strict
```

The following repository-relative commands ran on a dependency-complete prepared source carrying the same proposal content. Its immutable identity was not captured, so these results are corroboration only and are excluded from acceptance and receipt totals:

```powershell
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
dotnet build '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' -c Release
dotnet vstest '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\Release\net8.0-windows10.0.19041.0\GraniteEdgeAI.UnitTests.dll' --TestCaseFilter:"FullyQualifiedName~PinnedGraniteModelCatalogTests" --Logger:"trx;LogFileName=R42-f1-catalog-owner.trx" --ResultsDirectory:.\TestResults\R42-f1-catalog-owner
dotnet build '.\IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' -c Release --no-restore -m:1 -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:GenerateAppxPackageOnBuild=false -p:AppxBundle=Never -p:OpenVinoOfficialWorkerPackagingRequired=false -p:OpenVinoConverterPackagingRequired=false -p:OpenVinoTurboQuantPackagingRequired=false -p:GgufQuantizerPackagingRequired=false -p:HardwareInspectionLlamaCppProbeSkipPackaging=true
```

The app build used Release/x64 with `GenerateAppxPackageOnBuild=false`, `OpenVinoConverterPackagingRequired=false`, `OpenVinoOfficialWorkerPackagingRequired=false`, `OpenVinoTurboQuantPackagingRequired=false`, and `GgufQuantizerPackagingRequired=false`. These explicit exclusions make it managed compile/link proof only; no package producer or native workload was accepted.

## Exact intentional RED inventory

1. `ProportionalReserveIsBoundedByAvailabilityAndBudgetNeverNegative (1,0)` — fixed reserve is not availability-bounded.
2. `ProportionalReserveIsBoundedByAvailabilityAndBudgetNeverNegative (1,1)` — same owner arithmetic gap.
3. `ProportionalReserveIsBoundedByAvailabilityAndBudgetNeverNegative (1073741824,268435456)` — same owner arithmetic gap.
4. `ZeroAvailableMemoryProjectsAnEstablishedZeroBudgetWithoutThrowing` — zero availability constructs an invalid policy-less result.
5. `TerminalSuccessWithSubstitutedJourneyIdentityIsSuppressed` — reducer accepts substituted hardware/source identity.
6. `PublishedGgufLookupRejectsNonExactResultAuthority` — lookup accepts mutated result authority.
7. `RetiringUnsealedLeaseLeavesNoStagedOutputOrPublication` — retirement leaves staged output.
8. `QuantizerChildEnvironmentIsAllowlistedAndDiagnosticsDisabled` — child TEMP remains inherited rather than operation-owned.
9. `RecommendedModelDownloadActionIsFunctionallyWired` — production download composition remains unwired on the T1 source.
10. `OpenVinoChatAndExportSourceCompositionRequiresExactResultAndLifecycleToken` — Chat/export trusts ambient publication state without exact lifecycle binding.

These are eight unique contracts and ten cases because the reserve contract has three data rows. Assertions were not weakened and no product gap is claimed closed.

## Durable evidence, scope, and privacy

Raw TRX remains ignored local corroboration and is not committed because it contains machine paths. The sanitised summary at `docs/audits/2026-08-30/evidence/T1-cross-feature-r4-2-summary.json` records identities, counts, exit codes, intentional RED names, and local raw hashes without publishing private values. A hash does not give C0 possession; C0 must regenerate the raw results. The receipt's `evidenceManifest` is `null` because the governing v2 schema requires null for T1.

The immutable subject range changes exactly:

- `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/C0-R4.1-CROSS-FEATURE-SEAMS.patch`
- `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/C0ProposalAuthorityIntegrityTests.cs`
- `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/COVERAGE-MAP.md`

No production app, XAML, project, shared-library, owner-test, or specialist-branch file changed on the T1 branch. Production-looking paths occur only inside the proposal patch. Privacy scans found no worktree path, username, secret, raw payload, or model data in published artifacts. Updated text is UTF-8 without BOM, LF-only, with one final newline; `git diff --check` passes.

## Independent review

The first independent read-only review found no Critical issues and five Important issues: an application-wide friend grant, broken F1 owner-fixture constructor compatibility, no positive verifier-acceptance test, placeholder rather than executable evidence commands, and omission of the newer local F1 branch identity. All five were reproduced and corrected in final subject `9b3bff...`; affected builds and tests were rerun. The first final reread then found one Important evidence-provenance issue: prepared-source F1 corroboration was included in authoritative totals and the future C0 downloader seam was overstated. Both were corrected without changing the immutable subject. Final independent rereview disposition: **APPROVED**, with no Critical or Important findings remaining.

## Preserved evidence and nonclaims

R4.1's independent reserve oracle, non-negative arithmetic, incoherent-memory rejection, real-reducer RED, public-preflight GREEN, exact-result seam, three compile-link projects, GGUF/OpenVINO separation, path privacy, and historical 24-to-fresh-22 Model Inspection reconciliation remain intact.

T1 did not implement production behavior, merge a specialist branch, modify `main`, open a PR, execute a model or native candidate, perform a hardware action, claim Intel-native/performance/package/release acceptance, publish raw TRX, or weaken a gate.
