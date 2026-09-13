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
    private const string DevicePathPrefix = "\\\\.\\";

    internal static KnowledgeAttachmentValidationResult Validate(
        IReadOnlyList<KnowledgeFileCandidate>? selected,
        IReadOnlyList<KnowledgeAttachment>? existing)
    {
        var accepted = new List<KnowledgeAttachment>();
        var rejections = new List<KnowledgeAttachmentRejection>();
        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int existingCount = existing?.Count ?? 0;

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
            else if (existingCount + accepted.Count >= MaximumAttachmentCount)
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
        IReadOnlyList<KnowledgeAttachment>? existing,
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
                Path.EndsInDirectorySeparator(path) ||
                !TryGetAttachmentPathKind(path, out AttachmentPathKind pathKind) ||
                ContainsExtendedDotSegment(path, pathKind))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            string? root = Path.GetPathRoot(fullPath);
            fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName is "." or ".." ||
                IsRootPath(fullPath, root) ||
                ContainsInvalidFileNameCharacter(path, pathKind))
            {
                fileName = string.Empty;
                normalizedPath = string.Empty;
                return false;
            }

            normalizedPath = NormalizeAttachmentPathKey(fullPath);
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

    private static bool TryGetAttachmentPathKind(string path, out AttachmentPathKind pathKind)
    {
        if (path.StartsWith(ExtendedUncPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            pathKind = AttachmentPathKind.ExtendedUnc;
            return HasUncServerShareAndLeaf(path, ExtendedUncPathPrefix.Length);
        }

        if (path.StartsWith(ExtendedPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            pathKind = AttachmentPathKind.ExtendedDrive;
            return HasDriveRoot(path, ExtendedPathPrefix.Length);
        }

        if (path.StartsWith(DevicePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            pathKind = default;
            return false;
        }

        if (path.StartsWith("\\\\", StringComparison.Ordinal))
        {
            pathKind = AttachmentPathKind.Unc;
            return HasUncServerShareAndLeaf(path, 2);
        }

        pathKind = AttachmentPathKind.Drive;
        return HasDriveRoot(path, 0);
    }

    private static bool HasDriveRoot(string path, int start) =>
        path.Length > start + 3 &&
        HasDrivePrefix(path[start..]) &&
        IsDirectorySeparator(path[start + 2]);

    private static bool HasUncServerShareAndLeaf(string path, int start) =>
        path[start..].Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Length >= 3;

    private static bool ContainsInvalidFileNameCharacter(string path, AttachmentPathKind pathKind)
    {
        int start = GetPathSegmentStart(pathKind);
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

    private static bool ContainsExtendedDotSegment(string path, AttachmentPathKind pathKind)
    {
        if (pathKind is not AttachmentPathKind.ExtendedDrive and not AttachmentPathKind.ExtendedUnc)
        {
            return false;
        }

        foreach (string segment in path[GetPathSegmentStart(pathKind)..]
                     .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeAttachmentPathKey(string normalizedPath)
    {
        if (normalizedPath.StartsWith(ExtendedUncPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "\\\\" + normalizedPath[ExtendedUncPathPrefix.Length..];
        }

        if (normalizedPath.StartsWith(ExtendedPathPrefix, StringComparison.OrdinalIgnoreCase) &&
            HasDrivePrefix(normalizedPath[ExtendedPathPrefix.Length..]))
        {
            return normalizedPath[ExtendedPathPrefix.Length..];
        }

        return normalizedPath;
    }

    private static string GetSafeFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsUncRootWithoutLeaf(path))
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

    private static bool IsDirectorySeparator(char character) => character is '\\' or '/';

    private static bool IsUncRootWithoutLeaf(string path)
    {
        int start;
        if (path.StartsWith(ExtendedUncPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            start = ExtendedUncPathPrefix.Length;
        }
        else if (path.StartsWith("\\\\", StringComparison.Ordinal) &&
                 !path.StartsWith(ExtendedPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            start = 2;
        }
        else
        {
            return false;
        }

        return path[start..].Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Length <= 2;
    }

    private static int GetPathSegmentStart(AttachmentPathKind pathKind) => pathKind switch
    {
        AttachmentPathKind.Drive => 2,
        AttachmentPathKind.Unc => 2,
        AttachmentPathKind.ExtendedDrive => ExtendedPathPrefix.Length + 2,
        AttachmentPathKind.ExtendedUnc => ExtendedUncPathPrefix.Length,
        _ => 0
    };

    private enum AttachmentPathKind
    {
        Drive,
        Unc,
        ExtendedDrive,
        ExtendedUnc
    }
}
