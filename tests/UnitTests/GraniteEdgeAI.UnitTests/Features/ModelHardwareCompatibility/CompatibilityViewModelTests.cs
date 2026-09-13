using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using InspectionOutcome = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class CompatibilityViewModelTests
{
    [TestMethod]
    [TestCategory("ExactOptionStartGate")]
    public async Task ExactAcknowledgementGuardRejectsFreshStatusDuplicateIdentityAndRevokedConsent()
    {
        var fixture = RealExactFixture();
        CompatibilityEvaluation? current = null;
        var vm = new CompatibilityViewModel((consent, token) => Task.FromResult(current =
            fixture.Authority.Evaluate(fixture.FreshResources, consent, fixture.Now, token)),
            actionAuthority: fixture.Authority, timeProvider: fixture.TimeProvider);
        await vm.StartAsync();
        var choice = vm.Presentation.Optimization!.ExactSafeModes.First(x => x.Mode.IsExperimental);
        vm.SelectExactPreference(choice.CandidateIdentity);
        await vm.SetExperimentalConsentAsync(vm.SelectedExperimentalConsentEvidenceId!, true);
        vm.SetExperimentalFinalConfirmation(true);
        var presentation = vm.Presentation.Optimization!;
        var session = current!.PlanningSession!;
        var guard = typeof(CompatibilityViewModel).GetMethod("TryGetAcknowledgedExactIdentity",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        bool Accepted(CompatibilityOptimizationPresentation value) => (bool)guard.Invoke(vm,
            new object?[] { value, vm.SelectedPreference!, session, null })!;
        Assert.IsTrue(Accepted(presentation), "The unchanged acknowledged identity is valid.");
        Assert.IsFalse(Accepted(presentation with { SelectionStatusText = "Selection is no longer available." }),
            "Fresh exact selection status must fail closed, just like the initial Start guard.");
        Assert.IsFalse(Accepted(presentation with { ExactSafeModes = [choice, choice] }),
            "An ambiguous exact identity must never issue.");
        vm.SetExperimentalFinalConfirmation(false);
        Assert.IsFalse(Accepted(presentation), "Revoked acknowledgement must not survive a retained presentation.");
        Assert.IsFalse(vm.StartOptimizationCommand.CanExecute(null));
    }

    [TestMethod]
    [TestCategory("ExactOptionStartGate")]
    public async Task ConsentedExactExperimentalStartReevaluatesSameIdentityAndUsesNormalIssuer()
    {
        var fixture = RealExactFixture();
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel((consent, token) =>
        {
            evaluations++;
            return Task.FromResult(fixture.Authority.Evaluate(fixture.FreshResources, consent, fixture.Now, token));
        }, actionAuthority: fixture.Authority, timeProvider: fixture.TimeProvider);
        int requests = 0;
        viewModel.OptimizationRequested += (_, _) => requests++;
        await viewModel.StartAsync();
        var experimental = viewModel.Presentation.Optimization!.ExactSafeModes.First(mode => mode.Mode.IsExperimental);
        viewModel.SelectExactPreference(experimental.CandidateIdentity);
        Assert.IsFalse(viewModel.StartOptimizationCommand.CanExecute(null));
        Assert.IsTrue(await viewModel.SetExperimentalConsentAsync(viewModel.SelectedExperimentalConsentEvidenceId!, true));
        Assert.IsFalse(viewModel.StartOptimizationCommand.CanExecute(null));
        viewModel.SetExperimentalFinalConfirmation(true);
        Assert.IsNotNull(viewModel.CurrentOptimizationHandoff,
            "The existing normal issuer must already accept the exact acknowledged candidate.");
        Assert.IsNull(viewModel.Presentation.Optimization!.SafeSliderSelectedIndex,
            "Experimental execution must not be made reachable by relabelling it as a released slider stop.");
        Assert.IsTrue(viewModel.StartOptimizationCommand.CanExecute(null),
            "The actual Start command must support an authoritative fully acknowledged exact candidate.");
        int beforeStart = evaluations;
        await viewModel.StartOptimizationAsync();
        Assert.AreEqual(beforeStart + 1, evaluations, "Start must still refresh real resource/authority facts.");
        Assert.AreEqual(1, requests);
        Assert.AreEqual(experimental.CandidateIdentity,
            viewModel.CurrentOptimizationHandoff!.Plan.Preference.ExactCandidateIdentity);
    }

    [TestMethod]
    public async Task ExpiredSafeSliderStart_RefreshesSameIdentityAndEmitsOnce()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var clock = new MutableTimeProvider(fixture.Now);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (consent, token) =>
            {
                evaluations++;
                return Task.FromResult(fixture.Authority.Evaluate(
                    FreshAt(fixture, clock.GetUtcNow()),
                    consent,
                    clock.GetUtcNow(),
                    token));
            },
            actionAuthority: fixture.Authority,
            timeProvider: clock);
        int optimizationRequests = 0;
        int continueRequests = 0;
        int chatRequests = 0;
        OptimizationRequestedEventArgs? request = null;
        viewModel.OptimizationRequested += (_, args) =>
        {
            optimizationRequests++;
            request = args;
        };
        viewModel.ContinueRequested += (_, _) => continueRequests++;
        viewModel.CurrentModelChatRequested += (_, _) => chatRequests++;

        await viewModel.StartAsync();
        string identity = viewModel.Presentation.Optimization!.SafeSliderModes[3]
            .CandidateIdentity;
        viewModel.SelectExactPreference(identity);
        OptimizationSelectionHandoff original =
            viewModel.CurrentOptimizationHandoff!;
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.IsTrue(viewModel.StartOptimizationCommand.CanExecute(null),
            "A synchronized safe identity remains a revalidation intent.");

        await viewModel.StartOptimizationAsync();

        Assert.AreEqual(2, evaluations);
        Assert.AreEqual(1, optimizationRequests);
        Assert.AreEqual(1, continueRequests);
        Assert.AreEqual(0, chatRequests);
        Assert.IsNotNull(request);
        Assert.AreEqual(identity,
            request.Context.OptimizationHandoff.Plan.Preference.ExactCandidateIdentity);
        Assert.AreNotEqual(original.OptimizationPlanId,
            request.Context.OptimizationHandoff.OptimizationPlanId,
            "Start must never forward the pre-refresh plan.");
        Assert.AreEqual(clock.GetUtcNow(),
            request.Context.OptimizationHandoff.Plan.CreatedAtUtc);
    }

    [TestMethod]
    public async Task FreshSafeSliderStart_StillRefreshesAndDispatchesExactlyOnce()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var clock = new MutableTimeProvider(fixture.Now);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (consent, token) =>
            {
                evaluations++;
                return Task.FromResult(fixture.Authority.Evaluate(
                    FreshAt(fixture, clock.GetUtcNow()), consent,
                    clock.GetUtcNow(), token));
            },
            actionAuthority: fixture.Authority,
            timeProvider: clock);
        int requests = 0;
        viewModel.OptimizationRequested += (_, _) => requests++;

        await viewModel.StartAsync();
        string identity = viewModel.Presentation.Optimization!.SafeSliderModes[^1]
            .CandidateIdentity;
        viewModel.SelectExactPreference(identity);

        await viewModel.StartOptimizationAsync();

        Assert.AreEqual(2, evaluations,
            "Every Start intent refreshes current memory and storage authority.");
        Assert.AreEqual(1, requests);
    }

    [TestMethod]
    public async Task ExpiredSafeSliderStart_MissingIdentityFailsClosedWithoutFallback()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var clock = new MutableTimeProvider(fixture.Now);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (consent, token) =>
            {
                evaluations++;
                ulong available = evaluations == 1
                    ? fixture.FreshResources.AvailableSystemMemoryBytes
                    : 2_800_000_000UL;
                return Task.FromResult(fixture.Authority.Evaluate(
                    FreshAt(fixture, clock.GetUtcNow(), available),
                    consent, clock.GetUtcNow(), token));
            },
            actionAuthority: fixture.Authority,
            timeProvider: clock);
        int requests = 0;
        int chats = 0;
        viewModel.OptimizationRequested += (_, _) => requests++;
        viewModel.CurrentModelChatRequested += (_, _) => chats++;

        await viewModel.StartAsync();
        string identity = viewModel.Presentation.Optimization!.SafeSliderModes[^1]
            .CandidateIdentity;
        clock.Advance(TimeSpan.FromSeconds(31));
        viewModel.SelectExactPreference(identity);

        await viewModel.StartOptimizationAsync();

        Assert.AreEqual(2, evaluations);
        Assert.AreEqual(0, requests);
        Assert.AreEqual(0, chats);
        Assert.AreEqual(OptimizationPreferenceKind.Exact,
            viewModel.SelectedPreference?.Kind);
        Assert.AreEqual(identity,
            viewModel.SelectedPreference?.ExactCandidateIdentity);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.StartOptimizationCommand.CanExecute(null));
        StringAssert.Contains(
            viewModel.Presentation.Optimization?.SelectionStatusText ?? string.Empty,
            "no longer available");
    }

    [TestMethod]
    public async Task ExpiredSafeSliderStart_FrozenIssuerFailsClosedWithoutDispatch()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var clock = new MutableTimeProvider(fixture.Now);
        var frozenAuthority = new FrozenOptimizationAuthority(fixture.Authority);
        var viewModel = new CompatibilityViewModel(
            (consent, token) => Task.FromResult(fixture.Authority.Evaluate(
                FreshAt(fixture, clock.GetUtcNow()), consent,
                clock.GetUtcNow(), token)),
            actionAuthority: frozenAuthority,
            timeProvider: clock);
        int requests = 0;
        viewModel.OptimizationRequested += (_, _) => requests++;

        await viewModel.StartAsync();
        string identity = viewModel.Presentation.Optimization!.SafeSliderModes[0]
            .CandidateIdentity;
        clock.Advance(TimeSpan.FromSeconds(31));
        viewModel.SelectExactPreference(identity);

        await viewModel.StartOptimizationAsync();

        Assert.AreEqual(0, requests);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.StartOptimizationCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ExpiredSafeSliderStart_SuppressesDoubleInvokeAndLateChangedSelection()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var clock = new MutableTimeProvider(fixture.Now);
        var refresh = new TaskCompletionSource<CompatibilityEvaluation>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (_, _) => ++evaluations == 1
                ? Task.FromResult(fixture.Authority.Evaluate(
                    FreshAt(fixture, clock.GetUtcNow()), EmptyConsent(),
                    clock.GetUtcNow(), CancellationToken.None))
                : refresh.Task,
            actionAuthority: fixture.Authority,
            timeProvider: clock);
        int requests = 0;
        viewModel.OptimizationRequested += (_, _) => requests++;

        await viewModel.StartAsync();
        IReadOnlyList<CompatibilityExactOptimizationModePresentation> choices =
            viewModel.Presentation.Optimization!.SafeSliderModes;
        string first = choices[0].CandidateIdentity;
        string second = choices[1].CandidateIdentity;
        clock.Advance(TimeSpan.FromSeconds(31));
        viewModel.SelectExactPreference(first);

        Task pending = viewModel.StartOptimizationAsync();
        Task duplicate = viewModel.StartOptimizationAsync();
        Assert.AreEqual(2, evaluations,
            "A busy Start intent must not begin another refresh.");

        viewModel.SelectExactPreference(second);
        CompatibilityEvaluation late = fixture.Authority.Evaluate(
            FreshAt(fixture, clock.GetUtcNow()), EmptyConsent(),
            clock.GetUtcNow(), CancellationToken.None);
        refresh.SetResult(late);
        await Task.WhenAll(pending, duplicate);

        Assert.AreEqual(0, requests);
        Assert.AreEqual(second,
            viewModel.SelectedPreference?.ExactCandidateIdentity);
    }

    [TestMethod]
    public async Task ExpiredSafeSliderStart_ExplicitCancellationRejectsLateCompletion()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var clock = new MutableTimeProvider(fixture.Now);
        var refresh = new TaskCompletionSource<CompatibilityEvaluation>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (_, _) => ++evaluations == 1
                ? Task.FromResult(fixture.Authority.Evaluate(
                    FreshAt(fixture, clock.GetUtcNow()), EmptyConsent(),
                    clock.GetUtcNow(), CancellationToken.None))
                : refresh.Task,
            actionAuthority: fixture.Authority,
            timeProvider: clock);
        int requests = 0;
        viewModel.OptimizationRequested += (_, _) => requests++;

        await viewModel.StartAsync();
        string identity = viewModel.Presentation.Optimization!.SafeSliderModes[0]
            .CandidateIdentity;
        clock.Advance(TimeSpan.FromSeconds(31));
        viewModel.SelectExactPreference(identity);
        Task pending = viewModel.StartOptimizationAsync();

        viewModel.CancelOptimizationStartIntent();
        refresh.SetResult(fixture.Authority.Evaluate(
            FreshAt(fixture, clock.GetUtcNow()), EmptyConsent(),
            clock.GetUtcNow(), CancellationToken.None));
        await pending;

        Assert.AreEqual(0, requests);
    }

    [TestMethod]
    public async Task SafeSliderStart_RetiredAttemptRejectsLateCompletion()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var refresh = new TaskCompletionSource<CompatibilityEvaluation>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (_, _) => ++evaluations == 1
                ? Task.FromResult(fixture.Evaluation)
                : refresh.Task,
            actionAuthority: fixture.Authority,
            timeProvider: fixture.TimeProvider);
        int requests = 0;
        viewModel.OptimizationRequested += (_, _) => requests++;

        await viewModel.StartAsync();
        string identity = viewModel.Presentation.Optimization!.SafeSliderModes[0]
            .CandidateIdentity;
        viewModel.SelectExactPreference(identity);
        Task pending = viewModel.StartOptimizationAsync();

        viewModel.RetireAttempt();
        refresh.SetResult(fixture.Evaluation);
        await pending;

        Assert.AreEqual(0, requests);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
    }

    [TestMethod]
    public async Task SafeSliderStart_FailedRefreshClearsIndependentChatHandoff()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        CompatibilitySetupView current = fixture.Evaluation.Screen.CurrentSetup
            ?? throw new AssertFailedException("The real fixture needs a current setup.");
        CompatibilityEvaluation optionalEvaluation = new(
            AuthoritativeEstimatedCompatibleScreen(current),
            fixture.Evaluation.PlanningSession,
            CurrentConfiguration: null)
        {
            OptionalOptimization = fixture.Evaluation.Screen.Optimization,
            MachineMemory = fixture.Evaluation.MachineMemory
        };
        CompatibilityEvaluation missingFreshAuthority = new(
            AuthoritativeEstimatedCompatibleScreen(current),
            PlanningSession: null,
            CurrentConfiguration: null);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(++evaluations == 1
                ? optionalEvaluation
                : missingFreshAuthority),
            actionAuthority: new OptimizationAndChatAuthority(fixture.Authority),
            currentModelHandoffResolver: _ => CurrentHandoff(),
            timeProvider: fixture.TimeProvider);
        int chatRequests = 0;
        viewModel.CurrentModelChatRequested += (_, _) => chatRequests++;

        await viewModel.StartAsync();
        Assert.IsTrue(viewModel.CanChatWithCurrentModel);
        viewModel.BeginOptionalOptimization();
        string identity = viewModel.Presentation.Optimization!.SafeSliderModes[0]
            .CandidateIdentity;
        viewModel.SelectExactPreference(identity);

        await viewModel.StartOptimizationAsync();

        Assert.IsFalse(viewModel.CanChatWithCurrentModel,
            "A failed refresh must not leave the retained Stage 3 chat handoff usable.");
        viewModel.ChatCurrentModelCommand.Execute(null);
        Assert.AreEqual(0, chatRequests);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
    }

    [TestMethod]
    public async Task SafeSliderStart_RequiresApplicationExecutionAuthority()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(fixture.Evaluation),
            timeProvider: fixture.TimeProvider);

        await viewModel.StartAsync();
        string identity = viewModel.Presentation.Optimization!.SafeSliderModes[0]
            .CandidateIdentity;
        viewModel.SelectExactPreference(identity);

        Assert.IsFalse(viewModel.StartOptimizationCommand.CanExecute(null));
        await viewModel.StartOptimizationAsync();
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
    }

    [TestMethod]
    public async Task SafeSliderStart_MismatchedFreshOptionalContextFailsBeforeDispatch()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        CompatibilitySetupView current = fixture.Evaluation.Screen.CurrentSetup
            ?? throw new AssertFailedException("The real fixture needs a current setup.");
        CompatibilityEvaluation optionalEvaluation = new(
            AuthoritativeEstimatedCompatibleScreen(current),
            fixture.Evaluation.PlanningSession,
            CurrentConfiguration: null)
        {
            OptionalOptimization = fixture.Evaluation.Screen.Optimization,
            MachineMemory = fixture.Evaluation.MachineMemory
        };
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(optionalEvaluation),
            actionAuthority: new OptimizationAndChatAuthority(fixture.Authority),
            currentModelHandoffResolver: _ => CurrentHandoff(),
            timeProvider: fixture.TimeProvider);
        int optimizationRequests = 0;
        int continueRequests = 0;
        int chatRequests = 0;
        viewModel.OptimizationRequested += (_, _) => optimizationRequests++;
        viewModel.ContinueRequested += (_, _) => continueRequests++;
        viewModel.CurrentModelChatRequested += (_, _) => chatRequests++;

        await viewModel.StartAsync();
        viewModel.BeginOptionalOptimization();
        string identity = viewModel.Presentation.Optimization!.SafeSliderModes[0]
            .CandidateIdentity;
        viewModel.SelectExactPreference(identity);

        await viewModel.StartOptimizationAsync();

        Assert.AreEqual(0, optimizationRequests);
        Assert.AreEqual(0, continueRequests);
        Assert.AreEqual(0, chatRequests);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.CanChatWithCurrentModel);
        Assert.IsFalse(viewModel.StartOptimizationCommand.CanExecute(null));
        StringAssert.Contains(
            viewModel.Presentation.Optimization?.SelectionStatusText ?? string.Empty,
            "no longer available");
    }

    [TestMethod]
    public async Task SafeSliderSelection_UsesSourceBackedIdentityAndStartEmitsOnlyThatPlan()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(fixture.Evaluation),
            actionAuthority: fixture.Authority,
            timeProvider: fixture.TimeProvider);
        int optimizationRequests = 0;
        int continueRequests = 0;
        int chatRequests = 0;
        OptimizationRequestedEventArgs? request = null;
        viewModel.OptimizationRequested += (_, args) =>
        {
            optimizationRequests++;
            request = args;
        };
        viewModel.ContinueRequested += (_, _) => continueRequests++;
        viewModel.CurrentModelChatRequested += (_, _) => chatRequests++;

        await viewModel.StartAsync();
        Assert.IsNotNull(viewModel.Presentation.Optimization);
        CompatibilityOptimizationPresentation optimization =
            viewModel.Presentation.Optimization;
        Assert.IsTrue(optimization.SafeSliderModes.Count > 1,
            "The real source-backed fixture must exercise more than one safe slider stop.");
        CompatibilityExactOptimizationModePresentation exact =
            optimization.SafeSliderModes[^1];

        viewModel.SelectExactPreference(exact.CandidateIdentity);

        Assert.AreEqual(OptimizationPreferenceKind.Exact,
            viewModel.SelectedPreference?.Kind);
        Assert.AreEqual(exact.CandidateIdentity,
            viewModel.SelectedPreference?.ExactCandidateIdentity);
        Assert.AreEqual(exact.Mode, viewModel.Presentation.Optimization?.SelectedMode);
        Assert.AreEqual(optimization.SafeSliderModes.Count - 1,
            viewModel.Presentation.Optimization?.SafeSliderSelectedIndex);
        Assert.IsTrue(viewModel.ContinueCommand.CanExecute(null));
        Assert.AreEqual(0, optimizationRequests);
        Assert.AreEqual(0, continueRequests);
        Assert.AreEqual(0, chatRequests);

        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(1, optimizationRequests);
        Assert.AreEqual(1, continueRequests);
        Assert.AreEqual(0, chatRequests);
        Assert.IsNotNull(request);
        Assert.AreEqual(exact.CandidateIdentity,
            request.Context.OptimizationHandoff.Plan.Preference.ExactCandidateIdentity);
    }

    [TestMethod]
    public async Task RecommendedAndExactModes_RetainIndependentSelectionsWithoutLaunching()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(fixture.Evaluation),
            actionAuthority: fixture.Authority,
            timeProvider: fixture.TimeProvider);
        int requests = 0;
        viewModel.OptimizationRequested += (_, _) => requests++;
        await viewModel.StartAsync();

        viewModel.SelectManualPreference(10);
        CompatibilityExactOptimizationModePresentation exact = viewModel.Presentation
            .Optimization!.ExactSafeModes.Last(mode => !mode.Mode.IsExperimental);
        viewModel.SelectExactPreference(exact.CandidateIdentity);
        viewModel.SelectRecommendedPreference();

        Assert.AreEqual(OptimizationPreferenceKind.Manual,
            viewModel.SelectedPreference?.Kind);
        Assert.AreEqual(10, viewModel.SelectedPreference?.PreferenceValue);

        viewModel.SelectRetainedExactPreference();

        Assert.AreEqual(OptimizationPreferenceKind.Exact,
            viewModel.SelectedPreference?.Kind);
        Assert.AreEqual(exact.CandidateIdentity,
            viewModel.SelectedPreference?.ExactCandidateIdentity);
        Assert.AreEqual(0, requests);
    }

    [TestMethod]
    public async Task ExperimentalExactSelection_RequiresFreshExactConsentAndSeparateFinalConfirmation()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var seenConsent = new List<string[]>();
        var viewModel = new CompatibilityViewModel(
            (consent, token) =>
            {
                seenConsent.Add(consent.OrderBy(value => value).ToArray());
                return Task.FromResult(fixture.Authority.Evaluate(
                    fixture.FreshResources,
                    consent,
                    fixture.Now,
                    token));
            },
            actionAuthority: fixture.Authority,
            timeProvider: fixture.TimeProvider);
        await viewModel.StartAsync();
        CompatibilityExactOptimizationModePresentation experimental = viewModel
            .Presentation.Optimization!.ExactSafeModes.First(mode =>
                mode.Mode.IsExperimental);

        viewModel.SelectExactPreference(experimental.CandidateIdentity);
        string evidenceId = viewModel.SelectedExperimentalConsentEvidenceId
            ?? throw new AssertFailedException(
                "The source-backed experimental exact setup needs evidence consent.");

        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        Assert.IsTrue(await viewModel.SetExperimentalConsentAsync(
            evidenceId,
            granted: true));
        Assert.AreEqual(experimental.CandidateIdentity,
            viewModel.SelectedPreference?.ExactCandidateIdentity);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        CollectionAssert.AreEqual(new[] { evidenceId }, seenConsent.Last());

        viewModel.SetExperimentalFinalConfirmation(confirmed: true);

        Assert.IsNotNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsTrue(viewModel.ContinueCommand.CanExecute(null));
        Assert.AreEqual(experimental.CandidateIdentity,
            viewModel.CurrentOptimizationHandoff.Plan.Preference.ExactCandidateIdentity);
    }

    [TestMethod]
    public async Task MissingExactIdentity_FailsClosedWithoutFallingBackToAutomatic()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(fixture.Evaluation),
            actionAuthority: fixture.Authority,
            timeProvider: fixture.TimeProvider);
        await viewModel.StartAsync();
        CompatibilityExactOptimizationModePresentation released = viewModel
            .Presentation.Optimization!.ExactSafeModes.First(mode =>
                !mode.Mode.IsExperimental);
        viewModel.SelectExactPreference(released.CandidateIdentity);
        Assert.IsNotNull(viewModel.CurrentOptimizationHandoff);

        viewModel.SelectExactPreference(new string('f', 64));

        Assert.AreEqual(OptimizationPreferenceKind.Exact,
            viewModel.SelectedPreference?.Kind,
            "A missing exact identity must not silently become Automatic.");
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        Assert.IsFalse(viewModel.IsExperimentalFinalConfirmationGranted);
        Assert.AreEqual(
            "That exact setup is no longer available. Choose another setup to continue.",
            viewModel.Presentation.Optimization?.SelectionStatusText);
    }

    [TestMethod]
    public async Task MissingExactIdentity_WithValidChatAuthority_DoesNotFallBackToChat()
    {
        RealExactPlanningFixture fixture = RealExactFixture();
        CompatibilitySetupView current = fixture.Evaluation.Screen.CurrentSetup
            ?? throw new AssertFailedException("The real fixture needs a current setup.");
        CompatibilityEvaluation optionalEvaluation = new(
            AuthoritativeEstimatedCompatibleScreen(current),
            fixture.Evaluation.PlanningSession,
            CurrentConfiguration: null)
        {
            OptionalOptimization = fixture.Evaluation.Screen.Optimization,
            ExperimentalConsentOptions = fixture.Evaluation.ExperimentalConsentOptions,
            MachineMemory = fixture.Evaluation.MachineMemory
        };
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(optionalEvaluation),
            actionAuthority: new OptimizationAndChatAuthority(fixture.Authority),
            currentModelHandoffResolver: _ => CurrentHandoff(),
            timeProvider: fixture.TimeProvider);
        int chatRequests = 0;
        int optimizationRequests = 0;
        viewModel.CurrentModelChatRequested += (_, _) => chatRequests++;
        viewModel.OptimizationRequested += (_, _) => optimizationRequests++;

        await viewModel.StartAsync();
        Assert.IsTrue(viewModel.CanChatWithCurrentModel);
        viewModel.BeginOptionalOptimization();
        CompatibilityExactOptimizationModePresentation released = viewModel
            .Presentation.Optimization!.ExactSafeModes.First(mode =>
                mode.Availability ==
                    CompatibilityExactOptimizationAvailability.Released);
        viewModel.SelectExactPreference(released.CandidateIdentity);

        viewModel.SelectExactPreference(new string('e', 64));
        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(OptimizationPreferenceKind.Exact,
            viewModel.SelectedPreference?.Kind);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        Assert.AreEqual(0, optimizationRequests);
        Assert.AreEqual(0, chatRequests,
            "A stale exact Start action must not fall through to independent Chat.");
    }
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

            // a late-cleaning adapter is allowed to register cleanup and inspect
            // the token after cancellation. its attempt owns the source until
            // this delegate has completely returned
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
    public async Task ExperimentalConsent_RejectsKnownEvidenceWhenNoCandidateIsSelected()
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
        bool unselectedAccepted = await viewModel.SetExperimentalConsentAsync(
            evidenceId,
            granted: true);
        bool revoked = await viewModel.SetExperimentalConsentAsync(
            evidenceId,
            granted: false);

        Assert.IsFalse(wrongAccepted);
        Assert.IsFalse(unselectedAccepted);
        Assert.IsFalse(revoked);
        Assert.AreEqual(1, observedConsent.Count);
        CollectionAssert.AreEqual(Array.Empty<string>(), observedConsent[0]);
        Assert.IsFalse(viewModel.IsExperimentalConsentGranted);
    }

    [TestMethod]
    public async Task MultipleExperimentalOptions_DoNotGuessAConsentIdentity()
    {
        var viewModel = new CompatibilityViewModel((_, _) =>
            Task.FromResult(new CompatibilityEvaluation(
                Screen(CompatibilityScreenState.EstimatedCompatible),
                PlanningSession: null,
                CurrentConfiguration: null)
            {
                ExperimentalConsentOptions =
                [
                    CompatibilityExperimentalConsentOption.Create(
                        OptimizationRoute.Gguf,
                        "tbq-three-bit"),
                    CompatibilityExperimentalConsentOption.Create(
                        OptimizationRoute.Gguf,
                        "tbq-four-bit")
                ]
            }));

        await viewModel.StartAsync();

        Assert.IsNull(viewModel.SelectedExperimentalConsentEvidenceId,
            "Consent must bind to the selected candidate, never an arbitrary " +
            "member of an available-evidence union.");
        Assert.IsFalse(viewModel.IsExperimentalConsentGranted);
    }

    [TestMethod]
    public async Task RetiringPageClearsExperimentalConsentForTheNextRun()
    {
        ExperimentalPlanningFixture fixture = ExperimentalFixture();
        List<string[]> observedConsent = [];
        var viewModel = new CompatibilityViewModel(
            (consent, _) =>
            {
                observedConsent.Add(
                    consent.Order(StringComparer.Ordinal).ToArray());
                return Task.FromResult(fixture.Evaluation);
            },
            actionAuthority: new DualOpenVinoAuthority(
                fixture.Composer,
                fixture.IssuanceAuthority),
            currentModelHandoffResolver: _ => CurrentHandoff(fixture.Binding),
            timeProvider: new FixedTimeProvider(fixture.Now));

        await viewModel.StartAsync();
        Assert.IsTrue(await viewModel.SetExperimentalConsentAsync(
            fixture.HighEvidenceId,
            granted: true));
        viewModel.RetireAttempt();
        await viewModel.StartAsync();

        Assert.AreEqual(3, observedConsent.Count);
        CollectionAssert.AreEqual(Array.Empty<string>(), observedConsent[0]);
        CollectionAssert.AreEqual(
            new[] { fixture.HighEvidenceId },
            observedConsent[1]);
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            observedConsent[^1]);
    }

    [TestMethod]
    public async Task CurrentModelChatCommand_UsesCurrentHandoffWithoutRequestingOptimisation()
    {
        CurrentModelLaunchHandoff expected = CurrentHandoff();
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: true,
            continueEnabled: true);
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(new CompatibilityEvaluation(
                screen,
                PlanningSession: null,
                CurrentConfiguration: null)),
            actionAuthority: new ChatOnlyAuthority(),
            currentModelHandoffResolver: _ => expected);
        CurrentModelLaunchHandoff? observed = null;
        int optimizationRequests = 0;
        viewModel.CurrentModelChatRequested += (_, args) => observed = args.Handoff;
        viewModel.OptimizationRequested += (_, _) => optimizationRequests++;

        await viewModel.StartAsync();
        viewModel.ChatCurrentModelCommand.Execute(null);

        Assert.AreSame(expected, observed);
        Assert.AreEqual(0, optimizationRequests);
    }

    [TestMethod]
    public async Task ExperimentalOptionalSetup_RequiresExactConsentAndFinalConfirmation_WhileChatRemainsIndependent()
    {
        ExperimentalPlanningFixture fixture = ExperimentalFixture();
        List<string[]> observedConsent = [];
        var authority = new DualOpenVinoAuthority(
            fixture.Composer,
            fixture.IssuanceAuthority);
        CurrentModelLaunchHandoff current = CurrentHandoff(fixture.Binding);
        var viewModel = new CompatibilityViewModel(
            (consent, _) =>
            {
                observedConsent.Add(
                    consent.Order(StringComparer.Ordinal).ToArray());
                return Task.FromResult(fixture.Evaluation);
            },
            actionAuthority: authority,
            currentModelHandoffResolver: _ => current,
            timeProvider: new FixedTimeProvider(fixture.Now));
        OptimizationJourneyEntryContext? optimization = null;
        CurrentModelLaunchHandoff? chat = null;
        int optimizationRequests = 0;
        int chatRequests = 0;
        viewModel.OptimizationRequested += (_, args) =>
        {
            optimizationRequests++;
            optimization = args.Context;
        };
        viewModel.CurrentModelChatRequested += (_, args) =>
        {
            chatRequests++;
            chat = args.Handoff;
        };

        await viewModel.StartAsync();

        Assert.IsTrue(viewModel.CanChatWithCurrentModel);
        Assert.IsTrue(viewModel.RequiresExperimentalConfirmation);
        Assert.AreEqual(fixture.HighEvidenceId,
            viewModel.SelectedExperimentalConsentEvidenceId);
        Assert.IsFalse(viewModel.IsExperimentalConsentGranted);
        Assert.IsFalse(viewModel.IsExperimentalFinalConfirmationGranted);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));

        Assert.IsFalse(await viewModel.SetExperimentalConsentAsync(
            "wrong-evidence", granted: true));
        Assert.IsTrue(await viewModel.SetExperimentalConsentAsync(
            fixture.HighEvidenceId, granted: true));
        Assert.AreEqual(2, observedConsent.Count);
        CollectionAssert.AreEqual(Array.Empty<string>(), observedConsent[0]);
        CollectionAssert.AreEqual(
            new[] { fixture.HighEvidenceId },
            observedConsent[1]);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));

        viewModel.SetExperimentalFinalConfirmation(confirmed: true);
        Assert.IsTrue(viewModel.IsExperimentalFinalConfirmationGranted);
        Assert.IsTrue(viewModel.ContinueCommand.CanExecute(null));
        OptimizationSelectionHandoff? expectedPlan =
            viewModel.CurrentOptimizationHandoff;
        Assert.IsNotNull(expectedPlan);

        Assert.IsTrue(await viewModel.SetExperimentalConsentAsync(
            fixture.HighEvidenceId, granted: true));
        Assert.IsTrue(viewModel.IsExperimentalFinalConfirmationGranted,
            "Repeating unchanged exact consent is an idempotent no-op.");
        Assert.AreSame(expectedPlan, viewModel.CurrentOptimizationHandoff);
        Assert.IsTrue(viewModel.ContinueCommand.CanExecute(null));

        viewModel.ChatCurrentModelCommand.Execute(null);

        Assert.AreSame(current, chat);
        Assert.AreEqual(1, chatRequests);
        Assert.AreEqual(0, optimizationRequests);

        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(1, chatRequests);
        Assert.AreEqual(1, optimizationRequests);
        Assert.IsNotNull(optimization);
        Assert.AreSame(expectedPlan, optimization.OptimizationHandoff);
        Assert.AreEqual(fixture.HighEvidenceId,
            optimization.OptimizationHandoff.Plan.Candidate.EvidenceId);
    }

    [TestMethod]
    public async Task ManualExperimentalConsent_ReevaluatesWithoutResettingTheSelectedCandidateOrPresentation()
    {
        ExperimentalPlanningFixture initial = ExperimentalFixture(
            "ov-experimental-capability-initial");
        ExperimentalPlanningFixture fresh = ExperimentalFixture(
            "ov-experimental-capability-consented");
        List<string[]> observedConsent = [];
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (consent, _) =>
            {
                observedConsent.Add(
                    consent.Order(StringComparer.Ordinal).ToArray());
                evaluations++;
                return Task.FromResult(
                    evaluations == 1 ? initial.Evaluation : fresh.Evaluation);
            },
            actionAuthority: new DualOpenVinoAuthority(
                fresh.Composer,
                fresh.IssuanceAuthority),
            currentModelHandoffResolver: _ => CurrentHandoff(fresh.Binding),
            timeProvider: new FixedTimeProvider(fresh.Now));

        await viewModel.StartAsync();
        viewModel.SelectManualPreference(10);
        OptimizationPreferenceSelection selectedPreference =
            viewModel.SelectedPreference
            ?? throw new AssertFailedException(
                "The manual preference was not retained.");
        CompatibilityOptimizationModePresentation selectedMode =
            viewModel.Presentation.Optimization?.SelectedMode
            ?? throw new AssertFailedException(
                "The selected optimization mode was not projected.");
        List<CompatibilityPresentation> consentPresentations = [];
        viewModel.PresentationChanged += (_, presentation) =>
            consentPresentations.Add(presentation);

        Assert.AreEqual(OptimizationPreferenceKind.Manual,
            selectedPreference.Kind);
        Assert.AreEqual(10, selectedPreference.PreferenceValue);
        Assert.AreEqual(initial.LowEvidenceId,
            viewModel.SelectedExperimentalConsentEvidenceId);
        Assert.IsFalse(await viewModel.SetExperimentalConsentAsync(
            initial.HighEvidenceId,
            granted: true),
            "Known evidence for a different candidate must be rejected.");
        Assert.AreEqual(1, evaluations,
            "Wrong or stale evidence must not invoke the evaluator.");

        Assert.IsTrue(await viewModel.SetExperimentalConsentAsync(
            initial.LowEvidenceId,
            granted: true));

        Assert.AreEqual(2, evaluations);
        CollectionAssert.AreEqual(Array.Empty<string>(), observedConsent[0]);
        CollectionAssert.AreEqual(
            new[] { initial.LowEvidenceId },
            observedConsent[1]);
        Assert.IsFalse(consentPresentations.Any(presentation =>
                string.Equals(
                    presentation.OutcomeBadge,
                    "WORKING",
                    StringComparison.Ordinal)),
            "Consent re-evaluation must keep Configure visible instead of " +
            "publishing the analysis state.");
        Assert.AreEqual(selectedPreference, viewModel.SelectedPreference,
            "Consent must preserve the exact manual slider value.");
        Assert.AreEqual(selectedMode,
            viewModel.Presentation.Optimization?.SelectedMode,
            "The same authoritative candidate must remain selected.");
        Assert.AreEqual(fresh.LowEvidenceId,
            viewModel.SelectedExperimentalConsentEvidenceId);
        Assert.IsTrue(viewModel.IsExperimentalConsentGranted);
        Assert.IsFalse(viewModel.IsExperimentalFinalConfirmationGranted);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff,
            "Evidence consent alone must not issue an optimization handoff.");
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null),
            "Fresh evidence consent still requires separate final confirmation.");
        viewModel.SetExperimentalFinalConfirmation(confirmed: true);
        OptimizationSelectionHandoff handoff =
            viewModel.CurrentOptimizationHandoff
            ?? throw new AssertFailedException(
                "Final confirmation should issue the selected candidate from " +
                "the freshly consented session.");
        Assert.IsTrue(viewModel.IsExperimentalFinalConfirmationGranted);
        Assert.IsTrue(viewModel.ContinueCommand.CanExecute(null));
        CompatibilityPlanningSession freshSession =
            fresh.Evaluation.PlanningSession
            ?? throw new AssertFailedException(
                "The consented evaluation did not retain a planning session.");
        CompatibilityPlanningSession initialSession =
            initial.Evaluation.PlanningSession
            ?? throw new AssertFailedException(
                "The initial evaluation did not retain a planning session.");
        Assert.IsTrue(freshSession.MatchesIssuedPlan(
            handoff.Plan,
            selectedPreference),
            "The handoff must come from the freshly consented session.");
        Assert.IsFalse(initialSession.MatchesIssuedPlan(
            handoff.Plan,
            selectedPreference),
            "The pre-consent planning session must not be reused.");
    }

    [TestMethod]
    public async Task ManualExperimentalConsent_FailsClosedWhenTheFreshEvaluationDropsTheSelectedEvidence()
    {
        ExperimentalPlanningFixture fixture = ExperimentalFixture();
        CompatibilityEvaluation droppedSelection = fixture.Evaluation with
        {
            ExperimentalConsentOptions =
            [
                CompatibilityExperimentalConsentOption.Create(
                    OptimizationRoute.OpenVino,
                    fixture.HighEvidenceId)
            ]
        };
        List<string[]> observedConsent = [];
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (consent, _) =>
            {
                observedConsent.Add(
                    consent.Order(StringComparer.Ordinal).ToArray());
                evaluations++;
                return Task.FromResult(
                    evaluations == 1 ? fixture.Evaluation : droppedSelection);
            },
            actionAuthority: new DualOpenVinoAuthority(
                fixture.Composer,
                fixture.IssuanceAuthority),
            currentModelHandoffResolver: _ => CurrentHandoff(fixture.Binding),
            timeProvider: new FixedTimeProvider(fixture.Now));
        int optimizationRequests = 0;
        viewModel.OptimizationRequested += (_, _) => optimizationRequests++;

        await viewModel.StartAsync();
        viewModel.SelectManualPreference(10);
        OptimizationPreferenceSelection selectedPreference =
            viewModel.SelectedPreference
            ?? throw new AssertFailedException(
                "The manual preference was not retained.");
        CompatibilityOptimizationModePresentation selectedMode =
            viewModel.Presentation.Optimization?.SelectedMode
            ?? throw new AssertFailedException(
                "The selected optimization mode was not projected.");

        Assert.IsFalse(await viewModel.SetExperimentalConsentAsync(
            fixture.LowEvidenceId,
            granted: true));

        Assert.AreEqual(2, evaluations);
        CollectionAssert.AreEqual(
            new[] { fixture.LowEvidenceId },
            observedConsent[1]);
        Assert.AreEqual(selectedPreference, viewModel.SelectedPreference,
            "Failing closed must not substitute Automatic for the user's choice.");
        Assert.AreEqual(selectedMode,
            viewModel.Presentation.Optimization?.SelectedMode);
        Assert.IsFalse(viewModel.IsExperimentalConsentGranted);
        Assert.IsFalse(viewModel.IsExperimentalFinalConfirmationGranted);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        Assert.AreEqual(
            "That experimental preview could not be confirmed. Choose another setup or try again.",
            viewModel.Presentation.Optimization?.SelectionStatusText);
        viewModel.SetExperimentalFinalConfirmation(confirmed: true);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        viewModel.ContinueCommand.Execute(null);
        Assert.AreEqual(0, optimizationRequests);
    }

    [TestMethod]
    public async Task ManualSelection_InvalidatesAnInFlightConsentReevaluation()
    {
        ExperimentalPlanningFixture initial = ExperimentalFixture(
            "ov-experimental-capability-race-initial");
        ExperimentalPlanningFixture fresh = ExperimentalFixture(
            "ov-experimental-capability-race-consented");
        var consentEvaluationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConsentEvaluation =
            new TaskCompletionSource<CompatibilityEvaluation>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            (_, token) =>
            {
                evaluations++;
                if (evaluations == 1)
                {
                    return Task.FromResult(initial.Evaluation);
                }

                consentEvaluationStarted.TrySetResult();
                return releaseConsentEvaluation.Task.WaitAsync(token);
            },
            actionAuthority: new DualOpenVinoAuthority(
                fresh.Composer,
                fresh.IssuanceAuthority),
            currentModelHandoffResolver: _ => CurrentHandoff(fresh.Binding),
            timeProvider: new FixedTimeProvider(fresh.Now));
        int optimizationRequests = 0;
        viewModel.OptimizationRequested += (_, _) => optimizationRequests++;

        await viewModel.StartAsync();
        viewModel.SelectManualPreference(10);
        Task<bool> consent = viewModel.SetExperimentalConsentAsync(
            initial.LowEvidenceId,
            granted: true);
        await consentEvaluationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.SelectManualPreference(90);
        OptimizationPreferenceSelection selectedPreference =
            viewModel.SelectedPreference
            ?? throw new AssertFailedException(
                "The newer manual preference was not retained.");
        CompatibilityOptimizationModePresentation selectedMode =
            viewModel.Presentation.Optimization?.SelectedMode
            ?? throw new AssertFailedException(
                "The newer optimization mode was not projected.");

        releaseConsentEvaluation.TrySetResult(fresh.Evaluation);

        Assert.IsFalse(await consent,
            "A consent evaluation for the superseded candidate must be stale.");
        Assert.AreEqual(OptimizationPreferenceKind.Manual,
            selectedPreference.Kind);
        Assert.AreEqual(90, selectedPreference.PreferenceValue);
        Assert.AreEqual(selectedPreference, viewModel.SelectedPreference);
        Assert.AreEqual(selectedMode,
            viewModel.Presentation.Optimization?.SelectedMode);
        Assert.AreEqual(initial.HighEvidenceId,
            viewModel.SelectedExperimentalConsentEvidenceId);
        Assert.IsFalse(viewModel.IsExperimentalConsentGranted,
            "The newer candidate must not inherit the stale candidate's consent.");
        Assert.IsFalse(viewModel.IsExperimentalFinalConfirmationGranted);
        Assert.IsNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        viewModel.ContinueCommand.Execute(null);
        Assert.AreEqual(0, optimizationRequests);
    }

    [TestMethod]
    public async Task ExperimentalSelectionChange_ClearsFinalConfirmation_AndRejectsStaleConsentIdentity()
    {
        ExperimentalPlanningFixture fixture = ExperimentalFixture();
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(fixture.Evaluation),
            actionAuthority: new DualOpenVinoAuthority(
                fixture.Composer,
                fixture.IssuanceAuthority),
            currentModelHandoffResolver: _ => CurrentHandoff(fixture.Binding),
            timeProvider: new FixedTimeProvider(fixture.Now));

        await viewModel.StartAsync();
        Assert.IsTrue(await viewModel.SetExperimentalConsentAsync(
            fixture.HighEvidenceId, granted: true));
        viewModel.SetExperimentalFinalConfirmation(confirmed: true);
        Assert.IsTrue(viewModel.ContinueCommand.CanExecute(null));

        viewModel.SelectManualPreference(10);

        Assert.AreEqual(fixture.LowEvidenceId,
            viewModel.SelectedExperimentalConsentEvidenceId);
        Assert.IsFalse(viewModel.IsExperimentalConsentGranted);
        Assert.IsFalse(viewModel.IsExperimentalFinalConfirmationGranted);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        Assert.IsFalse(await viewModel.SetExperimentalConsentAsync(
            fixture.HighEvidenceId, granted: true),
            "Consent for the previously selected plan must be rejected as stale.");
        viewModel.SetExperimentalFinalConfirmation(confirmed: true);
        Assert.IsFalse(viewModel.IsExperimentalFinalConfirmationGranted);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task StaleOptionalOptimizationHandoff_CannotTurnStartIntoChat()
    {
        ExperimentalPlanningFixture fixture = ExperimentalFixture();
        CurrentModelLaunchHandoff current = CurrentHandoff(
            fixture.Binding,
            useMismatchedJourney: true);
        var viewModel = new CompatibilityViewModel(
            (_, _) => Task.FromResult(fixture.Evaluation),
            actionAuthority: new DualOpenVinoAuthority(
                fixture.Composer,
                fixture.IssuanceAuthority),
            currentModelHandoffResolver: _ => current,
            timeProvider: new FixedTimeProvider(fixture.Now));
        int chatRequests = 0;
        int optimizationRequests = 0;
        viewModel.CurrentModelChatRequested += (_, _) => chatRequests++;
        viewModel.OptimizationRequested += (_, _) => optimizationRequests++;

        await viewModel.StartAsync();
        Assert.IsTrue(await viewModel.SetExperimentalConsentAsync(
            fixture.HighEvidenceId,
            granted: true));
        viewModel.SetExperimentalFinalConfirmation(confirmed: true);

        Assert.IsNotNull(viewModel.CurrentOptimizationHandoff,
            "The test requires a real issued plan whose fallback journey is stale.");
        Assert.IsTrue(viewModel.CanChatWithCurrentModel,
            "The independent Chat action remains available for the current model.");
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null),
            "Start must fail closed when the plan and current fallback journeys differ.");
        viewModel.ContinueCommand.Execute(null);
        Assert.AreEqual(0, optimizationRequests);
        Assert.AreEqual(0, chatRequests,
            "Start optimisation must never fall through to current-model chat.");
        viewModel.ChatCurrentModelCommand.Execute(null);
        Assert.AreEqual(1, chatRequests);
    }

    internal static ExperimentalPlanningFixture ExperimentalFixture(
        string capabilitySnapshotId = "ov-experimental-capability")
    {
        const ulong gib = 1024UL * 1024 * 1024;
        const string digest =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string lowEvidenceId = "ov-experimental-efficient";
        const string highEvidenceId = "ov-experimental-capable";
        DateTimeOffset now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        OpenVinoBuildIdentity build = OpenVinoBuildIdentity.Create(
            "2026.3.0", "2026.3.0", "2026.3.0", digest);
        IReadOnlyDictionary<string, string> versions =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openvino"] = "2026.3.0"
            };
        OpenVinoAdmittedConfiguration lowAdmission = OpenVinoAdmittedConfiguration.Create(
            lowEvidenceId,
            DeviceRouteId.Cpu,
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.U8,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled,
            1,
            512,
            8192,
            SupportLevel.Experimental,
            requiresEvidence: true);
        OpenVinoAdmittedConfiguration highAdmission = OpenVinoAdmittedConfiguration.Create(
            highEvidenceId,
            DeviceRouteId.Cpu,
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.F16,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled,
            1,
            512,
            8192,
            SupportLevel.Experimental,
            requiresEvidence: true);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                capabilitySnapshotId,
                digest,
                OpenVinoCapabilityPayload.Create(
                    "2026.3.0",
                    [lowAdmission, highAdmission],
                    [
                        OpenVinoExecutionAuthority.Create(
                            lowEvidenceId,
                            "ov-experimental-efficient-config",
                            OpenVinoWeightPrecision.Fp16,
                            build,
                            versions,
                            compiledCacheIsDisposable: true),
                        OpenVinoExecutionAuthority.Create(
                            highEvidenceId,
                            "ov-experimental-capable-config",
                            OpenVinoWeightPrecision.Fp16,
                            build,
                            versions,
                            compiledCacheIsDisposable: true)
                    ]));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat",
            512,
            OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "11111111111141118111111111111111",
            "22222222222242228222222222222222",
            digest,
            3 * gib,
            "33333333333343338333333333333333",
            digest);
        OptimizationIssuanceAuthority issuance =
            OptimizationIssuanceAuthority.CreateCurrent(
                digest,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.OpenVinoCpu],
                8 * gib,
                safeDedicatedDeviceMemoryBudgetBytes: null,
                availableDiskBytes: 500 * gib,
                observedAtUtc: now,
                evaluatedAtUtc: now);
        OptimizationCandidate low = AdmittedExperimentalCandidate(
            snapshot,
            workload,
            binding,
            issuance,
            lowEvidenceId,
            OpenVinoKvCacheFormat.U8,
            OptimizationAssessment.Acceptable,
            2 * gib,
            6 * gib);
        OptimizationCandidate high = AdmittedExperimentalCandidate(
            snapshot,
            workload,
            binding,
            issuance,
            highEvidenceId,
            OpenVinoKvCacheFormat.F16,
            OptimizationAssessment.Good,
            4 * gib,
            4 * gib);
        IReadOnlySet<string> optedIn = new HashSet<string>(StringComparer.Ordinal)
        {
            lowEvidenceId,
            highEvidenceId
        };
        CompatibilityPlanningSession? session =
            CreatePlanningSession(
                [low, high],
                snapshot,
                workload,
                binding,
                optedIn);
        Assert.IsNotNull(session);
        CompatibilityOptimizationView optimization = ExperimentalOptimizationView();
        CompatibilitySetupView current = CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.OpenVinoGenAi,
            CompatibilityBackend.OpenVinoCpu,
            DeviceRouteId.Cpu,
            WeightQuantisation.F16,
            4096,
            CompatibilityFitState.Safe,
            4 * gib,
            8 * gib,
            4 * gib,
            0,
            false,
            false,
            [],
            openVinoKvCache: OpenVinoKvCacheFormat.RouteDefault);
        CompatibilityScreenModel screen = AuthoritativeEstimatedCompatibleScreen(current);
        CompatibilityEvaluation evaluation = new(
            screen,
            session,
            CurrentConfiguration: null)
        {
            OptionalOptimization = optimization,
            ExperimentalConsentOptions =
            [
                CompatibilityExperimentalConsentOption.Create(
                    OptimizationRoute.OpenVino, lowEvidenceId),
                CompatibilityExperimentalConsentOption.Create(
                    OptimizationRoute.OpenVino, highEvidenceId)
            ]
        };
        return new ExperimentalPlanningFixture(
            evaluation,
            binding,
            issuance,
            new OpenVinoExperimentalComposer(build, versions),
            lowEvidenceId,
            highEvidenceId,
            now);
    }

    private static OptimizationCandidate AdmittedExperimentalCandidate(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        OptimizationIssuanceAuthority issuance,
        string evidenceId,
        OpenVinoKvCacheFormat cache,
        OptimizationAssessment quality,
        ulong predictedPeakBytes,
        ulong headroomBytes)
    {
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Original,
                cache,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                1),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                quality,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                4096,
                predictedPeakBytes,
                predictedPeakBytes + headroomBytes,
                headroomBytes,
                0,
                0,
                requiresPersistentChange: false,
                availableDiskBytes: 500UL * 1024 * 1024 * 1024),
            evidenceId,
            isExperimental: true);
        IReadOnlySet<string> optedIn = new HashSet<string>(StringComparer.Ordinal)
        {
            evidenceId
        };
        Type proofType = typeof(OptimizationCandidate).Assembly.GetType(
                "GraniteEdgeAI.ModelHardwareCompatibility.Core.Application." +
                "Optimization.OptimizationAdmissionProof")
            ?? throw new AssertFailedException(
                "The core admission proof type was not found.");
        System.Reflection.MethodInfo create = proofType.GetMethod(
                "Create",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException(
                "The core admission proof factory was not found.");
        object proof = create.Invoke(null,
        [
            snapshot,
            workload,
            binding,
            candidate,
            SupportLevel.Experimental,
            true,
            optedIn,
            issuance
        ]) ?? throw new AssertFailedException(
            "The core admission proof factory returned no proof.");
        System.Reflection.MethodInfo attach = typeof(OptimizationCandidate).GetMethod(
                "AttachAdmissionProof",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException(
                "The candidate admission attachment method was not found.");
        return Assert.IsInstanceOfType<OptimizationCandidate>(
            attach.Invoke(null, [candidate, proof]));
    }

    private static CompatibilityPlanningSession? CreatePlanningSession(
        IReadOnlyList<OptimizationCandidate> candidates,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        IReadOnlySet<string> optedIn)
    {
        System.Reflection.MethodInfo create =
            typeof(CompatibilityPlanningSession).GetMethod(
                "Create",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException(
                "The planning-session factory was not found.");
        return create.Invoke(null,
        [
            OptimizationRoute.OpenVino,
            candidates,
            snapshot,
            workload,
            binding,
            32,
            optedIn,
            false
        ]) as CompatibilityPlanningSession;
    }

    private static CompatibilityScreenModel AuthoritativeEstimatedCompatibleScreen(
        CompatibilitySetupView current)
    {
        System.Reflection.ConstructorInfo constructor =
            typeof(CompatibilityScreenModel)
                .GetConstructors(System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .Single(candidate => candidate.GetParameters().Length == 11);
        return (CompatibilityScreenModel)constructor.Invoke(
        [
            CompatibilityScreenState.EstimatedCompatible,
            Array.Empty<CompatibilityFindingView>(),
            Array.Empty<CompatibilityModeView>(),
            BaselineExclusionReason.None,
            true,
            true,
            current,
            null,
            true,
            null,
            null
        ]);
    }

    private static CompatibilityOptimizationView ExperimentalOptimizationView()
    {
        CompatibilityOptimizationModeView Low(
            CompatibilityOptimizationLabelCode label,
            int? slider) => CompatibilityOptimizationModeView.ForPresentation(
                label,
                slider,
                OptimizationRoute.OpenVino,
                null,
                null,
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OptimizationAssessment.Acceptable,
                4096,
                2UL * 1024 * 1024 * 1024,
                8UL * 1024 * 1024 * 1024,
                6UL * 1024 * 1024 * 1024,
                false,
                false,
                OptimizationQualityNotice.SomeQualityReduction,
                true,
                false);
        CompatibilityOptimizationModeView High(
            CompatibilityOptimizationLabelCode label,
            int? slider) => CompatibilityOptimizationModeView.ForPresentation(
                label,
                slider,
                OptimizationRoute.OpenVino,
                null,
                null,
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.F16,
                DeviceRouteId.Cpu,
                OptimizationAssessment.Good,
                4096,
                4UL * 1024 * 1024 * 1024,
                8UL * 1024 * 1024 * 1024,
                4UL * 1024 * 1024 * 1024,
                false,
                false,
                OptimizationQualityNotice.None,
                true,
                false);
        return CompatibilityOptimizationView.ForPresentation(
            CompatibilityOptimizationLabelCode.Automatic,
            null,
            [
                High(CompatibilityOptimizationLabelCode.Automatic, null),
                Low(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10),
                Low(CompatibilityOptimizationLabelCode.Efficient, 30),
                High(CompatibilityOptimizationLabelCode.Balanced, 50),
                High(CompatibilityOptimizationLabelCode.HighCapability, 70),
                High(CompatibilityOptimizationLabelCode.MaximumCapability, 90)
            ],
            requiresPersistentArtifact: false,
            requiresRequantisationAcknowledgement: false,
            OptimizationQualityNotice.None);
    }

    internal static CurrentModelLaunchHandoff CurrentHandoff(
        OptimizationJourneyBinding binding,
        bool useMismatchedJourney = false)
    {
        const string digest =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        OpenVinoExecutionPayload payload = OpenVinoExecutionPayload.Create(
            "ov-current-config",
            "CPU",
            "Released",
            "ov-current",
            OpenVinoWeightPrecision.Fp16,
            OpenVinoWeightPrecision.Fp16,
            OpenVinoKvCachePrecision.ReleasedDefault,
            compiledCacheEnabled: false,
            compiledCacheIsDisposable: true,
            compiledCacheIsModelArtifact: false,
            createsCompletePackage: false,
            OpenVinoBuildIdentity.Create(
                "2026.3.0", "2026.3.0", "2026.3.0", digest),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openvino"] = "2026.3.0"
            });
        OptimizationExecutionPayload exact =
            OptimizationExecutionPayload.ForOpenVino(payload);
        return CurrentModelLaunchHandoff.Create(
            OptimizationRoute.OpenVino,
            useMismatchedJourney
                ? Guid.ParseExact("44444444444444448444444444444444", "N")
                : Guid.ParseExact(binding.ModelInspectionRunId, "N"),
            Guid.ParseExact(binding.ModelInspectionHandoffId, "N"),
            binding.ModelSha256,
            checked((long)binding.ModelLengthBytes),
            Guid.ParseExact(binding.ProductHardwareRunId, "N"),
            binding.HardwareSnapshotSha256,
            new CurrentCompatibleConfiguration(
                OptimizationRoute.OpenVino,
                exact,
                exact.ComputeRuntimeConfigurationSha256(),
                "decision-openvino"));
    }

    internal sealed record ExperimentalPlanningFixture(
        CompatibilityEvaluation Evaluation,
        OptimizationJourneyBinding Binding,
        OptimizationIssuanceAuthority IssuanceAuthority,
        IOptimizationExecutionPayloadComposer Composer,
        string LowEvidenceId,
        string HighEvidenceId,
        DateTimeOffset Now);

    internal sealed record RealExactPlanningFixture(
        CompatibilityEvaluation Evaluation,
        OpenVinoOptimizationProductionAuthority Authority,
        CompatibilityFreshResourcesInput FreshResources,
        DateTimeOffset Now,
        FixedTimeProvider TimeProvider);

    internal static RealExactPlanningFixture RealExactFixture(
        ulong availableMemoryBytes = 4_300_000_000UL)
    {
        Guid modelRunId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        Guid modelHandoffId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        Guid hardwareRunId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        const string packageSha =
            "1111111111111111111111111111111111111111111111111111111111111111";
        const string modelSha = VerifiedOpenVinoOptimizationEvidence.SourceModelSha256;
        DateTimeOffset now = new(2026, 9, 6, 11, 0, 0, TimeSpan.Zero);
        OpenVinoStaticPackageEvidence evidence = new(
            1, packageSha, modelSha, 6_805_673_303, "granite",
            "GraniteForCausalLM", "text-generation-with-past", 131_072,
            "float16", "PreTrainedTokenizerFast", 10, true,
            40, 4_096, 32, 8);
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            modelHandoffId,
            modelRunId,
            InspectionOutcome.Ready,
            modelSha,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            hardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                hardwareRunId));
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(
            model, evidence, hardwareRunId, hardware,
            out PreparedOpenVinoCompatibilityInput? prepared));
        Assert.IsNotNull(prepared);
        OpenVinoBuildEvidence released = new(
            VerifiedOpenVinoOptimizationEvidence.RuntimeBuild,
            VerifiedOpenVinoOptimizationEvidence.GenAiBuild,
            VerifiedOpenVinoOptimizationEvidence.TokenizersBuild,
            VerifiedOpenVinoOptimizationEvidence.WorkerManifestSha256);
        OpenVinoBuildEvidence turboQuant = new(
            VerifiedOpenVinoOptimizationEvidence.TurboRuntimeBuild,
            VerifiedOpenVinoOptimizationEvidence.TurboGenAiBuild,
            VerifiedOpenVinoOptimizationEvidence.TurboTokenizersBuild,
            VerifiedOpenVinoOptimizationEvidence.TurboWorkerManifestSha256,
            new GraniteEdgeAI.OpenVino.Contracts.TurboQuantBuildEvidence(
                "f5f594dc0c9e5961785f0d17743486d52eac87e7",
                "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
                VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256,
                VerifiedOpenVinoOptimizationEvidence.TurboRuntimeManifestSha256));
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            released,
            turboQuant,
            optimizationAvailable: true,
            out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);
        CompatibilityFreshResourcesInput fresh = CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(availableMemoryBytes),
            availableDedicatedDeviceMemoryBytes: null,
            availableStorageBytes: 64UL * 1024 * 1024 * 1024,
            now);
        CompatibilityEvaluation evaluation = authority.Evaluate(
            fresh,
            new HashSet<string>(StringComparer.Ordinal),
            now,
            CancellationToken.None);
        Assert.IsNotNull(evaluation.PlanningSession);
        Assert.IsNotNull(evaluation.Screen.Optimization);
        Assert.IsTrue(evaluation.Screen.Optimization.HasAdditionalExactSafeModes);
        return new(evaluation, authority, fresh, now, new FixedTimeProvider(now));
    }

    internal sealed class DualOpenVinoAuthority(
        IOptimizationExecutionPayloadComposer composer,
        OptimizationIssuanceAuthority issuanceAuthority)
        : ICompatibilityActionAuthority
    {
        public bool TryGetOptimizationAuthority(
            OptimizationRoute route,
            out IOptimizationExecutionPayloadComposer? selectedComposer,
            out OptimizationIssuanceAuthority? selectedAuthority)
        {
            bool available = route == OptimizationRoute.OpenVino;
            selectedComposer = available ? composer : null;
            selectedAuthority = available ? issuanceAuthority : null;
            return available;
        }

        public bool IsCurrentModelChatAvailable(OptimizationRoute route) =>
            route == OptimizationRoute.OpenVino;
    }

    private sealed class OptimizationAndChatAuthority(
        ICompatibilityActionAuthority optimizationAuthority)
        : ICompatibilityActionAuthority
    {
        public bool TryGetOptimizationAuthority(
            OptimizationRoute route,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? issuanceAuthority) =>
            optimizationAuthority.TryGetOptimizationAuthority(
                route,
                out composer,
                out issuanceAuthority);

        public bool IsCurrentModelChatAvailable(OptimizationRoute route) => true;
    }

    private sealed class OpenVinoExperimentalComposer(
        OpenVinoBuildIdentity build,
        IReadOnlyDictionary<string, string> versions)
        : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.OpenVino;

        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate)
        {
            OpenVinoRouteConfiguration configuration =
                Assert.IsInstanceOfType<OpenVinoRouteConfiguration>(candidate.Configuration);
            OpenVinoKvCachePrecision cache = configuration.KvCache switch
            {
                OpenVinoKvCacheFormat.U8 => OpenVinoKvCachePrecision.U8,
                OpenVinoKvCacheFormat.F16 => OpenVinoKvCachePrecision.F16,
                _ => throw new AssertFailedException("Unexpected test cache format.")
            };
            return OptimizationExecutionPayload.ForOpenVino(
                OpenVinoExecutionPayload.Create(
                    configuration.KvCache == OpenVinoKvCacheFormat.U8
                        ? "ov-experimental-efficient-config"
                        : "ov-experimental-capable-config",
                    "CPU",
                    "Experimental candidate",
                    candidate.EvidenceId,
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoWeightPrecision.Fp16,
                    cache,
                    compiledCacheEnabled: false,
                    compiledCacheIsDisposable: true,
                    compiledCacheIsModelArtifact: false,
                    createsCompletePackage: false,
                    build,
                    versions));
        }
    }

    internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        internal void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    private sealed class FrozenOptimizationAuthority
        : ICompatibilityActionAuthority
    {
        private readonly IOptimizationExecutionPayloadComposer _composer;
        private readonly OptimizationIssuanceAuthority _issuance;

        internal FrozenOptimizationAuthority(ICompatibilityActionAuthority source)
        {
            Assert.IsTrue(source.TryGetOptimizationAuthority(
                OptimizationRoute.OpenVino,
                out IOptimizationExecutionPayloadComposer? composer,
                out OptimizationIssuanceAuthority? issuance));
            _composer = composer!;
            _issuance = issuance!;
        }

        public bool TryGetOptimizationAuthority(
            OptimizationRoute route,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? issuanceAuthority)
        {
            bool available = route == OptimizationRoute.OpenVino;
            composer = available ? _composer : null;
            issuanceAuthority = available ? _issuance : null;
            return available;
        }

        public bool IsCurrentModelChatAvailable(OptimizationRoute route) => false;
    }

    private static CompatibilityFreshResourcesInput FreshAt(
        RealExactPlanningFixture fixture,
        DateTimeOffset now,
        ulong? availableSystemMemoryBytes = null) =>
        CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(
                availableSystemMemoryBytes
                ?? fixture.FreshResources.AvailableSystemMemoryBytes),
            fixture.FreshResources.DedicatedDeviceMemoryEstablished
                ? fixture.FreshResources.AvailableDedicatedDeviceMemoryBytes
                : null,
            fixture.FreshResources.AvailableStorageBytes,
            now);

    private static IReadOnlySet<string> EmptyConsent() =>
        new HashSet<string>(StringComparer.Ordinal);

    private static CurrentModelLaunchHandoff CurrentHandoff()
    {
        const string digest =
            "1111111111111111111111111111111111111111111111111111111111111111";
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForGguf(
            GgufExecutionPayload.Create(
                "runtime",
                "0123456789abcdef0123456789abcdef01234567",
                GgufRuntimeBackend.Cpu,
                "CPU",
                4096,
                GgufCacheType.F16,
                GgufCacheType.F16,
                0,
                false,
                4,
                128,
                "Estimated",
                "profile",
                256,
                GgufWeightFormat.Imported));
        CurrentCompatibleConfiguration current = new(
            OptimizationRoute.Gguf,
            payload,
            payload.ComputeRuntimeConfigurationSha256(),
            "decision-1");
        return CurrentModelLaunchHandoff.Create(
            OptimizationRoute.Gguf,
            Guid.NewGuid(),
            Guid.NewGuid(),
            digest,
            2L * 1024 * 1024 * 1024,
            Guid.NewGuid(),
            digest,
            current);
    }

    private sealed class ChatOnlyAuthority : ICompatibilityActionAuthority
    {
        public bool TryGetOptimizationAuthority(
            OptimizationRoute route,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? issuanceAuthority)
        {
            composer = null;
            issuanceAuthority = null;
            return false;
        }

        public bool IsCurrentModelChatAvailable(OptimizationRoute route) =>
            route == OptimizationRoute.Gguf;
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
