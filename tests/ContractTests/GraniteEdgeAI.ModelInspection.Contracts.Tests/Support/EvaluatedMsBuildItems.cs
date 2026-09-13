using System.Diagnostics;
using System.Text.Json;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests.Support;

internal sealed record EvaluatedMsBuildItem(
    string ItemType,
    string Identity,
    string FullPath,
    string? TargetPath,
    string? Link,
    string? DefiningProjectFullPath);

internal static class EvaluatedMsBuildItems
{
    private const int MaximumOutputCharacters = 16 * 1024 * 1024;

    internal static IReadOnlyList<EvaluatedMsBuildItem> Evaluate(
        string projectPath,
        IReadOnlyCollection<string> itemTypes,
        IReadOnlyCollection<string>? targets = null,
        IReadOnlyDictionary<string, string>? properties = null,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        if (itemTypes.Count == 0)
        {
            throw new ArgumentException("At least one item type is required.", nameof(itemTypes));
        }

        var startInfo = new ProcessStartInfo(ResolveDotNetHost())
        {
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in new[]
                 {
                     "msbuild",
                     Path.GetFullPath(projectPath),
                     "-nologo",
                     "-v:q",
                     $"-getItem:{string.Join(',', itemTypes)}",
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }
        if (targets is { Count: > 0 })
        {
            startInfo.ArgumentList.Add($"-target:{string.Join(';', targets)}");
        }
        if (properties is not null)
        {
            foreach ((string name, string value) in properties.OrderBy(
                         pair => pair.Key,
                         StringComparer.Ordinal))
            {
                startInfo.ArgumentList.Add($"-p:{name}={value}");
            }
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("MSBuild host did not start.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(
            timeout ?? TimeSpan.FromSeconds(45));
        try
        {
            process.WaitForExitAsync(cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("MSBuild package evaluation exceeded its timeout.");
        }

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"MSBuild package evaluation failed with exit code {process.ExitCode}. " +
                Bounded(error, output));
        }
        if (output.Length == 0 || output.Length > MaximumOutputCharacters)
        {
            throw new InvalidDataException("MSBuild package evaluation output is missing or oversized.");
        }

        int jsonStart = output.IndexOf('{');
        int jsonEnd = output.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
        {
            throw new InvalidDataException("MSBuild package evaluation returned no bounded JSON object.");
        }
        using JsonDocument document = JsonDocument.Parse(
            output.Substring(jsonStart, jsonEnd - jsonStart + 1));
        JsonElement items = document.RootElement.GetProperty("Items");
        var result = new List<EvaluatedMsBuildItem>();
        foreach (string itemType in itemTypes)
        {
            if (!items.TryGetProperty(itemType, out JsonElement entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    $"MSBuild package evaluation omitted item type '{itemType}'.");
            }
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                string identity = RequiredString(entry, "Identity", itemType);
                string fullPath = RequiredString(entry, "FullPath", itemType);
                var item = new EvaluatedMsBuildItem(
                    itemType,
                    identity,
                    fullPath,
                    OptionalString(entry, "TargetPath"),
                    OptionalString(entry, "Link"),
                    OptionalString(entry, "DefiningProjectFullPath"));
                AssertResolved(item);
                result.Add(item);
            }
        }

        string[] duplicateTargets = result
            .Where(item => !string.IsNullOrWhiteSpace(item.TargetPath))
            .GroupBy(item => item.TargetPath!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateTargets.Length != 0)
        {
            throw new InvalidDataException(
                $"Evaluated package closure contains duplicate target '{duplicateTargets[0]}'.");
        }
        return result;
    }

    internal static void RequireContained(
        IEnumerable<EvaluatedMsBuildItem> items,
        string root)
    {
        string prefix = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (EvaluatedMsBuildItem item in items)
        {
            string fullPath = Path.GetFullPath(item.FullPath);
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Evaluated package item escapes the controlled root: {item.Identity}");
            }
        }
    }

    private static void AssertResolved(EvaluatedMsBuildItem item)
    {
        foreach (string? value in new[]
                 {
                     item.Identity,
                     item.FullPath,
                     item.TargetPath,
                     item.Link,
                 })
        {
            if (value is not null &&
                (value.Contains("$(", StringComparison.Ordinal) ||
                 value.Contains("@(", StringComparison.Ordinal) ||
                 value.Contains("%(", StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Evaluated package closure retains unresolved expression '{value}'.");
            }
        }
    }

    private static string RequiredString(
        JsonElement element,
        string name,
        string itemType)
    {
        string? value = OptionalString(element, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException(
                $"Evaluated {itemType} item has no {name}.")
            : value;
    }

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    internal static string ResolveDotNetHost()
    {
        string hostFileName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        const string approvedSdk = @"C:\GEAI-Tools\dotnet-sdk-10.0.301\dotnet.exe";
        IReadOnlyList<string?> candidates = BuildDotNetHostCandidates(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"),
            Environment.ProcessPath,
            Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            approvedSdk,
            hostFileName);

        return SelectDotNetHost(candidates, hostFileName);
    }

    internal static IReadOnlyList<string?> BuildDotNetHostCandidates(
        string? explicitHost,
        string? currentProcess,
        string? dotnetRoot,
        string? programFiles,
        string approvedSdk,
        string hostFileName) =>
        [
            explicitHost,
            currentProcess,
            UnderRoot(dotnetRoot, hostFileName),
            UnderRoot(programFiles, "dotnet", hostFileName),
            approvedSdk,
        ];

    internal static string SelectDotNetHost(
        IReadOnlyList<string?> candidates,
        string hostFileName)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostFileName);
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (string? candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) ||
                !Path.IsPathFullyQualified(candidate) ||
                !string.Equals(
                    Path.GetFileName(candidate),
                    hostFileName,
                    pathComparison))
            {
                continue;
            }

            string concreteCandidate;
            try
            {
                concreteCandidate = Path.GetFullPath(candidate);
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (!string.Equals(candidate, concreteCandidate, pathComparison) ||
                !File.Exists(concreteCandidate))
            {
                continue;
            }

            return concreteCandidate;
        }

        throw new FileNotFoundException("No installed dotnet host is available.");
    }

    private static string? UnderRoot(string? root, params string[] segments) =>
        string.IsNullOrWhiteSpace(root)
            ? null
            : Path.Combine([root, .. segments]);

    private static string Bounded(string error, string output)
    {
        string value = string.Concat(error, Environment.NewLine, output).Trim();
        return value.Length <= 2_000 ? value : value[..2_000];
    }
}
