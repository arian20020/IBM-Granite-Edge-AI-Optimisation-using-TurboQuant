using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Failures;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Configuration;

public sealed record GgufCapabilityValidationResult(
    bool IsSupported,
    GgufRuntimeConfiguration Configuration,
    GgufRuntimeFailure? Failure);
