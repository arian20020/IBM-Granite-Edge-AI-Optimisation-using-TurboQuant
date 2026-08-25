namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

internal static class HardwareInspectionAcceptanceResultStore
{
    private const string TestRootName = "GraniteEdgeAI.HardwareInspection.Tests";
    private const string AcceptanceDirectoryName = "Acceptance";

    internal static bool IsResultToken(string value) =>
        value is not null &&
        value.Length == 32 &&
        value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static string GetResultPath(string token)
    {
        if (!IsResultToken(token))
        {
            throw new ArgumentException("The acceptance result token is invalid.", nameof(token));
        }

        string resultRoot = GetResultRoot();
        string resultPath = Path.GetFullPath(Path.Combine(resultRoot, $"{token}.json"));
        if (!string.Equals(
                Path.GetDirectoryName(resultPath),
                resultRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The acceptance result path escaped its root.");
        }

        return resultPath;
    }

    internal static void WriteAtomically(
        string token,
        ReadOnlySpan<byte> json,
        int maximumBytes)
    {
        string resultPath = GetResultPath(token);
        if (json.IsEmpty ||
            json.Contains((byte)'\r') ||
            json.Contains((byte)'\n') ||
            json.Length >= 3 &&
            json[0] == 0xef &&
            json[1] == 0xbb &&
            json[2] == 0xbf)
        {
            throw new ArgumentException(
                "The acceptance result framing is invalid.",
                nameof(json));
        }

        int lengthWithLf = checked(json.Length + 1);
        if (lengthWithLf > maximumBytes)
        {
            throw new InvalidOperationException("The acceptance result exceeds its bound.");
        }

        string resultRoot = Path.GetDirectoryName(resultPath) ??
            throw new InvalidOperationException("The acceptance result root is invalid.");
        Directory.CreateDirectory(resultRoot);
        string temporaryPath = Path.Combine(
            resultRoot,
            $".{token}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(json);
                stream.WriteByte(0x0a);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, resultPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string GetResultRoot() => Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        TestRootName,
        AcceptanceDirectoryName));
}
