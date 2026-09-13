using System.Reflection;
using System.Security.Cryptography;
using GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Failures;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
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
        var validator = FakeValidator.Success();
        var executor = new GgufOptimizationExecutor(registry, runner, validator);
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
        Assert.AreEqual(1, validator.Calls);
        Assert.AreEqual(GgufQuantizationFormat.F16, runner.Command!.SourceFormat);
        Assert.AreEqual(GgufQuantizationFormat.Q3KM, runner.Command.TargetFormat);
        Assert.AreEqual(OptimizationProgressStage.Publish, progress[^1].Stage);
        Assert.AreEqual(1d, progress[^1].Fraction);
    }

    [DataTestMethod]
    [DataRow(OptimizationSupportCode.ValidationFailed)]
    [DataRow(OptimizationSupportCode.SmokeTestFailed)]
    [DataRow(OptimizationSupportCode.ReinspectionFailed)]
    public async Task PersistentValidationFailureStopsBeforePublication(
        OptimizationSupportCode supportCode)
    {
        using var fixture = new ExecutorFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.OutputStaging,
            fixture.OutputCommitted);
        var validator = new FakeValidator(supportCode);
        var executor = new GgufOptimizationExecutor(
            registry,
            new FakeRunner(),
            validator);
        var context = new OptimizationAttemptContext(
            1,
            fixture.SourceSnapshot(),
            "operation-root-validation-failure");

        OptimizationExecutionResult result = await executor.ExecuteAsync(
            plan,
            context,
            new InlineProgress<OptimizationProgress>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.AreEqual(supportCode, result.SupportCode);
        Assert.AreEqual(0, registry.AdmittedCount);
        Assert.AreEqual(1, validator.Calls);
    }

    [TestMethod]
    public async Task PersistentValidationCancellationPropagates()
    {
        using var fixture = new ExecutorFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.OutputStaging,
            fixture.OutputCommitted);
        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var executor = new GgufOptimizationExecutor(
            registry,
            new FakeRunner(),
            FakeValidator.Success());
        var context = new OptimizationAttemptContext(
            1,
            fixture.SourceSnapshot(),
            "operation-root-validation-cancel");

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(
                plan,
                context,
                new InlineProgress<OptimizationProgress>(_ => { }),
                cancellation.Token));

        Assert.AreEqual(0, registry.AdmittedCount);
    }

    [TestMethod]
    public async Task MutationAfterValidationBeforeSealFailsWithoutAdmission()
    {
        using var fixture = new ExecutorFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.OutputStaging,
            fixture.OutputCommitted);
        var executor = new GgufOptimizationExecutor(
            registry,
            new FakeRunner(),
            new MutatingValidator());
        var context = new OptimizationAttemptContext(
            1,
            fixture.SourceSnapshot(),
            "operation-root-post-validation-mutation");

        OptimizationExecutionResult result = await executor.ExecuteAsync(
            plan,
            context,
            new InlineProgress<OptimizationProgress>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.AreEqual(
            OptimizationSupportCode.ReinspectionFailed,
            result.SupportCode);
        Assert.AreEqual(0, registry.AdmittedCount);
    }

    [TestMethod]
    public async Task ExactBf16Q3EvidenceRejectsDifferentValidOutputBeforePublication()
    {
        using var fixture = new ExecutorFixture();
        OptimizationExecutionPlan plan = fixture.ExactBf16Q3Plan();
        var registry = new OptimizationOutputRegistry(
            fixture.OutputStaging,
            fixture.OutputCommitted);
        var executor = new GgufOptimizationExecutor(
            registry,
            new FakeRunner(),
            new FixedOutputValidator(new string('a', 64), 1_725_577_824));
        var context = new OptimizationAttemptContext(
            1,
            fixture.SourceSnapshot(),
            "operation-root-exact-q3-output");

        OptimizationExecutionResult result = await executor.ExecuteAsync(
            plan,
            context,
            new InlineProgress<OptimizationProgress>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.AreEqual(OptimizationSupportCode.ReinspectionFailed, result.SupportCode);
        Assert.AreEqual(0, registry.AdmittedCount);
    }

    [TestMethod]
    public void ExactBf16Q3ValidationAcceptsOnlyTheMeasuredOutputIdentity()
    {
        using var fixture = new ExecutorFixture();
        OptimizationExecutionPlan plan = fixture.ExactBf16Q3Plan();
        GgufOptimizationOutputValidationResult exact =
            GgufOptimizationOutputValidationResult.Success(
                GgufWeightFormat.Q3KM,
                "77eeab5d868bee624e283f141d5b6be3fa4f4a59b7c78b0163c3a28c7a80a3ca",
                1_725_577_824);

        Assert.IsTrue(GgufOptimizationExecutor.MatchesMeasuredOutputIdentity(
            plan, exact));
        Assert.IsFalse(GgufOptimizationExecutor.MatchesMeasuredOutputIdentity(
            plan, GgufOptimizationOutputValidationResult.Success(
                GgufWeightFormat.Q3KM, new string('a', 64), 1_725_577_824)));
        Assert.IsFalse(GgufOptimizationExecutor.MatchesMeasuredOutputIdentity(
            plan, GgufOptimizationOutputValidationResult.Success(
                GgufWeightFormat.Q3KM,
                "77eeab5d868bee624e283f141d5b6be3fa4f4a59b7c78b0163c3a28c7a80a3ca",
                1_725_577_823)));
    }

    [TestMethod]
    public void ExecutorRequiresValidatorBeforeAnyPlanCanRun()
    {
        using var fixture = new ExecutorFixture();
        var registry = new OptimizationOutputRegistry(
            fixture.OutputStaging,
            fixture.OutputCommitted);
        var runner = new FakeRunner();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new GgufOptimizationExecutor(registry, runner, null!));
        Assert.AreEqual(0, runner.Calls);
        Assert.AreEqual(0, registry.AdmittedCount);
    }

    [TestMethod]
    public async Task RuntimeOnlyPlanWaitsForExactSmokeBeforeSuccess()
    {
        using var fixture = new ExecutorFixture();
        OptimizationExecutionPlan plan = fixture.RuntimeOnlyPlan();
        var registry = new OptimizationOutputRegistry(
            fixture.OutputStaging,
            fixture.OutputCommitted);
        var runner = new FakeRunner();
        var validator = FakeValidator.Success(blockRuntime: true);
        var executor = new GgufOptimizationExecutor(registry, runner, validator);
        var context = new OptimizationAttemptContext(
            1,
            fixture.SourceSnapshot(),
            "operation-root-runtime-smoke");
        var progress = new List<OptimizationProgress>();

        Task<OptimizationExecutionResult> operation = executor.ExecuteAsync(
            plan,
            context,
            new InlineProgress<OptimizationProgress>(progress.Add),
            CancellationToken.None);
        await validator.RuntimeEntered.Task;

        Assert.IsFalse(operation.IsCompleted);
        Assert.AreEqual(0, runner.Calls);
        Assert.AreEqual(1, validator.RuntimeCalls);
        Assert.AreEqual(0, validator.Calls);

        validator.ReleaseRuntime.SetResult();
        OptimizationExecutionResult result = await operation;

        Assert.AreEqual(
            OptimizationExecutionStatus.SucceededRuntimeProfile,
            result.Status);
        Assert.AreEqual($"gguf-profile-{plan.OptimizationPlanId:N}",
            result.OutputIdentity);
        Assert.AreEqual(plan.ConfigurationSha256, result.OutputManifestSha256);
        Assert.AreEqual(0UL, result.OutputSizeBytes);
        CollectionAssert.AreEqual(
            new[]
            {
                OptimizationProgressStage.Preflight,
                OptimizationProgressStage.Validate,
                OptimizationProgressStage.SmokeTest,
                OptimizationProgressStage.Reinspect,
            },
            progress.Select(item => item.Stage).ToArray());
    }

    [TestMethod]
    public async Task RuntimeOnlySmokeFailurePublishesNoSuccess()
    {
        using var fixture = new ExecutorFixture();
        OptimizationExecutionPlan plan = fixture.RuntimeOnlyPlan();
        var registry = new OptimizationOutputRegistry(
            fixture.OutputStaging,
            fixture.OutputCommitted);
        var validator = new FakeValidator(
            OptimizationSupportCode.None,
            runtimeSupportCode: OptimizationSupportCode.SmokeTestFailed);
        var executor = new GgufOptimizationExecutor(
            registry, new FakeRunner(), validator);
        var context = new OptimizationAttemptContext(
            1,
            fixture.SourceSnapshot(),
            "operation-root-runtime-failure");

        OptimizationExecutionResult result = await executor.ExecuteAsync(
            plan,
            context,
            new InlineProgress<OptimizationProgress>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.AreEqual(OptimizationSupportCode.SmokeTestFailed, result.SupportCode);
        Assert.IsNull(result.OutputIdentity);
        Assert.AreEqual(1, validator.RuntimeCalls);
        Assert.AreEqual(0, registry.AdmittedCount);
    }

    [TestMethod]
    public void VulkanRuntimeUnavailabilityRequiresASeparatelyIssuedReplan()
    {
        using var fixture = new ExecutorFixture();
        OptimizationExecutionPlan plan = fixture.Plan();
        var context = new OptimizationAttemptContext(
            1,
            fixture.SourceSnapshot(),
            "operation-root-vulkan");
        var failure = new GgufRuntimeFailure(
            GgufRuntimeFailureCategory.RuntimeUnavailable,
            "vulkan-runtime-unavailable");

        OptimizationExecutionResult result = GgufRuntimeFailureResultMapper.Map(
            plan,
            context,
            GgufRuntimeBackend.Vulkan,
            failure,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        Assert.AreEqual(OptimizationExecutionStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.ToolNotAdmitted, result.SupportCode);
        Assert.AreEqual(plan.ConfigurationSha256, result.ConfigurationSha256);
        Assert.AreEqual(plan.OptimizationPlanId, result.OptimizationPlanId);
        Assert.IsNull(result.OutputIdentity);
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

        internal OptimizationExecutionPlan ExactBf16Q3Plan()
        {
            OptimizationExecutionPlan basis = Plan();
            OptimizationCandidate candidate = OptimizationCandidate.Create(
                (GgufRouteConfiguration)basis.Candidate.Configuration,
                basis.Candidate.Metrics,
                "GGUF-V5-BF16-Q3-CPU-F16-01",
                isExperimental: false);
            GgufAdmittedConfiguration admission = GgufAdmittedConfiguration.Create(
                "GGUF-V5-BF16-Q3-CPU-F16-01",
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GgufWeightFormat.Q3KM,
                GgufKvCacheFormat.F16,
                GpuOffloadLevel.None,
                512,
                32768,
                SupportLevel.DeclaredSupported,
                requiresEvidence: false);
            GgufCapabilityPayload old = basis.CapabilitySnapshot.Gguf!;
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForGguf(
                    "exact-bf16-q3-executor",
                    basis.CapabilitySnapshot.CapabilitySnapshotSha256,
                    GgufCapabilityPayload.Create(
                        old.RuntimeVersion,
                        [admission],
                        hasHigherPrecisionSource: true,
                        conversionSource: old.ConversionSource,
                        admittedQuantiser: old.AdmittedQuantiser));
            ConstructorInfo constructor = typeof(OptimizationExecutionPlan)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(candidateConstructor =>
                    candidateConstructor.GetParameters().Length == 11);
            return (OptimizationExecutionPlan)constructor.Invoke(
            [
                OptimizationExecutionPlan.CurrentContractVersion,
                basis.OptimizationPlanId,
                basis.Binding,
                snapshot,
                basis.Workload,
                candidate,
                basis.ExecutionPayload,
                basis.Preference,
                false,
                basis.ConfigurationSha256,
                basis.CreatedAtUtc,
            ]);
        }

        internal OptimizationExecutionPlan RuntimeOnlyPlan()
        {
            OptimizationExecutionPlan basis = Plan();
            GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None);
            OptimizationCandidateMetrics metrics =
                OptimizationCandidateMetrics.Create(
                    EvidenceGrade.Estimated,
                    OptimizationAssessment.Acceptable,
                    OptimizationAssessment.Acceptable,
                    OptimizationAssessment.Acceptable,
                    4096,
                    1,
                    2,
                    1,
                    0,
                    0,
                    requiresPersistentChange: false);
            OptimizationCandidate candidate =
                OptimizationCandidate.Create(
                    configuration,
                    metrics,
                    "gguf-imported",
                    isExperimental: false);
            OptimizationExecutionPayload payload =
                OptimizationExecutionPayload.ForGguf(
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
                        "profile-imported",
                        256,
                        GgufWeightFormat.Imported));
            ConstructorInfo constructor = typeof(OptimizationExecutionPlan)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(candidateConstructor =>
                    candidateConstructor.GetParameters().Length == 11);
            return (OptimizationExecutionPlan)constructor.Invoke(
            [
                OptimizationExecutionPlan.CurrentContractVersion,
                Guid.NewGuid(),
                basis.Binding,
                basis.CapabilitySnapshot,
                basis.Workload,
                candidate,
                payload,
                OptimizationPreferenceSelection.Automatic(),
                false,
                "3333333333333333333333333333333333333333333333333333333333333333",
                DateTimeOffset.UnixEpoch,
            ]);
        }

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

    private sealed class FakeValidator(
        OptimizationSupportCode supportCode,
        OptimizationSupportCode runtimeSupportCode = OptimizationSupportCode.None,
        bool blockRuntime = false)
        : IGgufOptimizationOutputValidator
    {
        internal int Calls { get; private set; }
        internal int RuntimeCalls { get; private set; }
        internal TaskCompletionSource RuntimeEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseRuntime { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal static FakeValidator Success(bool blockRuntime = false) =>
            new(OptimizationSupportCode.None, blockRuntime: blockRuntime);

        public Task<GgufOptimizationOutputValidationResult> ValidateAsync(
            OptimizationExecutionPlan plan,
            string outputPath,
            IProgress<GgufOptimizationOutputValidationPhase> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            progress.Report(GgufOptimizationOutputValidationPhase.Validate);
            if (supportCode == OptimizationSupportCode.None)
            {
                progress.Report(GgufOptimizationOutputValidationPhase.SmokeTest);
                progress.Report(GgufOptimizationOutputValidationPhase.Reinspect);
            }
            if (supportCode != OptimizationSupportCode.None)
            {
                return Task.FromResult(
                    GgufOptimizationOutputValidationResult.Failure(supportCode));
            }

            byte[] output = File.ReadAllBytes(outputPath);
            return Task.FromResult(
                GgufOptimizationOutputValidationResult.Success(
                    plan.ExecutionPayload.Gguf!.PersistentTargetWeightFormat,
                    Convert.ToHexString(SHA256.HashData(output)).ToLowerInvariant(),
                    checked((ulong)output.Length)));
        }

        public async Task<OptimizationSupportCode> ValidateRuntimeProfileAsync(
            OptimizationExecutionPlan plan,
            string sourcePath,
            IProgress<GgufOptimizationOutputValidationPhase> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RuntimeCalls++;
            RuntimeEntered.TrySetResult();
            progress.Report(GgufOptimizationOutputValidationPhase.Validate);
            progress.Report(GgufOptimizationOutputValidationPhase.SmokeTest);
            if (blockRuntime)
            {
                await ReleaseRuntime.Task.WaitAsync(cancellationToken);
            }
            if (runtimeSupportCode == OptimizationSupportCode.None)
            {
                progress.Report(GgufOptimizationOutputValidationPhase.Reinspect);
            }
            return runtimeSupportCode;
        }
    }

    private sealed class MutatingValidator : IGgufOptimizationOutputValidator
    {
        public Task<GgufOptimizationOutputValidationResult> ValidateAsync(
            OptimizationExecutionPlan plan,
            string outputPath,
            IProgress<GgufOptimizationOutputValidationPhase> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] validated = File.ReadAllBytes(outputPath);
            string sha256 = Convert.ToHexString(SHA256.HashData(validated))
                .ToLowerInvariant();
            File.AppendAllText(outputPath, "post-validation-mutation");
            return Task.FromResult(
                GgufOptimizationOutputValidationResult.Success(
                    plan.ExecutionPayload.Gguf!.PersistentTargetWeightFormat,
                    sha256,
                    checked((ulong)validated.Length)));
        }

        public Task<OptimizationSupportCode> ValidateRuntimeProfileAsync(
            OptimizationExecutionPlan plan,
            string sourcePath,
            IProgress<GgufOptimizationOutputValidationPhase> progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(OptimizationSupportCode.UnexpectedFailure);
    }

    private sealed class FixedOutputValidator(string sha256, ulong lengthBytes)
        : IGgufOptimizationOutputValidator
    {
        public Task<GgufOptimizationOutputValidationResult> ValidateAsync(
            OptimizationExecutionPlan plan,
            string outputPath,
            IProgress<GgufOptimizationOutputValidationPhase> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GgufOptimizationOutputValidationResult.Success(
                GgufWeightFormat.Q3KM,
                sha256,
                lengthBytes));
        }

        public Task<OptimizationSupportCode> ValidateRuntimeProfileAsync(
            OptimizationExecutionPlan plan,
            string sourcePath,
            IProgress<GgufOptimizationOutputValidationPhase> progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(OptimizationSupportCode.UnexpectedFailure);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
