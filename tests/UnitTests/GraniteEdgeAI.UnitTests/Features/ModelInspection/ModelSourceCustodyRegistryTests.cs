using System;
using System.Linq;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection;

[TestClass]
public sealed class ModelSourceCustodyRegistryTests
{
    private const string Digest =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [TestMethod]
    public void ResolveRequiresExactHandoffDigestLengthAndRoute()
    {
        using var registry = new ModelSourceCustodyRegistry();
        ModelSourceCustodyKey exact = Key();
        Assert.IsTrue(registry.Register(
            new ModelSourceCustodyRecord(exact, @"C:\models\granite.gguf")));

        Assert.IsTrue(registry.TryAcquire(exact, out ModelSourceLease? lease));
        Assert.IsFalse(registry.TryAcquire(
            new ModelSourceCustodyKey(
                exact.ModelInspectionHandoffId,
                exact.ModelSha256,
                exact.ModelLengthBytes + 1,
                exact.Route),
            out _));
        Assert.IsFalse(registry.TryAcquire(
            new ModelSourceCustodyKey(
                exact.ModelInspectionHandoffId,
                new string('0', 64),
                exact.ModelLengthBytes,
                exact.Route),
            out _));
        Assert.IsFalse(registry.TryAcquire(
            new ModelSourceCustodyKey(
                exact.ModelInspectionHandoffId,
                exact.ModelSha256,
                exact.ModelLengthBytes,
                OptimizationRoute.OpenVino),
            out _));
        lease!.Dispose();
    }

    [TestMethod]
    public void RetirePreventsNewLeaseWhileAnExistingLeaseCanFinish()
    {
        using var registry = new ModelSourceCustodyRegistry();
        ModelSourceCustodyKey key = Key();
        registry.Register(new ModelSourceCustodyRecord(
            key,
            @"C:\models\granite.gguf"));
        registry.TryAcquire(key, out ModelSourceLease? active);

        registry.Retire(key.ModelInspectionHandoffId);

        Assert.IsFalse(registry.TryAcquire(key, out _));
        Assert.AreEqual(@"C:\models\granite.gguf", active!.SourcePath);
        active.Dispose();
        Assert.IsFalse(registry.TryAcquire(key, out _));
    }

    [TestMethod]
    public void PublicSurfaceCannotExposeThePrivateSourcePath()
    {
        Type[] types =
        [
            typeof(ModelSourceCustodyKey),
            typeof(ModelSourceCustodyRegistry),
            typeof(ModelSourceLease),
            typeof(ModelSourceCustodyRecord)
        ];

        Assert.IsTrue(types.All(type => !type.IsPublic));
        Assert.IsTrue(types.SelectMany(type => type.GetProperties())
            .All(property => !property.Name.Contains(
                "Path",
                StringComparison.OrdinalIgnoreCase)));
    }

    private static ModelSourceCustodyKey Key() => new(
        Guid.NewGuid(),
        Digest,
        2L * 1024 * 1024 * 1024,
        OptimizationRoute.Gguf);
}
