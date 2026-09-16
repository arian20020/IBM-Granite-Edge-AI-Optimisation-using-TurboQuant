using System.Collections.Generic;
using GraniteEdgeAI.Features.Prompting;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

public sealed record OpenVinoConfigurationCandidate(
    string ConfigurationId,
    string Device,
    string Backend,
    string Maturity,
    int MaximumContextTokens,
    int DefaultRequestedNewTokens,
    int MaximumRequestedNewTokens);

/// <summary>The single C1-approved initial official CPU capability.</summary>
public static class OpenVinoRouteCapability
{
    public const string RouteId = "openvino.official";
    public const string ConfigurationId = "openvino.official.cpu";
    public const string BackendLabel = "OpenVINO GenAI";
    public const string Device = "CPU";
    public const string Maturity = "Official MVP";
    public const int MaximumContextTokens = 4_096;
    public const int DefaultRequestedNewTokens = 512;
    public const int MaximumRequestedNewTokens = 512;
    public const int MaximumPromptUtf8Bytes = 64 * 1024;

    public static IReadOnlyList<OpenVinoConfigurationCandidate> Candidates
        { get; } =
    [
        new OpenVinoConfigurationCandidate(
            ConfigurationId,
            Device,
            BackendLabel,
            Maturity,
            MaximumContextTokens,
            DefaultRequestedNewTokens,
            MaximumRequestedNewTokens)
    ];

    public static PromptRouteCapability PromptCapability { get; } = new(
        PromptRouteKind.OpenVino,
        RouteId,
        ConfigurationId,
        BackendLabel,
        Device,
        Maturity,
        MaximumContextTokens,
        DefaultRequestedNewTokens,
        MaximumRequestedNewTokens);
}
