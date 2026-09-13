namespace GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

public static class TrustedManifestFile
{
    public static byte[] ReadBounded(string path, int maximumBytes)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.IsPathFullyQualified(path) ||
            maximumBytes is <= 0 or > 16 * 1024 * 1024)
        {
            throw Invalid();
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            var file = new FileInfo(fullPath);
            if (!file.Exists || (file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw Invalid();
            }

            for (DirectoryInfo? directory = file.Directory;
                 directory is not null;
                 directory = directory.Parent)
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw Invalid();
                }
            }

            using FileStream stream = new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.SequentialScan);
            if (stream.Length is <= 0 || stream.Length > maximumBytes)
            {
                throw Invalid();
            }

            byte[] bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
            {
                throw Invalid();
            }

            return bytes;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or
                UnauthorizedAccessException or NotSupportedException or
                OverflowException)
        {
            throw Invalid();
        }
    }

    private static InvalidDataException Invalid() =>
        new("The trusted manifest file is invalid.");
}
