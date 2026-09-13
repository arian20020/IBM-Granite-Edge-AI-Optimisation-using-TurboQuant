using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Text;
using SharedHandoff = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionHandoffV2;
using SharedOutcome = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionOutcomeV2;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelInspectionHandoffTests
{
    private const string Sha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly Guid HandoffId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    [TestMethod]
    public void Contract_ContainsExactlySixImmutableFields()
    {
        PropertyInfo[] properties = typeof(ModelInspectionHandoff)
            .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "ModelInspectionHandoffId",
                "ModelInspectionRunId",
                "ModelLengthBytes",
                "ModelSha256",
                "Outcome",
                "SchemaVersion"
            },
            properties.Select(property => property.Name).ToArray());
        Assert.IsTrue(properties.All(property => property.SetMethod is null));
    }

    [TestMethod]
    public void Constructor_RequiresVersionTwoCanonicalIdentitiesAndModelFacts()
    {
        ModelInspectionHandoff valid = CreateHandoff();

        Assert.AreEqual((ushort)2, valid.SchemaVersion);
        Assert.AreEqual(HandoffId, valid.ModelInspectionHandoffId);
        Assert.AreEqual(ModelRunId, valid.ModelInspectionRunId);
        Assert.AreEqual(ModelInspectionOutcome.Ready, valid.Outcome);
        Assert.AreEqual(Sha256, valid.ModelSha256);
        Assert.AreEqual(4_096L, valid.ModelLengthBytes);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionHandoff(
                1,
                HandoffId,
                ModelRunId,
                ModelInspectionOutcome.Ready,
                Sha256,
                4_096));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionHandoff(
                2,
                Guid.Empty,
                ModelRunId,
                ModelInspectionOutcome.Ready,
                Sha256,
                4_096));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionHandoff(
                2,
                Guid.Parse("11111111-1111-3111-8111-111111111111"),
                ModelRunId,
                ModelInspectionOutcome.Ready,
                Sha256,
                4_096));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionHandoff(
                2,
                HandoffId,
                ModelRunId,
                ModelInspectionOutcome.Unsupported,
                Sha256,
                4_096));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionHandoff(
                2,
                HandoffId,
                ModelRunId,
                ModelInspectionOutcome.Ready,
                Sha256.ToUpperInvariant(),
                4_096));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionHandoff(
                2,
                HandoffId,
                ModelRunId,
                ModelInspectionOutcome.Ready,
                Sha256,
                0));
    }

    [TestMethod]
    public void Codec_UsesTheExactCanonicalBoundedRepresentation()
    {
        ModelInspectionHandoff handoff = CreateHandoff();

        byte[] encoded = ModelInspectionHandoffCodec.Serialize(handoff);

        Assert.IsLessThanOrEqualTo(512, encoded.Length);
        Assert.AreEqual(
            "{\"schemaVersion\":2," +
            "\"modelInspectionHandoffId\":\"11111111-1111-4111-8111-111111111111\"," +
            "\"modelInspectionRunId\":\"22222222-2222-4222-8222-222222222222\"," +
            "\"outcome\":\"Ready\"," +
            $"\"modelSha256\":\"{Sha256}\"," +
            "\"modelLengthBytes\":4096}",
            Encoding.UTF8.GetString(encoded));
        Assert.IsTrue(
            ModelInspectionHandoffCodec.TryDeserialize(encoded, out var parsed));
        Assert.IsNotNull(parsed);
        Assert.AreEqual(handoff.ModelInspectionHandoffId, parsed.ModelInspectionHandoffId);
        Assert.AreEqual(handoff.ModelInspectionRunId, parsed.ModelInspectionRunId);
        Assert.AreEqual(handoff.Outcome, parsed.Outcome);
        Assert.AreEqual(handoff.ModelSha256, parsed.ModelSha256);
        Assert.AreEqual(handoff.ModelLengthBytes, parsed.ModelLengthBytes);
    }

    [TestMethod]
    public void Codec_EmitsTheSharedCanonicalHandoffBytes()
    {
        ModelInspectionHandoff handoff = CreateHandoff();
        var shared = new SharedHandoff(
            2,
            HandoffId,
            ModelRunId,
            SharedOutcome.Ready,
            Sha256,
            4_096);

        CollectionAssert.AreEqual(
            shared.ToCanonicalUtf8Json(),
            ModelInspectionHandoffCodec.Serialize(handoff));
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"schemaVersion\":2,\"modelInspectionHandoffId\":\"11111111-1111-4111-8111-111111111111\",\"modelInspectionRunId\":\"22222222-2222-4222-8222-222222222222\",\"outcome\":\"Ready\",\"modelSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"modelLengthBytes\":4096,\"path\":\"C:\\\\Private\\\\model.gguf\"}")]
    [DataRow("{\"schemaVersion\":2,\"schemaVersion\":2,\"modelInspectionHandoffId\":\"11111111-1111-4111-8111-111111111111\",\"modelInspectionRunId\":\"22222222-2222-4222-8222-222222222222\",\"outcome\":\"Ready\",\"modelSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"modelLengthBytes\":4096}")]
    [DataRow("{ \"schemaVersion\":2,\"modelInspectionHandoffId\":\"11111111-1111-4111-8111-111111111111\",\"modelInspectionRunId\":\"22222222-2222-4222-8222-222222222222\",\"outcome\":\"Ready\",\"modelSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"modelLengthBytes\":4096}")]
    public void Codec_RejectsMissingUnknownDuplicateOrNoncanonicalInput(
        string input)
    {
        Assert.IsFalse(ModelInspectionHandoffCodec.TryDeserialize(
            Encoding.UTF8.GetBytes(input),
            out ModelInspectionHandoff? handoff));
        Assert.IsNull(handoff);
    }

    [TestMethod]
    public void Codec_RejectsOversizedAndInvalidUtf8WithoutLeakingInput()
    {
        byte[] oversized = Enumerable.Repeat((byte)' ', 513).ToArray();
        byte[] invalidUtf8 = [0xc3, 0x28];

        Assert.IsFalse(ModelInspectionHandoffCodec.TryDeserialize(
            oversized,
            out _));
        Assert.IsFalse(ModelInspectionHandoffCodec.TryDeserialize(
            invalidUtf8,
            out _));
    }

    [TestMethod]
    public void Projector_AcceptsOnlyTheCurrentEligibleTerminalResult()
    {
        ModelInspectionExecutionResult ready =
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(ModelInspectionOutcome.Ready));
        ModelInspectionExecutionResult warning =
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(
                    ModelInspectionOutcome.ReadyWithWarnings));

        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            ready,
            out ModelInspectionHandoff? readyHandoff));
        Assert.IsNotNull(readyHandoff);
        Assert.AreEqual(ModelInspectionOutcome.Ready, readyHandoff.Outcome);
        Assert.AreEqual(Sha256, readyHandoff.ModelSha256);

        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            warning,
            out ModelInspectionHandoff? warningHandoff));
        Assert.IsNotNull(warningHandoff);
        Assert.AreEqual(
            ModelInspectionOutcome.ReadyWithWarnings,
            warningHandoff.Outcome);

        Assert.IsFalse(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            Guid.NewGuid(),
            ready,
            out _));
        Assert.IsFalse(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(
                    ModelInspectionOutcome.Unsupported)),
            out _));
        Assert.IsFalse(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            ModelInspectionExecutionResult.Cancelled(cooperative: true),
            out _));
    }

    private static ModelInspectionHandoff CreateHandoff() =>
        new(
            2,
            HandoffId,
            ModelRunId,
            ModelInspectionOutcome.Ready,
            Sha256,
            4_096);

}
