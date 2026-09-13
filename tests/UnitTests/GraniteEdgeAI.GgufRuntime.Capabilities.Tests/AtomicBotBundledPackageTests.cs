using System.IO.Compression;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Tests;

[TestClass]
public sealed class AtomicBotBundledPackageTests
{
    private const string ExactCommit =
        "519f0c594a8e31467d2e2f2cf17054c9e7e11536";
    private const string ExactPackageIdentity =
        "atomicbot-turboquant-519f0c594a8e31467d2e2f2cf17054c9e7e11536-win-x64";
    private static readonly string[] ExpectedRoles = ["cpu", "vulkan"];
    private static readonly string[] RequiredTools =
    [
        "llama-server.exe", "llama-server-impl.dll", "llama-quantize.exe",
        "llama-quantize-impl.dll", "llama.dll", "llama-common.dll",
        "ggml.dll", "ggml-base.dll", "mtmd.dll", "LICENSE"
    ];

    [TestMethod]
    public void BundledArchivesMatchThePinnedReleaseAndContainRequiredTools()
    {
        string root = FindRepositoryRoot();
        string packageRoot = Path.Combine(root, "runtime", "gguf", "atomicbot");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(packageRoot, "package-manifest.json")));
        JsonElement manifest = document.RootElement;

        Assert.AreEqual(ExactCommit, manifest.GetProperty("sourceCommit").GetString());
        Assert.AreEqual(
            ExactPackageIdentity,
            manifest.GetProperty("packageId").GetString());
        Assert.AreEqual("MIT", manifest.GetProperty("license").GetString());

        JsonElement[] archives = [.. manifest.GetProperty("archives").EnumerateArray()];
        CollectionAssert.AreEquivalent(
            ExpectedRoles,
            archives.Select(entry => entry.GetProperty("role").GetString()).ToArray());

        foreach (JsonElement entry in archives)
        {
            string relative = entry.GetProperty("file").GetString()!;
            string archivePath = Path.GetFullPath(Path.Combine(packageRoot, relative));
            Assert.IsTrue(archivePath.StartsWith(
                Path.GetFullPath(packageRoot) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(File.Exists(archivePath), $"Missing bundled archive {relative}.");
            Assert.AreEqual(entry.GetProperty("length").GetInt64(), new FileInfo(archivePath).Length);

            using FileStream stream = File.OpenRead(archivePath);
            string digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            Assert.AreEqual(entry.GetProperty("sha256").GetString(), digest);

            using ZipArchive zip = ZipFile.OpenRead(archivePath);
            string[] names = [.. zip.Entries.Select(item => item.Name)];
            CollectionAssert.IsSubsetOf(
                RequiredTools,
                names);
            if (entry.GetProperty("role").GetString() == "vulkan")
            {
                CollectionAssert.Contains(names, "ggml-vulkan.dll");
            }
        }
    }

    [TestMethod]
    public async Task StagingScriptVerifiesAndExpandsBothNativeBackendsOffline()
    {
        string root = FindRepositoryRoot();
        string packageRoot = Path.Combine(root, "runtime", "gguf", "atomicbot");
        string stage = Path.Combine(Path.GetTempPath(), $"atomicbot-stage-{Guid.NewGuid():N}");
        try
        {
            ProcessResult result = await RunPowerShellAsync(
                Path.Combine(root, "scripts", "gguf-runtime", "Stage-AtomicBotRuntime.ps1"),
                "-BundledPackageRoot", packageRoot,
                "-StageRoot", stage);

            Assert.AreEqual(0, result.ExitCode, result.Output);
            StringAssert.Contains(result.Output, "atomicbot_runtime_staged");
            Assert.IsTrue(File.Exists(Path.Combine(stage, "Cpu", "llama-server.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(stage, "Vulkan", "llama-server.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(stage, "Cpu", "mtmd.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(stage, "Vulkan", "mtmd.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(stage, "Vulkan", "ggml-vulkan.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(stage, "Cpu", "llama-quantize.exe")));

            foreach (string role in ExpectedRoles)
            {
                string roleDirectory = role == "cpu" ? "Cpu" : "Vulkan";
                ProcessResult help = await RunExecutableAsync(
                    Path.Combine(stage, roleDirectory, "llama-server.exe"),
                    "--help");
                Assert.AreEqual(0, help.ExitCode, help.Output);
                StringAssert.Contains(help.Output, "turbo3");
                StringAssert.Contains(help.Output, "turbo4");
            }
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task StagingScriptRejectsAnArchiveThatNoLongerMatchesItsPinnedDigest()
    {
        string root = FindRepositoryRoot();
        string sourceRoot = Path.Combine(root, "runtime", "gguf", "atomicbot");
        string operationRoot = Path.Combine(
            Path.GetTempPath(), $"atomicbot-tamper-{Guid.NewGuid():N}");
        string copiedPackage = Path.Combine(operationRoot, "package");
        string stage = Path.Combine(operationRoot, "stage");
        try
        {
            Directory.CreateDirectory(Path.Combine(copiedPackage, "archives"));
            File.Copy(
                Path.Combine(sourceRoot, "package-manifest.json"),
                Path.Combine(copiedPackage, "package-manifest.json"));
            foreach (string archive in Directory.GetFiles(
                         Path.Combine(sourceRoot, "archives"), "*.zip"))
            {
                File.Copy(archive, Path.Combine(copiedPackage, "archives", Path.GetFileName(archive)));
            }
            await File.AppendAllTextAsync(
                Path.Combine(copiedPackage, "archives", "llama-turboquant-windows-x64-cpu.zip"),
                "tampered");

            ProcessResult result = await RunPowerShellAsync(
                Path.Combine(root, "scripts", "gguf-runtime", "Stage-AtomicBotRuntime.ps1"),
                "-BundledPackageRoot", copiedPackage,
                "-StageRoot", stage);

            Assert.AreNotEqual(0, result.ExitCode);
            StringAssert.Contains(result.Output, "atomicbot_archive_integrity_failed");
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void StagingScriptPrevalidatesZipTraversalAndDeclaresBothCompleteClosures()
    {
        string root = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(
            root, "scripts", "gguf-runtime", "Stage-AtomicBotRuntime.ps1"));

        StringAssert.Contains(script, "Assert-ZipEntriesSafe");
        StringAssert.Contains(script, "ggml-vulkan.dll");
        StringAssert.Contains(script, "ggml-cpu-alderlake.dll");
        StringAssert.Contains(script, "ggml-cpu-x64.dll");
        StringAssert.Contains(script, "archive_member_path_invalid");
    }

    [TestMethod]
    public async Task StagingScriptRejectsUnsafeZipMembersBeforeExtraction()
    {
        foreach (string members in new[]
                 {
                     "../outside.dll",
                     "C:/outside.dll",
                     "build/bin/x.dll|build//bin/x.dll",
                 })
        {
            await AssertUnsafeZipMembersAsync(members);
        }
    }

    private static async Task AssertUnsafeZipMembersAsync(string members)
    {
        string root = FindRepositoryRoot();
        string operationRoot = Path.Combine(Path.GetTempPath(),
            $"atomicbot-unsafe-zip-{Guid.NewGuid():N}");
        string packageRoot = Path.Combine(operationRoot, "package");
        string archiveDirectory = Path.Combine(packageRoot, "archives");
        string archive = Path.Combine(archiveDirectory, "unsafe.zip");
        try
        {
            Directory.CreateDirectory(archiveDirectory);
            using (ZipArchive zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                foreach (string member in members.Split('|'))
                {
                    using Stream stream = zip.CreateEntry(member).Open();
                    await stream.WriteAsync(new byte[] { 0 });
                }
            }

            FileInfo info = new(archive);
            string hash;
            await using (FileStream stream = File.OpenRead(archive))
            {
                hash = Convert.ToHexString(await SHA256.HashDataAsync(stream))
                    .ToLowerInvariant();
            }
            await File.WriteAllTextAsync(
                Path.Combine(packageRoot, "package-manifest.json"),
                $$"""{"schemaVersion":1,"packageId":"atomicbot-turboquant-519f0c594a8e31467d2e2f2cf17054c9e7e11536-win-x64","sourceCommit":"{{ExactCommit}}","archives":[{"role":"cpu","file":"archives/unsafe.zip","length":{{info.Length}},"sha256":"{{hash}}"},{"role":"vulkan","file":"archives/unsafe.zip","length":{{info.Length}},"sha256":"{{hash}}"}]}""");

            ProcessResult result = await RunPowerShellAsync(
                Path.Combine(root, "scripts", "gguf-runtime", "Stage-AtomicBotRuntime.ps1"),
                "-BundledPackageRoot", packageRoot,
                "-StageRoot", Path.Combine(operationRoot, "stage"));

            Assert.AreNotEqual(0, result.ExitCode);
            StringAssert.Contains(result.Output, "atomicbot_archive_member_path_invalid");
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task GeneratedApplicationManifestPinsEveryNativeExecutionRole()
    {
        string root = FindRepositoryRoot();
        string packageRoot = Path.Combine(root, "runtime", "gguf", "atomicbot");
        string operationRoot = Path.Combine(
            Path.GetTempPath(), $"atomicbot-manifest-{Guid.NewGuid():N}");
        string stage = Path.Combine(operationRoot, "stage");
        string manifest = Path.Combine(operationRoot, "runtime-manifest.json");
        try
        {
            ProcessResult staged = await RunPowerShellAsync(
                Path.Combine(root, "scripts", "gguf-runtime", "Stage-AtomicBotRuntime.ps1"),
                "-BundledPackageRoot", packageRoot,
                "-StageRoot", Path.Combine(stage, "Native"));
            Assert.AreEqual(0, staged.ExitCode, staged.Output);
            Directory.CreateDirectory(Path.Combine(stage, "Worker"));
            Directory.CreateDirectory(Path.Combine(stage, "Adapter"));
            await File.WriteAllTextAsync(
                Path.Combine(stage, "Worker", "GraniteEdgeAI.GgufRuntime.Worker.exe"),
                "worker");
            await File.WriteAllTextAsync(
                Path.Combine(stage, "Adapter", "GraniteEdgeAI.GgufRuntime.NativeAdapter.exe"),
                "adapter");

            ProcessResult generated = await RunPowerShellAsync(
                Path.Combine(root, "scripts", "gguf-runtime", "New-GgufRuntimeManifest.ps1"),
                "-PackageRoot", stage,
                "-ManifestPath", manifest,
                "-RuntimeSourceCommit", ExactCommit,
                "-RuntimeBuildId", "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan",
                "-BuildFlags", "Backend=CPU+Vulkan,Runtime=win-x64");
            Assert.AreEqual(0, generated.ExitCode, generated.Output);

            using JsonDocument document = JsonDocument.Parse(await File.ReadAllBytesAsync(manifest));
            Assert.AreEqual(2, document.RootElement.GetProperty("schemaVersion").GetInt32());
            string[] roles =
            [
                .. document.RootElement.GetProperty("files").EnumerateArray()
                    .Select(file => file.GetProperty("role").GetString()!)
            ];
            CollectionAssert.Contains(roles, "NativeRuntimeCpu");
            CollectionAssert.Contains(roles, "NativeRuntimeVulkan");
            CollectionAssert.Contains(roles, "Quantizer");

            ProcessResult verified = await RunPowerShellAsync(
                Path.Combine(root, "scripts", "gguf-runtime", "Test-GgufRuntimeManifest.ps1"),
                "-PackageRoot", stage,
                "-ManifestPath", manifest);
            Assert.AreEqual(0, verified.ExitCode, verified.Output);
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }
    }

    private static async Task<ProcessResult> RunPowerShellAsync(
        string script,
        params string[] arguments)
    {
        ProcessStartInfo start = new("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start PowerShell.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, output + error);
    }

    private static async Task<ProcessResult> RunExecutableAsync(
        string executable,
        params string[] arguments)
    {
        ProcessStartInfo start = new(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start the staged executable.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, output + error);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return current.FullName;
            }
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
