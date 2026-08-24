using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal static class LlamaCppProbeManifestParser
{
    private const int MaximumManifestBytes = 64 * 1024;
    private const string InvalidManifestMessage = "The packaged probe manifest is invalid.";
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private static readonly string[] RootProperties =
    [
        "schemaVersion",
        "toolId",
        "version",
        "executable",
        "executableSha256",
        "members",
        "machine",
        "disposition",
        "commands",
    ];
    private static readonly string[] CommandProperties = ["identity", "arguments"];

    internal static TrustedToolPackageManifest Parse(ReadOnlySpan<byte> bytes)
    {
        try
        {
            ValidateFraming(bytes);
            string json = StrictUtf8.GetString(bytes[..^1]);
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });

            JsonElement root = document.RootElement;
            RequireExactProperties(root, RootProperties);
            if (ReadInt32(root, "schemaVersion") != 1 ||
                !string.Equals(ReadString(root, "toolId"), LlamaCppCapabilityCommandContract.ToolId, StringComparison.Ordinal) ||
                !string.Equals(ReadString(root, "version"), LlamaCppCapabilityCommandContract.Version, StringComparison.Ordinal) ||
                !string.Equals(ReadString(root, "executable"), LlamaCppCapabilityCommandContract.ExecutableName, StringComparison.Ordinal) ||
                !string.Equals(ReadString(root, "machine"), nameof(PeMachine.Amd64), StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(root, "disposition"),
                    nameof(TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation),
                    StringComparison.Ordinal))
            {
                throw InvalidManifest();
            }

            string hash = ReadString(root, "executableSha256");
            if (!IsCanonicalSha256(hash))
            {
                throw InvalidManifest();
            }

            string[] members = ReadMembers(root.GetProperty("members"));
            TrustedToolCommand[] commands = ReadCommands(root.GetProperty("commands"));
            return new TrustedToolPackageManifest(
                LlamaCppCapabilityCommandContract.ToolId,
                LlamaCppCapabilityCommandContract.Version,
                LlamaCppCapabilityCommandContract.ExecutableName,
                hash,
                members,
                PeMachine.Amd64,
                TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation,
                commands);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or
                                      ArgumentException or InvalidOperationException or OverflowException)
        {
            throw InvalidManifest();
        }
    }

    private static void ValidateFraming(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > MaximumManifestBytes || bytes[^1] != (byte)'\n' ||
            bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
        {
            throw InvalidManifest();
        }

        int lineFeeds = 0;
        foreach (byte value in bytes)
        {
            if (value == (byte)'\r')
            {
                throw InvalidManifest();
            }

            if (value == (byte)'\n' && ++lineFeeds > 1)
            {
                throw InvalidManifest();
            }
        }

        if (lineFeeds != 1)
        {
            throw InvalidManifest();
        }
    }

    private static string[] ReadMembers(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw InvalidManifest();
        }

        List<string> members = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (members.Count == 64 || item.ValueKind != JsonValueKind.String)
            {
                throw InvalidManifest();
            }

            string member = item.GetString()!;
            if (!IsSafeLeafName(member))
            {
                throw InvalidManifest();
            }

            members.Add(member);
        }

        if (members.Count == 0 ||
            members.Distinct(StringComparer.OrdinalIgnoreCase).Count() != members.Count ||
            !members.SequenceEqual(members.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            !members.Contains(LlamaCppCapabilityCommandContract.ExecutableName, StringComparer.Ordinal))
        {
            throw InvalidManifest();
        }

        return members.ToArray();
    }

    private static TrustedToolCommand[] ReadCommands(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw InvalidManifest();
        }

        JsonElement[] commands = element.EnumerateArray().ToArray();
        if (commands.Length != 2)
        {
            throw InvalidManifest();
        }

        RequireCommand(commands[0], "identity", ["identity", "--format", "json-v1"]);
        RequireCommand(commands[1], "capabilities", ["capabilities", "--format", "json-v1"]);
        return
        [
            LlamaCppCapabilityCommandContract.CreateIdentityCommand(),
            LlamaCppCapabilityCommandContract.CreateCapabilitiesCommand(),
        ];
    }

    private static void RequireCommand(JsonElement element, string identity, string[] arguments)
    {
        RequireExactProperties(element, CommandProperties);
        if (!string.Equals(ReadString(element, "identity"), identity, StringComparison.Ordinal))
        {
            throw InvalidManifest();
        }

        JsonElement argumentElement = element.GetProperty("arguments");
        if (argumentElement.ValueKind != JsonValueKind.Array)
        {
            throw InvalidManifest();
        }

        string[] actual = argumentElement.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()! : throw InvalidManifest())
            .ToArray();
        if (!actual.SequenceEqual(arguments, StringComparer.Ordinal))
        {
            throw InvalidManifest();
        }
    }

    private static void RequireExactProperties(JsonElement element, IReadOnlyCollection<string> expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw InvalidManifest();
        }

        string[] actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != expected.Count ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected))
        {
            throw InvalidManifest();
        }
    }

    private static int ReadInt32(JsonElement element, string propertyName)
    {
        JsonElement property = element.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int value)
            ? value
            : throw InvalidManifest();
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        JsonElement property = element.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.String && property.GetString() is string value
            ? value
            : throw InvalidManifest();
    }

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSafeLeafName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value is not "." and not ".." &&
        value.IndexOf('\0') < 0 &&
        !Path.IsPathRooted(value) &&
        string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal) &&
        value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;

    private static InvalidDataException InvalidManifest() => new(InvalidManifestMessage);
}
