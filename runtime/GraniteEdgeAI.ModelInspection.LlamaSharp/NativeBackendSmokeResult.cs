namespace GraniteEdgeAI.ModelInspection.LlamaSharp;

/// <summary>
/// Contains one native-library loader log entry without retaining native
/// objects or arbitrary exception state.
/// </summary>
/// <param name="Level">LLamaSharp/native log level.</param>
/// <param name="Message">Log message emitted during the dry run.</param>
public sealed record NativeBackendLogEntry(
    string Level,
    string Message);

/// <summary>
/// Contains the selected native CPU backend metadata exposed by LLamaSharp.
/// </summary>
public sealed record SelectedNativeBackend
{
    public string? ImplementationType { get; init; }

    public string? NativeLibraryName { get; init; }

    public bool? UsesCuda { get; init; }

    public bool? UsesVulkan { get; init; }

    public string? AvxLevel { get; init; }
}

/// <summary>
/// Contains the complete project-owned result of one native backend dry run.
/// </summary>
public sealed record NativeBackendSmokeResult
{
    public string SchemaVersion { get; init; } = "1.0";

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public bool Succeeded { get; init; }

    public string ManagedPackageName { get; init; } = string.Empty;

    public string ManagedPackageVersion { get; init; } = string.Empty;

    public string BackendPackageName { get; init; } = string.Empty;

    public string BackendPackageVersion { get; init; } = string.Empty;

    public string LlamaSharpSourceTag { get; init; } = string.Empty;

    public string LlamaSharpReleaseCommit { get; init; } = string.Empty;

    public string ExpectedLlamaCppCommit { get; init; } = string.Empty;

    public string IntendedProductionRuntimeIdentifier { get; init; } =
        string.Empty;

    public string ProcessArchitecture { get; init; } = string.Empty;

    public string OperatingSystem { get; init; } = string.Empty;

    public string FrameworkDescription { get; init; } = string.Empty;

    public SelectedNativeBackend? SelectedBackend { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureType { get; init; }

    public string? FailureMessage { get; init; }

    public IReadOnlyList<NativeBackendLogEntry> Logs { get; init; } =
        Array.Empty<NativeBackendLogEntry>();
}
