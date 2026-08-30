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

    internal static TrustedToolOperationEnvironment CaptureCurrent()
    {
        try
        {
            var parent = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot"),
                ["WINDIR"] = Environment.GetEnvironmentVariable("WINDIR"),
            };

            return TrustedToolOperationEnvironment.Create(parent);
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

            string systemRoot = RequireDirectory(normalized, "SystemRoot");
            string windowsDirectory = RequireDirectory(normalized, "WINDIR");
            string canonicalWindowsDirectory = ValidateDirectory(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            if (!string.Equals(systemRoot, windowsDirectory, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    systemRoot,
                    canonicalWindowsDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw Failure();
            }

            var child = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["SystemRoot"] = systemRoot,
                ["WINDIR"] = windowsDirectory,
                ["TEMP"] = RequireDirectory(normalized, "TEMP"),
                ["TMP"] = RequireDirectory(normalized, "TMP"),
            };

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
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.StartsWith(@"\\", StringComparison.Ordinal)
            || value.StartsWith("//", StringComparison.Ordinal)
            || value.IndexOf(':', 2) >= 0
            || HasUnsafeSegment(value)
            || !Path.IsPathFullyQualified(value))
        {
            throw Failure();
        }

        string canonical = Path.GetFullPath(value);
        if (!Directory.Exists(canonical) || HasReparseAncestor(canonical))
        {
            throw Failure();
        }

        return canonical;
    }

    private static string RequireDirectory(
        Dictionary<string, string?> environment,
        string key)
    {
        if (!environment.TryGetValue(key, out string? value) || value is null)
        {
            throw Failure();
        }

        return ValidateDirectory(value);
    }

    private static bool HasUnsafeSegment(string value) =>
        value.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(static segment =>
                segment is "." or ".."
                || !string.Equals(segment, segment.Trim(), StringComparison.Ordinal)
                || segment.Any(static character => char.IsControl(character)));

    private static bool HasReparseAncestor(string canonical)
    {
        DirectoryInfo? directory = new(canonical);
        while (directory is not null)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            directory = directory.Parent;
        }

        return false;
    }

    private static InvalidOperationException Failure() => new(FailureMessage);
}
