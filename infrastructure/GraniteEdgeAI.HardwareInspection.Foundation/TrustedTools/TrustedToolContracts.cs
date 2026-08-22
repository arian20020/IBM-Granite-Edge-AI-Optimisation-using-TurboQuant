using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

public enum PeMachine
{
    Amd64,
}

public enum TrustedToolPackageDisposition
{
    FunctionalPassWithPackagingConcern,
    AcceptedForFunctionalEvaluation,
}

public enum TrustedToolVerificationFailure
{
    ApprovedRootInvalid,
    PackageRootInvalid,
    PathEscape,
    ReparsePoint,
    InventoryMismatch,
    FileUnavailable,
    HashMismatch,
    PeInvalid,
    PeArchitectureMismatch,
    PackageChangedDuringVerification,
}

public sealed class TrustedToolCommand
{
    private static readonly Regex IdentityPattern = new(
        "^[a-z][a-z0-9-]{0,63}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public TrustedToolCommand(string identity, IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (string.IsNullOrWhiteSpace(identity) || !IdentityPattern.IsMatch(identity))
        {
            throw new ArgumentException("Command identity is invalid.", nameof(identity));
        }

        string[] copy = arguments.ToArray();
        if (copy.Length is 0 or > 32 || copy.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Command arguments are invalid.", nameof(arguments));
        }

        Identity = identity;
        Arguments = new ReadOnlyCollection<string>(copy);
    }

    public string Identity { get; }

    public IReadOnlyList<string> Arguments { get; }
}

public sealed class TrustedToolPackageManifest
{
    private static readonly Regex Sha256Pattern = new(
        "^[0-9a-f]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public TrustedToolPackageManifest(
        string toolId,
        string version,
        string executableRelativePath,
        string executableSha256,
        IEnumerable<string> requiredMembers,
        PeMachine requiredMachine,
        TrustedToolPackageDisposition disposition,
        IEnumerable<TrustedToolCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(requiredMembers);
        ArgumentNullException.ThrowIfNull(commands);
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("Tool identity is required.", nameof(toolId));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Tool version is required.", nameof(version));
        }

        if (!IsSafeLeafName(executableRelativePath))
        {
            throw new ArgumentException("Executable member is unsafe.", nameof(executableRelativePath));
        }

        if (string.IsNullOrWhiteSpace(executableSha256) ||
            !Sha256Pattern.IsMatch(executableSha256))
        {
            throw new ArgumentException("Executable SHA-256 is not canonical.", nameof(executableSha256));
        }

        if (!Enum.IsDefined(requiredMachine))
        {
            throw new ArgumentOutOfRangeException(nameof(requiredMachine));
        }

        if (!Enum.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition));
        }

        string[] memberCopy = requiredMembers.ToArray();
        if (memberCopy.Length is 0 or > 64 ||
            memberCopy.Any(member => !IsSafeLeafName(member)) ||
            memberCopy.Distinct(StringComparer.OrdinalIgnoreCase).Count() != memberCopy.Length ||
            !memberCopy.Contains(executableRelativePath, StringComparer.Ordinal))
        {
            throw new ArgumentException("Required package inventory is invalid.", nameof(requiredMembers));
        }

        TrustedToolCommand[] commandCopy = commands.ToArray();
        if (commandCopy.Length is 0 or > 16 ||
            commandCopy.Any(command => command is null) ||
            commandCopy.Select(command => command.Identity)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != commandCopy.Length)
        {
            throw new ArgumentException("Command inventory is invalid.", nameof(commands));
        }

        ToolId = toolId;
        Version = version;
        ExecutableRelativePath = executableRelativePath;
        ExecutableSha256 = executableSha256;
        RequiredMembers = new ReadOnlyCollection<string>(memberCopy);
        RequiredMachine = requiredMachine;
        Disposition = disposition;
        Commands = new ReadOnlyCollection<TrustedToolCommand>(commandCopy);
    }

    public string ToolId { get; }

    public string Version { get; }

    public string ExecutableRelativePath { get; }

    public string ExecutableSha256 { get; }

    public IReadOnlyList<string> RequiredMembers { get; }

    public PeMachine RequiredMachine { get; }

    public TrustedToolPackageDisposition Disposition { get; }

    public IReadOnlyList<TrustedToolCommand> Commands { get; }

    private static bool IsSafeLeafName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value is not "." and not ".." &&
        !Path.IsPathRooted(value) &&
        string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal) &&
        value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;
}

public sealed class VerifiedTrustedTool : IDisposable
{
    private readonly IDisposable _custody;
    private int _disposed;

    internal VerifiedTrustedTool(
        string toolId,
        string version,
        string packageRoot,
        string executablePath,
        TrustedToolPackageDisposition disposition,
        IReadOnlyDictionary<string, TrustedToolCommand> commands,
        IDisposable custody)
    {
        ToolId = toolId;
        Version = version;
        PackageRoot = packageRoot;
        ExecutablePath = executablePath;
        Disposition = disposition;
        Commands = commands;
        _custody = custody;
    }

    public string ToolId { get; }

    public string Version { get; }

    internal string PackageRoot { get; }

    internal string ExecutablePath { get; }

    public TrustedToolPackageDisposition Disposition { get; }

    internal IReadOnlyDictionary<string, TrustedToolCommand> Commands { get; }

    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    internal bool TryAcquireExecutionCustody(out IDisposable? lease)
    {
        if (IsDisposed || _custody is not TrustedToolCustody trustedCustody)
        {
            lease = null;
            return false;
        }

        return trustedCustody.TryAcquire(out lease);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _custody.Dispose();
        }
    }
}

public sealed record TrustedToolVerificationResult
{
    private TrustedToolVerificationResult(
        VerifiedTrustedTool? tool,
        TrustedToolVerificationFailure? failure)
    {
        Tool = tool;
        Failure = failure;
    }

    public bool IsVerified => Tool is not null;

    public VerifiedTrustedTool? Tool { get; }

    public TrustedToolVerificationFailure? Failure { get; }

    internal static TrustedToolVerificationResult Verified(VerifiedTrustedTool tool) => new(tool, null);

    internal static TrustedToolVerificationResult Rejected(TrustedToolVerificationFailure failure) =>
        new(null, failure);
}
