using System.Text;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class SupportCodeTests
{
    [TestMethod]
    public void FailureSerializationEmitsEveryFixedSupportCodeInsteadOfAnArbitraryDiagnosticString()
    {
        Guid sessionId = Guid.Parse("d8e0372f-7cf0-47eb-b069-700acde19f1b");
        Guid turnId = Guid.Parse("ce258ce1-3b3d-4c14-8742-91ddc18f0774");

        (OpenVinoSupportCode Code, string WireValue)[] expectedCodes =
        [
            (OpenVinoSupportCode.PackageMissingResource, "package_missing_resource"),
            (OpenVinoSupportCode.PackageInconsistentResource, "package_inconsistent_resource"),
            (OpenVinoSupportCode.PackageUnsafePath, "package_unsafe_path"),
            (OpenVinoSupportCode.PackageChanged, "package_changed"),
            (OpenVinoSupportCode.PackageUnreadable, "package_unreadable"),
            (OpenVinoSupportCode.ModelArchitectureUnsupported, "model_architecture_unsupported"),
            (OpenVinoSupportCode.ModelTaskUnsupported, "model_task_unsupported"),
            (OpenVinoSupportCode.TokenizerUnsupported, "tokenizer_unsupported"),
            (OpenVinoSupportCode.RuntimeIntegrityFailed, "runtime_integrity_failed"),
            (OpenVinoSupportCode.RuntimeDependencyMissing, "runtime_dependency_missing"),
            (OpenVinoSupportCode.RuntimeLoadFailed, "runtime_load_failed"),
            (OpenVinoSupportCode.RuntimeDeviceUnavailable, "runtime_device_unavailable"),
            (OpenVinoSupportCode.RuntimeDeviceMismatch, "runtime_device_mismatch"),
            (OpenVinoSupportCode.RuntimeContextExceeded, "runtime_context_exceeded"),
            (OpenVinoSupportCode.RuntimeProtocolFailed, "runtime_protocol_failed"),
            (OpenVinoSupportCode.RuntimeTimedOut, "runtime_timed_out"),
            (OpenVinoSupportCode.OperationCancelled, "operation_cancelled"),
            (OpenVinoSupportCode.ConversionPreflightFailed, "conversion_preflight_failed"),
            (OpenVinoSupportCode.ConversionFailed, "conversion_failed"),
            (OpenVinoSupportCode.ConversionOutputInvalid, "conversion_output_invalid"),
            (OpenVinoSupportCode.ConversionPublishFailed, "conversion_publish_failed"),
            (OpenVinoSupportCode.OptimizationUnsupported, "optimization_unsupported"),
            (OpenVinoSupportCode.TurboQuantUnavailable, "turboquant_unavailable"),
            (OpenVinoSupportCode.TurboQuantActivationUnverified, "turboquant_activation_unverified")
        ];

        Assert.AreEqual(24, expectedCodes.Length);
        foreach ((OpenVinoSupportCode code, string wireValue) in expectedCodes)
        {
            string json = Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(
                new TurnFailedEvent(sessionId, turnId, code)));
            StringAssert.Contains(json, "\"supportCode\":\"" + wireValue + "\"");
        }
    }

    [TestMethod]
    public void FailureParsingRejectsRawNativeExceptionTextInsteadOfLeakingItThroughSupportCode()
    {
        byte[] rawDiagnostic = Encoding.UTF8.GetBytes(
            """{"eventType":"turnFailed","sessionId":"d8e0372f-7cf0-47eb-b069-700acde19f1b","turnId":"ce258ce1-3b3d-4c14-8742-91ddc18f0774","supportCode":"native backend failed at C:\\users\\secret"}""");

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoProtocolJson.DeserializeEvent(rawDiagnostic));
    }
}
