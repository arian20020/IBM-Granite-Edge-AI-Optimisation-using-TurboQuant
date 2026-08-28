using GraniteEdgeAI.GgufQuantization.WorkerClient;

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
        var parent = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SystemRoot"] = existing,
            ["WINDIR"] = existing,
            ["TEMP"] = existing,
            ["TMP"] = existing,
            ["PATH"] = @"C:\private\toolchain",
            ["SECRET_CANARY"] = "must-not-propagate"
        };

        IReadOnlyDictionary<string, string> child =
            GgufQuantizerEnvironmentPolicy.Create(parent);

        CollectionAssert.AreEquivalent(ExpectedKeys, child.Keys.ToArray());
        Assert.IsFalse(child.ContainsKey("PATH"));
        Assert.IsFalse(child.ContainsKey("SECRET_CANARY"));
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_IPC"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Debugger"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Profiler"]);
    }
}
