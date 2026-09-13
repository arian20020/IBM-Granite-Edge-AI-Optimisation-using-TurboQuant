using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class CurrentModelLaunchHandoffTests
{
    private const string Digest =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    public void HandoffContainsOnlyTheApprovedPathFreePublicProperties()
    {
        string[] names = typeof(CurrentModelLaunchHandoff)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
        new[]
        {
            "CompatibilityDecisionId",
            "HardwareSnapshotSha256",
            "ModelInspectionHandoffId",
            "ModelInspectionRunId",
            "ModelLengthBytes",
            "ModelSha256",
            "ProductHardwareRunId",
            "Route",
            "RuntimeConfigurationSha256"
        }, names);
        Assert.IsTrue(names.All(name =>
            !name.Contains("Path", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("File", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("Folder", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ContextRejectsAChangedSourceBindingOrRuntimeDigest()
    {
        Fixture fixture = CreateFixture();
        Assert.ThrowsExactly<ArgumentException>(() =>
            CurrentModelLaunchContext.Create(
                fixture.Handoff,
                fixture.Payload,
                new ModelSourceCustodyKey(
                    Guid.NewGuid(),
                    Digest,
                    fixture.Handoff.ModelLengthBytes,
                    OptimizationRoute.Gguf)));

        Assert.ThrowsExactly<ArgumentException>(() =>
            new CurrentCompatibleConfiguration(
                OptimizationRoute.Gguf,
                fixture.Payload,
                new string('0', 64),
                "decision-1"));
    }

    [TestMethod]
    public async Task RegistryIsUnavailableUntilTheExactRouteIsRegistered()
    {
        Fixture fixture = CreateFixture();
        using var custody = new ModelSourceCustodyRegistry();
        custody.Register(new ModelSourceCustodyRecord(
            fixture.SourceKey,
            @"C:\models\granite.gguf"));
        using var registry = new CurrentModelChatLaunchRegistry(custody);
        registry.RegisterContext(fixture.Context);

        CurrentModelChatLaunchResult unavailable = await registry.LaunchAsync(
            fixture.Handoff,
            CancellationToken.None);
        var launcher = new CapturingLauncher();
        registry.RegisterRoute(launcher);
        CurrentModelChatLaunchResult launched = await registry.LaunchAsync(
            fixture.Handoff,
            CancellationToken.None);

        Assert.AreEqual(
            CurrentModelChatSupportCode.RuntimeUnavailable,
            unavailable.SupportCode);
        Assert.IsTrue(launched.Succeeded);
        Assert.AreEqual(@"C:\models\granite.gguf", launcher.ObservedPrivateSource);
    }

    [TestMethod]
    public async Task RegistryReportsOneSafeUnexpectedGgufLaunchFault()
    {
        Fixture fixture = CreateFixture();
        using var custody = new ModelSourceCustodyRegistry();
        custody.Register(new ModelSourceCustodyRecord(
            fixture.SourceKey,
            @"C:\private\model.gguf"));
        var reporter = new BoundedApplicationFaultReporter(4);
        using var registry = new CurrentModelChatLaunchRegistry(custody, reporter);
        registry.RegisterContext(fixture.Context);
        registry.RegisterRoute(new ThrowingLauncher(
            new InvalidOperationException(@"private C:\Users\person\model.gguf")));

        CurrentModelChatLaunchResult first = await registry.LaunchAsync(
            fixture.Handoff, CancellationToken.None);
        CurrentModelChatLaunchResult second = await registry.LaunchAsync(
            fixture.Handoff, CancellationToken.None);

        Assert.IsFalse(first.Succeeded);
        Assert.IsFalse(second.Succeeded);
        IReadOnlyList<ApplicationFault> faults = reporter.Capture();
        Assert.HasCount(1, faults);
        Assert.AreEqual(ApplicationFaultCode.GgufChatOperationUnexpected, faults[0].Code);
        Assert.AreEqual(ApplicationFaultClassification.InvalidOperation, faults[0].Classification);
    }

    private static Fixture CreateFixture()
    {
        OptimizationExecutionPayload payload =
            OptimizationExecutionPayload.ForGguf(
                GgufExecutionPayload.Create(
                    "runtime",
                    Commit,
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
                    "profile",
                    256,
                    GgufWeightFormat.Imported));
        CurrentCompatibleConfiguration current = new(
            OptimizationRoute.Gguf,
            payload,
            payload.ComputeRuntimeConfigurationSha256(),
            "decision-1");
        Guid run = Guid.NewGuid();
        Guid handoffId = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        CurrentModelLaunchHandoff handoff = CurrentModelLaunchHandoff.Create(
            OptimizationRoute.Gguf,
            run,
            handoffId,
            Digest,
            2L * 1024 * 1024 * 1024,
            hardwareRun,
            Digest,
            current);
        ModelSourceCustodyKey sourceKey = new(
            handoffId,
            Digest,
            handoff.ModelLengthBytes,
            OptimizationRoute.Gguf);
        CurrentModelLaunchContext context = CurrentModelLaunchContext.Create(
            handoff,
            payload,
            sourceKey);
        return new Fixture(payload, handoff, sourceKey, context);
    }

    private sealed record Fixture(
        OptimizationExecutionPayload Payload,
        CurrentModelLaunchHandoff Handoff,
        ModelSourceCustodyKey SourceKey,
        CurrentModelLaunchContext Context);

    private sealed class CapturingLauncher : ICurrentModelChatRouteLauncher
    {
        internal string? ObservedPrivateSource { get; private set; }
        public OptimizationRoute Route => OptimizationRoute.Gguf;

        public Task<CurrentModelChatLaunchResult> LaunchAsync(
            CurrentModelLaunchContext context,
            ModelSourceLease sourceLease,
            CancellationToken cancellationToken)
        {
            ObservedPrivateSource = sourceLease.SourcePath;
            return Task.FromResult(CurrentModelChatLaunchResult.Success);
        }
    }

    private sealed class ThrowingLauncher(Exception exception)
        : ICurrentModelChatRouteLauncher
    {
        public OptimizationRoute Route => OptimizationRoute.Gguf;
        public Task<CurrentModelChatLaunchResult> LaunchAsync(
            CurrentModelLaunchContext context,
            ModelSourceLease sourceLease,
            CancellationToken cancellationToken) =>
            Task.FromException<CurrentModelChatLaunchResult>(exception);
    }
}
