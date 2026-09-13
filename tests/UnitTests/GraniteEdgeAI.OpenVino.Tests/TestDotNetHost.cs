namespace GraniteEdgeAI.OpenVino.Tests;

internal static class TestDotNetHost
{
    internal static string Resolve()
    {
        string hostFileName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        string? programFiles = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles);
        string approvedSdk = OperatingSystem.IsWindows()
            ? @"C:\GEAI-Tools\dotnet-sdk-10.0.301\dotnet.exe"
            : string.Empty;

        string?[] candidates =
        [
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"),
            Environment.ProcessPath,
            UnderRoot(dotnetRoot, hostFileName),
            UnderRoot(programFiles, "dotnet", hostFileName),
            approvedSdk,
        ];

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        foreach (string? candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)
                || !Path.IsPathFullyQualified(candidate)
                || !string.Equals(
                    Path.GetFileName(candidate), hostFileName, comparison))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (string.Equals(candidate, fullPath, comparison)
                && File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new FileNotFoundException("No installed dotnet host is available.");
    }

    private static string? UnderRoot(string? root, params string[] segments) =>
        string.IsNullOrWhiteSpace(root)
            ? null
            : Path.Combine([root, .. segments]);
}
