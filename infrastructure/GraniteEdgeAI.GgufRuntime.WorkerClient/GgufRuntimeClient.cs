using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;
using GraniteEdgeAI.GgufRuntime.Transport;
using GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient;

public sealed class GgufRuntimeClient
{
    private readonly string _workerExecutable;
    private readonly GgufWorkerBootstrap _bootstrap;

    private GgufRuntimeClient(
        string workerExecutable,
        string cliExecutable,
        string modelFile,
        IReadOnlyList<string> cliArgumentsOverride)
    {
        _workerExecutable = workerExecutable;
        _bootstrap = new GgufWorkerBootstrap(
            cliExecutable,
            modelFile,
            cliArgumentsOverride);
    }

    internal static GgufRuntimeClient CreateForTesting(
        string workerExecutable,
        string cliExecutable,
        string modelFile,
        string scenario) => new(
            workerExecutable,
            cliExecutable,
            modelFile,
            ["--scenario", scenario]);

    public static GgufRuntimeClient Create(
        VerifiedGgufRuntimePackage package,
        string modelFile)
    {
        ArgumentNullException.ThrowIfNull(package);
        string supervisor = RequireExistingAbsoluteFile(
            package.SupervisorExecutable,
            nameof(package));
        string cli = RequireExistingAbsoluteFile(
            package.AdapterExecutable,
            nameof(package));
        string model = RequireExistingAbsoluteFile(modelFile, nameof(modelFile));
        return new GgufRuntimeClient(supervisor, cli, model, []);
    }

    public static GgufRuntimeClient CreateFromPackage(
        string packageRoot,
        ReadOnlySpan<byte> trustedManifest,
        string modelFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        string root = Path.GetFullPath(packageRoot);
        VerifiedGgufRuntimePackage package = GgufRuntimePackageLoader.Verify(
            root,
            trustedManifest,
            Path.Combine(root, GgufRuntimePackageLoader.DetachedManifestFileName));
        return Create(package, modelFile);
    }

    public async Task<GgufRuntimeSession> StartAsync(
        GgufRuntimeConfiguration configuration,
        IReadOnlyList<GgufConversationTurn> initialTurns,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(initialTurns);
        TrustedToolOperationEnvironment operationEnvironment =
            TrustedToolOperationEnvironment.CreateCurrent(includeDotnetRoots: true);
        GgufWorkerProcessSession process;
        try
        {
            process = GgufWorkerProcessLauncher.Launch(
                _workerExecutable,
                [],
                operationEnvironment.Variables,
                Path.GetDirectoryName(_workerExecutable)!,
                operationEnvironment);
        }
        catch (Exception primaryFailure)
        {
            operationEnvironment.Dispose();
            if (!operationEnvironment.CleanupSucceeded)
            {
                throw new GgufWorkerPolicyException(
                    "worker-cleanup-failed",
                    "The GGUF runtime worker startup cleanup could not be verified.",
                    primaryFailure);
            }

            throw;
        }
        try
        {
            await GgufFrameWriter.WriteAsync(
                process.StandardInput,
                GgufWorkerBootstrapCodec.Serialize(_bootstrap),
                cancellationToken).ConfigureAwait(false);
            var channel = new GgufFramedChannel(
                process.StandardOutput,
                process.StandardInput);
            var session = new GgufRuntimeSession(
                process,
                channel,
                new GgufSessionId(Guid.NewGuid()));
            await session.StartAsync(configuration, initialTurns, cancellationToken)
                .ConfigureAwait(false);
            return session;
        }
        catch (Exception primaryFailure)
        {
            await GgufRuntimeStartupCleanup.RethrowPrimaryAfterCleanupAsync(
                primaryFailure,
                () => process.DisposeAsync(),
                BoundedGgufRuntimeCleanupFaultReporter.Shared).ConfigureAwait(false);
            throw new UnreachableException();
        }
    }

    private static string RequireExistingAbsoluteFile(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "A fully qualified local file is required.",
                parameterName);
        }

        string fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException(
                "A required local runtime input is missing.");
        }

        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new ArgumentException(
                "A redirected runtime input is not accepted.",
                parameterName);
        }

        for (DirectoryInfo? directory = file.Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException(
                    "A redirected runtime input is not accepted.",
                    parameterName);
            }
        }

        return fullPath;
    }
}

internal enum GgufRuntimeCleanupFaultClassification
{
    Io,
    InvalidOperation,
    ObjectDisposed,
    Unexpected,
}

internal readonly record struct GgufRuntimeCleanupFault(
    GgufRuntimeCleanupFaultClassification Classification);

internal interface IGgufRuntimeCleanupFaultReporter
{
    void Report(GgufRuntimeCleanupFault fault);
}

internal sealed class BoundedGgufRuntimeCleanupFaultReporter :
    IGgufRuntimeCleanupFaultReporter
{
    private static readonly BoundedGgufRuntimeCleanupFaultReporter s_shared = new();
    private readonly object _sync = new();
    private GgufRuntimeCleanupFault? _last;

    internal static IGgufRuntimeCleanupFaultReporter Shared => s_shared;

    public void Report(GgufRuntimeCleanupFault fault)
    {
        lock (_sync)
        {
            _last = fault;
        }
    }

    internal GgufRuntimeCleanupFault? Capture()
    {
        lock (_sync)
        {
            return _last;
        }
    }
}

internal static class GgufRuntimeStartupCleanup
{
    internal static async Task RethrowPrimaryAfterCleanupAsync(
        Exception primary,
        Func<ValueTask> cleanup,
        IGgufRuntimeCleanupFaultReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentNullException.ThrowIfNull(reporter);
        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (GgufWorkerPolicyException cleanupIntegrityFailure)
        {
            throw new GgufWorkerPolicyException(
                "worker-cleanup-failed",
                "The GGUF runtime worker startup cleanup could not be verified.",
                new AggregateException(primary, cleanupIntegrityFailure));
        }
        catch (Exception cleanupFailure)
        {
            reporter.Report(new GgufRuntimeCleanupFault(
                cleanupFailure switch
                {
                    IOException => GgufRuntimeCleanupFaultClassification.Io,
                    ObjectDisposedException =>
                        GgufRuntimeCleanupFaultClassification.ObjectDisposed,
                    InvalidOperationException =>
                        GgufRuntimeCleanupFaultClassification.InvalidOperation,
                    _ => GgufRuntimeCleanupFaultClassification.Unexpected,
                }));
        }

        ExceptionDispatchInfo.Capture(primary).Throw();
        throw new UnreachableException();
    }
}
