using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.DownloadedModels;

internal sealed class BoundedDownloadedModelFinder : IDownloadedModelFinder
{
    private readonly Func<KnownFolderId, string?> folderResolver;
    private readonly int maximumChildDepth;
    private IReadOnlyList<string> lastSafeResults = Array.Empty<string>();

    internal BoundedDownloadedModelFinder(
        Func<KnownFolderId, string?>? folderResolver = null,
        int maximumChildDepth = 1)
    {
        this.folderResolver = folderResolver ?? ResolveKnownFolder;
        this.maximumChildDepth = maximumChildDepth;
    }

    internal int EnumerateCallCount { get; private set; }
    internal int VisitedItemCount { get; private set; }
    internal IReadOnlyList<string> LastSafeResults => lastSafeResults;

    public Task<DownloadedModelSearchResult> FindAsync(
        DownloadedModelSearchPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Validate(policy);
        return Task.Run(() => FindCore(policy, cancellationToken), cancellationToken);
    }

    public void ClearResults() => lastSafeResults = Array.Empty<string>();

    private DownloadedModelSearchResult FindCore(
        DownloadedModelSearchPolicy policy,
        CancellationToken cancellationToken)
    {
        ClearResults();
        EnumerateCallCount = 0;
        VisitedItemCount = 0;
        var names = new List<string>();
        Stopwatch elapsed = Stopwatch.StartNew();

        try
        {
            foreach (KnownFolderId folder in policy.AllowedFolders.Take(policy.MaximumLocations))
            {
                ThrowIfStopped(elapsed, policy.MaximumElapsed, cancellationToken);
                string? root = folderResolver(folder);
                if (string.IsNullOrWhiteSpace(root) || IsNetworkPath(root) || !IsUsableDirectory(root))
                {
                    continue;
                }

                EnumerateCallCount++;
                ScanDirectory(root, depth: 0, policy, elapsed, names, cancellationToken);
                if (VisitedItemCount >= policy.MaximumVisitedItems)
                {
                    break;
                }
            }

            lastSafeResults = names.AsReadOnly();
            return new DownloadedModelSearchResult(lastSafeResults);
        }
        catch
        {
            ClearResults();
            throw;
        }
    }

    private void ScanDirectory(
        string directory,
        int depth,
        DownloadedModelSearchPolicy policy,
        Stopwatch elapsed,
        List<string> names,
        CancellationToken cancellationToken)
    {
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(directory);
        }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        foreach (string entry in entries)
        {
            ThrowIfStopped(elapsed, policy.MaximumElapsed, cancellationToken);
            if (VisitedItemCount >= policy.MaximumVisitedItems)
            {
                return;
            }

            VisitedItemCount++;
            if (!TryGetAttributes(entry, out FileAttributes attributes) ||
                attributes.HasFlag(FileAttributes.ReparsePoint) ||
                IsNetworkPath(entry))
            {
                continue;
            }

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                if (depth < maximumChildDepth)
                {
                    ScanDirectory(entry, depth + 1, policy, elapsed, names, cancellationToken);
                }
                continue;
            }

            if (IsModelCandidate(entry))
            {
                names.Add(Path.GetFileName(entry));
            }
        }
    }

    private static bool IsModelCandidate(string fileName) =>
        fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetFileName(fileName), "config.json", StringComparison.OrdinalIgnoreCase);

    private static bool IsUsableDirectory(string path)
    {
        try
        {
            return Directory.Exists(path) &&
                !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (IOException) { attributes = default; return false; }
        catch (UnauthorizedAccessException) { attributes = default; return false; }
    }

    private static void ThrowIfStopped(
        Stopwatch elapsed,
        TimeSpan maximumElapsed,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (elapsed.Elapsed > maximumElapsed)
        {
            throw new OperationCanceledException("Downloaded model search timed out.");
        }
    }

    private static bool IsNetworkPath(string path) =>
        path.StartsWith("\\\\", StringComparison.Ordinal) ||
        path.StartsWith("//", StringComparison.Ordinal);

    private static string? ResolveKnownFolder(KnownFolderId folder) => folder switch
    {
        KnownFolderId.Downloads => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        KnownFolderId.Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        KnownFolderId.Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        _ => null
    };

    private static void Validate(DownloadedModelSearchPolicy policy)
    {
        if (policy.MaximumLocations is < 1 or > 3 ||
            policy.MaximumVisitedItems is < 1 or > 250 ||
            policy.MaximumElapsed <= TimeSpan.Zero ||
            policy.MaximumElapsed > TimeSpan.FromSeconds(10))
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }
    }
}
