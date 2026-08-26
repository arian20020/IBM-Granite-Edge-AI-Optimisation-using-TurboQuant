using System.Security.Cryptography;
using GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class GgufOptimizationExecutorTests
{
    private const string ToolDigest =
        "2222222222222222222222222222222222222222222222222222222222222222";

    [TestMethod]
    public async Task PersistentPlanCreatesAndAdmitsOneBoundOutput()
    {
        using var fixture = new ExecutorFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.OutputStaging,
            fixture.OutputCommitted);
        var runner = new FakeRunner();
        var executor = new GgufOptimizationExecutor(registry, runner);
        var context = new OptimizationAttemptContext(
            1,
            fixture.SourceSnapshot(),
            "operation-root-1");
        var progress = new List<OptimizationProgress>();

        OptimizationExecutionResult result = await executor.ExecuteAsync(
            plan,
            context,
            new InlineProgress<OptimizationProgress>(progress.Add),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.SucceededPersistent, result.Status);
        Assert.IsTrue(result.SourceUnchanged);
        Assert.IsTrue(result.OutputSizeBytes > 0);
        Assert.AreEqual(1, registry.AdmittedCount);
        Assert.AreEqual(1, runner.Calls);
        Assert.AreEqual(GgufQuantizationFormat.F16, runner.Command!.SourceFormat);
        Assert.AreEqual(GgufQuantizationFormat.Q3KM, runner.Command.TargetFormat);
        Assert.AreEqual(OptimizationProgressStage.Publish, progress[^1].Stage);
        Assert.AreEqual(1d, progress[^1].Fraction);
    }

    private sealed class FakeRunner : IGgufQuantizationRunner
    {
        internal int Calls { get; private set; }
        internal GgufQuantizationCommand? Command { get; private set; }
        public string ManifestSha256 => ToolDigest;
        public string ExecutableSha256 => ToolDigest;

        public Task<GgufQuantizationEvent> RunAsync(
            GgufQuantizationCommand command,
            string sourcePath,
            string sourceSha256,
            ulong sourceLengthBytes,
            string outputPath,
            CancellationToken cancellationToken)
        {
            Calls++;
            Command = command;
            File.WriteAllBytes(outputPath, "optimized-gguf"u8.ToArray());
            return Task.FromResult(GgufQuantizationEvent.Create(
                command.CorrelationId,
                command.OptimizationPlanId,
                command.ConfigurationSha256,
                GgufQuantizationEventKind.Completed,
                100,
                GgufQuantizationSupportCode.None,
                command.OutputToken,
                command.RequantizationAuthorizationSha256));
        }
    }

    private sealed class ExecutorFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "geai-gguf-executor-" + Guid.NewGuid().ToString("N"));
        private readonly byte[] _source = "source-gguf"u8.ToArray();

        internal ExecutorFixture()
        {
            Directory.CreateDirectory(OutputStaging);
            Directory.CreateDirectory(OutputCommitted);
            File.WriteAllBytes(SourcePath, _source);
        }

        internal string OutputStaging => Path.Combine(_root, "staging");
        internal string OutputCommitted => Path.Combine(_root, "committed");
        private string SourcePath => Path.Combine(_root, "source.gguf");
        private string SourceDigest => Convert.ToHexString(
            SHA256.HashData(_source)).ToLowerInvariant();

        internal OptimizationExecutionPlan Plan() =>
            OptimizationSelectionHandoffTests.PersistentPlanForSource(
                SourceDigest,
                (ulong)_source.Length);

        internal StagedSourceSnapshot SourceSnapshot() => new(
            SourceDigest,
            (ulong)_source.Length,
            "sealed-source-executor",
            SourcePath);

        public void Dispose()
        {
            if (!Directory.Exists(_root)) return;
            foreach (string file in Directory.EnumerateFiles(
                _root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class InlineProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }
}
