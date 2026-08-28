using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed record AssetRecord(string Id, string Route, string Sha256, long Bytes);

internal sealed record AssetManifest(IReadOnlyList<AssetRecord> Assets)
{
    internal void VerifyPath(string route, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string[] files = EnumerateRegularFiles(fullPath);
        if (files.Length == 0)
        {
            throw new InvalidDataException("The guarded asset directory is empty.");
        }

        foreach (string file in files)
        {
            FileInfo info = new(file);
            if (!info.Exists
                || info.Length < 0
                || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"A guarded {route} asset is not a regular file.");
            }

            string digest;
            using (FileStream stream = info.OpenRead())
            {
                digest = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(stream))
                    .ToLowerInvariant();
                if (stream.Length != info.Length)
                {
                    throw new InvalidDataException(
                        $"A guarded {route} asset changed during verification.");
                }
            }
            if (!Assets.Any(asset => asset.Route == route && asset.Bytes == info.Length && asset.Sha256 == digest))
            {
                throw new InvalidDataException($"A guarded {route} asset file is not bound by the asset manifest.");
            }
        }
    }

    private static string[] EnumerateRegularFiles(string path)
    {
        if (File.Exists(path))
        {
            return [path];
        }
        if (!Directory.Exists(path))
        {
            throw new FileNotFoundException(
                "The guarded asset path does not exist.",
                path);
        }

        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    "A guarded asset directory cannot be a reparse point.");
            }

            files.AddRange(Directory.EnumerateFiles(directory));
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        "A guarded asset directory cannot contain a reparse point.");
                }

                pending.Push(child);
            }
        }

        return files.ToArray();
    }

    internal static AssetManifest Load(string path)
    {
        using JsonDocument document = JsonContract.Open(path);
        JsonElement root = document.RootElement;
        JsonContract.RequireOnly(root, "schemaVersion", "assets");
        if (JsonContract.RequiredInt64(root, "schemaVersion") != 1 || !root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Asset manifest must use schemaVersion 1 and an assets array.");
        }

        List<AssetRecord> records = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonElement asset in assets.EnumerateArray())
        {
            JsonContract.RequireOnly(asset, "id", "route", "sha256", "bytes");
            string id = JsonContract.RequiredString(asset, "id");
            string route = JsonContract.RequiredString(asset, "route");
            string sha256 = JsonContract.RequiredString(asset, "sha256");
            long bytes = JsonContract.RequiredInt64(asset, "bytes");
            if (!ids.Add(id))
            {
                throw new InvalidDataException($"Duplicate asset id '{id}'.");
            }

            if (route is not ("gguf" or "openvino") || bytes < 0)
            {
                throw new InvalidDataException($"Asset '{id}' has an invalid route or byte length.");
            }

            JsonContract.RequireSha256(sha256, "sha256");
            records.Add(new AssetRecord(id, route, sha256, bytes));
        }

        return records.Count > 0 ? new AssetManifest(records) : throw new InvalidDataException("Asset manifest must contain at least one asset.");
    }
}
