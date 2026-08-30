using System.Collections.ObjectModel;
using System.Security.AccessControl;
using System.Security.Principal;
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

    [TestMethod]
    public void CreateRejectsExistingDirectoryThatIsNotTheCanonicalWindowsRoot()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent["SystemRoot"] = Path.GetTempPath();
        parent["WINDIR"] = Path.GetTempPath();

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => TrustedToolEnvironmentPolicy.Create(parent));

        Assert.AreEqual(
            "The trusted hardware tool environment could not be secured.",
            failure.Message);
    }

    [TestMethod]
    public void CreateRejectsMismatchedSystemRootAndWindir()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent["WINDIR"] = Path.GetTempPath();

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => TrustedToolEnvironmentPolicy.Create(parent));

        Assert.AreEqual(
            "The trusted hardware tool environment could not be secured.",
            failure.Message);
    }

    [TestMethod]
    public void OperationEnvironmentUsesPrivateAclAndDeletesItsExactDirectory()
    {
        Dictionary<string, string?> parent = ValidParent();
        string operationDirectory;
        using (TrustedToolOperationEnvironment operation =
               TrustedToolOperationEnvironment.Create(parent))
        {
            operationDirectory = operation.Variables["TEMP"];
            Assert.AreEqual(operationDirectory, operation.Variables["TMP"]);
            Assert.IsTrue(Directory.Exists(operationDirectory));

            DirectorySecurity security =
                new DirectoryInfo(operationDirectory).GetAccessControl();
            Assert.IsTrue(security.AreAccessRulesProtected);
            SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User
                ?? throw new AssertFailedException("Current user SID was unavailable.");
            AuthorizationRuleCollection rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                typeof(SecurityIdentifier));
            Assert.IsTrue(rules.Cast<FileSystemAccessRule>().Any(rule =>
                rule.IdentityReference == currentUser
                && rule.AccessControlType == AccessControlType.Allow
                && (rule.FileSystemRights & FileSystemRights.FullControl) != 0));
        }

        Assert.IsFalse(Directory.Exists(operationDirectory));
    }

    private static Dictionary<string, string?> ValidParent()
    {
        string windowsDirectory =
            Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string tempDirectory = Path.GetTempPath();
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SystemRoot"] = windowsDirectory,
            ["WINDIR"] = windowsDirectory,
            ["TEMP"] = tempDirectory,
            ["TMP"] = tempDirectory,
        };
    }
}
