using System.ComponentModel;
using System.Runtime.InteropServices;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Proves that every launcher setup failure maps to one stable policy code,
/// disposes partial resources, and never retries through a relaxed path.
/// </summary>
[TestClass]
public sealed class WindowsWorkerProcessLauncherFailureTests
{
    [TestMethod]
    public void EnvironmentFailureStopsBeforeProcessCreation() =>
        AssertFailure(
            LaunchFailureStage.Environment,
            WorkerClientFailureCodes.WorkerEnvironmentPolicyFailed,
            expectedCreateProcessCalls: 0);

    [TestMethod]
    public void JobCreationFailureStopsBeforeProcessCreation() =>
        AssertFailure(
            LaunchFailureStage.JobCreation,
            WorkerClientFailureCodes.WorkerContainmentFailed,
            expectedCreateProcessCalls: 0);

    [TestMethod]
    public void HandleListFailureStopsBeforeProcessCreation() =>
        AssertFailure(
            LaunchFailureStage.HandleList,
            WorkerClientFailureCodes.WorkerHandlePolicyFailed,
            expectedCreateProcessCalls: 0);

    [TestMethod]
    public void JobListFailureStopsBeforeProcessCreation() =>
        AssertFailure(
            LaunchFailureStage.JobList,
            WorkerClientFailureCodes.WorkerContainmentFailed,
            expectedCreateProcessCalls: 0);

    [TestMethod]
    public void CreateProcessFailureHasNoFallbackAttempt() =>
        AssertFailure(
            LaunchFailureStage.CreateProcess,
            WorkerClientFailureCodes.WorkerLaunchFailed,
            expectedCreateProcessCalls: 1);

    private static void AssertFailure(
        LaunchFailureStage stage,
        string expectedCode,
        int expectedCreateProcessCalls)
    {
        using VerifiedExecutableFixture fixture =
            VerifiedExecutableFixture.Create();
        using VerifiedWorkerExecutable executable =
            new WorkerExecutableResolver("worker.exe")
                .Resolve(fixture.RootDirectory);
        WindowsProcessLaunchRequest request = new(
            executable,
            CreateEnvironment(),
            []);
        FailingWindowsWorkerProcessPlatform platform = new(stage);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                () => WindowsWorkerProcessLauncher.Launch(request, platform));

        Assert.AreEqual(expectedCode, error.Failure.Code);
        Assert.AreEqual(
            expectedCreateProcessCalls,
            platform.CreateProcessCalls);
        platform.AssertAllCreatedResourcesWereDisposed();
    }

    private static IReadOnlyDictionary<string, string> CreateEnvironment()
    {
        string windowsDirectory =
            Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        Dictionary<string, string?> parent =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["SystemRoot"] = windowsDirectory,
                ["WINDIR"] = windowsDirectory,
                ["TEMP"] = Path.GetTempPath(),
                ["TMP"] = Path.GetTempPath()
            };
        return WorkerEnvironmentPolicy.Create(parent);
    }

    private enum LaunchFailureStage
    {
        Environment,
        JobCreation,
        HandleList,
        JobList,
        CreateProcess
    }

    private sealed class FailingWindowsWorkerProcessPlatform :
        IWindowsWorkerProcessPlatform
    {
        private readonly LaunchFailureStage _stage;
        private readonly List<SafeHandle> _createdHandles = [];
        private WindowsEnvironmentBlock? _environmentBlock;

        internal FailingWindowsWorkerProcessPlatform(
            LaunchFailureStage stage)
        {
            _stage = stage;
        }

        internal int CreateProcessCalls { get; private set; }

        public WindowsEnvironmentBlock CreateEnvironmentBlock(
            IReadOnlyDictionary<string, string> environment)
        {
            if (_stage == LaunchFailureStage.Environment)
            {
                throw new IOException("Injected environment failure.");
            }

            _environmentBlock = WindowsEnvironmentBlock.Create(environment);
            return _environmentBlock;
        }

        public WindowsJobObject CreateJob()
        {
            if (_stage == LaunchFailureStage.JobCreation)
            {
                throw new Win32Exception(5, "Injected Job Object failure.");
            }

            WindowsJobObject job = WindowsJobObject.CreateKillOnClose();
            _createdHandles.Add(job.Handle);
            return job;
        }

        public WindowsPipeSet CreatePipes()
        {
            WindowsPipeSet pipes = WindowsPipeSet.Create();
            _createdHandles.Add(pipes.ChildStandardInputRead);
            _createdHandles.Add(pipes.ParentStandardInputWrite);
            _createdHandles.Add(pipes.ParentStandardOutputRead);
            _createdHandles.Add(pipes.ChildStandardOutputWrite);
            _createdHandles.Add(pipes.ParentStandardErrorRead);
            _createdHandles.Add(pipes.ChildStandardErrorWrite);
            return pipes;
        }

        public SafeAttributeListBuffer CreateAttributeList()
        {
            SafeAttributeListBuffer attributes =
                SafeAttributeListBuffer.Create(attributeCount: 2);
            _createdHandles.Add(attributes);
            return attributes;
        }

        public void ApplyHandleAllowlist(
            SafeAttributeListBuffer attributes,
            WindowsPipeSet pipes)
        {
            if (_stage == LaunchFailureStage.HandleList)
            {
                throw new Win32Exception(5, "Injected handle-list failure.");
            }

            attributes.UpdatePointerList(
                NativeConstants.ProcThreadAttributeHandleList,
                pipes.GetChildHandleAllowlist());
        }

        public void ApplyJobContainment(
            SafeAttributeListBuffer attributes,
            WindowsJobObject job)
        {
            if (_stage == LaunchFailureStage.JobList)
            {
                throw new Win32Exception(5, "Injected job-list failure.");
            }

            attributes.UpdatePointerList(
                NativeConstants.ProcThreadAttributeJobList,
                [job.Handle.DangerousGetHandle()]);
        }

        public ProcessInformation CreateProcess(
            string applicationName,
            char[] commandLine,
            IntPtr environment,
            string workingDirectory,
            ref StartupInfoEx startupInfo)
        {
            CreateProcessCalls++;
            throw new Win32Exception(5, "Injected CreateProcessW failure.");
        }

        internal void AssertAllCreatedResourcesWereDisposed()
        {
            if (_environmentBlock is not null)
            {
                Assert.AreEqual(IntPtr.Zero, _environmentBlock.Pointer);
            }

            foreach (SafeHandle handle in _createdHandles)
            {
                Assert.IsTrue(
                    handle.IsClosed,
                    $"Handle {handle.GetType().Name} remained open.");
            }
        }
    }

    private sealed class VerifiedExecutableFixture : IDisposable
    {
        private VerifiedExecutableFixture(string rootDirectory)
        {
            RootDirectory = rootDirectory;
        }

        internal string RootDirectory { get; }

        internal static VerifiedExecutableFixture Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "GraniteEdgeAI-LauncherFailure",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.Copy(
                typeof(WindowsWorkerProcessLauncherFailureTests)
                    .Assembly.Location,
                Path.Combine(root, "worker.exe"));
            return new VerifiedExecutableFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
    }
}
