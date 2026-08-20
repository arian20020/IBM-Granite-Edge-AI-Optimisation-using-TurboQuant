using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Tests;

[TestClass]
public sealed class GgufWorkerEnvironmentPolicyTests
{
    private static readonly string[] ExpectedChildKeys =
    [
        "SystemRoot",
        "WINDIR",
        "TEMP",
        "TMP",
        "DOTNET_EnableDiagnostics",
        "DOTNET_EnableDiagnostics_IPC",
        "DOTNET_EnableDiagnostics_Debugger",
        "DOTNET_EnableDiagnostics_Profiler",
    ];

    [TestMethod]
    public void CreateRetainsOnlyRequiredRuntimePathsAndDisabledDiagnostics()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var parent = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = windows,
            ["WINDIR"] = windows,
            ["TEMP"] = temp,
            ["TMP"] = temp,
            ["PATH"] = "C:\\untrusted-bin",
            ["HTTP_PROXY"] = "http://proxy.invalid",
            ["HF_TOKEN"] = "secret",
            ["OPENAI_API_KEY"] = "secret",
            ["GGML_VK_VISIBLE_DEVICES"] = "0",
        };

        IReadOnlyDictionary<string, string> child =
            GgufWorkerEnvironmentPolicy.Create(parent);

        CollectionAssert.AreEquivalent(
            ExpectedChildKeys,
            child.Keys.ToArray());
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics"]);
        Assert.IsFalse(child.ContainsKey("PATH"));
        Assert.IsFalse(child.ContainsKey("HF_TOKEN"));
    }

    [TestMethod]
    public void CreateRejectsMissingRequiredDirectoryWithoutEchoingValue()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string secretPath = "C:\\secret-user-value-that-does-not-exist";
        var parent = new Dictionary<string, string?>
        {
            ["SystemRoot"] = windows,
            ["WINDIR"] = windows,
            ["TEMP"] = secretPath,
            ["TMP"] = Path.GetTempPath(),
        };

        GgufWorkerPolicyException exception = Assert.ThrowsExactly<GgufWorkerPolicyException>(
            () => GgufWorkerEnvironmentPolicy.Create(parent));

        Assert.AreEqual("worker-environment-policy-failed", exception.Code);
        Assert.IsFalse(exception.Message.Contains(secretPath, StringComparison.Ordinal));
    }
}
