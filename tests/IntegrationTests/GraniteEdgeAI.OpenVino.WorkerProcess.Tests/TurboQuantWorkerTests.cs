using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[TestCategory("TurboQuantNative")]
[DoNotParallelize]
public sealed class TurboQuantWorkerTests
{
    private static readonly Guid SessionId = Guid.Parse("9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0");
    private static readonly Guid InspectionId = Guid.Parse("8c75d530-1e7e-4e24-9e40-b6d1ab2c06b1");
    private static readonly Guid TurnId = Guid.Parse("7b64c420-0d6d-4d13-8d2f-a5c09a1b05a2");

    [TestMethod]
    public void TurboQuantHandshakeBindsExactSourceAndRuntimeClosure()
    {
        OpenVinoBuildEvidence evidence = BuildEvidence();
        HelloEvent hello = new(OpenVinoProtocol.TurboQuantProtocolId, evidence);

        byte[] json = OpenVinoProtocolJson.Serialize(hello);
        HelloEvent parsed = Assert.IsInstanceOfType<HelloEvent>(
            OpenVinoProtocolJson.DeserializeEvent(json));

        Assert.AreEqual(OpenVinoProtocol.TurboQuantProtocolId, parsed.ProtocolId);
        Assert.IsNotNull(parsed.BuildEvidence.TurboQuantBuild);
        Assert.AreEqual(
            "f5f594dc0c9e5961785f0d17743486d52eac87e7",
            parsed.BuildEvidence.TurboQuantBuild.SourceCommit);
        Assert.AreEqual(
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            parsed.BuildEvidence.TurboQuantBuild.ImplementationCommit);
    }

    [TestMethod]
    public void TypedActivationEvidenceRequiresExecutedDispatchAndPackedRecords()
    {
        TurboQuantActivationEvent valid = Activation();
        valid.Validate();
        Assert.AreEqual(valid, OpenVinoProtocolJson.DeserializeEvent(
            OpenVinoProtocolJson.Serialize(valid)));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            (valid with { RuntimeDispatchCount = 0 }).Validate());
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            (valid with { EncodedRecordCount = 0 }).Validate());
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            (valid with { EncodedRecordCount = long.MaxValue }).Validate());
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            (valid with { ModelSdpaNodeCount = -1 }).Validate());
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            (valid with { ActualKeyCodec = TurboQuantCodec.ScalarU4 }).Validate());
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            (valid with { PackedBytesPerRecord = 31 }).Validate());
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            (valid with { EvidenceOrigin = TurboQuantEvidenceOrigin.ParsedConsoleText }).Validate());
    }

    [TestMethod]
    public void TurboQuantConversationRequiresOneActivationBeforeTurnCompletion()
    {
        OpenVinoConversationValidator validator = StartedTurboQuantConversation();
        validator.Accept(new PromptCommand(SessionId, TurnId, "hello", 2));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        Assert.ThrowsExactly<OpenVinoProtocolException>(() => validator.Accept(
            new TurnCompletedEvent(SessionId, TurnId, 1, 1, OpenVinoTurnDisposition.Completed)));

        validator = StartedTurboQuantConversation();
        validator.Accept(new PromptCommand(SessionId, TurnId, "hello", 2));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        validator.Accept(Activation());
        validator.Accept(new TurnCompletedEvent(
            SessionId, TurnId, 1, 1, OpenVinoTurnDisposition.Completed));
        Assert.IsFalse(validator.IsTerminal);
        Assert.ThrowsExactly<OpenVinoProtocolException>(() => validator.Accept(Activation()));
    }

    [TestMethod]
    public void OfficialConversationRejectsTurboQuantRuntimeAndActivation()
    {
        StartSessionCommand turboStart = Start(OpenVinoRuntimeOptions.Tbq4);
        OpenVinoConversationValidator validator = new();
        validator.Accept(new HelloEvent(
            OpenVinoProtocol.OfficialProtocolId,
            BuildEvidence() with { TurboQuantBuild = null }));
        Assert.ThrowsExactly<OpenVinoProtocolException>(() => validator.Accept(turboStart));
    }

    [TestMethod]
    public void WorkerAndActivationGateFilesExist()
    {
        string root = FindRepositoryRoot();
        foreach (string path in new[]
        {
            "workers/OpenVinoTurboQuant.Worker/CMakeLists.txt",
            "workers/OpenVinoTurboQuant.Worker/src/main.cpp",
            "workers/OpenVinoTurboQuant.Worker/src/session.cpp",
            "workers/OpenVinoTurboQuant.Worker/src/activation_evidence.cpp",
            "scripts/openvino/New-OpenVinoTurboQuantWorkerManifest.ps1",
            "scripts/openvino/Test-OpenVinoTurboQuantActivation.ps1",
            "scripts/openvino/Test-OpenVinoTurboQuantWorkerManifest.ps1",
            ".github/workflows/openvino-turboquant-ucl.yml",
        })
        {
            Assert.IsTrue(File.Exists(Path.Combine(root, path)), path);
        }
        string build = File.ReadAllText(Path.Combine(
            root,
            "scripts/openvino/Build-OpenVinoTurboQuantWorker.ps1"));
        StringAssert.Contains(build, "Test-PathOverlap");
        StringAssert.Contains(build, "$inputRoots");
        StringAssert.Contains(build, "$ownedRoots");
    }

    [TestMethod]
    public void UclWorkflowIsManualTrustedAndFailClosed()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), ".github", "workflows", "openvino-turboquant-ucl.yml"));
        StringAssert.Contains(source, "workflow_dispatch:");
        StringAssert.Contains(source, "environment: openvino-turboquant-ucl");
        StringAssert.Contains(source, "TURBOQUANT-UCL-approved");
        StringAssert.Contains(source, "openvino.turboquant/1");
        StringAssert.Contains(source, "Test-OpenVinoTurboQuantActivation.ps1");
        StringAssert.Contains(source, "-ExpectedPackageManifestSha256");
        StringAssert.Contains(source, "-ExpectedModelLength");
        StringAssert.Contains(source, "dotnet test --project");
        Assert.IsFalse(source.Contains("--no-restore", StringComparison.Ordinal));
        StringAssert.Contains(source, "steps.negative.outcome == 'success'");
        Assert.IsTrue(
            source.IndexOf("- name: Verify exact checked-out commit", StringComparison.Ordinal) <
            source.IndexOf("- name: Set up repository .NET SDK", StringComparison.Ordinal));
        StringAssert.Contains(source, "retention-days: 30");
        StringAssert.Contains(source, "if: ${{ always() }}");
        Assert.IsFalse(source.Contains("pull_request:", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("push:", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SealedWorkerStreamsTwoTurnsAndPublishesRuntimeActivation()
    {
        string stage = Environment.GetEnvironmentVariable("OPENVINO_TURBOQUANT_WORKER_STAGE")
            ?? throw new InvalidOperationException("OPENVINO_TURBOQUANT_WORKER_STAGE is required.");
        string root = FindRepositoryRoot();
        (string package, string packageDigest, string modelDigest, long modelLength) =
            SelectedPackage(root);
        string manifest = Path.Combine(stage, "worker-manifest.json");
        OpenVinoBuildEvidence build = new(
            "2026.5.0-22950-f5f594dc0c9",
            "2026.5.0.0-3409-6fbc103538d",
            "2026.5.0.0-737-824033c3061",
            Sha256(manifest),
            new TurboQuantBuildEvidence(
                "f5f594dc0c9e5961785f0d17743486d52eac87e7",
                "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
                Sha256(Path.Combine(stage, "patch-manifest.json")),
                Sha256(Path.Combine(stage, "turboquant-runtime.manifest.json"))));
        Dictionary<string, OpenVinoWorkerBinaryMachine> binaries = Directory
            .EnumerateFiles(stage, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                path => Path.GetRelativePath(stage, path).Replace('\\', '/'),
                _ => OpenVinoWorkerBinaryMachine.Amd64,
                StringComparer.Ordinal);
        OpenVinoWorkerInstallation installation = new(
            stage,
            "OpenVinoTurboQuant.Worker.exe",
            OpenVinoProtocol.TurboQuantProtocolId,
            build,
            binaries);
        OpenVinoWorkerClient client = new(OpenVinoWorkerClientOptions.CreateDefault(installation));

        await using OpenVinoConversation conversation = await client.StartSessionAsync(
            new StartSessionCommand(
                SessionId,
                InspectionId,
                package,
                packageDigest,
                modelDigest,
                modelLength,
                new OpenVinoDeviceRequest("CPU"),
                new OpenVinoGenerationLimits(64, 2),
                OpenVinoRuntimeOptions.Tbq4),
            CancellationToken.None).ConfigureAwait(false);
        long streamedFragments = 0;
        TurboQuantActivationEvent? finalActivation = null;
        for (int turn = 0; turn < 2; ++turn)
        {
            List<TokenEvent> tokens = [];
            TurnCompletedEvent completed = Assert.IsInstanceOfType<TurnCompletedEvent>(
                await conversation.PromptAsync(
                    new PromptCommand(conversation.SessionId, Guid.NewGuid(), "hello", 2),
                    new InlineProgress<TokenEvent>(tokens.Add),
                    CancellationToken.None).ConfigureAwait(false));
            Assert.AreEqual(OpenVinoTurnDisposition.Completed, completed.Disposition);
            Assert.IsGreaterThan(0, tokens.Count);
            streamedFragments += tokens.Count;
            Assert.IsNotNull(conversation.LatestTurboQuantActivation);
            TurboQuantActivationEvent activation = conversation.LatestTurboQuantActivation;
            Assert.IsGreaterThan(0L, activation.RuntimeDispatchCount);
            Assert.IsGreaterThan(0L, activation.EncodedRecordCount);
            Assert.IsTrue(activation.ForcedScalarNegative);
            finalActivation = activation;
        }
        await conversation.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        await Task.Delay(100).ConfigureAwait(false);
        Assert.AreEqual(0, Process.GetProcessesByName("OpenVinoTurboQuant.Worker").Length);

        string? evidencePath = Environment.GetEnvironmentVariable(
            "OPENVINO_TURBOQUANT_EVIDENCE_PATH");
        if (!string.IsNullOrWhiteSpace(evidencePath))
        {
            Assert.IsNotNull(finalActivation);
            string commit = Environment.GetEnvironmentVariable("OPENVINO_EVIDENCE_COMMIT")
                ?? throw new InvalidOperationException("OPENVINO_EVIDENCE_COMMIT is required.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidencePath))!);
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                commitSha = commit,
                protocolId = OpenVinoProtocol.TurboQuantProtocolId,
                workerManifestDigest = build.WorkerManifestDigest,
                runtimeManifestDigest = build.TurboQuantBuild!.RuntimeManifestDigest,
                sourceCommit = build.TurboQuantBuild.SourceCommit,
                implementationCommit = build.TurboQuantBuild.ImplementationCommit,
                packageManifestSha256 = packageDigest,
                modelSha256 = modelDigest,
                modelLength,
                requestedDevice = "CPU",
                actualExecutionDevices = conversation.StartupEvidence.ActualExecutionDevices,
                requestedKeyCodec = "tbq4",
                requestedValueCodec = "tbq4",
                actualKeyCodec = "tbq4",
                actualValueCodec = "tbq4",
                attentionPath = "sdpa",
                headDimension = finalActivation.HeadDimension,
                runtimeDispatchCount = finalActivation.RuntimeDispatchCount,
                encodedRecordCount = finalActivation.EncodedRecordCount,
                modelSdpaNodeCount = finalActivation.ModelSdpaNodeCount,
                packedBytesPerRecord = finalActivation.PackedBytesPerRecord,
                fullPrecisionBytesPerRecord = finalActivation.FullPrecisionBytesPerRecord,
                packedCacheBytes = finalActivation.PackedCacheBytes,
                fullPrecisionCacheBytes = finalActivation.FullPrecisionCacheBytes,
                evidenceOrigin = "openVinoProfilingApi",
                forcedScalarNegative = finalActivation.ForcedScalarNegative,
                streamedFragmentCount = streamedFragments,
                completedTurnCount = 2,
                cleanupVerified = true
            });
            File.WriteAllBytes(evidencePath, payload);
        }
    }

    [TestMethod]
    [DataRow("tbq4")]
    [DataRow("tbq3")]
    [Timeout(240_000)]
    public async Task SealedWorkerExecutesEachAdmittedTurboQuantCodec(string codec)
    {
        string stage = Environment.GetEnvironmentVariable("OPENVINO_TURBOQUANT_WORKER_STAGE")
            ?? throw new InvalidOperationException("OPENVINO_TURBOQUANT_WORKER_STAGE is required.");
        (string package, string packageDigest, string modelDigest, long modelLength) =
            SelectedPackage(FindRepositoryRoot());
        OpenVinoRuntimeOptions runtime = codec == "tbq3"
            ? OpenVinoRuntimeOptions.Tbq3
            : OpenVinoRuntimeOptions.Tbq4;
        TurboQuantCodec expected = codec == "tbq3"
            ? TurboQuantCodec.Tbq3
            : TurboQuantCodec.Tbq4;

        await using OpenVinoConversation conversation = await CreateClient(stage)
            .StartSessionAsync(
                new StartSessionCommand(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    package,
                    packageDigest,
                    modelDigest,
                    modelLength,
                    new OpenVinoDeviceRequest("CPU"),
                    new OpenVinoGenerationLimits(64, 2),
                    runtime),
                CancellationToken.None)
            .ConfigureAwait(false);
        List<TokenEvent> tokens = [];
        TurnCompletedEvent completed = Assert.IsInstanceOfType<TurnCompletedEvent>(
            await conversation.PromptAsync(
                new PromptCommand(conversation.SessionId, Guid.NewGuid(), "hello", 2),
                new InlineProgress<TokenEvent>(tokens.Add),
                CancellationToken.None).ConfigureAwait(false));

        Assert.AreEqual(OpenVinoTurnDisposition.Completed, completed.Disposition);
        Assert.IsGreaterThan(0, tokens.Count);
        Assert.AreEqual(codec, conversation.StartupEvidence.ActualKvCachePrecision);
        Assert.IsNotNull(conversation.LatestTurboQuantActivation);
        TurboQuantActivationEvent activation = conversation.LatestTurboQuantActivation;
        Assert.AreEqual(expected, activation.ActualKeyCodec);
        Assert.AreEqual(expected, activation.ActualValueCodec);
        Assert.IsGreaterThan(0L, activation.RuntimeDispatchCount);
        Assert.IsGreaterThan(0L, activation.ModelSdpaNodeCount);
        await conversation.CloseAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task SealedWorkerCancellationIsBoundedAndLeavesNoProcess()
    {
        string stage = Environment.GetEnvironmentVariable("OPENVINO_TURBOQUANT_WORKER_STAGE")
            ?? throw new InvalidOperationException("OPENVINO_TURBOQUANT_WORKER_STAGE is required.");
        (string package, string packageDigest, string modelDigest, long modelLength) =
            SelectedPackage(FindRepositoryRoot());
        OpenVinoWorkerClient client = CreateClient(stage);
        List<TokenEvent> tokens = [];
        await using (OpenVinoConversation conversation = await client.StartSessionAsync(
            new StartSessionCommand(
                Guid.NewGuid(), Guid.NewGuid(), package, packageDigest, modelDigest, modelLength,
                new OpenVinoDeviceRequest("CPU"),
                new OpenVinoGenerationLimits(64, 2),
                OpenVinoRuntimeOptions.Tbq4),
            CancellationToken.None).ConfigureAwait(false))
        {
            Task<IOpenVinoEvent> active = conversation.PromptAsync(
                new PromptCommand(conversation.SessionId, Guid.NewGuid(), "hello", 2),
                new InlineProgress<TokenEvent>(tokens.Add),
                CancellationToken.None);
            await Task.Delay(50).ConfigureAwait(false);
            OpenVinoWorkerClientException cancellation =
                await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(async () =>
                {
                    await conversation.CancelAsync(CancellationToken.None).ConfigureAwait(false);
                    await active.ConfigureAwait(false);
                }).ConfigureAwait(false);
            Assert.AreEqual(OpenVinoSupportCode.OperationCancelled, cancellation.SupportCode);
        }
        await Task.Delay(100).ConfigureAwait(false);
        Assert.AreEqual(0, Process.GetProcessesByName("OpenVinoTurboQuant.Worker").Length);
    }

    private static OpenVinoConversationValidator StartedTurboQuantConversation()
    {
        OpenVinoBuildEvidence build = BuildEvidence();
        OpenVinoConversationValidator validator = new();
        validator.Accept(new HelloEvent(OpenVinoProtocol.TurboQuantProtocolId, build));
        validator.Accept(Start(OpenVinoRuntimeOptions.Tbq4));
        validator.Accept(new SessionStartedEvent(
            SessionId,
            "CPU",
            ["CPU"],
            "tbq4",
            "tbq4",
            OpenVinoProtocol.TurboQuantProtocolId,
            build));
        return validator;
    }

    private static StartSessionCommand Start(OpenVinoRuntimeOptions runtime) => new(
        SessionId,
        InspectionId,
        @"C:\models\granite",
        new string('a', 64),
        new string('b', 64),
        100,
        new OpenVinoDeviceRequest("CPU"),
        new OpenVinoGenerationLimits(128, 2),
        runtime);

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.5.0-22950-f5f594dc0c9",
        "2026.5.0.0-3409-6fbc103538d",
        "2026.5.0.0-737-824033c3061",
        new string('1', 64),
        new TurboQuantBuildEvidence(
            "f5f594dc0c9e5961785f0d17743486d52eac87e7",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            new string('2', 64),
            new string('3', 64)));

    private static TurboQuantActivationEvent Activation() => new(
        SessionId,
        TurnId,
        TurboQuantCodec.Tbq4,
        TurboQuantCodec.Tbq4,
        TurboQuantCodec.Tbq4,
        TurboQuantCodec.Tbq4,
        TurboQuantAttentionPath.Sdpa,
        64,
        2,
        22,
        40,
        32,
        128,
        704,
        2816,
        TurboQuantEvidenceOrigin.OpenVinoProfilingApi,
        true);

    private static OpenVinoWorkerClient CreateClient(string stage)
    {
        OpenVinoBuildEvidence build = new(
            "2026.5.0-22950-f5f594dc0c9",
            "2026.5.0.0-3409-6fbc103538d",
            "2026.5.0.0-737-824033c3061",
            Sha256(Path.Combine(stage, "worker-manifest.json")),
            new TurboQuantBuildEvidence(
                "f5f594dc0c9e5961785f0d17743486d52eac87e7",
                "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
                Sha256(Path.Combine(stage, "patch-manifest.json")),
                Sha256(Path.Combine(stage, "turboquant-runtime.manifest.json"))));
        Dictionary<string, OpenVinoWorkerBinaryMachine> binaries = Directory
            .EnumerateFiles(stage, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                path => Path.GetRelativePath(stage, path).Replace('\\', '/'),
                _ => OpenVinoWorkerBinaryMachine.Amd64,
                StringComparer.Ordinal);
        return new OpenVinoWorkerClient(OpenVinoWorkerClientOptions.CreateDefault(
            new OpenVinoWorkerInstallation(
                stage,
                "OpenVinoTurboQuant.Worker.exe",
                OpenVinoProtocol.TurboQuantProtocolId,
                build,
                binaries)));
    }

    private static (string Package, string PackageDigest, string ModelDigest, long ModelLength)
        SelectedPackage(string root)
    {
        string package = Environment.GetEnvironmentVariable("OPENVINO_TURBOQUANT_PACKAGE_ROOT") ??
            Path.Combine(root, "tests", "TestFixtures", "OpenVINO", "GenAI", "TinySyntheticV1", "package");
        string packageDigest = Environment.GetEnvironmentVariable(
            "OPENVINO_TURBOQUANT_PACKAGE_MANIFEST_SHA256") ?? OfficialCpuFixtureTests.PackageDigest;
        string modelDigest = Environment.GetEnvironmentVariable(
            "OPENVINO_TURBOQUANT_MODEL_SHA256") ?? OfficialCpuFixtureTests.ModelDigest;
        long modelLength = long.TryParse(
            Environment.GetEnvironmentVariable("OPENVINO_TURBOQUANT_MODEL_LENGTH"),
            out long controlledLength) ? controlledLength : OfficialCpuFixtureTests.ModelLength;
        return (package, packageDigest, modelDigest, modelLength);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string Sha256(string path) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
