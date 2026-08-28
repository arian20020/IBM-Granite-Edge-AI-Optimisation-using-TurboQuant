using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class CompatibilityEvaluationOrchestratorTests
{
    [TestMethod]
    public async Task AuthorityEvaluationUsesTheInjectedClock()
    {
        DateTimeOffset expected = new(2035, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var orchestrator = new CompatibilityEvaluationOrchestrator(
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
    public async Task CaptureFailureUsesOneFailClosedFallback()
    {
        var orchestrator = new CompatibilityEvaluationOrchestrator(
            new ThrowingSource(new IOException("capture failed")),
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
    public async Task CancellationIsNeverConvertedIntoCompatibilityFallback()
    {
        var orchestrator = new CompatibilityEvaluationOrchestrator(
            new ThrowingSource(new OperationCanceledException()),
            TimeProvider.System);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            orchestrator.EvaluateAuthorityAsync(
                new HashSet<string>(StringComparer.Ordinal),
                (fresh, optedIn, evaluatedAtUtc, token) =>
                    throw new InvalidOperationException(),
                CancellationToken.None));
    }

    [TestMethod]
    public async Task UnboundProductionInputUsesTheSameFailClosedFallback()
    {
        DateTimeOffset now = new(2035, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var orchestrator = new CompatibilityEvaluationOrchestrator(
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
