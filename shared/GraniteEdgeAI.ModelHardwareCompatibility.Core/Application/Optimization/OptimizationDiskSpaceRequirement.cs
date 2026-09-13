namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>Path-private measurement of the volume used for optimisation.</summary>
public sealed record OptimizationDiskSpaceRequirement(
    ulong RequiredBytes,
    ulong AvailableBytes)
{
    public ulong AdditionalBytes => RequiredBytes > AvailableBytes
        ? RequiredBytes - AvailableBytes : 0;
}
