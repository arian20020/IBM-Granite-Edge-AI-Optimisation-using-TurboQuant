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
