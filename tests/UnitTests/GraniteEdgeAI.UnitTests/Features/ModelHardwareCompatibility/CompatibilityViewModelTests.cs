using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
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

    private static CompatibilityScreenModel Screen(CompatibilityScreenState state) =>
        CompatibilityScreenModel.ForPresentation(
            state,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: state == CompatibilityScreenState.EstimatedCompatible);
}
