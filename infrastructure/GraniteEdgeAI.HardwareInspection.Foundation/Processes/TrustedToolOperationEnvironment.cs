using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

public sealed class TrustedToolOperationEnvironment : IDisposable
{
    private const int MaximumEntries = 512;
    private const int MaximumDepth = 16;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint GenericRead = 0x80000000;
    private string? _temporaryDirectory;
    private SafeFileHandle? _directoryCustody;

    private TrustedToolOperationEnvironment(
        string temporaryDirectory,
        SafeFileHandle directoryCustody,
        IReadOnlyDictionary<string, string> variables)
    {
        _temporaryDirectory = temporaryDirectory;
        _directoryCustody = directoryCustody;
        Variables = variables;
    }

    public IReadOnlyDictionary<string, string> Variables { get; }

    public bool CleanupSucceeded { get; private set; }

    public static TrustedToolOperationEnvironment CreateCurrent(bool includeDotnetRoots)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot"),
            ["WINDIR"] = Environment.GetEnvironmentVariable("WINDIR"),
        };
        if (includeDotnetRoots)
        {
            environment["DOTNET_ROOT"] = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            environment["DOTNET_ROOT_X64"] =
                Environment.GetEnvironmentVariable("DOTNET_ROOT_X64");
        }

        return Create(environment);
    }

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
            SafeFileHandle custody = OpenDirectoryCustody(operationDirectory);
            return new TrustedToolOperationEnvironment(
                operationDirectory,
                custody,
                child);
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
        SafeFileHandle? custody = Interlocked.Exchange(ref _directoryCustody, null);
        if (directory is not null)
        {
            CleanupSucceeded = TryDeleteBounded(directory, custody);
        }
        else
        {
            custody?.Dispose();
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

    private static SafeFileHandle OpenDirectoryCustody(string directory)
    {
        SafeFileHandle handle = CreateFile(
            directory,
            GenericRead,
            FileShare.Read | FileShare.Write,
            IntPtr.Zero,
            FileMode.Open,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw Failure();
        }

        return handle;
    }

    private static bool TryDeleteBounded(
        string root,
        SafeFileHandle? custody = null)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                return true;
            }

            var pending = new Stack<(string Path, int Depth, bool Visited)>();
            pending.Push((root, 0, false));
            int entries = 0;
            while (pending.Count > 0 && entries <= MaximumEntries)
            {
                (string path, int depth, bool visited) = pending.Pop();
                if (visited)
                {
                    if (!string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.Delete(path, recursive: false);
                    }

                    continue;
                }

                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0 || depth > MaximumDepth)
                {
                    return false;
                }

                pending.Push((path, depth, true));
                foreach (string entry in Directory.EnumerateFileSystemEntries(path))
                {
                    if (++entries > MaximumEntries)
                    {
                        return false;
                    }

                    FileAttributes entryAttributes = File.GetAttributes(entry);
                    if ((entryAttributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return false;
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

            custody?.Dispose();
            custody = null;
            Directory.Delete(root, recursive: false);
            return !Directory.Exists(root);
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
        finally
        {
            custody?.Dispose();
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        FileMode creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    private static InvalidOperationException Failure() => new(
        "The trusted hardware tool environment could not be secured.");
}
