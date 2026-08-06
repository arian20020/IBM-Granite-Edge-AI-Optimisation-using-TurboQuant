using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Proves an expected trust-policy failure never exposes a raw installation
/// path through either its public failure or its full exception chain.
/// </summary>
[TestClass]
public sealed class WorkerPolicyDiagnosticPrivacyTests
{
    private const string SensitivePath =
        @"C:\SensitiveWorkerRoot\GraniteEdgeAI.ModelInspection.Worker.exe";

    [TestMethod]
    public void ResolveDoesNotPreservePathBearingFilesystemException()
    {
        WorkerExecutableResolver resolver = new(
            "GraniteEdgeAI.ModelInspection.Worker.exe",
            new PathBearingFailureFileSystem());

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => resolver.Resolve(@"C:\Approved"));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
        Assert.IsFalse(
            error.ToString().Contains(
                SensitivePath,
                StringComparison.OrdinalIgnoreCase));
    }

    private sealed class PathBearingFailureFileSystem :
        IWorkerExecutableFileSystem
    {
        public bool DirectoryExists(string path) => true;

        public bool FileExists(string path) => true;

        public FileAttributes GetAttributes(string path) =>
            path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? FileAttributes.Normal
                : FileAttributes.Directory;

        public ushort ReadPortableExecutableMachine(string path) =>
            throw new IOException($"Access failed for {SensitivePath}.");

        public SafeFileHandle OpenReadHandle(string path) =>
            throw new InvalidOperationException(
                "The test should fail before opening a handle.");

        public string GetFinalDirectoryPath(string path) =>
            throw new InvalidOperationException(
                "The test should fail before final-path resolution.");

        public string GetFinalFilePath(SafeFileHandle handle) =>
            throw new InvalidOperationException(
                "The test should fail before final-path resolution.");
    }
}
