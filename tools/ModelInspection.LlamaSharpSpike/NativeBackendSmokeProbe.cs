using System.Runtime.InteropServices;
using LLama.Abstractions;
using LLama.Native;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;

/// <summary>
/// Performs one CPU-only LLamaSharp native-library dry run without loading a
/// model or changing the WinUI application.
/// </summary>
public sealed class NativeBackendSmokeProbe
{
    /// <summary>
    /// Runs the published CPU backend selection once and returns project-owned
    /// evidence rather than exposing LLamaSharp native objects.
    /// </summary>
    public NativeBackendSmokeResult Run()
    {
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        var logs = new List<NativeBackendLogEntry>();

        try
        {
            ConfigureCpuOnlySelection(logs);

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

            NativeLibraryMetadata? metadata =
                selectedLibrary?.Metadata;

            var selectedBackend = new SelectedNativeBackend
            {
                ImplementationType =
                    selectedLibrary?.GetType().FullName,
                NativeLibraryName =
                    metadata?.NativeLibraryName.ToString(),
                UsesCuda = metadata?.UseCuda,
                UsesVulkan = metadata?.UseVulkan,
                AvxLevel = metadata?.AvxLevel.ToString()
            };

            return CreateBaseResult(
                startedAtUtc,
                completedAtUtc,
                succeeded: true,
                selectedBackend,
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

    private static void ConfigureCpuOnlySelection(
        ICollection<NativeBackendLogEntry> logs)
    {
        NativeLibraryConfig.LLama
            .WithCuda(enable: false)
            .WithVulkan(enable: false)
            .WithAutoFallback(enable: true)
            .WithLogCallback(
                (level, message) =>
                {
                    logs.Add(
                        new NativeBackendLogEntry(
                            level.ToString(),
                            message.TrimEnd('\r', '\n')));
                });
    }

    private static NativeBackendSmokeResult CreateBaseResult(
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool succeeded,
        SelectedNativeBackend? selectedBackend,
        string? failureCode,
        string? failureType,
        string? failureMessage,
        IReadOnlyList<NativeBackendLogEntry> logs)
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
            OperatingSystem =
                RuntimeInformation.OSDescription,
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
