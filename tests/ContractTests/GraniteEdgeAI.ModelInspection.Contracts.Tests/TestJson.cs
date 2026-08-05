using System.Text;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Creates deterministic protocol values used by strict JSON contract tests.
/// </summary>
internal static class TestJson
{
    internal static byte[] Utf8(string json)
    {
        return Encoding.UTF8.GetBytes(json);
    }

    internal static WorkerHelloMessage CreateValidHello()
    {
        return new WorkerHelloMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Hello,
            WorkerId = WorkerProtocol.WorkerId,
            WorkerVersion = "1.0.0",
            WorkerProcessId = 123,
            RuntimeProfile = WorkerProtocol.RuntimeProfile,
            ProcessArchitecture = "X64"
        };
    }

    internal static WorkerCancelInspectionCommand CreateValidCancel()
    {
        return new WorkerCancelInspectionCommand
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.CancelInspection,
            RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };
    }

    internal static WorkerStartInspectionCommand CreateValidStart()
    {
        DateTimeOffset timestamp = new(
            2026,
            8,
            5,
            12,
            0,
            0,
            TimeSpan.Zero);

        return new WorkerStartInspectionCommand
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ParentProcessId = 4321,
            ParentProcessStartTimeUtc = timestamp,
            ModelPath = @"C:\Models\granite.gguf",
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = 100,
                LastWriteTimeUtc = timestamp
            },
            QuickScan = new WorkerQuickScanSnapshot
            {
                Format = "GGUF",
                ModelName = "Granite 4.1 3B",
                Architecture = "granite",
                ParameterSizeLabel = "3B",
                Quantisation = "Q4_K_M",
                FileSizeBytes = 100,
                DeclaredContextLength = 131_072,
                GgufVersion = 3
            }
        };
    }
}
