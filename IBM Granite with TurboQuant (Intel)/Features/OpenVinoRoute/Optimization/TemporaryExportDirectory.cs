using System;
using System.IO;
using GraniteEdgeAI.Features.ModelOptimization.Storage;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

internal static class TemporaryExportDirectory
{
    internal static void Cleanup(string temporary, bool operationWasCancelled)
    {
        try
        {
            StoragePathGuard.RequireNoReparseAncestors(temporary);
            if (!Directory.Exists(temporary))
            {
                return;
            }
            FileAttributes rootAttributes = File.GetAttributes(temporary);
            if ((rootAttributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException();
            }

            foreach (string entry in Directory.EnumerateFileSystemEntries(
                         temporary,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                StoragePathGuard.RequireNoReparseAncestors(entry);
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes &
                    (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                {
                    throw new InvalidDataException();
                }
                File.Delete(entry);
            }
            Directory.Delete(temporary, recursive: false);
        }
        catch (Exception failure) when (failure is IOException
                                        or UnauthorizedAccessException
                                        or InvalidDataException
                                        or InvalidOperationException
                                        or ArgumentException)
        {
            throw new OpenVinoExportCleanupException(operationWasCancelled);
        }
    }
}
