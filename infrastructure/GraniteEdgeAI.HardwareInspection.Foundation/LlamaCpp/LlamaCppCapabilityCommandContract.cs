using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;

public static class LlamaCppCapabilityCommandContract
{
    public const string ToolId = "granite-edge-hardware-llamacpp-probe";
    public const string Version = "0.27.0-cpu-win-x64";
    public const string ExecutableName = "GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe";
    public const string IdentityCommandIdentity = "identity";
    public const string CapabilitiesCommandIdentity = "capabilities";
    public const int UsageExitCode = 64;
    public const int NativeUnavailableExitCode = 70;
    public const int OutputFailureExitCode = 74;

    public static TrustedToolCommand CreateIdentityCommand() =>
        new(IdentityCommandIdentity, ["identity", "--format", "json-v1"]);

    public static TrustedToolCommand CreateCapabilitiesCommand() =>
        new(CapabilitiesCommandIdentity, ["capabilities", "--format", "json-v1"]);
}
