using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationDestinationExportServiceTests
{
    private const string SourceSha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ManifestSha256 =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [TestMethod]
    public async Task ExactPersistentResultIsPickedExportedAndReceipted()
    {
        OptimizationExecutionResult result = PersistentResult();
        VerifiedPersistentExportTarget target =
            VerifiedPersistentExportTarget.FromExecutionResult(result);
        var progress = new List<OptimizationExportProgress>();
        int pickerCalls = 0;
        int exportCalls = 0;
        var service = new OptimizationDestinationExportService(
            result,
            (route, cancellationToken) =>
            {
                Assert.AreEqual(OptimizationRoute.Gguf, route);
                cancellationToken.ThrowIfCancellationRequested();
                pickerCalls++;
                return Task.FromResult<string?>("D:\\Exports\\optimised.gguf");
            },
            (candidate, destination, maximumBytes, cancellationToken) =>
            {
                Assert.AreSame(result, candidate);
                Assert.AreEqual("D:\\Exports\\optimised.gguf", destination);
                Assert.AreEqual(result.OutputSizeBytes, maximumBytes);
                cancellationToken.ThrowIfCancellationRequested();
                exportCalls++;
                return Task.FromResult(
                    OptimizationDestinationExportResult.Succeeded(result));
            });

        OptimizationExportResult exported = await service.ExportAsync(
            target,
            new RecordingProgress(progress),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExportResultKind.Succeeded, exported.Kind);
        Assert.AreEqual(1, pickerCalls);
        Assert.AreEqual(1, exportCalls);
        Assert.IsNotNull(exported.Receipt);
        Assert.AreEqual(target.OptimizationPlanId,
            exported.Receipt.OptimizationPlanId);
        CollectionAssert.AreEqual(
            new[] { OptimizationExportStage.Writing,
                    OptimizationExportStage.Verifying,
                    OptimizationExportStage.Publishing },
            progress.Select(item => item.Stage).ToArray());
    }

    [TestMethod]
    public async Task CancelledPickerNeverInvokesDestinationBackend()
    {
        OptimizationExecutionResult result = PersistentResult();
        int exportCalls = 0;
        var service = new OptimizationDestinationExportService(
            result,
            (_, _) => Task.FromResult<string?>(null),
            (_, _, _, _) =>
            {
                exportCalls++;
                return Task.FromResult(
                    OptimizationDestinationExportResult.Succeeded(result));
            });

        OptimizationExportResult exported = await service.ExportAsync(
            VerifiedPersistentExportTarget.FromExecutionResult(result),
            new Progress<OptimizationExportProgress>(),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExportResultKind.Cancelled, exported.Kind);
        Assert.AreEqual(0, exportCalls);
    }

    [TestMethod]
    public async Task ForeignTargetFailsClosedBeforeOpeningPicker()
    {
        OptimizationExecutionResult result = PersistentResult();
        int pickerCalls = 0;
        var service = new OptimizationDestinationExportService(
            result,
            (_, _) =>
            {
                pickerCalls++;
                return Task.FromResult<string?>("D:\\Exports\\foreign.gguf");
            },
            (_, _, _, _) => throw new AssertFailedException(
                "A foreign target must not reach the destination backend."));
        var foreign = new VerifiedPersistentExportTarget(
            OptimizationRoute.Gguf,
            Guid.NewGuid(),
            result.ConfigurationSha256,
            result.SourceSha256,
            true,
            result.OutputIdentity!,
            result.OutputManifestSha256!,
            result.OutputSizeBytes);

        OptimizationExportResult exported = await service.ExportAsync(
            foreign,
            new Progress<OptimizationExportProgress>(),
            CancellationToken.None);

        Assert.AreEqual(OptimizationExportResultKind.Failed, exported.Kind);
        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch,
            exported.Failure);
        Assert.AreEqual(0, pickerCalls);
    }

    [TestMethod]
    public void PersistentServiceDoesNotImplementRuntimeBundleServiceArm()
    {
        OptimizationExecutionResult result = PersistentResult();
        var service = new OptimizationDestinationExportService(
            result,
            (_, _) => Task.FromResult<string?>(null),
            (_, _, _, _) => throw new AssertFailedException(
                "The destination must not be called."));

        Assert.IsFalse(typeof(IGgufRuntimeBundleExportService)
            .IsAssignableFrom(service.GetType()));
    }

    private static OptimizationExecutionResult PersistentResult()
    {
        OptimizationExecutionPlan plan =
            OptimizationSelectionHandoffTests.PersistentPlanForSource(
                SourceSha256, 4096);
        return OptimizationExecutionResult.Succeeded(
            plan,
            "output-verified",
            ManifestSha256,
            8192,
            sourceUnchanged: true,
            DateTimeOffset.UtcNow,
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }

    private sealed class RecordingProgress(
        List<OptimizationExportProgress> values)
        : IProgress<OptimizationExportProgress>
    {
        public void Report(OptimizationExportProgress value) =>
            values.Add(value);
    }
}
