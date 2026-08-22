using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class GpuDeviceContractTests
{
    private static readonly Guid SessionId =
        Guid.Parse("9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0");
    private static readonly Guid InspectionId =
        Guid.Parse("6e1ff10c-fd83-4b03-9a12-d35247e5a6a3");
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    [DataRow("CPU")]
    [DataRow("GPU")]
    [DataRow("GPU.0")]
    [DataRow("GPU.12")]
    public void DeviceRequestAcceptsOnlyCpuOrExplicitGpuIdentity(string deviceId)
    {
        new OpenVinoDeviceRequest(deviceId).Validate();
    }

    [TestMethod]
    [DataRow("AUTO")]
    [DataRow("HETERO")]
    [DataRow("MULTI")]
    [DataRow("NPU")]
    [DataRow("gpu")]
    [DataRow("GPU.")]
    [DataRow("GPU.-1")]
    [DataRow("GPU.01")]
    [DataRow(" GPU")]
    [DataRow("GPU ")]
    public void DeviceRequestRejectsFallbackAndNonCanonicalIdentities(string deviceId)
    {
        Assert.ThrowsExactly<OpenVinoProtocolException>(
            () => new OpenVinoDeviceRequest(deviceId).Validate());
    }

    [TestMethod]
    [DataRow("AUTO")]
    [DataRow("HETERO:GPU,CPU")]
    [DataRow("GPU.01")]
    public void SessionStartedRejectsUnapprovedActualExecutionIdentity(string actualDevice)
    {
        SessionStartedEvent started = new(
            Guid.Parse("9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0"),
            "GPU.0",
            [actualDevice],
            OpenVinoProtocol.OfficialProtocolId,
            new OpenVinoBuildEvidence(
                "2026.3.0-22451-8a17657b995-releases/2026/3",
                "2026.3.0.0-3277-bd8d6542e3c",
                "2026.3.0.0-703-183c6f25cda",
                new string('1', 64)));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => started.Validate());
    }

    [TestMethod]
    public void ConversationRejectsCpuResolutionForExplicitGpuRequest()
    {
        OpenVinoBuildEvidence build = BuildEvidence();
        OpenVinoConversationValidator validator = new();
        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId, build));
        validator.Accept(new StartSessionCommand(
            SessionId,
            InspectionId,
            @"C:\operation\package",
            Digest,
            Digest,
            88,
            new OpenVinoDeviceRequest("GPU.0"),
            new OpenVinoGenerationLimits(64, 2)));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new SessionStartedEvent(
                SessionId,
                "GPU.0",
                ["CPU"],
                OpenVinoProtocol.OfficialProtocolId,
                build)));
    }

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.3.0-22451-8a17657b995-releases/2026/3",
        "2026.3.0.0-3277-bd8d6542e3c",
        "2026.3.0.0-703-183c6f25cda",
        new string('1', 64));
}
