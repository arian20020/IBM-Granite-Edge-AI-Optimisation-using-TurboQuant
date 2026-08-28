using System.Collections;
using System.Collections.ObjectModel;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

internal static class TrustedToolEnvironmentPolicy
{
    private const string FailureMessage =
        "The trusted hardware tool environment could not be secured.";

    private static readonly string[] RequiredDirectoryKeys =
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

    internal static IReadOnlyDictionary<string, string> CaptureCurrent()
    {
        try
        {
            var parent = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is not string key || entry.Value is not string value)
                {
                    throw Failure();
                }

                parent.Add(key, value);
            }

            return Create(parent);
        }
        catch (InvalidOperationException exception)
            when (exception.Message == FailureMessage)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or System.Security.SecurityException)
        {
            throw Failure();
        }
    }

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
                if (string.IsNullOrWhiteSpace(key)
                    || key.Contains('=')
                    || key.Contains('\0')
                    || !normalized.TryAdd(key, value))
                {
                    throw Failure();
                }
            }

            var child = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string key in RequiredDirectoryKeys)
            {
                if (!normalized.TryGetValue(key, out string? value) || value is null)
                {
                    throw Failure();
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
        catch (InvalidOperationException exception)
            when (exception.Message == FailureMessage)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or
                UnauthorizedAccessException or NotSupportedException)
        {
            throw Failure();
        }
    }

    private static string ValidateDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('\0')
            || !Path.IsPathFullyQualified(value))
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

    private static InvalidOperationException Failure() => new(FailureMessage);
}
