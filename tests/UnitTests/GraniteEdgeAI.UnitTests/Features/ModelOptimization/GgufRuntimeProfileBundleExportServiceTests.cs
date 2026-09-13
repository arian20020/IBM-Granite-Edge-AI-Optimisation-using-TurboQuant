using System.Reflection;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class GgufRuntimeProfileBundleExportServiceTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;
    private const string SourceSha256 =
        "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
    private const string HardwareSha256 =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [TestMethod]
    public void RuntimeTargetAndReceiptCarryNoPersistentOutputIdentity()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();

        VerifiedGgufRuntimeBundleExportTarget target =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result);
        var receipt = new GgufRuntimeBundleExportReceipt(
            target,
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            512,
            target.SourceLengthBytes + 1024);

        Assert.AreEqual(OptimizationExportTargetKind.GgufRuntimeBundle,
            target.Kind);
        Assert.AreEqual(OptimizationExportReceiptKind.GgufRuntimeBundle,
            receipt.Kind);
        Assert.AreEqual(result.ExecutionId, receipt.ExecutionId);
        Assert.AreEqual(target.SourceLengthBytes + 1024,
            receipt.BundleLengthBytes);
        Assert.IsFalse(typeof(GgufRuntimeBundleExportReceipt)
            .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)
            .Any(property => property.Name is "OutputIdentity"
                or "OutputManifestSha256"));
    }

    [TestMethod]
    public void RuntimeReceiptRejectsBoundaryAndOverflowLengths()
    {
        (OptimizationExecutionPlan basis, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        VerifiedGgufRuntimeBundleExportTarget target =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(basis, result);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new GgufRuntimeBundleExportReceipt(
                target,
                new string('e', 64),
                512,
                basis.Binding.ModelLengthBytes + 512));

        OptimizationJourneyBinding oversizedBinding =
            OptimizationJourneyBinding.Create(
                basis.Binding.ModelInspectionRunId,
                basis.Binding.ModelInspectionHandoffId,
                basis.Binding.ModelSha256,
                ulong.MaxValue,
                basis.Binding.ProductHardwareRunId,
                basis.Binding.HardwareSnapshotSha256);
        OptimizationExecutionPlan oversized = ClonePlan(
            basis, binding: oversizedBinding);
        VerifiedGgufRuntimeBundleExportTarget oversizedTarget =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(
                oversized, RuntimeResult(oversized));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new GgufRuntimeBundleExportReceipt(
                oversizedTarget,
                new string('e', 64),
                1,
                ulong.MaxValue));
    }

    [TestMethod]
    public void RuntimeBundleNameUsesTypedUnchangedWeightAndCacheTokens()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        string parent = Path.Combine(
            Path.GetTempPath(), $"granite-runtime-name-{Guid.NewGuid():N}");
        Directory.CreateDirectory(parent);
        try
        {
            Assert.IsTrue(GgufRuntimeProfileBundleExportService
                .TryCreateAbsentDestination(parent, plan, result,
                    out string? destination));
            Assert.AreEqual(
                Path.Combine(parent, "GGUF-Original-Q8_0"), destination);
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(parent).Count());

            CollectionAssert.AreEqual(
                new[] { "F16", "Q8_0", "TQ2", "TQ3", "TQ4" },
                new[]
                {
                    GgufRuntimeProfileBundleExportService.CacheToken(
                        GgufCacheType.F16, GgufCacheType.F16),
                    GgufRuntimeProfileBundleExportService.CacheToken(
                        GgufCacheType.Q8Zero, GgufCacheType.Q8Zero),
                    GgufRuntimeProfileBundleExportService.CacheToken(
                        GgufCacheType.Turbo2, GgufCacheType.Turbo2),
                    GgufRuntimeProfileBundleExportService.CacheToken(
                        GgufCacheType.Turbo3, GgufCacheType.Turbo3),
                    GgufRuntimeProfileBundleExportService.CacheToken(
                        GgufCacheType.Turbo4, GgufCacheType.Turbo4),
                });
            Assert.IsNull(GgufRuntimeProfileBundleExportService.CacheToken(
                GgufCacheType.F16, GgufCacheType.Q8Zero));
            Assert.IsNull(GgufRuntimeProfileBundleExportService.CacheToken(
                GgufCacheType.Q4Zero, GgufCacheType.Q4Zero));
            Assert.IsNull(GgufRuntimeProfileBundleExportService.CacheToken(
                (GgufCacheType)999, (GgufCacheType)999));
        }
        finally
        {
            Directory.Delete(parent);
        }
    }

    [TestMethod]
    public void RuntimeBundleNameChecksFilesDirectoriesAndBoundedExhaustion()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        string parent = Path.Combine(
            Path.GetTempPath(), $"granite-runtime-collision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(parent);
        try
        {
            File.WriteAllText(Path.Combine(parent, "GGUF-Original-Q8_0"), "x");
            Directory.CreateDirectory(Path.Combine(parent, "GGUF-Original-Q8_0-2"));
            Assert.IsTrue(GgufRuntimeProfileBundleExportService
                .TryCreateAbsentDestination(parent, plan, result,
                    out string? destination));
            Assert.AreEqual(
                Path.Combine(parent, "GGUF-Original-Q8_0-3"), destination);

            for (int suffix = 3; suffix <= 1000; suffix++)
            {
                Directory.CreateDirectory(Path.Combine(
                    parent, $"GGUF-Original-Q8_0-{suffix}"));
            }
            Assert.IsFalse(GgufRuntimeProfileBundleExportService
                .TryCreateAbsentDestination(parent, plan, result, out _));
        }
        finally
        {
            foreach (string path in Directory.EnumerateFiles(parent))
            {
                File.Delete(path);
            }
            foreach (string path in Directory.EnumerateDirectories(parent))
            {
                Directory.Delete(path);
            }
            Directory.Delete(parent);
        }
    }

    [TestMethod]
    public void RuntimeBundleNameRejectsInvalidParentAndNonRuntimeBinding()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        Assert.IsFalse(GgufRuntimeProfileBundleExportService
            .TryCreateAbsentDestination("relative", plan, result, out _));
        Assert.IsFalse(GgufRuntimeProfileBundleExportService
            .TryCreateAbsentDestination(
                @"\\server\share", plan, result, out _));
        Assert.IsFalse(GgufRuntimeProfileBundleExportService
            .TryCreateAbsentDestination(
                @"\\?\C:\exports", plan, result, out _));
        Assert.IsFalse(GgufRuntimeProfileBundleExportService
            .TryCreateAbsentDestination(Path.GetTempPath(), plan,
                RuntimeResult(ClonePlan(plan,
                    configurationSha256: new string('d', 64))), out _));

        OptimizationExecutionPlan persistent =
            OptimizationSelectionHandoffTests.PersistentPlanForSource(
                SourceSha256,
                VerifiedGgufOptimizationEvidence.SourceModelLengthBytes);
        OptimizationExecutionResult persistentResult =
            OptimizationExecutionResult.Succeeded(
                persistent,
                "persistent-output",
                new string('e', 64),
                4096,
                sourceUnchanged: true,
                DateTimeOffset.UnixEpoch,
                Guid.NewGuid());
        Assert.IsFalse(GgufRuntimeProfileBundleExportService
            .TryCreateAbsentDestination(
                Path.GetTempPath(), persistent, persistentResult, out _));

        OptimizationExecutionPlan openVino =
            OptimizationSelectionHandoffTests
                .RequiredJourneyEntry(OptimizationRoute.OpenVino)
                .OptimizationHandoff.Plan;
        OptimizationExecutionResult openVinoResult =
            OptimizationExecutionResult.Succeeded(
                openVino,
                "openvino-output",
                new string('f', 64),
                4096,
                sourceUnchanged: true,
                DateTimeOffset.UnixEpoch,
                Guid.NewGuid());
        Assert.IsFalse(GgufRuntimeProfileBundleExportService
            .TryCreateAbsentDestination(
                Path.GetTempPath(), openVino, openVinoResult, out _));
    }

    [TestMethod]
    public async Task ExactRuntimeProfileIsPickedExportedAndReceipted()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        VerifiedGgufRuntimeBundleExportTarget target =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result);
        var progress = new List<OptimizationExportProgress>();
        int pickerCalls = 0;
        int exportCalls = 0;
        var service = new GgufRuntimeProfileBundleExportService(
            plan,
            result,
            _ => new MemoryStream(),
            (route, cancellationToken) =>
            {
                Assert.AreEqual(OptimizationRoute.Gguf, route);
                cancellationToken.ThrowIfCancellationRequested();
                pickerCalls++;
                return Task.FromResult<string?>("D:\\Exports\\runtime-bundle");
            },
            (candidate, destination, maximumBytes, primitiveProgress,
                cancellationToken) =>
            {
                Assert.AreSame(result, candidate);
                Assert.AreEqual("D:\\Exports\\runtime-bundle", destination);
                Assert.IsGreaterThan(plan.Binding.ModelLengthBytes, maximumBytes);
                cancellationToken.ThrowIfCancellationRequested();
                exportCalls++;
                primitiveProgress.Report(
                    GgufRuntimeProfileBundleExportStage.CopyingModel);
                primitiveProgress.Report(
                    GgufRuntimeProfileBundleExportStage.Verifying);
                primitiveProgress.Report(
                    GgufRuntimeProfileBundleExportStage.Publishing);
                return Task.FromResult(
                    GgufRuntimeProfileBundleExportResult.Succeeded(
                        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                        512,
                        plan.Binding.ModelLengthBytes + 1024));
            });

        OptimizationExportResult exported = await service.ExportAsync(
            target,
            new RecordingProgress(progress),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExportResultKind.Succeeded, exported.Kind);
        Assert.IsNull(exported.Receipt);
        Assert.IsNotNull(exported.RuntimeBundleReceipt);
        Assert.AreEqual(1, pickerCalls);
        Assert.AreEqual(1, exportCalls);
        CollectionAssert.AreEqual(
            new[] { OptimizationExportStage.Writing,
                    OptimizationExportStage.Verifying,
                    OptimizationExportStage.Publishing },
            progress.Select(item => item.Stage).Distinct().ToArray());
    }

    [TestMethod]
    public async Task ForeignRuntimeTargetFailsBeforePickerOrDestination()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        (OptimizationExecutionPlan foreignPlan,
            OptimizationExecutionResult foreignResult) = RuntimeProfileResult();
        int pickerCalls = 0;
        int exportCalls = 0;
        var service = new GgufRuntimeProfileBundleExportService(
            plan,
            result,
            _ => new MemoryStream(),
            (_, _) =>
            {
                pickerCalls++;
                return Task.FromResult<string?>("D:\\Exports\\foreign");
            },
            (_, _, _, _, _) =>
            {
                exportCalls++;
                return Task.FromResult(GgufRuntimeProfileBundleExportResult.For(
                    GgufRuntimeProfileBundleExportDisposition.Failed));
            });

        OptimizationExportResult exported = await service.ExportAsync(
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(
                foreignPlan, foreignResult),
            new Progress<OptimizationExportProgress>(),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExportResultKind.Failed, exported.Kind);
        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch,
            exported.Failure);
        Assert.AreEqual(0, pickerCalls);
        Assert.AreEqual(0, exportCalls);
    }

    [TestMethod]
    public async Task UnavailableSourceCustodyFailsBeforePickerOrDestination()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        int pickerCalls = 0;
        int exportCalls = 0;
        var service = new GgufRuntimeProfileBundleExportService(
            plan,
            result,
            _ => null,
            (_, _) =>
            {
                pickerCalls++;
                return Task.FromResult<string?>("D:\\Exports\\missing-source");
            },
            (_, _, _, _, _) =>
            {
                exportCalls++;
                return Task.FromResult(GgufRuntimeProfileBundleExportResult.For(
                    GgufRuntimeProfileBundleExportDisposition.Failed));
            });

        OptimizationExportResult exported = await service.ExportAsync(
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result),
            new Progress<OptimizationExportProgress>(),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExportResultKind.Failed, exported.Kind);
        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch,
            exported.Failure);
        Assert.AreEqual(0, pickerCalls);
        Assert.AreEqual(0, exportCalls);
    }

    [TestMethod]
    public void MutatedPlanPayloadIsRejectedBeforeCustodyOrPicker()
    {
        (OptimizationExecutionPlan plan, _) = RuntimeProfileResult();
        GgufExecutionPayload original = plan.ExecutionPayload.Gguf!;
        OptimizationExecutionPlan[] malformed =
        {
            ClonePlan(plan, configurationSha256: new string('d', 64)),
            ClonePlan(plan, payload: MutatePayload(original,
                deviceId: "foreign-device")),
            ClonePlan(plan, payload: MutatePayload(original,
                gpuLayerCount: 1)),
            ClonePlan(plan, payload: MutatePayload(original,
                flashAttention: false)),
            ClonePlan(plan, payload: MutatePayload(original,
                threadCount: 5)),
            ClonePlan(plan, payload: MutatePayload(original,
                batchSize: 129)),
            ClonePlan(plan, payload: MutatePayload(original,
                profileId: "foreign-profile")),
            ClonePlan(plan, payload: MutatePayload(original,
                runtimeBuildId: "foreign-runtime")),
            ClonePlan(plan, payload: MutatePayload(original,
                runtimeSourceCommit:
                    "fedcba9876543210fedcba9876543210fedcba98")),
        };
        int custodyCalls = 0;
        int pickerCalls = 0;

        foreach (OptimizationExecutionPlan candidate in malformed)
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new GgufRuntimeProfileBundleExportService(
                    candidate,
                    RuntimeResult(candidate),
                    _ =>
                    {
                        custodyCalls++;
                        return new MemoryStream();
                    },
                    (_, _) =>
                    {
                        pickerCalls++;
                        return Task.FromResult<string?>(
                            "D:\\Exports\\malformed");
                    },
                    (_, _, _, _, _) => throw new AssertFailedException(
                        "A malformed plan must not reach the destination.")),
                candidate.ConfigurationSha256);
        }

        Assert.AreEqual(0, custodyCalls);
        Assert.AreEqual(0, pickerCalls);
    }

    [TestMethod]
    public async Task ReceiptLengthsMustIncludeModelManifestAndProfile()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        const ulong manifestLength = 512;
        var service = new GgufRuntimeProfileBundleExportService(
            plan,
            result,
            _ => new MemoryStream(),
            (_, _) => Task.FromResult<string?>("D:\\Exports\\boundary"),
            (_, _, _, _, _) => Task.FromResult(
                GgufRuntimeProfileBundleExportResult.Succeeded(
                    new string('e', 64),
                    manifestLength,
                    plan.Binding.ModelLengthBytes + manifestLength)));

        OptimizationExportResult exported = await service.ExportAsync(
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result),
            new Progress<OptimizationExportProgress>(),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExportResultKind.Failed, exported.Kind);
        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch,
            exported.Failure);
        Assert.IsNull(exported.RuntimeBundleReceipt);
    }

    [TestMethod]
    public async Task ReceiptLengthArithmeticOverflowFailsClosed()
    {
        (OptimizationExecutionPlan basis, _) = RuntimeProfileResult();
        OptimizationJourneyBinding oversizedBinding =
            OptimizationJourneyBinding.Create(
                basis.Binding.ModelInspectionRunId,
                basis.Binding.ModelInspectionHandoffId,
                basis.Binding.ModelSha256,
                ulong.MaxValue,
                basis.Binding.ProductHardwareRunId,
                basis.Binding.HardwareSnapshotSha256);
        OptimizationExecutionPlan plan = ClonePlan(
            basis, binding: oversizedBinding);
        OptimizationExecutionResult result = RuntimeResult(plan);
        var service = new GgufRuntimeProfileBundleExportService(
            plan,
            result,
            _ => new MemoryStream(),
            (_, _) => Task.FromResult<string?>("D:\\Exports\\overflow"),
            (_, _, _, _, _) => Task.FromResult(
                GgufRuntimeProfileBundleExportResult.Succeeded(
                    new string('e', 64), 1, ulong.MaxValue)));

        OptimizationExportResult exported = await service.ExportAsync(
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result),
            new Progress<OptimizationExportProgress>(),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExportResultKind.Failed, exported.Kind);
        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch,
            exported.Failure);
        Assert.IsNull(exported.RuntimeBundleReceipt);
    }

    [TestMethod]
    public async Task CancelledRuntimePickerNeverInvokesDestination()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        int exportCalls = 0;
        var service = new GgufRuntimeProfileBundleExportService(
            plan,
            result,
            _ => new MemoryStream(),
            (_, _) => Task.FromResult<string?>(null),
            (_, _, _, _, _) =>
            {
                exportCalls++;
                throw new AssertFailedException(
                    "A cancelled picker must not invoke the destination.");
            });

        OptimizationExportResult exported = await service.ExportAsync(
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result),
            new Progress<OptimizationExportProgress>(),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExportResultKind.Cancelled, exported.Kind);
        Assert.AreEqual(0, exportCalls);
    }

    [TestMethod]
    public void PersistentResultCannotConstructRuntimeBundleTarget()
    {
        OptimizationExecutionResult persistent =
            OptimizationExecutionResult.Succeeded(
                OptimizationSelectionHandoffTests.PersistentPlanForSource(
                    SourceSha256, 4096),
                "persistent-output",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                4096,
                sourceUnchanged: true,
                DateTimeOffset.UnixEpoch,
                Guid.NewGuid());

        Assert.ThrowsExactly<ArgumentException>(() =>
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(
                OptimizationSelectionHandoffTests.PersistentPlanForSource(
                    SourceSha256, 4096),
                persistent));
    }

    [TestMethod]
    public async Task PrimitiveFailureDispositionMapsWithoutReceipt()
    {
        var cases = new[]
        {
            (GgufRuntimeProfileBundleExportDisposition.ResultRejected,
                OptimizationExportFailure.IntegrityMismatch),
            (GgufRuntimeProfileBundleExportDisposition.SourceUnavailable,
                OptimizationExportFailure.IntegrityMismatch),
            (GgufRuntimeProfileBundleExportDisposition.SourceChanged,
                OptimizationExportFailure.IntegrityMismatch),
            (GgufRuntimeProfileBundleExportDisposition.DestinationRejected,
                OptimizationExportFailure.DestinationUnavailable),
            (GgufRuntimeProfileBundleExportDisposition.DestinationExists,
                OptimizationExportFailure.DestinationUnavailable),
            (GgufRuntimeProfileBundleExportDisposition.Oversized,
                OptimizationExportFailure.InsufficientSpace),
            (GgufRuntimeProfileBundleExportDisposition.CleanupFailed,
                OptimizationExportFailure.CleanupFailure),
            (GgufRuntimeProfileBundleExportDisposition.Failed,
                OptimizationExportFailure.PublicationFailure),
        };

        foreach ((GgufRuntimeProfileBundleExportDisposition disposition,
                     OptimizationExportFailure expected) in cases)
        {
            (OptimizationExecutionPlan plan,
                OptimizationExecutionResult result) = RuntimeProfileResult();
            var service = new GgufRuntimeProfileBundleExportService(
                plan,
                result,
                _ => new MemoryStream(),
                (_, _) => Task.FromResult<string?>("D:\\Exports\\bundle"),
                (_, _, _, _, _) => Task.FromResult(
                    GgufRuntimeProfileBundleExportResult.For(disposition)));

            OptimizationExportResult exported = await service.ExportAsync(
                VerifiedGgufRuntimeBundleExportTarget.FromExecution(
                    plan, result),
                new Progress<OptimizationExportProgress>(),
                CancellationToken.None);

            Assert.AreEqual(
                OptimizationExportResultKind.Failed,
                exported.Kind,
                disposition.ToString());
            Assert.AreEqual(expected, exported.Failure,
                disposition.ToString());
            Assert.IsNull(exported.Receipt, disposition.ToString());
            Assert.IsNull(exported.RuntimeBundleReceipt,
                disposition.ToString());
        }
    }

    [TestMethod]
    public async Task DestinationCancellationPreservesTheCallerToken()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            RuntimeProfileResult();
        using var cancellation = new CancellationTokenSource();
        var service = new GgufRuntimeProfileBundleExportService(
            plan,
            result,
            _ => new MemoryStream(),
            (_, _) => Task.FromResult<string?>("D:\\Exports\\bundle"),
            (_, _, _, _, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                throw new AssertFailedException("Cancellation was not observed.");
            });

        OperationCanceledException error =
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                service.ExportAsync(
                    VerifiedGgufRuntimeBundleExportTarget.FromExecution(
                        plan, result),
                    new Progress<OptimizationExportProgress>(),
                    cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
    }

    internal static (OptimizationExecutionPlan Plan,
        OptimizationExecutionResult Result) RuntimeProfileResult()
    {
        const string evidenceId = "GGUF-V5-CPU-Q8-01";
        const string runtimeBuild =
            PublishedGgufOptimizationEvidence.RuntimeBuildId;
        const string runtimeCommit =
            PublishedGgufOptimizationEvidence.RuntimeSourceCommit;
        const ulong sourceLength =
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes;
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "22222222222242228222222222222222",
            "33333333333343338333333333333333",
            SourceSha256,
            sourceLength,
            "44444444444444448444444444444444",
            HardwareSha256);
        GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);
        OptimizationExecutionPayload payload =
            OptimizationExecutionPayload.ForGguf(
                GgufExecutionPayload.Create(
                    runtimeBuild,
                    runtimeCommit,
                    GgufRuntimeBackend.Cpu,
                    "CPU",
                    4096,
                    GgufCacheType.Q8Zero,
                    GgufCacheType.Q8Zero,
                    0,
                    true,
                    4,
                    512,
                    "Measured",
                    "cpu",
                    256,
                    GgufWeightFormat.Imported));
        var evidenceIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "GGUF-V5-CPU-F16-01",
            evidenceId,
        };
        GgufRuntimeAuthority runtimeAuthority = GgufRuntimeAuthority.Create(
            runtimeBuild,
            runtimeCommit,
            VerifiedGgufOptimizationEvidence.ExecutionProfiles(evidenceIds));
        OptimizationCapabilitySnapshot capability =
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-capability-runtime-profile",
                new string('c', 64),
                GgufCapabilityPayload.Create(
                    runtimeBuild,
                    VerifiedGgufOptimizationEvidence.Admissions(evidenceIds),
                    runtimeAuthority: runtimeAuthority));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "runtime-profile-workload",
            4096,
            OptimizationAssessment.Acceptable,
            [ContextTokenCount.FromTokens(4096)]);
        var current = CompatibilityCurrentModelInput.ForGguf(
            GgufCompatibilityModelInput.Create(
                sourceLength, 40, 2560, 40, 8, 131072, 15, 2,
                VerifiedGgufOptimizationEvidence.ParameterCount),
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None));
        var hardware = CompatibilityHardwareInput.Create(
            TotalPhysicalMemory.FromBytes(64 * GiB),
            0,
            500 * GiB,
            [DeviceRouteId.Cpu],
            [CompatibilityBackend.Cpu]);
        var qualityEvidence = new OptimizationEvidenceCatalog(
            VerifiedGgufOptimizationEvidence.Records(
                SourceSha256,
                sourceLength,
                VerifiedGgufOptimizationEvidence.ParameterCount,
                "5E2204C791A44D2FC696F0F37EBB5568DDD40D7BD6BDE869D4844E029D326941",
                runtimeBuild,
                runtimeCommit));
        CompatibilityProductionInput input = CompatibilityProductionInput.Create(
            Guid.Parse(binding.ModelInspectionRunId),
            Guid.Parse(binding.ProductHardwareRunId),
            current,
            CompatibilityJourneyAuthorityInput.Create(
                Guid.Parse(binding.ModelInspectionHandoffId),
                binding.ModelSha256,
                CompatibilityFactDigest.ComputeModel(current),
                HardwareSha256,
                CompatibilityFactDigest.ComputeHardware(hardware)),
            hardware,
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(48 * GiB),
                0,
                500 * GiB,
                DateTimeOffset.UnixEpoch),
            CompatibilityOptimizationProductionInput.Create(
                capability,
                workload,
                binding,
                new HashSet<string>(StringComparer.Ordinal),
                qualityEvidence));
        OptimizationIssuanceAuthority authority =
            CompatibilityEngine.CreateOptimizationIssuanceAuthority(
                input, DateTimeOffset.UnixEpoch);
        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            input,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        CompatibilityPlanningSession session = evaluation.PlanningSession
            ?? throw new InvalidOperationException(
                "The real compatibility evaluation did not retain planning authority.");
        var mode = (evaluation.Screen.Optimization
            ?? evaluation.OptionalOptimization)!
            .SafeSliderModes.Single(candidate =>
                candidate.Mode.GgufKvCache == GgufKvCacheFormat.Q8_0);
        OptimizationExecutionPlan plan = session.Issue(
            OptimizationPreferenceSelection.Exact(mode.CandidateIdentity),
            new FixedPayloadComposer(payload),
            authority,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        return (plan, RuntimeResult(plan));
    }

    private static OptimizationExecutionResult RuntimeResult(
        OptimizationExecutionPlan plan) => OptimizationExecutionResult.Succeeded(
            plan,
            $"gguf-profile-{plan.OptimizationPlanId:N}",
            plan.ConfigurationSha256,
            0,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch,
            Guid.NewGuid());

    private static GgufExecutionPayload MutatePayload(
        GgufExecutionPayload payload,
        string? runtimeBuildId = null,
        string? runtimeSourceCommit = null,
        string? deviceId = null,
        int? gpuLayerCount = null,
        bool? flashAttention = null,
        int? threadCount = null,
        int? batchSize = null,
        string? profileId = null) => GgufExecutionPayload.Create(
            runtimeBuildId ?? payload.RuntimeBuildId,
            runtimeSourceCommit ?? payload.RuntimeSourceCommit,
            payload.Backend,
            deviceId ?? payload.DeviceId,
            payload.ContextSize,
            payload.KeyCacheType,
            payload.ValueCacheType,
            gpuLayerCount ?? payload.GpuLayerCount,
            flashAttention ?? payload.FlashAttention,
            threadCount ?? payload.ThreadCount,
            batchSize ?? payload.BatchSize,
            payload.EvidenceGrade,
            profileId ?? payload.ProfileId,
            payload.MaximumGeneratedTokens,
            payload.PersistentTargetWeightFormat);

    private static OptimizationExecutionPlan ClonePlan(
        OptimizationExecutionPlan plan,
        OptimizationJourneyBinding? binding = null,
        GgufExecutionPayload? payload = null,
        string? configurationSha256 = null)
    {
        ConstructorInfo constructor = typeof(OptimizationExecutionPlan)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 11);
        return (OptimizationExecutionPlan)constructor.Invoke(
        [
            plan.ContractVersion,
            plan.OptimizationPlanId,
            binding ?? plan.Binding,
            plan.CapabilitySnapshot,
            plan.Workload,
            plan.Candidate,
            payload is null
                ? plan.ExecutionPayload
                : OptimizationExecutionPayload.ForGguf(payload),
            plan.Preference,
            plan.SharedWithAdjacentBand,
            configurationSha256 ?? plan.ConfigurationSha256,
            plan.CreatedAtUtc,
        ]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedPayloadComposer(
        OptimizationExecutionPayload payload)
        : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.Gguf;

        public OptimizationExecutionPayload Compose(
            OptimizationCandidate candidate) => payload;
    }

    private sealed class RecordingProgress(
        List<OptimizationExportProgress> values)
        : IProgress<OptimizationExportProgress>
    {
        public void Report(OptimizationExportProgress value) => values.Add(value);
    }
}
