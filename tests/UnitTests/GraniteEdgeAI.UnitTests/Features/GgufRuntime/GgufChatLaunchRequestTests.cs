using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
public sealed class GgufChatLaunchRequestTests
{
    [TestMethod]
    public void LaunchRequestSnapshotsTrustedManifestAndRequiresLocalInputs()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string model = Path.Combine(root, "granite.gguf");
        File.WriteAllBytes(model, [1]);
        byte[] manifest = Manifest();
        byte expectedFirstByte = manifest[0];
        try
        {
            var request = new GgufChatLaunchRequest(
                root,
                manifest,
                model,
                "Granite 3B",
                Configuration());

            manifest[0] = 9;

            Assert.AreEqual(expectedFirstByte, request.TrustedManifest.Span[0]);
            Assert.AreEqual(Path.GetFullPath(root), request.PackageRoot);
            Assert.AreEqual(Path.GetFullPath(model), request.ModelFile);
            Assert.AreEqual("Granite 3B", request.DisplayName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void LaunchRequestRejectsConfigurationForAnotherRuntimeBuild()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string model = Path.Combine(root, "granite.gguf");
        File.WriteAllBytes(model, [1]);
        try
        {
            GgufRuntimeConfiguration mismatch = Configuration("other-build");

            Assert.ThrowsExactly<ArgumentException>(() =>
                new GgufChatLaunchRequest(
                    root,
                    Manifest(),
                    model,
                    "Granite 3B",
                    mismatch));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void LaunchRequestAcceptsVulkanOnlyWhenTheTrustedPackageDeclaresItsRole()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string model = Path.Combine(root, "granite.gguf");
        File.WriteAllBytes(model, [1]);
        try
        {
            var request = new GgufChatLaunchRequest(
                root,
                ManifestWithVulkanRole(),
                model,
                "Granite 3B",
                Configuration(
                    backend: GgufRuntimeBackend.Vulkan,
                    deviceId: "GPU.0",
                    gpuLayerCount: 32));

            Assert.AreEqual(GgufRuntimeBackend.Vulkan, request.Configuration.Backend);
            Assert.ThrowsExactly<ArgumentException>(() => new GgufChatLaunchRequest(
                root,
                Manifest(),
                model,
                "Granite 3B",
                Configuration(
                    backend: GgufRuntimeBackend.Vulkan,
                    deviceId: "GPU.0",
                    gpuLayerCount: 32)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ModelVerificationRejectsAFileChangedSinceInspection()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string model = Path.Combine(root, "granite.gguf");
        File.WriteAllBytes(model, [1]);
        string inspectedHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData([1])).ToLowerInvariant();
        try
        {
            var request = new GgufChatLaunchRequest(
                root,
                Manifest(),
                model,
                "Granite 3B",
                Configuration(modelSha256: inspectedHash));
            File.WriteAllBytes(model, [2]);

            GgufChatLaunchException error =
                await Assert.ThrowsExactlyAsync<GgufChatLaunchException>(() =>
                    request.VerifyModelAsync(CancellationToken.None));

            Assert.AreEqual("model-changed-since-inspection", error.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ModelVerificationAccessFailureBecomesTypedLaunchUnavailability()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string model = Path.Combine(root, "private-model.gguf");
        File.WriteAllBytes(model, [1]);
        try
        {
            var request = new GgufChatLaunchRequest(
                root,
                Manifest(),
                model,
                "Granite 3B",
                Configuration());
            File.Delete(model);

            GgufChatLaunchException error =
                await Assert.ThrowsExactlyAsync<GgufChatLaunchException>(() =>
                    request.VerifyModelAsync(CancellationToken.None));

            Assert.AreEqual("model-file-unavailable", error.Code);
            Assert.IsFalse(error.ToString().Contains(root, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void MissingModelFailureDoesNotExposeTheAbsolutePath()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string missingModel = Path.Combine(root, "private-model.gguf");
        try
        {
            Exception error = Assert.ThrowsExactly<GgufChatLaunchException>(() =>
                new GgufChatLaunchRequest(
                    root,
                    Manifest(),
                    missingModel,
                    "Granite 3B",
                    Configuration()));

            Assert.IsFalse(
                error.ToString().Contains(root, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] Manifest() => System.Text.Encoding.UTF8.GetBytes(
        $$"""
        {"schemaVersion":1,"runtimeBuildId":"llama-build","runtimeSourceCommit":"{{new string('b', 40)}}","buildFlags":[],"files":[]}
        """);

    private static byte[] ManifestWithVulkanRole() => System.Text.Encoding.UTF8.GetBytes(
        $$"""
        {"schemaVersion":2,"runtimeBuildId":"llama-build","runtimeSourceCommit":"{{new string('b', 40)}}","buildFlags":[],"files":[{"relativePath":"Native/Vulkan/llama-server.exe","length":1,"sha256":"{{new string('a', 64)}}","architecture":"X64","role":"NativeRuntimeVulkan","licenseReference":"LICENSE"}]}
        """);

    private static GgufRuntimeConfiguration Configuration(
        string runtimeBuildId = "llama-build",
        GgufRuntimeBackend backend = GgufRuntimeBackend.Cpu,
        string? modelSha256 = null,
        string deviceId = "cpu",
        int gpuLayerCount = 0) => new(
        "granite-test",
        modelSha256 ?? new string('a', 64),
        runtimeBuildId,
        new string('b', 40),
        backend,
        deviceId,
        4096,
        GgufCacheType.F16,
        GgufCacheType.F16,
        gpuLayerCount,
        false,
        4,
        256,
        "inspected",
        "cpu-default");
}
