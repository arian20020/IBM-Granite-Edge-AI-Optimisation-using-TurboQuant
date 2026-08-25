using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;

public interface ILlmFitHardwareEvidenceProvider
{
    Task<LlmFitHardwareEvidence> CaptureAsync(
        VerifiedTrustedTool tool,
        CancellationToken cancellationToken);
}
