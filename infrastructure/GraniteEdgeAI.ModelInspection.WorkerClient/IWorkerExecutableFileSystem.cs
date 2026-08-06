using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Isolates operating-system path and file operations so executable trust rules
/// can be tested deterministically without depending on a developer machine.
/// </summary>
internal interface IWorkerExecutableFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    FileAttributes GetAttributes(string path);

    ushort ReadPortableExecutableMachine(string path);

    SafeFileHandle OpenReadHandle(string path);

    string GetFinalDirectoryPath(string path);

    string GetFinalFilePath(SafeFileHandle handle);
}
