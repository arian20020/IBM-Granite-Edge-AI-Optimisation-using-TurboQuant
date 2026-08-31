using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class QuantizerEnvironmentIsolationIntegrationTests
{
    private static readonly string[] ExpectedKeys =
    [
        "SystemRoot", "WINDIR", "TEMP", "TMP",
        "DOTNET_EnableDiagnostics", "DOTNET_EnableDiagnostics_IPC",
        "DOTNET_EnableDiagnostics_Debugger",
        "DOTNET_EnableDiagnostics_Profiler"
    ];

    [TestMethod]
    public void QuantizerChildEnvironmentIsAllowlistedAndDiagnosticsDisabled()
    {
        string existing = Path.GetTempPath();
        using TrustedToolOperationEnvironment operation =
            TrustedToolOperationEnvironment.CreateCurrent(includeDotnetRoots: false);
        IReadOnlyDictionary<string, string> child = operation.Variables;

        CollectionAssert.AreEquivalent(ExpectedKeys, child.Keys.ToArray());
        Assert.IsFalse(child.ContainsKey("PATH"));
        Assert.IsFalse(child.ContainsKey("SECRET_CANARY"));
        Assert.AreNotEqual(existing, child["TEMP"],
            "The quantizer must receive an operation-owned private TEMP, not the parent TEMP.");
        Assert.AreNotEqual(existing, child["TMP"],
            "The quantizer must receive an operation-owned private TMP, not the parent TMP.");
        Assert.AreEqual(child["TEMP"], child["TMP"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_IPC"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Debugger"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Profiler"]);
    }
}
