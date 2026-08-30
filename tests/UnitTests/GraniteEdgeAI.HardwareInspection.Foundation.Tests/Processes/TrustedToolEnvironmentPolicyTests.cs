using System.Collections.ObjectModel;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Globalization;
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
    public void CreateRejectsUnicodeFormatCharactersWithoutEchoingThem()
    {
        Dictionary<string, string?> parent = ValidParent();
        parent["TEMP"] = parent["TEMP"] + "\u202eprivate";

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => TrustedToolEnvironmentPolicy.Create(parent));

        Assert.IsFalse(failure.Message.Contains("private", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ArbitraryEnvironmentCreationIsNotPublicProductionSurface()
    {
        Assert.IsNull(typeof(TrustedToolOperationEnvironment).GetMethod(
            "Create",
            System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static));
    }

    [TestMethod]
    [DataRow(@"\\server\private-share")]
    [DataRow(@"\\?\C:\private-device")]
    [DataRow(@"\??\C:\private-device")]
    public void OperationEnvironmentRejectsNonLocalOrDeviceLocalAppData(
        string hostileRoot)
    {
        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => TrustedToolOperationEnvironment.Create(ValidParent(), hostileRoot));

        Assert.IsFalse(failure.Message.Contains("private", StringComparison.Ordinal));
    }

    [TestMethod]
    public void OperationEnvironmentRejectsRedirectedLocalAppDataAncestor()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "geai-s1-root-test-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(root, "target");
        string link = Path.Combine(root, "redirected");
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(link, target);
        try
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                TrustedToolOperationEnvironment.Create(ValidParent(), link));
        }
        finally
        {
            Directory.Delete(link);
            Directory.Delete(root, recursive: true);
        }
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

    [TestMethod]
    public void OperationEnvironmentReportsBoundedCleanupFailureWithoutFollowingEntries()
    {
        TrustedToolOperationEnvironment operation =
            TrustedToolOperationEnvironment.Create(ValidParent());
        string operationDirectory = operation.Variables["TEMP"];
        try
        {
            for (int index = 0; index < 513; index++)
            {
                File.WriteAllText(
                    Path.Combine(
                        operationDirectory,
                        index.ToString("D4", CultureInfo.InvariantCulture) + ".tmp"),
                    "bounded");
            }

            operation.Dispose();

            Assert.IsFalse(operation.CleanupSucceeded);
            Assert.IsTrue(Directory.Exists(operationDirectory));
        }
        finally
        {
            operation.Dispose();
            if (Directory.Exists(operationDirectory))
            {
                Directory.Delete(operationDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void OperationEnvironmentBoundsCleanupByLogicalBytes()
    {
        TrustedToolOperationEnvironment operation =
            TrustedToolOperationEnvironment.Create(ValidParent());
        string operationDirectory = operation.Variables["TEMP"];
        string oversized = Path.Combine(operationDirectory, "oversized.tmp");
        try
        {
            using (FileStream stream = File.Create(oversized))
            {
                stream.SetLength((64L * 1024 * 1024) + 1);
            }

            operation.Dispose();

            Assert.IsFalse(operation.CleanupSucceeded);
            Assert.IsTrue(File.Exists(oversized));
        }
        finally
        {
            operation.Dispose();
            if (Directory.Exists(operationDirectory))
            {
                Directory.Delete(operationDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void OperationEnvironmentRetainsDirectoryCustodyUntilDisposed()
    {
        using TrustedToolOperationEnvironment operation =
            TrustedToolOperationEnvironment.Create(ValidParent());
        string operationDirectory = operation.Variables["TEMP"];
        string replacementPath = operationDirectory + "-replacement";
        try
        {
            IOException failure = Assert.ThrowsExactly<IOException>(
                () => Directory.Move(operationDirectory, replacementPath));

            Assert.IsFalse(string.IsNullOrWhiteSpace(failure.Message));
            Assert.IsTrue(Directory.Exists(operationDirectory));
            Assert.IsFalse(Directory.Exists(replacementPath));
        }
        finally
        {
            if (Directory.Exists(replacementPath))
            {
                Directory.Delete(replacementPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public void OperationEnvironmentDeletesNestedFilesUnderStableDirectoryCustody()
    {
        TrustedToolOperationEnvironment operation =
            TrustedToolOperationEnvironment.Create(ValidParent());
        string operationDirectory = operation.Variables["TEMP"];
        string childDirectory = Path.Combine(operationDirectory, "child");
        Directory.CreateDirectory(childDirectory);
        File.WriteAllText(Path.Combine(childDirectory, "sentinel.txt"), "retain");
        try
        {
            operation.Dispose();

            Assert.IsTrue(operation.CleanupSucceeded);
            Assert.IsFalse(Directory.Exists(operationDirectory));
        }
        finally
        {
            operation.Dispose();
            if (Directory.Exists(operationDirectory))
            {
                Directory.Delete(operationDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void OperationEnvironmentRejectsChildDirectoryReparseWithoutFollowingTarget()
    {
        TrustedToolOperationEnvironment operation =
            TrustedToolOperationEnvironment.Create(ValidParent());
        string operationDirectory = operation.Variables["TEMP"];
        string externalDirectory = Path.Combine(
            Path.GetTempPath(),
            "geai-s1-cleanup-target-" + Guid.NewGuid().ToString("N"));
        string sentinel = Path.Combine(externalDirectory, "sentinel.txt");
        string link = Path.Combine(operationDirectory, "child-link");
        Directory.CreateDirectory(externalDirectory);
        File.WriteAllText(sentinel, "retain");
        Directory.CreateSymbolicLink(link, externalDirectory);
        try
        {
            operation.Dispose();

            Assert.IsFalse(operation.CleanupSucceeded);
            Assert.IsTrue(File.Exists(sentinel));
        }
        finally
        {
            operation.Dispose();
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            if (Directory.Exists(operationDirectory))
            {
                Directory.Delete(operationDirectory, recursive: true);
            }

            Directory.Delete(externalDirectory, recursive: true);
        }
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
