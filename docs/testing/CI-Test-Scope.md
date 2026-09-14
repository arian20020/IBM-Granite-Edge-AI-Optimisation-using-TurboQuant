# Application test scope

The build-and-test workflow builds the application and runs the packaged WinUI
tests on a Windows runner. Every selected test must pass. A missing TRX,
failed test or unexpected skipped test fails the job.

Five test categories need inputs that are not supplied to the hosted runner:

| Category | Required inputs |
| --- | --- |
| `RequiresVerifiedQuantizer` | The exact verified app-local GGUF quantizer package. |
| `RequiresVerifiedGgufRuntimeClosure` | A GGUF runtime package whose exact manifest is admitted by the released evidence. A fresh build is not automatically that verified package. |
| `RequiresLocalModelPackage` | The retained OpenVINO models and verified runtime packages named in the tests. |
| `ManualRealModel` | The opt-in end-to-end optimisation matrix, its model packages and an output directory. |
| `OfficialNative` | The app-local verified OpenVINO worker and its canonical model fixture. |

These tests remain in the test project. They are excluded from the hosted job,
not counted as passes. The absent or invalid quantizer tests still run in CI;
they check that missing evidence does not enable a conversion.

For package checks, build the test project with the required verified packages,
then run its `.build.appxrecipe` with Visual Studio's `vstest.console.exe` and:

```text
/TestCaseFilter:"TestCategory=RequiresVerifiedQuantizer|TestCategory=RequiresVerifiedGgufRuntimeClosure|TestCategory=RequiresLocalModelPackage|TestCategory=OfficialNative"
/Logger:trx
```

Keep the resulting TRX with the release test results. Passing hosted CI alone
does not prove that the release packages work. The end-to-end matrix is a
separate check: follow the inputs and opt-in settings in
`OpenVinoHeadlessOptimizationMatrixTests.cs` before running it.

The GGUF runtime category retains the exact released-format assertions and the
check that these choices remain available without a quantizer. Hosted CI still
checks that an absent or invalid quantizer cannot enable persistent conversion.
No new runtime hash is accepted by moving the package-specific checks here.

Always build the test project with its dependencies before packaged checks.
Using `--no-dependencies` can combine an older embedded manifest with newer
worker files; the integrity checks correctly reject that mixed test package.

## Deferred chat scrolling issue

On 14 September 2026, the project owner agreed to defer chat-scroll tests
that would require application changes. These three tests are excluded:

- `DeferredFollowScrollsOverflowAfterLayout`
- `SwitchingOverflowingConversationFollowsTheNewTranscript`
- `PendingFollowDoesNotOverrideManualScrollAway`

The third test failed in both the full run and an isolated run after the
test-environment updates. After requesting a scroll to the top, the offset
was about 405 pixels instead of at most one pixel. Its assertion is retained;
this behaviour is deferred, not fixed or counted as a pass.

In the isolated test run, automatic scrolling stopped about 110 pixels above
the bottom, even after waiting five seconds. The issue is not fixed. Chat
application code is unchanged by this deferral.

All three tests keep their assertions and use the `DeferredChatAutoScroll` category.
The required CI run excludes this category; excluded tests are not passes.
All other selected tests remain required. Run the deferred tests explicitly with
`/TestCaseFilter:"TestCategory=DeferredChatAutoScroll"`. After fixing the issue,
require all three tests to pass and remove the category and its CI exclusion.

## Deferred inspection presentation checks

The project owner asked to keep application code unchanged and exclude checks
that need application changes. These tests retain their assertions under
`DeferredInspectionPresentation` and are not counted as passes:

- `FractionOnlyProgress_DoesNotRepeatTheSamePoliteAnnouncement`: a progress
  update replaces the active stage's accessible name with "Model inspection
  outcome". The progress announcement behaviour is not fixed.
- `LongOutcomeText_WrapsWithoutFixedLineClipping`: the retained standalone
  outcome card does not wrap its title. The current inspection page uses the
  preview views instead, but the old control's limitation remains.
- `TerminalOutcome_RetiresProgressLiveRegionAndStaleAnnouncement`: the preview
  announcement method still accepts a stale progress announcement after a
  terminal result. The test keeps the expected rejection; the app is unchanged.
- `FailureFocus_MovesOnlyWhenFocusedElementBecomesIneffective`: focus remains
  on the preview host instead of moving to the failure heading. The current
  focus method does not make that text heading focusable. This accessibility
  issue remains; the test's expected focus recovery is retained.

Run them separately with
`/TestCaseFilter:"TestCategory=DeferredInspectionPresentation"`. Remove the
exclusion only when these checks pass. A green required run does not establish
that these accessibility and text-layout issues are resolved.

## Source-conversion recovery gap

On 14 September 2026, the project owner requested removal of
`SourceFolderContinue_DispatchesConversionRequiredIntentWithoutNavigation`.
This test started the real OpenVINO conversion route without its worker package.
An invalid trusted manifest raised an unhandled `InvalidDataException` and
terminated the test host. The test was removed, not passed.

The application recovery handler is unchanged. Recovery from this missing or
invalid worker manifest remains an unresolved issue. A future fix needs a
regression test for this failure before this gap can be marked resolved.

## Removed checkpoint-script tests

`PackagedTestCheckpointTests.cs` tested old checkpoint scripts and helper types
that are no longer in the repository. These obsolete infrastructure tests were
removed. This is not a passing result for those scripts or a release-package
verification.

## Export cleanup timing coverage

`DelayedSuccessfulCleanupReconcilesToRetryableCancellation` now waits for the
overall cleanup timeout before completing the test provider. It still requires
retry to stay blocked during cleanup and a Cancelled result once cleanup ends.
This removes an ambiguous ordering in the test, not a change to the app.

The previous setup timed out once in the full suite but passed in the isolated
class run and ten repeat runs. It allowed two timeout paths to race. That exact
overlap is not covered by the ordered test and is not claimed to be fixed.

## Local verification — 14 September 2026

The Release build, including application dependencies, passed with no errors.
The final packaged run used the workflow's test filter: 1,879 tests passed,
with no failures, skips or aborted tests. The required scanner-class presence
checks also passed. The exclusions above are not included in that pass count.

The local result is `TestResults/CheckpointFinal/checkpoint-final.trx`, with
SHA-256 `3B71C43B20CA97B4A204442255B837A58C7BDA736C1DF99C115C2390256828B1`.
This verifies the working changes based on commit
`92d4686c8a9c40c766df4cdbd12f94cd05920e0b`, not a new GitHub Actions run.
No additional application source changes were made during this final CI pass;
the two earlier approved application edits were preserved.

After the hosted-environment test updates, the revised filter passed locally:
1,877 passed, with no failures or skips. The two exact GGUF release checks and
the unconditional negative quantizer check also passed in a separate run.
The full result is `TestResults/HostedAdaptationsFinal/checkpoint-final-filter.trx`
(SHA-256 `D3B0EA114F6BCC5C364D1D0FB3C262126818AAE6D0D00F3465850E9EA6B5F2BA`).
These are local results, not proof of a successful hosted run.

## Hosted verification — 14 September 2026

[Build and test run 34804762103](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/actions/runs/34804762103)
passed for commit `73684f838a1dc6cf08cb2633f8c7b87379388564`. Both builds passed,
and all 1,877 selected tests passed with no failures, skips or aborted tests.
The excluded and deferred checks above are not part of this pass count.

The downloaded result is
`TestResults/Hosted34804762103/GraniteEdgeAI.UnitTests.trx`, with SHA-256
`83ED044C7B268963AFAF3E3462BF5D2EEBBC48CB3E72597CAA523C5FEAE6C327`.
The CI updates after `02abd95f` changed only tests, the workflow and this
document. They did not change application or worker implementation files.
