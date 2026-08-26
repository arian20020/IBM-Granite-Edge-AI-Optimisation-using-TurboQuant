using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class CompatibilityViewModelTests
{
    [TestMethod]
    public async Task InjectedEvaluator_RunsOnceAndPublishesItsDecision()
    {
        int calls = 0;
        CompatibilityScreenModel expected = Screen(CompatibilityScreenState.EstimatedCompatible);
        var viewModel = new CompatibilityViewModel(_ =>
        {
            calls++;
            return Task.FromResult(expected);
        });

        await viewModel.StartAsync();

        Assert.AreEqual(1, calls);
        Assert.AreEqual("Yes — this model should run", viewModel.Presentation.OutcomeTitle);
    }

    [TestMethod]
    public async Task SupersededAttempt_CannotOverwriteTheNewerDecision()
    {
        TaskCompletionSource<CompatibilityScreenModel> first = new();
        int calls = 0;
        var viewModel = new CompatibilityViewModel(_ =>
        {
            calls++;
            return calls == 1
                ? first.Task
                : Task.FromResult(Screen(CompatibilityScreenState.NotEstablished));
        });

        Task oldAttempt = viewModel.StartAsync();
        await viewModel.StartAsync();
        first.SetResult(Screen(CompatibilityScreenState.EstimatedCompatible));
        await oldAttempt;

        Assert.AreEqual("We can't answer this yet", viewModel.Presentation.OutcomeTitle);
    }

    [TestMethod]
    public async Task Cancellation_PublishesTheCancelledPresentation()
    {
        var viewModel = new CompatibilityViewModel(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Screen(CompatibilityScreenState.EstimatedCompatible);
        });

        Task attempt = viewModel.StartAsync();
        viewModel.Cancel();
        await attempt;

        Assert.AreEqual("Check stopped", viewModel.Presentation.OutcomeTitle);
    }

    [TestMethod]
    public async Task Cancel_RetiresANonCooperativeLateResult()
    {
        TaskCompletionSource<CompatibilityScreenModel> late =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new CompatibilityViewModel(_ => late.Task);

        Task attempt = viewModel.StartAsync();
        viewModel.Cancel();
        late.SetResult(Screen(CompatibilityScreenState.EstimatedCompatible));
        await attempt;

        Assert.AreEqual("Check stopped", viewModel.Presentation.OutcomeTitle);
        Assert.IsFalse(viewModel.Presentation.PrimaryActionEnabled);
        Assert.IsNull(viewModel.SelectedPreference);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task CancelThenNewRun_DoesNotLetTheCancelledResultOverwriteTheNewRun()
    {
        TaskCompletionSource<CompatibilityScreenModel> cancelled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var viewModel = new CompatibilityViewModel(_ =>
            ++calls == 1
                ? cancelled.Task
                : Task.FromResult(Screen(CompatibilityScreenState.NotEstablished)));

        Task retiredAttempt = viewModel.StartAsync();
        viewModel.Cancel();
        await viewModel.StartAsync();
        cancelled.SetResult(Screen(CompatibilityScreenState.EstimatedCompatible));
        await retiredAttempt;

        Assert.AreEqual("We can't answer this yet", viewModel.Presentation.OutcomeTitle);
        Assert.IsFalse(viewModel.Presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    public async Task Cancel_DoesNotDisposeTheTokenSourceWhileItsEvaluatorIsStillReturning()
    {
        TaskCompletionSource started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new CompatibilityViewModel(async token =>
        {
            started.SetResult();
            await release.Task;

            // A late-cleaning adapter is allowed to register cleanup and inspect
            // the token after cancellation. Its attempt owns the source until
            // this delegate has completely returned.
            using CancellationTokenRegistration registration = token.Register(() => { });
            Assert.IsTrue(token.WaitHandle.WaitOne(0));
            return Screen(CompatibilityScreenState.EstimatedCompatible);
        });

        Task attempt = viewModel.StartAsync();
        await started.Task;
        viewModel.Cancel();
        release.SetResult();
        await attempt;

        Assert.AreEqual("Check stopped", viewModel.Presentation.OutcomeTitle);
    }

    [TestMethod]
    public async Task AttemptCancellation_CancelAndOwnerCompletionAreRaceSafe()
    {
        for (int iteration = 0; iteration < 250; iteration++)
        {
            var attempt = new CompatibilityViewModel.AttemptCancellation();
            using var release = new ManualResetEventSlim(false);
            Task cancel = Task.Run(() =>
            {
                release.Wait();
                attempt.Cancel();
            });
            Task complete = Task.Run(() =>
            {
                release.Wait();
                attempt.Complete();
            });

            release.Set();
            await Task.WhenAll(cancel, complete);
        }
    }

    [TestMethod]
    public async Task MissingDestination_StoresAndEmitsTheSameDisabledSnapshot()
    {
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(Screen(CompatibilityScreenState.EstimatedCompatible)),
            continueDestinationAvailable: false);
        CompatibilityPresentation? emitted = null;
        viewModel.PresentationChanged += (_, presentation) => emitted = presentation;

        await viewModel.StartAsync();

        Assert.IsNotNull(emitted);
        Assert.AreSame(viewModel.Presentation, emitted);
        Assert.IsFalse(emitted.PrimaryActionEnabled);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ArbitraryEvaluatorFault_IsReportedSafelyAndStillPropagates()
    {
        var viewModel = new CompatibilityViewModel(_ =>
            Task.FromException<CompatibilityScreenModel>(
                new InvalidOperationException("adapter failed")));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => viewModel.StartAsync());

        Assert.AreEqual(
            "The compatibility check could not finish",
            viewModel.Presentation.OutcomeTitle);
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Retry,
            viewModel.Presentation.SecondaryActionKind);
    }

    [TestMethod]
    public async Task ExperimentalConsent_ReevaluatesWithOnlyTheExactKnownEvidence()
    {
        const string evidenceId = "tbq-evidence-7";
        List<string[]> observedConsent = [];
        var viewModel = new CompatibilityViewModel((consent, _) =>
        {
            observedConsent.Add(consent.Order(StringComparer.Ordinal).ToArray());
            return Task.FromResult(new CompatibilityEvaluation(
                Screen(CompatibilityScreenState.EstimatedCompatible),
                PlanningSession: null,
                CurrentConfiguration: null)
            {
                ExperimentalConsentOptions =
                [
                    CompatibilityExperimentalConsentOption.Create(
                        OptimizationRoute.Gguf,
                        evidenceId)
                ]
            });
        });

        await viewModel.StartAsync();
        bool wrongAccepted = await viewModel.SetExperimentalConsentAsync(
            "other-evidence",
            granted: true);
        bool exactAccepted = await viewModel.SetExperimentalConsentAsync(
            evidenceId,
            granted: true);
        bool revoked = await viewModel.SetExperimentalConsentAsync(
            evidenceId,
            granted: false);

        Assert.IsFalse(wrongAccepted);
        Assert.IsTrue(exactAccepted);
        Assert.IsTrue(revoked);
        Assert.AreEqual(3, observedConsent.Count);
        CollectionAssert.AreEqual(Array.Empty<string>(), observedConsent[0]);
        CollectionAssert.AreEqual(new[] { evidenceId }, observedConsent[1]);
        CollectionAssert.AreEqual(Array.Empty<string>(), observedConsent[2]);
        Assert.IsFalse(viewModel.IsExperimentalConsentGranted);
    }

    [TestMethod]
    public async Task RetiringPageClearsExperimentalConsentForTheNextRun()
    {
        const string evidenceId = "tbq-evidence-7";
        List<string[]> observedConsent = [];
        var viewModel = new CompatibilityViewModel((consent, _) =>
        {
            observedConsent.Add(consent.ToArray());
            return Task.FromResult(new CompatibilityEvaluation(
                Screen(CompatibilityScreenState.EstimatedCompatible),
                PlanningSession: null,
                CurrentConfiguration: null)
            {
                ExperimentalConsentOptions =
                [
                    CompatibilityExperimentalConsentOption.Create(
                        OptimizationRoute.Gguf,
                        evidenceId)
                ]
            });
        });

        await viewModel.StartAsync();
        await viewModel.SetExperimentalConsentAsync(evidenceId, granted: true);
        viewModel.RetireAttempt();
        await viewModel.StartAsync();

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            observedConsent[^1]);
    }

    private static CompatibilityScreenModel Screen(CompatibilityScreenState state) =>
        CompatibilityScreenModel.ForPresentation(
            state,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: state == CompatibilityScreenState.EstimatedCompatible);
}
