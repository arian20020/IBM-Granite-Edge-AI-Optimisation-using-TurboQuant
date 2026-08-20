using System.Collections.ObjectModel;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient;

internal static class GgufWorkerEnvironmentPolicy
{
    private static readonly string[] RequiredPathKeys =
    [
        "SystemRoot",
        "WINDIR",
        "TEMP",
        "TMP",
    ];

    private static readonly string[] OptionalDotnetRootKeys =
    [
        "DOTNET_ROOT",
        "DOTNET_ROOT_X64",
    ];

    internal static IReadOnlyDictionary<string, string> Create(
        IReadOnlyDictionary<string, string?> parentEnvironment)
    {
        ArgumentNullException.ThrowIfNull(parentEnvironment);
        try
        {
            var normalized = new Dictionary<string, string?>(
                StringComparer.OrdinalIgnoreCase);
            foreach ((string key, string? value) in parentEnvironment)
            {
                if (string.IsNullOrWhiteSpace(key) ||
                    key.Contains('=') ||
                    key.Contains('\0') ||
                    !normalized.TryAdd(key, value))
                {
                    throw GgufWorkerPolicyException.EnvironmentFailure();
                }
            }

            var child = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in RequiredPathKeys)
            {
                if (!normalized.TryGetValue(key, out string? value) || value is null)
                {
                    throw GgufWorkerPolicyException.EnvironmentFailure();
                }

                child.Add(key, ValidateDirectory(value));
            }

            foreach (string key in OptionalDotnetRootKeys)
            {
                if (normalized.TryGetValue(key, out string? value) && value is not null)
                {
                    child.Add(key, ValidateDirectory(value));
                }
            }

            child["DOTNET_EnableDiagnostics"] = "0";
            child["DOTNET_EnableDiagnostics_IPC"] = "0";
            child["DOTNET_EnableDiagnostics_Debugger"] = "0";
            child["DOTNET_EnableDiagnostics_Profiler"] = "0";
            return new ReadOnlyDictionary<string, string>(child);
        }
        catch (GgufWorkerPolicyException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or
                UnauthorizedAccessException or NotSupportedException)
        {
            throw GgufWorkerPolicyException.EnvironmentFailure();
        }
    }

    private static string ValidateDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains('\0') ||
            !Path.IsPathFullyQualified(value))
        {
            throw GgufWorkerPolicyException.EnvironmentFailure();
        }

        string canonical = Path.GetFullPath(value);
        if (!Directory.Exists(canonical))
        {
            throw GgufWorkerPolicyException.EnvironmentFailure();
        }

        return canonical;
    }
}
