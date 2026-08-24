using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using System.Reflection;
using System.Text;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

[TestClass]
[DoNotParallelize]
public sealed class HardwareInspectionGate9AcceptanceHostTests
{
    [TestMethod]
    public void TryParseActivation_AcceptsOnlyExactGate9Command()
    {
        string token = new('a', 32);
        string[] commandLine =
        [
            @"C:\Program Files\WindowsApps\GraniteEdgeAI.UnitTests.exe",
            "--hardware-inspection-gate9-acceptance",
            "--result-token",
            token,
        ];

        bool parsed = HardwareInspectionGate9AcceptanceHost.TryParseActivation(
            commandLine,
            out string actualToken);

        Assert.IsTrue(parsed);
        Assert.AreEqual(token, actualToken);
        Assert.IsFalse(HardwareInspectionProcessAcceptanceHost.TryParseActivation(
            commandLine,
            out _));
    }

    [TestMethod]
    [DataRow("--hardware-inspection-process-acceptance", "--result-token", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [DataRow("--hardware-inspection-gate9-acceptance", "--result", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [DataRow("--hardware-inspection-gate9-acceptance", "--result-token", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [DataRow("--hardware-inspection-gate9-acceptance", "--result-token", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void TryParseActivation_RejectsNonCanonicalCommand(
        string command,
        string resultSwitch,
        string token)
    {
        string[] commandLine = ["app.exe", command, resultSwitch, token];

        Assert.IsFalse(HardwareInspectionGate9AcceptanceHost.TryParseActivation(
            commandLine,
            out string actualToken));
        Assert.AreEqual(string.Empty, actualToken);
    }

    [TestMethod]
    public void TryParseActivation_RejectsMissingOrAdditionalArguments()
    {
        string token = new('a', 32);

        Assert.IsFalse(HardwareInspectionGate9AcceptanceHost.TryParseActivation(
            ["app.exe", "--hardware-inspection-gate9-acceptance", "--result-token"],
            out _));
        Assert.IsFalse(HardwareInspectionGate9AcceptanceHost.TryParseActivation(
            [
                "app.exe",
                "--hardware-inspection-gate9-acceptance",
                "--result-token",
                token,
                "unexpected",
            ],
            out _));
    }

    [TestMethod]
    public async Task RunAsync_RejectsInvalidResultTokenBeforeInvokingService()
    {
        bool invoked = false;
        var service = new Gate9ScriptedHardwareInspectionService((_, _, _) =>
        {
            invoked = true;
            throw new InvalidOperationException();
        });

        int exitCode = await HardwareInspectionGate9AcceptanceHost.RunAsync(
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            service,
            () => true);

        Assert.AreEqual(64, exitCode);
        Assert.IsFalse(invoked);
    }

    [TestMethod]
    public async Task RunAsync_PublishesExactCompletedContractWithoutSnapshot()
    {
        Gate9RunObservation observation = await RunHostAsync((id, progress, cancellationToken) =>
            CompleteAsync(id, progress, cancellationToken, HardwareInspectionOutcome.Completed));
        string json = observation.Json!;

        Assert.AreEqual(0, observation.ExitCode);
        Assert.AreEqual(
            "{\"schema\":\"granite.hardware-inspection.gate9-production-run/v1\"," +
            "\"packageIdentityPresent\":true,\"outcome\":\"Completed\"," +
            "\"stageCount\":7,\"handoffPresent\":true," +
            "\"manifestFieldCount\":19,\"diagnostics\":[]}\n",
            json);
        Assert.IsLessThan(4096, Encoding.UTF8.GetByteCount(json));
        Assert.DoesNotContain("Snapshot", json);
        Assert.DoesNotContain("Intel", json);
    }

    [TestMethod]
    public async Task RunAsync_AcceptsCompletedWithWarnings()
    {
        Gate9RunObservation observation = await RunHostAsync((id, progress, cancellationToken) =>
            CompleteAsync(
                id,
                progress,
                cancellationToken,
                HardwareInspectionOutcome.CompletedWithWarnings));
        string json = observation.Json!;

        Assert.AreEqual(0, observation.ExitCode);
        Assert.Contains("\"outcome\":\"CompletedWithWarnings\"", json);
        Assert.Contains("\"diagnostics\":[]", json);
    }

    [TestMethod]
    public async Task RunAsync_PublishesOnlySafeDiagnosticForFailedOutcome()
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            await ReportStagesAsync(id, progress, cancellationToken);
            return HardwareInspectionRunResult.CreateFailed(
                id,
                HardwareInspectionFailureKind.TransientOperation,
                "HI-OPERATION-FAILED");
        });

        Assert.AreEqual(1, observation.ExitCode);
        Assert.AreEqual(
            "{\"schema\":\"granite.hardware-inspection.gate9-production-run/v1\"," +
            "\"packageIdentityPresent\":true,\"outcome\":\"Failed\"," +
            "\"stageCount\":7,\"handoffPresent\":false," +
            "\"manifestFieldCount\":0,\"diagnostics\":[\"HI-OPERATION-FAILED\"]}\n",
            observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_PublishesFailureAfterValidProgressPrefix()
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            progress.Report(new HardwareInspectionRunProgress(
                id,
                1,
                HardwareInspectionRunStage.StartingHardwareInspection));
            await Task.Delay(50, cancellationToken);
            return HardwareInspectionRunResult.CreateFailed(
                id,
                HardwareInspectionFailureKind.TransientOperation,
                "HI-TOOL-INTEGRITY");
        });

        Assert.AreEqual(1, observation.ExitCode);
        Assert.AreEqual(
            "{\"schema\":\"granite.hardware-inspection.gate9-production-run/v1\"," +
            "\"packageIdentityPresent\":true,\"outcome\":\"Failed\"," +
            "\"stageCount\":1,\"handoffPresent\":false," +
            "\"manifestFieldCount\":0,\"diagnostics\":[\"HI-TOOL-INTEGRITY\"]}\n",
            observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_PublishesSanitizedCancelledOutcome()
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            await ReportStagesAsync(id, progress, cancellationToken);
            return HardwareInspectionRunResult.CreateCancelled(id);
        });
        string json = observation.Json!;

        Assert.AreEqual(1, observation.ExitCode);
        Assert.Contains("\"outcome\":\"Cancelled\"", json);
        Assert.Contains("\"handoffPresent\":false", json);
        Assert.Contains("\"manifestFieldCount\":0", json);
        Assert.Contains("\"diagnostics\":[]", json);
    }

    [TestMethod]
    public async Task RunAsync_PublishesCancellationAfterValidProgressPrefix()
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            progress.Report(new HardwareInspectionRunProgress(
                id,
                1,
                HardwareInspectionRunStage.StartingHardwareInspection));
            await Task.Delay(50, cancellationToken);
            return HardwareInspectionRunResult.CreateCancelled(id);
        });

        Assert.AreEqual(1, observation.ExitCode);
        Assert.AreEqual(
            "{\"schema\":\"granite.hardware-inspection.gate9-production-run/v1\"," +
            "\"packageIdentityPresent\":true,\"outcome\":\"Cancelled\"," +
            "\"stageCount\":1,\"handoffPresent\":false," +
            "\"manifestFieldCount\":0,\"diagnostics\":[]}\n",
            observation.Json);
    }

    [TestMethod]
    [DataRow("wrong-identity")]
    [DataRow("duplicate")]
    [DataRow("out-of-order")]
    [DataRow("missing")]
    [DataRow("non-monotonic-sequence")]
    public async Task RunAsync_RejectsInvalidProgressContract(string mutation)
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            HardwareInspectionRunStage[] stages = Enum.GetValues<HardwareInspectionRunStage>();
            int count = mutation == "missing" ? stages.Length - 1 : stages.Length;
            for (int index = 0; index < count; index++)
            {
                HardwareInspectionRunStage stage = stages[index];
                long sequence = index + 1;
                Guid progressId = id;
                if (mutation == "wrong-identity" && index == 2)
                {
                    progressId = Guid.NewGuid();
                }
                else if (mutation == "duplicate" && index == 3)
                {
                    stage = stages[index - 1];
                }
                else if (mutation == "out-of-order" && index == 2)
                {
                    stage = stages[index + 1];
                }
                else if (mutation == "non-monotonic-sequence" && index == 4)
                {
                    sequence = index;
                }

                progress.Report(new HardwareInspectionRunProgress(progressId, sequence, stage));
            }

            await Task.Delay(50, cancellationToken);
            return CreateCompleted(id, HardwareInspectionOutcome.Completed, 19);
        });

        Assert.AreEqual(70, observation.ExitCode);
        Assert.IsNull(observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_RejectsWrongResultIdentity()
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            await ReportStagesAsync(id, progress, cancellationToken);
            return CreateCompleted(Guid.NewGuid(), HardwareInspectionOutcome.Completed, 19);
        });

        Assert.AreEqual(70, observation.ExitCode);
        Assert.IsNull(observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_RejectsMissingHandoff()
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            await ReportStagesAsync(id, progress, cancellationToken);
            HardwareSnapshot snapshot = CreateSnapshot(19);
            return CreateRawResult(
                id,
                HardwareInspectionOutcome.Completed,
                snapshot,
                failureKind: null,
                safeDiagnosticCode: null,
                handoff: null);
        });

        Assert.AreEqual(70, observation.ExitCode);
        Assert.IsNull(observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_RejectsWrongManifestFieldCount()
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            await ReportStagesAsync(id, progress, cancellationToken);
            return CreateCompleted(id, HardwareInspectionOutcome.Completed, 18);
        });

        Assert.AreEqual(70, observation.ExitCode);
        Assert.IsNull(observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_RejectsUnsafeDiagnostic()
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            await ReportStagesAsync(id, progress, cancellationToken);
            return CreateRawResult(
                id,
                HardwareInspectionOutcome.Failed,
                snapshot: null,
                HardwareInspectionFailureKind.TransientOperation,
                @"C:\private\hardware.txt",
                handoff: null);
        });

        Assert.AreEqual(70, observation.ExitCode);
        Assert.IsNull(observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_RejectsUndefinedOutcome()
    {
        Gate9RunObservation observation = await RunHostAsync(async (id, progress, cancellationToken) =>
        {
            await ReportStagesAsync(id, progress, cancellationToken);
            return CreateRawResult(
                id,
                (HardwareInspectionOutcome)99,
                snapshot: null,
                failureKind: null,
                safeDiagnosticCode: null,
                handoff: null);
        });

        Assert.AreEqual(70, observation.ExitCode);
        Assert.IsNull(observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_RejectsMissingPackageIdentity()
    {
        Gate9RunObservation observation = await RunHostAsync(
            (id, progress, cancellationToken) =>
                CompleteAsync(id, progress, cancellationToken, HardwareInspectionOutcome.Completed),
            packageIdentityPresent: false);

        Assert.AreEqual(70, observation.ExitCode);
        Assert.IsNull(observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_ContainsServiceExceptionAtHostBoundary()
    {
        Gate9RunObservation observation = await RunHostAsync(
            (_, _, _) => throw new InvalidOperationException("private host detail"));

        Assert.AreEqual(70, observation.ExitCode);
        Assert.IsNull(observation.Json);
    }

    [TestMethod]
    public async Task RunAsync_ContainsPublicationFailureAndCleansTemporaryFile()
    {
        string token = Guid.NewGuid().ToString("N");
        string resultPath = HardwareInspectionAcceptanceResultStore.GetResultPath(token);
        string resultRoot = Path.GetDirectoryName(resultPath)!;
        Directory.CreateDirectory(resultPath);

        try
        {
            var service = new Gate9ScriptedHardwareInspectionService(
                (id, progress, cancellationToken) => CompleteAsync(
                    id,
                    progress,
                    cancellationToken,
                    HardwareInspectionOutcome.Completed));

            int exitCode = await HardwareInspectionGate9AcceptanceHost.RunAsync(
                token,
                service,
                () => true);

            Assert.AreEqual(70, exitCode);
            Assert.IsEmpty(Directory.GetFiles(
                resultRoot,
                $".{token}.*.tmp",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            if (Directory.Exists(resultPath))
            {
                Directory.Delete(resultPath);
            }
        }
    }

    private static async Task<Gate9RunObservation> RunHostAsync(
        Func<Guid, IProgress<HardwareInspectionRunProgress>, CancellationToken,
            Task<HardwareInspectionRunResult>> run,
        bool packageIdentityPresent = true)
    {
        string token = Guid.NewGuid().ToString("N");
        string resultPath = HardwareInspectionAcceptanceResultStore.GetResultPath(token);
        var service = new Gate9ScriptedHardwareInspectionService(run);

        try
        {
            int exitCode = await HardwareInspectionGate9AcceptanceHost.RunAsync(
                token,
                service,
                () => packageIdentityPresent);
            string? json = File.Exists(resultPath)
                ? Encoding.UTF8.GetString(File.ReadAllBytes(resultPath))
                : null;
            return new Gate9RunObservation(exitCode, json);
        }
        finally
        {
            if (File.Exists(resultPath))
            {
                File.Delete(resultPath);
            }
        }
    }

    private static async Task<HardwareInspectionRunResult> CompleteAsync(
        Guid inspectionId,
        IProgress<HardwareInspectionRunProgress> progress,
        CancellationToken cancellationToken,
        HardwareInspectionOutcome outcome)
    {
        await ReportStagesAsync(inspectionId, progress, cancellationToken);
        return CreateCompleted(inspectionId, outcome, 19);
    }

    private static async Task ReportStagesAsync(
        Guid inspectionId,
        IProgress<HardwareInspectionRunProgress> progress,
        CancellationToken cancellationToken)
    {
        long sequence = 0;
        foreach (HardwareInspectionRunStage stage in Enum.GetValues<HardwareInspectionRunStage>())
        {
            progress.Report(new HardwareInspectionRunProgress(
                inspectionId,
                ++sequence,
                stage));
        }

        await Task.Delay(50, cancellationToken);
    }

    private static HardwareInspectionRunResult CreateCompleted(
        Guid inspectionId,
        HardwareInspectionOutcome outcome,
        int manifestFieldCount) => HardwareInspectionRunResult.CreateCompleted(
            inspectionId,
            outcome,
            CreateSnapshot(manifestFieldCount));

    private static HardwareSnapshot CreateSnapshot(int manifestFieldCount)
    {
        HardwareSnapshot template =
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation();
        string[] requiredFields =
        [
            "processor.name",
            "memory.installedBytes",
            "memory.osUsableBytes",
            "memory.availableBytes",
        ];
        HardwareEvidenceEntry[] evidence = Enumerable.Range(0, manifestFieldCount)
            .Select(index => new HardwareEvidenceEntry(
                index < requiredFields.Length ? requiredFields[index] : $"optional.field-{index:D2}",
                EvidenceSourceKind.Windows,
                EvidenceResolutionState.ResolvedCorroborated,
                template.CapturedAtUtc,
                EvidenceConfidence.High,
                safeDiagnosticCode: null))
            .ToArray();

        return new HardwareSnapshot(
            Guid.NewGuid(),
            template.CapturedAtUtc,
            template.SchemaVersion,
            template.PolicyVersion,
            template.Processor,
            template.Memory,
            template.GraphicsAdapters,
            template.NeuralProcessor,
            template.Storage,
            template.OperatingSystem,
            template.LocalRuntime,
            new HardwareEvidenceManifest(evidence),
            HardwareSnapshotUsability.Usable);
    }

    private static HardwareInspectionRunResult CreateRawResult(
        Guid inspectionId,
        HardwareInspectionOutcome outcome,
        HardwareSnapshot? snapshot,
        HardwareInspectionFailureKind? failureKind,
        string? safeDiagnosticCode,
        HardwareInspectionHandoff? handoff)
    {
        ConstructorInfo constructor = typeof(HardwareInspectionRunResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (HardwareInspectionRunResult)constructor.Invoke(
            [inspectionId, outcome, snapshot, failureKind, safeDiagnosticCode, handoff]);
    }

    private sealed record Gate9RunObservation(int ExitCode, string? Json);

    private sealed class Gate9ScriptedHardwareInspectionService(
        Func<Guid, IProgress<HardwareInspectionRunProgress>, CancellationToken,
            Task<HardwareInspectionRunResult>> run)
        : IHardwareInspectionService
    {
        public Task<HardwareInspectionRunResult> RunAsync(
            Guid inspectionId,
            IProgress<HardwareInspectionRunProgress> progress,
            CancellationToken cancellationToken) => run(
                inspectionId,
                progress,
                cancellationToken);
    }
}
