using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Defines the exact environment allowlist used for the protected worker.
/// Values not explicitly required by the runtime must not cross the process
/// boundary.
/// </summary>
[TestClass]
public sealed class WorkerEnvironmentPolicyTests
{
    [TestMethod]
    public void CreateDropsPathSecretsRequestsAndDiagnosticPorts()
    {
        Dictionary<string, string?> parent = CreateRequiredParent();
        parent["PATH"] = @"C:\Untrusted";
        parent["AZURE_CLIENT_SECRET"] = "secret";
        parent["OPENAI_API_KEY"] = "secret";
        parent["DOTNET_DiagnosticPorts"] = "listen";
        parent["REQUEST_ID"] = "sensitive";
        parent["MODEL_PATH"] = @"C:\Sensitive\model.gguf";

        IReadOnlyDictionary<string, string> child =
            WorkerEnvironmentPolicy.Create(parent);

        Assert.IsFalse(child.ContainsKey("PATH"));
        Assert.IsFalse(child.ContainsKey("AZURE_CLIENT_SECRET"));
        Assert.IsFalse(child.ContainsKey("OPENAI_API_KEY"));
        Assert.IsFalse(child.ContainsKey("DOTNET_DiagnosticPorts"));
        Assert.IsFalse(child.ContainsKey("REQUEST_ID"));
        Assert.IsFalse(child.ContainsKey("MODEL_PATH"));
    }

    [TestMethod]
    public void CreateCopiesOnlyApprovedParentKeys()
    {
        Dictionary<string, string?> parent = CreateRequiredParent();
        parent["PROCESSOR_ARCHITECTURE"] = "AMD64";
        parent["PROCESSOR_IDENTIFIER"] = "Test processor";

        IReadOnlyDictionary<string, string> child =
            WorkerEnvironmentPolicy.Create(parent);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "SystemRoot",
                "WINDIR",
                "ComSpec",
                "TEMP",
                "TMP",
                "PROCESSOR_ARCHITECTURE",
                "PROCESSOR_IDENTIFIER",
                "DOTNET_EnableDiagnostics",
                "DOTNET_EnableDiagnostics_IPC",
                "DOTNET_EnableDiagnostics_Debugger",
                "DOTNET_EnableDiagnostics_Profiler",
                "COMPlus_EnableDiagnostics"
            },
            child.Keys.ToArray());
        Assert.AreEqual(@"C:\Windows", child["SystemRoot"]);
        Assert.AreEqual(@"C:\Temp", child["TEMP"]);
    }

    [TestMethod]
    public void CreateForcesEveryManagedDiagnosticEntryPointOff()
    {
        Dictionary<string, string?> parent = CreateRequiredParent();
        parent["DOTNET_EnableDiagnostics"] = "1";
        parent["COMPlus_EnableDiagnostics"] = "1";

        IReadOnlyDictionary<string, string> child =
            WorkerEnvironmentPolicy.Create(parent);

        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_IPC"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Debugger"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Profiler"]);
        Assert.AreEqual("0", child["COMPlus_EnableDiagnostics"]);
    }

    [TestMethod]
    public void CreateRejectsMissingRequiredWindowsValue()
    {
        Dictionary<string, string?> parent = CreateRequiredParent();
        parent.Remove("SystemRoot");

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => WorkerEnvironmentPolicy.Create(parent));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerEnvironmentPolicyFailed,
            error.Failure.Code);
        Assert.IsFalse(error.Failure.Message.Contains("SystemRoot", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateRejectsNulInApprovedValueWithoutEchoingIt()
    {
        Dictionary<string, string?> parent = CreateRequiredParent();
        parent["TEMP"] = "safe\0sensitive";

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => WorkerEnvironmentPolicy.Create(parent));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerEnvironmentPolicyFailed,
            error.Failure.Code);
        Assert.IsFalse(error.Failure.Message.Contains("sensitive", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateTreatsParentKeysCaseInsensitively()
    {
        Dictionary<string, string?> parent = new(StringComparer.OrdinalIgnoreCase)
        {
            ["systemroot"] = @"C:\Windows",
            ["windir"] = @"C:\Windows",
            ["comspec"] = @"C:\Windows\System32\cmd.exe",
            ["temp"] = @"C:\Temp",
            ["tmp"] = @"C:\Temp"
        };

        IReadOnlyDictionary<string, string> child =
            WorkerEnvironmentPolicy.Create(parent);

        Assert.AreEqual(@"C:\Windows", child["SystemRoot"]);
        Assert.AreEqual(@"C:\Temp", child["TMP"]);
    }

    private static Dictionary<string, string?> CreateRequiredParent() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = @"C:\Windows",
            ["WINDIR"] = @"C:\Windows",
            ["ComSpec"] = @"C:\Windows\System32\cmd.exe",
            ["TEMP"] = @"C:\Temp",
            ["TMP"] = @"C:\Temp"
        };
}
