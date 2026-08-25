using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection;

[TestClass]
public sealed class OpenVinoHardwareHandoffAdapterTests
{
    [TestMethod]
    public void ValidV2Handoff_PreservesExactIdentityAndMarksOpenVinoRoute()
    {
        var source = new ModelInspectionHandoffV2(
            Guid.NewGuid(),
            Guid.NewGuid(),
            GraniteEdgeAI.OpenVino.Contracts.ModelInspectionOutcome.ReadyWithWarnings,
            new string('a', 64),
            4_096);

        bool projected = OpenVinoHardwareHandoffAdapter.TryProject(
            source,
            out ModelInspectionHandoff? actual);

        Assert.IsTrue(projected);
        Assert.IsNotNull(actual);
        Assert.AreEqual(ModelInspectionRouteKind.OpenVino, actual.Route);
        Assert.AreEqual(source.ModelInspectionHandoffId, actual.ModelInspectionHandoffId);
        Assert.AreEqual(source.ModelInspectionRunId, actual.ModelInspectionRunId);
        Assert.AreEqual(source.ModelSha256, actual.ModelSha256);
        Assert.AreEqual(source.ModelLengthBytes, actual.ModelLengthBytes);
        Assert.AreEqual(
            GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome.ReadyWithWarnings,
            actual.Outcome);
    }

    [TestMethod]
    public void OlderOrMalformedHandoff_FailsClosed()
    {
        var old = new ModelInspectionHandoffV2(
            SchemaVersion: 1,
            ModelInspectionHandoffId: Guid.NewGuid(),
            ModelInspectionRunId: Guid.NewGuid(),
            Outcome: GraniteEdgeAI.OpenVino.Contracts.ModelInspectionOutcome.Ready,
            ModelSha256: new string('a', 64),
            ModelLengthBytes: 4_096);

        Assert.IsFalse(OpenVinoHardwareHandoffAdapter.TryProject(old, out _));
        Assert.IsFalse(OpenVinoHardwareHandoffAdapter.TryProject(null, out _));
    }
}
