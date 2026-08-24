using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>
/// The one implementation each seam ships today: a typed refusal with a stable
/// reason.
///
/// This is what lets the whole engine be built and tested before any owner
/// publishes a contract, and it is why the production page can honestly report
/// that no compatibility conclusion is available rather than showing a guess.
/// </summary>
internal static class UnavailablePorts
{
    internal static IInspectedModelFactsSource ModelFacts() => new UnavailableModelFacts();

    internal static IHardwareFactsSource HardwareFacts() => new UnavailableHardwareFacts();

    internal static IFreshSystemMemoryProbe MemoryProbe() => new UnavailableMemoryProbe();

    internal static ICompatibilityInputGateway Gateway() => new UnavailableGateway();

    internal static IRuntimeVerificationRunner VerificationRunner() =>
        new UnavailableVerificationRunner();

    private sealed class UnavailableModelFacts : IInspectedModelFactsSource
    {
        public ModelFactsResolution Resolve(string modelInspectionRunId) =>
            ModelFactsResolution.Unavailable(PortUnavailableReason.AdapterNotImplemented);
    }

    private sealed class UnavailableHardwareFacts : IHardwareFactsSource
    {
        public HardwareFactsResolution Resolve(string productHardwareRunId) =>
            HardwareFactsResolution.Unavailable(PortUnavailableReason.AdapterNotImplemented);
    }

    private sealed class UnavailableMemoryProbe : IFreshSystemMemoryProbe
    {
        // Returns no reading rather than a zero one: zero would be
        // indistinguishable from a machine with no free memory, and the gate
        // would then refuse for the wrong reason.
        public FreshMemoryReading Probe() =>
            FreshMemoryReading.Unavailable(PortUnavailableReason.AdapterNotImplemented);
    }

    private sealed class UnavailableGateway : ICompatibilityInputGateway
    {
        public HandoffClaim Claim() =>
            HandoffClaim.Refused(PortUnavailableReason.AdapterNotImplemented);

        public void Rollback()
        {
            // Nothing was claimed, so nothing is released. Callers roll back on
            // every failure path without first asking whether a claim succeeded.
        }

        public bool Commit(CompatibilityRunId runId) => false;
    }

    private sealed class UnavailableVerificationRunner : IRuntimeVerificationRunner
    {
        public VerificationOutcome Verify(CompatibilityRunId runId) =>
            VerificationOutcome.Unavailable(PortUnavailableReason.RunnerNotRegistered);
    }
}
