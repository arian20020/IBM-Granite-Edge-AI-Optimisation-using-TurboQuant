using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Tests;

[TestClass]
public sealed class GgufRuntimeClientFactoryTests
{
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
}
