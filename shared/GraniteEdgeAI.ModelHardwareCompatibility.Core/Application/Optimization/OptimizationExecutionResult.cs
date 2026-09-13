namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// How an execution ended. Exactly these five, and every result carries one.
///
/// The two successes are separate because they promise different things. A
/// persistent success created a file the user can save; a runtime-profile
/// success did not, and a destination page that offered to "save the model" for
/// one of them would be describing something that does not exist.
///
/// ReplanRequired is not a failure. It means the world moved between planning
/// and execution, and the honest response is to plan again rather than to run
/// something the user did not confirm.
/// </summary>
public enum OptimizationExecutionStatus
{
    /// <summary>Never a real outcome. Present so a default value cannot pass as one.</summary>
    Unspecified = 0,
    SucceededPersistent = 1,
    SucceededRuntimeProfile = 2,
    Cancelled = 3,
    Failed = 4,
    ReplanRequired = 5
}

/// <summary>
/// Why an executor could not run the plan it was given.
///
/// Bounded and privacy-safe by construction. No paths, no tool output, no
/// native error text: a support record built from these can be shown to a user
/// and shipped in a log without leaking what was on their machine.
/// </summary>
public enum OptimizationSupportCode
{
    None = 0,
    SourceIdentityMismatch,
    CapabilityDrift,
    ModelBindingMismatch,
    HardwareBindingMismatch,
    ToolNotAdmitted,
    InsufficientDiskSpace,
    StagingUnavailable,
    ConversionFailed,
    ValidationFailed,
    SmokeTestFailed,
    ReinspectionFailed,
    PublicationFailed,
    CancelledByUser,
    UnexpectedFailure
}

/// <summary>
/// What an executor produced, bound to the exact plan it was given.
///
/// This is the only optimisation output the destination page may consume, and
/// only its two successful states may reach there at all. A cancelled or failed
/// run has nothing to chat with and nothing to save.
///
/// A successful result records the output identity and the attestation that the
/// source is unchanged. The source attestation is not ceremony: the whole
/// promise of this feature is that the original model survives, and a result
/// that could not evidence that would be asking the user to take it on trust.
/// </summary>
public sealed record OptimizationExecutionResult
{
    private OptimizationExecutionResult(
        OptimizationExecutionStatus status,
        Guid optimizationPlanId,
        OptimizationRoute route,
        string configurationSha256,
        string sourceSha256,
        bool sourceUnchanged,
        string? outputIdentity,
        string? outputManifestSha256,
        ulong outputSizeBytes,
        OptimizationSupportCode supportCode,
        DateTimeOffset completedAtUtc,
        Guid executionId)
    {
        Status = status;
        OptimizationPlanId = optimizationPlanId;
        Route = route;
        ConfigurationSha256 = configurationSha256;
        SourceSha256 = sourceSha256;
        SourceUnchanged = sourceUnchanged;
        OutputIdentity = outputIdentity;
        OutputManifestSha256 = outputManifestSha256;
        OutputSizeBytes = outputSizeBytes;
        SupportCode = supportCode;
        CompletedAtUtc = completedAtUtc;
        ExecutionId = executionId == Guid.Empty ? Guid.NewGuid() : executionId;
    }

    public OptimizationExecutionStatus Status { get; }

    public OptimizationDiskSpaceRequirement? DiskSpaceRequirement { get; init; }

    public Guid OptimizationPlanId { get; }

    /// <summary>
    /// Identity of this terminal execution, distinct from both the reusable
    /// plan identity and any UI attempt generation.
    /// </summary>
    public Guid ExecutionId { get; }

    public OptimizationRoute Route { get; }

    public string ConfigurationSha256 { get; }

    public string SourceSha256 { get; }

    public ulong SourceLengthBytes { get; private init; }

    public string ModelInspectionRunId { get; private init; } = string.Empty;

    public string ModelInspectionHandoffId { get; private init; } = string.Empty;

    public string ProductHardwareRunId { get; private init; } = string.Empty;

    public string HardwareSnapshotSha256 { get; private init; } = string.Empty;

    /// <summary>
    /// Verified after the run, not assumed. The original being untouched is the
    /// central promise, so it is recorded as an observation.
    /// </summary>
    public bool SourceUnchanged { get; }

    /// <summary>The artifact or profile produced. Null unless the run succeeded.</summary>
    public string? OutputIdentity { get; }

    public string? OutputManifestSha256 { get; }

    public ulong OutputSizeBytes { get; }

    public OptimizationSupportCode SupportCode { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    /// <summary>
    /// Whether this may reach the destination page. Only the two successes may.
    /// </summary>
    public bool IsSuccessful =>
        Status is OptimizationExecutionStatus.SucceededPersistent
            or OptimizationExecutionStatus.SucceededRuntimeProfile;

    /// <summary>
    /// Whether a new model file or package exists as a result. Drives whether
    /// the destination offers to save a model or to save a setup, so it is
    /// derived from the status rather than supplied separately.
    /// </summary>
    public bool ProducedPersistentArtifact =>
        Status == OptimizationExecutionStatus.SucceededPersistent;

    public static OptimizationExecutionResult Succeeded(
        OptimizationExecutionPlan plan,
        string outputIdentity,
        string outputManifestSha256,
        ulong outputSizeBytes,
        bool sourceUnchanged,
        DateTimeOffset completedAtUtc,
        Guid executionId = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        OptimizationIdentifier.Require(
            outputIdentity, nameof(outputIdentity), "The output this run produced");

        if (!OptimizationDigest.IsCanonical(outputManifestSha256))
        {
            throw new ArgumentException(
                "A successful result must carry its manifest digest, or nothing "
                + "downstream can verify what it is about to use.",
                nameof(outputManifestSha256));
        }

        if (!sourceUnchanged)
        {
            throw new ArgumentException(
                "A run that cannot attest the source is unchanged has not succeeded, "
                + "whatever it produced. The original surviving is the promise.",
                nameof(sourceUnchanged));
        }

        if (plan.ProducesPersistentArtifact && outputSizeBytes == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputSizeBytes),
                outputSizeBytes,
                "A persistent result of zero bytes is not an artifact, and offering "
                + "to save it would hand the user an empty file.");
        }

        return new OptimizationExecutionResult(
            plan.ProducesPersistentArtifact
                ? OptimizationExecutionStatus.SucceededPersistent
                : OptimizationExecutionStatus.SucceededRuntimeProfile,
            plan.OptimizationPlanId,
            plan.Route,
            plan.ConfigurationSha256,
            plan.Binding.ModelSha256,
            sourceUnchanged: true,
            outputIdentity,
            outputManifestSha256,
            outputSizeBytes,
            OptimizationSupportCode.None,
            completedAtUtc,
            executionId)
        {
            SourceLengthBytes = plan.Binding.ModelLengthBytes,
            ModelInspectionRunId = plan.Binding.ModelInspectionRunId,
            ModelInspectionHandoffId = plan.Binding.ModelInspectionHandoffId,
            ProductHardwareRunId = plan.Binding.ProductHardwareRunId,
            HardwareSnapshotSha256 = plan.Binding.HardwareSnapshotSha256
        };
    }

    /// <summary>
    /// The world moved between planning and execution. Not a failure: the plan
    /// was valid, it simply no longer describes this machine, and running it
    /// anyway would execute something the user never confirmed.
    /// </summary>
    public static OptimizationExecutionResult ReplanRequired(
        OptimizationExecutionPlan plan,
        OptimizationSupportCode supportCode,
        bool sourceUnchanged,
        DateTimeOffset completedAtUtc,
        Guid executionId = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (supportCode is not (OptimizationSupportCode.SourceIdentityMismatch
            or OptimizationSupportCode.CapabilityDrift
            or OptimizationSupportCode.ModelBindingMismatch
            or OptimizationSupportCode.HardwareBindingMismatch
            or OptimizationSupportCode.ToolNotAdmitted))
        {
            throw new ArgumentException(
                "ReplanRequired means a planning input changed. An environmental "
                + "problem that left the plan valid is a Failed result with recovery "
                + "guidance, and confusing the two sends the user to redo the wrong "
                + "thing.",
                nameof(supportCode));
        }

        return Terminal(
            plan, OptimizationExecutionStatus.ReplanRequired, supportCode,
            sourceUnchanged, completedAtUtc, executionId);
    }

    public static OptimizationExecutionResult Failed(
        OptimizationExecutionPlan plan,
        OptimizationSupportCode supportCode,
        bool sourceUnchanged,
        DateTimeOffset completedAtUtc,
        Guid executionId = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (supportCode == OptimizationSupportCode.None)
        {
            throw new ArgumentException(
                "A failure must name itself, or the user is told something went "
                + "wrong and nothing about what would help.",
                nameof(supportCode));
        }

        return Terminal(plan, OptimizationExecutionStatus.Failed, supportCode,
            sourceUnchanged, completedAtUtc, executionId);
    }

    public static OptimizationExecutionResult Cancelled(
        OptimizationExecutionPlan plan,
        bool sourceUnchanged,
        DateTimeOffset completedAtUtc,
        Guid executionId = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return Terminal(
            plan,
            OptimizationExecutionStatus.Cancelled,
            OptimizationSupportCode.CancelledByUser,
            sourceUnchanged,
            completedAtUtc,
            executionId);
    }

    /// <summary>
    /// Every non-successful ending. It publishes nothing: no output identity,
    /// no manifest, no size. A partial artifact recorded here would be one the
    /// destination page could offer to save.
    /// </summary>
    private static OptimizationExecutionResult Terminal(
        OptimizationExecutionPlan plan,
        OptimizationExecutionStatus status,
        OptimizationSupportCode supportCode,
        bool sourceUnchanged,
        DateTimeOffset completedAtUtc,
        Guid executionId) =>
        new OptimizationExecutionResult(
            status,
            plan.OptimizationPlanId,
            plan.Route,
            plan.ConfigurationSha256,
            plan.Binding.ModelSha256,
            sourceUnchanged,
            outputIdentity: null,
            outputManifestSha256: null,
            outputSizeBytes: 0,
            supportCode,
            completedAtUtc,
            executionId)
        {
            SourceLengthBytes = plan.Binding.ModelLengthBytes,
            ModelInspectionRunId = plan.Binding.ModelInspectionRunId,
            ModelInspectionHandoffId = plan.Binding.ModelInspectionHandoffId,
            ProductHardwareRunId = plan.Binding.ProductHardwareRunId,
            HardwareSnapshotSha256 = plan.Binding.HardwareSnapshotSha256
        };
}
