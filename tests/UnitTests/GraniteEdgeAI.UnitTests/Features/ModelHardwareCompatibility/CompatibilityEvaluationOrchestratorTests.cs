using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ApplicationComposition;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class CompatibilityEvaluationOrchestratorTests
{
    [TestMethod]
    public async Task AuthorityEvaluationUsesTheInjectedClock()
    {
        DateTimeOffset expected = new(2035, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var orchestrator = A1BackendProductionAuthorities.Shared.CreateCompatibility(
            new FixedSource(Fresh(expected)),
            new FixedTimeProvider(expected));
        DateTimeOffset observed = default;

        CompatibilityEvaluation result = await orchestrator.EvaluateAuthorityAsync(
            new HashSet<string>(StringComparer.Ordinal),
            (fresh, optedIn, evaluatedAtUtc, token) =>
            {
                observed = evaluatedAtUtc;
                return new CompatibilityEvaluation(
                    CompatibilityEngine.RunWithAvailableAdapters(token),
                    null,
                    null);
            },
            CancellationToken.None);

        Assert.AreEqual(expected, observed);
        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.Screen.State);
    }

    [TestMethod]
    public async Task TypedCaptureUnavailabilityUsesOneFailClosedFallback()
    {
        var orchestrator = A1BackendProductionAuthorities.Shared.CreateCompatibility(
            new ThrowingSource(new CompatibilityFreshResourcesUnavailableException(
                CompatibilityFreshResourcesUnavailableReason.StorageInaccessible)),
            TimeProvider.System);
        bool evaluatorInvoked = false;

        CompatibilityEvaluation result = await orchestrator.EvaluateAuthorityAsync(
            new HashSet<string>(StringComparer.Ordinal),
            (fresh, optedIn, evaluatedAtUtc, token) =>
            {
                evaluatorInvoked = true;
                throw new InvalidOperationException();
            },
            CancellationToken.None);

        Assert.IsFalse(evaluatorInvoked);
        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.Screen.State);
        Assert.IsNull(result.PlanningSession);
    }

    [TestMethod]
    public async Task CancellationPreservesTheExactCallerToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var orchestrator = A1BackendProductionAuthorities.Shared.CreateCompatibility(
            new CancellingSource(cancellation.Token),
            TimeProvider.System);

        OperationCanceledException exception =
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            orchestrator.EvaluateAuthorityAsync(
                new HashSet<string>(StringComparer.Ordinal),
                (fresh, optedIn, evaluatedAtUtc, token) =>
                    throw new InvalidOperationException(),
                cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
    }

    [TestMethod]
    public async Task SourceProgrammingFaultIsNotConvertedIntoCompatibilityFallback()
    {
        var orchestrator = A1BackendProductionAuthorities.Shared.CreateCompatibility(
            new ThrowingSource(new InvalidOperationException("programming defect")),
            TimeProvider.System);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            orchestrator.EvaluateAuthorityAsync(
                new HashSet<string>(StringComparer.Ordinal),
                (fresh, optedIn, evaluatedAtUtc, token) =>
                    throw new AssertFailedException("Evaluator must not run."),
                CancellationToken.None));
    }

    [TestMethod]
    public async Task EvaluatorProgrammingFaultIsNotConvertedIntoCompatibilityFallback()
    {
        DateTimeOffset now = new(2035, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var orchestrator = A1BackendProductionAuthorities.Shared.CreateCompatibility(
            new FixedSource(Fresh(now)),
            new FixedTimeProvider(now));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            orchestrator.EvaluateAuthorityAsync(
                new HashSet<string>(StringComparer.Ordinal),
                (fresh, optedIn, evaluatedAtUtc, token) =>
                    throw new InvalidOperationException("programming defect"),
                CancellationToken.None));
    }

    [TestMethod]
    public async Task UnboundProductionInputUsesTheSameFailClosedFallback()
    {
        DateTimeOffset now = new(2035, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var orchestrator = A1BackendProductionAuthorities.Shared.CreateCompatibility(
            new FixedSource(Fresh(now)),
            new FixedTimeProvider(now));

        CompatibilityScreenModel result = await orchestrator.EvaluateBoundAsync(
            RefuseBinding,
            CancellationToken.None);

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.State);
        Assert.IsFalse(result.ContinueEnabled);
    }

    private static CompatibilityFreshResourcesInput Fresh(DateTimeOffset observedAtUtc) =>
        CompatibilityFreshResourcesInput.Create(
            8UL * 1024 * 1024 * 1024,
            availableDedicatedDeviceMemoryBytes: null,
            64UL * 1024 * 1024 * 1024,
            observedAtUtc);

    private static bool RefuseBinding(
        CompatibilityFreshResourcesInput fresh,
        out CompatibilityProductionInput? input)
    {
        input = null;
        return false;
    }

    private sealed class FixedSource(CompatibilityFreshResourcesInput value)
        : ICompatibilityFreshResourcesSource
    {
        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken) => ValueTask.FromResult(value);
    }

    private sealed class ThrowingSource(Exception exception)
        : ICompatibilityFreshResourcesSource
    {
        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken) => ValueTask.FromException<
                CompatibilityFreshResourcesInput>(exception);
    }

    private sealed class CancellingSource(CancellationToken token)
        : ICompatibilityFreshResourcesSource
    {
        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken) => ValueTask.FromException<
                CompatibilityFreshResourcesInput>(new OperationCanceledException(token));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
