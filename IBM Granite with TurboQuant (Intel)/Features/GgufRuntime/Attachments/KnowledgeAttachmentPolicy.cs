using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GraniteEdgeAI.Features.GgufRuntime.Attachments;

internal static class KnowledgeAttachmentPolicy
{
    private const int MaximumAttachmentCount = 8;
    private const long MaximumAttachmentSizeInBytes = 8L * 1024 * 1024;
    private const int MaximumSafeFileNameLength = 255;
    private const string ExtendedPathPrefix = "\\\\?\\";
    private const string ExtendedUncPathPrefix = "\\\\?\\UNC\\";

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
            string? path = candidate?.Path;
            if (candidate is null ||
                !TryValidateFilePath(path, out string fileName, out string normalizedPath))
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-invalid", GetSafeFileName(path)));
            }
            else if (!knownPaths.Add(normalizedPath))
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-duplicate", fileName));
            }
            else if (!candidate.IsAccessible)
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-inaccessible", fileName));
            }
            else if (!IsSupportedFileName(fileName))
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-unsupported-type", fileName));
            }
            else if (candidate.SizeInBytes == 0)
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-empty", fileName));
            }
            else if (candidate.SizeInBytes < 0)
            {
                rejections.Add(new KnowledgeAttachmentRejection("attachment-invalid", fileName));
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
                accepted.Add(new KnowledgeAttachment(path!));
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
            if (attachment is not null &&
                TryValidateFilePath(attachment.Path, out _, out string normalizedPath))
            {
                knownPaths.Add(normalizedPath);
            }
        }
    }

    private static bool IsSupportedFileName(string fileName) =>
        fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    private static bool TryValidateFilePath(
        string? path,
        out string fileName,
        out string normalizedPath)
    {
        fileName = string.Empty;
        normalizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 ||
                !Path.IsPathFullyQualified(path) ||
                Path.EndsInDirectorySeparator(path))
            {
                return false;
            }

            normalizedPath = Path.GetFullPath(path);
            string? root = Path.GetPathRoot(normalizedPath);
            fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName is "." or ".." ||
                IsRootPath(normalizedPath, root) ||
                ContainsInvalidFileNameCharacter(path))
            {
                fileName = string.Empty;
                normalizedPath = string.Empty;
                return false;
            }

            return true;
        }
        catch (ArgumentException)
        {
            fileName = string.Empty;
            normalizedPath = string.Empty;
            return false;
        }
        catch (NotSupportedException)
        {
            fileName = string.Empty;
            normalizedPath = string.Empty;
            return false;
        }
        catch (PathTooLongException)
        {
            fileName = string.Empty;
            normalizedPath = string.Empty;
            return false;
        }
    }

    private static bool IsRootPath(string fullPath, string? root) =>
        !string.IsNullOrEmpty(root) &&
        string.Equals(
            Path.TrimEndingDirectorySeparator(fullPath),
            Path.TrimEndingDirectorySeparator(root),
            StringComparison.OrdinalIgnoreCase);

    private static bool ContainsInvalidFileNameCharacter(string path)
    {
        int start = GetPathSegmentStart(path);
        char[] invalidFileNameCharacters = Path.GetInvalidFileNameChars();

        foreach (string segment in path[start..].Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.IndexOfAny(invalidFileNameCharacters) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetSafeFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        int separatorIndex = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
        string fileName = path[(separatorIndex + 1)..];
        if (HasDrivePrefix(fileName))
        {
            fileName = fileName[2..];
        }

        char[] invalidFileNameCharacters = Path.GetInvalidFileNameChars();
        var safeFileName = new StringBuilder(Math.Min(fileName.Length, MaximumSafeFileNameLength));
        foreach (char character in fileName)
        {
            if (safeFileName.Length == MaximumSafeFileNameLength)
            {
                break;
            }

            if (!char.IsControl(character) && Array.IndexOf(invalidFileNameCharacters, character) < 0)
            {
                safeFileName.Append(character);
            }
        }

        return safeFileName.ToString();
    }

    private static bool HasDrivePrefix(string path) =>
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    private static int GetPathSegmentStart(string path)
    {
        if (path.StartsWith(ExtendedUncPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ExtendedUncPathPrefix.Length;
        }

        if (path.StartsWith(ExtendedPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            int prefixLength = ExtendedPathPrefix.Length;
            return HasDrivePrefix(path[prefixLength..]) ? prefixLength + 2 : prefixLength;
        }

        return HasDrivePrefix(path) ? 2 : 0;
    }
}
