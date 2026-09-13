using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;
using SharedHandoff = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionHandoffV2;
using SharedOutcome = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionOutcomeV2;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

/// <summary>
/// Implements the only accepted local serialized form of a Model handoff.
/// </summary>
internal static class ModelInspectionHandoffCodec
{
    internal const int MaximumUtf8Bytes = SharedHandoff.MaximumCanonicalUtf8Bytes;

    internal static byte[] Serialize(ModelInspectionHandoff handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);

        return new SharedHandoff(
            handoff.SchemaVersion,
            handoff.ModelInspectionHandoffId,
            handoff.ModelInspectionRunId,
            ToSharedOutcome(handoff.Outcome),
            handoff.ModelSha256,
            handoff.ModelLengthBytes).ToCanonicalUtf8Json();
    }

    internal static bool TryDeserialize(
        ReadOnlySpan<byte> utf8Json,
        out ModelInspectionHandoff? handoff)
    {
        handoff = null;
        try
        {
            SharedHandoff shared = SharedHandoff.Parse(utf8Json);
            handoff = new ModelInspectionHandoff(
                shared.SchemaVersion,
                shared.ModelInspectionHandoffId,
                shared.ModelInspectionRunId,
                FromSharedOutcome(shared.Outcome),
                shared.ModelSha256,
                shared.ModelLengthBytes);
            return true;
        }
        catch (Exception error) when (
            error is GraniteEdgeAI.ModelInspection.Contracts.WorkerProtocolException or
            ArgumentException or
            InvalidOperationException or
            FormatException)
        {
            handoff = null;
            return false;
        }
    }

    private static SharedOutcome ToSharedOutcome(ModelInspectionOutcome outcome) =>
        outcome switch
        {
            ModelInspectionOutcome.Ready => SharedOutcome.Ready,
            ModelInspectionOutcome.ReadyWithWarnings =>
                SharedOutcome.ReadyWithWarnings,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };

    private static ModelInspectionOutcome FromSharedOutcome(SharedOutcome outcome) =>
        outcome switch
        {
            SharedOutcome.Ready => ModelInspectionOutcome.Ready,
            SharedOutcome.ReadyWithWarnings =>
                ModelInspectionOutcome.ReadyWithWarnings,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
}
