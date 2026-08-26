Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:EvidenceClassification = 'nonpublishable-core'
$script:MaximumCapturedProcessCharacters = 1048576
$script:PackagedOperationOwnership = New-Object -TypeName 'Collections.Generic.Dictionary[string,IDisposable]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)

if ($null -eq ('PackagedCheckpointJob' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

public sealed class PackagedCheckpointJob : IDisposable
{
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
    private const int JobObjectExtendedLimitInformation = 9;
    private SafeFileHandle handle;

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BASIC_LIMIT_INFORMATION { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)]
    private struct EXTENDED_LIMIT_INFORMATION { public BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref EXTENDED_LIMIT_INFORMATION information, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);

    public PackagedCheckpointJob(Process process)
    {
        handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == null || handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create checkpoint process job.");
        try {
            var information = new EXTENDED_LIMIT_INFORMATION();
            information.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, ref information, (uint)Marshal.SizeOf(typeof(EXTENDED_LIMIT_INFORMATION))))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to configure checkpoint process job.");
            if (!AssignProcessToJobObject(handle, process.Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to assign checkpoint process to its job.");
        }
        catch { handle.Dispose(); handle = null; throw; }
    }

    public void Terminate() { if (handle != null && !handle.IsInvalid && !TerminateJobObject(handle, 1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to terminate checkpoint process job."); }
    public void Dispose() { if (handle != null) { handle.Dispose(); handle = null; } }
}

public sealed class PackagedCheckpointOwnedProcess : IDisposable
{
    public Process Process { get; private set; }
    public Stream Output { get; private set; }
    public Stream Error { get; private set; }
    public PackagedCheckpointJob Job { get; private set; }

    [StructLayout(LayoutKind.Sequential)] private struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; [MarshalAs(UnmanagedType.Bool)] public bool bInheritHandle; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct STARTUPINFO { public int cb; public string lpReserved, lpDesktop, lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError; }
    [StructLayout(LayoutKind.Sequential)] private struct STARTUPINFOEX { public STARTUPINFO StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential)] private struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out IntPtr readPipe, out IntPtr writePipe, ref SECURITY_ATTRIBUTES attributes, int size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateFileW(string fileName, uint desiredAccess, uint shareMode, ref SECURITY_ATTRIBUTES securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr attributeList, int attributeCount, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr attributeList, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previousValue, IntPtr returnSize);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcessW(string application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string currentDirectory, ref STARTUPINFOEX startup, out PROCESS_INFORMATION processInformation);
    [DllImport("kernel32.dll")] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }

    public static PackagedCheckpointOwnedProcess Start(ProcessStartInfo start)
    {
        return Start(start, -1);
    }

    public static PackagedCheckpointOwnedProcess Start(ProcessStartInfo start, int constructionFaultDelayMilliseconds)
    {
        const uint CREATE_SUSPENDED = 0x00000004, CREATE_NO_WINDOW = 0x08000000, CREATE_UNICODE_ENVIRONMENT = 0x00000400, EXTENDED_STARTUPINFO_PRESENT = 0x00080000, HANDLE_FLAG_INHERIT = 0x00000001;
        const uint GENERIC_READ = 0x80000000, FILE_SHARE_READ = 0x00000001, FILE_SHARE_WRITE = 0x00000002, OPEN_EXISTING = 3, WAIT_TIMEOUT = 0x00000102, WAIT_FAILED = 0xffffffff;
        IntPtr PROC_THREAD_ATTRIBUTE_HANDLE_LIST = new IntPtr(0x00020002);
        var security = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)), bInheritHandle = true };
        IntPtr stdinRead = IntPtr.Zero, stdoutRead = IntPtr.Zero, stdoutWrite = IntPtr.Zero, stderrRead = IntPtr.Zero, stderrWrite = IntPtr.Zero;
        IntPtr attributeList = IntPtr.Zero, inheritedHandles = IntPtr.Zero;
        bool attributeListInitialized = false;
        if (!CreatePipe(out stdoutRead, out stdoutWrite, ref security, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create checkpoint stdout pipe.");
        if (!CreatePipe(out stderrRead, out stderrWrite, ref security, 0)) { CloseHandle(stdoutRead); CloseHandle(stdoutWrite); throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create checkpoint stderr pipe."); }
        PROCESS_INFORMATION information = new PROCESS_INFORMATION();
        GCHandle environmentHandle = new GCHandle();
        Process process = null;
        PackagedCheckpointJob job = null;
        Stream output = null;
        Stream error = null;
        try {
            if (!SetHandleInformation(stdoutRead, HANDLE_FLAG_INHERIT, 0) || !SetHandleInformation(stderrRead, HANDLE_FLAG_INHERIT, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to isolate checkpoint pipe handles.");
            stdinRead = CreateFileW("NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, ref security, OPEN_EXISTING, 0, IntPtr.Zero);
            if (stdinRead == new IntPtr(-1)) { stdinRead = IntPtr.Zero; throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create checkpoint stdin handle."); }
            IntPtr attributeListSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
            if (attributeListSize == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to size the checkpoint handle-list attribute.");
            attributeList = Marshal.AllocHGlobal(attributeListSize);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to initialize the checkpoint handle-list attribute.");
            attributeListInitialized = true;
            inheritedHandles = Marshal.AllocHGlobal(IntPtr.Size * 3);
            Marshal.WriteIntPtr(inheritedHandles, 0, stdinRead);
            Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size, stdoutWrite);
            Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size * 2, stderrWrite);
            if (!UpdateProcThreadAttribute(attributeList, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, inheritedHandles, new IntPtr(IntPtr.Size * 3), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to bind the checkpoint stdio handle list.");
            var environment = new StringBuilder();
            var keys = new System.Collections.Generic.List<string>();
            foreach (string key in start.EnvironmentVariables.Keys) keys.Add(key);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string key in keys) environment.Append(key).Append('=').Append(start.EnvironmentVariables[key]).Append('\0');
            environment.Append('\0');
            byte[] environmentBytes = Encoding.Unicode.GetBytes(environment.ToString());
            environmentHandle = GCHandle.Alloc(environmentBytes, GCHandleType.Pinned);
            var startup = new STARTUPINFOEX();
            startup.StartupInfo = new STARTUPINFO { cb = Marshal.SizeOf(typeof(STARTUPINFOEX)), dwFlags = 0x00000100, hStdInput = stdinRead, hStdOutput = stdoutWrite, hStdError = stderrWrite };
            startup.lpAttributeList = attributeList;
            var commandLine = new StringBuilder(Quote(start.FileName) + (String.IsNullOrWhiteSpace(start.Arguments) ? "" : " " + start.Arguments));
            if (!CreateProcessW(start.FileName, commandLine, IntPtr.Zero, IntPtr.Zero, true, CREATE_SUSPENDED | CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT | EXTENDED_STARTUPINFO_PRESENT, environmentHandle.AddrOfPinnedObject(), start.WorkingDirectory, ref startup, out information)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create suspended checkpoint process.");
            CloseHandle(stdoutWrite); stdoutWrite = IntPtr.Zero; CloseHandle(stderrWrite); stderrWrite = IntPtr.Zero;
            CloseHandle(stdinRead); stdinRead = IntPtr.Zero;
            process = Process.GetProcessById(information.dwProcessId);
            job = new PackagedCheckpointJob(process);
            if (ResumeThread(information.hThread) == 0xffffffff) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to resume owned checkpoint process.");
            if (constructionFaultDelayMilliseconds >= 0) {
                Thread.Sleep(constructionFaultDelayMilliseconds);
                throw new InvalidOperationException("Injected checkpoint process construction failure.");
            }
            output = new FileStream(new SafeFileHandle(stdoutRead, true), FileAccess.Read, 4096, false); stdoutRead = IntPtr.Zero;
            error = new FileStream(new SafeFileHandle(stderrRead, true), FileAccess.Read, 4096, false); stderrRead = IntPtr.Zero;
            var owned = new PackagedCheckpointOwnedProcess();
            owned.Process = process;
            owned.Job = job;
            owned.Output = output;
            owned.Error = error;
            process = null;
            job = null;
            output = null;
            error = null;
            return owned;
        }
        catch {
            Exception cleanupFailure = null;
            if (job != null) {
                try { job.Dispose(); }
                catch (Exception exception) { cleanupFailure = exception; }
                job = null;
            }
            if (information.hProcess != IntPtr.Zero) {
                uint constructionWait = WaitForSingleObject(information.hProcess, 5000);
                if (constructionWait == WAIT_FAILED) cleanupFailure = cleanupFailure ?? new Win32Exception(Marshal.GetLastWin32Error(), "Unable to verify checkpoint process construction cleanup.");
                else if (constructionWait == WAIT_TIMEOUT) {
                    if (!TerminateProcess(information.hProcess, 1)) cleanupFailure = cleanupFailure ?? new Win32Exception(Marshal.GetLastWin32Error(), "Unable to terminate checkpoint process after construction failure.");
                    else {
                        uint fallbackWait = WaitForSingleObject(information.hProcess, 5000);
                        if (fallbackWait == WAIT_FAILED) cleanupFailure = cleanupFailure ?? new Win32Exception(Marshal.GetLastWin32Error(), "Unable to verify checkpoint process fallback termination.");
                        else if (fallbackWait == WAIT_TIMEOUT) cleanupFailure = cleanupFailure ?? new TimeoutException("Checkpoint process remained live after construction-fault termination.");
                    }
                }
            }
            if (output != null) output.Dispose();
            if (error != null) error.Dispose();
            if (process != null) process.Dispose();
            if (cleanupFailure != null) throw new InvalidOperationException("Checkpoint process construction cleanup failed.", cleanupFailure);
            throw;
        }
        finally {
            if (environmentHandle.IsAllocated) environmentHandle.Free();
            if (attributeListInitialized) DeleteProcThreadAttributeList(attributeList);
            if (attributeList != IntPtr.Zero) Marshal.FreeHGlobal(attributeList);
            if (inheritedHandles != IntPtr.Zero) Marshal.FreeHGlobal(inheritedHandles);
            if (stdinRead != IntPtr.Zero) CloseHandle(stdinRead);
            if (stdoutRead != IntPtr.Zero) CloseHandle(stdoutRead); if (stdoutWrite != IntPtr.Zero) CloseHandle(stdoutWrite);
            if (stderrRead != IntPtr.Zero) CloseHandle(stderrRead); if (stderrWrite != IntPtr.Zero) CloseHandle(stderrWrite);
            if (information.hThread != IntPtr.Zero) CloseHandle(information.hThread); if (information.hProcess != IntPtr.Zero) CloseHandle(information.hProcess);
        }
    }

    public int GetAuthoritativeExitCode()
    {
        uint exitCode;
        if (Process == null || !GetExitCodeProcess(Process.Handle, out exitCode)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to read the checkpoint process exit status.");
        if (exitCode == 259) throw new InvalidOperationException("Checkpoint process exit status remained active after its bounded wait.");
        return unchecked((int)exitCode);
    }

    public void Dispose() { if (Output != null) Output.Dispose(); if (Error != null) Error.Dispose(); if (Job != null) Job.Dispose(); if (Process != null) Process.Dispose(); }
}

public static class PackagedCheckpointStream
{
    public static async Task<byte[]> ReadCappedAsync(Stream stream, int maximumBytes)
    {
        using (var output = new MemoryStream(Math.Min(maximumBytes, 8192)))
        {
            var buffer = new byte[4096];
            while (true)
            {
                int count = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (count == 0) return output.ToArray();
                if (output.Length > maximumBytes - count) throw new InvalidDataException("Checkpoint process output exceeded its privacy bound.");
                output.Write(buffer, 0, count);
            }
        }
    }
}

public sealed class PackagedOperationLease : IDisposable
{
    private const uint DELETE = 0x00010000;
    private const uint FILE_READ_ATTRIBUTES = 0x00000080;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private SafeFileHandle directoryHandle;
    private FileStream markerStream;
    public string MarkerPath { get; private set; }
    public bool IsLive { get { return directoryHandle != null && !directoryHandle.IsInvalid && markerStream != null; } }

    [StructLayout(LayoutKind.Sequential)] private struct FILE_DISPOSITION_INFO { [MarshalAs(UnmanagedType.Bool)] public bool DeleteFile; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, ref FILE_DISPOSITION_INFO information, uint size);

    public PackagedOperationLease(string directory, string markerPath)
    {
        directoryHandle = CreateFile(directory, DELETE | FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
        if (directoryHandle == null || directoryHandle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to lock operation directory identity.");
        try {
            MarkerPath = markerPath;
            markerStream = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough);
            markerStream.Flush(true);
        }
        catch { directoryHandle.Dispose(); throw; }
    }

    public void ReleaseForPublication()
    {
        if (!IsLive) throw new InvalidOperationException("Operation lease is not live.");
        try { markerStream.Dispose(); markerStream = null; File.Delete(MarkerPath); }
        finally { directoryHandle.Dispose(); directoryHandle = null; }
    }

    public void DeleteEmptyDirectory()
    {
        if (!IsLive) throw new InvalidOperationException("Operation lease is not live.");
        markerStream.Dispose(); markerStream = null;
        File.Delete(MarkerPath);
        var disposition = new FILE_DISPOSITION_INFO { DeleteFile = true };
        try {
            if (!SetFileInformationByHandle(directoryHandle, 4, ref disposition, (uint)Marshal.SizeOf(typeof(FILE_DISPOSITION_INFO))))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to atomically delete the locked operation directory.");
        }
        finally { directoryHandle.Dispose(); directoryHandle = null; }
    }

    public void Dispose()
    {
        if (markerStream != null) { markerStream.Dispose(); markerStream = null; }
        if (directoryHandle != null) { directoryHandle.Dispose(); directoryHandle = null; }
    }
}
'@
}

function ConvertTo-PackagedArgument {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Value)

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }
    return '"' + ([regex]::Replace($Value, '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}

function Get-PackagedSha256Bytes {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return @($algorithm.ComputeHash($Bytes) | ForEach-Object { $_.ToString('x2') }) -join ''
    }
    finally {
        $algorithm.Dispose()
    }
}

function Assert-PackagedNoReparseAncestors {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [switch]$IncludeLeaf
    )

    $full = [IO.Path]::GetFullPath($Path)
    $cursor = if ($IncludeLeaf) { $full } else { Split-Path -Parent $full }
    while (-not [string]::IsNullOrWhiteSpace($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -Force -LiteralPath $cursor -ErrorAction Stop
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Reparse points are not permitted in checkpoint paths.'
            }
        }
        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or [StringComparer]::OrdinalIgnoreCase.Equals($parent, $cursor)) {
            break
        }
        $cursor = $parent
    }
}

function Get-PackagedRegularFileIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label
    )

    try {
        $full = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path)
    }
    catch {
        throw "$Label must exist as an exact regular file."
    }
    Assert-PackagedNoReparseAncestors -Path $full -IncludeLeaf
    $item = Get-Item -Force -LiteralPath $full -ErrorAction Stop
    if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Label must be an exact regular non-reparse file."
    }
    $bytes = [IO.File]::ReadAllBytes($full)
    $itemAfter = Get-Item -Force -LiteralPath $full -ErrorAction Stop
    if (
        [long]$bytes.LongLength -ne [long]$item.Length -or
        [long]$itemAfter.Length -ne [long]$item.Length -or
        [datetime]$itemAfter.LastWriteTimeUtc -ne [datetime]$item.LastWriteTimeUtc -or
        [datetime]$itemAfter.CreationTimeUtc -ne [datetime]$item.CreationTimeUtc
    ) {
        throw "$Label changed while its exact identity was captured."
    }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($full)
    return [pscustomobject]@{
        Path = $full
        Length = [long]$item.Length
        Sha256 = Get-PackagedSha256Bytes -Bytes $bytes
        LastWriteTimeUtc = [datetime]$item.LastWriteTimeUtc
        CreationTimeUtc = [datetime]$item.CreationTimeUtc
        ProductName = [string]$version.ProductName
        ProductVersion = [string]$version.ProductVersion
        FileVersion = [string]$version.FileVersion
    }
}

function Assert-PackagedFileIdentityUnchanged {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][pscustomobject]$Before,
        [Parameter(Mandatory)][pscustomobject]$After,
        [Parameter(Mandatory)][string]$Label
    )

    if (
        -not [StringComparer]::OrdinalIgnoreCase.Equals([string]$Before.Path, [string]$After.Path) -or
        [long]$Before.Length -ne [long]$After.Length -or
        [string]$Before.Sha256 -cne [string]$After.Sha256 -or
        [datetime]$Before.LastWriteTimeUtc -ne [datetime]$After.LastWriteTimeUtc -or
        [datetime]$Before.CreationTimeUtc -ne [datetime]$After.CreationTimeUtc
    ) {
        throw "$Label identity changed during the bounded operation."
    }
}

function Resolve-PackagedSystemExecutable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Label
    )

    $systemDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::System)
    if ([string]::IsNullOrWhiteSpace($systemDirectory)) {
        throw 'The exact System32 directory could not be resolved.'
    }
    $path = Join-Path -Path $systemDirectory -ChildPath $Name
    return Get-PackagedRegularFileIdentity -Path $path -Label $Label
}

function Create-PackagedProcessJob {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][Diagnostics.Process]$Process
    )

    return New-Object -TypeName PackagedCheckpointJob -ArgumentList $Process
}

function Stop-PackagedProcessTree {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][Diagnostics.Process]$Process,
        [Parameter(Mandatory)][PackagedCheckpointJob]$Job
    )

    $Job.Terminate()
    if (-not $Process.HasExited -and -not $Process.WaitForExit(5000)) {
        throw 'Timed-out checkpoint process root did not exit within five seconds after job termination.'
    }
}

function Invoke-PackagedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [string[]]$Arguments = @(),
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 60,
        [hashtable]$Environment = @{},
        [switch]$ClearGitEnvironment
    )

    if ([IO.Path]::IsPathRooted($FilePath)) {
        $executableBefore = Get-PackagedRegularFileIdentity -Path $FilePath -Label 'Checkpoint executable'
    }
    elseif ([StringComparer]::OrdinalIgnoreCase.Equals($FilePath, 'git.exe')) {
        $executableBefore = Resolve-PackagedGitExecutable
    }
    else {
        throw 'Checkpoint executable must be an absolute path or the exact trusted git.exe name.'
    }
    $start = New-Object -TypeName Diagnostics.ProcessStartInfo
    $start.FileName = $executableBefore.Path
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    if ($ClearGitEnvironment) {
        foreach ($environmentName in @($start.EnvironmentVariables.Keys)) {
            if ($environmentName.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase)) {
                $start.EnvironmentVariables.Remove($environmentName)
            }
        }
    }
    foreach ($environmentName in $Environment.Keys) {
        $start.EnvironmentVariables[[string]$environmentName] = [string]$Environment[$environmentName]
    }
    $start.Arguments = @(
        $Arguments | ForEach-Object { ConvertTo-PackagedArgument -Value ([string]$_) }
    ) -join ' '
    $ownedProcess = [PackagedCheckpointOwnedProcess]::Start($start)
    $process = $ownedProcess.Process
    $job = $ownedProcess.Job
    try {
        $outputTask = [PackagedCheckpointStream]::ReadCappedAsync($ownedProcess.Output, $script:MaximumCapturedProcessCharacters)
        $errorTask = [PackagedCheckpointStream]::ReadCappedAsync($ownedProcess.Error, $script:MaximumCapturedProcessCharacters)
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            Stop-PackagedProcessTree -Process $process -Job $job
            if (-not [Threading.Tasks.Task]::WaitAll(@($outputTask, $errorTask), 5000)) {
                throw 'Timed-out checkpoint process post-termination stream capture did not finish within five seconds.'
            }
            throw "Checkpoint process timed out after $TimeoutSeconds seconds; its owned job was terminated."
        }
        if (-not [Threading.Tasks.Task]::WaitAll(@($outputTask, $errorTask), 5000)) {
            $job.Terminate()
            if (-not [Threading.Tasks.Task]::WaitAll(@($outputTask, $errorTask), 5000)) {
                throw 'Checkpoint process post-termination stream capture did not finish within five seconds.'
            }
        }
        $outputBytes = [byte[]]$outputTask.GetAwaiter().GetResult()
        $errorBytes = [byte[]]$errorTask.GetAwaiter().GetResult()
        $displayEncoding = New-Object -TypeName Text.UTF8Encoding -ArgumentList @($false, $false)
        return [pscustomobject]@{
            ExitCode = [int]$ownedProcess.GetAuthoritativeExitCode()
            Output = [string]$displayEncoding.GetString($outputBytes)
            Error = [string]$displayEncoding.GetString($errorBytes)
            OutputBytes = [byte[]]$outputBytes
            ErrorBytes = [byte[]]$errorBytes
        }
    }
    finally {
        $ownedProcess.Dispose()
        $executableAfter = Get-PackagedRegularFileIdentity -Path $executableBefore.Path -Label 'Checkpoint executable'
        Assert-PackagedFileIdentityUnchanged -Before $executableBefore -After $executableAfter -Label 'Checkpoint executable'
    }
}

function ConvertFrom-PackagedUtf8Bytes {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes)

    $strictUtf8 = New-Object -TypeName Text.UTF8Encoding -ArgumentList @($false, $true)
    try {
        return [string]$strictUtf8.GetString($Bytes)
    }
    catch {
        throw 'Required Git identity output is not strict UTF-8.'
    }
}

function ConvertFrom-PackagedNulBytes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes,
        [Parameter(Mandatory)][string]$Label
    )

    if ($Bytes.Length -eq 0) {
        return @()
    }
    $text = ConvertFrom-PackagedUtf8Bytes -Bytes $Bytes
    if ($text[$text.Length - 1] -ne [char]0) {
        throw "$Label is not terminated by an exact NUL record boundary."
    }
    $parts = @($text.Split([char]0))
    if ($parts[$parts.Count - 1].Length -ne 0) {
        throw "$Label has an invalid final NUL record boundary."
    }
    $records = New-Object -TypeName 'Collections.Generic.List[string]'
    $index = 0
    while ($index -lt ($parts.Count - 1)) {
        if ($parts[$index].Length -eq 0) {
            throw "$Label contains an empty NUL record."
        }
        $records.Add([string]$parts[$index])
        $index++
    }
    return @($records)
}

function Assert-PackagedIndexFlagRecords {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes,
        [Parameter(Mandatory)][ValidateSet('Tracked', 'Fsmonitor')][string]$Kind
    )

    $label = if ($Kind -ceq 'Fsmonitor') { 'Git fsmonitor-index flags' } else { 'Git tracked-index flags' }
    foreach ($record in @(ConvertFrom-PackagedNulBytes -Bytes $Bytes -Label $label)) {
        $tag = if ($record.Length -gt 0) { [char]$record[0] } else { [char]0 }
        $isAsciiLetter = ($tag -ge [char]'A' -and $tag -le [char]'Z') -or ($tag -ge [char]'a' -and $tag -le [char]'z')
        if ($record.Length -lt 3 -or $record[1] -ne ' ' -or -not $isAsciiLetter) {
            if ($Kind -ceq 'Fsmonitor') {
                throw 'Git returned an invalid fsmonitor-index flag record.'
            }
            throw 'Git returned an invalid tracked-index flag record.'
        }
        if ($Kind -ceq 'Fsmonitor' -and $tag -ge [char]'a' -and $tag -le [char]'z') {
            throw 'Repository contains an fsmonitor-valid tracked entry.'
        }
        if ($Kind -ceq 'Tracked' -and (($tag -ge [char]'a' -and $tag -le [char]'z') -or $tag -eq 'S')) {
            throw 'Repository contains an assume-unchanged or skip-worktree tracked entry.'
        }
    }
}

function Resolve-PackagedGitExecutable {
    [CmdletBinding()]
    param()

    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    $path = Join-Path -Path $programFiles -ChildPath 'Git\cmd\git.exe'
    return Get-PackagedRegularFileIdentity -Path $path -Label 'Program Files Git'
}

function Invoke-PackagedGit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$Raw,
        [switch]$InspectFsmonitorFlags
    )

    $gitBefore = Resolve-PackagedGitExecutable
    $environment = @{
        GIT_NO_REPLACE_OBJECTS = '1'
        GIT_OPTIONAL_LOCKS = '0'
        GIT_CONFIG_NOSYSTEM = '1'
        GIT_CONFIG_GLOBAL = 'NUL'
        GIT_CONFIG_SYSTEM = 'NUL'
        GIT_TERMINAL_PROMPT = '0'
    }
    $gitIsolationArguments = New-Object -TypeName 'Collections.Generic.List[string]'
    foreach ($argument in @(
        '--no-replace-objects'
        '--no-optional-locks'
        '-c'
        'core.fsmonitor=false'
        '-c'
        'core.hooksPath=NUL'
        '-c'
        'core.untrackedCache=false'
        '-c'
        'core.preloadIndex=false'
    )) {
        $gitIsolationArguments.Add([string]$argument)
    }
    if ($InspectFsmonitorFlags) {
        $gitIsolationArguments.Add('-c')
        $gitIsolationArguments.Add('core.fsmonitor=true')
    }
    $gitIsolationArguments.Add('-C')
    $gitIsolationArguments.Add($Repository)
    $result = Invoke-PackagedProcess `
        -FilePath $gitBefore.Path `
        -WorkingDirectory $Repository `
        -Arguments ($gitIsolationArguments + $Arguments) `
        -TimeoutSeconds 30 `
        -Environment $environment `
        -ClearGitEnvironment
    $gitAfter = Get-PackagedRegularFileIdentity -Path $gitBefore.Path -Label 'Program Files Git'
    Assert-PackagedFileIdentityUnchanged -Before $gitBefore -After $gitAfter -Label 'Program Files Git'
    if ($result.ExitCode -ne 0) {
        throw "Required Git identity command failed: git $($Arguments -join ' ')"
    }
    if ($Raw) {
        return ,([byte[]]$result.OutputBytes)
    }
    return (ConvertFrom-PackagedUtf8Bytes -Bytes ([byte[]]$result.OutputBytes)).Trim()
}

function Assert-PackagedRepositoryIndexFlags {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository)

    $fsmonitorBytes = Invoke-PackagedGit `
        -Repository $Repository `
        -Arguments @('ls-files', '-f', '-z') `
        -Raw `
        -InspectFsmonitorFlags
    Assert-PackagedIndexFlagRecords -Bytes $fsmonitorBytes -Kind Fsmonitor
    $indexFlagBytes = Invoke-PackagedGit -Repository $Repository -Arguments @('ls-files', '-v', '-z') -Raw
    Assert-PackagedIndexFlagRecords -Bytes $indexFlagBytes -Kind Tracked
    return [pscustomobject]@{
        IndexFlags = [byte[]]$indexFlagBytes
        FsmonitorFlags = [byte[]]$fsmonitorBytes
    }
}

function Resolve-PackagedRepository {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $root = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path).TrimEnd('\', '/')
    Assert-PackagedNoReparseAncestors -Path $root -IncludeLeaf
    $rootItem = Get-Item -Force -LiteralPath $root -ErrorAction Stop
    if (-not $rootItem.PSIsContainer) {
        throw 'RepositoryRoot must be a regular directory.'
    }
    $gitText = Invoke-PackagedGit -Repository $root -Arguments @('rev-parse', '--show-toplevel')
    $gitRoot = [IO.Path]::GetFullPath(($gitText -replace '/', [IO.Path]::DirectorySeparatorChar)).TrimEnd('\', '/')
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($root, $gitRoot)) {
        throw 'RepositoryRoot must be the exact repository root.'
    }
    return $root
}

function Get-PackagedRepositoryIdentity {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository)

    $root = Resolve-PackagedRepository -Path $Repository
    $indexFlags = Assert-PackagedRepositoryIndexFlags -Repository $root
    $indexEntries = Invoke-PackagedGit -Repository $root -Arguments @('ls-files', '--stage', '-z') -Raw
    $rawStatus = Invoke-PackagedGit -Repository $root -Arguments @('status', '--porcelain=v1', '-z', '--untracked-files=all') -Raw
    return [pscustomobject]@{
        Root = $root
        Head = Invoke-PackagedGit -Repository $root -Arguments @('rev-parse', 'HEAD')
        HeadTree = Invoke-PackagedGit -Repository $root -Arguments @('rev-parse', 'HEAD^{tree}')
        IndexEntries = [Convert]::ToBase64String([byte[]]$indexEntries)
        IndexFlags = [Convert]::ToBase64String([byte[]]$indexFlags.IndexFlags)
        FsmonitorFlags = [Convert]::ToBase64String([byte[]]$indexFlags.FsmonitorFlags)
        RawStatus = [Convert]::ToBase64String([byte[]]$rawStatus)
    }
}

function Assert-PackagedRepositoryIdentityUnchanged {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][pscustomobject]$Before,
        [Parameter(Mandatory)][pscustomobject]$After
    )

    if (
        -not [StringComparer]::OrdinalIgnoreCase.Equals([string]$Before.Root, [string]$After.Root) -or
        [string]$Before.Head -cne [string]$After.Head -or
        [string]$Before.HeadTree -cne [string]$After.HeadTree -or
        [string]$Before.IndexEntries -cne [string]$After.IndexEntries -or
        [string]$Before.IndexFlags -cne [string]$After.IndexFlags -or
        [string]$Before.FsmonitorFlags -cne [string]$After.FsmonitorFlags -or
        [string]$Before.RawStatus -cne [string]$After.RawStatus
    ) {
        throw 'Repository root, HEAD, tree, byte-exact index flags, or raw status changed during checkpoint.'
    }
}

function Test-PackagedContainedPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [IO.Path]::GetFullPath($Path)
    $prefix = $rootFull + [IO.Path]::DirectorySeparatorChar
    return $pathFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-PackagedActionResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowNull()][AllowEmptyCollection()][object[]]$Items,
        [Parameter(Mandatory)][string]$Action,
        [Parameter(Mandatory)][hashtable]$PropertyTypes
    )

    if ($null -eq $Items -or $Items.Count -ne 1 -or $null -eq $Items[0] -or $Items[0] -isnot [pscustomobject]) {
        throw "$Action action must return exactly one PSCustomObject."
    }
    $result = $Items[0]
    $actualNames = @($result.PSObject.Properties.Name | Sort-Object)
    $expectedNames = @($PropertyTypes.Keys | Sort-Object)
    if (($actualNames -join "`0") -cne ($expectedNames -join "`0")) {
        throw "$Action action result has an invalid property set."
    }
    foreach ($name in $expectedNames) {
        $value = $result.PSObject.Properties[$name].Value
        if ($null -eq $value -or $value.GetType() -ne $PropertyTypes[$name]) {
            throw "$Action action result property has an invalid exact type: $name"
        }
    }
    return $result
}

function Get-PackagedPayloadSetSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RecipePath,
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][datetime]$BuildStartUtc,
        [Parameter(Mandatory)][datetime]$BuildEndUtc
    )

    $settings = New-Object -TypeName Xml.XmlReaderSettings
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $stream = New-Object -TypeName IO.MemoryStream -ArgumentList @(,[IO.File]::ReadAllBytes($RecipePath))
    try {
        $reader = [Xml.XmlReader]::Create($stream, $settings)
        try {
            $document = New-Object -TypeName Xml.XmlDocument
            $document.XmlResolver = $null
            $document.Load($reader)
        }
        finally {
            $reader.Dispose()
        }
    }
    catch {
        throw 'Fresh recipe is malformed or unsafe XML.'
    }
    finally {
        $stream.Dispose()
    }

    $manager = New-Object -TypeName Xml.XmlNamespaceManager -ArgumentList $document.NameTable
    $manager.AddNamespace('m', 'http://schemas.microsoft.com/developer/msbuild/2003')
    $nodes = @($document.SelectNodes('/m:Project/m:ItemGroup/m:AppxPackagedFile | /m:Project/m:ItemGroup/m:AppXManifest', $manager))
    if ($nodes.Count -eq 0) {
        throw 'Fresh recipe contains no packaged payload entries.'
    }
    $packagePaths = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)
    $records = New-Object -TypeName 'Collections.Generic.List[string]'
    $assemblyIdentities = New-Object -TypeName 'Collections.Generic.List[object]'
    foreach ($node in $nodes) {
        $include = [Uri]::UnescapeDataString([string]$node.GetAttribute('Include'))
        $packageNode = $node.SelectSingleNode('m:PackagePath', $manager)
        $packagePath = if ($null -eq $packageNode) { 'AppxManifest.xml' } else { ([string]$packageNode.InnerText -replace '\\', '/') }
        if ([string]::IsNullOrWhiteSpace($include) -or -not [IO.Path]::IsPathRooted($include) -or [string]::IsNullOrWhiteSpace($packagePath) -or $packagePath.StartsWith('/') -or $packagePath.Contains(':') -or $packagePath -match '[\x00-\x1f\x7f]' -or @($packagePath.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -ne 0) {
            throw 'Fresh recipe contains an unsafe payload identity.'
        }
        if (-not $packagePaths.Add($packagePath)) {
            throw 'Fresh recipe contains a case-colliding or duplicate package path.'
        }
        $identity = Get-PackagedRegularFileIdentity -Path ([IO.Path]::GetFullPath($include)) -Label 'Recipe payload'
        $isFreshUnitTestAssembly = [StringComparer]::OrdinalIgnoreCase.Equals($packagePath, 'GraniteEdgeAI.UnitTests.dll')
        if ($isFreshUnitTestAssembly -and ($identity.LastWriteTimeUtc -lt $BuildStartUtc.AddSeconds(-2) -or $identity.LastWriteTimeUtc -gt $BuildEndUtc.AddSeconds(2))) {
            throw 'Recipe references a stale or future-dated generated payload.'
        }
        $records.Add($packagePath + [char]0 + $identity.Length.ToString([Globalization.CultureInfo]::InvariantCulture) + [char]0 + $identity.Sha256)
        if ([StringComparer]::OrdinalIgnoreCase.Equals($packagePath, 'GraniteEdgeAI.UnitTests.dll')) {
            $assemblyIdentities.Add($identity)
        }
    }
    if ($assemblyIdentities.Count -ne 1) {
        throw 'Fresh recipe must bind exactly one GraniteEdgeAI.UnitTests.dll payload.'
    }
    $assembly = $assemblyIdentities[0]
    if (-not (Test-PackagedContainedPath -Root $Repository -Path $assembly.Path)) {
        throw 'UnitTests assembly payload must be repository-contained.'
    }
    $canonicalRecords = @($records.ToArray() | Sort-Object)
    $payloadBytes = [Text.Encoding]::UTF8.GetBytes($canonicalRecords -join [char]10)
    return [pscustomobject]@{
        PayloadCount = [int]$records.Count
        PayloadSetSha256 = [string](Get-PackagedSha256Bytes -Bytes $payloadBytes)
        UnitTestAssemblySha256 = [string]$assembly.Sha256
        UnitTestAssemblyLength = [long]$assembly.Length
        UnitTestAssemblyLastWriteUtc = [datetime]$assembly.LastWriteTimeUtc
        UnitTestAssemblyPath = [string]$assembly.Path
    }
}

function ConvertFrom-StrictTrxCounters {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes,
        [string]$ExpectedUnitTestAssemblyPath,
        [string]$ExpectedUnitTestAssemblySha256,
        [long]$ExpectedUnitTestAssemblyLength = -1
    )

    $settings = New-Object -TypeName Xml.XmlReaderSettings
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $stream = New-Object -TypeName IO.MemoryStream -ArgumentList @(,$Bytes)
    try {
        $reader = [Xml.XmlReader]::Create($stream, $settings)
        try {
            $document = New-Object -TypeName Xml.XmlDocument
            $document.XmlResolver = $null
            $document.Load($reader)
        }
        finally {
            $reader.Dispose()
        }
    }
    catch {
        throw 'TRX is malformed or unsafe XML.'
    }
    finally {
        $stream.Dispose()
    }

    $manager = New-Object -TypeName Xml.XmlNamespaceManager -ArgumentList $document.NameTable
    $manager.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $testRun = $document.SelectSingleNode('/t:TestRun', $manager)
    $testRunId = [guid]::Empty
    if ($null -eq $testRun -or -not [guid]::TryParseExact([string]$testRun.GetAttribute('id'), 'D', [ref]$testRunId)) {
        throw 'TRX TestRun must have one canonical GUID identity.'
    }
    $summaries = @($document.SelectNodes('/t:TestRun/t:ResultSummary', $manager))
    if ($summaries.Count -ne 1) {
        throw 'TRX must contain exactly one ResultSummary element.'
    }
    $nodes = @($document.SelectNodes('/t:TestRun/t:ResultSummary/t:Counters', $manager))
    if ($nodes.Count -ne 1) {
        throw 'TRX must contain exactly one Counters element.'
    }
    $counterNode = $nodes[0]
    $names = @(
        'total'
        'executed'
        'passed'
        'failed'
        'error'
        'timeout'
        'aborted'
        'inconclusive'
        'passedButRunAborted'
        'notRunnable'
        'notExecuted'
        'disconnected'
        'warning'
        'completed'
        'inProgress'
        'pending'
    )
    if ($counterNode.Attributes.Count -ne $names.Count) {
        throw 'TRX Counters must contain exactly the 16 canonical attributes.'
    }
    $actualNames = @($counterNode.Attributes | ForEach-Object { $_.Name } | Sort-Object)
    if (($actualNames -join "`0") -cne (@($names | Sort-Object) -join "`0")) {
        throw 'TRX Counters contains a missing or extra attribute.'
    }
    $values = [ordered]@{}
    foreach ($name in $names) {
        $number = 0
        $attribute = $counterNode.Attributes[$name]
        if (-not [int]::TryParse($attribute.Value, [Globalization.NumberStyles]::None, [Globalization.CultureInfo]::InvariantCulture, [ref]$number) -or $number -lt 0) {
            throw "TRX counter is not a canonical nonnegative Int32: $name"
        }
        $values[$name] = [int]$number
    }
    foreach ($name in @($names | Where-Object { $_ -notin @('total', 'executed', 'passed') })) {
        if ($values[$name] -ne 0) {
            throw "TRX contains a non-passing or other outcome: $name"
        }
    }
    if ($values.total -le 0 -or $values.total -ne $values.executed -or $values.executed -ne $values.passed) {
        throw 'TRX requires total=executed=passed>0.'
    }
    $results = @($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $manager))
    if ($results.Count -ne $values.total) {
        throw 'TRX UnitTestResult count must equal the total counter.'
    }
    $definitions = @($document.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $manager))
    if ($definitions.Count -ne $results.Count) {
        throw 'TRX UnitTest definition count must equal its result count.'
    }
    $definitionById = @{}
    foreach ($definition in $definitions) {
        $definitionId = [guid]::Empty
        $definitionName = [string]$definition.GetAttribute('name')
        if (-not [guid]::TryParseExact([string]$definition.GetAttribute('id'), 'D', [ref]$definitionId) -or [string]::IsNullOrWhiteSpace($definitionName) -or $definitionName.Length -gt 512 -or $definitionName -match '[:\\/\x00-\x1f\x7f]') {
            throw 'TRX contains an invalid UnitTest definition identity.'
        }
        $key = $definitionId.ToString('D')
        if ($definitionById.ContainsKey($key)) {
            throw 'TRX contains a duplicate UnitTest definition identity.'
        }
        $executionNode = $definition.SelectSingleNode('t:Execution', $manager)
        $methodNode = $definition.SelectSingleNode('t:TestMethod', $manager)
        $definitionExecutionId = [guid]::Empty
        $className = if ($null -eq $methodNode) { '' } else { [string]$methodNode.GetAttribute('className') }
        $methodName = if ($null -eq $methodNode) { '' } else { [string]$methodNode.GetAttribute('name') }
        $storageValue = [string]$definition.GetAttribute('storage')
        $codeBaseValue = if ($null -eq $methodNode) { '' } else { [string]$methodNode.GetAttribute('codeBase') }
        $storageName = [IO.Path]::GetFileName($storageValue)
        $codeBaseName = [IO.Path]::GetFileName($codeBaseValue)
        if (
            $null -eq $executionNode -or
            -not [guid]::TryParseExact([string]$executionNode.GetAttribute('id'), 'D', [ref]$definitionExecutionId) -or
            [string]::IsNullOrWhiteSpace($className) -or
            [string]::IsNullOrWhiteSpace($methodName) -or
            $className -match '[:\\/\x00-\x1f\x7f]' -or
            $methodName -match '[:\\/\x00-\x1f\x7f]' -or
            -not [StringComparer]::OrdinalIgnoreCase.Equals($storageName, 'GraniteEdgeAI.UnitTests.dll') -or
            -not [StringComparer]::OrdinalIgnoreCase.Equals($codeBaseName, 'GraniteEdgeAI.UnitTests.dll')
        ) {
            throw 'TRX UnitTest definition provenance is incomplete or unsafe.'
        }
        if (-not [string]::IsNullOrWhiteSpace($ExpectedUnitTestAssemblyPath)) {
            $expectedAssembly = [IO.Path]::GetFullPath($ExpectedUnitTestAssemblyPath)
            if (
                -not [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($storageValue), $expectedAssembly) -or
                -not [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($codeBaseValue), $expectedAssembly)
            ) {
                throw 'TRX storage and codeBase must bind the exact recipe UnitTests assembly path.'
            }
            $trxAssembly = Get-PackagedRegularFileIdentity -Path $expectedAssembly -Label 'TRX UnitTests assembly'
            if ($trxAssembly.Sha256 -cne $ExpectedUnitTestAssemblySha256 -or $trxAssembly.Length -ne $ExpectedUnitTestAssemblyLength) {
                throw 'TRX UnitTests assembly hash/length differs from the exact recipe payload.'
            }
        }
        $definitionById[$key] = [pscustomobject]@{
            Name = [string]$definitionName
            ExecutionId = [string]$definitionExecutionId.ToString('D')
            ClassName = [string]$className
            MethodName = [string]$methodName
        }
    }
    $executionIds = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::Ordinal)
    $identities = New-Object -TypeName 'Collections.Generic.List[object]'
    foreach ($result in $results) {
        $testId = [guid]::Empty
        $executionId = [guid]::Empty
        $testName = [string]$result.GetAttribute('testName')
        if (-not [guid]::TryParseExact([string]$result.GetAttribute('testId'), 'D', [ref]$testId) -or -not [guid]::TryParseExact([string]$result.GetAttribute('executionId'), 'D', [ref]$executionId)) {
            throw 'TRX result contains an invalid test or execution GUID.'
        }
        if ([string]$result.GetAttribute('outcome') -cne 'Passed') {
            throw 'TRX result outcome must be exactly Passed.'
        }
        if ([string]::IsNullOrWhiteSpace($testName) -or $testName.Length -gt 512 -or $testName -match '[:\\/\x00-\x1f\x7f]') {
            throw 'TRX result contains an invalid test name.'
        }
        $testKey = $testId.ToString('D')
        $executionKey = $executionId.ToString('D')
        if (-not $definitionById.ContainsKey($testKey) -or [string]$definitionById[$testKey].Name -cne $testName -or [string]$definitionById[$testKey].ExecutionId -cne $executionKey) {
            throw 'TRX result is not bound to an exact matching UnitTest definition.'
        }
        if (-not $executionIds.Add($executionKey)) {
            throw 'TRX contains a duplicate execution identity.'
        }
        $identities.Add([pscustomobject][ordered]@{
            testId = [string]$testKey
            executionId = [string]$executionKey
            name = [string]$testName
            className = [string]$definitionById[$testKey].ClassName
            methodName = [string]$definitionById[$testKey].MethodName
        })
    }
    return [pscustomobject]@{
        Counters = $values
        TestRunId = [string]$testRunId.ToString('D')
        TestIdentities = @($identities.ToArray() | Sort-Object -Property name, executionId)
    }
}

function Read-PackagedTrxSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][datetime]$RunStartUtc,
        [Parameter(Mandatory)][datetime]$RunEndUtc,
        [string]$ExpectedUnitTestAssemblyPath,
        [string]$ExpectedUnitTestAssemblySha256,
        [long]$ExpectedUnitTestAssemblyLength = -1
    )

    try {
        $full = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path)
    }
    catch {
        throw 'Operation-owned TRX must exist as an exact regular file.'
    }
    Assert-PackagedNoReparseAncestors -Path $full -IncludeLeaf
    $itemBefore = Get-Item -Force -LiteralPath $full -ErrorAction Stop
    if ($itemBefore.PSIsContainer -or ($itemBefore.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Operation-owned TRX must be an exact regular non-reparse file.'
    }
    if ($itemBefore.LastWriteTimeUtc -lt $RunStartUtc.AddSeconds(-2) -or $itemBefore.LastWriteTimeUtc -gt $RunEndUtc.AddSeconds(2)) {
        throw 'TRX timestamp is stale or future-dated for the bounded run.'
    }
    $bytes = [IO.File]::ReadAllBytes($full)
    $itemAfter = Get-Item -Force -LiteralPath $full -ErrorAction Stop
    if (
        [long]$bytes.LongLength -ne [long]$itemBefore.Length -or
        [long]$itemAfter.Length -ne [long]$itemBefore.Length -or
        [datetime]$itemAfter.LastWriteTimeUtc -ne [datetime]$itemBefore.LastWriteTimeUtc -or
        [datetime]$itemAfter.CreationTimeUtc -ne [datetime]$itemBefore.CreationTimeUtc
    ) {
        throw 'TRX length changed while its bytes were captured.'
    }
    $capturedHash = Get-PackagedSha256Bytes -Bytes $bytes
    return [pscustomobject]@{
        Identity = [pscustomobject]@{
            Path = [string]$full
            Length = [long]$itemBefore.Length
            Sha256 = [string]$capturedHash
            LastWriteTimeUtc = [datetime]$itemBefore.LastWriteTimeUtc
            CreationTimeUtc = [datetime]$itemBefore.CreationTimeUtc
        }
        Bytes = $bytes
        Model = ConvertFrom-StrictTrxCounters `
            -Bytes $bytes `
            -ExpectedUnitTestAssemblyPath $ExpectedUnitTestAssemblyPath `
            -ExpectedUnitTestAssemblySha256 $ExpectedUnitTestAssemblySha256 `
            -ExpectedUnitTestAssemblyLength $ExpectedUnitTestAssemblyLength
    }
}

function Get-StrictTrxCounters {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TrxPath,
        [Parameter(Mandatory)][datetime]$RunStartUtc,
        [Parameter(Mandatory)][datetime]$RunEndUtc
    )

    $snapshot = Read-PackagedTrxSnapshot -Path $TrxPath -RunStartUtc $RunStartUtc -RunEndUtc $RunEndUtc
    return $snapshot.Model.Counters
}

function Remove-PackagedOwnedDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$EvidenceRoot,
        [Parameter(Mandatory)][string]$OperationDirectory
    )

    $root = [IO.Path]::GetFullPath($EvidenceRoot).TrimEnd('\', '/')
    $operation = [IO.Path]::GetFullPath($OperationDirectory).TrimEnd('\', '/')
    $expectedParent = Split-Path -Parent $operation
    $name = Split-Path -Leaf $operation
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($root, $expectedParent) -or $name -notmatch '^operation-[0-9a-f]{32}$') {
        throw 'Operation ownership validation failed before cleanup.'
    }
    if (-not $script:PackagedOperationOwnership.ContainsKey($operation)) {
        throw 'Cleanup requires a live module-owned operation lease.'
    }
    $lease = [PackagedOperationLease]$script:PackagedOperationOwnership[$operation]
    if (-not $lease.IsLive) { throw 'Cleanup operation lease is not live.' }
    if (Test-Path -LiteralPath $operation) {
        $rootItem = Get-Item -Force -LiteralPath $operation -ErrorAction Stop
        if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Operation ownership root became a reparse point.'
        }
        $deadline = [datetime]::UtcNow.AddSeconds(5)
        foreach ($entry in [IO.Directory]::EnumerateFileSystemEntries($operation)) {
            if ([datetime]::UtcNow -gt $deadline) {
                throw 'Operation-owned cleanup exceeded five seconds.'
            }
            if ([StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($entry), [IO.Path]::GetFullPath($lease.MarkerPath))) { continue }
            $item = Get-Item -Force -LiteralPath $entry -ErrorAction Stop
            if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Operation-owned cleanup refuses nested directories and reparse points.'
            }
            [IO.File]::SetAttributes($item.FullName, [IO.FileAttributes]::Normal)
            [IO.File]::Delete($item.FullName)
        }
        $remaining = @([IO.Directory]::EnumerateFileSystemEntries($operation))
        if ($remaining.Count -ne 1 -or -not [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($remaining[0]), [IO.Path]::GetFullPath($lease.MarkerPath))) {
            throw 'Operation-owned cleanup did not reach the locked marker-only state.'
        }
        $lease.DeleteEmptyDirectory()
        [void]$script:PackagedOperationOwnership.Remove($operation)
    }
    else {
        throw 'Live operation directory disappeared before cleanup.'
    }
}

function Complete-PackagedOperationOwnership {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$OperationDirectory)

    $operation = [IO.Path]::GetFullPath($OperationDirectory).TrimEnd('\', '/')
    if (-not $script:PackagedOperationOwnership.ContainsKey($operation)) {
        throw 'Publication requires a live module-owned operation lease.'
    }
    $lease = [PackagedOperationLease]$script:PackagedOperationOwnership[$operation]
    if (-not $lease.IsLive) { throw 'Publication operation lease is not live.' }
    $lease.ReleaseForPublication()
    [void]$script:PackagedOperationOwnership.Remove($operation)
}

function Remove-PackagedPublishedTrx {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$OperationDirectory,
        [Parameter(Mandatory)][string]$TrxName,
        [Parameter(Mandatory)][string]$ExpectedSha256,
        [Parameter(Mandatory)][long]$ExpectedLength
    )

    if ($TrxName -cnotmatch '^packaged-checkpoint-[0-9a-f]{32}\.trx$' -or $ExpectedSha256 -cnotmatch '^[0-9a-f]{64}$' -or $ExpectedLength -lt 0) {
        throw 'Published TRX ownership identity is invalid.'
    }
    $root = [IO.Path]::GetFullPath($OperationDirectory).TrimEnd('\', '/')
    $path = [IO.Path]::GetFullPath((Join-Path -Path $root -ChildPath $TrxName))
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals((Split-Path -Parent $path), $root)) {
        throw 'Published TRX escaped its operation directory.'
    }
    $identity = Get-PackagedRegularFileIdentity -Path $path -Label 'Published TRX'
    if ($identity.Length -ne $ExpectedLength -or $identity.Sha256 -cne $ExpectedSha256) {
        throw 'Published TRX identity changed before privacy cleanup.'
    }
    [IO.File]::Delete($path)
    if (Test-Path -LiteralPath $path) {
        throw 'Published raw TRX remained after privacy cleanup.'
    }
}

function Assert-PackagedFilterTestIdentityBinding {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Filter,
        [Parameter(Mandatory)][object[]]$TestIdentity
    )

    $clauses = @($Filter -split '\|')
    if ($clauses.Count -eq 0) { throw 'Checkpoint filter contains no supported clauses.' }
    $values = New-Object -TypeName 'Collections.Generic.List[string]'
    foreach ($clause in $clauses) {
        if ($clause -cnotmatch '^FullyQualifiedName~(?<value>[A-Za-z0-9_.+]+)$') {
            throw 'Filter supports only OR-separated FullyQualifiedName~value clauses.'
        }
        $values.Add([string]$Matches.value)
    }
    $matchedClauses = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)
    foreach ($identity in $TestIdentity) {
        $matched = $false
        $fullyQualifiedName = [string]$identity.className + '.' + [string]$identity.methodName
        foreach ($value in $values) {
            if ($fullyQualifiedName -like ('*' + $value + '*')) {
                $matched = $true
                [void]$matchedClauses.Add($value)
            }
        }
        if (-not $matched) {
            throw 'Test identity does not satisfy the exact requested filter.'
        }
    }
    if ($matchedClauses.Count -ne $values.Count) {
        throw 'Every requested filter clause must discover at least one exact test identity.'
    }
}

function New-PackagedOperationDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$EvidenceDirectory
    )

    $evidenceRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $EvidenceDirectory -ErrorAction Stop).Path).TrimEnd('\', '/')
    Assert-PackagedNoReparseAncestors -Path $evidenceRoot -IncludeLeaf
    $rootItem = Get-Item -Force -LiteralPath $evidenceRoot -ErrorAction Stop
    if (-not $rootItem.PSIsContainer) {
        throw 'EvidenceDirectory must be a regular non-reparse directory.'
    }
    if ((Test-PackagedContainedPath -Root $Repository -Path $evidenceRoot) -or [StringComparer]::OrdinalIgnoreCase.Equals($Repository, $evidenceRoot)) {
        throw 'EvidenceDirectory must be outside the source repository.'
    }
    $operationDirectory = Join-Path -Path $evidenceRoot -ChildPath ('operation-' + [guid]::NewGuid().ToString('N'))
    if (Test-Path -LiteralPath $operationDirectory) {
        throw 'Fresh operation ownership path unexpectedly exists.'
    }
    [IO.Directory]::CreateDirectory($operationDirectory) | Out-Null
    try {
        Assert-PackagedNoReparseAncestors -Path $operationDirectory -IncludeLeaf
        if (@([IO.Directory]::EnumerateFileSystemEntries($operationDirectory)).Count -ne 0) {
            throw 'Fresh operation ownership directory was not empty.'
        }
        $ownershipPath = Join-Path -Path $operationDirectory -ChildPath '.operation-owner'
        $ownershipLease = New-Object -TypeName PackagedOperationLease -ArgumentList @($operationDirectory, $ownershipPath)
    }
    catch {
        if ((Test-Path -LiteralPath $operationDirectory) -and @([IO.Directory]::EnumerateFileSystemEntries($operationDirectory)).Count -eq 0) {
            [IO.Directory]::Delete($operationDirectory, $false)
        }
        throw
    }
    $script:PackagedOperationOwnership.Add([IO.Path]::GetFullPath($operationDirectory).TrimEnd('\', '/'), $ownershipLease)
    return [pscustomobject]@{
        EvidenceRoot = $evidenceRoot
        OperationDirectory = $operationDirectory
    }
}

function Invoke-PackagedCheckpointCore {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$RecipePath,
        [Parameter(Mandatory)][string]$EvidenceDirectory,
        [Parameter(Mandatory)][string]$Filter,
        [Parameter(Mandatory)][scriptblock]$BuildAction,
        [Parameter(Mandatory)][scriptblock]$TestAction
    )

    if ([string]::IsNullOrWhiteSpace($Filter) -or $Filter.Length -gt 4096 -or $Filter -cnotmatch '^FullyQualifiedName~[A-Za-z0-9_.+]+(?:\|FullyQualifiedName~[A-Za-z0-9_.+]+)*$') {
        throw 'Filter must use OR-separated FullyQualifiedName~value clauses and be at most 4096 characters.'
    }
    $repository = Resolve-PackagedRepository -Path $RepositoryRoot
    $preIdentity = Get-PackagedRepositoryIdentity -Repository $repository
    if ($preIdentity.RawStatus.Length -ne 0) {
        throw 'Repository must be clean before packaged checkpoint.'
    }
    $recipe = [IO.Path]::GetFullPath($RecipePath)
    if (-not (Test-PackagedContainedPath -Root $repository -Path $recipe)) {
        throw 'RecipePath must be contained by the repository.'
    }
    Assert-PackagedNoReparseAncestors -Path $recipe
    $recipeRelativeName = $recipe.Substring($repository.Length).TrimStart('\', '/') -replace '\\', '/'
    $operation = New-PackagedOperationDirectory -Repository $repository -EvidenceDirectory $EvidenceDirectory
    $operationDirectory = $operation.OperationDirectory
    $trxName = 'packaged-checkpoint-' + [guid]::NewGuid().ToString('N') + '.trx'
    $trxPath = Join-Path -Path $operationDirectory -ChildPath $trxName
    try {
        if (Test-Path -LiteralPath $recipe) {
            [void](Get-PackagedRegularFileIdentity -Path $recipe -Label 'Prior recipe')
            Remove-Item -Force -LiteralPath $recipe
        }
        $started = [DateTime]::UtcNow
        $buildContext = [pscustomobject]@{
            RepositoryRoot = $repository
            RecipePath = $recipe
            StartedUtc = $started
        }
        $buildItems = @(& $BuildAction $buildContext)
        $buildEnd = [DateTime]::UtcNow
        $buildResult = Assert-PackagedActionResult -Items $buildItems -Action 'Build' -PropertyTypes @{ ExitCode = [int] }
        if ($buildResult.ExitCode -ne 0) {
            throw "Build action failed with exit code $($buildResult.ExitCode)."
        }
        $recipeAfterBuild = Get-PackagedRegularFileIdentity -Path $recipe -Label 'Fresh recipe'
        if ($recipeAfterBuild.LastWriteTimeUtc -lt $started.AddSeconds(-2) -or $recipeAfterBuild.LastWriteTimeUtc -gt $buildEnd.AddSeconds(2)) {
            throw 'Recipe timestamp is stale or future-dated.'
        }
        $recipeBeforeTest = Get-PackagedRegularFileIdentity -Path $recipe -Label 'Fresh recipe immediately before test'
        Assert-PackagedFileIdentityUnchanged -Before $recipeAfterBuild -After $recipeBeforeTest -Label 'Fresh recipe'
        $payloadSnapshot = Get-PackagedPayloadSetSnapshot -RecipePath $recipe -Repository $repository -BuildStartUtc $started -BuildEndUtc $buildEnd
        $recipeAfterPayloadCapture = Get-PackagedRegularFileIdentity -Path $recipe -Label 'Fresh recipe after payload capture'
        Assert-PackagedFileIdentityUnchanged -Before $recipeBeforeTest -After $recipeAfterPayloadCapture -Label 'Fresh recipe'
        $runStart = [DateTime]::UtcNow
        $context = [pscustomobject]@{
            RepositoryRoot = $repository
            RecipePath = $recipe
            ResultsDirectory = $operationDirectory
            TrxName = $trxName
            TrxPath = $trxPath
            Filter = $Filter
            RunStartUtc = $runStart
        }
        $testItems = @(& $TestAction $context)
        $runEnd = [DateTime]::UtcNow
        $testResult = Assert-PackagedActionResult `
            -Items $testItems `
            -Action 'Test' `
            -PropertyTypes @{
                ExitCode = [int]
                Output = [string]
                Error = [string]
                RecipePathUsed = [string]
                TrxPathUsed = [string]
                FilterUsed = [string]
            }
        if ($testResult.ExitCode -ne 0) {
            throw "Test runner failed with exit code $($testResult.ExitCode)."
        }
        if ([string]$testResult.FilterUsed -cne $Filter) {
            throw 'Test action did not use the exact requested filter.'
        }
        $recipeAfterTest = Get-PackagedRegularFileIdentity -Path $recipe -Label 'Fresh recipe immediately after test'
        Assert-PackagedFileIdentityUnchanged -Before $recipeBeforeTest -After $recipeAfterTest -Label 'Fresh recipe'
        $payloadAfterTest = Get-PackagedPayloadSetSnapshot -RecipePath $recipe -Repository $repository -BuildStartUtc $started -BuildEndUtc $buildEnd
        if (
            $payloadAfterTest.PayloadCount -ne $payloadSnapshot.PayloadCount -or
            $payloadAfterTest.PayloadSetSha256 -cne $payloadSnapshot.PayloadSetSha256 -or
            $payloadAfterTest.UnitTestAssemblySha256 -cne $payloadSnapshot.UnitTestAssemblySha256 -or
            $payloadAfterTest.UnitTestAssemblyLength -ne $payloadSnapshot.UnitTestAssemblyLength -or
            $payloadAfterTest.UnitTestAssemblyLastWriteUtc -ne $payloadSnapshot.UnitTestAssemblyLastWriteUtc -or
            -not [StringComparer]::OrdinalIgnoreCase.Equals($payloadAfterTest.UnitTestAssemblyPath, $payloadSnapshot.UnitTestAssemblyPath)
        ) {
            throw 'Recipe payload identity changed during the packaged test run.'
        }
        if (-not [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($testResult.RecipePathUsed), $recipe)) {
            throw 'Test runner did not receive the exact fresh recipe.'
        }
        if (-not [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($testResult.TrxPathUsed), $trxPath)) {
            throw 'Test runner did not report the exact operation-owned TRX path.'
        }
        $trxSnapshot = Read-PackagedTrxSnapshot `
            -Path $trxPath `
            -RunStartUtc $runStart `
            -RunEndUtc $runEnd `
            -ExpectedUnitTestAssemblyPath $payloadSnapshot.UnitTestAssemblyPath `
            -ExpectedUnitTestAssemblySha256 $payloadSnapshot.UnitTestAssemblySha256 `
            -ExpectedUnitTestAssemblyLength $payloadSnapshot.UnitTestAssemblyLength
        Assert-PackagedFilterTestIdentityBinding -Filter $Filter -TestIdentity ([object[]]$trxSnapshot.Model.TestIdentities)
        $operationEntries = @([IO.Directory]::EnumerateFileSystemEntries($operationDirectory))
        $ownerPath = Join-Path -Path $operationDirectory -ChildPath '.operation-owner'
        $expectedOperationEntries = @([IO.Path]::GetFullPath($trxPath), [IO.Path]::GetFullPath($ownerPath)) | Sort-Object
        $actualOperationEntries = @($operationEntries | ForEach-Object { [IO.Path]::GetFullPath($_) } | Sort-Object)
        if ($operationEntries.Count -ne 2 -or ($actualOperationEntries -join [char]0) -cne ($expectedOperationEntries -join [char]0)) {
            throw 'Operation-owned evidence must contain the exact TRX and locked owner marker only before checkpoint publication.'
        }
        $postIdentity = Get-PackagedRepositoryIdentity -Repository $repository
        Assert-PackagedRepositoryIdentityUnchanged -Before $preIdentity -After $postIdentity
        if ($postIdentity.RawStatus.Length -ne 0) {
            throw 'Repository must remain clean after packaged checkpoint.'
        }
        $checkpointPath = Join-Path -Path $operationDirectory -ChildPath 'checkpoint.json'
        if (Test-Path -LiteralPath $checkpointPath) {
            throw 'Core orchestration cannot publish accepted checkpoint evidence.'
        }
        return [pscustomobject]@{
            EvidenceClass = [string]$script:EvidenceClassification
            Publishable = [bool]$false
            Discovered = [int]$trxSnapshot.Model.Counters.total
            Executed = [int]$trxSnapshot.Model.Counters.executed
            Passed = [int]$trxSnapshot.Model.Counters.passed
            Failed = [int]$trxSnapshot.Model.Counters.failed
            Filter = [string]$Filter
            FilterSha256 = [string](Get-PackagedSha256Bytes -Bytes ([Text.Encoding]::UTF8.GetBytes($Filter)))
            TestRunId = [string]$trxSnapshot.Model.TestRunId
            TestIdentities = [object[]]$trxSnapshot.Model.TestIdentities
            PreHead = [string]$preIdentity.Head
            PostHead = [string]$postIdentity.Head
            PreTree = [string]$preIdentity.HeadTree
            PostTree = [string]$postIdentity.HeadTree
            PreIndexTree = [string]($preIdentity.IndexEntries + '.' + $preIdentity.IndexFlags + '.' + $preIdentity.FsmonitorFlags)
            PostIndexTree = [string]($postIdentity.IndexEntries + '.' + $postIdentity.IndexFlags + '.' + $postIdentity.FsmonitorFlags)
            RecipeName = [string]$recipeRelativeName
            RecipeSha256 = [string]$recipeAfterTest.Sha256
            RecipeLength = [long]$recipeAfterTest.Length
            RecipeLastWriteUtc = [string]$recipeAfterTest.LastWriteTimeUtc.ToString('o')
            PayloadCount = [int]$payloadSnapshot.PayloadCount
            PayloadSetSha256 = [string]$payloadSnapshot.PayloadSetSha256
            UnitTestAssemblySha256 = [string]$payloadSnapshot.UnitTestAssemblySha256
            UnitTestAssemblyLength = [long]$payloadSnapshot.UnitTestAssemblyLength
            UnitTestAssemblyLastWriteUtc = [string]$payloadSnapshot.UnitTestAssemblyLastWriteUtc.ToString('o')
            TrxName = [string]$trxName
            TrxSha256 = [string]$trxSnapshot.Identity.Sha256
            TrxLength = [long]$trxSnapshot.Identity.Length
            TrxLastWriteUtc = [string]$trxSnapshot.Identity.LastWriteTimeUtc.ToString('o')
            StartedUtc = [string]$started.ToString('o')
            CompletedUtc = [string]$runEnd.ToString('o')
            EvidenceRoot = [string]$operation.EvidenceRoot
            OperationDirectory = [string]$operationDirectory
        }
    }
    catch {
        $failure = $_.Exception
        try {
            Remove-PackagedOwnedDirectory -EvidenceRoot $operation.EvidenceRoot -OperationDirectory $operationDirectory
        }
        catch {
            throw 'Checkpoint failed and exact operation-owned cleanup also failed.'
        }
        throw $failure
    }
}

Export-ModuleMember -Function @(
    'Invoke-PackagedCheckpointCore'
    'Invoke-PackagedProcess'
    'Get-PackagedRegularFileIdentity'
    'Assert-PackagedFileIdentityUnchanged'
    'Remove-PackagedOwnedDirectory'
    'Remove-PackagedPublishedTrx'
    'Complete-PackagedOperationOwnership'
)
