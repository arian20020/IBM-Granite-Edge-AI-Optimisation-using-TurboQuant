using System.Collections.ObjectModel;
using GraniteEdgeAI.GgufQuantization.WorkerClient;

namespace GraniteEdgeAI.GgufQuantization.WorkerClient.Tests;

[TestClass]
public sealed class GgufQuantizerEnvironmentPolicyTests
{
    [TestMethod]
    public void CreateUsesClosedAllowlistAndDisablesDiagnostics()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent["GRANITE_SECURITY_AUDIT_SENTINEL"] = "parent-secret";
        parent["PATH"] = "unapproved";

        IReadOnlyDictionary<string, string> child =
            GgufQuantizerEnvironmentPolicy.Create(parent);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "SystemRoot",
                "WINDIR",
                "TEMP",
                "TMP",
                "DOTNET_EnableDiagnostics",
                "DOTNET_EnableDiagnostics_IPC",
                "DOTNET_EnableDiagnostics_Debugger",
                "DOTNET_EnableDiagnostics_Profiler",
            },
            child.Keys.ToArray());
        Assert.IsFalse(child.ContainsKey("GRANITE_SECURITY_AUDIT_SENTINEL"));
        Assert.IsFalse(child.ContainsKey("PATH"));
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_IPC"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Debugger"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Profiler"]);
        Assert.IsInstanceOfType<ReadOnlyDictionary<string, string>>(child);
    }

    [TestMethod]
    public void CreateRejectsCaseVariantDuplicates()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent["SYSTEMROOT"] = parent["SystemRoot"];

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => GgufQuantizerEnvironmentPolicy.Create(parent));

        Assert.AreEqual(
            "The GGUF quantizer environment could not be secured.",
            failure.Message);
    }

    [TestMethod]
    public void CreateRejectsMissingRequiredDirectory()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent.Remove("TEMP");

        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => GgufQuantizerEnvironmentPolicy.Create(parent));
    }

    [TestMethod]
    public void CreateRejectsRelativeDirectoryWithoutDisclosingIt()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent["TMP"] = "private-relative-value";

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => GgufQuantizerEnvironmentPolicy.Create(parent));

        Assert.IsFalse(failure.Message.Contains("private-relative-value", StringComparison.Ordinal));
    }

    private static Dictionary<string, string?> ValidParent()
    {
        string existing = Path.GetTempPath();
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SystemRoot"] = existing,
            ["WINDIR"] = existing,
            ["TEMP"] = existing,
            ["TMP"] = existing,
        };
    }
}
