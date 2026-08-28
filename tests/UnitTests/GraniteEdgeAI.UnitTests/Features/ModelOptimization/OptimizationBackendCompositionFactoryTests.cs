using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationBackendCompositionFactoryTests
{
    [TestMethod]
    public void ExactRouteBuilderIsSelectedWithoutCrossRouteFallback()
    {
        using var root = new TemporaryDirectory();
        var gguf = new FakeBuilder(OptimizationRoute.Gguf);
        var openVino = new FakeBuilder(OptimizationRoute.OpenVino);
        var factory = new OptimizationBackendCompositionFactory(
            root.Path,
            [gguf, openVino],
            TimeProvider.System);
        OptimizationJourneyEntryContext entry =
            OptimizationSelectionHandoffTests.RequiredJourneyEntry(
                OptimizationRoute.OpenVino);

        bool created = factory.TryCreate(entry, out OptimizationBackendComposition? result);

        Assert.IsTrue(created);
        Assert.IsNotNull(result);
        Assert.AreSame(openVino.Executor, result.Executor);
        Assert.AreEqual(0, gguf.BuildCount);
        Assert.AreEqual(1, openVino.BuildCount);
    }

    [TestMethod]
    public void MissingRouteAuthorityRefusesCompositionWithoutFallback()
    {
        using var root = new TemporaryDirectory();
        var factory = new OptimizationBackendCompositionFactory(
            root.Path,
            [new FakeBuilder(OptimizationRoute.Gguf)],
            TimeProvider.System);
        OptimizationJourneyEntryContext entry =
            OptimizationSelectionHandoffTests.RequiredJourneyEntry(
                OptimizationRoute.OpenVino);

        bool created = factory.TryCreate(entry, out OptimizationBackendComposition? result);

        Assert.IsFalse(created);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void DuplicateRouteAuthorityIsRejected()
    {
        using var root = new TemporaryDirectory();

        Assert.ThrowsExactly<ArgumentException>(() =>
            new OptimizationBackendCompositionFactory(
                root.Path,
                [
                    new FakeBuilder(OptimizationRoute.Gguf),
                    new FakeBuilder(OptimizationRoute.Gguf)
                ],
                TimeProvider.System));
    }

    private sealed class FakeBuilder(OptimizationRoute route)
        : IOptimizationBackendBuilder
    {
        internal FakeExecutor Executor { get; } = new(route);
        internal int BuildCount { get; private set; }
        public OptimizationRoute Route => route;

        public OptimizationBackendParts Build(
            OptimizationJourneyEntryContext entry,
            OptimizationOutputRegistry outputs)
        {
            BuildCount++;
            return new OptimizationBackendParts(
                Executor,
                new FakeRevalidator(),
                new FakeContextFactory());
        }
    }

    private sealed class FakeExecutor(OptimizationRoute route) : IOptimizationExecutor
    {
        public OptimizationRoute Route => route;

        public Task<OptimizationExecutionResult> ExecuteAsync(
            OptimizationExecutionPlan plan,
            OptimizationAttemptContext context,
            IProgress<OptimizationProgress> progress,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRevalidator : IOptimizationRevalidator
    {
        public Task<OptimizationRevalidationResult> RevalidateAsync(
            OptimizationExecutionPlan plan,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeContextFactory : IOptimizationAttemptContextFactory
    {
        public Task<OptimizationAttemptContext> CreateAsync(
            long generation,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory() =>
            Path = Directory.CreateTempSubdirectory("OptimizationComposition-").FullName;

        internal string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
