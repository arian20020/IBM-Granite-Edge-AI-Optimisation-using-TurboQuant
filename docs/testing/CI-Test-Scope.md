# Application test scope

The build-and-test workflow builds the application and runs the packaged WinUI
tests on a Windows runner. Every selected test must pass. A missing TRX,
failed test or unexpected skipped test fails the job.

Four test categories need inputs that are not supplied to the hosted runner:

| Category | Required inputs |
| --- | --- |
| `RequiresVerifiedQuantizer` | The exact verified app-local GGUF quantizer package. |
| `RequiresLocalModelPackage` | The retained OpenVINO models and verified runtime packages named in the tests. |
| `ManualRealModel` | The opt-in end-to-end optimisation matrix, its model packages and an output directory. |
| `OfficialNative` | The app-local verified OpenVINO worker and its canonical model fixture. |

These tests remain in the test project. They are excluded from the hosted job,
not counted as passes. The absent or invalid quantizer tests still run in CI;
they check that missing evidence does not enable a conversion.

For package checks, build the test project with the required verified packages,
then run its `.build.appxrecipe` with Visual Studio's `vstest.console.exe` and:

```text
/TestCaseFilter:"TestCategory=RequiresVerifiedQuantizer|TestCategory=RequiresLocalModelPackage|TestCategory=OfficialNative"
/Logger:trx
```

Keep the resulting TRX with the release test results. Passing hosted CI alone
does not prove that the release packages work. The end-to-end matrix is a
separate check: follow the inputs and opt-in settings in
`OpenVinoHeadlessOptimizationMatrixTests.cs` before running it.

## Deferred chat scrolling issue

On 14 September 2026, the project owner agreed to defer these two tests:

- `DeferredFollowScrollsOverflowAfterLayout`
- `SwitchingOverflowingConversationFollowsTheNewTranscript`

In the isolated test run, automatic scrolling stopped about 110 pixels above
the bottom, even after waiting five seconds. The issue is not fixed. Chat
application code is unchanged by this deferral.

Both tests keep their assertions and use the `DeferredChatAutoScroll` category.
The required CI run excludes this category; excluded tests are not passes.
All other selected tests remain required. Run the deferred tests explicitly with
`/TestCaseFilter:"TestCategory=DeferredChatAutoScroll"`. After fixing the issue,
require both tests to pass and remove the category and its CI exclusion.

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
