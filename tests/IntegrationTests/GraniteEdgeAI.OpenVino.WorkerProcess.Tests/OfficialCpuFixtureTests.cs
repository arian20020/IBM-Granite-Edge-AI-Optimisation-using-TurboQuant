using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[TestCategory("OfficialNative")]
[DoNotParallelize]
public sealed class OfficialCpuFixtureTests
{
    internal const string PackageDigest =
        "b5316ac62e1e846b33ec92e5ad555859238af70ffd6b75aa50500995fbe15372";
    internal const string ModelDigest =
        "894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c";
    internal const long ModelLength = 88;
    private const string ExpectedRuntime =
        "2026.3.0-22451-8a17657b995-releases/2026/3";
    private const string ExpectedGenAi = "2026.3.0.0-3277-bd8d6542e3c";
    private const string ExpectedTokenizers =
        "2026.3.0.0-703-183c6f25cda";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void UclOverrideRejectsRepositoryFixtureAndRecordsExactExternalIdentity()
    {
        string? originalRoot = Environment.GetEnvironmentVariable(
            "OPENVINO_UCL_CONTROLLED_FIXTURE_ROOT");
        string? originalIdentity = Environment.GetEnvironmentVariable(
            "OPENVINO_UCL_CONTROLLED_FIXTURE_MANIFEST_SHA256");
        string repositoryFixture = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "TestFixtures",
            "OpenVINO",
            "GenAI",
            "TinySyntheticV1");
        string repositoryManifest = Path.Combine(repositoryFixture, "manifest.json");
        try
        {
            Environment.SetEnvironmentVariable(
                "OPENVINO_UCL_CONTROLLED_FIXTURE_ROOT",
                repositoryFixture);
            Environment.SetEnvironmentVariable(
                "OPENVINO_UCL_CONTROLLED_FIXTURE_MANIFEST_SHA256",
                Convert.ToHexString(SHA256.HashData(
                    File.ReadAllBytes(repositoryManifest))).ToLowerInvariant());
            Assert.ThrowsExactly<InvalidOperationException>(LocateCanonicalPackage);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "OPENVINO_UCL_CONTROLLED_FIXTURE_ROOT",
                originalRoot);
            Environment.SetEnvironmentVariable(
                "OPENVINO_UCL_CONTROLLED_FIXTURE_MANIFEST_SHA256",
                originalIdentity);
        }

        if (!string.IsNullOrWhiteSpace(originalRoot))
        {
            _ = LocateCanonicalPackage();
            TestContext.WriteLine(
                "OPENVINO_UCL_FIXTURE_CONSUMED_SHA256=" + originalIdentity);
        }
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
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
        OpenVinoBuildEvidence evidenceA = await VerifyClosureAsync(
            "A", stageA, package).ConfigureAwait(false);
        OpenVinoBuildEvidence evidenceB = await VerifyClosureAsync(
            "B", stageB, package).ConfigureAwait(false);
        Assert.AreEqual(evidenceA.RuntimeBuild, evidenceB.RuntimeBuild);
        Assert.AreEqual(evidenceA.GenAiBuild, evidenceB.GenAiBuild);
        Assert.AreEqual(evidenceA.TokenizersBuild, evidenceB.TokenizersBuild);
        TestContext.WriteLine("OPENVINO_MEASURED_REQUESTED_DEVICE=CPU");
        TestContext.WriteLine("OPENVINO_MEASURED_ACTUAL_EXECUTION_DEVICES=CPU");
        TestContext.WriteLine("OPENVINO_MEASURED_RUNTIME_BUILD=" + evidenceA.RuntimeBuild);
        TestContext.WriteLine("OPENVINO_MEASURED_GENAI_BUILD=" + evidenceA.GenAiBuild);
        TestContext.WriteLine(
            "OPENVINO_MEASURED_TOKENIZERS_BUILD=" + evidenceA.TokenizersBuild);
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    public async Task RealCpuU8KvCacheRequestIsAppliedAndReportedBeforeGeneration()
    {
        string stage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string package = LocateCanonicalPackage();
        await using OpenVinoConversation conversation = await CreateClient(stage)
            .StartSessionAsync(
                new StartSessionCommand(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    package,
                    PackageDigest,
                    ModelDigest,
                    ModelLength,
                    new OpenVinoDeviceRequest("CPU"),
                    new OpenVinoGenerationLimits(64, 2),
                    OpenVinoRuntimeOptions.U8),
                CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("u8", conversation.StartupEvidence.RequestedKvCachePrecision);
        Assert.AreEqual("u8", conversation.StartupEvidence.ActualKvCachePrecision);
        List<TokenEvent> tokens = [];
        TurnCompletedEvent completed = Assert.IsInstanceOfType<TurnCompletedEvent>(
            await conversation.PromptAsync(
                new PromptCommand(conversation.SessionId, Guid.NewGuid(), "hello", 2),
                new InlineProgress<TokenEvent>(tokens.Add),
                CancellationToken.None).ConfigureAwait(false));
        Assert.AreEqual(OpenVinoTurnDisposition.Completed, completed.Disposition);
        Assert.AreEqual("fixture", string.Concat(tokens.Select(static token => token.Text)));
        await conversation.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        await AssertNoOfficialWorkerProcessAsync().ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    public async Task RealCpuStopCancellationAndContextFailureAreBoundedAndLeaveNoResidue()
    {
        string stage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string package = LocateCanonicalPackage();

        await using (OpenVinoConversation stopped = await StartConversationAsync(
            stage, package, maximumContextTokens: 64).ConfigureAwait(false))
        {
            List<TokenEvent> stoppedTokens = [];
            TaskCompletionSource firstFragment = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            PromptCommand stoppedCommand = new(
                stopped.SessionId,
                Guid.NewGuid(),
                "hello",
                2);
            Task<IOpenVinoEvent> active = stopped.PromptAsync(
                stoppedCommand,
                new InlineProgress<TokenEvent>(token =>
                {
                    stoppedTokens.Add(token);
                    firstFragment.TrySetResult();
                }),
                CancellationToken.None);
            await firstFragment.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            await stopped.StopAsync(
                stoppedCommand.TurnId,
                CancellationToken.None).ConfigureAwait(false);
            TurnCompletedEvent terminal = Assert.IsInstanceOfType<TurnCompletedEvent>(
                await active.ConfigureAwait(false));
            Assert.AreEqual(OpenVinoTurnDisposition.Stopped, terminal.Disposition);
            Assert.IsGreaterThanOrEqualTo(1, stoppedTokens.Count);

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
        TestContext.WriteLine("OPENVINO_MEASURED_CANCELLATION_DISPOSITION=passed");
        TestContext.WriteLine("OPENVINO_MEASURED_CLEANUP_DISPOSITION=zero_residue");
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
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
    [TestCategory("StableRouteAcceptance")]
    public async Task CorruptPackageSessionStartupEmitsOneTypedFailureAndLeavesNoResidue()
    {
        string stage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        using TemporaryPackage package = TemporaryPackage.CopyFrom(LocateCanonicalPackage());
        string tokenizer = Path.Combine(package.Path, "openvino_tokenizer.xml");
        byte[] bytes = File.ReadAllBytes(tokenizer);
        File.WriteAllBytes(tokenizer, bytes[..32]);
        PackageIdentity identity = CalculateIdentity(package.Path);

        OpenVinoWorkerClientException error =
            await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(() =>
                CreateClient(stage).StartSessionAsync(
                    new StartSessionCommand(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        package.Path,
                        identity.PackageDigest,
                        identity.ModelDigest,
                        identity.ModelLength,
                        new OpenVinoDeviceRequest("CPU"),
                        new OpenVinoGenerationLimits(64, 2)),
                    CancellationToken.None)).ConfigureAwait(false);

        Assert.AreEqual(OpenVinoSupportCode.PackageInconsistentResource, error.SupportCode);
        await AssertNoOfficialWorkerProcessAsync().ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    public async Task TerminationReleasesAllOwnedResourcesAndCreatesNoListenerOrTempArtifacts()
    {
        string sourceStage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        using TemporaryTree stage = TemporaryTree.CopyFrom(
            sourceStage,
            "GraniteEdgeAI-OpenVino-Stage-");
        using TemporaryTree package = TemporaryTree.CopyFrom(
            LocateCanonicalPackage(),
            "GraniteEdgeAI-OpenVino-Package-");
        using TemporaryTree operationTemp = TemporaryTree.CreateEmpty(
            "GraniteEdgeAI-OpenVino-Temp-");
        OpenVinoWorkerInstallation installation = CreateInstallation(stage.Path);
        OpenVinoWorkerClientOptions options = OpenVinoWorkerClientOptions
            .CreateDefault(installation);
        Dictionary<string, string?> environment = new(
            StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot"),
            ["WINDIR"] = Environment.GetEnvironmentVariable("WINDIR"),
            ["TEMP"] = operationTemp.Path,
            ["TMP"] = operationTemp.Path
        };
        OpenVinoWorkerClient client = new(
            options,
            new ProtectedWorkerSessionFactory(),
            () => environment);

        await using (OpenVinoConversation conversation =
            await StartConversationAsync(client, package.Path, 64)
                .ConfigureAwait(false))
        {
            await conversation.CloseAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }

        await AssertNoOfficialWorkerProcessAsync().ConfigureAwait(false);
        Assert.IsFalse(Directory.Exists(Path.Combine(operationTemp.Path, "listener")));
        Assert.IsFalse(Directory.Exists(Path.Combine(operationTemp.Path, "cache")));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(operationTemp.Path));
        foreach (string resource in new[]
        {
            Path.Combine(stage.Path, "worker-manifest.json"),
            Path.Combine(stage.Path, "OpenVinoOfficial.Worker.exe"),
            Path.Combine(stage.Path, "openvino.dll"),
            Path.Combine(package.Path, "openvino_model.bin")
        })
        {
            using FileStream exclusive = File.Open(
                resource,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
        }

        package.DeleteAndAssert();
        stage.DeleteAndAssert();
        operationTemp.DeleteAndAssert();
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    public async Task RealWorkerExitsWhenItsManagedParentClosesPipesDuringGeneration()
    {
        string stage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string repository = FindRepositoryRoot();
        ProcessStartInfo start = CreateParentExitFixtureStartInfo(repository);
        start.ArgumentList.Add(stage);
        start.ArgumentList.Add(LocateCanonicalPackage());
        using Process parent = Process.Start(start) ?? throw new InvalidOperationException();
        string? childLine = await parent.StandardOutput.ReadLineAsync()
            .WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        if (!int.TryParse(childLine, out int childId))
        {
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
            Assert.Fail(
                "Parent-loss fixture did not publish a worker identity: " +
                await parent.StandardError.ReadToEndAsync().ConfigureAwait(false));
        }
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

    internal static ProcessStartInfo CreateParentExitFixtureStartInfo(string repository)
    {
        string output = Path.Combine(
            repository,
            "tests",
            "ProcessFixtures",
            "GraniteEdgeAI.OpenVino.ParentExitFixture",
            "bin",
#if DEBUG
            "Debug",
#else
            "Release",
#endif
            "net8.0-windows10.0.19041.0",
            "win-x64");
        string executable = Path.Combine(
            output,
            "GraniteEdgeAI.OpenVino.ParentExitFixture.exe");
        string assembly = Path.Combine(
            output,
            "GraniteEdgeAI.OpenVino.ParentExitFixture.dll");
        ProcessStartInfo start = new()
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        string? configuredAssembly = Environment.GetEnvironmentVariable(
            "OPENVINO_PARENT_EXIT_FIXTURE_ASSEMBLY");
        if (!string.IsNullOrWhiteSpace(configuredAssembly))
        {
            string fullAssembly = Path.GetFullPath(configuredAssembly);
            if (!Path.GetFileName(fullAssembly).Equals(
                    "GraniteEdgeAI.OpenVino.ParentExitFixture.dll",
                    StringComparison.Ordinal) ||
                !File.Exists(fullAssembly))
            {
                Assert.Fail("The configured managed parent-loss fixture is invalid.");
            }
            start.FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            start.ArgumentList.Add(fullAssembly);
            return start;
        }

        if (File.Exists(executable))
        {
            start.FileName = executable;
            return start;
        }

        if (File.Exists(assembly))
        {
            start.FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            start.ArgumentList.Add(assembly);
            return start;
        }

        Assert.Fail("The managed parent-loss fixture was not built.");
        throw new InvalidOperationException();
    }

    [TestMethod]
    public void ParentLossFixtureLauncherFallsBackToFrameworkDependentAssembly()
    {
        string repository = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-ParentFixture-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(
            repository,
            "tests",
            "ProcessFixtures",
            "GraniteEdgeAI.OpenVino.ParentExitFixture",
            "bin",
#if DEBUG
            "Debug",
#else
            "Release",
#endif
            "net8.0-windows10.0.19041.0",
            "win-x64");
        Directory.CreateDirectory(output);
        string assembly = Path.Combine(
            output,
            "GraniteEdgeAI.OpenVino.ParentExitFixture.dll");
        File.WriteAllBytes(assembly, [1]);
        try
        {
            ProcessStartInfo start = CreateParentExitFixtureStartInfo(repository);
            Assert.IsFalse(string.IsNullOrWhiteSpace(start.FileName));
            Assert.HasCount(1, start.ArgumentList);
            Assert.AreEqual(assembly, start.ArgumentList[0]);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    private static async Task<OpenVinoBuildEvidence> VerifyClosureAsync(
        string closureLabel,
        string stage,
        string package)
    {
        OpenVinoWorkerClient client = CreateClient(stage);
        Guid inspectionRunId = Guid.NewGuid();
        IOpenVinoEvent inspected;
        try
        {
            inspected = await client.InspectAsync(
                new StartInspectionCommand(
                    inspectionRunId,
                    package,
                    PackageDigest,
                    ModelDigest,
                    ModelLength),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (OpenVinoWorkerClientException error)
        {
            Assert.Fail(
                $"Closure {closureLabel} inspection boundary failed with " +
                $"{error.SupportCode}; " +
                $"stderr='{error.RetainedStandardError}', " +
                $"truncated={error.StandardErrorTruncated}.");
            throw;
        }

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
        return inspection.BuildEvidence;
    }

    private static async Task<OpenVinoConversation> StartConversationAsync(
        string stage,
        string package,
        int maximumContextTokens)
    {
        return await StartConversationAsync(
            CreateClient(stage), package, maximumContextTokens).ConfigureAwait(false);
    }

    private static async Task<OpenVinoConversation> StartConversationAsync(
        OpenVinoWorkerClient client,
        string package,
        int maximumContextTokens)
    {
        return await client.StartSessionAsync(
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

    internal static OpenVinoWorkerClient CreateClient(string stage)
    {
        OpenVinoWorkerInstallation installation = CreateInstallation(stage);
        return new OpenVinoWorkerClient(
            OpenVinoWorkerClientOptions.CreateDefault(installation));
    }

    private static OpenVinoWorkerInstallation CreateInstallation(string stage) => new(
            Path.GetFullPath(stage),
            "OpenVinoOfficial.Worker.exe",
            OpenVinoProtocol.OfficialProtocolId,
            new OpenVinoBuildEvidence(
                ExpectedRuntime,
                ExpectedGenAi,
                ExpectedTokenizers,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
                    Path.Combine(stage, "worker-manifest.json"))))
                    .ToLowerInvariant()),
            new Dictionary<string, OpenVinoWorkerBinaryMachine>(StringComparer.Ordinal)
            {
                ["OpenVinoOfficial.Worker.exe"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_genai.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_cpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_gpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_ir_frontend.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_tokenizers.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbb12.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbbbind_2_5.dll"] = OpenVinoWorkerBinaryMachine.Amd64
            });

    internal static string RequireStage(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Inconclusive(variable + " is required for official native tests.");
        }

        return value!;
    }

    internal static string LocateCanonicalPackage()
    {
        string? controlledRoot = Environment.GetEnvironmentVariable(
            "OPENVINO_UCL_CONTROLLED_FIXTURE_ROOT");
        if (!string.IsNullOrWhiteSpace(controlledRoot))
        {
            string fixtureRoot = Path.GetFullPath(controlledRoot);
            string repositoryRoot = Path.GetFullPath(FindRepositoryRoot())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string repositoryPrefix = repositoryRoot + Path.DirectorySeparatorChar;
            string? expectedManifestSha = Environment.GetEnvironmentVariable(
                "OPENVINO_UCL_CONTROLLED_FIXTURE_MANIFEST_SHA256");
            string manifestPath = Path.Combine(fixtureRoot, "manifest.json");
            string packagePath = Path.Combine(fixtureRoot, "package");
            if (fixtureRoot.Equals(repositoryRoot, StringComparison.OrdinalIgnoreCase) ||
                fixtureRoot.StartsWith(repositoryPrefix, StringComparison.OrdinalIgnoreCase) ||
                expectedManifestSha is null ||
                !System.Text.RegularExpressions.Regex.IsMatch(
                    expectedManifestSha,
                    "^[0-9a-f]{64}$",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
                !File.Exists(manifestPath) ||
                !Directory.Exists(packagePath))
            {
                throw new InvalidOperationException(
                    "The UCL controlled fixture override is invalid.");
            }

            string actualManifestSha = Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(manifestPath))).ToLowerInvariant();
            if (!actualManifestSha.Equals(
                    expectedManifestSha,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The UCL controlled fixture identity is invalid.");
            }

            return packagePath;
        }

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

    internal static async Task AssertNoOfficialWorkerProcessAsync()
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

    private static string CreateOperationTempRoot(string prefix)
    {
        string root = Path.GetFullPath(
            Directory.CreateTempSubdirectory(prefix).FullName);
        Assert.IsTrue(
            IsStrictDescendant(Path.GetTempPath(), root),
            "Operation-owned cleanup root escaped the system temp root.");
        return root;
    }

    private static void AssertCleanupSeparatedFromSource(
        string source,
        string cleanupRoot)
    {
        string fullSource = Path.GetFullPath(source);
        string fullCleanup = Path.GetFullPath(cleanupRoot);
        Assert.AreNotEqual(fullSource, fullCleanup, ignoreCase: true);
        Assert.IsFalse(IsStrictDescendant(fullSource, fullCleanup));
        Assert.IsFalse(IsStrictDescendant(fullCleanup, fullSource));
    }

    private static bool IsStrictDescendant(string parent, string candidate)
    {
        string fullParent = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string fullCandidate = Path.GetFullPath(candidate);
        return fullCandidate.Length > fullParent.Length &&
            fullCandidate.StartsWith(
                fullParent, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TemporaryPackage : IDisposable
    {
        private TemporaryPackage(string path) => Path = path;
        public string Path { get; }

        public static TemporaryPackage CopyFrom(string source)
        {
            string root = CreateOperationTempRoot(
                "GraniteEdgeAI-OpenVino-Official-");
            AssertCleanupSeparatedFromSource(source, root);
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
                File.SetAttributes(
                    destination,
                    File.GetAttributes(destination) & ~FileAttributes.ReadOnly);
            }
            return new TemporaryPackage(System.IO.Path.GetFullPath(root));
        }

        public void Dispose()
        {
            Assert.IsTrue(IsStrictDescendant(System.IO.Path.GetTempPath(), Path));
            Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class TemporaryTree : IDisposable
    {
        private bool _deleted;

        private TemporaryTree(string path) => Path = path;

        public string Path { get; }

        public static TemporaryTree CreateEmpty(string prefix) => new(
            CreateOperationTempRoot(prefix));

        public static TemporaryTree CopyFrom(string source, string prefix)
        {
            TemporaryTree result = CreateEmpty(prefix);
            AssertCleanupSeparatedFromSource(source, result.Path);
            foreach (string directory in Directory.EnumerateDirectories(
                source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(System.IO.Path.Combine(
                    result.Path,
                    System.IO.Path.GetRelativePath(source, directory)));
            }
            foreach (string file in Directory.EnumerateFiles(
                source, "*", SearchOption.AllDirectories))
            {
                string destination = System.IO.Path.Combine(
                    result.Path,
                    System.IO.Path.GetRelativePath(source, file));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
                File.SetAttributes(
                    destination,
                    File.GetAttributes(destination) & ~FileAttributes.ReadOnly);
            }
            return result;
        }

        public void DeleteAndAssert()
        {
            Assert.IsTrue(IsStrictDescendant(System.IO.Path.GetTempPath(), Path));
            Directory.Delete(Path, recursive: true);
            Assert.IsFalse(Directory.Exists(Path));
            _deleted = true;
        }

        public void Dispose()
        {
            if (!_deleted && Directory.Exists(Path))
            {
                Assert.IsTrue(IsStrictDescendant(System.IO.Path.GetTempPath(), Path));
                Directory.Delete(Path, recursive: true);
            }
        }
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
