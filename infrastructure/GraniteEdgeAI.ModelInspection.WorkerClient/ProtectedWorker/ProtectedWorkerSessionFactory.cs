using System.Collections.ObjectModel;
using System.Globalization;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;

/// <summary>
/// Validates the public route-neutral launch contract, snapshots caller-owned
/// collections, and delegates to the one existing Windows launcher.
/// </summary>
public sealed class ProtectedWorkerSessionFactory :
    IProtectedWorkerSessionFactory
{
    private const int MaximumProtocolIdentifierCharacters = 96;
    private static readonly TimeSpan MaximumCleanupTimeout =
        TimeSpan.FromSeconds(5);
    private const string InvalidArgumentsMessage =
        "The protected worker fixed arguments are invalid.";

    private readonly IWindowsWorkerProcessPlatform _platform;

    public ProtectedWorkerSessionFactory()
        : this(WindowsWorkerProcessPlatform.Instance)
    {
    }

    internal ProtectedWorkerSessionFactory(
        IWindowsWorkerProcessPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        _platform = platform;
    }

    public Task<ProtectedWorkerSession> StartAsync(
        ProtectedWorkerLaunchSpec spec,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        cancellationToken.ThrowIfCancellationRequested();

        ValidatePolicies(spec);
        ReadOnlyCollection<string> fixedArguments =
            ValidateAndSnapshotFixedArguments(spec.FixedArguments);
        ReadOnlyDictionary<string, string> environment =
            WorkerEnvironmentPolicy.ValidateChild(spec.Environment);
        WindowsProcessLaunchRequest request = new(
            spec.Executable,
            environment,
            fixedArguments,
            spec.CleanupTimeout);
        WorkerProcessSession session = WindowsWorkerProcessLauncher.Launch(
            request,
            _platform);

        ProtectedWorkerLaunchSpec snapshot = spec with
        {
            FixedArguments = fixedArguments,
            Environment = environment
        };
        return Task.FromResult(
            new ProtectedWorkerSession(session, snapshot));
    }

    private static void ValidatePolicies(ProtectedWorkerLaunchSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec.Executable);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            spec.MaximumStandardInputLineBytes,
            0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            spec.MaximumStandardOutputLineBytes,
            0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            spec.MaximumStandardErrorBytes,
            0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            spec.StartupTimeout,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            spec.CancellationGrace,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            spec.CleanupTimeout,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            spec.CleanupTimeout,
            MaximumCleanupTimeout);
    }

    private static ReadOnlyCollection<string> ValidateAndSnapshotFixedArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        if (arguments.Count != 2)
        {
            throw InvalidArguments();
        }

        string selector = arguments[0];
        string identifier = arguments[1];
        if (
            !string.Equals(
                selector,
                "--protocol",
                StringComparison.Ordinal) ||
            !IsCanonicalProtocolIdentifier(identifier))
        {
            throw InvalidArguments();
        }

        return Array.AsReadOnly([selector, identifier]);
    }

    private static bool IsCanonicalProtocolIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length > MaximumProtocolIdentifierCharacters)
        {
            return false;
        }

        int separator = value.IndexOf('/');
        if (separator <= 0 ||
            separator != value.LastIndexOf('/') ||
            separator == value.Length - 1)
        {
            return false;
        }

        string route = value[..separator];
        string versionText = value[(separator + 1)..];
        string[] segments = route.Split('.');
        return segments.Length >= 2 &&
            segments.All(IsCanonicalRouteSegment) &&
            long.TryParse(
                versionText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long version) &&
            version > 0 &&
            string.Equals(
                versionText,
                version.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static bool IsCanonicalRouteSegment(string segment) =>
        segment.Length is > 0 and <= 32 &&
        segment[0] is >= 'a' and <= 'z' &&
        segment[^1] is (>= 'a' and <= 'z') or (>= '0' and <= '9') &&
        segment.All(static character =>
            character is (>= 'a' and <= 'z') or
                (>= '0' and <= '9') or '-');

    private static WorkerClientPolicyException InvalidArguments() =>
        WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerLaunchFailed,
            InvalidArgumentsMessage);
}
