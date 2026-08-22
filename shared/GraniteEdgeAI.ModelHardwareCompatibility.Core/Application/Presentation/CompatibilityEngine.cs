using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// The way in.
///
/// Everything the engine does is internal; this is the single door, and what it
/// returns is a decision plus the codes explaining it. A caller cannot reach the
/// estimator, the generator or the policies, which is deliberate — those are
/// reasoning, and a screen that touched them would be re-deriving the answer
/// rather than showing it.
/// </summary>
public static class CompatibilityEngine
{
    /// <summary>
    /// Runs a check with the adapters that exist today.
    ///
    /// None of them do. Every seam this needs — the paired handoffs, the model
    /// facts, the machine facts, the reading of memory free right now — ships a
    /// typed refusal until the teams that own them publish a contract. So this
    /// reliably returns "no answer yet", naming exactly what was missing, and
    /// that is the honest production behaviour rather than a placeholder.
    ///
    /// When an adapter is written it is supplied here, and the same code path
    /// starts producing real conclusions without anything else changing.
    /// </summary>
    public static CompatibilityScreenModel RunWithAvailableAdapters(
        CancellationToken cancellationToken = default)
    {
        CompatibilityRunResult result = CompatibilityRunCoordinator.Execute(
            new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
            CompatibilityRunDependencies.Create(
                UnavailablePorts.Gateway(),
                UnavailablePorts.ModelFacts(),
                UnavailablePorts.HardwareFacts(),
                UnavailablePorts.MemoryProbe(),
                SupportMatrix.ProvisionalV1(),
                EstimatorPolicy.ProvisionalV1(),
                SafetyPolicy.ProvisionalV1(),
                new HashSet<string>(),
                TrustedSourceAvailability.None(),
                // The llama.cpp default stands in for what the user actually
                // imported until an adapter can report it.
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None),
                ContextTokenCount.FromTokens(4096),
                TimeProvider.System),
            cancellationToken);

        return CompatibilityScreenModel.From(result);
    }
}
