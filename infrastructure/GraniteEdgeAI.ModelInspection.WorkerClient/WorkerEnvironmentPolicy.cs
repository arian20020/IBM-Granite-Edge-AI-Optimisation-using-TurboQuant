using System.Collections.ObjectModel;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Builds the worker's empty-by-default environment from the exact hardened
/// Gate 2 allowlist. No caller can append arbitrary variables.
/// </summary>
public static class WorkerEnvironmentPolicy
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

    private static readonly string[] ConverterPathKeys =
    [
        "SystemRoot", "WINDIR", "TEMP", "TMP", "HOME", "USERPROFILE",
        "APPDATA", "LOCALAPPDATA", "HF_HOME", "XDG_CACHE_HOME", "TORCH_HOME",
        "GRANITE_CONVERTER_ROOT", "GRANITE_CONVERTER_SCRATCH"
    ];

    private static readonly string[] ConverterFlagKeys =
        ["HF_HUB_OFFLINE", "TRANSFORMERS_OFFLINE"];

    private static readonly string[] ConverterScratchPathKeys =
    [
        "TEMP", "TMP", "HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA",
        "HF_HOME", "XDG_CACHE_HOME", "TORCH_HOME"
    ];

    private static readonly ReadOnlyDictionary<string, string>
        RequiredDiagnosticValues = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DOTNET_EnableDiagnostics"] = "0",
                ["DOTNET_EnableDiagnostics_IPC"] = "0",
                ["DOTNET_EnableDiagnostics_Debugger"] = "0",
                ["DOTNET_EnableDiagnostics_Profiler"] = "0"
            });

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

            foreach ((string key, string value) in RequiredDiagnosticValues)
            {
                child[key] = value;
            }

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

    /// <summary>
    /// Revalidates a caller-supplied child environment against this same
    /// closed policy and returns an immutable canonical snapshot.
    /// </summary>
    internal static ReadOnlyDictionary<string, string> ValidateChild(
        IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        try
        {
            Dictionary<string, string?> parent =
                new(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, string value) in environment)
            {
                if (!IsAllowedChildKey(key) || !parent.TryAdd(key, value))
                {
                    throw Failure();
                }
            }

            IReadOnlyDictionary<string, string> normalized = Create(parent);
            if (normalized.Count != environment.Count)
            {
                throw Failure();
            }

            Dictionary<string, string> snapshot =
                new(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, string normalizedValue) in normalized)
            {
                if (!parent.TryGetValue(key, out string? suppliedValue) ||
                    !string.Equals(
                        normalizedValue,
                        suppliedValue,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw Failure();
                }

                snapshot.Add(key, normalizedValue);
            }

            return new ReadOnlyDictionary<string, string>(snapshot);
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
            throw Failure();
        }
    }

    internal static ReadOnlyDictionary<string, string>
        ValidateOpenVinoConverterChild(
        IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        try
        {
            if (environment.Count != ConverterPathKeys.Length + ConverterFlagKeys.Length)
            {
                throw Failure();
            }
            Dictionary<string, string> snapshot =
                new(StringComparer.OrdinalIgnoreCase);
            foreach (string key in ConverterPathKeys)
            {
                if (!environment.TryGetValue(key, out string? value))
                {
                    throw Failure();
                }
                snapshot.Add(key, ValidateExistingAbsolutePath(value));
            }
            foreach (string key in ConverterFlagKeys)
            {
                if (!environment.TryGetValue(key, out string? value) || value != "1")
                {
                    throw Failure();
                }
                snapshot.Add(key, value);
            }
            string scratch = snapshot["GRANITE_CONVERTER_SCRATCH"];
            foreach (string key in ConverterScratchPathKeys)
            {
                string candidate = snapshot[key];
                string prefix = scratch.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!string.Equals(candidate, scratch, StringComparison.OrdinalIgnoreCase) &&
                    !candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw Failure();
                }
            }
            return new ReadOnlyDictionary<string, string>(snapshot);
        }
        catch (WorkerClientPolicyException)
        {
            throw;
        }
        catch (Exception error) when (error is ArgumentException or IOException or
                                      UnauthorizedAccessException or NotSupportedException)
        {
            throw Failure();
        }
    }

    private static bool IsAllowedChildKey(string key) =>
        RequiredPathKeys.Contains(key, StringComparer.OrdinalIgnoreCase) ||
        OptionalDotnetRootKeys.Contains(key, StringComparer.OrdinalIgnoreCase) ||
        RequiredDiagnosticValues.ContainsKey(key);

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
