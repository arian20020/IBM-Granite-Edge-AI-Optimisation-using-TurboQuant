using System.Collections.ObjectModel;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Processes;

[TestClass]
public sealed class TrustedToolEnvironmentPolicyTests
{
    private static readonly string[] ExpectedKeys =
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
    public void CreateCarriesOnlyRequiredDirectoriesAndDisablesDiagnostics()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent["PATH"] = "unapproved";
        parent["HTTP_PROXY"] = "http://private-proxy.invalid";
        parent["COR_ENABLE_PROFILING"] = "1";

        IReadOnlyDictionary<string, string> child =
            TrustedToolEnvironmentPolicy.Create(parent);

        CollectionAssert.AreEquivalent(
            ExpectedKeys,
            child.Keys.ToArray());
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_IPC"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Debugger"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Profiler"]);
        Assert.IsInstanceOfType<ReadOnlyDictionary<string, string>>(child);
    }

    [TestMethod]
    public void CreateRejectsCaseConfusedDuplicateNames()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent["SYSTEMROOT"] = parent["SystemRoot"];

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => TrustedToolEnvironmentPolicy.Create(parent));

        Assert.AreEqual(
            "The trusted hardware tool environment could not be secured.",
            failure.Message);
    }

    [TestMethod]
    public void CreateRejectsRelativeRequiredDirectoryWithoutEchoingIt()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent["TEMP"] = "private-relative-path";

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => TrustedToolEnvironmentPolicy.Create(parent));

        Assert.IsFalse(
            failure.Message.Contains("private-relative-path", StringComparison.Ordinal));
    }

    private static Dictionary<string, string?> ValidParent()
    {
        string directory = Path.GetTempPath();
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SystemRoot"] = directory,
            ["WINDIR"] = directory,
            ["TEMP"] = directory,
            ["TMP"] = directory,
        };
    }
}
