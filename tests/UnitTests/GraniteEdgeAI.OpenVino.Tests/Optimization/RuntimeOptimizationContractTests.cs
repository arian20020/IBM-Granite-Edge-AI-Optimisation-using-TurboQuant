using System.Text.Json;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class RuntimeOptimizationContractTests
{
    [TestMethod]
    public void StartSessionCarriesExactU8RuntimeRequestAndStartedEventReportsActual()
    {
        Guid sessionId = Guid.NewGuid();
        StartSessionCommand command = new(
            sessionId,
            Guid.NewGuid(),
            @"C:\model",
            new string('a', 64),
            new string('b', 64),
            1,
            new OpenVinoDeviceRequest("CPU"),
            new OpenVinoGenerationLimits(4096, 128),
            OpenVinoRuntimeOptions.U8);
        command.Validate();

        using JsonDocument json = JsonDocument.Parse(OpenVinoProtocolJson.Serialize(command));
        JsonElement runtime = json.RootElement.GetProperty("runtime");
        Assert.AreEqual("u8", runtime.GetProperty("kvCachePrecision").GetString());

        SessionStartedEvent started = new(
            sessionId,
            "CPU",
            ["CPU"],
            "u8",
            "u8",
            OpenVinoProtocol.OfficialProtocolId,
            BuildEvidence());
        started.Validate();
        Assert.AreEqual("u8", started.ActualKvCachePrecision);
    }

    [TestMethod]
    public void UnknownKvPrecisionIsRejected()
    {
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            new OpenVinoRuntimeOptions("q4_k").Validate());
    }

    [TestMethod]
    public void QualityRubricRejectsEmptyAndPunctuationOnlyNativeOutput()
    {
        PromptTurnResult structurallyValid = new(
            PromptTurnStatus.Completed,
            "Granite",
            PromptTokenCount: 3,
            GeneratedTokenCount: 8,
            Failure: null);
        Assert.IsTrue(OpenVinoSmokeQualityRubric.Passes(structurallyValid, 8));
        Assert.IsTrue(OpenVinoSmokeQualityRubric.Passes(
            structurallyValid with { GeneratedTokenCount = 1 }, 8),
            "A completed generation may legitimately stop early at EOS.");
        Assert.IsFalse(OpenVinoSmokeQualityRubric.Passes(
            structurallyValid with { GeneratedTokenCount = 0 }, 8));
        Assert.IsFalse(OpenVinoSmokeQualityRubric.Passes(
            structurallyValid with { GeneratedTokenCount = 9 }, 8));
        Assert.IsFalse(OpenVinoSmokeQualityRubric.Passes(
            structurallyValid with { Status = PromptTurnStatus.Stopped }, 8));
        Assert.IsFalse(OpenVinoSmokeQualityRubric.Passes(
            structurallyValid with { Text = "\uFFFD" }, 8));
        Assert.IsFalse(OpenVinoSmokeQualityRubric.Passes(
            structurallyValid with { Text = ", , , ," }, 8));
        Assert.IsFalse(OpenVinoSmokeQualityRubric.Passes(
            structurallyValid with { Text = string.Empty }, 8));
    }

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.3.0-22451-8a17657b995-releases/2026/3",
        "2026.3.0.0-3277-bd8d6542e3c",
        "2026.3.0.0-703-183c6f25cda",
        new string('c', 64));
}
