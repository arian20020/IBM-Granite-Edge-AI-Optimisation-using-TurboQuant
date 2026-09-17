using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal enum GgufSavedProfileDisposition { Absent, Restored, Rejected }
// This is an integrity-bound saved selection, not proof of runtime activation.
internal sealed record GgufSavedRuntimeProfile(string KeyCache, string ValueCache, int Context,
    string Backend, string Device, int GpuLayers, bool FlashAttention,
    int ThreadCount, int BatchSize, int MaximumGeneratedTokens);
internal static class GgufSavedRuntimeProfileReader
{
    internal static GgufSavedProfileDisposition Read(string modelPath, string modelHash,
        ulong modelLength, string runtimeBuild, string runtimeCommit,
        out GgufSavedRuntimeProfile? profile)
    {
        profile = null;
        try
        {
            string root = Path.GetDirectoryName(Path.GetFullPath(modelPath))!;
            string profilePath = Path.Combine(root, "runtime-profile.json");
            string manifestPath = Path.Combine(root, "bundle-manifest.json");
            if (!ExistsOrThrow(profilePath) && !ExistsOrThrow(manifestPath))
                return GgufSavedProfileDisposition.Absent;
            RequireRegularPath(modelPath);
            if (!string.Equals(Path.GetFileName(modelPath), "model.gguf", StringComparison.Ordinal)
                || new FileInfo(modelPath).Length != checked((long)modelLength))
                return GgufSavedProfileDisposition.Rejected;
            byte[] profileBytes = ReadBounded(profilePath);
            using JsonDocument manifestDocument = JsonDocument.Parse(ReadBounded(manifestPath));
            using JsonDocument profileDocument = JsonDocument.Parse(profileBytes);
            JsonElement manifest = manifestDocument.RootElement;
            JsonElement data = profileDocument.RootElement;
            if (!UniqueProperties(manifest) || !UniqueProperties(data)
                || Text(manifest, "schema") != "granite.gguf-runtime-profile-bundle.v1"
                || Text(manifest, "route") != "gguf"
                || Text(manifest, "profile_claim") != "issued-declared-not-runtime-validated"
                || Text(data, "schema") != "granite.gguf-issued-runtime-profile.v1"
                || Text(data, "validation_claim") != "declared-not-runtime-validated"
                || Text(manifest, "source_sha256") != modelHash
                || manifest.GetProperty("source_length_bytes").GetUInt64() != modelLength
                || Text(data, "runtime_build_id") != runtimeBuild
                || Text(data, "runtime_source_commit") != runtimeCommit
                || Text(data, "persistent_target_weight_format") != "imported"
                || data.GetProperty("requires_persistent_conversion").GetBoolean()
                // The issuance digest is an opaque cross-document binding, not
                // an admission/activation claim. Fresh authority checks every setting.
                || !IsHash(Text(data, "configuration_sha256"))
                || Text(data, "configuration_sha256") != Text(manifest, "configuration_sha256"))
                return GgufSavedProfileDisposition.Rejected;
            JsonElement[] members = manifest.GetProperty("members").EnumerateArray().ToArray();
            if (members.Length != 2 || members.Any(member => !UniqueProperties(member)))
                return GgufSavedProfileDisposition.Rejected;
            string profileHash = Convert.ToHexString(SHA256.HashData(profileBytes)).ToLowerInvariant();
            if (members.Count(member => MemberMatches(member, "model.gguf", modelHash, modelLength)) != 1
                || members.Count(member => MemberMatches(member, "runtime-profile.json", profileHash,
                    checked((ulong)profileBytes.Length))) != 1)
                return GgufSavedProfileDisposition.Rejected;
            string key = Text(data, "key_cache_type");
            string value = Text(data, "value_cache_type");
            if (!KnownCache(key) || !KnownCache(value)) return GgufSavedProfileDisposition.Rejected;
            profile = new(key, value, data.GetProperty("context_size").GetInt32(),
                Text(data, "backend"), Text(data, "device_id"), data.GetProperty("gpu_layer_count").GetInt32(),
                data.GetProperty("flash_attention").GetBoolean(), data.GetProperty("thread_count").GetInt32(),
                data.GetProperty("batch_size").GetInt32(), data.GetProperty("maximum_generated_tokens").GetInt32());
            if (profile.Context < 512 || profile.ThreadCount < 1 || profile.BatchSize < 1
                || profile.MaximumGeneratedTokens < 1 || profile.GpuLayers < 0)
            {
                profile = null;
                return GgufSavedProfileDisposition.Rejected;
            }
            return GgufSavedProfileDisposition.Restored;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or JsonException
            or KeyNotFoundException or OverflowException or FormatException)
        {
            profile = null;
            return GgufSavedProfileDisposition.Rejected;
        }
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()!;
    private static bool ExistsOrThrow(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
    }
    private static bool KnownCache(string value) => value is "f16" or "q8_0" or "q4_0" or "turbo2" or "turbo3" or "turbo4";
    private static bool IsHash(string value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool MemberMatches(JsonElement member, string name, string hash, ulong length) =>
        Text(member, "name") == name && Text(member, "sha256") == hash
        && member.GetProperty("length_bytes").GetUInt64() == length;
    private static bool UniqueProperties(JsonElement value) => value.ValueKind == JsonValueKind.Object
        && value.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count()
            == value.EnumerateObject().Count();
    private static byte[] ReadBounded(string path)
    {
        RequireRegularPath(path);
        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is < 1 or > 65536) throw new InvalidDataException("Saved profile size is invalid.");
        byte[] bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        RequireRegularPath(path);
        return bytes;
    }
    private static void RequireRegularPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.AsSpan(Path.GetPathRoot(full)!.Length).Contains(':'))
            throw new InvalidDataException("Alternate stream paths are not accepted.");
        if ((File.GetAttributes(full) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new InvalidDataException("An ordinary file is required.");
        for (DirectoryInfo? parent = Directory.GetParent(full); parent is not null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked ancestry is not accepted.");
    }
}
