using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal enum HardwareEvidenceCollectionFailureCode
{
    ProviderUnavailable,
    OrchestrationFailure,
    ProgressCallbackFailure,
}

internal sealed class HardwareEvidenceCollectionResult
{
    private HardwareEvidenceCollectionResult(
        CollectedHardwareEvidence? evidence,
        HardwareEvidenceCollectionFailureCode? failureCode)
    {
        Evidence = evidence;
        FailureCode = failureCode;
    }

    internal bool IsSuccess => Evidence is not null;

    internal CollectedHardwareEvidence? Evidence { get; }

    internal HardwareEvidenceCollectionFailureCode? FailureCode { get; }

    internal static HardwareEvidenceCollectionResult Success(
        CollectedHardwareEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new(evidence, failureCode: null);
    }

    internal static HardwareEvidenceCollectionResult Failure(
        HardwareEvidenceCollectionFailureCode failureCode)
    {
        if (!Enum.IsDefined(failureCode))
        {
            throw new ArgumentOutOfRangeException(nameof(failureCode));
        }

        return new(evidence: null, failureCode);
    }
}
