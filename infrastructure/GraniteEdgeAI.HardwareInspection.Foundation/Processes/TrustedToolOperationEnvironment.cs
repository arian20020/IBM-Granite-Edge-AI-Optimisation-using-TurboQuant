using System.Collections.ObjectModel;
using System.Security.AccessControl;
using System.Security.Principal;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

public sealed class TrustedToolOperationEnvironment : IDisposable
{
    private const int MaximumEntries = 512;
    private const int MaximumDepth = 16;
    private string? _temporaryDirectory;

    private TrustedToolOperationEnvironment(
        string temporaryDirectory,
        IReadOnlyDictionary<string, string> variables)
    {
        _temporaryDirectory = temporaryDirectory;
        Variables = variables;
    }

    public IReadOnlyDictionary<string, string> Variables { get; }

    public static TrustedToolOperationEnvironment Create(
        IReadOnlyDictionary<string, string?> explicitEnvironment)
    {
        ArgumentNullException.ThrowIfNull(explicitEnvironment);
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw Failure();
        }

        string ownedRoot = Path.Combine(
            Path.GetFullPath(localApplicationData),
            "GraniteEdgeAI",
            "TrustedToolTemp");
        Directory.CreateDirectory(ownedRoot);
        EnsureNonReparseAncestry(ownedRoot);

        string operationDirectory = Path.Combine(
            ownedRoot,
            "operation-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(operationDirectory);
            ApplyPrivateAcl(operationDirectory);
            EnsureNonReparseAncestry(operationDirectory);

            var values = new Dictionary<string, string?>(
                explicitEnvironment,
                StringComparer.OrdinalIgnoreCase)
            {
                ["TEMP"] = operationDirectory,
                ["TMP"] = operationDirectory,
            };
            IReadOnlyDictionary<string, string> child =
                TrustedToolEnvironmentPolicy.Create(values);
            return new TrustedToolOperationEnvironment(operationDirectory, child);
        }
        catch
        {
            TryDeleteBounded(operationDirectory);
            throw;
        }
    }

    public void Dispose()
    {
        string? directory = Interlocked.Exchange(ref _temporaryDirectory, null);
        if (directory is not null)
        {
            TryDeleteBounded(directory);
        }
    }

    private static void ApplyPrivateAcl(string directory)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier owner = identity.User ?? throw Failure();
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(owner);
        security.AddAccessRule(new FileSystemAccessRule(
            owner,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(security);
    }

    private static void EnsureNonReparseAncestry(string path)
    {
        DirectoryInfo? current = new(path);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw Failure();
            }

            current = current.Parent;
        }
    }

    private static void TryDeleteBounded(string root)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            var pending = new Stack<(string Path, int Depth, bool Visited)>();
            pending.Push((root, 0, false));
            int entries = 0;
            while (pending.Count > 0 && entries <= MaximumEntries)
            {
                (string path, int depth, bool visited) = pending.Pop();
                if (visited)
                {
                    Directory.Delete(path, recursive: false);
                    continue;
                }

                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0 || depth > MaximumDepth)
                {
                    return;
                }

                pending.Push((path, depth, true));
                foreach (string entry in Directory.EnumerateFileSystemEntries(path))
                {
                    if (++entries > MaximumEntries)
                    {
                        return;
                    }

                    FileAttributes entryAttributes = File.GetAttributes(entry);
                    if ((entryAttributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return;
                    }

                    if ((entryAttributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push((entry, depth + 1, false));
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
            }
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

    private static InvalidOperationException Failure() => new(
        "The trusted hardware tool environment could not be secured.");
}
