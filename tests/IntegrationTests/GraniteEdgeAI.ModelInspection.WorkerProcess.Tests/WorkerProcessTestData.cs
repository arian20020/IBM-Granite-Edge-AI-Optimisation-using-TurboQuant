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
    private const string FixtureProcessName =
        "GraniteEdgeAI.ModelInspection.ProtocolTestWorker";
    private const string ProductionWorkerExecutableName =
        "GraniteEdgeAI.ModelInspection.Worker.exe";
    private const string ProductionWorkerProcessName =
        "GraniteEdgeAI.ModelInspection.Worker";

    internal static InspectionWorkerClient CreateClient(
        PublishedFixture fixture,
        string scenario,
        TimeSpan? startupTimeout = null,
        TimeSpan? overallTimeout = null,
        TimeSpan? cancellationGrace = null,
        int maximumRetainedStandardErrorBytes = 4 * 1024)
    {
        WorkerClientOptions options = new(
            fixture.OutputDirectory,
            startupTimeout ?? TimeSpan.FromSeconds(3),
            overallTimeout ?? TimeSpan.FromSeconds(6),
            cancellationGrace ?? TimeSpan.FromSeconds(1),
            maximumRetainedStandardErrorBytes,
            ProcessTreeCleanupTimeout: TimeSpan.FromSeconds(3));
        options.Validate();

        return new InspectionWorkerClient(
            options,
            FixtureExecutableName,
            [scenario]);
    }

    internal static InspectionWorkerClient CreateProductionClient(
        PublishedWorker worker)
    {
        WorkerClientOptions options = new(
            worker.OutputDirectory,
            StartupTimeout: TimeSpan.FromSeconds(3),
            OverallTimeout: TimeSpan.FromSeconds(10),
            CancellationGracePeriod: TimeSpan.FromSeconds(1),
            MaximumRetainedStandardErrorBytes: 4 * 1024,
            ProcessTreeCleanupTimeout: TimeSpan.FromSeconds(3));
        options.Validate();

        return new InspectionWorkerClient(
            options,
            ProductionWorkerExecutableName,
            []);
    }

    internal static InspectionWorkerClient CreateDelayedHelloClient(
        PublishedFixture fixture,
        int helloDelayMilliseconds,
        TimeSpan overallTimeout,
        TimeSpan cancellationGrace)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            helloDelayMilliseconds);

        WorkerClientOptions options = new(
            fixture.OutputDirectory,
            StartupTimeout: TimeSpan.FromSeconds(3),
            overallTimeout,
            cancellationGrace,
            MaximumRetainedStandardErrorBytes: 4 * 1024,
            ProcessTreeCleanupTimeout: TimeSpan.FromSeconds(3));
        options.Validate();

        return new InspectionWorkerClient(
            options,
            FixtureExecutableName,
            [
                "cooperative-cancellation",
                helloDelayMilliseconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            ]);
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

    /// <summary>
    /// Polls only the dedicated fixture process name. The bounded wait avoids
    /// arbitrary sleeps while proving that Job Object cleanup has completed.
    /// </summary>
    internal static Task AssertNoFixtureProcessRemainsAsync() =>
        AssertNoProcessRemainsAsync(FixtureProcessName);

    internal static Task AssertNoProductionWorkerProcessRemainsAsync() =>
        AssertNoProcessRemainsAsync(ProductionWorkerProcessName);

    internal static async Task AssertNoProcessRemainsAsync(string processName)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(4);
        while (DateTimeOffset.UtcNow < deadline)
        {
            Process[] processes = Process.GetProcessesByName(processName);
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

            await Task.Delay(TimeSpan.FromMilliseconds(50))
                .ConfigureAwait(false);
        }

        Process[] remaining = Process.GetProcessesByName(processName);
        try
        {
            string processIds = string.Join(
                ",",
                remaining.Select(static process => process.Id));
            throw new InvalidOperationException(
                $"{processName} processes remained after cleanup: {processIds}.");
        }
        finally
        {
            foreach (Process process in remaining)
            {
                process.Dispose();
            }
        }
    }
}

/// <summary>
/// Invokes progress callbacks synchronously so cancellation can be triggered at
/// the exact observed worker stage without relying on a captured UI context.
/// </summary>
internal sealed class DelegatingProgress<T> : IProgress<T>
{
    private readonly Action<T> _callback;

    internal DelegatingProgress(Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callback = callback;
    }

    public void Report(T value) => _callback(value);
}
