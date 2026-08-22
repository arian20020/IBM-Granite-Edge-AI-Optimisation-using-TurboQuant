using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

/// <summary>
/// Implements the only accepted local serialized form of a Model handoff.
/// </summary>
internal static class ModelInspectionHandoffCodec
{
    internal const int MaximumUtf8Bytes = 512;

    private static readonly string[] ExactPropertyOrder =
    [
        "schemaVersion",
        "modelInspectionHandoffId",
        "modelInspectionRunId",
        "outcome",
        "modelSha256",
        "modelLengthBytes"
    ];

    internal static byte[] Serialize(ModelInspectionHandoff handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);

        var buffer = new ArrayBufferWriter<byte>(MaximumUtf8Bytes);
        using (var writer = new Utf8JsonWriter(
            buffer,
            new JsonWriterOptions
            {
                Indented = false,
                SkipValidation = false
            }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", handoff.SchemaVersion);
            writer.WriteString(
                "modelInspectionHandoffId",
                handoff.ModelInspectionHandoffId.ToString(
                    "D",
                    CultureInfo.InvariantCulture));
            writer.WriteString(
                "modelInspectionRunId",
                handoff.ModelInspectionRunId.ToString(
                    "D",
                    CultureInfo.InvariantCulture));
            writer.WriteString("outcome", handoff.Outcome.ToString());
            writer.WriteString("modelSha256", handoff.ModelSha256);
            writer.WriteNumber("modelLengthBytes", handoff.ModelLengthBytes);
            writer.WriteEndObject();
        }

        if (buffer.WrittenCount > MaximumUtf8Bytes)
        {
            throw new InvalidOperationException(
                "The canonical Model Inspection handoff exceeds its size limit.");
        }

        return buffer.WrittenSpan.ToArray();
    }

    internal static bool TryDeserialize(
        ReadOnlySpan<byte> utf8Json,
        out ModelInspectionHandoff? handoff)
    {
        handoff = null;
        if (utf8Json.IsEmpty || utf8Json.Length > MaximumUtf8Bytes)
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 2
                });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (index >= ExactPropertyOrder.Length ||
                    !string.Equals(
                        property.Name,
                        ExactPropertyOrder[index],
                        StringComparison.Ordinal) ||
                    !names.Add(property.Name))
                {
                    return false;
                }

                index++;
            }

            if (index != ExactPropertyOrder.Length)
            {
                return false;
            }

            ushort schemaVersion = root
                .GetProperty("schemaVersion")
                .GetUInt16();
            Guid handoffId = Guid.ParseExact(
                root.GetProperty("modelInspectionHandoffId").GetString()!,
                "D");
            Guid modelRunId = Guid.ParseExact(
                root.GetProperty("modelInspectionRunId").GetString()!,
                "D");
            string outcomeText = root.GetProperty("outcome").GetString()!;
            ModelInspectionOutcome outcome = outcomeText switch
            {
                "Ready" => ModelInspectionOutcome.Ready,
                "ReadyWithWarnings" =>
                    ModelInspectionOutcome.ReadyWithWarnings,
                _ => throw new ArgumentOutOfRangeException(nameof(outcomeText))
            };
            string digest = root.GetProperty("modelSha256").GetString()!;
            long length = root.GetProperty("modelLengthBytes").GetInt64();

            var candidate = new ModelInspectionHandoff(
                schemaVersion,
                handoffId,
                modelRunId,
                outcome,
                digest,
                length);
            byte[] canonical = Serialize(candidate);
            if (!utf8Json.SequenceEqual(canonical))
            {
                return false;
            }

            handoff = candidate;
            return true;
        }
        catch (Exception error) when (
            error is JsonException or
            ArgumentException or
            InvalidOperationException or
            FormatException or
            OverflowException)
        {
            handoff = null;
            return false;
        }
    }
}
