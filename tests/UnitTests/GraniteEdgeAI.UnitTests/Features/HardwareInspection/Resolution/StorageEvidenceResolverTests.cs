using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

[TestClass]
public sealed class StorageEvidenceResolverTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_MapsCapacityAndCallerAvailableBytesExactlyWithoutVolumeIdentity()
    {
        ComponentResolution<StorageFacts> result = StorageEvidenceResolver.Resolve(
            HardwareResolutionTestData.Storage(),
            HardwareResolutionTestData.Now);

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.AreEqual(1_000_000_000_000UL, result.Value!.SystemVolumeCapacityBytes);
        Assert.AreEqual(500_000_000_000UL, result.Value.SystemVolumeAvailableBytes);
        CollectionAssert.AreEqual(
            new[]
            {
                "storage.systemVolumeCapacityBytes",
                "storage.systemVolumeAvailableBytes",
            },
            result.Entries.Select(static entry => entry.CanonicalField).ToArray());
        Assert.IsTrue(result.Entries.All(static entry =>
            entry.Source == EvidenceSourceKind.Windows &&
            entry.Resolution == EvidenceResolutionState.ResolvedPrimary &&
            entry.SafeDiagnosticCode is null));
        CollectionAssert.AreEquivalent(
            new[] { "SystemVolumeCapacityBytes", "SystemVolumeAvailableBytes" },
            typeof(StorageFacts).GetProperties().Select(static property => property.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_AcceptsExactStaticAndFutureClockBoundaries()
    {
        foreach (DateTimeOffset capturedAt in new[]
                 {
                     HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge,
                     HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew,
                 })
        {
            ComponentResolution<StorageFacts> result = StorageEvidenceResolver.Resolve(
                HardwareResolutionTestData.Storage(capturedAt),
                HardwareResolutionTestData.Now);
            Assert.IsFalse(result.HasCriticalFailure);
            Assert.IsNotNull(result.Value);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_RejectsStaleFutureAndUnavailableEvidenceWithoutInventingCapacity()
    {
        (WindowsStorageEvidence Evidence, HardwareResolutionDiagnosticCode Diagnostic)[] cases =
        [
            (HardwareResolutionTestData.Storage(
                HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge -
                    TimeSpan.FromTicks(1)), HardwareResolutionDiagnosticCode.EvidenceStale),
            (HardwareResolutionTestData.Storage(
                HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew +
                    TimeSpan.FromTicks(1)), HardwareResolutionDiagnosticCode.ClockFuture),
            (WindowsStorageEvidence.Unavailable(
                WindowsStorageDiagnosticCode.NativeApiUnavailable,
                HardwareResolutionTestData.Now), HardwareResolutionDiagnosticCode.StorageUnavailable),
        ];

        foreach ((WindowsStorageEvidence evidence, HardwareResolutionDiagnosticCode diagnostic) in cases)
        {
            ComponentResolution<StorageFacts> result = StorageEvidenceResolver.Resolve(
                evidence,
                HardwareResolutionTestData.Now);

            Assert.IsTrue(result.HasCriticalFailure);
            Assert.IsNull(result.Value);
            Assert.IsTrue(result.Entries.All(static entry =>
                entry.Resolution == EvidenceResolutionState.Unavailable));
            CollectionAssert.AreEqual(new[] { diagnostic }, result.Diagnostics.ToArray());
            Assert.IsFalse(result.Entries.Any(static entry =>
                entry.SafeDiagnosticCode?.Contains("path", StringComparison.Ordinal) == true ||
                entry.SafeDiagnosticCode?.Contains("volume", StringComparison.Ordinal) == true));
        }
    }
}
