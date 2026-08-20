using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.GgufRuntime.Attachments;

internal static class KnowledgeAttachmentPolicy
{
    private const int MaximumAttachmentCount = 8;
    private const long MaximumAttachmentSizeInBytes = 8L * 1024 * 1024;

    internal static KnowledgeAttachmentValidationResult Validate(
        IReadOnlyList<KnowledgeFileCandidate> selected,
        IReadOnlyList<KnowledgeAttachment> existing)
    {
        var accepted = new List<KnowledgeAttachment>();
        var rejections = new List<KnowledgeAttachmentRejection>();
        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddExistingPaths(existing, knownPaths);

        if (selected is null)
        {
            return new KnowledgeAttachmentValidationResult(accepted.AsReadOnly(), rejections.AsReadOnly());
        }

        foreach (KnowledgeFileCandidate? candidate in selected)
        {
            if (candidate is null || !TryGetFileName(candidate.Path, out string fileName))
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-invalid", string.Empty));
            }
            else if (candidate.SizeInBytes < 0)
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-invalid", fileName));
            }
            else if (!candidate.IsAccessible)
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-inaccessible", fileName));
            }
            else if (!IsSupportedFileName(fileName))
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-unsupported-type", fileName));
            }
            else if (knownPaths.Contains(candidate.Path))
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-duplicate", fileName));
            }
            else if (candidate.SizeInBytes == 0)
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-empty", fileName));
            }
            else if (candidate.SizeInBytes > MaximumAttachmentSizeInBytes)
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-too-large", fileName));
            }
            else if (existing.Count + accepted.Count >= MaximumAttachmentCount)
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-count-exceeded", fileName));
            }
            else
            {
                accepted.Add(new KnowledgeAttachment(candidate.Path));
                knownPaths.Add(candidate.Path);
            }
        }

        return new KnowledgeAttachmentValidationResult(accepted.AsReadOnly(), rejections.AsReadOnly());
    }

    private static void AddExistingPaths(
        IReadOnlyList<KnowledgeAttachment> existing,
        ISet<string> knownPaths)
    {
        if (existing is null)
        {
            return;
        }

        foreach (KnowledgeAttachment? attachment in existing)
        {
            if (attachment is not null && TryGetFileName(attachment.Path, out _))
            {
                knownPaths.Add(attachment.Path);
            }
        }
    }

    private static bool IsSupportedFileName(string fileName) =>
        fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetFileName(string? path, out string fileName)
    {
        fileName = string.Empty;

        if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0)
        {
            return false;
        }

        int separatorIndex = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
        if (separatorIndex == path.Length - 1)
        {
            return false;
        }

        fileName = path[(separatorIndex + 1)..];
        return !string.IsNullOrWhiteSpace(fileName);
    }
}
