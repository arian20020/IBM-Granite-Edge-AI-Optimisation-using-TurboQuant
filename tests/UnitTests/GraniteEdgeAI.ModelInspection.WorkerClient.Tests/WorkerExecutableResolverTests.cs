using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Defines the trusted executable-resolution policy before any process is
/// created. The tests combine deterministic filesystem doubles with real
/// minimal PE files so policy and parsing failures remain reproducible.
/// </summary>
[TestClass]
public sealed class WorkerExecutableResolverTests
{
    private const string WorkerFileName =
        "GraniteEdgeAI.ModelInspection.Worker.exe";
    private const ushort Amd64Machine = 0x8664;
    private const ushort I386Machine = 0x014c;

    [TestMethod]
    public void ResolveRejectsRelativeApprovedRoot()
    {
        WorkerExecutableResolver resolver = new(WorkerFileName);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => resolver.Resolve("relative"));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
        Assert.IsFalse(error.Failure.Message.Contains("relative", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ResolveRejectsCandidateOutsideApprovedRoot()
    {
        WorkerExecutableResolver resolver = new(
            Path.Combine("..", "Outside", WorkerFileName));

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => resolver.Resolve(@"C:\Approved"));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void ResolveRejectsMissingWorkerFile()
    {
        FakeWorkerExecutableFileSystem fileSystem =
            FakeWorkerExecutableFileSystem.CreateValid();
        fileSystem.FileExistsResult = false;
        WorkerExecutableResolver resolver = new(WorkerFileName, fileSystem);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => resolver.Resolve(fileSystem.ApprovedRoot));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void ResolveRejectsDirectoryInsteadOfWorkerFile()
    {
        FakeWorkerExecutableFileSystem fileSystem =
            FakeWorkerExecutableFileSystem.CreateValid();
        fileSystem.CandidateAttributes = FileAttributes.Directory;
        WorkerExecutableResolver resolver = new(WorkerFileName, fileSystem);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => resolver.Resolve(fileSystem.ApprovedRoot));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void ResolveRejectsReparsePointInApprovedPath()
    {
        FakeWorkerExecutableFileSystem fileSystem =
            FakeWorkerExecutableFileSystem.CreateValid();
        fileSystem.ReparsePaths.Add(fileSystem.CandidatePath);
        WorkerExecutableResolver resolver = new(WorkerFileName, fileSystem);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => resolver.Resolve(fileSystem.ApprovedRoot));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void ResolveRejectsFinalPathOutsideFinalApprovedRoot()
    {
        FakeWorkerExecutableFileSystem fileSystem =
            FakeWorkerExecutableFileSystem.CreateValid();
        fileSystem.FinalExecutablePath =
            @"C:\Approved-Elsewhere\GraniteEdgeAI.ModelInspection.Worker.exe";
        WorkerExecutableResolver resolver = new(WorkerFileName, fileSystem);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => resolver.Resolve(fileSystem.ApprovedRoot));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            error.Failure.Code);
    }

    [TestMethod]
    public void ResolveRejectsUnsupportedPortableExecutableMachine()
    {
        FakeWorkerExecutableFileSystem fileSystem =
            FakeWorkerExecutableFileSystem.CreateValid();
        fileSystem.PortableExecutableMachine = I386Machine;
        WorkerExecutableResolver resolver = new(WorkerFileName, fileSystem);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => resolver.Resolve(fileSystem.ApprovedRoot));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerArchitectureUnsupported,
            error.Failure.Code);
    }

    [TestMethod]
    public void ResolveReturnsMaintainedVerificationHandleForAmd64Worker()
    {
        FakeWorkerExecutableFileSystem fileSystem =
            FakeWorkerExecutableFileSystem.CreateValid();
        WorkerExecutableResolver resolver = new(WorkerFileName, fileSystem);

        VerifiedWorkerExecutable executable =
            resolver.Resolve(fileSystem.ApprovedRoot);

        Assert.AreEqual(
            fileSystem.FinalApprovedRoot,
            executable.ApprovedRootFinalPath);
        Assert.AreEqual(
            fileSystem.FinalExecutablePath,
            executable.ExecutableFinalPath);
        Assert.IsFalse(executable.VerificationHandle.IsClosed);

        executable.Dispose();

        Assert.IsTrue(executable.VerificationHandle.IsClosed);
    }

    [TestMethod]
    public void ResolveReadsRealAmd64PortableExecutableHeader()
    {
        using TemporaryWorkerRoot root =
            TemporaryWorkerRoot.Create(Amd64Machine, WorkerFileName);
        WorkerExecutableResolver resolver = new(WorkerFileName);

        using VerifiedWorkerExecutable executable = resolver.Resolve(root.Path);

        Assert.IsTrue(
            executable.ExecutableFinalPath.EndsWith(
                WorkerFileName,
                StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ResolveRejectsRealI386PortableExecutableHeader()
    {
        using TemporaryWorkerRoot root =
            TemporaryWorkerRoot.Create(I386Machine, WorkerFileName);
        WorkerExecutableResolver resolver = new(WorkerFileName);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => resolver.Resolve(root.Path));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerArchitectureUnsupported,
            error.Failure.Code);
        Assert.IsFalse(
            error.Failure.Message.Contains(root.Path, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeWorkerExecutableFileSystem :
        IWorkerExecutableFileSystem
    {
        private FakeWorkerExecutableFileSystem()
        {
        }

        public string ApprovedRoot { get; } = @"C:\Approved";

        public string CandidatePath => Path.Combine(ApprovedRoot, WorkerFileName);

        public string FinalApprovedRoot { get; set; } = @"C:\Approved";

        public string FinalExecutablePath { get; set; } =
            @"C:\Approved\GraniteEdgeAI.ModelInspection.Worker.exe";

        public bool DirectoryExistsResult { get; set; } = true;

        public bool FileExistsResult { get; set; } = true;

        public FileAttributes CandidateAttributes { get; set; } =
            FileAttributes.Normal;

        public ushort PortableExecutableMachine { get; set; } = Amd64Machine;

        public HashSet<string> ReparsePaths { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public static FakeWorkerExecutableFileSystem CreateValid() => new();

        public bool DirectoryExists(string path) => DirectoryExistsResult;

        public bool FileExists(string path) => FileExistsResult;

        public FileAttributes GetAttributes(string path)
        {
            if (ReparsePaths.Contains(path))
            {
                return FileAttributes.ReparsePoint;
            }

            return string.Equals(
                path,
                CandidatePath,
                StringComparison.OrdinalIgnoreCase)
                    ? CandidateAttributes
                    : FileAttributes.Directory;
        }

        public ushort ReadPortableExecutableMachine(string path) =>
            PortableExecutableMachine;

        public SafeFileHandle OpenReadHandle(string path) =>
            new(new IntPtr(123), ownsHandle: false);

        public string GetFinalDirectoryPath(string path) => FinalApprovedRoot;

        public string GetFinalFilePath(SafeFileHandle handle) =>
            FinalExecutablePath;
    }

    private sealed class TemporaryWorkerRoot : IDisposable
    {
        private TemporaryWorkerRoot(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryWorkerRoot Create(
            ushort machine,
            string workerFileName)
        {
            string root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"GraniteEdgeAI-WorkerPolicy-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            WriteMinimalPortableExecutable(
                System.IO.Path.Combine(root, workerFileName),
                machine);
            return new TemporaryWorkerRoot(root);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }

        private static void WriteMinimalPortableExecutable(
            string path,
            ushort machine)
        {
            byte[] image = new byte[512];
            image[0] = (byte)'M';
            image[1] = (byte)'Z';
            BitConverter.TryWriteBytes(image.AsSpan(0x3c, 4), 0x80);
            image[0x80] = (byte)'P';
            image[0x81] = (byte)'E';
            BitConverter.TryWriteBytes(image.AsSpan(0x84, 2), machine);
            File.WriteAllBytes(path, image);
        }
    }
}
