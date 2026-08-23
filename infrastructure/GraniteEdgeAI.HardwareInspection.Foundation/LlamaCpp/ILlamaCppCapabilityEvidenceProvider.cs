using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;

public interface ILlamaCppCapabilityEvidenceProvider
{
    Task<LlamaCppCapabilityEvidence> CaptureAsync(
        VerifiedTrustedTool tool,
        CancellationToken cancellationToken);
}
