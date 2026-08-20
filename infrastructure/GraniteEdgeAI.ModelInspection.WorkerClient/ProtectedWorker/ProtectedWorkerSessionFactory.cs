using System.Collections.ObjectModel;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;

/// <summary>
/// Validates the public route-neutral launch contract, snapshots caller-owned
/// collections, and delegates to the one existing Windows launcher.
/// </summary>
public sealed class ProtectedWorkerSessionFactory :
    IProtectedWorkerSessionFactory
{
    private const int MaximumFixedArgumentCount = 16;
    private const int MaximumFixedArgumentCharacters = 128;
    private const string InvalidArgumentsMessage =
        "The protected worker fixed arguments are invalid.";
    private static readonly string[] SensitiveArgumentNames =
    [
        "--model",
        "--model-path",
        "--prompt",
        "--input",
        "--json",
        "--payload"
    ];

    private static readonly string[] ModelPayloadExtensions =
    [
        ".gguf",
        ".onnx",
        ".bin",
        ".xml",
        ".safetensors"
    ];

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
            fixedArguments);
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
    }

    private static ReadOnlyCollection<string> ValidateAndSnapshotFixedArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count > MaximumFixedArgumentCount)
        {
            throw InvalidArguments();
        }

        List<string> snapshot = new(arguments.Count);
        foreach (string argument in arguments)
        {
            if (!IsSafeSelector(argument))
            {
                throw InvalidArguments();
            }

            snapshot.Add(argument);
        }

        return snapshot.AsReadOnly();
    }

    private static bool IsSafeSelector(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument) ||
            argument.Length > MaximumFixedArgumentCharacters ||
            argument.Any(char.IsWhiteSpace) ||
            argument.Contains('\0') ||
            argument.Contains('\\') ||
            argument.Contains("..", StringComparison.Ordinal) ||
            argument.IndexOfAny(['{', '}', '[', ']', '"', '\'', ':', ',']) >= 0 ||
            Path.IsPathRooted(argument) ||
            SensitiveArgumentNames.Contains(
                argument,
                StringComparer.OrdinalIgnoreCase) ||
            ModelPayloadExtensions.Any(extension =>
                argument.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        foreach (char character in argument)
        {
            if (!(character is >= 'a' and <= 'z') &&
                !(character is >= 'A' and <= 'Z') &&
                !(character is >= '0' and <= '9') &&
                character is not '-' and not '_' and not '.' and not '/')
            {
                return false;
            }
        }

        // A bare word is indistinguishable from a one-word prompt. Approved
        // fixed selectors are flags, numeric values, kebab/snake identifiers,
        // or versioned protocol identifiers.
        return argument.StartsWith("--", StringComparison.Ordinal) ||
            argument.All(char.IsDigit) ||
            argument.IndexOfAny(['-', '_', '.', '/']) >= 0;
    }

    private static WorkerClientPolicyException InvalidArguments() =>
        WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerLaunchFailed,
            InvalidArgumentsMessage);
}
