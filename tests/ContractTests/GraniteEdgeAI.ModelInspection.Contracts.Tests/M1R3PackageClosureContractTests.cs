using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.ModelInspection.Contracts.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class M1R3PackageClosureContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    [DoNotParallelize]
    public void EvaluatorResolvesInstalledDotNetWithoutEnvironmentOverride()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        string payload = directory.WriteText("payload.txt", "portable host probe");
        string project = directory.WriteText(
            "PortableHost.proj",
            $"""
            <Project>
              <ItemGroup>
                <None Include="{EscapeXml(payload)}" />
              </ItemGroup>
            </Project>
            """);
        string? originalHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", null);
            IReadOnlyList<EvaluatedMsBuildItem> items = EvaluatedMsBuildItems.Evaluate(
                project,
                ["None"]);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(Path.GetFullPath(payload), Path.GetFullPath(items[0].FullPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", originalHost);
        }
    }

    [TestMethod]
    public void DotNetHostResolutionRetainsPortableCandidateClasses()
    {
        string source = File.ReadAllText(Path.Combine(
            Root,
            "tests",
            "ContractTests",
            "GraniteEdgeAI.ModelInspection.Contracts.Tests",
            "Support",
            "EvaluatedMsBuildItems.cs"));

        StringAssert.Contains(source, "DOTNET_HOST_PATH");
        StringAssert.Contains(source, "Environment.ProcessPath");
        StringAssert.Contains(source, "DOTNET_ROOT");
        StringAssert.Contains(source, "Environment.SpecialFolder.ProgramFiles");
        Assert.IsFalse(
            source.Contains(
                "return File.Exists(approvedSdk)",
                StringComparison.Ordinal),
            "One absolute machine-specific SDK path must not be the sole fallback.");
    }

    [TestMethod]
    public void DotNetHostSelectorPreservesExplicitCandidatePrecedence()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        string hostFileName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string explicitHost = directory.WriteBytes(
            $"explicit/{hostFileName}",
            [1]);
        string standardHost = directory.WriteBytes(
            $"standard/{hostFileName}",
            [2]);

        Assert.AreEqual(
            explicitHost,
            EvaluatedMsBuildItems.SelectDotNetHost(
                [explicitHost, standardHost],
                hostFileName));
    }

    [TestMethod]
    public void DotNetHostCandidateConstructionPreservesTrustedPrecedence()
    {
        string hostFileName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        string explicitHost = Path.Combine(root, "explicit", hostFileName);
        string currentProcess = Path.Combine(root, "current", hostFileName);
        string dotnetRoot = Path.Combine(root, "sdk-root");
        string programFiles = Path.Combine(root, "program-files");
        string approvedSdk = Path.Combine(root, "approved", hostFileName);

        IReadOnlyList<string?> candidates =
            EvaluatedMsBuildItems.BuildDotNetHostCandidates(
                explicitHost,
                currentProcess,
                dotnetRoot,
                programFiles,
                approvedSdk,
                hostFileName);

        CollectionAssert.AreEqual(
            new string?[]
            {
                explicitHost,
                currentProcess,
                Path.Combine(dotnetRoot, hostFileName),
                Path.Combine(programFiles, "dotnet", hostFileName),
                approvedSdk,
            },
            candidates.ToArray());
    }

    [TestMethod]
    public void DotNetHostSelectorRejectsEveryUnverifiedCandidateClass()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        string hostFileName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string validHost = directory.WriteBytes(
            $"valid/{hostFileName}",
            [1]);
        string missingHost = Path.Combine(
            directory.CreateDirectory("missing"),
            hostFileName);
        string wrongFileName = directory.WriteBytes(
            "wrong/worker-host.exe",
            [2]);
        string relativeExistingHost = Path.GetRelativePath(
            Environment.CurrentDirectory,
            validHost);
        string nonConcreteHost = Path.Combine(
            Path.GetDirectoryName(validHost)!,
            "nested",
            "..",
            hostFileName);

        Assert.AreEqual(
            validHost,
            EvaluatedMsBuildItems.SelectDotNetHost(
                [missingHost, validHost],
                hostFileName),
            "A missing candidate must not be selected.");
        Assert.AreEqual(
            validHost,
            EvaluatedMsBuildItems.SelectDotNetHost(
                [wrongFileName, validHost],
                hostFileName),
            "A candidate with the wrong executable name must not be selected.");
        Assert.AreEqual(
            validHost,
            EvaluatedMsBuildItems.SelectDotNetHost(
                [relativeExistingHost, validHost],
                hostFileName),
            "A relative candidate must not be selected even when it resolves to an existing file.");
        Assert.AreEqual(
            validHost,
            EvaluatedMsBuildItems.SelectDotNetHost(
                [nonConcreteHost, validHost],
                hostFileName),
            "A non-normalized candidate must not be selected even when it resolves to an existing file.");
    }

    [TestMethod]
    public void PackageContractDoesNotWhitelistUnresolvedImportedExpressions()
    {
        string source = File.ReadAllText(Path.Combine(
            Root,
            "tests",
            "ContractTests",
            "GraniteEdgeAI.ModelInspection.Contracts.Tests",
            "ModelInspectionVisualSourceContractTests.cs"));

        Assert.IsFalse(
            source.Contains(
                "IsControlledImportedPackageExpression",
                StringComparison.Ordinal),
            "Imported package expressions must be evaluated to concrete items, never accepted by a source allowlist.");
    }

    [TestMethod]
    public void EvaluatorExecutesImportedTargetAndReturnsConcreteClosure()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        string stage = directory.CreateDirectory("stage");
        string payload = directory.WriteBytes("stage/payload.bin", [1, 2, 3]);
        string targets = directory.WriteText(
            "Imported.targets",
            """
            <Project>
              <Target Name="CreatePackageItems">
                <ItemGroup>
                  <_ImportedFiles Include="$(StageRoot)\**\*" />
                  <Content Include="@(_ImportedFiles)" TargetPath="payload\%(RecursiveDir)%(Filename)%(Extension)" />
                </ItemGroup>
              </Target>
            </Project>
            """);
        string project = directory.WriteText(
            "Closure.proj",
            $"""
            <Project>
              <Import Project="{EscapeXml(targets)}" />
            </Project>
            """);

        IReadOnlyList<EvaluatedMsBuildItem> items = EvaluatedMsBuildItems.Evaluate(
            project,
            ["Content"],
            ["CreatePackageItems"],
            new Dictionary<string, string> { ["StageRoot"] = stage });

        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(Path.GetFullPath(payload), Path.GetFullPath(items[0].FullPath));
        EvaluatedMsBuildItems.RequireContained(items, stage);
    }

    [TestMethod]
    public void EvaluatorRejectsExpressionThatSurvivesTargetExecution()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        string project = directory.WriteText(
            "Unresolved.proj",
            """
            <Project>
              <PropertyGroup><_Dollar>$</_Dollar></PropertyGroup>
              <Target Name="CreatePackageItems">
                <ItemGroup>
                  <Content Include="$(_Dollar)(_MissingPackageFile)" />
                </ItemGroup>
              </Target>
            </Project>
            """);

        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() =>
            EvaluatedMsBuildItems.Evaluate(
                project,
                ["Content"],
                ["CreatePackageItems"]));
        StringAssert.Contains(error.Message, "retains unresolved expression");
    }

    [TestMethod]
    public void ProductionGgufTargetProducesConcreteContainedPackageClosure()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        string stage = CreateControlledGgufStage(directory);
        string manifest = Path.Combine(
            stage,
            "llama-quantize.package.manifest.json");
        string manifestSha = Sha256(manifest);
        string appProject = Path.Combine(
            Root,
            "IBM Granite with TurboQuant (Intel)",
            "IBM Granite with TurboQuant (Intel).csproj");

        IReadOnlyList<EvaluatedMsBuildItem> items = EvaluatedMsBuildItems.Evaluate(
            appProject,
            ["Content"],
            ["VerifyAndPackageGgufQuantizer"],
            new Dictionary<string, string>
            {
                ["Configuration"] = "Debug",
                ["Platform"] = "x64",
                ["DesignTimeBuild"] = "false",
                ["GgufQuantizerPackagingRequired"] = "true",
                ["GgufQuantizerStageDirectory"] = stage,
                ["GgufQuantizerManifestSha256"] = manifestSha,
            },
            TimeSpan.FromSeconds(60));
        EvaluatedMsBuildItem[] stageItems = items
            .Where(item => Path.GetFullPath(item.FullPath).StartsWith(
                Path.GetFullPath(stage) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.AreEqual(3, stageItems.Length);
        EvaluatedMsBuildItems.RequireContained(stageItems, stage);
        Assert.IsTrue(stageItems.All(item =>
            item.TargetPath?.StartsWith(
                "Tools\\GgufQuantizer\\",
                StringComparison.OrdinalIgnoreCase) is true));
    }

    private static string CreateControlledGgufStage(TemporaryDirectory directory)
    {
        string stage = directory.CreateDirectory("gguf-stage");
        string executable = directory.WriteBytes(
            "gguf-stage/bin/llama-quantize.exe",
            Encoding.ASCII.GetBytes("controlled contract fixture"));
        string license = directory.WriteText(
            "gguf-stage/licenses/LICENSE.llama.cpp.txt",
            "controlled licence fixture\n");
        object manifest = new
        {
            schemaVersion = 1,
            packageId = "granite-edge-ai-llama-quantize-x64",
            source = new
            {
                url = "https://github.com/ggml-org/llama.cpp.git",
                commit = "3f7c29d318e317b63f54c558bc69803963d7d88c",
            },
            architecture = "x64",
            configuration = "Release",
            executableRelativePath = "bin/llama-quantize.exe",
            allowedTokens = new[] { "Q2_K", "Q3_K_M", "Q4_K_M", "Q5_K_M", "Q6_K", "Q8_0" },
            maximumSourceBytes = 1_000_000L,
            maximumOutputBytes = 1_000_000L,
            timeoutSeconds = 60,
            files = new[]
            {
                FileRecord("bin/llama-quantize.exe", executable),
                FileRecord("licenses/LICENSE.llama.cpp.txt", license),
            },
        };
        directory.WriteText(
            "gguf-stage/llama-quantize.package.manifest.json",
            JsonSerializer.Serialize(manifest));
        return stage;
    }

    private static object FileRecord(string relativePath, string path) => new
    {
        relativePath,
        length = new FileInfo(path).Length,
        sha256 = Sha256(path),
    };

    private static string Sha256(string path) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string EscapeXml(string value) => value.Replace("&", "&amp;");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;
        private string Path { get; }

        internal static TemporaryDirectory Create() => new(
            Directory.CreateTempSubdirectory("m1-r3-package-").FullName);

        internal string CreateDirectory(string relativePath) =>
            Directory.CreateDirectory(Resolve(relativePath)).FullName;

        internal string WriteText(string relativePath, string value)
        {
            string path = Resolve(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, value, new UTF8Encoding(false));
            return path;
        }

        internal string WriteBytes(string relativePath, byte[] value)
        {
            string path = Resolve(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, value);
            return path;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);

        private string Resolve(string relativePath) => System.IO.Path.Combine(
            Path,
            relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
    }
}
