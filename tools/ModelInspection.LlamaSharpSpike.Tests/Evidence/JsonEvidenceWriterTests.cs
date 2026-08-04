using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies parseable, privacy-safe, atomic local JSON evidence writing.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class JsonEvidenceWriterTests
{
    private enum ExampleCompletionStatus
    {
        Succeeded,
        Cancelled
    }

    private sealed class UnserializableEvidence
    {
        public string Value =>
            throw new InvalidOperationException("serialization failed");
    }

    [TestMethod]
    public async Task WriteAsync_CreatesParentDirectoryAndParseableCamelCaseJson()
    {
        using var directory = new TemporaryDirectory("json-create-parent");
        string outputPath = directory.Combine(
            "nested",
            "runtime-smoke.json");
        var writer = new JsonEvidenceWriter();

        string writtenPath = await writer.WriteAsync(
            CreateResult(succeeded: true),
            outputPath,
            CancellationToken.None);

        Assert.AreEqual(Path.GetFullPath(outputPath), writtenPath);
        Assert.IsTrue(File.Exists(writtenPath));

        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllTextAsync(writtenPath));

        Assert.IsTrue(
            document.RootElement.GetProperty("succeeded").GetBoolean());
        Assert.AreEqual(
            "0.27.0",
            document.RootElement
                .GetProperty("managedPackageVersion")
                .GetString());
        Assert.IsFalse(
            document.RootElement.TryGetProperty(
                "ManagedPackageVersion",
                out _));
    }

    [TestMethod]
    public async Task WriteAsync_SerializesEnumsAsReadableStrings()
    {
        using var directory = new TemporaryDirectory("json-string-enum");
        string outputPath = directory.Combine("enum-evidence.json");
        var writer = new JsonEvidenceWriter();

        await writer.WriteAsync(
            new
            {
                CompletionStatus = ExampleCompletionStatus.Cancelled
            },
            outputPath,
            CancellationToken.None);

        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllTextAsync(outputPath));

        Assert.AreEqual(
            "Cancelled",
            document.RootElement
                .GetProperty("completionStatus")
                .GetString());
    }

    [TestMethod]
    public async Task WriteAsync_WhenPathExists_AtomicallyReplacesPreviousEvidence()
    {
        using var directory = new TemporaryDirectory("json-replacement");
        string outputPath = directory.Combine("runtime-smoke.json");
        var writer = new JsonEvidenceWriter();

        await writer.WriteAsync(
            CreateResult(succeeded: false),
            outputPath,
            CancellationToken.None);
        await writer.WriteAsync(
            CreateResult(succeeded: true),
            outputPath,
            CancellationToken.None);

        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllTextAsync(outputPath));

        Assert.IsTrue(
            document.RootElement.GetProperty("succeeded").GetBoolean());
        AssertNoTemporaryFiles(directory.Path);
    }

    [TestMethod]
    public async Task WriteAsync_WhenSerializationFails_PreservesExistingEvidence()
    {
        using var directory = new TemporaryDirectory(
            "json-serialization-failure");
        string outputPath = directory.Combine("evidence.json");
        const string original = "{\"valid\":true}";
        await File.WriteAllTextAsync(outputPath, original);
        var writer = new JsonEvidenceWriter();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await writer.WriteAsync(
                new UnserializableEvidence(),
                outputPath,
                CancellationToken.None));

        Assert.AreEqual(original, await File.ReadAllTextAsync(outputPath));
        AssertNoTemporaryFiles(directory.Path);
    }

    [TestMethod]
    public async Task WriteAsync_WithPreCancelledToken_PreservesExistingEvidence()
    {
        using var directory = new TemporaryDirectory("json-pre-cancelled");
        string outputPath = directory.Combine("evidence.json");
        const string original = "{\"valid\":true}";
        await File.WriteAllTextAsync(outputPath, original);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var writer = new JsonEvidenceWriter();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await writer.WriteAsync(
                CreateResult(succeeded: false),
                outputPath,
                cancellationSource.Token));

        Assert.AreEqual(original, await File.ReadAllTextAsync(outputPath));
        AssertNoTemporaryFiles(directory.Path);
    }

    [TestMethod]
    public async Task WriteAsync_WhenDestinationIsLocked_CleansTemporaryFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory("json-locked-output");
        string outputPath = directory.Combine("evidence.json");
        await File.WriteAllTextAsync(outputPath, "existing");

        await using var lockStream = new FileStream(
            outputPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var writer = new JsonEvidenceWriter();

        await AssertFileSystemWriteFailureAsync(
            async () => await writer.WriteAsync(
                CreateResult(succeeded: true),
                outputPath,
                CancellationToken.None));

        AssertNoTemporaryFiles(directory.Path);
    }

    [TestMethod]
    public async Task WriteAsync_WhenDestinationIsDirectory_CleansTemporaryFile()
    {
        using var directory = new TemporaryDirectory("json-directory-output");
        string outputPath = directory.Combine("evidence.json");
        Directory.CreateDirectory(outputPath);
        var writer = new JsonEvidenceWriter();

        await AssertFileSystemWriteFailureAsync(
            async () => await writer.WriteAsync(
                CreateResult(succeeded: true),
                outputPath,
                CancellationToken.None));

        AssertNoTemporaryFiles(directory.Path);
    }

    [TestMethod]
    public async Task WriteAsync_WithBlankPath_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await new JsonEvidenceWriter().WriteAsync(
                CreateResult(succeeded: true),
                "   ",
                CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteAsync_WithNullEvidence_ThrowsArgumentNullException()
    {
        using var directory = new TemporaryDirectory("json-null-evidence");

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await new JsonEvidenceWriter().WriteAsync<object>(
                null!,
                directory.Combine("evidence.json"),
                CancellationToken.None));
    }

    private static async Task AssertFileSystemWriteFailureAsync(
        Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        Assert.Fail(
            "Expected an IOException or UnauthorizedAccessException from the " +
            "operating-system file move.");
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

    private static void AssertNoTemporaryFiles(string directory)
    {
        Assert.AreEqual(
            0,
            Directory.GetFiles(
                directory,
                "*.tmp-*",
                SearchOption.AllDirectories).Length);
    }
}
