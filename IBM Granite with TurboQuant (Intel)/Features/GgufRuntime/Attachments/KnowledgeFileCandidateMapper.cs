using System;
using System.Collections.Generic;
using System.IO;

namespace GraniteEdgeAI.Features.GgufRuntime.Attachments;

internal static class KnowledgeFileCandidateMapper
{
    // Bounds background metadata probes while preserving every selected path for policy evaluation.
    internal const int MaximumCandidateMetadataCount = 64;

    internal static IReadOnlyList<KnowledgeFileCandidate> Map(IReadOnlyList<string?>? paths)
    {
        if (paths is null || paths.Count == 0)
        {
            return Array.Empty<KnowledgeFileCandidate>();
        }

        var candidates = new List<KnowledgeFileCandidate>(paths.Count);
        for (int index = 0; index < paths.Count; index++)
        {
            string? path = paths[index];
            candidates.Add(index < MaximumCandidateMetadataCount
                ? MapMetadata(path)
                : new KnowledgeFileCandidate(path, 0, false));
        }

        return candidates.AsReadOnly();
    }

    private static KnowledgeFileCandidate MapMetadata(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }

        try
        {
            long sizeInBytes = new FileInfo(path).Length;
            return new KnowledgeFileCandidate(path, sizeInBytes, true);
        }
        // PathTooLongException is covered by IOException.
        catch (IOException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
        catch (UnauthorizedAccessException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
        catch (System.Security.SecurityException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
        catch (ArgumentException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
        catch (NotSupportedException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
    }
}
