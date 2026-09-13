using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[TestCategory("OpenVinoRoute")]
[TestCategory("OfficialNative")]
[TestCategory("ManualRealModel")]
[DoNotParallelize]
public sealed class OpenVinoRealExperimentTests
{
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly ExperimentPrompt[] Prompts =
    [
        new("literal", "Reply with exactly: GRANITE_OK", "granite_ok"),
        new("arithmetic", "What is 17 + 25? Reply with only the number.", "42"),
        new("knowledge", "Name the capital of France in one word.", "paris"),
        new(
            "explanation",
            "In one short sentence, explain why local inference can improve privacy.",
            null)
    ];

    [TestMethod]
    [Timeout(3_600_000)]
    public Task ConfiguredOptimizedPackageProducesOfficialRuntimeEvidence() =>
        RunExperimentAsync(turboQuant: false);

    [TestMethod]
    [Timeout(3_600_000)]
    public Task ConfiguredOptimizedPackageProducesTurboQuantRuntimeEvidence() =>
        RunExperimentAsync(turboQuant: true);

    [TestMethod]
    [Timeout(14_400_000)]
    public async Task ConfiguredPackageCapturesIndependentPublishedQualityPrompts()
    {
        AppDomain.CurrentDomain.FirstChanceException += TraceQualityProtocolFailure;
        try
        {
        string suitePath = Environment.GetEnvironmentVariable("GRANITE_OPENVINO_QUALITY_SUITE") ?? "";
        if (!File.Exists(suitePath))
        {
            Assert.Inconclusive("GRANITE_OPENVINO_QUALITY_SUITE must name the retained 48-prompt JSON suite.");
        }
        ExperimentPrompt[] suite = JsonSerializer.Deserialize<ExperimentPrompt[]>(File.ReadAllText(suitePath))!;
        Assert.HasCount(48, suite);
        Assert.HasCount(48, suite.Select(prompt => prompt.Id).Distinct().ToArray());
        string package = RequireDirectory("GRANITE_OPENVINO_REAL_OPTIMIZED_MODEL");
        bool turbo = Environment.GetEnvironmentVariable("GRANITE_OPENVINO_QUALITY_TURBO") == "1";
        string stage = RequireDirectory(turbo ? "OPENVINO_TURBOQUANT_WORKER_STAGE" : "OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string output = RequireOutputDirectory("GRANITE_OPENVINO_EVIDENCE_OUTPUT");
        OpenVinoStaticPackageEvidence packageEvidence = new OpenVinoStaticPackageInspector().Inspect(package).Evidence!;
        Assert.IsNotNull(packageEvidence);
        List<string> unhealthy = [];
        foreach (OpenVinoRuntimeOptions runtime in turbo
                     ? new[] { OpenVinoRuntimeOptions.Tbq3, OpenVinoRuntimeOptions.Tbq4 }
                     : RequestedRuntimes())
        {
            foreach (ExperimentPrompt prompt in suite)
            {
                // a fresh worker session per prompt prevents earlier answers contaminating the rubric
                ConfigurationObservation observation;
                try
                {
                    observation = await RunConfigurationAsync(
                        stage, package, packageEvidence, runtime, CancellationToken.None, [prompt], 128);
                }
                catch (OpenVinoWorkerClientException error)
                {
                    throw new AssertFailedException($"Quality capture {runtime.KvCachePrecision}/{prompt.Id}: {error.SupportCode}; stderr={error.RetainedStandardError}; inner={error.InnerException}", error);
                }
                string destination = Path.Combine(output, runtime.KvCachePrecision + "-" + prompt.Id + ".json");
                Assert.IsFalse(File.Exists(destination), "Use a fresh evidence directory; retained observations must not be overwritten.");
                File.WriteAllText(destination, JsonSerializer.Serialize(new
                {
                    SuiteSha256 = FileDigest(suitePath),
                    ModelSha256 = packageEvidence.ModelSha256,
                    PackageManifestDigest = packageEvidence.PackageManifestDigest,
                    CapturedUtc = DateTimeOffset.UtcNow,
                    Observation = observation
                }, EvidenceJsonOptions));
                Console.WriteLine($"Captured {runtime.KvCachePrecision}/{prompt.Id}: healthy={observation.OutputHealthy}");
                if (!observation.OutputHealthy) unhealthy.Add(destination);
            }
        }
        Assert.IsEmpty(unhealthy, "Unhealthy outputs retained: " + string.Join(", ", unhealthy));
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= TraceQualityProtocolFailure;
        }
    }

    private static void TraceQualityProtocolFailure(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs args)
    {
        if (args.Exception is OpenVinoProtocolException)
            Console.WriteLine("Quality protocol diagnostic: " + args.Exception.Message);
    }

    private static async Task RunExperimentAsync(bool turboQuant)
    {
        string package = RequireDirectory("GRANITE_OPENVINO_REAL_OPTIMIZED_MODEL");
        string workerStage = RequireDirectory(turboQuant
            ? "OPENVINO_TURBOQUANT_WORKER_STAGE"
            : "OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string evidenceRoot = RequireOutputDirectory("GRANITE_OPENVINO_EVIDENCE_OUTPUT");
        OpenVinoStaticPackageEvidence packageEvidence =
            new OpenVinoStaticPackageInspector().Inspect(package).Evidence
            ?? throw new AssertFailedException(
                "The configured optimized package was not statically inspectable.");
        string runId = DateTimeOffset.UtcNow.ToString(
            "yyyyMMddTHHmmssfffZ",
            System.Globalization.CultureInfo.InvariantCulture);
        string runDirectory = Path.Combine(evidenceRoot, runId);
        Directory.CreateDirectory(runDirectory);

        List<ConfigurationObservation> configurations = [];
        OpenVinoRuntimeOptions[] runtimes = turboQuant
            ? [OpenVinoRuntimeOptions.Tbq3, OpenVinoRuntimeOptions.Tbq4]
            : RequestedRuntimes();
        foreach (OpenVinoRuntimeOptions runtime in runtimes)
        {
            configurations.Add(await RunConfigurationAsync(
                workerStage,
                package,
                packageEvidence,
                runtime,
                CancellationToken.None));
        }

        ExperimentEvidence evidence = new(
            SchemaVersion: 1,
            RunId: runId,
            StartedUtc: DateTimeOffset.UtcNow,
            Host: new HostObservation(
                Environment.OSVersion.VersionString,
                Environment.ProcessorCount,
                Environment.Is64BitOperatingSystem,
                Environment.Is64BitProcess),
            Model: new ModelObservation(
                packageEvidence.PackageManifestDigest,
                packageEvidence.ModelSha256,
                packageEvidence.ModelLengthBytes,
                packageEvidence.WeightPrecision ?? packageEvidence.Precision),
            Configurations: configurations);
        string evidencePath = Path.Combine(runDirectory, "openvino-real-evidence.json");
        File.WriteAllText(
            evidencePath,
            JsonSerializer.Serialize(evidence, EvidenceJsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Assert.IsNotEmpty(configurations,
            "No cache configurations were requested. Evidence: " + evidencePath);
        Assert.IsTrue(configurations.All(static item => string.Equals(
                item.RequestedKvCachePrecision, item.ActualKvCachePrecision,
                StringComparison.Ordinal)),
            "At least one runtime cache did not match the request. Evidence: " + evidencePath);
        Assert.IsTrue(configurations.All(static item =>
                item.ActualExecutionDevices.Count > 0 &&
                item.ActualExecutionDevices.All(static device =>
                    string.Equals(device, "CPU", StringComparison.Ordinal))),
            "At least one configuration did not execute on CPU. Evidence: " + evidencePath);
        Assert.IsTrue(configurations.All(static item => item.OutputHealthy),
            "At least one configuration produced unhealthy output. Evidence: " +
            evidencePath);
        Assert.IsTrue(configurations.All(static item => item.SanityChecksPassed >= 3),
            "At least one configuration failed a deterministic sanity prompt. Evidence: " +
            evidencePath);
    }

    private static async Task<ConfigurationObservation> RunConfigurationAsync(
        string workerStage,
        string package,
        OpenVinoStaticPackageEvidence packageEvidence,
        OpenVinoRuntimeOptions runtime,
        CancellationToken cancellationToken,
        ExperimentPrompt[]? prompts = null,
        int maxNewTokens = 48)
    {
        bool turboQuant = runtime.KvCachePrecision is "tbq3" or "tbq4";
        OpenVinoWorkerClient client = turboQuant
            ? new OpenVinoWorkerClient(OpenVinoWorkerClientOptions.CreateDefault(
                OpenVinoTurboQuantWorkerAuthority.CreateInstallation(
                    workerStage,
                    FileDigest(Path.Combine(workerStage, "worker-manifest.json")),
                    FileDigest(Path.Combine(workerStage, "patch-manifest.json")),
                    FileDigest(Path.Combine(workerStage, "turboquant-runtime.manifest.json")))))
            : OfficialCpuFixtureTests.CreateClient(workerStage);
        string workerProcessName = turboQuant
            ? "OpenVinoTurboQuant.Worker" : "OpenVinoOfficial.Worker";
        HashSet<int> baselineWorkerIds = Process.GetProcessesByName(
                workerProcessName)
            .Select(static process => process.Id)
            .ToHashSet();
        using CancellationTokenSource monitorCancellation = new();
        WorkerMemoryMonitor memory = new(baselineWorkerIds, workerProcessName);
        Task monitor = memory.RunAsync(monitorCancellation.Token);
        Stopwatch sessionTimer = Stopwatch.StartNew();
        Guid sessionId = Guid.NewGuid();
        try
        {
            OpenVinoConversation conversation;
            try
            {
                conversation = await client.StartSessionAsync(
                    new StartSessionCommand(
                        sessionId,
                        Guid.NewGuid(),
                        package,
                        packageEvidence.PackageManifestDigest,
                        packageEvidence.ModelSha256,
                        packageEvidence.ModelLengthBytes,
                        new OpenVinoDeviceRequest("CPU"),
                        new OpenVinoGenerationLimits(4_096, Math.Max(64, maxNewTokens)),
                        runtime),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OpenVinoWorkerClientException error)
            {
                throw new AssertFailedException(
                    $"Real OpenVINO session failed with {error.SupportCode}; " +
                    $"stderr='{error.RetainedStandardError}'.",
                    error);
            }
            await using (conversation.ConfigureAwait(false))
            {
            sessionTimer.Stop();

            List<PromptObservation> promptObservations = [];
            foreach (ExperimentPrompt prompt in prompts ?? Prompts)
            {
                promptObservations.Add(await RunPromptAsync(
                    conversation,
                    prompt,
                    cancellationToken, maxNewTokens));
            }

            await conversation.CloseAsync(cancellationToken).ConfigureAwait(false);
            SessionStartedEvent startup = conversation.StartupEvidence;
            bool outputHealthy = promptObservations.All(static item =>
                item.OutputHealthy &&
                string.Equals(item.TerminalDisposition, "completed", StringComparison.Ordinal));
            return new ConfigurationObservation(
                RequestedKvCachePrecision: runtime.KvCachePrecision,
                ActualKvCachePrecision: startup.ActualKvCachePrecision,
                RequestedDevice: startup.RequestedDevice,
                ActualExecutionDevices: startup.ActualExecutionDevices,
                ProtocolId: startup.ProtocolId,
                BuildEvidence: startup.BuildEvidence,
                SessionLoadMilliseconds: sessionTimer.Elapsed.TotalMilliseconds,
                PeakWorkerWorkingSetBytes: memory.PeakWorkingSetBytes,
                PeakWorkerPrivateBytes: memory.PeakPrivateBytes,
                OutputHealthy: outputHealthy,
                SanityChecksPassed: promptObservations.Count(static item =>
                    item.ExpectedFragmentMatched is true),
                Prompts: promptObservations);
            }
        }
        finally
        {
            monitorCancellation.Cancel();
            try
            {
                await monitor.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task<PromptObservation> RunPromptAsync(
        OpenVinoConversation conversation,
        ExperimentPrompt prompt,
        CancellationToken cancellationToken,
        int maxNewTokens = 48)
    {
        Stopwatch timer = Stopwatch.StartNew();
        long firstTokenTimestamp = -1;
        List<string> deltas = [];
        object deltaGate = new();
        Guid turnId = Guid.NewGuid();
        IOpenVinoEvent terminal = await conversation.PromptAsync(
            new PromptCommand(conversation.SessionId, turnId, prompt.Text, maxNewTokens),
            new InlineProgress<TokenEvent>(token =>
            {
                Interlocked.CompareExchange(
                    ref firstTokenTimestamp,
                    timer.ElapsedTicks,
                    -1);
                lock (deltaGate)
                {
                    deltas.Add(token.Text);
                }
            }),
            cancellationToken).ConfigureAwait(false);
        timer.Stop();

        string output;
        lock (deltaGate)
        {
            output = string.Concat(deltas);
        }
        TurnCompletedEvent? completed = terminal as TurnCompletedEvent;
        TurboQuantActivationEvent? activation = conversation.LatestTurboQuantActivation;
        if (conversation.StartupEvidence.ActualKvCachePrecision is "tbq3" or "tbq4")
        {
            Assert.IsNotNull(activation);
            Assert.AreEqual(turnId, activation.TurnId);
            Assert.IsGreaterThan(0L, activation.RuntimeDispatchCount);
            Assert.IsGreaterThan(0L, activation.ModelSdpaNodeCount);
        }
        bool outputHealthy = !string.IsNullOrWhiteSpace(output) &&
            !output.Contains('\0', StringComparison.Ordinal) &&
            !output.Contains('\uFFFD', StringComparison.Ordinal) &&
            completed is not null &&
            completed.GeneratedTokenCount > 0;
        bool? expectedMatched = prompt.ExpectedFragment is null
            ? null
            : output.Contains(prompt.ExpectedFragment, StringComparison.OrdinalIgnoreCase);
        double elapsedSeconds = timer.Elapsed.TotalSeconds;
        return new PromptObservation(
            prompt.Id,
            prompt.Text,
            prompt.ExpectedFragment,
            expectedMatched,
            output,
            Sha256(output),
            outputHealthy,
            terminal switch
            {
                TurnCompletedEvent value => value.Disposition.ToString().ToLowerInvariant(),
                TurnFailedEvent value => "failed:" + value.SupportCode.ToProtocolValue(),
                _ => terminal.GetType().Name
            },
            completed?.PromptTokenCount,
            completed?.GeneratedTokenCount,
            firstTokenTimestamp < 0
                ? null
                : TimeSpan.FromSeconds(
                    (double)firstTokenTimestamp / Stopwatch.Frequency).TotalMilliseconds,
            timer.Elapsed.TotalMilliseconds,
            completed is null || elapsedSeconds <= 0
                ? null
                : completed.GeneratedTokenCount / elapsedSeconds,
            activation);
    }

    private static OpenVinoRuntimeOptions[] RequestedRuntimes()
    {
        string value = Environment.GetEnvironmentVariable(
            "GRANITE_OPENVINO_EXPERIMENT_KV") ?? "u8,u4";
        return value.Split(',', StringSplitOptions.TrimEntries |
                                  StringSplitOptions.RemoveEmptyEntries)
            .Select(static item => item.ToLowerInvariant() switch
            {
                "released-default" => OpenVinoRuntimeOptions.ReleasedDefault,
                "u8" => OpenVinoRuntimeOptions.U8,
                "u4" => OpenVinoRuntimeOptions.U4,
                _ => throw new AssertFailedException(
                    "GRANITE_OPENVINO_EXPERIMENT_KV supports only released-default, u8 and u4.")
            })
            .DistinctBy(static item => item.KvCachePrecision)
            .ToArray();
    }

    private static string RequireDirectory(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
        {
            Assert.Inconclusive(variable + " must name an existing directory.");
        }
        return Path.GetFullPath(value!);
    }

    private static string RequireOutputDirectory(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Inconclusive(variable + " is required.");
        }
        string path = Path.GetFullPath(value!);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Sha256(string value) => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))
        .ToLowerInvariant();

    private static string FileDigest(string path) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class WorkerMemoryMonitor(
        HashSet<int> baselineWorkerIds, string workerProcessName)
    {
        private long peakWorkingSetBytes;
        private long peakPrivateBytes;

        internal long PeakWorkingSetBytes => Interlocked.Read(ref peakWorkingSetBytes);
        internal long PeakPrivateBytes => Interlocked.Read(ref peakPrivateBytes);

        internal async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (Process process in Process.GetProcessesByName(
                             workerProcessName))
                {
                    using (process)
                    {
                        if (baselineWorkerIds.Contains(process.Id))
                        {
                            continue;
                        }
                        try
                        {
                            process.Refresh();
                            UpdateMaximum(ref peakWorkingSetBytes, process.WorkingSet64);
                            UpdateMaximum(ref peakPrivateBytes, process.PrivateMemorySize64);
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                }
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        private static void UpdateMaximum(ref long target, long candidate)
        {
            long current = Interlocked.Read(ref target);
            while (candidate > current)
            {
                long observed = Interlocked.CompareExchange(
                    ref target,
                    candidate,
                    current);
                if (observed == current)
                {
                    return;
                }
                current = observed;
            }
        }
    }

    private sealed record ExperimentPrompt(
        string Id,
        string Text,
        string? ExpectedFragment);

    private sealed record ExperimentEvidence(
        int SchemaVersion,
        string RunId,
        DateTimeOffset StartedUtc,
        HostObservation Host,
        ModelObservation Model,
        IReadOnlyList<ConfigurationObservation> Configurations);

    private sealed record HostObservation(
        string OperatingSystem,
        int LogicalProcessorCount,
        bool Is64BitOperatingSystem,
        bool Is64BitProcess);

    private sealed record ModelObservation(
        string PackageManifestSha256,
        string ModelSha256,
        long ModelLengthBytes,
        string WeightPrecision);

    private sealed record ConfigurationObservation(
        string RequestedKvCachePrecision,
        string ActualKvCachePrecision,
        string RequestedDevice,
        IReadOnlyList<string> ActualExecutionDevices,
        string ProtocolId,
        OpenVinoBuildEvidence BuildEvidence,
        double SessionLoadMilliseconds,
        long PeakWorkerWorkingSetBytes,
        long PeakWorkerPrivateBytes,
        bool OutputHealthy,
        int SanityChecksPassed,
        IReadOnlyList<PromptObservation> Prompts);

    private sealed record PromptObservation(
        string PromptId,
        string Prompt,
        string? ExpectedFragment,
        bool? ExpectedFragmentMatched,
        string Output,
        string OutputSha256,
        bool OutputHealthy,
        string TerminalDisposition,
        long? PromptTokenCount,
        long? GeneratedTokenCount,
        double? TimeToFirstTokenMilliseconds,
        double TotalLatencyMilliseconds,
        double? GeneratedTokensPerSecond,
        TurboQuantActivationEvent? TurboQuantActivation);
}
