using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol;

[TestClass]
[TestCategory("Contract")]
public sealed class ModelInspectionProjectionV2Tests
{
    private static readonly Guid RunId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid HandoffId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly string Digest = new('a', 64);

    [TestMethod]
    public void GgufProjectionSerializesCanonicalPathPrivateSchemaV2()
    {
        ModelInspectionProjectionV2 projection = Create(
            ModelInspectionRoute.Gguf,
            "gguf");

        byte[] payload = projection.ToCanonicalUtf8Json();

        string expected =
            "{\"schemaVersion\":2," +
            "\"modelSource\":{\"modelType\":\"gguf\",\"route\":\"gguf\"," +
            $"\"modelSha256\":\"{Digest}\",\"modelLengthBytes\":4096}}," +
            "\"modelInspectionResult\":{" +
            $"\"modelInspectionRunId\":\"{RunId:D}\",\"outcome\":\"Ready\"," +
            $"\"route\":\"gguf\",\"modelSha256\":\"{Digest}\"," +
            "\"modelLengthBytes\":4096}," +
            "\"modelInspectionHandoff\":{\"schemaVersion\":2," +
            $"\"modelInspectionHandoffId\":\"{HandoffId:D}\"," +
            $"\"modelInspectionRunId\":\"{RunId:D}\",\"outcome\":\"Ready\"," +
            $"\"modelSha256\":\"{Digest}\",\"modelLengthBytes\":4096}}}}";
        Assert.AreEqual(expected, Encoding.UTF8.GetString(payload));
        Assert.IsLessThanOrEqualTo(
            ModelInspectionProjectionV2.MaximumCanonicalUtf8Bytes,
            payload.Length);
        Assert.IsFalse(expected.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(projection, ModelInspectionProjectionV2.Parse(payload));
    }

    [TestMethod]
    public void GgufAndOpenVinoUseTheSameProjectionShape()
    {
        byte[] gguf = Create(ModelInspectionRoute.Gguf, "gguf")
            .ToCanonicalUtf8Json();
        byte[] openVino = Create(ModelInspectionRoute.OpenVino, "openvino-ir")
            .ToCanonicalUtf8Json();

        using JsonDocument ggufDocument = JsonDocument.Parse(gguf);
        using JsonDocument openVinoDocument = JsonDocument.Parse(openVino);
        CollectionAssert.AreEqual(
            PropertyShape(ggufDocument.RootElement),
            PropertyShape(openVinoDocument.RootElement));
    }

    [TestMethod]
    [DataRow("source-digest")]
    [DataRow("source-length")]
    [DataRow("result-route")]
    [DataRow("result-run")]
    [DataRow("handoff-digest")]
    [DataRow("handoff-length")]
    [DataRow("handoff-run")]
    [DataRow("same-identities")]
    [DataRow("unsupported-model-type")]
    public void ValidateRejectsChangedStaleMismatchedOrUnsupportedIdentity(
        string mutation)
    {
        ModelInspectionProjectionV2 valid = Create(
            ModelInspectionRoute.Gguf,
            "gguf");
        ModelInspectionProjectionV2 invalid = mutation switch
        {
            "source-digest" => valid with
            {
                ModelSource = valid.ModelSource with { ModelSha256 = new string('b', 64) }
            },
            "source-length" => valid with
            {
                ModelSource = valid.ModelSource with { ModelLengthBytes = 4097 }
            },
            "result-route" => valid with
            {
                ModelInspectionResult = valid.ModelInspectionResult with
                {
                    Route = ModelInspectionRoute.OpenVino
                }
            },
            "result-run" => valid with
            {
                ModelInspectionResult = valid.ModelInspectionResult with
                {
                    ModelInspectionRunId = Guid.Parse(
                        "33333333-3333-4333-8333-333333333333")
                }
            },
            "handoff-digest" => valid with
            {
                ModelInspectionHandoff = valid.ModelInspectionHandoff with
                {
                    ModelSha256 = new string('b', 64)
                }
            },
            "handoff-length" => valid with
            {
                ModelInspectionHandoff = valid.ModelInspectionHandoff with
                {
                    ModelLengthBytes = 4097
                }
            },
            "handoff-run" => valid with
            {
                ModelInspectionHandoff = valid.ModelInspectionHandoff with
                {
                    ModelInspectionRunId = Guid.Parse(
                        "33333333-3333-4333-8333-333333333333")
                }
            },
            "same-identities" => valid with
            {
                ModelInspectionHandoff = valid.ModelInspectionHandoff with
                {
                    ModelInspectionHandoffId = RunId
                }
            },
            "unsupported-model-type" => valid with
            {
                ModelSource = valid.ModelSource with { ModelType = "onnx" }
            },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(invalid.Validate);
    }

    [TestMethod]
    [DataRow("extra-property")]
    [DataRow("reordered")]
    [DataRow("uppercase-digest")]
    [DataRow("path-property")]
    [DataRow("trailing-content")]
    public void ParseRejectsMalformedOrNonCanonicalPayload(string mutation)
    {
        string valid = Encoding.UTF8.GetString(
            Create(ModelInspectionRoute.Gguf, "gguf").ToCanonicalUtf8Json());
        string invalid = mutation switch
        {
            "extra-property" => valid.Replace(
                "{\"schemaVersion\":2,",
                "{\"schemaVersion\":2,\"extra\":true,",
                StringComparison.Ordinal),
            "reordered" => valid.Replace(
                "{\"schemaVersion\":2,\"modelSource\":",
                "{\"modelSource\":",
                StringComparison.Ordinal).Replace(
                    "\"modelInspectionResult\":",
                    "\"schemaVersion\":2,\"modelInspectionResult\":",
                    StringComparison.Ordinal),
            "uppercase-digest" => valid.Replace(Digest, Digest.ToUpperInvariant(), StringComparison.Ordinal),
            "path-property" => valid.Replace(
                "\"modelType\":\"gguf\",",
                "\"path\":\"private\",\"modelType\":\"gguf\",",
                StringComparison.Ordinal),
            "trailing-content" => valid + "\n",
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            ModelInspectionProjectionV2.Parse(Encoding.UTF8.GetBytes(invalid)));
    }

    private static ModelInspectionProjectionV2 Create(
        ModelInspectionRoute route,
        string modelType) =>
        ModelInspectionProjectionV2.Create(
            route,
            modelType,
            RunId,
            HandoffId,
            ModelInspectionOutcomeV2.Ready,
            Digest,
            4096);

    private static string[] PropertyShape(JsonElement root) =>
        root.EnumerateObject()
            .SelectMany(property => property.Value.ValueKind == JsonValueKind.Object
                ? property.Value.EnumerateObject().Select(child =>
                    property.Name + "." + child.Name)
                : [property.Name])
            .ToArray();
}
