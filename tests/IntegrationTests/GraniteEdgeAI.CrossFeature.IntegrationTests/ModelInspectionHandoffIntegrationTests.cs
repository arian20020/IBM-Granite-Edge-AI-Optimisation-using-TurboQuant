using System.Text;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class ModelInspectionHandoffIntegrationTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid HandoffId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private const string ModelDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public void V2HandoffUsesTheIndependentExactSixFieldCanonicalEncoding()
    {
        // Characterization: this exact v2 boundary exists at the frozen base.
        // The literal is independent of the production serializer.
        ModelInspectionHandoff handoff = Create(HandoffId);
        const string expected =
            "{\"schemaVersion\":2," +
            "\"modelInspectionHandoffId\":\"22222222-2222-4222-8222-222222222222\"," +
            "\"modelInspectionRunId\":\"11111111-1111-4111-8111-111111111111\"," +
            "\"outcome\":\"ReadyWithWarnings\"," +
            "\"modelSha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"," +
            "\"modelLengthBytes\":4096}";

        byte[] encoded = ModelInspectionHandoffCodec.Serialize(handoff);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expected), encoded);
        Assert.IsTrue(ModelInspectionHandoffCodec.TryDeserialize(encoded, out var decoded));
        Assert.AreEqual(ModelDigest, decoded!.ModelSha256);
        Assert.AreEqual(4096L, decoded.ModelLengthBytes);
    }

    [TestMethod]
    [DataRow("{\"schemaVersion\":2,\"modelInspectionHandoffId\":\"22222222-2222-4222-8222-222222222222\",\"modelInspectionRunId\":\"11111111-1111-4111-8111-111111111111\",\"outcome\":\"ReadyWithWarnings\",\"modelSha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"modelLengthBytes\":4096,\"path\":\"C:\\\\Users\\\\private\\\\model.gguf\"}")]
    [DataRow("{\"modelInspectionRunId\":\"11111111-1111-4111-8111-111111111111\",\"schemaVersion\":2,\"modelInspectionHandoffId\":\"22222222-2222-4222-8222-222222222222\",\"outcome\":\"ReadyWithWarnings\",\"modelSha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"modelLengthBytes\":4096}")]
    [DataRow("{\"schemaVersion\":2,\"modelInspectionHandoffId\":\"22222222-2222-4222-8222-222222222222\",\"modelInspectionRunId\":\"11111111-1111-4111-8111-111111111111\",\"outcome\":\"ReadyWithWarnings\",\"modelSha256\":\"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF\",\"modelLengthBytes\":4096}")]
    public void V2HandoffRejectsPathOrderAndDigestMutations(string hostileJson)
    {
        Assert.IsFalse(ModelInspectionHandoffCodec.TryDeserialize(
            Encoding.UTF8.GetBytes(hostileJson),
            out ModelInspectionHandoff? handoff));
        Assert.IsNull(handoff);
    }

    [TestMethod]
    public void ClaimRollbackAndReissueRemainOneUseAndIdentityBound()
    {
        // Characterization: a change that reuses a hardware-run identity,
        // accepts a mutated handoff, or retains the prior capability fails.
        using var registry = new ModelInspectionHandoffRegistry();
        registry.ActivateModelRun(ModelRunId);
        ModelInspectionHandoff issued = Create(HandoffId);
        Assert.IsTrue(registry.TryRegisterIssued(issued));

        Guid firstHardwareRun =
            Guid.Parse("33333333-3333-4333-8333-333333333333");
        Assert.IsTrue(registry.TryBindToHardwareRun(
            issued, ModelRunId, firstHardwareRun, out var firstClaim));
        Assert.IsTrue(registry.TryRollbackBeforeHardwareStart(firstClaim));
        Assert.IsFalse(registry.TryBindToHardwareRun(
            issued, ModelRunId, firstHardwareRun, out _));

        Guid secondHardwareRun =
            Guid.Parse("44444444-4444-4444-8444-444444444444");
        Assert.IsTrue(registry.TryBindToHardwareRun(
            issued, ModelRunId, secondHardwareRun, out var secondClaim));
        Assert.IsTrue(registry.TryMarkHardwareStarted(secondClaim));

        ModelInspectionHandoff replacement = Create(
            Guid.Parse("55555555-5555-4555-8555-555555555555"));
        Assert.IsTrue(registry.TryAcceptReissue(HandoffId, replacement));
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Invalidated,
            registry.GetState(HandoffId));
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Issued,
            registry.GetState(replacement.ModelInspectionHandoffId));
    }

    private static ModelInspectionHandoff Create(Guid handoffId) => new(
        ModelInspectionHandoff.CurrentSchemaVersion,
        handoffId,
        ModelRunId,
        ModelInspectionOutcome.ReadyWithWarnings,
        ModelDigest,
        4096);
}
