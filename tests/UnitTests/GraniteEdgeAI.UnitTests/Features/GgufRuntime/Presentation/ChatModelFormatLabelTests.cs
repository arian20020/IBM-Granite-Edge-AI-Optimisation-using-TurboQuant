using GraniteEdgeAI.Features.GgufRuntime.Presentation;
using GraniteEdgeAI.Features.ChatModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Presentation;

[TestClass]
public sealed class ChatModelFormatLabelTests
{
    [TestMethod]
    [DataRow("output-0123456789abcdef0123456789abcdef-1", "Granite-4.1-3B", "Granite-4.1-3B")]
    [DataRow("output-0123456789abcdef0123456789abcdef-1", null, "Optimised OpenVINO model")]
    [DataRow("output-0123456789abcdef0123456789abcdef", "output-abcdef0123456789abcdef0123456789-2", "Optimised OpenVINO model")]
    [DataRow("My custom model", "Granite-4.1-3B", "My custom model")]
    [DataRow("output-results", null, "output-results")]
    [DataRow(null, null, "Imported OpenVINO model")]
    [DataRow("  ", null, "Imported OpenVINO model")]
    [DataRow("C:\\Models\\My model\\", null, "My model")]
    [DataRow("  My\n model  ", null, "My model")]
    public void OpenVinoDisplayNameUsesReadableOriginOnlyForGeneratedNames(string? candidate, string? origin, string expected)
    {
        Assert.AreEqual(expected, ChatModelDisplayName.OpenVino(candidate, origin));
    }

    [TestMethod]
    public void OpenVinoDisplayNameStaysWithinDescriptorLimit()
    {
        Assert.AreEqual(new string('a', 128), ChatModelDisplayName.OpenVino(new string('a', 140)));
    }

    [TestMethod]
    [DataRow("Q2K", "Q2_K")]
    [DataRow("Q3KM", "Q3_K_M")]
    [DataRow("Q4KM", "Q4_K_M")]
    [DataRow("Q5KM", "Q5_K_M")]
    [DataRow("Q6K", "Q6_K")]
    [DataRow("Q8_0", "Q8_0")]
    [DataRow("F16", "F16")]
    [DataRow("BF16", "BF16")]
    [DataRow("TQ3_1S", "TQ3_1S")]
    [DataRow("TQ4_1S", "TQ4_1S")]
    public void GgufWeightFormatsRemainSeparateFromCache(string weights, string expected)
    {
        Assert.AreEqual($"F16 cache (selected) · {expected} weights · GGUF",
            ChatModelFormatLabel.Gguf(weights, "F16", "F16"));
    }

    [TestMethod]
    [DataRow("Fp16", "FP16")]
    [DataRow("EightBit", "INT8")]
    [DataRow("FourBit", "INT4")]
    [DataRow("MxFp4", "MXFP4")]
    public void OpenVinoWeightFormatsRemainSeparateFromCache(string weights, string expected)
    {
        Assert.AreEqual($"TQ3 cache (selected) · {expected} weights · OpenVINO",
            ChatModelFormatLabel.OpenVino(weights, "tbq3"));
    }

    [TestMethod]
    [DataRow("tbq3", "TQ3")]
    [DataRow("tbq4", "TQ4")]
    [DataRow("u4", "U4")]
    [DataRow("u8", "U8")]
    [DataRow("Tbq3", "TQ3")]
    [DataRow("Tbq4", "TQ4")]
    [DataRow("U4", "U4")]
    [DataRow("U8", "U8")]
    public void OpenVinoSeparatesWeightsFromSelectedCache(string cache, string expected)
    {
        Assert.AreEqual($"{expected} cache (selected) · INT4 weights · OpenVINO",
            ChatModelFormatLabel.OpenVino("FourBit", cache));
    }

    [TestMethod]
    [DataRow("Turbo2", "TQ2")]
    [DataRow("Turbo3", "TQ3")]
    [DataRow("Turbo4", "TQ4")]
    [DataRow("F16", "F16")]
    [DataRow("Q8Zero", "Q8_0")]
    [DataRow("Q4Zero", "Q4_0")]
    public void GgufSeparatesWeightsFromSelectedCache(string cache, string expected)
    {
        Assert.AreEqual($"{expected} cache (selected) · Q4_K_M weights · GGUF",
            ChatModelFormatLabel.Gguf("Q4KM", cache, cache));
    }

    [TestMethod]
    public void DifferentKeyAndValueFormatsStayDistinct()
    {
        Assert.AreEqual("K: TQ3 / V: Q8_0 cache (selected) · Q5_K_M weights · GGUF",
            ChatModelFormatLabel.Gguf("Q5KM", "Turbo3", "Q8Zero"));
    }

    [TestMethod]
    public void DefaultAndUnknownCacheNeverClaimActivation()
    {
        Assert.AreEqual("default cache (unverified) · FP16 weights · OpenVINO",
            ChatModelFormatLabel.OpenVino("Fp16", "released-default"));
        Assert.AreEqual("default cache (unverified) · FP16 weights · OpenVINO",
            ChatModelFormatLabel.OpenVino("Fp16", "ReleasedDefault"));
        Assert.AreEqual("cache unknown · weights unknown · OpenVINO",
            ChatModelFormatLabel.OpenVino(null, "unexpected"));
        Assert.AreEqual("cache unknown · weights unknown · GGUF",
            ChatModelFormatLabel.Gguf("Imported", null, null));
    }

    [TestMethod]
    public void TurboQuantWeightNameDoesNotBecomeCacheEvidence()
    {
        Assert.AreEqual("F16 cache (selected) · TQ3_1S weights · GGUF",
            ChatModelFormatLabel.Gguf("TQ3_1S", "F16", "F16"));
    }

    [TestMethod]
    public void SelectedCacheReachesButtonMenuAndAccessibleNamesWithoutChangingIdentity()
    {
        string label = ChatModelFormatLabel.OpenVino("FourBit", "tbq3");
        var descriptor = ChatModelDescriptor.Create("model-1", "Original name", ChatModelRoute.OpenVino,
            label, "OpenVINO · CPU", ChatModelReadiness.Ready);
        var snapshot = new ChatModelSnapshot(descriptor.Id, descriptor.DisplayName, descriptor.Route,
            descriptor.FormatLabel, descriptor.RuntimeLabel, descriptor.Readiness, true);
        var result = ChatModelSelectorPresentation.Create([snapshot]);
        Assert.AreEqual("TQ3 cache (selected) · INT4 weights · OpenVINO", result.CurrentLabel);
        Assert.AreEqual(label, result.Models[0].FormatLabel);
        Assert.IsTrue(result.CurrentAutomationName.Contains(label, StringComparison.Ordinal));
        Assert.IsTrue(result.Models[0].AutomationName.Contains(label, StringComparison.Ordinal));
        Assert.AreEqual("Original name", result.Models[0].DisplayName);
        Assert.AreEqual("model-1", result.ActiveModelId);
        Assert.IsTrue(result.Models[0].IsSelectable);
    }

    [TestMethod]
    public void LabelsFitDescriptorBoundEvenWithUnknownOrMixedInputs()
    {
        string[] values = ["Turbo2", "Turbo3", "Turbo4", "Q8Zero", "Q4Zero", "F16", new string('x', 200)];
        foreach (string key in values)
        foreach (string value in values)
            Assert.IsTrue(ChatModelFormatLabel.Gguf(new string('x', 200), key, value).Length <= 96);
        Assert.IsTrue(ChatModelFormatLabel.OpenVino(new string('x', 200), new string('x', 200)).Length <= 96);
    }
}
