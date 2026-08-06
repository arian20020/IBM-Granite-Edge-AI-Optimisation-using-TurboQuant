using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Defines the exact environment allowlist used for the protected worker.
/// Values not explicitly approved by the hardened Gate 2 specification must
/// not cross the process boundary.
/// </summary>
[TestClass]
public sealed class WorkerEnvironmentPolicyTests
{
    [TestMethod]
    public void CreateDropsPathSecretsRequestsAndDiagnosticPorts()
    {
        Dictionary<string, string?> parent = CreateRequiredParent();
        parent["PATH"] = @"C:\Untrusted";
        parent["ComSpec"] = @"C:\Windows\System32\cmd.exe";
        parent["PROCESSOR_ARCHITECTURE"] = "AMD64";
        parent["AZURE_CLIENT_SECRET"] = "secret";
        parent["OPENAI_API_KEY"] = "secret";
        parent["DOTNET_DiagnosticPorts"] = "listen";
        parent["COMPlus_EnableDiagnostics"] = "1";
        parent["REQUEST_ID"] = "sensitive";
        parent["MODEL_PATH"] = @"C:\Sensitive\model.gguf";

        IReadOnlyDictionary<string, string> child =
            WorkerEnvironmentPolicy.Create(parent);

        string[] forbidden =
        [
            "PATH",
            "ComSpec",
            "PROCESSOR_ARCHITECTURE",
            "AZURE_CLIENT_SECRET",
            "OPENAI_API_KEY",
            "DOTNET_DiagnosticPorts",
            "COMPlus_EnableDiagnostics",
            "REQUEST_ID",
            "MODEL_PATH"
        ];

        foreach (string key in forbidden)
        {
            Assert.IsFalse(child.ContainsKey(key), key);
        }
    }

    [TestMethod]
    public void CreateCopiesOnlyApprovedValidatedParentKeys()
    {
        Dictionary<string, string?> parent = CreateRequiredParent();
        string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
        parent["DOTNET_ROOT"] = runtimeDirectory;
        parent["DOTNET_ROOT_X64"] = runtimeDirectory;

        IReadOnlyDictionary<string, string> child =
            WorkerEnvironmentPolicy.Create(parent);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "SystemRoot",
                "WINDIR",
                "TEMP",
                "TMP",
                "DOTNET_ROOT",
                "DOTNET_ROOT_X64",
                "DOTNET_EnableDiagnostics",
                "DOTNET_EnableDiagnostics_IPC",
                "DOTNET_EnableDiagnostics_Debugger",
                "DOTNET_EnableDiagnostics_Profiler"
            },
            child.Keys.ToArray());
        Assert.AreEqual(parent["SystemRoot"], child["SystemRoot"]);
        Assert.AreEqual(parent["TEMP"], child["TEMP"]);
        Assert.AreEqual(runtimeDirectory, child["DOTNET_ROOT"]);
    }

    [TestMethod]
    public void CreateForcesEveryApprovedDiagnosticEntryPointOff()
    {
        Dictionary<string, string?> parent = CreateRequiredParent();
        parent["DOTNET_EnableDiagnostics"] = "1";
        parent["DOTNET_EnableDiagnostics_IPC"] = "1";
        parent["DOTNET_EnableDiagnostics_Debugger"] = "1";
        parent["DOTNET_EnableDiagnostics_Profiler"] = "1";

        IReadOnlyDictionary<string, string> child =
            WorkerEnvironmentPolicy.Create(parent);

        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_IPC"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Debugger"]);
        Assert.AreEqual("0", child["DOTNET_EnableDiagnostics_Profiler"]);
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
    public void CreateRejectsInvalidApprovedPathWithoutEchoingIt()
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
        Dictionary<string, string?> required = CreateRequiredParent();
        Dictionary<string, string?> parent =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["systemroot"] = required["SystemRoot"],
                ["windir"] = required["WINDIR"],
                ["temp"] = required["TEMP"],
                ["tmp"] = required["TMP"]
            };

        IReadOnlyDictionary<string, string> child =
            WorkerEnvironmentPolicy.Create(parent);

        Assert.AreEqual(required["SystemRoot"], child["SystemRoot"]);
        Assert.AreEqual(required["TMP"], child["TMP"]);
    }

    private static Dictionary<string, string?> CreateRequiredParent()
    {
        string windows = Environment.GetFolderPath(
            Environment.SpecialFolder.Windows);
        string temporary = Path.GetFullPath(Path.GetTempPath());

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = windows,
            ["WINDIR"] = windows,
            ["TEMP"] = temporary,
            ["TMP"] = temporary
        };
    }
}
