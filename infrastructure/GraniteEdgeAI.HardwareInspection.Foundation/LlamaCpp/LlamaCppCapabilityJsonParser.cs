using System.Text;
using System.Text.Json;
using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;

internal sealed record LlamaCppIdentityParseResult(
    bool IsValid,
    LlamaCppRuntimeIdentity? RuntimeIdentity)
{
    internal static LlamaCppIdentityParseResult Invalid { get; } = new(false, null);
}

internal sealed record LlamaCppCapabilitiesParseResult(
    bool IsValid,
    IReadOnlyList<LlamaCppBackend> Backends,
    IReadOnlyList<LlamaCppVisibleDevice> Devices)
{
    internal static LlamaCppCapabilitiesParseResult Invalid { get; } = new(
        false,
        Array.AsReadOnly(Array.Empty<LlamaCppBackend>()),
        Array.AsReadOnly(Array.Empty<LlamaCppVisibleDevice>()));
}

internal static class LlamaCppCapabilityJsonParser
{
    private const int MaximumProtocolBytes = 64 * 1024;
    private const string ProbeIdentity = "granite-edge-hardware-llamacpp-capabilities/1";
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    internal static LlamaCppIdentityParseResult ParseIdentity(string? value)
    {
        if (!TryEncodeFramedJson(value, out byte[] json))
        {
            return LlamaCppIdentityParseResult.Invalid;
        }

        try
        {
            var reader = CreateReader(json);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return LlamaCppIdentityParseResult.Invalid;
            }

            const int requiredMask = (1 << 9) - 1;
            int seen = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    return LlamaCppIdentityParseResult.Invalid;
                }

                string? property = reader.GetString();
                int bit = property switch
                {
                    "schemaVersion" => 1 << 0,
                    "probeIdentity" => 1 << 1,
                    "managedPackage" => 1 << 2,
                    "managedVersion" => 1 << 3,
                    "backendPackage" => 1 << 4,
                    "backendVersion" => 1 << 5,
                    "llamaSharpCommit" => 1 << 6,
                    "mappedLlamaCppCommit" => 1 << 7,
                    "runtimeIdentifier" => 1 << 8,
                    _ => 0,
                };
                if (bit == 0 || (seen & bit) != 0 || !reader.Read())
                {
                    return LlamaCppIdentityParseResult.Invalid;
                }

                bool matches = property switch
                {
                    "schemaVersion" =>
                        reader.TokenType == JsonTokenType.Number &&
                        reader.TryGetInt32(out int version) &&
                        version == 1,
                    "probeIdentity" => IsExactString(ref reader, ProbeIdentity),
                    "managedPackage" => IsExactString(ref reader, "LLamaSharp"),
                    "managedVersion" => IsExactString(ref reader, "0.27.0"),
                    "backendPackage" => IsExactString(ref reader, "LLamaSharp.Backend.Cpu"),
                    "backendVersion" => IsExactString(ref reader, "0.27.0"),
                    "llamaSharpCommit" => IsExactString(
                        ref reader,
                        "7cbbc45e421d55794d5050d126e0b96511007007"),
                    "mappedLlamaCppCommit" => IsExactString(
                        ref reader,
                        "3f7c29d318e317b63f54c558bc69803963d7d88c"),
                    "runtimeIdentifier" => IsExactString(ref reader, "win-x64"),
                    _ => false,
                };
                if (!matches)
                {
                    return LlamaCppIdentityParseResult.Invalid;
                }

                seen |= bit;
            }

            return reader.TokenType == JsonTokenType.EndObject &&
                seen == requiredMask &&
                !reader.Read()
                ? new LlamaCppIdentityParseResult(true, LlamaCppRuntimeIdentity.PinnedCpu)
                : LlamaCppIdentityParseResult.Invalid;
        }
        catch (JsonException)
        {
            return LlamaCppIdentityParseResult.Invalid;
        }
    }

    internal static LlamaCppCapabilitiesParseResult ParseCapabilities(string? value)
    {
        if (!TryEncodeFramedJson(value, out byte[] json))
        {
            return LlamaCppCapabilitiesParseResult.Invalid;
        }

        var backends = new List<LlamaCppBackend>(capacity: 1);
        var devices = new List<LlamaCppVisibleDevice>(capacity: 16);
        try
        {
            var reader = CreateReader(json);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return LlamaCppCapabilitiesParseResult.Invalid;
            }

            const int requiredMask = (1 << 4) - 1;
            int seen = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    return InvalidCapabilities(backends, devices);
                }

                string? property = reader.GetString();
                int bit = property switch
                {
                    "schemaVersion" => 1 << 0,
                    "probeIdentity" => 1 << 1,
                    "backends" => 1 << 2,
                    "devices" => 1 << 3,
                    _ => 0,
                };
                if (bit == 0 || (seen & bit) != 0 || !reader.Read())
                {
                    return InvalidCapabilities(backends, devices);
                }

                bool valid = property switch
                {
                    "schemaVersion" =>
                        reader.TokenType == JsonTokenType.Number &&
                        reader.TryGetInt32(out int version) &&
                        version == 1,
                    "probeIdentity" => IsExactString(ref reader, ProbeIdentity),
                    "backends" => TryReadBackends(ref reader, backends),
                    "devices" => TryReadDevices(ref reader, devices),
                    _ => false,
                };
                if (!valid)
                {
                    return InvalidCapabilities(backends, devices);
                }

                seen |= bit;
            }

            if (reader.TokenType != JsonTokenType.EndObject ||
                seen != requiredMask ||
                reader.Read() ||
                backends.Count != 1 ||
                backends[0] != LlamaCppBackend.Cpu ||
                devices.Count == 0)
            {
                return InvalidCapabilities(backends, devices);
            }

            return new(
                true,
                Array.AsReadOnly(backends.ToArray()),
                Array.AsReadOnly(devices.ToArray()));
        }
        catch (JsonException)
        {
            return InvalidCapabilities(backends, devices);
        }
    }

    private static Utf8JsonReader CreateReader(byte[] json) => new(
        json,
        new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8,
        });

    private static bool TryReadBackends(
        ref Utf8JsonReader reader,
        List<LlamaCppBackend> backends)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            return false;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (backends.Count == 8 ||
                !IsExactString(ref reader, "cpu") ||
                backends.Contains(LlamaCppBackend.Cpu))
            {
                return false;
            }

            backends.Add(LlamaCppBackend.Cpu);
        }

        return reader.TokenType == JsonTokenType.EndArray && backends.Count == 1;
    }

    private static bool TryReadDevices(
        ref Utf8JsonReader reader,
        List<LlamaCppVisibleDevice> devices)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            return false;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (devices.Count == 16 ||
                reader.TokenType != JsonTokenType.StartObject ||
                !TryReadDevice(ref reader, devices.Count, out LlamaCppVisibleDevice? device))
            {
                return false;
            }

            devices.Add(device!);
        }

        return reader.TokenType == JsonTokenType.EndArray && devices.Count > 0;
    }

    private static bool TryReadDevice(
        ref Utf8JsonReader reader,
        int expectedOrdinal,
        out LlamaCppVisibleDevice? device)
    {
        device = null;
        int seen = 0;
        int ordinal = -1;
        string? bufferType = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                return false;
            }

            string? property = reader.GetString();
            int bit = property switch
            {
                "ordinal" => 1,
                "bufferType" => 2,
                _ => 0,
            };
            if (bit == 0 || (seen & bit) != 0 || !reader.Read())
            {
                return false;
            }

            if (property == "ordinal")
            {
                if (reader.TokenType != JsonTokenType.Number ||
                    !reader.TryGetInt32(out ordinal))
                {
                    return false;
                }
            }
            else
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    return false;
                }

                bufferType = reader.GetString();
            }

            seen |= bit;
        }

        if (reader.TokenType != JsonTokenType.EndObject ||
            seen != 3 ||
            ordinal != expectedOrdinal ||
            !HardwareText.IsSafe(bufferType, 128))
        {
            return false;
        }

        device = new LlamaCppVisibleDevice(ordinal, bufferType!);
        return true;
    }

    private static bool IsExactString(ref Utf8JsonReader reader, string expected) =>
        reader.TokenType == JsonTokenType.String &&
        reader.ValueTextEquals(expected);

    private static bool TryEncodeFramedJson(string? value, out byte[] json)
    {
        json = [];
        if (string.IsNullOrEmpty(value) ||
            value[0] == '\ufeff' ||
            value[^1] != '\n' ||
            (value.Length > 1 && value[^2] == '\r'))
        {
            return false;
        }

        ReadOnlySpan<char> body = value.AsSpan(0, value.Length - 1);
        if (body.ContainsAny('\r', '\n'))
        {
            return false;
        }

        try
        {
            json = StrictUtf8.GetBytes(body.ToString());
            return json.Length <= MaximumProtocolBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static LlamaCppCapabilitiesParseResult InvalidCapabilities(
        List<LlamaCppBackend> backends,
        List<LlamaCppVisibleDevice> devices)
    {
        backends.Clear();
        devices.Clear();
        return LlamaCppCapabilitiesParseResult.Invalid;
    }
}
