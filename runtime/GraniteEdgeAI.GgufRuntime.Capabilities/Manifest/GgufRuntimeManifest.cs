namespace GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

public enum GgufRuntimeArchitecture
{
    Any,
    X64,
    Arm64,
}

public enum GgufRuntimeFileRole
{
    Supervisor,
    Adapter,
    Dependency,
    License,
    NativeRuntimeCpu,
    NativeRuntimeVulkan,
    Quantizer,
}

public sealed record GgufRuntimeManifestEntry(
    string RelativePath,
    long Length,
    string Sha256,
    GgufRuntimeArchitecture Architecture,
    GgufRuntimeFileRole Role,
    string LicenseReference);

public sealed record GgufRuntimeManifest(
    int SchemaVersion,
    string RuntimeBuildId,
    string RuntimeSourceCommit,
    IReadOnlyList<string> BuildFlags,
    IReadOnlyList<GgufRuntimeManifestEntry> Files);
