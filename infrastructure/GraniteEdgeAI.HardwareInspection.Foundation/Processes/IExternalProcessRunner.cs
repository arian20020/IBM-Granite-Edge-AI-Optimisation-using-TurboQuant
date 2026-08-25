using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

public interface IExternalProcessRunner
{
    Task<ExternalProcessResult> RunAsync(
        VerifiedTrustedTool tool,
        ExternalProcessRequest request,
        CancellationToken cancellationToken);
}
