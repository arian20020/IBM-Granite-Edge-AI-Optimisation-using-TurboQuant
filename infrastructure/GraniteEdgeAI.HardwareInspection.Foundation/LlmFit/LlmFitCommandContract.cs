using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;

public static class LlmFitCommandContract
{
    public const string ToolId = "llmfit";
    public const string Version = "1.1.9";
    public const string ExpectedVersionOutput = "llmfit 1.1.9";
    public const string VersionCommandIdentity = "version";
    public const string SystemCommandIdentity = "system";

    public static TrustedToolCommand CreateVersionCommand() =>
        new(VersionCommandIdentity, ["--version"]);

    public static TrustedToolCommand CreateSystemCommand() =>
        new(SystemCommandIdentity, ["--no-dashboard", "--json", "system"]);
}
