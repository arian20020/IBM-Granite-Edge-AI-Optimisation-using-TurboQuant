using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.CrossFeature.IntegrationTests;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using System.Security.Cryptography;

namespace GraniteEdgeAI.Q1.Identity.Tests;

[TestClass]
public sealed class OptimizationDestinationFacadeTests
{
    [TestMethod]
    public async Task ExactResultIsRoutedOnlyToItsTypedDestinationProvider()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.PersistentGgufPlan(
            CrossFeaturePlanFixture.ModelDigest, 4096);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan, "exact-output", new string('a', 64), 2048, true,
            DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var gguf = new RecordingRoute(OptimizationRoute.Gguf);
        var openVino = new RecordingRoute(OptimizationRoute.OpenVino);
        var facade = new OptimizationDestinationFacade(gguf, openVino);

        using OptimizationChatTarget? target = await facade.CreateChatTargetAsync(
            result, CancellationToken.None);

        Assert.IsNotNull(target);
        Assert.AreSame(result, target.Result);
        Assert.AreEqual(OptimizationRoute.Gguf, target.Route);
        Assert.AreEqual(1, gguf.ChatCalls);
        Assert.AreEqual(0, openVino.ChatCalls);
    }

    [TestMethod]
    public async Task RuntimeOnlyExportRefusalRemainsTypedAndIdentityBound()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.Issue(
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoWeightFormat.Original);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan, "runtime-profile", plan.ConfigurationSha256, 0, true,
            DateTimeOffset.UnixEpoch);
        var facade = new OptimizationDestinationFacade(
            new RecordingRoute(OptimizationRoute.Gguf),
            new RecordingRoute(OptimizationRoute.OpenVino));

        OptimizationDestinationExportResult export = await facade.ExportPersistentAsync(
            result, "unused", 1UL << 40, CancellationToken.None);

        Assert.AreEqual(OptimizationDestinationExportDisposition.RuntimeOnly, export.Disposition);
        Assert.AreEqual(result.OptimizationPlanId, export.OptimizationPlanId);
        Assert.AreEqual(result.ConfigurationSha256, export.ConfigurationSha256);
        Assert.AreEqual(result.ExecutionId, export.ExecutionId);
    }

    [TestMethod]
    public async Task CancellationIsObservedBeforeAProviderCanReceiveTheResult()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.PersistentGgufPlan(
            CrossFeaturePlanFixture.ModelDigest, 4096);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan, "exact-output", new string('b', 64), 2048, true,
            DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var gguf = new RecordingRoute(OptimizationRoute.Gguf);
        var facade = new OptimizationDestinationFacade(
            gguf, new RecordingRoute(OptimizationRoute.OpenVino));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            facade.CreateChatTargetAsync(result, cancellation.Token));
        Assert.AreEqual(0, gguf.ChatCalls);
    }

    [TestMethod]
    public async Task CancellationAfterResolutionDisposesTheRetainedTarget()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.PersistentGgufPlan(
            CrossFeaturePlanFixture.ModelDigest, 4096);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan, "exact-output", new string('c', 64), 2048, true,
            DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var route = new RecordingRoute(OptimizationRoute.Gguf)
        {
            BlockChat = true,
        };
        var facade = new OptimizationDestinationFacade(
            route, new RecordingRoute(OptimizationRoute.OpenVino));
        using var cancellation = new CancellationTokenSource();
        Task<OptimizationChatTarget?> pending = facade.CreateChatTargetAsync(
            result, cancellation.Token);
        await route.ChatEntered.Task;

        cancellation.Cancel();
        route.ReleaseChat.SetResult();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => pending);
        Assert.IsTrue(route.LastTarget!.Disposed);
    }

    [TestMethod]
    public async Task SubstitutedExportReceiptIsRejectedByTheFacade()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.PersistentGgufPlan(
            CrossFeaturePlanFixture.ModelDigest, 4096);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan, "exact-output", new string('d', 64), 2048, true,
            DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var route = new RecordingRoute(OptimizationRoute.Gguf)
        {
            SubstituteExportIdentity = true,
        };
        var facade = new OptimizationDestinationFacade(
            route, new RecordingRoute(OptimizationRoute.OpenVino));

        OptimizationDestinationExportResult export = await facade.ExportPersistentAsync(
            result, "destination", 4096, CancellationToken.None);

        Assert.AreEqual(OptimizationDestinationExportDisposition.ResultRejected,
            export.Disposition);
        Assert.AreEqual(result.OutputIdentity, export.OutputIdentity);
        Assert.AreEqual(result.OutputManifestSha256, export.OutputManifestSha256);
        Assert.AreEqual(result.OutputSizeBytes, export.OutputLengthBytes);
    }

    [TestMethod]
    public void PersistentGgufLeasePreventsReplacementUntilTargetDisposal()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "q1-gguf-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "model.gguf");
        File.WriteAllBytes(path, "leased-output"u8.ToArray());
        try
        {
            var handle = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read);
            using (var lease = new GgufPublishedOutputLease(
                path, new string('e', 64), (ulong)handle.Length, handle))
            {
                Assert.ThrowsExactly<IOException>(() =>
                    File.Open(path, FileMode.Create, FileAccess.Write,
                        FileShare.Read).Dispose());
            }

            using FileStream replacement = File.Open(path, FileMode.Create,
                FileAccess.Write, FileShare.None);
            replacement.WriteByte(1);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
            Directory.Delete(root);
        }
    }

    [TestMethod]
    public async Task RegistryAcquisitionRetainsVerifiedPersistentBytes()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "q1-registry-lease-" + Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(root, "staging");
        string committed = Path.Combine(root, "committed");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(committed);
        byte[] sourceBytes = "source-model"u8.ToArray();
        string sourcePath = Path.Combine(root, "source.gguf");
        File.WriteAllBytes(sourcePath, sourceBytes);
        string sourceDigest = Convert.ToHexString(SHA256.HashData(sourceBytes))
            .ToLowerInvariant();
        try
        {
            OptimizationExecutionPlan plan = CrossFeaturePlanFixture.PersistentGgufPlan(
                sourceDigest, (ulong)sourceBytes.Length);
            var registry = new OptimizationOutputRegistry(staging, committed);
            using OptimizationOutputLease output = registry.CreateLease(plan, 1);
            await using (FileStream writer = output.CreateFileForWrite("model.gguf"))
                await writer.WriteAsync("verified-output"u8.ToArray());
            SealedOptimizationCandidate sealedOutput = output.Seal("verified-output");
            using var source = new StagedSourceSnapshot(sourceDigest,
                (ulong)sourceBytes.Length, "source-snapshot", sourcePath);
            OptimizationCommitReceipt receipt = await registry.AdmitAsync(
                plan, 1, source, true, sealedOutput, CancellationToken.None);
            OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
                plan, receipt.Key.OutputIdentity, receipt.Key.OutputManifestSha256,
                receipt.OutputSizeBytes, true, DateTimeOffset.UnixEpoch,
                receipt.Key.ExecutionId);

            using var sourceCustody = new GraniteEdgeAI.Features.ModelInspection.SourceCustody.ModelSourceCustodyRegistry();
            var ggufRoute = new GgufOptimizationDestinationRoute(
                plan, registry, sourceCustody);
            var facade = new OptimizationDestinationFacade(
                ggufRoute, new RecordingRoute(OptimizationRoute.OpenVino));
            using OptimizationChatTarget? target = await facade.CreateChatTargetAsync(
                result, CancellationToken.None);
            Assert.IsInstanceOfType<GgufOptimizationChatTarget>(target);
            var retained = (GgufOptimizationChatTarget)target;
            using (retained)
            {
                Assert.ThrowsExactly<IOException>(() =>
                    File.Open(retained.VerifiedModelPath, FileMode.Create, FileAccess.Write,
                        FileShare.Read).Dispose());
            }
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(
                         root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task LifecycleRejectsAnOverlappingChatAdmissionWithoutReplacingItsTarget()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.PersistentGgufPlan(
            CrossFeaturePlanFixture.ModelDigest, 4096);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan, "exact-output", new string('f', 64), 2048, true,
            DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var route = new RecordingRoute(OptimizationRoute.Gguf)
        {
            BlockChat = true,
        };
        var lifecycle = new OptimizationDestinationLifecycle(
            new OptimizationDestinationFacade(
                route, new RecordingRoute(OptimizationRoute.OpenVino)),
            CancellationToken.None);

        Task<OptimizationChatTargetUse?> first =
            lifecycle.CreateChatTargetUseAsync(result);
        await route.ChatEntered.Task;

        OptimizationChatTargetUse? overlapping =
            await lifecycle.CreateChatTargetUseAsync(result);

        Assert.IsNull(overlapping);
        Assert.AreEqual(1, route.ChatCalls);
        route.ReleaseChat.SetResult();
        using OptimizationChatTargetUse? admitted = await first;
        Assert.IsNotNull(admitted);
        Assert.AreSame(route.LastTarget, admitted.Target);

        admitted.Dispose();
        await lifecycle.RetireAsync();
        Assert.IsFalse(route.LastTarget!.Disposed);
        lifecycle.RetireChatTarget();
        Assert.IsTrue(route.LastTarget.Disposed);
    }

    [TestMethod]
    public async Task RetirementClosesAdmissionBeforeJoiningAnInFlightExport()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.PersistentGgufPlan(
            CrossFeaturePlanFixture.ModelDigest, 4096);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan, "exact-output", new string('1', 64), 2048, true,
            DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var route = new RecordingRoute(OptimizationRoute.Gguf)
        {
            BlockExport = true,
        };
        var lifecycle = new OptimizationDestinationLifecycle(
            new OptimizationDestinationFacade(
                route, new RecordingRoute(OptimizationRoute.OpenVino)),
            CancellationToken.None);

        Task<OptimizationDestinationExportResult> admitted =
            lifecycle.ExportPersistentAsync(result, "destination", 4096);
        await route.ExportEntered.Task;
        Task retirement = lifecycle.RetireAsync();

        OptimizationDestinationExportResult rejected =
            await lifecycle.ExportPersistentAsync(result, "late", 4096);
        Assert.AreEqual(
            OptimizationDestinationExportDisposition.ResultRejected,
            rejected.Disposition);
        Assert.AreEqual(1, route.ExportCalls);
        Assert.IsFalse(retirement.IsCompleted);

        route.ReleaseExport.SetResult();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => admitted);
        await retirement;
    }

    [TestMethod]
    public async Task RetirementWaitsForChatConsumerBeforeTargetCanBeReleased()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.PersistentGgufPlan(
            CrossFeaturePlanFixture.ModelDigest, 4096);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan, "exact-output", new string('2', 64), 2048, true,
            DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var route = new RecordingRoute(OptimizationRoute.Gguf);
        var lifecycle = new OptimizationDestinationLifecycle(
            new OptimizationDestinationFacade(
                route, new RecordingRoute(OptimizationRoute.OpenVino)),
            CancellationToken.None);
        OptimizationChatTargetUse? targetUse =
            await lifecycle.CreateChatTargetUseAsync(result);
        Assert.IsNotNull(targetUse);

        Task retirement = lifecycle.RetireAsync();
        Assert.IsFalse(retirement.IsCompleted);
        Assert.ThrowsExactly<InvalidOperationException>(
            lifecycle.RetireChatTarget);

        targetUse.Dispose();
        await retirement;
        Assert.IsFalse(route.LastTarget!.Disposed);
        lifecycle.RetireChatTarget();
        Assert.IsTrue(route.LastTarget.Disposed);
    }

    [TestMethod]
    public async Task ChatHandoffClosureWaitsForOwnerAndRejectsLateAdmission()
    {
        using var gate = new OptimizationChatHandoffGate();
        OptimizationChatHandoffLease? owner =
            await gate.TryEnterAsync(CancellationToken.None);
        Assert.IsNotNull(owner);

        Task closure = gate.CloseAsync();
        Assert.IsFalse(closure.IsCompleted);
        Assert.IsNull(await gate.TryEnterAsync(CancellationToken.None));

        owner.Dispose();
        await closure;
        Assert.IsNull(await gate.TryEnterAsync(CancellationToken.None));
        Assert.AreSame(closure, gate.CloseAsync());
    }

    private sealed class RecordingRoute(OptimizationRoute route)
        : IOptimizationDestinationRoute
    {
        public OptimizationRoute Route { get; } = route;
        internal int ChatCalls { get; private set; }
        internal bool BlockChat { get; init; }
        internal bool BlockExport { get; init; }
        internal bool SubstituteExportIdentity { get; init; }
        internal TaskCompletionSource ChatEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseChat { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ExportEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseExport { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal TestChatTarget? LastTarget { get; private set; }
        internal int ExportCalls { get; private set; }

        public async Task<OptimizationChatTarget?> CreateChatTargetAsync(
            OptimizationExecutionResult result,
            CancellationToken cancellationToken)
        {
            ChatCalls++;
            ChatEntered.TrySetResult();
            if (BlockChat)
            {
                await ReleaseChat.Task;
            }
            LastTarget = new TestChatTarget(result);
            return LastTarget;
        }

        public async Task<OptimizationDestinationExportResult> ExportPersistentAsync(
            OptimizationExecutionResult result,
            string destination,
            ulong maximumBytes,
            CancellationToken cancellationToken)
        {
            ExportCalls++;
            ExportEntered.TrySetResult();
            if (BlockExport)
            {
                await ReleaseExport.Task;
            }
            OptimizationDestinationExportResult value =
                OptimizationDestinationExportResult.Succeeded(result);
            if (SubstituteExportIdentity)
            {
                value = value with { ExecutionId = Guid.NewGuid() };
            }
            return value;
        }
    }

    private sealed class TestChatTarget(OptimizationExecutionResult result)
        : OptimizationChatTarget(result)
    {
        internal bool Disposed { get; private set; }
        protected override void DisposeCore() => Disposed = true;
    }
}
