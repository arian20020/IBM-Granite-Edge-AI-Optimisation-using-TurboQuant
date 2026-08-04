using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies that local feasibility evidence is written as parseable JSON and
/// can safely replace an earlier run at the same ignored path.
/// </summary>
[TestClass]
public sealed class SmokeEvidenceWriterTests
{
    [TestMethod]
    public async Task WriteAsync_CreatesParseableJsonEvidence()
    {
        string testDirectory = CreateTestDirectory();

        try
        {
            string outputPath =
                Path.Combine(testDirectory, "runtime-smoke.json");
            var writer = new SmokeEvidenceWriter();
            NativeBackendSmokeResult result = CreateResult(succeeded: true);

            string writtenPath = await writer.WriteAsync(
                result,
                outputPath,
                CancellationToken.None);

            Assert.AreEqual(
                Path.GetFullPath(outputPath),
                writtenPath);
            Assert.IsTrue(File.Exists(writtenPath));

            await using FileStream stream = File.OpenRead(writtenPath);
            using JsonDocument document =
                await JsonDocument.ParseAsync(stream);

            Assert.IsTrue(
                document.RootElement
                    .GetProperty("succeeded")
                    .GetBoolean());
            Assert.AreEqual(
                "0.27.0",
                document.RootElement
                    .GetProperty("managedPackageVersion")
                    .GetString());
        }
        finally
        {
            DeleteDirectory(testDirectory);
        }
    }

    [TestMethod]
    public async Task WriteAsync_WhenPathExists_AtomicallyReplacesPreviousEvidence()
    {
        string testDirectory = CreateTestDirectory();

        try
        {
            string outputPath =
                Path.Combine(testDirectory, "runtime-smoke.json");
            var writer = new SmokeEvidenceWriter();

            await writer.WriteAsync(
                CreateResult(succeeded: false),
                outputPath,
                CancellationToken.None);
            await writer.WriteAsync(
                CreateResult(succeeded: true),
                outputPath,
                CancellationToken.None);

            await using FileStream stream = File.OpenRead(outputPath);
            using JsonDocument document =
                await JsonDocument.ParseAsync(stream);

            Assert.IsTrue(
                document.RootElement
                    .GetProperty("succeeded")
                    .GetBoolean());
        }
        finally
        {
            DeleteDirectory(testDirectory);
        }
    }

    private static NativeBackendSmokeResult CreateResult(bool succeeded)
    {
        DateTimeOffset startedAt =
            new(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

        return new NativeBackendSmokeResult
        {
            StartedAtUtc = startedAt,
            CompletedAtUtc = startedAt.AddSeconds(1),
            Succeeded = succeeded,
            ManagedPackageName =
                PinnedApplicationRuntime.ManagedPackageName,
            ManagedPackageVersion =
                PinnedApplicationRuntime.ManagedPackageVersion,
            BackendPackageName =
                PinnedApplicationRuntime.BackendPackageName,
            BackendPackageVersion =
                PinnedApplicationRuntime.BackendPackageVersion,
            ExpectedLlamaCppCommit =
                PinnedApplicationRuntime.ExpectedLlamaCppCommit,
            IntendedProductionRuntimeIdentifier =
                PinnedApplicationRuntime.IntendedProductionRuntimeIdentifier,
            ProcessArchitecture = "X64",
            OperatingSystem = "Test OS",
            FrameworkDescription = ".NET 8 Test",
            Logs = Array.Empty<NativeBackendLogEntry>()
        };
    }

    private static string CreateTestDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-LlamaSharpSpikeTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
