using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;
using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Tests;

[TestClass]
public sealed class GgufRuntimeClientFactoryTests
{
    [TestMethod]
    public async Task StartupCleanupFailurePreservesPrimaryAndReportsOnlySafeFact()
    {
        var primary = new InvalidOperationException("private-primary-detail");
        var reporter = new RecordingCleanupReporter();

        InvalidOperationException actual =
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                GgufRuntimeStartupCleanup.RethrowPrimaryAfterCleanupAsync(
                    primary,
                    () => ValueTask.FromException(
                        new IOException("private-cleanup-path")),
                    reporter));

        Assert.AreSame(primary, actual);
        Assert.AreEqual(
            GgufRuntimeCleanupFaultClassification.Io,
            reporter.Fault?.Classification);
        Assert.IsFalse(reporter.Fault.ToString()!.Contains("private", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task StartupCleanupFailurePreservesExactCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var primary = new OperationCanceledException(source.Token);

        OperationCanceledException actual =
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                GgufRuntimeStartupCleanup.RethrowPrimaryAfterCleanupAsync(
                    primary,
                    () => ValueTask.FromException(
                        new InvalidOperationException("cleanup")),
                    new RecordingCleanupReporter()));

        Assert.AreSame(primary, actual);
        Assert.AreEqual(source.Token, actual.CancellationToken);
    }

    [TestMethod]
    public async Task SessionTeardownRunsEveryPhaseAndPreservesCloseFailure()
    {
        var primary = new IOException("private-close-detail");
        int channelDisposals = 0;
        int processDisposals = 0;

        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(() =>
            GgufRuntimeSession.DisposePreservingFirstFailureAsync(
                () => Task.FromException(primary),
                () => channelDisposals++,
                () =>
                {
                    processDisposals++;
                    return ValueTask.CompletedTask;
                }).AsTask());

        Assert.AreSame(primary, actual);
        Assert.AreEqual(1, channelDisposals);
        Assert.AreEqual(1, processDisposals);
    }
    [TestMethod]
    public void CreateAcceptsVerifiedPackageAndExplicitAbsoluteModel()
    {
        string executable = Environment.ProcessPath!;
        string model = Path.GetTempFileName();
        try
        {
            var package = new VerifiedGgufRuntimePackage(
                executable,
                executable,
                "build",
                "0123456789abcdef0123456789abcdef01234567",
                ["GGML_NATIVE=OFF"]);

            GgufRuntimeClient client = GgufRuntimeClient.Create(package, model);

            Assert.IsNotNull(client);
        }
        finally
        {
            File.Delete(model);
        }
    }

    [TestMethod]
    public void CreateBootstrapCarriesOnlyVerifiedNativeRuntimeRoles()
    {
        string cpu = Path.GetTempFileName();
        string vulkan = Path.GetTempFileName();
        string model = Path.GetTempFileName();
        try
        {
            string executable = Environment.ProcessPath!;
            var package = new VerifiedGgufRuntimePackage(
                executable,
                executable,
                "atomicbot-519f0c5-cpu-vulkan",
                "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                ["GGML_NATIVE=OFF"],
                cpu,
                vulkan);

            GgufWorkerBootstrap bootstrap =
                GgufRuntimeClient.CreateBootstrap(package, model);

            Assert.AreEqual(Path.GetFullPath(cpu), bootstrap.CpuRuntimeExecutable);
            Assert.AreEqual(Path.GetFullPath(vulkan), bootstrap.VulkanRuntimeExecutable);
        }
        finally
        {
            File.Delete(cpu);
            File.Delete(vulkan);
            File.Delete(model);
        }
    }

    [TestMethod]
    public void CreateRejectsRelativeOrMissingLaunchInputs()
    {
        string executable = Environment.ProcessPath!;
        var package = new VerifiedGgufRuntimePackage(
            executable,
            executable,
            "build",
            "0123456789abcdef0123456789abcdef01234567",
            ["GGML_NATIVE=OFF"]);

        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufRuntimeClient.Create(package, "relative.gguf"));
        Assert.ThrowsExactly<FileNotFoundException>(() =>
            GgufRuntimeClient.Create(
                package with { SupervisorExecutable = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe") },
                executable));
    }

    [TestMethod]
    public void CreateFromPackageRejectsDetachedManifestMismatchBeforeLaunch()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "granite-client-package-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "runtime-manifest.json"), "{ }");

            GgufRuntimeTrustException exception =
                Assert.ThrowsExactly<GgufRuntimeTrustException>(() =>
                    GgufRuntimeClient.CreateFromPackage(
                        root,
                        "{\"schemaVersion\":1}"u8,
                        Environment.ProcessPath!));

            Assert.AreEqual("runtime-manifest-copy-mismatch", exception.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void CreateRejectsReparsePointLaunchInputs()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "granite-client-reparse-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string target = Path.Combine(root, "model-target.gguf");
            string link = Path.Combine(root, "model-link.gguf");
            File.WriteAllBytes(target, [1, 2, 3, 4]);
            try
            {
                File.CreateSymbolicLink(link, target);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Assert.Inconclusive(
                    $"File symbolic links are unavailable: {exception.GetType().Name}.");
            }

            string executable = Environment.ProcessPath!;
            var package = new VerifiedGgufRuntimePackage(
                executable,
                executable,
                "build",
                "0123456789abcdef0123456789abcdef01234567",
                ["GGML_NATIVE=OFF"]);

            _ = Assert.ThrowsExactly<ArgumentException>(() =>
                GgufRuntimeClient.Create(package, link));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RecordingCleanupReporter : IGgufRuntimeCleanupFaultReporter
    {
        internal GgufRuntimeCleanupFault? Fault { get; private set; }

        public void Report(GgufRuntimeCleanupFault fault) => Fault = fault;
    }
}
