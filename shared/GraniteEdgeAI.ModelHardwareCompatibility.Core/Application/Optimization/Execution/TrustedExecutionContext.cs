using System.Security.Cryptography;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

/// <summary>Why a trusted context refused to admit what it was given.</summary>
public enum TrustedResolutionOutcome
{
    Unspecified = 0,

    /// <summary>Resolved, verified, and safe to hand to a tool.</summary>
    Verified = 1,

    /// <summary>Nothing is there, or it is not a regular file.</summary>
    NotARegularFile = 2,

    /// <summary>The length does not match what the plan bound.</summary>
    LengthMismatch = 3,

    /// <summary>The digest does not match what the plan bound.</summary>
    DigestMismatch = 4,

    /// <summary>The context belongs to a different plan.</summary>
    PlanMismatch = 5
}

/// <summary>
/// The result of checking a local file against a plan, carrying no path.
///
/// Deliberately not an exception and deliberately not a string. The caller needs
/// to know which check failed so it can return the right terminal status, and it
/// needs that answer in a form it can log - which rules out anything containing
/// the path that was examined.
/// </summary>
public sealed record TrustedResolution
{
    private TrustedResolution(TrustedResolutionOutcome outcome)
    {
        Outcome = outcome;
    }

    public TrustedResolutionOutcome Outcome { get; }

    public bool IsVerified => Outcome == TrustedResolutionOutcome.Verified;

    internal static TrustedResolution Of(TrustedResolutionOutcome outcome) => new(outcome);

    /// <summary>
    /// Names the outcome and nothing else. Overridden explicitly because the
    /// record default would print every member, and the whole discipline here is
    /// that no accidental string carries a path.
    /// </summary>
    public override string ToString() => Outcome.ToString();
}

/// <summary>
/// A local source file handed to an executor, kept out of the plan.
///
/// The plan says which bytes are expected; it must never say where they live. A
/// path in a serialised plan is a path in a manifest, a log and a support
/// record, and this feature has no reason to publish where a user keeps their
/// models.
///
/// So this type exists alongside the plan rather than inside it. It is not a
/// record and it is not serialisable: it carries a path, it stays in memory, it
/// crosses one boundary, and <see cref="ToString"/> is overridden so it cannot
/// leak through interpolation or a logger that formats its arguments.
/// </summary>
public sealed class TrustedSourceContext
{
    private readonly string _localSourcePath;

    private TrustedSourceContext(
        Guid optimizationPlanId,
        string modelInspectionRunId,
        string modelInspectionHandoffId,
        string expectedModelSha256,
        ulong expectedModelLengthBytes,
        string localSourcePath)
    {
        OptimizationPlanId = optimizationPlanId;
        ModelInspectionRunId = modelInspectionRunId;
        ModelInspectionHandoffId = modelInspectionHandoffId;
        ExpectedModelSha256 = expectedModelSha256;
        ExpectedModelLengthBytes = expectedModelLengthBytes;
        _localSourcePath = localSourcePath;
    }

    public Guid OptimizationPlanId { get; }

    /// <summary>Serialized elsewhere as <c>modelInspectionRunId</c>.</summary>
    public string ModelInspectionRunId { get; }

    /// <summary>Serialized elsewhere as <c>modelInspectionHandoffId</c>.</summary>
    public string ModelInspectionHandoffId { get; }

    /// <summary>Serialized elsewhere as <c>modelSha256</c>.</summary>
    public string ExpectedModelSha256 { get; }

    /// <summary>Serialized elsewhere as <c>modelLengthBytes</c>.</summary>
    public ulong ExpectedModelLengthBytes { get; }

    /// <summary>
    /// Builds a context from the plan itself, so the expected identity cannot be
    /// mistyped into disagreeing with what will be checked against it.
    /// </summary>
    public static TrustedSourceContext ForPlan(
        OptimizationExecutionPlan plan, string localSourcePath)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(localSourcePath))
        {
            throw new ArgumentException(
                "A source context must name a local file, or there is nothing to "
                + "verify and nothing for a tool to read.",
                nameof(localSourcePath));
        }

        return new TrustedSourceContext(
            plan.OptimizationPlanId,
            plan.Binding.ModelInspectionRunId,
            plan.Binding.ModelInspectionHandoffId,
            plan.Binding.ModelSha256,
            plan.Binding.ModelLengthBytes,
            localSourcePath);
    }

    /// <summary>
    /// Resolves and verifies the local file immediately before a tool runs.
    ///
    /// Order matters. The file is canonicalised and confirmed to be a regular
    /// file before anything is read, then the cheap length check runs, then the
    /// digest. A directory, a device or a reparse point never reaches the hash,
    /// and a file of obviously wrong size is refused without reading gigabytes.
    ///
    /// Both length and digest are compared, because a length alone collides
    /// trivially and a digest alone would accept a file that had been truncated
    /// and re-padded to match.
    /// </summary>
    public TrustedResolution Verify(OptimizationExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.OptimizationPlanId != OptimizationPlanId)
        {
            return TrustedResolution.Of(TrustedResolutionOutcome.PlanMismatch);
        }

        FileInfo file;

        try
        {
            file = new FileInfo(Path.GetFullPath(_localSourcePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException
                or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            // A path that cannot even be resolved is not a file. The exception
            // is deliberately not carried outward: its message contains the
            // path.
            return TrustedResolution.Of(TrustedResolutionOutcome.NotARegularFile);
        }

        if (!file.Exists || (file.Attributes & FileAttributes.Directory) != 0
            || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return TrustedResolution.Of(TrustedResolutionOutcome.NotARegularFile);
        }

        if ((ulong)file.Length != ExpectedModelLengthBytes)
        {
            return TrustedResolution.Of(TrustedResolutionOutcome.LengthMismatch);
        }

        string digest;

        try
        {
            using FileStream stream = file.OpenRead();
            digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (IOException)
        {
            return TrustedResolution.Of(TrustedResolutionOutcome.NotARegularFile);
        }
        catch (UnauthorizedAccessException)
        {
            return TrustedResolution.Of(TrustedResolutionOutcome.NotARegularFile);
        }

        return string.Equals(digest, ExpectedModelSha256, StringComparison.Ordinal)
            ? TrustedResolution.Of(TrustedResolutionOutcome.Verified)
            : TrustedResolution.Of(TrustedResolutionOutcome.DigestMismatch);
    }

    /// <summary>
    /// The path, handed over only after verification succeeded.
    ///
    /// Gated on purpose. A property returning it unconditionally would be read
    /// by something that had not checked, and the check is the only thing
    /// standing between a tool and the wrong file.
    /// </summary>
    public string RevealVerifiedPath(OptimizationExecutionPlan plan)
    {
        if (!Verify(plan).IsVerified)
        {
            throw new InvalidOperationException(
                "The local source did not match the plan, so its path is not "
                + "released. Verify before launching a tool.");
        }

        return Path.GetFullPath(_localSourcePath);
    }

    /// <summary>
    /// Never the path. This type exists to keep a private location out of logs,
    /// and the default record or object formatting is exactly how it would get
    /// there.
    /// </summary>
    public override string ToString() =>
        $"TrustedSourceContext(plan={OptimizationPlanId})";
}

/// <summary>
/// A local executable handed to an executor, kept out of the plan.
///
/// The plan pins which tool is permitted by identity and digest. This carries
/// where that tool happens to live on one machine, which is not something a
/// plan should record - and the digest is recomputed here before launch, so an
/// executable swapped after admission fails closed rather than running.
/// </summary>
public sealed class TrustedToolContext
{
    private readonly string _localExecutablePath;

    private TrustedToolContext(
        Guid optimizationPlanId,
        string packageId,
        string expectedExecutableSha256,
        string localExecutablePath)
    {
        OptimizationPlanId = optimizationPlanId;
        PackageId = packageId;
        ExpectedExecutableSha256 = expectedExecutableSha256;
        _localExecutablePath = localExecutablePath;
    }

    public Guid OptimizationPlanId { get; }

    public string PackageId { get; }

    public string ExpectedExecutableSha256 { get; }

    /// <summary>
    /// Builds a tool context from the plan's own quantiser identity.
    ///
    /// Refuses when the plan performs no conversion. A tool context on a
    /// runtime-only plan is a tool nothing authorised, and admitting one here
    /// would let a conversion run under a plan that never proposed one.
    /// </summary>
    public static TrustedToolContext ForGgufQuantiser(
        OptimizationExecutionPlan plan, string localExecutablePath)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(localExecutablePath))
        {
            throw new ArgumentException(
                "A tool context must name a local executable.",
                nameof(localExecutablePath));
        }

        if (plan.ExecutionPayload.Gguf?.Quantiser is not { } quantiser)
        {
            throw new InvalidOperationException(
                "This plan pins no quantiser, so no conversion tool is authorised "
                + "for it. A tool admitted here would run work the plan never "
                + "proposed.");
        }

        return new TrustedToolContext(
            plan.OptimizationPlanId,
            quantiser.PackageId,
            quantiser.ExecutableSha256,
            localExecutablePath);
    }

    /// <summary>
    /// Recomputes the executable's digest and compares it with the pinned one.
    /// </summary>
    public TrustedResolution Verify(OptimizationExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.OptimizationPlanId != OptimizationPlanId)
        {
            return TrustedResolution.Of(TrustedResolutionOutcome.PlanMismatch);
        }

        FileInfo file;

        try
        {
            file = new FileInfo(Path.GetFullPath(_localExecutablePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException
                or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return TrustedResolution.Of(TrustedResolutionOutcome.NotARegularFile);
        }

        if (!file.Exists || (file.Attributes & FileAttributes.Directory) != 0
            || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return TrustedResolution.Of(TrustedResolutionOutcome.NotARegularFile);
        }

        string digest;

        try
        {
            using FileStream stream = file.OpenRead();
            digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return TrustedResolution.Of(TrustedResolutionOutcome.NotARegularFile);
        }

        return string.Equals(digest, ExpectedExecutableSha256, StringComparison.Ordinal)
            ? TrustedResolution.Of(TrustedResolutionOutcome.Verified)
            : TrustedResolution.Of(TrustedResolutionOutcome.DigestMismatch);
    }

    public string RevealVerifiedPath(OptimizationExecutionPlan plan)
    {
        if (!Verify(plan).IsVerified)
        {
            throw new InvalidOperationException(
                "The local executable did not match the digest this plan pins, so "
                + "its path is not released.");
        }

        return Path.GetFullPath(_localExecutablePath);
    }

    public override string ToString() =>
        $"TrustedToolContext(plan={OptimizationPlanId}, package={PackageId})";
}
