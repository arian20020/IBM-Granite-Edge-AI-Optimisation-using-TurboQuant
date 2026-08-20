using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[TestCategory("OfficialNative")]
public sealed class OfficialCpuFixtureTests
{
    private const string PackageDigest =
        "b5316ac62e1e846b33ec92e5ad555859238af70ffd6b75aa50500995fbe15372";
    private const string ModelDigest =
        "894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c";
    private const long ModelLength = 88;
    private const string ExpectedRuntime =
        "2026.3.0-22451-8a17657b995-releases/2026/3";
    private const string ExpectedGenAi = "2026.3.0.0-3277-bd8d6542e3c";
    private const string ExpectedTokenizers =
        "2026.3.0.0-703-183c6f25cda";

    [TestMethod]
    public async Task CanonicalFixtureInspectsAndGeneratesFromTwoCleanClosures()
    {
        string stageA = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string stageB = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_B");
        Assert.AreNotEqual(
            Path.GetFullPath(stageA),
            Path.GetFullPath(stageB),
            ignoreCase: true,
            "FIX-01 requires independent staged closures.");

        string package = LocateCanonicalPackage();
        await VerifyClosureAsync(stageA, package).ConfigureAwait(false);
        await VerifyClosureAsync(stageB, package).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task RealCpuStopCancellationAndContextFailureAreBoundedAndLeaveNoResidue()
    {
        string stage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string package = LocateCanonicalPackage();

        await using (OpenVinoConversation stopped = await StartConversationAsync(
            stage, package, maximumContextTokens: 64).ConfigureAwait(false))
        {
            List<TokenEvent> stoppedTokens = [];
            Task<IOpenVinoEvent> active = stopped.PromptAsync(
                new PromptCommand(stopped.SessionId, Guid.NewGuid(), "hello", 2),
                new InlineProgress<TokenEvent>(stoppedTokens.Add),
                CancellationToken.None);
            await Task.Delay(50).ConfigureAwait(false);
            await stopped.StopAsync(CancellationToken.None).ConfigureAwait(false);
            TurnCompletedEvent terminal = Assert.IsInstanceOfType<TurnCompletedEvent>(
                await active.ConfigureAwait(false));
            Assert.AreEqual(OpenVinoTurnDisposition.Stopped, terminal.Disposition);

            List<TokenEvent> nextTokens = [];
            TurnCompletedEvent next = Assert.IsInstanceOfType<TurnCompletedEvent>(
                await stopped.PromptAsync(
                    new PromptCommand(stopped.SessionId, Guid.NewGuid(), "hello", 2),
                    new InlineProgress<TokenEvent>(nextTokens.Add),
                    CancellationToken.None).ConfigureAwait(false));
            Assert.AreEqual(OpenVinoTurnDisposition.Completed, next.Disposition);
            Assert.AreEqual("fixture", string.Concat(nextTokens.Select(static value => value.Text)));
            await stopped.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }

        List<TokenEvent> cancelledTokens = [];
        await using (OpenVinoConversation cancelled = await StartConversationAsync(
            stage, package, maximumContextTokens: 64).ConfigureAwait(false))
        {
            Task<IOpenVinoEvent> active = cancelled.PromptAsync(
                new PromptCommand(cancelled.SessionId, Guid.NewGuid(), "hello", 2),
                new InlineProgress<TokenEvent>(cancelledTokens.Add),
                CancellationToken.None);
            await Task.Delay(50).ConfigureAwait(false);
            OpenVinoWorkerClientException cancellation = await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(
                async () =>
                {
                    await cancelled.CancelAsync(CancellationToken.None).ConfigureAwait(false);
                    await active.ConfigureAwait(false);
                }).ConfigureAwait(false);
            Assert.AreEqual(OpenVinoSupportCode.OperationCancelled, cancellation.SupportCode);
        }
        Assert.AreEqual(0, cancelledTokens.Count, "Cancellation must publish no actionable token text.");

        await using (OpenVinoConversation constrained = await StartConversationAsync(
            stage, package, maximumContextTokens: 1).ConfigureAwait(false))
        {
            IOpenVinoEvent terminal = await constrained.PromptAsync(
                new PromptCommand(constrained.SessionId, Guid.NewGuid(), "hello", 2),
                tokens: null,
                CancellationToken.None).ConfigureAwait(false);
            TurnFailedEvent failed = Assert.IsInstanceOfType<TurnFailedEvent>(terminal);
            Assert.AreEqual(OpenVinoSupportCode.RuntimeContextExceeded, failed.SupportCode);
            await constrained.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await AssertNoOfficialWorkerProcessAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task CorruptMainAndTokenizerModelsFailWithFixedPathFreeInspectionEvidence()
    {
        string stage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        foreach (string relative in new[] { "openvino_model.xml", "openvino_tokenizer.xml" })
        {
            using TemporaryPackage package = TemporaryPackage.CopyFrom(LocateCanonicalPackage());
            string corrupt = Path.Combine(package.Path, relative);
            byte[] bytes = File.ReadAllBytes(corrupt);
            File.WriteAllBytes(corrupt, bytes[..32]);
            PackageIdentity identity = CalculateIdentity(package.Path);

            IOpenVinoEvent terminal = await CreateClient(stage).InspectAsync(
                new StartInspectionCommand(
                    Guid.NewGuid(),
                    package.Path,
                    identity.PackageDigest,
                    identity.ModelDigest,
                    identity.ModelLength),
                CancellationToken.None).ConfigureAwait(false);
            InspectionFailedEvent failed = Assert.IsInstanceOfType<InspectionFailedEvent>(terminal);
            Assert.AreEqual(OpenVinoSupportCode.PackageInconsistentResource, failed.SupportCode);
        }

        await AssertNoOfficialWorkerProcessAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task RealWorkerExitsWhenItsManagedParentClosesPipesDuringGeneration()
    {
        string stage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string repository = FindRepositoryRoot();
        string helper = Path.Combine(
            repository,
            "tests",
            "ProcessFixtures",
            "GraniteEdgeAI.OpenVino.ParentExitFixture",
            "bin",
#if DEBUG
            "x64",
            "Debug",
#else
            "x64",
            "Release",
#endif
            "net8.0-windows10.0.19041.0",
            "win-x64",
            "GraniteEdgeAI.OpenVino.ParentExitFixture.exe");
        Assert.IsTrue(File.Exists(helper), "The managed parent-loss fixture was not built.");
        ProcessStartInfo start = new()
        {
            FileName = helper,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(stage);
        start.ArgumentList.Add(LocateCanonicalPackage());
        using Process parent = Process.Start(start) ?? throw new InvalidOperationException();
        string? childLine = await parent.StandardOutput.ReadLineAsync()
            .WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Assert.IsTrue(int.TryParse(childLine, out int childId));
        await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.AreEqual(0, parent.ExitCode, await parent.StandardError.ReadToEndAsync().ConfigureAwait(false));

        try
        {
            using Process child = Process.GetProcessById(childId);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            // The worker exited before it could be reopened by PID.
        }
        await AssertNoOfficialWorkerProcessAsync().ConfigureAwait(false);
    }

    private static async Task VerifyClosureAsync(string stage, string package)
    {
        OpenVinoWorkerClient client = CreateClient(stage);
        Guid inspectionRunId = Guid.NewGuid();
        IOpenVinoEvent inspected = await client.InspectAsync(
            new StartInspectionCommand(
                inspectionRunId,
                package,
                PackageDigest,
                ModelDigest,
                ModelLength),
            CancellationToken.None).ConfigureAwait(false);

        InspectionCompletedEvent inspection =
            Assert.IsInstanceOfType<InspectionCompletedEvent>(inspected);
        Assert.AreEqual(PackageDigest, inspection.PackageManifestDigest);
        Assert.AreEqual(ModelDigest, inspection.ModelSha256);
        Assert.AreEqual(ModelLength, inspection.ModelLengthBytes);
        Assert.IsTrue(inspection.MainModelParsed);
        Assert.IsTrue(inspection.TokenizerParsed);
        Assert.IsTrue(inspection.DetokenizerParsed);
        AssertEvidence(inspection.BuildEvidence, stage);

        Guid sessionId = Guid.NewGuid();
        await using OpenVinoConversation conversation =
            await client.StartSessionAsync(
                new StartSessionCommand(
                    sessionId,
                    inspectionRunId,
                    package,
                    PackageDigest,
                    ModelDigest,
                    ModelLength,
                    new OpenVinoDeviceRequest("CPU"),
                    new OpenVinoGenerationLimits(64, 2)),
                CancellationToken.None).ConfigureAwait(false);

        foreach (string prompt in new[] { "hello", "hello" })
        {
            List<TokenEvent> tokens = [];
            IOpenVinoEvent terminal = await conversation.PromptAsync(
                new PromptCommand(sessionId, Guid.NewGuid(), prompt, 2),
                new InlineProgress<TokenEvent>(tokens.Add),
                CancellationToken.None).ConfigureAwait(false);
            if (terminal is TurnFailedEvent failed)
            {
                Assert.Fail($"Official native turn failed with {failed.SupportCode}.");
            }
            TurnCompletedEvent completed =
                Assert.IsInstanceOfType<TurnCompletedEvent>(terminal);
            Assert.AreEqual(OpenVinoTurnDisposition.Completed, completed.Disposition);
            Assert.AreEqual(1, tokens.Count, "EOS must not produce an empty token frame.");
            Assert.AreEqual(2, completed.GeneratedTokenCount);
            Assert.IsTrue(completed.PromptTokenCount is > 0 and <= 64);
            Assert.AreEqual("fixture", string.Concat(tokens.Select(static token => token.Text)));
        }

        await conversation.CloseAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<OpenVinoConversation> StartConversationAsync(
        string stage,
        string package,
        int maximumContextTokens)
    {
        return await CreateClient(stage).StartSessionAsync(
            new StartSessionCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                package,
                PackageDigest,
                ModelDigest,
                ModelLength,
                new OpenVinoDeviceRequest("CPU"),
                new OpenVinoGenerationLimits(maximumContextTokens, 2)),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static void AssertEvidence(OpenVinoBuildEvidence evidence, string stage)
    {
        Assert.AreEqual(ExpectedRuntime, evidence.RuntimeBuild);
        Assert.AreEqual(ExpectedGenAi, evidence.GenAiBuild);
        Assert.AreEqual(ExpectedTokenizers, evidence.TokenizersBuild);
        string expectedManifestDigest = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(Path.Combine(stage, "worker-manifest.json")))).ToLowerInvariant();
        Assert.AreEqual(expectedManifestDigest, evidence.WorkerManifestDigest);
    }

    private static OpenVinoWorkerClient CreateClient(string stage)
    {
        OpenVinoWorkerInstallation installation = new(
            Path.GetFullPath(stage),
            "OpenVinoOfficial.Worker.exe",
            OpenVinoProtocol.OfficialProtocolId);
        return new OpenVinoWorkerClient(
            OpenVinoWorkerClientOptions.CreateDefault(installation));
    }

    private static string RequireStage(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Inconclusive(variable + " is required for official native tests.");
        }

        return value!;
    }

    private static string LocateCanonicalPackage()
    {
        DirectoryInfo? cursor = new(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            string candidate = Path.Combine(
                cursor.FullName,
                "tests",
                "TestFixtures",
                "OpenVINO",
                "GenAI",
                "TinySyntheticV1",
                "package");
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
            cursor = cursor.Parent;
        }

        throw new InvalidOperationException("Canonical fixture package is unavailable.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? cursor = new(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(Path.Combine(cursor.FullName, "global.json")))
        {
            cursor = cursor.Parent;
        }
        return cursor?.FullName ?? throw new DirectoryNotFoundException();
    }

    private static PackageIdentity CalculateIdentity(string package)
    {
        StringBuilder tuples = new();
        foreach (string file in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(package, file).Replace('\\', '/');
            FileInfo info = new(file);
            string digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            tuples.Append(relative).Append('\0').Append(info.Length).Append('\0').Append(digest).Append('\n');
        }
        string packageDigest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(tuples.ToString()))).ToLowerInvariant();
        string model = Path.Combine(package, "openvino_model.bin");
        return new PackageIdentity(
            packageDigest,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(model))).ToLowerInvariant(),
            new FileInfo(model).Length);
    }

    private static async Task AssertNoOfficialWorkerProcessAsync()
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            Process[] processes = Process.GetProcessesByName("OpenVinoOfficial.Worker");
            try
            {
                if (processes.Length == 0)
                {
                    return;
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
            await Task.Delay(25).ConfigureAwait(false);
        }
        Assert.Fail("An official OpenVINO worker process remained after cleanup.");
    }

    private sealed class TemporaryPackage : IDisposable
    {
        private TemporaryPackage(string path) => Path = path;
        public string Path { get; }

        public static TemporaryPackage CopyFrom(string source)
        {
            string root = Directory.CreateTempSubdirectory("GraniteEdgeAI-OpenVino-Official-").FullName;
            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(System.IO.Path.Combine(
                    root,
                    System.IO.Path.GetRelativePath(source, directory)));
            }
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = System.IO.Path.Combine(
                    root,
                    System.IO.Path.GetRelativePath(source, file));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            return new TemporaryPackage(System.IO.Path.GetFullPath(root));
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed record PackageIdentity(
        string PackageDigest,
        string ModelDigest,
        long ModelLength);

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
