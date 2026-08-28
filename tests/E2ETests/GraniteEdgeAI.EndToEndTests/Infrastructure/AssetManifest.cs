using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed record AssetRecord(string Id, string Route, string Sha256, long Bytes);

internal sealed record AssetManifest(IReadOnlyList<AssetRecord> Assets)
{
    internal void VerifyPath(string route, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string[] files = File.Exists(fullPath)
            ? [fullPath]
            : Directory.Exists(fullPath)
                ? Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories)
                : throw new FileNotFoundException("The guarded asset path does not exist.", fullPath);
        if (files.Length == 0)
        {
            throw new InvalidDataException("The guarded asset directory is empty.");
        }

        foreach (string file in files)
        {
            FileInfo info = new(file);
            string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            if (!Assets.Any(asset => asset.Route == route && asset.Bytes == info.Length && asset.Sha256 == digest))
            {
                throw new InvalidDataException($"A guarded {route} asset file is not bound by the asset manifest.");
            }
        }
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
