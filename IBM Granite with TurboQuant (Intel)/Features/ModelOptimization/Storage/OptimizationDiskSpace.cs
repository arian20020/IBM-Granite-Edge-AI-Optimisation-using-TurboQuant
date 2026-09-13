using System;
using System.IO;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal sealed class OptimizationDiskSpaceException(
    OptimizationDiskSpaceRequirement requirement) : IOException("Insufficient disk space for optimisation.")
{
    internal OptimizationDiskSpaceRequirement Requirement { get; } = requirement;
}

internal static class OptimizationDiskSpace
{
    internal static bool IsDiskFull(Exception exception) => exception is IOException
        && (exception.HResult & 0xffff) is 112 or 39;

    internal static ulong RequiredForGguf(ulong sourceBytes, ulong workingBytes) =>
        checked(sourceBytes + workingBytes);

    internal static void Require(string path, ulong requiredBytes)
    {
        string root = Path.GetPathRoot(Path.GetFullPath(path))
            ?? throw new IOException("The optimisation volume is unavailable.");
        var drive = new DriveInfo(root);
        var requirement = new OptimizationDiskSpaceRequirement(
            requiredBytes, checked((ulong)drive.AvailableFreeSpace));
        if (requirement.AdditionalBytes > 0)
        {
            throw new OptimizationDiskSpaceException(requirement);
        }
    }
}
