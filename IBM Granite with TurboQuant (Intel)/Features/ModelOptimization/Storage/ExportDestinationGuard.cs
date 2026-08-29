using System;
using System.IO;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal readonly record struct ExportDestinationPaths(
    string Destination,
    string Temporary);

internal static class ExportDestinationGuard
{
    internal static ExportDestinationPaths RequireAbsentDirectory(
        string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath)
            || !Path.IsPathFullyQualified(destinationPath))
        {
            throw new ArgumentException(
                "A fully qualified export destination is required.",
                nameof(destinationPath));
        }

        string destination = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(destinationPath));
        string parent = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException(
                "The export destination has no parent directory.",
                nameof(destinationPath));
        StoragePathGuard.RequireNoReparseAncestors(parent);
        if (!Directory.Exists(parent)
            || (File.GetAttributes(parent) &
                (FileAttributes.ReparsePoint | FileAttributes.Directory))
                != FileAttributes.Directory)
        {
            throw new InvalidOperationException(
                "The export destination parent is invalid.");
        }
        StoragePathGuard.RequireNoReparseAncestors(destination);
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new IOException("The export destination already exists.");
        }

        string temporary = Path.Combine(
            parent,
            $".export-{Guid.NewGuid():N}.tmp");
        StoragePathGuard.RequireNoReparseAncestors(temporary);
        if (Directory.Exists(temporary) || File.Exists(temporary))
        {
            throw new IOException("The export temporary destination already exists.");
        }
        return new ExportDestinationPaths(destination, temporary);
    }
}
