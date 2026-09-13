using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
public sealed class TurboQuantPrecisionContractTests
{
    [TestMethod]
    [DataRow("tbq4", false)]
    [DataRow("tbq3", false)]
    [DataRow("tbq4", true)]
    [DataRow("tbq3", true)]
    public void TurboQuantConversationAcceptsBothPinnedRuntimePrecisions(
        string precision, bool wrongCodec)
    {
        Guid sessionId = Guid.NewGuid();
        OpenVinoBuildEvidence build = BuildEvidence();
        OpenVinoRuntimeOptions runtime = precision == "tbq3"
            ? OpenVinoRuntimeOptions.Tbq3
            : OpenVinoRuntimeOptions.Tbq4;
        OpenVinoConversationValidator validator = new();
        validator.Accept(new HelloEvent(OpenVinoProtocol.TurboQuantProtocolId, build));
        validator.Accept(new StartSessionCommand(
            sessionId,
            Guid.NewGuid(),
            @"C:\models\granite",
            new string('a', 64),
            new string('b', 64),
            100,
            new OpenVinoDeviceRequest("CPU"),
            new OpenVinoGenerationLimits(128, 2),
            runtime));
        validator.Accept(new SessionStartedEvent(
            sessionId,
            "CPU",
            ["CPU"],
            precision,
            precision,
            OpenVinoProtocol.TurboQuantProtocolId,
            build));
        Guid turnId = Guid.NewGuid();
        validator.Accept(new PromptCommand(sessionId, turnId, "Hello", 2));
        validator.Accept(new GenerationStartedEvent(sessionId, turnId));
        bool tbq3 = precision == "tbq3";
        if (wrongCodec) tbq3 = !tbq3;
        TurboQuantCodec codec = tbq3 ? TurboQuantCodec.Tbq3 : TurboQuantCodec.Tbq4;
        int packed = tbq3 ? 24 : 32;
        var activation = new TurboQuantActivationEvent(sessionId, turnId,
            codec, codec, codec, codec, TurboQuantAttentionPath.Sdpa,
            64, 2, 22, 1, packed, 128, 22 * packed, 2816,
            TurboQuantEvidenceOrigin.OpenVinoProfilingApi, true);
        if (wrongCodec)
            Assert.ThrowsExactly<OpenVinoProtocolException>(() => validator.Accept(activation));
        else
            validator.Accept(activation);
    }

    [TestMethod]
    public void RuntimeOptionsAdmitTbq3AsADistinctPrecision()
    {
        OpenVinoRuntimeOptions.Tbq3.Validate();

        Assert.AreEqual("tbq3", OpenVinoRuntimeOptions.Tbq3.KvCachePrecision);
        Assert.AreNotEqual(
            OpenVinoRuntimeOptions.Tbq4.KvCachePrecision,
            OpenVinoRuntimeOptions.Tbq3.KvCachePrecision);
    }

    [TestMethod]
    public void OfficialConversationAdmitsScalarU4AsDistinctFromTbq4()
    {
        OpenVinoRuntimeOptions.U4.Validate();
        Assert.AreEqual("u4", OpenVinoRuntimeOptions.U4.KvCachePrecision);
        Assert.AreNotEqual(
            OpenVinoRuntimeOptions.Tbq4.KvCachePrecision,
            OpenVinoRuntimeOptions.U4.KvCachePrecision);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    public void ActivationEvidenceValidatesExactTbq3Widths(int sdpaNodes)
    {
        var activation = new TurboQuantActivationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TurboQuantCodec.Tbq3,
            TurboQuantCodec.Tbq3,
            TurboQuantCodec.Tbq3,
            TurboQuantCodec.Tbq3,
            TurboQuantAttentionPath.Sdpa,
            64,
            2,
            22,
            sdpaNodes,
            24,
            128,
            528,
            2816,
            TurboQuantEvidenceOrigin.OpenVinoProfilingApi,
            true);

        if (sdpaNodes == 0)
            Assert.ThrowsExactly<OpenVinoProtocolException>(activation.Validate);
        else
            activation.Validate();
    }

    [TestMethod]
    public void ActivationEvidenceRejectsRequestedActualCodecFallback()
    {
        var activation = new TurboQuantActivationEvent(
            Guid.NewGuid(), Guid.NewGuid(),
            TurboQuantCodec.Tbq3, TurboQuantCodec.Tbq3,
            TurboQuantCodec.Tbq4, TurboQuantCodec.Tbq4,
            TurboQuantAttentionPath.Sdpa, 64, 2, 22, 1,
            24, 128, 528, 2816,
            TurboQuantEvidenceOrigin.OpenVinoProfilingApi, true);

        Assert.ThrowsExactly<OpenVinoProtocolException>(activation.Validate);
    }

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.5.0-22950-f5f594dc0c9",
        "6fbc103538d30d42da4b0b5130a4792a20f728ba",
        "2026.5.0.0-737-824033c3061",
        new string('1', 64),
        new TurboQuantBuildEvidence(
            "f5f594dc0c9e5961785f0d17743486d52eac87e7",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            new string('2', 64),
            new string('3', 64)));
}
