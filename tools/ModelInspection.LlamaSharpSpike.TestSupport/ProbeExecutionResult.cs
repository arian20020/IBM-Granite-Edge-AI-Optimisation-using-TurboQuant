namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Contains all bounded observations retained by the parent test process.
/// </summary>
public sealed record ProbeExecutionResult
{
    public required ProcessTerminationKind TerminationKind { get; init; }

    public int? ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public required TimeSpan Duration { get; init; }

    public int ProcessId { get; init; }
}
