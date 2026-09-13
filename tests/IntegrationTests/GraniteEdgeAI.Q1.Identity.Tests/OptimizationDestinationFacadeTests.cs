using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.CrossFeature.IntegrationTests;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using System.Reflection;
using System.Security.Cryptography;

namespace GraniteEdgeAI.Q1.Identity.Tests;

[TestClass]
public sealed class OptimizationDestinationFacadeTests
{
    private static readonly string[] RuntimeBundleMembers =
    {
        "bundle-manifest.json",
        "model.gguf",
        "runtime-profile.json",
    };

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
    public async Task RuntimeBundleFacadeUsesExactPlanCustodyAndLeavesPersistentRefusalIntact()
    {
        string root = Directory.CreateTempSubdirectory(
            "q1-runtime-bundle-").FullName;
        string sourcePath = Path.Combine(root, "source.gguf");
        byte[] sourceBytes = "exact runtime bundle source"u8.ToArray();
        await File.WriteAllBytesAsync(sourcePath, sourceBytes);
        string sourceSha = Convert.ToHexString(SHA256.HashData(sourceBytes))
            .ToLowerInvariant();
        using var custody = new ModelSourceCustodyRegistry();
        try
        {
            OptimizationExecutionPlan plan = RuntimePlan(
                sourceSha, checked((ulong)sourceBytes.Length));
            OptimizationExecutionResult result = RuntimeResult(plan);
            var key = new ModelSourceCustodyKey(
                Guid.ParseExact(plan.Binding.ModelInspectionHandoffId, "N"),
                sourceSha,
                sourceBytes.Length,
                OptimizationRoute.Gguf);
            Assert.IsTrue(custody.Register(new ModelSourceCustodyRecord(
                key, sourcePath)));
            var facade = new OptimizationDestinationFacade(
                new GgufOptimizationDestinationRoute(
                    plan, new OptimizationOutputRegistry(
                        Path.Combine(root, "staging"),
                        Path.Combine(root, "committed")), custody),
                new RecordingRoute(OptimizationRoute.OpenVino));
            string destination = Path.Combine(root, "bundle");

            GgufRuntimeProfileBundleExportResult bundle =
                await facade.ExportGgufRuntimeBundleAsync(
                    result,
                    destination,
                    1024 * 1024,
                    new InlineRuntimeProgress(_ => { }),
                    CancellationToken.None);
            OptimizationDestinationExportResult persistent =
                await facade.ExportPersistentAsync(
                    result, destination + "-persistent", 1024 * 1024,
                    CancellationToken.None);

            Assert.AreEqual(
                GgufRuntimeProfileBundleExportDisposition.Succeeded,
                bundle.Disposition);
            Assert.AreEqual(
                OptimizationDestinationExportDisposition.RuntimeOnly,
                persistent.Disposition);
            CollectionAssert.AreEqual(
                RuntimeBundleMembers,
                Directory.GetFiles(destination)
                    .Select(Path.GetFileName)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            CollectionAssert.AreEqual(sourceBytes,
                await File.ReadAllBytesAsync(
                    Path.Combine(destination, "model.gguf")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(
                    root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task RuntimeBundleLifecycleRetirementCancelsAndJoinsTheOperation()
    {
        string root = Directory.CreateTempSubdirectory(
            "q1-runtime-bundle-retire-").FullName;
        string sourcePath = Path.Combine(root, "source.gguf");
        byte[] sourceBytes = new byte[256 * 1024];
        Random.Shared.NextBytes(sourceBytes);
        await File.WriteAllBytesAsync(sourcePath, sourceBytes);
        string sourceSha = Convert.ToHexString(SHA256.HashData(sourceBytes))
            .ToLowerInvariant();
        using var custody = new ModelSourceCustodyRegistry();
        try
        {
            OptimizationExecutionPlan plan = RuntimePlan(
                sourceSha, checked((ulong)sourceBytes.Length));
            OptimizationExecutionResult result = RuntimeResult(plan);
            var key = new ModelSourceCustodyKey(
                Guid.ParseExact(plan.Binding.ModelInspectionHandoffId, "N"),
                sourceSha,
                sourceBytes.Length,
                OptimizationRoute.Gguf);
            Assert.IsTrue(custody.Register(new ModelSourceCustodyRecord(
                key, sourcePath)));
            var lifecycle = new OptimizationDestinationLifecycle(
                new OptimizationDestinationFacade(
                    new GgufOptimizationDestinationRoute(
                        plan, new OptimizationOutputRegistry(
                            Path.Combine(root, "staging"),
                            Path.Combine(root, "committed")), custody),
                    new RecordingRoute(OptimizationRoute.OpenVino)),
                CancellationToken.None);
            var entered = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim(false);
            var progress = new InlineRuntimeProgress(stage =>
            {
                if (stage == GgufRuntimeProfileBundleExportStage.CopyingModel)
                {
                    entered.TrySetResult();
                    release.Wait();
                }
            });

            Task<GgufRuntimeProfileBundleExportResult> operation =
                Task.Run(() => lifecycle.ExportGgufRuntimeBundleAsync(
                        result,
                        Path.Combine(root, "bundle"),
                        1024 * 1024,
                        progress));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Task retirement = lifecycle.RetireAsync();

            Assert.IsFalse(retirement.IsCompleted);
            release.Set();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => operation);
            await retirement;
            GgufRuntimeProfileBundleExportResult late =
                await lifecycle.ExportGgufRuntimeBundleAsync(
                    result,
                    Path.Combine(root, "late"),
                    1024 * 1024,
                    new InlineRuntimeProgress(_ => { }));
            Assert.AreEqual(
                GgufRuntimeProfileBundleExportDisposition.ResultRejected,
                late.Disposition);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(
                    root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(root, recursive: true);
            }
        }
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

    private static OptimizationExecutionPlan RuntimePlan(
        string sourceSha,
        ulong sourceLength)
    {
        OptimizationExecutionPlan basis =
            CrossFeaturePlanFixture.PersistentGgufPlan(sourceSha, sourceLength);
        GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);
        OptimizationCandidateMetrics metrics =
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Measured,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                4096,
                1,
                2,
                1,
                0,
                0,
                requiresPersistentChange: false);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            configuration,
            metrics,
            "q1-runtime-q8",
            isExperimental: false);
        OptimizationExecutionPayload payload =
            OptimizationExecutionPayload.ForGguf(
                GgufExecutionPayload.Create(
                    "runtime",
                    "0123456789abcdef0123456789abcdef01234567",
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
            new string('c', 64),
            DateTimeOffset.UnixEpoch,
        ]);
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

    private sealed class InlineRuntimeProgress(
        Action<GgufRuntimeProfileBundleExportStage> report)
        : IProgress<GgufRuntimeProfileBundleExportStage>
    {
        public void Report(GgufRuntimeProfileBundleExportStage value) =>
            report(value);
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
