namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// Everything a caller supplies: the user's context intent, and nothing else.
///
/// Recorded deviation, spec section 7. The approved decision has the request
/// carrying both handoff objects directly. Those types cannot be referenced today
/// and guessing their shape is prohibited, so the handoffs are reached through
/// ICompatibilityInputGateway, which owns which handoffs are current and performs
/// the claim. Any future change is confined to this record and one port signature.
///
/// The request deliberately carries no run id, availability figure, policy
/// version, progress sink or cancellation token: those are the coordinator's to
/// create or a port's to resolve, and letting a caller supply one is how a stale
/// number reaches a safety gate.
/// </summary>
internal sealed record CompatibilityRunRequest(CompatibilityContextRequest Context);
