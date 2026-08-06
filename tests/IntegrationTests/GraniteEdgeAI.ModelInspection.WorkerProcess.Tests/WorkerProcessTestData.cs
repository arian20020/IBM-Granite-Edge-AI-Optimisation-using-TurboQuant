using System.Diagnostics;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Creates valid request data and short integration-test policies without
/// weakening the production defaults stored in <see cref="WorkerProtocol"/>.
/// </summary>
internal static class WorkerProcessTestData
{
    private const string FixtureExecutableName =
        "GraniteEdgeAI.ModelInspection.ProtocolTestWorker.exe";

    internal static InspectionWorkerClient CreateClient(
        PublishedFixture fixture,
        string scenario,
        TimeSpan? startupTimeout = null,
        TimeSpan? overallTimeout = null,
        TimeSpan? cancellationGrace = null)
    {
        WorkerClientOptions options = new(
            fixture.OutputDirectory,
            startupTimeout ?? TimeSpan.FromSeconds(3),
            overallTimeout ?? TimeSpan.FromSeconds(6),
            cancellationGrace ?? TimeSpan.FromSeconds(1),
            MaximumRetainedStandardErrorBytes: 4 * 1024,
            ProcessTreeCleanupTimeout: TimeSpan.FromSeconds(3));
        options.Validate();

        return new InspectionWorkerClient(
            options,
            FixtureExecutableName,
            [scenario]);
    }

    internal static WorkerStartInspectionCommand StartCommand()
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        return new WorkerStartInspectionCommand
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = Guid.NewGuid(),
            ParentProcessId = Environment.ProcessId,
            ParentProcessStartTimeUtc =
                new DateTimeOffset(
                    Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                    TimeSpan.Zero),
            ModelPath = Path.Combine(
                Path.GetTempPath(),
                "GraniteEdgeAI-WorkerClient-Test",
                "model.gguf"),
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = 1024,
                LastWriteTimeUtc = utcNow
            },
            QuickScan = new WorkerQuickScanSnapshot
            {
                Format = "GGUF",
                ModelName = "fixture-model",
                Architecture = "granite",
                ParameterSizeLabel = "fixture",
                Quantisation = "Q4_K_M",
                FileSizeBytes = 1024,
                DeclaredContextLength = 4096,
                GgufVersion = 3
            }
        };
    }
}
