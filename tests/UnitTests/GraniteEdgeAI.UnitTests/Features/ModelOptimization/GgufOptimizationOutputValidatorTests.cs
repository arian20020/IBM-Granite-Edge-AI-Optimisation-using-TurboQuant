using System.Reflection;
using System.Security.Cryptography;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class GgufOptimizationOutputValidatorTests
{
    [TestMethod]
    public async Task ExactTargetSurvivesInspectionSmokeAndReinspection()
    {
        using var fixture = new ValidatorFixture();
        var phases = new List<GgufOptimizationOutputValidationPhase>();
        var validator = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (_, _, _, _) => Task.CompletedTask);

        GgufOptimizationOutputValidationResult result = await validator.ValidateAsync(
            fixture.Plan,
            fixture.OutputPath,
            new InlineProgress<GgufOptimizationOutputValidationPhase>(phases.Add),
            CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.None, result.SupportCode);
        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(GgufWeightFormat.Q3KM, result.ValidatedTarget);
        Assert.AreEqual(64UL, result.OutputLengthBytes);
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(new byte[64])).ToLowerInvariant(),
            result.OutputSha256);
        CollectionAssert.AreEqual(
            new[]
            {
                GgufOptimizationOutputValidationPhase.Validate,
                GgufOptimizationOutputValidationPhase.SmokeTest,
                GgufOptimizationOutputValidationPhase.Reinspect,
            },
            phases);
    }

    [TestMethod]
    public async Task WrongTargetFormatFailsValidationBeforeSmoke()
    {
        using var fixture = new ValidatorFixture();
        int smokeCalls = 0;
        var validator = fixture.Validator(
            scanAsync: (_, _) => Task.FromResult(SuccessfulScan("Q4_K_M", 64)),
            smokeAsync: (_, _, _, _) =>
            {
                smokeCalls++;
                return Task.CompletedTask;
            });

        GgufOptimizationOutputValidationResult result = await validator.ValidateAsync(
            fixture.Plan,
            fixture.OutputPath,
            new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.ValidationFailed, result.SupportCode);
        Assert.AreEqual(0, smokeCalls);
    }

    [TestMethod]
    public async Task EmptyOutputFailsValidationBeforeInspection()
    {
        using var fixture = new ValidatorFixture(empty: true);
        int scanCalls = 0;
        var validator = fixture.Validator(
            scanAsync: (_, _) =>
            {
                scanCalls++;
                return Task.FromResult(SuccessfulScan("Q3_K_M", 0));
            },
            smokeAsync: (_, _, _, _) => Task.CompletedTask);

        GgufOptimizationOutputValidationResult result = await validator.ValidateAsync(
            fixture.Plan,
            fixture.OutputPath,
            new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.ValidationFailed, result.SupportCode);
        Assert.AreEqual(0, scanCalls);
    }

    [TestMethod]
    public async Task OutputMutationDuringSmokeFailsReinspection()
    {
        using var fixture = new ValidatorFixture();
        var validator = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (_, path, _, _) =>
            {
                File.AppendAllText(path, "changed");
                return Task.CompletedTask;
            });

        GgufOptimizationOutputValidationResult result = await validator.ValidateAsync(
            fixture.Plan,
            fixture.OutputPath,
            new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.ReinspectionFailed, result.SupportCode);
    }

    [TestMethod]
    public async Task RuntimeStartupFailureMapsToSmokeTestFailed()
    {
        using var fixture = new ValidatorFixture();
        var validator = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (_, _, _, _) => throw new InvalidOperationException("runtime failed"));

        GgufOptimizationOutputValidationResult result = await validator.ValidateAsync(
            fixture.Plan,
            fixture.OutputPath,
            new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.SmokeTestFailed, result.SupportCode);
    }

    [TestMethod]
    public async Task InternalSmokeDeadlineMapsToSmokeTestFailedNotCancellation()
    {
        using var fixture = new ValidatorFixture();
        bool cleanupJoined = false;
        var validator = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: async (_, _, _, token) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    cleanupJoined = true;
                }
            },
            smokeTimeout: TimeSpan.FromMilliseconds(20));

        GgufOptimizationOutputValidationResult result = await validator.ValidateAsync(
            fixture.Plan,
            fixture.OutputPath,
            new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.SmokeTestFailed, result.SupportCode);
        Assert.IsTrue(cleanupJoined);
    }

    [TestMethod]
    public async Task CancellationFromInspectionPropagates()
    {
        using var fixture = new ValidatorFixture();
        var validator = fixture.Validator(
            scanAsync: (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(SuccessfulScan("Q3_K_M", 64));
            },
            smokeAsync: (_, _, _, _) => Task.CompletedTask);
        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            validator.ValidateAsync(
                fixture.Plan,
                fixture.OutputPath,
                new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
                cancellation.Token));
    }

    [TestMethod]
    public async Task CallerCancellationDuringSmokePropagates()
    {
        using var fixture = new ValidatorFixture();
        using var cancellation = new CancellationTokenSource();
        var validator = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (_, _, _, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            validator.ValidateAsync(
                fixture.Plan,
                fixture.OutputPath,
                new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
                cancellation.Token));
    }

    [TestMethod]
    public async Task RuntimeOnlyProfileStartsExactSourceThenReinspectsBeforeSuccess()
    {
        using var fixture = new ValidatorFixture();
        OptimizationExecutionPlan plan = fixture.RuntimePlan();
        var phases = new List<GgufOptimizationOutputValidationPhase>();
        int smokeCalls = 0;
        var validator = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (observedPlan, path, sha256, token) =>
            {
                token.ThrowIfCancellationRequested();
                smokeCalls++;
                Assert.AreSame(plan, observedPlan);
                Assert.AreEqual(fixture.OutputPath, path);
                Assert.AreEqual(plan.Binding.ModelSha256, sha256);
                return Task.CompletedTask;
            });

        OptimizationSupportCode result =
            await validator.ValidateRuntimeProfileAsync(
                plan,
                fixture.OutputPath,
                new InlineProgress<GgufOptimizationOutputValidationPhase>(phases.Add),
                CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.None, result);
        Assert.AreEqual(1, smokeCalls);
        CollectionAssert.AreEqual(
            new[]
            {
                GgufOptimizationOutputValidationPhase.Validate,
                GgufOptimizationOutputValidationPhase.SmokeTest,
                GgufOptimizationOutputValidationPhase.Reinspect,
            },
            phases);
    }

    [TestMethod]
    public async Task RuntimeOnlySourceMismatchNeverStartsRuntime()
    {
        using var fixture = new ValidatorFixture();
        OptimizationExecutionPlan plan = fixture.RuntimePlan();
        File.AppendAllText(fixture.OutputPath, "changed");
        int smokeCalls = 0;
        var validator = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (_, _, _, _) =>
            {
                smokeCalls++;
                return Task.CompletedTask;
            });

        OptimizationSupportCode result =
            await validator.ValidateRuntimeProfileAsync(
                plan,
                fixture.OutputPath,
                new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
                CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.SourceIdentityMismatch, result);
        Assert.AreEqual(0, smokeCalls);
    }

    [TestMethod]
    public async Task RuntimeOnlyRejectsPersistentOrCandidatePayloadMismatch()
    {
        using var fixture = new ValidatorFixture();
        int smokeCalls = 0;
        var validator = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (_, _, _, _) =>
            {
                smokeCalls++;
                return Task.CompletedTask;
            });

        OptimizationSupportCode persistent =
            await validator.ValidateRuntimeProfileAsync(
                fixture.Plan,
                fixture.OutputPath,
                new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
                CancellationToken.None);
        OptimizationSupportCode mismatched =
            await validator.ValidateRuntimeProfileAsync(
                fixture.RuntimePlan(GgufCacheType.Q8Zero),
                fixture.OutputPath,
                new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
                CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.ValidationFailed, persistent);
        Assert.AreEqual(OptimizationSupportCode.ValidationFailed, mismatched);
        Assert.AreEqual(0, smokeCalls);
    }

    [TestMethod]
    public async Task RuntimeOnlyMutationDuringSmokeRejectsPostRuntimeSuccess()
    {
        using var fixture = new ValidatorFixture();
        OptimizationExecutionPlan plan = fixture.RuntimePlan();
        var validator = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (_, path, _, _) =>
            {
                File.AppendAllText(path, "changed-during-smoke");
                return Task.CompletedTask;
            });

        OptimizationSupportCode result =
            await validator.ValidateRuntimeProfileAsync(
                plan,
                fixture.OutputPath,
                new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
                CancellationToken.None);

        Assert.AreEqual(OptimizationSupportCode.SourceIdentityMismatch, result);
    }

    [TestMethod]
    public async Task RuntimeOnlyRuntimeFailureAndCallerCancellationRemainDistinct()
    {
        using var fixture = new ValidatorFixture();
        OptimizationExecutionPlan plan = fixture.RuntimePlan();
        var failure = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (_, _, _, _) =>
                throw new InvalidOperationException("runtime failed"));

        OptimizationSupportCode result =
            await failure.ValidateRuntimeProfileAsync(
                plan,
                fixture.OutputPath,
                new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
                CancellationToken.None);
        Assert.AreEqual(OptimizationSupportCode.SmokeTestFailed, result);

        bool timedOutSmokeJoined = false;
        var timedOut = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: async (_, _, _, token) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    timedOutSmokeJoined = true;
                }
            },
            smokeTimeout: TimeSpan.FromMilliseconds(20));
        result = await timedOut.ValidateRuntimeProfileAsync(
            plan,
            fixture.OutputPath,
            new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
            CancellationToken.None);
        Assert.AreEqual(OptimizationSupportCode.SmokeTestFailed, result);
        Assert.IsTrue(timedOutSmokeJoined);

        using var cancellation = new CancellationTokenSource();
        var cancelled = fixture.Validator(
            scanAsync: SuccessfulQ3ScanAsync,
            smokeAsync: (_, _, _, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            cancelled.ValidateRuntimeProfileAsync(
                plan,
                fixture.OutputPath,
                new InlineProgress<GgufOptimizationOutputValidationPhase>(_ => { }),
                cancellation.Token));
    }

    private static Task<ModelQuickScanResult> SuccessfulQ3ScanAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SuccessfulScan("Q3_K_M", new FileInfo(path).Length));
    }

    private static ModelQuickScanResult SuccessfulScan(
        string quantization,
        long length) => ModelQuickScanResult.CreateSuccess(
            "validated-output",
            "granite",
            "3B",
            quantization,
            Math.Max(1, length),
            4096,
            3,
            DateTimeOffset.UnixEpoch);

    private sealed class ValidatorFixture : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory(
            "GgufOutputValidator-").FullName;

        internal ValidatorFixture(bool empty = false)
        {
            OutputPath = Path.Combine(_root, "model.gguf");
            File.WriteAllBytes(OutputPath, empty ? [] : new byte[64]);
            byte[] source = "source"u8.ToArray();
            Plan = OptimizationSelectionHandoffTests.PersistentPlanForSource(
                Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant(),
                (ulong)source.Length);
        }

        internal string OutputPath { get; }
        internal OptimizationExecutionPlan Plan { get; }

        internal OptimizationExecutionPlan RuntimePlan(
            GgufCacheType payloadCache = GgufCacheType.F16)
        {
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
                        payloadCache,
                        payloadCache,
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
                OptimizationJourneyBinding.Create(
                    Plan.Binding.ModelInspectionRunId,
                    Plan.Binding.ModelInspectionHandoffId,
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(OutputPath)))
                        .ToLowerInvariant(),
                    checked((ulong)new FileInfo(OutputPath).Length),
                    Plan.Binding.ProductHardwareRunId,
                    Plan.Binding.HardwareSnapshotSha256),
                Plan.CapabilitySnapshot,
                Plan.Workload,
                candidate,
                payload,
                OptimizationPreferenceSelection.Automatic(),
                false,
                "3333333333333333333333333333333333333333333333333333333333333333",
                DateTimeOffset.UnixEpoch,
            ]);
        }

        internal GgufOptimizationOutputValidator Validator(
            Func<string, CancellationToken, Task<ModelQuickScanResult>> scanAsync,
            Func<OptimizationExecutionPlan, string, string, CancellationToken, Task>
                smokeAsync,
            TimeSpan? smokeTimeout = null) => new(
                    "unused-runtime-root",
                    "{}"u8.ToArray(),
                    scanAsync,
                    smokeAsync,
                    smokeTimeout);

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    private sealed class InlineProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }
}
