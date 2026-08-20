namespace GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

public sealed record VerifiedGgufRuntimePackage(
    string SupervisorExecutable,
    string CliExecutable,
    string RuntimeBuildId,
    string RuntimeSourceCommit,
    IReadOnlyList<string> BuildFlags);
