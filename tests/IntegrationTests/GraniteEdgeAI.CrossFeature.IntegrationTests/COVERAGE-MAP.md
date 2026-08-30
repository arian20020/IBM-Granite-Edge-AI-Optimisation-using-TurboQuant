# T1 R4 executable coverage map

Every linked-contract test compiles the canonical production file directly from the app, shared, or infrastructure tree. `LinkedProductionSourcesResolveCanonicallyWithoutLocalDuplicates` makes this apply-checkable: all includes must resolve, stay beneath allowed roots, exclude `bin`/`obj`, have unique canonical sources and `Link` destinations, and have no test-local `Production` shadow directory. A copied implementation therefore cannot silently diverge from the production source under test.

“Composition fitness” means an executing source/composition assertion only. It is neither behavioral runtime evidence nor native acceptance.

| Test method | Owner | Behavior | Layer | Current disposition | Native/C0 follow-up |
|---|---|---|---|---|---|
| `LinkedProductionSourcesResolveCanonicallyWithoutLocalDuplicates` | T1 | Canonical compile links cannot drift or be shadowed. | Managed build fitness | GREEN | None. |
| `CoverageMapListsEveryExecutableMethod` | T1 | Every executable method appears in this map. | Managed documentation fitness | GREEN | None. |
| `CurrentModelFitAllowsDirectChat` | T1 | Current fit permits direct Chat per route. | Managed behavioral | GREEN | E1 packaged journey. |
| `OnlyAdmittedAlternativeFitRequiresOptimization` | T1 | Only a safe admitted alternative permits optimization. | Managed behavioral | GREEN | E1 packaged journey. |
| `NoSafeConfigurationDisablesExecution` | T1 | No-safe outcome disables execution. | Managed behavioral | GREEN | E1 UI state. |
| `InstalledAvailableReserveAndExecutableBudgetRemainDistinct` | T1 | Four memory values remain distinct. | Managed behavioral | GREEN | H1 native observation. |
| `ProportionalReserveUsesAvailableMemoryWithoutASecondFixedAllowance` | T1 | 8/16/32 GiB classes use proportional reserve only. | Managed behavioral/table | GREEN | H1 native observation; no performance claim. |
| `ProportionalReserveAppliesFloorAndCoherentBudgetBounds` | T1 | Floor, bounds, and absent policy fail closed. | Managed behavioral | GREEN | H1 native observation. |
| `DisposalCannotReleaseRuntimeDuringActiveGeneration` | T1 | Disposal waits for generation using deterministic gates. | Managed behavioral | GREEN | E1 native runtime residue. |
| `StopRemainsAvailableWhileDisposalWaitsForGeneration` | T1 | Stop remains callable during disposal. | Managed behavioral | GREEN | E1 native runtime. |
| `PickerAndExplorerDropUseTheSamePathPrivatePipeline` | T1 | Picker/drop route equivalence. | Managed behavioral | GREEN | E1 native picker/drop. |
| `PickerGgufClassificationPublishesRouteWithoutRootedPath` | T1 | GGUF result is path-private. | Managed behavioral | GREEN | E1 native picker. |
| `DroppedOpenVinoPackagePublishesRouteWithoutFolderPath` | T1 | OpenVINO drop result is path-private. | Managed behavioral | GREEN | E1 native drop. |
| `DownloadedSourceFolderPublishesConversionIntentWithoutPath` | T1 | Source folder emits conversion intent, not OpenVINO inspection proof. | Managed behavioral | GREEN | C0 lacks real conversion composition; E1 executes converter. |
| `IncompleteOpenVinoPackageFailsClosedWithoutPathDisclosure` | T1 | Incomplete package rejects safely. | Managed behavioral | GREEN | E1 native folder route. |
| `TypedDiagnosticRejectsRootedPrivacyCanaries` | T1 | Typed diagnostic rejects hostile rooted free text. | Managed behavioral/table | GREEN | None. |
| `TypedFailureSanitizesHostileDisplayPathAndKeepsBoundedCode` | T1 | Typed failure reduces hostile path to leaf and safe code/message. | Managed behavioral | GREEN | E1 UI presentation. |
| `CallerCancellationSuppressesLateSelectionPublicationWithoutSleep` | T1 | Caller cancellation prevents late selection result. | Managed behavioral/deterministic | GREEN | Shell-wide retirement unavailable here; E1/C0. |
| `V2HandoffUsesTheIndependentExactSixFieldCanonicalEncoding` | T1 | Exact schema-v2 canonical bytes. | Managed behavioral | GREEN | None. |
| `GgufAndOpenVinoApprovedContractsProduceIdenticalV2Projection` | T1 | GGUF/OpenVINO handoff route equivalence. | Managed behavioral | GREEN | Native workers E1. |
| `V2HandoffRejectsPathOrderAndDigestMutations` | T1 | Hostile filename/free text/path/order/digest mutations reject. | Managed behavioral/table | GREEN | None. |
| `ClaimRollbackAndReissueRemainOneUseAndIdentityBound` | T1 | Handoff claim/reissue authority is one-use. | Managed behavioral | GREEN | Shell-wide navigation E1. |
| `CoordinatorForwardsTheExactSelectedPlanToRouteExecutor` | T1 | Real coordinator forwards exact selected plan. | Managed behavioral | GREEN | Worker execution E1. |
| `TerminalSuccessWithSubstitutedJourneyIdentityIsSuppressed` | T1 | Reducer must reject substituted model/hardware binding. | Managed behavioral | **INTENTIONAL RED** | C0/Q1 production fix. |
| `WrongGenerationSuppressesLateCancellationAndCompletion` | T1 | Reducer ignores wrong-generation terminal events. | Managed behavioral/deterministic | GREEN | Shell-wide restart retirement unavailable; E1/C0. |
| `PublishedGgufIdentityIsReusedByChatAndExportAfterRestart` | T1 | Real registry returns identical verified identity for two consumers after restart. | Managed behavioral | GREEN | Native Chat/export E1. |
| `RestartQuarantinesOutputWithoutIdentityBoundReceipt` | T1 | Restart removes unreceipted output. | Managed behavioral | GREEN | Native filesystem E1. |
| `SamePlanAttemptCannotPublishThroughDuplicateLiveLeases` | T1 | Duplicate live lease rejects. | Managed behavioral | GREEN | None. |
| `PublishedPlanRejectsASecondTerminalOutputAsStale` | T1 | Second terminal publication rejects. | Managed behavioral | GREEN | None. |
| `RetiringUnsealedLeaseLeavesNoStagedOutputOrPublication` | T1 | Retiring unsealed lease must leave no orphan. | Managed behavioral/deterministic | **INTENTIONAL RED** | C0 storage-owner fix; E1 residue acceptance. |
| `AdmittedPreferenceIssuesStableExactV3Plan` | T1 | Plan v3 is stable except attempt identity. | Managed behavioral | GREEN | Worker E1. |
| `ResultFactoryCopiesExactPersistentPlanAuthority` | T1 | Result factory copies exact plan/config/source authority; no journey claim. | Managed contract behavioral | GREEN | End-to-end propagation is covered only by coordinator/registry and E1. |
| `TurboQuantConfigurationCannotBeAdmittedWithoutExactBuildCapability` | T1 | TurboQuant requires exact build capability. | Managed behavioral | GREEN | Intel/TurboQuant native activation E1. |
| `EveryVisiblePreferenceResolvesOnlyToCapabilityAdmittedCandidate` | T1 | Automatic/manual preferences remain capability-admitted. | Managed behavioral/table | GREEN | UI selection E1. |
| `ExecutionBindingRejectsChangedModelCapabilityPayloadAndHardware` | T1 | Binding helpers reject model/capability/payload/hardware drift. | Managed behavioral | GREEN | Worker E1. |
| `ExactSelectedPlanAloneCanReachExecution` | T1 | Changed payload does not match selected plan. | Managed behavioral | GREEN | Worker E1. |
| `RetryMintsNewPlanIdentityAndRejectsPriorResultAsStale` | T1 | Retry mints new attempt authority. | Managed behavioral | GREEN | Shell retry E1. |
| `DriftPublishesReplanWithNoStaleOutput` | T1 | Drift codes carry no success output. | Managed behavioral/table | GREEN | Worker drift E1. |
| `FailuresPublishNoSuccessOrStaleOutput` | T1 | Failure codes carry no output. | Managed behavioral/table | GREEN | Worker/process failure E1. |
| `CancellationPublishesNoSuccessOrStaleOutput` | T1 | Cancellation carries no output. | Managed behavioral | GREEN | Shell/process cancellation E1. |
| `RuntimeOnlyOpenVinoSuccessCannotMasqueradeAsDownloadableModel` | T1 | Runtime-only result is non-exportable. | Managed behavioral | GREEN | Native UI E1. |
| `PersistentSuccessBindsExactPlanDigestAndRejectsEmptyArtifact` | T1 | Persistent result requires nonempty exact identity. | Managed behavioral | GREEN | Native filesystem E1. |
| `EveryVisiblePreferenceBandEdgeMapsMonotonically` | T1 | All band edges map monotonically. | Managed behavioral/table | GREEN | UI slider E1. |
| `PreferenceValuesOutsideTheVisibleRangeAreRejected` | T1 | Preference bounds reject. | Managed behavioral/table | GREEN | None. |
| `PlanningBindingCarriesExactModelAndHardwareIdentityWithoutPaths` | T1 | Planning binding carries typed path-free identity. | Managed behavioral | GREEN | None. |
| `PlanningBindingRejectsPathLikeCrossFeatureIdentifiers` | T1 | Path-like identifiers reject. | Managed behavioral/table | GREEN | None. |
| `PlanningBindingRejectsZeroLengthAndNoncanonicalDigests` | T1 | Invalid length/digest reject. | Managed behavioral | GREEN | None. |
| `CurrentFitRetainsAnExecutableOptionalOptimization` | T1 | Production evaluator retains optional authority. | Managed behavioral | GREEN | Packaged path E1. |
| `CompatibilityAuthorityRejectsCurrentIdentityMutation` | T1 | Current model/hardware mutation rejects. | Managed behavioral/table | GREEN | Native handoff E1. |
| `StaleMemoryObservationCannotIssuePlanningOrExecutionAuthority` | T1 | Stale resources issue no authority. | Managed behavioral/clock | GREEN | H1 native freshness. |
| `QuantizerChildEnvironmentIsAllowlistedAndDiagnosticsDisabled` | T1 | Child excludes PATH/secrets, disables diagnostics, and requires private TEMP/TMP. | Managed behavioral | **INTENTIONAL RED** | C0 security-owner fix; E1 ACL/process environment. |
| `ApplicationComposesExactlyOneOnboardingShell` | T1 | One onboarding shell composition. | Composition fitness | GREEN | E1 packaged shell. |
| `ChatStageHasNoOnboardingFooter` | T1 | Chat collapses onboarding footer. | Composition fitness | GREEN | E1 UIA/scaling. |
| `RecommendedModelDownloadActionIsFunctionallyWired` | T1 | Exact IBM pins, service, verified publication, resume/restart and SubmitInput composition required. | Composition fitness | **INTENTIONAL RED** | C0 download seam absent; E1 network/native lifecycle. |
| `OpenVinoChatConsumesExactResultBoundConfiguration` | T1 | Exact result Chat/export and real conversion-plus-inspection composition required; forbid `LastPublishedDirectory`. | Composition fitness | **INTENTIONAL RED** | C0 seams absent; E1 native Chat/export/conversion. |
| `RuntimeOnlyOpenVinoResultCannotEnterModelFileExport` | T1 | Save composition filters runtime-only result. | Composition fitness | GREEN | Behavioral destination API absent; E1/C0. |
| `LightShellAndChatComposerKeepKeyboardAndEnabledActionGuards` | T1 | Light/footer/Enter/Shift+Enter/guard source composition. | Composition fitness | GREEN | E1 UIA, enabled-only invocation, scaling/HC. |

## C0 intentional RED list

Exactly five unique methods are expected RED on this base:

1. `TerminalSuccessWithSubstitutedJourneyIdentityIsSuppressed`
2. `RetiringUnsealedLeaseLeavesNoStagedOutputOrPublication`
3. `QuantizerChildEnvironmentIsAllowlistedAndDiagnosticsDisabled`
4. `RecommendedModelDownloadActionIsFunctionallyWired`
5. `OpenVinoChatConsumesExactResultBoundConfiguration`

No T1 result claims Intel-native behavior, performance acceptance, packaged UIA/scaling, real worker/model execution, network transfer, or native filesystem acceptance.
