namespace GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

public sealed record VerifiedGgufRuntimePackage(
    string SupervisorExecutable,
    string AdapterExecutable,
    string RuntimeBuildId,
    string RuntimeSourceCommit,
    IReadOnlyList<string> BuildFlags,
    string? CpuRuntimeExecutable = null,
    string? VulkanRuntimeExecutable = null,
    string? QuantizerExecutable = null);
