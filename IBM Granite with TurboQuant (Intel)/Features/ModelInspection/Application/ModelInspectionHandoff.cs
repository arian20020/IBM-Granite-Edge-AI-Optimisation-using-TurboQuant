using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

internal enum ModelInspectionRouteKind
{
    Gguf,
    OpenVino
}

/// <summary>
/// Carries the minimum validated Model Inspection identity into one local
/// Hardware Inspection journey. It deliberately contains no path or display
/// data.
/// </summary>
internal sealed class ModelInspectionHandoff
{
    internal const ushort CurrentSchemaVersion = 2;

    private static readonly Regex LowercaseSha256 = new(
        "^[0-9a-f]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal ModelInspectionHandoff(
        ushort schemaVersion,
        Guid modelInspectionHandoffId,
        Guid modelInspectionRunId,
        ModelInspectionOutcome outcome,
        string modelSha256,
        long modelLengthBytes,
        ModelInspectionRouteKind route = ModelInspectionRouteKind.Gguf)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "Only ModelInspectionHandoff schema version 2 is accepted.");
        }

        RequireUuidV4(modelInspectionHandoffId, nameof(modelInspectionHandoffId));
        RequireUuidV4(modelInspectionRunId, nameof(modelInspectionRunId));
        if (modelInspectionHandoffId == modelInspectionRunId)
        {
            throw new ArgumentException(
                "Handoff and Model run identities must have separate roles.",
                nameof(modelInspectionHandoffId));
        }

        if (outcome is not ModelInspectionOutcome.Ready and
            not ModelInspectionOutcome.ReadyWithWarnings)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Only eligible Model Inspection outcomes may cross this boundary.");
        }

        if (modelSha256 is null || !LowercaseSha256.IsMatch(modelSha256))
        {
            throw new ArgumentException(
                "Model identity must be a lowercase SHA-256 digest.",
                nameof(modelSha256));
        }

        if (modelLengthBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modelLengthBytes),
                modelLengthBytes,
                "Validated model length must be positive.");
        }

        SchemaVersion = schemaVersion;
        ModelInspectionHandoffId = modelInspectionHandoffId;
        ModelInspectionRunId = modelInspectionRunId;
        Outcome = outcome;
        ModelSha256 = modelSha256;
        ModelLengthBytes = modelLengthBytes;
        Route = route;
    }

    internal ushort SchemaVersion { get; }

    internal Guid ModelInspectionHandoffId { get; }

    internal Guid ModelInspectionRunId { get; }

    internal ModelInspectionOutcome Outcome { get; }

    internal string ModelSha256 { get; }

    internal long ModelLengthBytes { get; }

    internal ModelInspectionRouteKind Route { get; }

    internal static bool IsUuidV4(Guid value)
    {
        if (value == Guid.Empty)
        {
            return false;
        }

        string canonical = value.ToString("D", CultureInfo.InvariantCulture);
        char variant = canonical[19];
        return canonical[14] == '4' &&
            variant is '8' or '9' or 'a' or 'b';
    }

    private static void RequireUuidV4(Guid value, string parameterName)
    {
        if (!IsUuidV4(value))
        {
            throw new ArgumentException(
                "Identity must be a non-zero RFC 4122 UUID version 4.",
                parameterName);
        }
    }
}
