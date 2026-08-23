using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal static class HardwareResolutionPolicy
{
    internal const string PolicyVersion = "hardware-policy-v1";
    internal const ushort SchemaVersion = 1;
    internal const ulong InstalledMemoryToleranceBytes = 1UL << 30;
    internal const ulong MinimumAvailableMemoryToleranceBytes = 2UL << 30;

    internal static readonly TimeSpan FutureClockSkew = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan DynamicMemoryMaximumAge = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan StaticEvidenceMaximumAge = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan MaximumCaptureSpan = TimeSpan.FromSeconds(60);
}
