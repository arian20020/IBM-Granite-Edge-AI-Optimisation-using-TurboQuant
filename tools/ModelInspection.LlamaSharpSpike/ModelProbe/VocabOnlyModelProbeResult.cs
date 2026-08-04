namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Identifies how one feasibility probe ended without implying a final model
/// outcome.
/// </summary>
public enum VocabOnlyProbeCompletionStatus
{
    Succeeded,
    Cancelled,
    Failed
}

/// <summary>
/// Records one special-token identifier and optional decoded text.
/// </summary>
public sealed record SpecialTokenEvidence
{
    public string? TokenId { get; init; }

    public string? DecodedText { get; init; }
}

/// <summary>
/// Records vocabulary and known special-token evidence exposed by LLamaSharp.
/// </summary>
public sealed record VocabularyEvidence
{
    public int Count { get; init; }

    public string Type { get; init; } = string.Empty;

    public SpecialTokenEvidence Bos { get; init; } = new();

    public SpecialTokenEvidence Eos { get; init; } = new();

    public SpecialTokenEvidence Newline { get; init; } = new();

    public SpecialTokenEvidence Pad { get; init; } = new();

    public SpecialTokenEvidence Mask { get; init; } = new();

    public SpecialTokenEvidence Separator { get; init; } = new();
}

/// <summary>
/// Records whether a fixed non-sensitive string could be tokenised through the
/// loaded vocabulary.
/// </summary>
public sealed record TokenizerSmokeEvidence
{
    public const string InputText = "Hello";

    public bool Succeeded { get; init; }

    public int? TokenCount { get; init; }

    public string? FailureType { get; init; }

    public string? FailureMessage { get; init; }
}

/// <summary>
/// Records embedded chat-template presence without copying the template into
/// the evidence report.
/// </summary>
public sealed record ChatTemplateEvidence
{
    public bool Present { get; init; }

    public int? LengthCharacters { get; init; }

    public string? Sha256 { get; init; }
}

/// <summary>
/// Contains normalised technical facts collected while the native model handle
/// is valid.
/// </summary>
public sealed record VocabOnlyRuntimeModelEvidence
{
    public string Description { get; init; } = string.Empty;

    public int MetadataCount { get; init; }

    public IReadOnlyList<string> MetadataKeys { get; init; } =
        Array.Empty<string>();

    public string? Architecture { get; init; }

    public string? ModelName { get; init; }

    public string? FileType { get; init; }

    public string? QuantizationVersion { get; init; }

    public string? TokenizerModel { get; init; }

    public int ContextSize { get; init; }

    public ulong RuntimeReportedSizeBytes { get; init; }

    public ulong ParameterCount { get; init; }

    public int EmbeddingSize { get; init; }

    public int LayerCount { get; init; }

    public int HeadCount { get; init; }

    public int KvHeadCount { get; init; }

    public bool HasEncoder { get; init; }

    public bool HasDecoder { get; init; }

    public bool IsRecurrent { get; init; }

    public bool IsDiffusion { get; init; }

    public VocabularyEvidence Vocabulary { get; init; } = new();

    public TokenizerSmokeEvidence TokenizerSmoke { get; init; } = new();

    public ChatTemplateEvidence ChatTemplate { get; init; } = new();
}

/// <summary>
/// Contains the complete project-owned result of one CPU VocabOnly feasibility
/// probe.
/// </summary>
public sealed record VocabOnlyModelProbeResult
{
    public string SchemaVersion { get; init; } = "1.0";

    public string ProbeMode { get; init; } = "VocabOnly";

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public long DurationMilliseconds { get; init; }

    public VocabOnlyProbeCompletionStatus CompletionStatus { get; init; }

    public bool Succeeded =>
        CompletionStatus == VocabOnlyProbeCompletionStatus.Succeeded;

    public bool VocabOnlyRequested { get; init; } = true;

    public int GpuLayerCount { get; init; }

    public bool UseMemoryMap { get; init; } = true;

    public bool UseMemoryLock { get; init; }

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

    public ModelFileSnapshot? BeforeSnapshot { get; init; }

    public ModelFileSnapshot? AfterSnapshot { get; init; }

    public ModelFileIntegrityComparison? Integrity { get; init; }

    public string? IntegrityVerificationErrorType { get; init; }

    public string? IntegrityVerificationErrorMessage { get; init; }

    public VocabOnlyRuntimeModelEvidence? ModelEvidence { get; init; }

    public IReadOnlyList<NativeLoadProgressSample> ProgressSamples { get; init; } =
        Array.Empty<NativeLoadProgressSample>();

    public long? LoadDurationMilliseconds { get; init; }

    public long WorkingSetBeforeBytes { get; init; }

    public long? WorkingSetAfterLoadBytes { get; init; }

    public long PeakWorkingSetBytes { get; init; }

    public long WorkingSetAfterDisposeBytes { get; init; }

    public bool? NativeHandleClosedAfterDispose { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureType { get; init; }

    public string? FailureMessage { get; init; }

    public IReadOnlyList<NativeBackendLogEntry> Logs { get; init; } =
        Array.Empty<NativeBackendLogEntry>();
}
