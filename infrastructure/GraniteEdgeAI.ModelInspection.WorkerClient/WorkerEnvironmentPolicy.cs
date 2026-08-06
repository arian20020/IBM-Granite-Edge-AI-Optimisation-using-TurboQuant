using System.Collections.ObjectModel;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Builds the worker's empty-by-default environment from the exact hardened
/// Gate 2 allowlist. No caller can append arbitrary variables.
/// </summary>
internal static class WorkerEnvironmentPolicy
{
    private const string FailureMessage =
        "The Model Inspection worker environment could not be secured.";

    private static readonly string[] RequiredPathKeys =
    [
        "SystemRoot",
        "WINDIR",
        "TEMP",
        "TMP"
    ];

    private static readonly string[] OptionalDotnetRootKeys =
    [
        "DOTNET_ROOT",
        "DOTNET_ROOT_X64"
    ];

    public static IReadOnlyDictionary<string, string> Create(
        IReadOnlyDictionary<string, string?> parentEnvironment)
    {
        ArgumentNullException.ThrowIfNull(parentEnvironment);

        try
        {
            Dictionary<string, string?> normalizedParent =
                NormalizeParent(parentEnvironment);
            Dictionary<string, string> child =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (string key in RequiredPathKeys)
            {
                child.Add(key, RequireExistingAbsolutePath(normalizedParent, key));
            }

            foreach (string key in OptionalDotnetRootKeys)
            {
                if (normalizedParent.TryGetValue(key, out string? value) &&
                    value is not null)
                {
                    child.Add(key, ValidateExistingAbsolutePath(value));
                }
            }

            child["DOTNET_EnableDiagnostics"] = "0";
            child["DOTNET_EnableDiagnostics_IPC"] = "0";
            child["DOTNET_EnableDiagnostics_Debugger"] = "0";
            child["DOTNET_EnableDiagnostics_Profiler"] = "0";

            return new ReadOnlyDictionary<string, string>(child);
        }
        catch (WorkerClientPolicyException)
        {
            throw;
        }
        catch (Exception error) when (
            error is ArgumentException or
            IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            // Path APIs can echo environment values in exception text. Replace
            // expected validation failures with the stable sanitized error.
            throw Failure();
        }
    }

    private static Dictionary<string, string?> NormalizeParent(
        IReadOnlyDictionary<string, string?> parentEnvironment)
    {
        Dictionary<string, string?> normalized =
            new(StringComparer.OrdinalIgnoreCase);

        foreach ((string key, string? value) in parentEnvironment)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                key.Contains('=') ||
                key.Contains('\0'))
            {
                throw Failure();
            }

            if (!normalized.TryAdd(key, value))
            {
                throw Failure();
            }
        }

        return normalized;
    }

    private static string RequireExistingAbsolutePath(
        Dictionary<string, string?> parent,
        string key)
    {
        if (!parent.TryGetValue(key, out string? value) || value is null)
        {
            throw Failure();
        }

        return ValidateExistingAbsolutePath(value);
    }

    private static string ValidateExistingAbsolutePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains('\0') ||
            !Path.IsPathFullyQualified(value))
        {
            throw Failure();
        }

        string canonical = Path.GetFullPath(value);
        if (!Directory.Exists(canonical))
        {
            throw Failure();
        }

        return canonical;
    }

    private static WorkerClientPolicyException Failure()
    {
        WorkerClientFailure failure = new(
            WorkerClientFailureCodes.WorkerEnvironmentPolicyFailed,
            FailureMessage);
        return new WorkerClientPolicyException(failure);
    }
}
