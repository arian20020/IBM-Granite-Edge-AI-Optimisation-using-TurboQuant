using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using System.Buffers;
using System.Text.Json;
using Windows.ApplicationModel;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

internal static class HardwareInspectionGate9AcceptanceHost
{
    private const string ActivationCommand = "--hardware-inspection-gate9-acceptance";
    private const string ResultTokenSwitch = "--result-token";
    private const string Schema = "granite.hardware-inspection.gate9-production-run/v1";
    private const string TestPackageIdentity = "GraniteEdgeAI.WinUI.UnitTests";
    private const int ExpectedManifestFieldCount = 19;
    private const int MaximumResultBytes = 4 * 1024;
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(150);
    private static readonly TimeSpan ProgressDeliveryTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly HardwareInspectionRunStage[] ExpectedStages =
    [
        HardwareInspectionRunStage.StartingHardwareInspection,
        HardwareInspectionRunStage.ReadingProcessorInformation,
        HardwareInspectionRunStage.ReadingSystemMemory,
        HardwareInspectionRunStage.DetectingGraphicsHardware,
        HardwareInspectionRunStage.CheckingLocalInferenceRuntimes,
        HardwareInspectionRunStage.NormalisingHardwareInformation,
        HardwareInspectionRunStage.CreatingHardwareReport,
    ];

    internal static bool TryParseActivation(
        IReadOnlyList<string> commandLine,
        out string resultToken)
    {
        resultToken = string.Empty;
        if (commandLine.Count != 4 ||
            !string.Equals(commandLine[1], ActivationCommand, StringComparison.Ordinal) ||
            !string.Equals(commandLine[2], ResultTokenSwitch, StringComparison.Ordinal) ||
            !HardwareInspectionAcceptanceResultStore.IsResultToken(commandLine[3]))
        {
            return false;
        }

        resultToken = commandLine[3];
        return true;
    }

    internal static async Task<int> RunAsync(string resultToken)
    {
        if (!HardwareInspectionAcceptanceResultStore.IsResultToken(resultToken))
        {
            return 64;
        }

        try
        {
            return await RunAsync(
                resultToken,
                HardwareInspectionComposition.CreateProduction(),
                HasExpectedPackageIdentity);
        }
        catch
        {
            return 70;
        }
    }

    internal static async Task<int> RunAsync(
        string resultToken,
        IHardwareInspectionService service,
        Func<bool> packageIdentityPresent)
    {
        if (!HardwareInspectionAcceptanceResultStore.IsResultToken(resultToken))
        {
            return 64;
        }

        try
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(packageIdentityPresent);
            if (!packageIdentityPresent())
            {
                return 70;
            }

            Guid inspectionId = Guid.NewGuid();
            var progressUpdates = new List<HardwareInspectionRunProgress>();
            object progressLock = new();
            var expectedProgressDelivered = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var progress = new Progress<HardwareInspectionRunProgress>(update =>
            {
                lock (progressLock)
                {
                    progressUpdates.Add(update);
                    if (progressUpdates.Count >= ExpectedStages.Length)
                    {
                        expectedProgressDelivered.TrySetResult();
                    }
                }
            });
            using var timeout = new CancellationTokenSource(RunTimeout);

            HardwareInspectionRunResult result = await service.RunAsync(
                    inspectionId,
                    progress,
                    timeout.Token)
                .WaitAsync(timeout.Token);
            await WaitForProgressDeliveryAsync(expectedProgressDelivered.Task, timeout.Token);

            HardwareInspectionRunProgress[] capturedProgress;
            lock (progressLock)
            {
                capturedProgress = progressUpdates.ToArray();
            }

            if (!IsValidProgress(inspectionId, capturedProgress) ||
                !TryCreateResult(result, inspectionId, out Gate9Result? gate9Result))
            {
                return 70;
            }

            byte[] json = Serialize(gate9Result!);
            HardwareInspectionAcceptanceResultStore.WriteAtomically(
                resultToken,
                json,
                MaximumResultBytes);
            return result.Outcome is HardwareInspectionOutcome.Completed or
                HardwareInspectionOutcome.CompletedWithWarnings
                ? 0
                : 1;
        }
        catch
        {
            return 70;
        }
    }

    private static bool HasExpectedPackageIdentity()
    {
        PackageId identity = Package.Current.Id;
        return string.Equals(identity.Name, TestPackageIdentity, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(identity.FullName);
    }

    private static async Task WaitForProgressDeliveryAsync(
        Task expectedProgressDelivered,
        CancellationToken cancellationToken)
    {
        if (expectedProgressDelivered.IsCompleted)
        {
            return;
        }

        await Task.WhenAny(
            expectedProgressDelivered,
            Task.Delay(ProgressDeliveryTimeout, cancellationToken));
    }

    private static bool IsValidProgress(
        Guid inspectionId,
        IReadOnlyList<HardwareInspectionRunProgress> progress)
    {
        if (progress.Count != ExpectedStages.Length)
        {
            return false;
        }

        long previousSequence = 0;
        for (int index = 0; index < ExpectedStages.Length; index++)
        {
            HardwareInspectionRunProgress update = progress[index];
            if (update.InspectionId != inspectionId ||
                update.Sequence <= previousSequence ||
                update.Stage != ExpectedStages[index])
            {
                return false;
            }

            previousSequence = update.Sequence;
        }

        return true;
    }

    private static bool TryCreateResult(
        HardwareInspectionRunResult result,
        Guid inspectionId,
        out Gate9Result? gate9Result)
    {
        gate9Result = null;
        if (result is null ||
            result.InspectionId != inspectionId ||
            !Enum.IsDefined(result.Outcome))
        {
            return false;
        }

        bool handoffPresent = result.Handoff is not null;
        int manifestFieldCount = result.Snapshot?.Evidence.Entries.Count ?? 0;
        string[] diagnostics;
        switch (result.Outcome)
        {
            case HardwareInspectionOutcome.Completed:
            case HardwareInspectionOutcome.CompletedWithWarnings:
                if (result.Snapshot is null ||
                    result.Handoff is null ||
                    result.Handoff.InspectionId != inspectionId ||
                    !ReferenceEquals(result.Handoff.Snapshot, result.Snapshot) ||
                    manifestFieldCount != ExpectedManifestFieldCount ||
                    result.FailureKind is not null ||
                    result.SafeDiagnosticCode is not null)
                {
                    return false;
                }

                diagnostics = [];
                break;

            case HardwareInspectionOutcome.Failed:
                if (result.Snapshot is not null ||
                    result.Handoff is not null ||
                    result.FailureKind is null ||
                    !Enum.IsDefined(result.FailureKind.Value) ||
                    !IsSafeDiagnostic(result.SafeDiagnosticCode))
                {
                    return false;
                }

                diagnostics = [result.SafeDiagnosticCode!];
                break;

            case HardwareInspectionOutcome.Cancelled:
                if (result.Snapshot is not null ||
                    result.Handoff is not null ||
                    result.FailureKind is not null ||
                    result.SafeDiagnosticCode is not null)
                {
                    return false;
                }

                diagnostics = [];
                break;

            default:
                return false;
        }

        gate9Result = new Gate9Result(
            result.Outcome.ToString(),
            handoffPresent,
            manifestFieldCount,
            diagnostics);
        return true;
    }

    private static bool IsSafeDiagnostic(string? value) =>
        value is not null &&
        value.Length is >= 1 and <= 64 &&
        value.All(static character =>
            character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-');

    private static byte[] Serialize(Gate9Result result)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", Schema);
            writer.WriteBoolean("packageIdentityPresent", true);
            writer.WriteString("outcome", result.Outcome);
            writer.WriteNumber("stageCount", ExpectedStages.Length);
            writer.WriteBoolean("handoffPresent", result.HandoffPresent);
            writer.WriteNumber("manifestFieldCount", result.ManifestFieldCount);
            writer.WriteStartArray("diagnostics");
            foreach (string diagnostic in result.Diagnostics)
            {
                writer.WriteStringValue(diagnostic);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private sealed record Gate9Result(
        string Outcome,
        bool HandoffPresent,
        int ManifestFieldCount,
        IReadOnlyList<string> Diagnostics);
}
