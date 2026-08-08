using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using LLama.Abstractions;
using LLama.Native;

namespace GraniteEdgeAI.ModelInspection.LlamaSharp;

/// <summary>
/// Performs one CPU-only LLamaSharp native-library dry run without loading a
/// model or changing the WinUI application.
/// </summary>
public static class NativeBackendSmokeProbe
{
    /// <summary>
    /// Runs the published CPU backend selection once and returns project-owned
    /// evidence rather than exposing LLamaSharp native objects.
    /// </summary>
    public static NativeBackendSmokeResult Run()
    {
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        var logs = new ConcurrentQueue<NativeBackendLogEntry>();

        try
        {
            CpuNativeRuntimeConfiguration.Configure(logs);

            bool loaded = NativeLibraryConfig.LLama.DryRun(
                out INativeLibrary? selectedLibrary);

            DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;

            if (!loaded)
            {
                return CreateBaseResult(
                    startedAtUtc,
                    completedAtUtc,
                    succeeded: false,
                    selectedBackend: null,
                    failureCode: "MI-OP-RUNTIME-UNAVAILABLE",
                    failureType: null,
                    failureMessage:
                        "LLamaSharp could not load a published CPU backend.",
                    logs);
            }

            return CreateBaseResult(
                startedAtUtc,
                completedAtUtc,
                succeeded: true,
                CpuNativeRuntimeConfiguration.Describe(selectedLibrary),
                failureCode: null,
                failureType: null,
                failureMessage: null,
                logs);
        }
        catch (Exception exception)
        {
            return CreateBaseResult(
                startedAtUtc,
                DateTimeOffset.UtcNow,
                succeeded: false,
                selectedBackend: null,
                failureCode:
                    "MI-OP-RUNTIME-INITIALISATION-FAILED",
                failureType: exception.GetType().FullName,
                failureMessage: exception.Message,
                logs);
        }
    }

    private static NativeBackendSmokeResult CreateBaseResult(
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool succeeded,
        SelectedNativeBackend? selectedBackend,
        string? failureCode,
        string? failureType,
        string? failureMessage,
        ConcurrentQueue<NativeBackendLogEntry> logs)
    {
        return new NativeBackendSmokeResult
        {
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Succeeded = succeeded,
            ManagedPackageName =
                PinnedApplicationRuntime.ManagedPackageName,
            ManagedPackageVersion =
                PinnedApplicationRuntime.ManagedPackageVersion,
            BackendPackageName =
                PinnedApplicationRuntime.BackendPackageName,
            BackendPackageVersion =
                PinnedApplicationRuntime.BackendPackageVersion,
            LlamaSharpSourceTag =
                PinnedApplicationRuntime.LlamaSharpSourceTag,
            LlamaSharpReleaseCommit =
                PinnedApplicationRuntime.LlamaSharpReleaseCommit,
            ExpectedLlamaCppCommit =
                PinnedApplicationRuntime.ExpectedLlamaCppCommit,
            IntendedProductionRuntimeIdentifier =
                PinnedApplicationRuntime
                    .IntendedProductionRuntimeIdentifier,
            ProcessArchitecture =
                RuntimeInformation.ProcessArchitecture.ToString(),
            OperatingSystem = RuntimeInformation.OSDescription,
            FrameworkDescription =
                RuntimeInformation.FrameworkDescription,
            SelectedBackend = selectedBackend,
            FailureCode = failureCode,
            FailureType = failureType,
            FailureMessage = failureMessage,
            Logs = logs.ToArray()
        };
    }
}
