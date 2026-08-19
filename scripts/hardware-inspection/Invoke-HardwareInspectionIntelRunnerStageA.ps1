[CmdletBinding()]
param(
    [string] $EvaluatedRoot,
    [string] $ApprovedSha,
    [string] $LocalWorkRoot,
    [string] $SummaryJsonPath,
    [string] $SummaryMarkdownPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:StageAFailure = 'HI-RUNNER-STAGEA-TESTS-FAILED: deterministic validation failed.'
$script:StageATrxNamespace = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
$script:StageAMaximumTrxBytes = 16MB
$script:StageAMaximumProcessStreamBytes = 16MB # Per stdout/stderr log; excess is drained and discarded.
$script:StageAOwnedProcesses = $null
$script:StageAProcessJob = $null
$script:StageAPendingEvaluatedRoot = $null
$script:StageAPendingSummaryJsonPath = $null
$script:StageAPendingSummaryMarkdownPath = $null
$script:StageAPendingSummaryJson = $null
$script:StageAPendingSummaryMarkdown = $null

function Initialize-StageACappedDrain {
if ($null -eq ('HardwareInspection.StageA.CappedDrain' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
namespace HardwareInspection.StageA {
    public static class CappedDrain {
        public static async Task<bool> CopyAsync(Stream source, string path, long maximumBytes) {
            var buffer = new byte[1048576]; long written = 0; bool truncated = false;
            using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1048576, true)) {
                int count;
                while ((count = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0) {
                    var accepted = (int)Math.Min((long)count, Math.Max(0L, maximumBytes - written));
                    if (accepted != 0) { await destination.WriteAsync(buffer, 0, accepted).ConfigureAwait(false); written += accepted; }
                    if (accepted != count) { truncated = true; }
                }
                await destination.FlushAsync().ConfigureAwait(false);
            }
            return truncated;
        }
    }

    public static class CancellationState {
        private const int Active = 0;
        private const int Cancelled = 1;
        private const int Completed = 2;
        private static readonly object Sync = new object();
        private static int state = Completed;
        private static ConsoleCancelEventHandler handler;

        private static void HandleCancel(object sender, ConsoleCancelEventArgs eventArgs) {
            Interlocked.CompareExchange(ref state, Cancelled, Active);
            eventArgs.Cancel = true;
        }

        public static bool IsCancellationRequested {
            get { return Interlocked.CompareExchange(ref state, Active, Active) == Cancelled; }
        }

        public static bool Request() {
            return Interlocked.CompareExchange(ref state, Cancelled, Active) == Active;
        }

        public static bool TryComplete() {
            return Interlocked.CompareExchange(ref state, Completed, Active) == Active;
        }

        public static void Install() {
            lock (Sync) {
                if (handler != null) {
                    throw new InvalidOperationException("Cancellation handling is already installed.");
                }
                Interlocked.Exchange(ref state, Active);
                handler = new ConsoleCancelEventHandler(HandleCancel);
                try {
                    Console.CancelKeyPress += handler;
                }
                catch {
                    handler = null;
                    Interlocked.Exchange(ref state, Cancelled);
                    throw;
                }
            }
        }

        public static void Remove() {
            lock (Sync) {
                if (handler == null) {
                    throw new InvalidOperationException("Cancellation handling is not installed.");
                }
                ConsoleCancelEventHandler current = handler;
                Console.CancelKeyPress -= current;
                handler = null;
            }
        }
    }

    public sealed class ProcessJob : IDisposable {
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const int JobObjectExtendedLimitInformation = 9;
        private IntPtr handle;

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr securityAttributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr job,
            int informationClass,
            IntPtr information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryInformationJobObject(
            IntPtr job,
            int informationClass,
            IntPtr information,
            uint informationLength,
            IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        private ProcessJob(IntPtr jobHandle) { handle = jobHandle; }

        public static ProcessJob CreateKillOnClose() {
            IntPtr job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero || job == new IntPtr(-1)) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            try {
                var information = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                information.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr buffer = Marshal.AllocHGlobal(length);
                try {
                    Marshal.StructureToPtr(information, buffer, false);
                    if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, buffer, (uint)length)) {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                finally { Marshal.FreeHGlobal(buffer); }
                return new ProcessJob(job);
            }
            catch {
                CloseHandle(job);
                throw;
            }
        }

        internal IntPtr GetHandleForChildCreation() {
            IntPtr job = Interlocked.CompareExchange(ref handle, IntPtr.Zero, IntPtr.Zero);
            if (job == IntPtr.Zero) { throw new ObjectDisposedException("ProcessJob"); }
            return job;
        }

        public void Dispose() {
            IntPtr job = Interlocked.Exchange(ref handle, IntPtr.Zero);
            if (job == IntPtr.Zero) { return; }
            Exception failure = null;
            try {
                if (!TerminateJobObject(job, 1)) {
                    failure = new Win32Exception(Marshal.GetLastWin32Error());
                }
                int length = Marshal.SizeOf(typeof(JOBOBJECT_BASIC_ACCOUNTING_INFORMATION));
                IntPtr buffer = Marshal.AllocHGlobal(length);
                try {
                    var deadline = Stopwatch.StartNew();
                    while (true) {
                        if (!QueryInformationJobObject(job, 1, buffer, (uint)length, IntPtr.Zero)) {
                            if (failure == null) {
                                failure = new Win32Exception(Marshal.GetLastWin32Error());
                            }
                            break;
                        }
                        var accounting = (JOBOBJECT_BASIC_ACCOUNTING_INFORMATION)
                            Marshal.PtrToStructure(buffer, typeof(JOBOBJECT_BASIC_ACCOUNTING_INFORMATION));
                        if (accounting.ActiveProcesses == 0) { break; }
                        if (deadline.ElapsedMilliseconds >= 5000) {
                            if (failure == null) { failure = new TimeoutException(); }
                            break;
                        }
                        Thread.Sleep(20);
                    }
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            finally {
                if (!CloseHandle(job) && failure == null) {
                    failure = new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            if (failure != null) {
                throw failure;
            }
        }
    }

    public sealed class ContainedProcess : IDisposable {
        private const uint CREATE_SUSPENDED = 0x00000004;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const uint STARTF_USESTDHANDLES = 0x00000100;
        private const uint HANDLE_FLAG_INHERIT = 0x00000001;
        private const int PROC_THREAD_ATTRIBUTE_HANDLE_LIST = 0x00020002;
        private const int PROC_THREAD_ATTRIBUTE_JOB_LIST = 0x0002000D;
        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint WAIT_OBJECT_0 = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)] public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOEX {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(
            out IntPtr readPipe,
            out IntPtr writePipe,
            ref SECURITY_ATTRIBUTES pipeAttributes,
            uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            ref SECURITY_ATTRIBUTES securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(
            IntPtr attributeList,
            int attributeCount,
            int flags,
            ref UIntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr attributeList,
            uint flags,
            IntPtr attribute,
            IntPtr value,
            UIntPtr size,
            IntPtr previousValue,
            IntPtr returnSize);

        [DllImport("kernel32.dll")]
        private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(
            string applicationName,
            StringBuilder commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref STARTUPINFOEX startupInfo,
            out PROCESS_INFORMATION processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr thread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        public Process Process { get; private set; }
        public Stream StandardOutput { get; private set; }
        public Stream StandardError { get; private set; }

        private ContainedProcess(Process process, Stream standardOutput, Stream standardError) {
            Process = process;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        private static IntPtr BuildEnvironment(ProcessStartInfo information) {
            var entries = new List<string>();
            foreach (DictionaryEntry entry in information.EnvironmentVariables) {
                entries.Add((string)entry.Key + "=" + (string)entry.Value);
            }
            entries.Sort(StringComparer.OrdinalIgnoreCase);
            return Marshal.StringToHGlobalUni(string.Join("\0", entries.ToArray()) + "\0\0");
        }

        public static ContainedProcess StartSuspendedAssigned(
            ProcessStartInfo information,
            ProcessJob job) {
            if (information == null || job == null || information.UseShellExecute ||
                !information.RedirectStandardOutput || !information.RedirectStandardError) {
                throw new InvalidOperationException("Contained process configuration is invalid.");
            }
            var security = new SECURITY_ATTRIBUTES {
                nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)),
                lpSecurityDescriptor = IntPtr.Zero,
                bInheritHandle = true
            };
            IntPtr stdoutRead = IntPtr.Zero, stdoutWrite = IntPtr.Zero;
            IntPtr stderrRead = IntPtr.Zero, stderrWrite = IntPtr.Zero;
            IntPtr stdin = IntPtr.Zero, environment = IntPtr.Zero;
            IntPtr attributeList = IntPtr.Zero, handleList = IntPtr.Zero;
            IntPtr jobList = IntPtr.Zero;
            bool attributeListInitialized = false;
            PROCESS_INFORMATION created = new PROCESS_INFORMATION();
            Process process = null;
            FileStream stdout = null, stderr = null;
            SafeFileHandle stdoutSafe = null, stderrSafe = null;
            bool resumed = false;
            try {
                if (!CreatePipe(out stdoutRead, out stdoutWrite, ref security, 0) ||
                    !CreatePipe(out stderrRead, out stderrWrite, ref security, 0) ||
                    !SetHandleInformation(stdoutRead, HANDLE_FLAG_INHERIT, 0) ||
                    !SetHandleInformation(stderrRead, HANDLE_FLAG_INHERIT, 0)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                stdin = CreateFile(
                    "NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    ref security, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
                if (stdin == IntPtr.Zero || stdin == new IntPtr(-1)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                IntPtr jobHandle = job.GetHandleForChildCreation();
                UIntPtr attributeSize = UIntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref attributeSize);
                attributeList = Marshal.AllocHGlobal((int)attributeSize.ToUInt64());
                if (!InitializeProcThreadAttributeList(attributeList, 2, 0, ref attributeSize)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                attributeListInitialized = true;
                handleList = Marshal.AllocHGlobal(IntPtr.Size * 3);
                Marshal.WriteIntPtr(handleList, 0, stdin);
                Marshal.WriteIntPtr(handleList, IntPtr.Size, stdoutWrite);
                Marshal.WriteIntPtr(handleList, IntPtr.Size * 2, stderrWrite);
                if (!UpdateProcThreadAttribute(
                    attributeList, 0, new IntPtr(PROC_THREAD_ATTRIBUTE_HANDLE_LIST),
                    handleList, new UIntPtr((uint)(IntPtr.Size * 3)), IntPtr.Zero, IntPtr.Zero)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                jobList = Marshal.AllocHGlobal(IntPtr.Size);
                Marshal.WriteIntPtr(jobList, jobHandle);
                if (!UpdateProcThreadAttribute(
                    attributeList, 0, new IntPtr(PROC_THREAD_ATTRIBUTE_JOB_LIST),
                    jobList, new UIntPtr((uint)IntPtr.Size), IntPtr.Zero, IntPtr.Zero)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                var startup = new STARTUPINFOEX();
                startup.StartupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFOEX));
                startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
                startup.StartupInfo.hStdInput = stdin;
                startup.StartupInfo.hStdOutput = stdoutWrite;
                startup.StartupInfo.hStdError = stderrWrite;
                startup.lpAttributeList = attributeList;
                environment = BuildEnvironment(information);
                string command = "\"" + information.FileName + "\"";
                if (!string.IsNullOrWhiteSpace(information.Arguments)) {
                    command += " " + information.Arguments;
                }
                string currentDirectory = string.IsNullOrWhiteSpace(information.WorkingDirectory)
                    ? null : information.WorkingDirectory;
                if (!CreateProcess(
                    information.FileName, new StringBuilder(command), IntPtr.Zero, IntPtr.Zero,
                    true, CREATE_SUSPENDED | CREATE_UNICODE_ENVIRONMENT | CREATE_NO_WINDOW |
                    EXTENDED_STARTUPINFO_PRESENT, environment, currentDirectory, ref startup, out created)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                if (!CloseHandle(stdoutWrite)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                stdoutWrite = IntPtr.Zero;
                if (!CloseHandle(stderrWrite)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                stderrWrite = IntPtr.Zero;
                process = Process.GetProcessById((int)created.dwProcessId);
                IntPtr retainedProcessHandle = process.Handle;
                if (retainedProcessHandle == IntPtr.Zero) {
                    throw new InvalidOperationException("Process handle was not retained.");
                }
                stdoutSafe = new SafeFileHandle(stdoutRead, true);
                stdoutRead = IntPtr.Zero;
                stdout = new FileStream(stdoutSafe, FileAccess.Read, 4096, false);
                stdoutSafe = null;
                stderrSafe = new SafeFileHandle(stderrRead, true);
                stderrRead = IntPtr.Zero;
                stderr = new FileStream(stderrSafe, FileAccess.Read, 4096, false);
                stderrSafe = null;
                if (ResumeThread(created.hThread) == UInt32.MaxValue) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                resumed = true;
                return new ContainedProcess(process, stdout, stderr);
            }
            catch (Exception primaryFailure) {
                Exception containmentFailure = null;
                if (created.hProcess != IntPtr.Zero && !resumed) {
                    bool terminated = TerminateProcess(created.hProcess, 1);
                    uint waitResult = WaitForSingleObject(created.hProcess, 5000);
                    if (!terminated || waitResult != WAIT_OBJECT_0) {
                        containmentFailure = new InvalidOperationException(
                            "Suspended process termination could not be verified.");
                    }
                }
                try { if (stdout != null) { stdout.Dispose(); } } catch { }
                try { if (stderr != null) { stderr.Dispose(); } } catch { }
                try { if (stdoutSafe != null) { stdoutSafe.Dispose(); } } catch { }
                try { if (stderrSafe != null) { stderrSafe.Dispose(); } } catch { }
                try { if (process != null) { process.Dispose(); } } catch { }
                if (containmentFailure != null) {
                    throw new AggregateException(primaryFailure, containmentFailure);
                }
                throw;
            }
            finally {
                if (created.hThread != IntPtr.Zero) { CloseHandle(created.hThread); }
                if (created.hProcess != IntPtr.Zero) { CloseHandle(created.hProcess); }
                if (stdoutRead != IntPtr.Zero) { CloseHandle(stdoutRead); }
                if (stdoutWrite != IntPtr.Zero) { CloseHandle(stdoutWrite); }
                if (stderrRead != IntPtr.Zero) { CloseHandle(stderrRead); }
                if (stderrWrite != IntPtr.Zero) { CloseHandle(stderrWrite); }
                if (stdin != IntPtr.Zero && stdin != new IntPtr(-1)) { CloseHandle(stdin); }
                if (attributeList != IntPtr.Zero) {
                    if (attributeListInitialized) { DeleteProcThreadAttributeList(attributeList); }
                    Marshal.FreeHGlobal(attributeList);
                }
                if (handleList != IntPtr.Zero) { Marshal.FreeHGlobal(handleList); }
                if (jobList != IntPtr.Zero) { Marshal.FreeHGlobal(jobList); }
                if (environment != IntPtr.Zero) { Marshal.FreeHGlobal(environment); }
            }
        }

        public void Dispose() {
            Exception failure = null;
            Stream output = StandardOutput; StandardOutput = null;
            Stream error = StandardError; StandardError = null;
            System.Diagnostics.Process process = Process; Process = null;
            try { if (output != null) { output.Dispose(); } }
            catch (Exception exception) { failure = exception; }
            try { if (error != null) { error.Dispose(); } }
            catch (Exception exception) { if (failure == null) { failure = exception; } }
            try { if (process != null) { process.Dispose(); } }
            catch (Exception exception) { if (failure == null) { failure = exception; } }
            if (failure != null) { throw failure; }
        }
    }
}
'@
}
}

function Initialize-StageARuntime {
    Initialize-StageACappedDrain
    $script:StageAOwnedProcesses = New-Object System.Collections.ArrayList
    $script:StageAProcessJob = [HardwareInspection.StageA.ProcessJob]::CreateKillOnClose()
}

function Assert-StageACondition {
    param([bool] $Condition)
    if (-not $Condition) { throw $script:StageAFailure }
}

function Test-StageANormalExistingPath {
    param([string] $Path, [bool] $Directory)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Assert-StageACondition (-not ($fullPath.StartsWith('\\', [System.StringComparison]::Ordinal) -or $fullPath.StartsWith('\\?\', [System.StringComparison]::Ordinal)))
    $item = Get-Item -LiteralPath $fullPath -Force
    Assert-StageACondition (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0)
    $component = if ($item.PSIsContainer) { $item } else { $item.Directory }
    while ($null -ne $component) {
        Assert-StageACondition (($component.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0)
        $component = $component.Parent
    }
    Assert-StageACondition ($item.PSIsContainer -eq $Directory)
    $drive = New-Object System.IO.DriveInfo($fullPath.Substring(0, 3))
    Assert-StageACondition ($drive.DriveType -eq [System.IO.DriveType]::Fixed)
    return $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
}

function Test-StageADisjointPaths {
    param([string] $First, [string] $Second)
    $separator = [System.IO.Path]::DirectorySeparatorChar
    Assert-StageACondition ($First -ine $Second)
    Assert-StageACondition (-not $First.StartsWith($Second + $separator, [System.StringComparison]::OrdinalIgnoreCase))
    Assert-StageACondition (-not $Second.StartsWith($First + $separator, [System.StringComparison]::OrdinalIgnoreCase))
}

function Get-StageAOutputPath {
    param([string] $Path, [string] $Evaluated)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Assert-StageACondition (-not ($fullPath.StartsWith('\\', [System.StringComparison]::Ordinal) -or $fullPath.StartsWith('\\?\', [System.StringComparison]::Ordinal)))
    $parent = [System.IO.Path]::GetDirectoryName($fullPath)
    Assert-StageACondition (-not [string]::IsNullOrWhiteSpace($parent))
    $parent = Test-StageANormalExistingPath $parent $true
    Assert-StageACondition (-not (Test-Path -LiteralPath $fullPath))
    Test-StageADisjointPaths $fullPath $Evaluated
    return $fullPath
}

function ConvertTo-StageACommandLine {
    param([string[]] $ArgumentList)
    return (($ArgumentList | ForEach-Object {
        '"' + $_.Replace('\\', '\\').Replace('"', '\"') + '"'
    }) -join ' ')
}

function Wait-StageAProcess {
    param([System.Diagnostics.Process] $Process, [int] $TimeoutSeconds)
    $deadline = [System.Diagnostics.Stopwatch]::StartNew()
    while (-not $Process.HasExited) {
        Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)
        Assert-StageACondition ($deadline.Elapsed.TotalSeconds -lt $TimeoutSeconds)
        Start-Sleep -Milliseconds 100
    }
}

function Copy-StageAProcessStream {
    param([System.IO.Stream] $Source, [string] $Path)
    return [HardwareInspection.StageA.CappedDrain]::CopyAsync($Source, $Path, $script:StageAMaximumProcessStreamBytes)
}

function Stop-StageAOwnedProcesses {
    $cleanupFailed = $false
    if ($null -ne $script:StageAProcessJob) {
        try {
            $script:StageAProcessJob.Dispose()
        }
        catch { $cleanupFailed = $true }
        finally { $script:StageAProcessJob = $null }
    }
    if ($null -ne $script:StageAOwnedProcesses) {
        foreach ($owned in @($script:StageAOwnedProcesses)) {
            try {
                $process = $owned.Process
                if ($null -ne $process -and -not $process.HasExited) {
                    $process.Kill()
                    Assert-StageACondition ($process.WaitForExit(5000))
                }
            }
            catch { $cleanupFailed = $true }
            finally {
                try {
                    if ($null -ne $owned.Contained) { $owned.Contained.Dispose() }
                    elseif ($null -ne $owned.Process) { $owned.Process.Dispose() }
                }
                catch { $cleanupFailed = $true }
            }
        }
        try { $script:StageAOwnedProcesses.Clear() }
        catch { $cleanupFailed = $true }
        $script:StageAOwnedProcesses = $null
    }
    Assert-StageACondition (-not $cleanupFailed)
}

function Invoke-StageAProcess {
    param([string] $Application, [string[]] $ArgumentList, [string] $LogPath, [int] $TimeoutSeconds, [bool] $RegisterOwned = $true, [bool] $ReturnOutput = $false)
    Assert-StageACondition $RegisterOwned
    $information = New-Object System.Diagnostics.ProcessStartInfo
    $information.FileName = $Application
    $information.Arguments = ConvertTo-StageACommandLine $ArgumentList
    $information.UseShellExecute = $false
    $information.CreateNoWindow = $true
    $information.RedirectStandardOutput = $true
    $information.RedirectStandardError = $true
    foreach ($environmentKey in @($information.EnvironmentVariables.Keys)) {
        $environmentName = [string]$environmentKey
        if ($environmentName.StartsWith('GITHUB_', [System.StringComparison]::OrdinalIgnoreCase) -or
            $environmentName.StartsWith('ACTIONS_', [System.StringComparison]::OrdinalIgnoreCase) -or
            $environmentName.StartsWith('RUNNER_', [System.StringComparison]::OrdinalIgnoreCase) -or
            $environmentName.StartsWith('STAGEA_', [System.StringComparison]::OrdinalIgnoreCase)) {
            $information.EnvironmentVariables.Remove($environmentName)
        }
    }
    Assert-StageACondition ($null -ne $script:StageAOwnedProcesses -and $null -ne $script:StageAProcessJob)
    $contained = [HardwareInspection.StageA.ContainedProcess]::StartSuspendedAssigned(
        $information,
        $script:StageAProcessJob
    )
    $process = $contained.Process
    try {
        [void]$script:StageAOwnedProcesses.Add([pscustomobject]@{
            Process = $process
            Contained = $contained
            LogPath = $LogPath
        })
    }
    catch {
        try { $contained.Dispose() } catch { }
        throw
    }
    $stdoutPath = $LogPath + '.stdout'
    $stderrPath = $LogPath + '.stderr'
    $outputTask = Copy-StageAProcessStream $contained.StandardOutput $stdoutPath
    $errorTask = Copy-StageAProcessStream $contained.StandardError $stderrPath
    $deadline = [System.Diagnostics.Stopwatch]::StartNew()
    while ((-not $outputTask.IsCompleted) -or (-not $errorTask.IsCompleted) -or (-not $process.HasExited)) {
            Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)
            Assert-StageACondition ($deadline.Elapsed.TotalSeconds -lt $TimeoutSeconds)
            Start-Sleep -Milliseconds 20
    }
    $truncated = $outputTask.GetAwaiter().GetResult() -or $errorTask.GetAwaiter().GetResult()
    Assert-StageACondition (-not $truncated)
    Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)
    Assert-StageACondition ($process.ExitCode -eq 0)
    if (-not $ReturnOutput) { return }
    Assert-StageACondition ((Get-Item -LiteralPath $stdoutPath).Length -le 4096)
    return (Get-Content -LiteralPath $stdoutPath -Raw).Trim()
}

function Read-HardwareInspectionIntelRunnerStageATrx {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][ValidateSet('Deterministic','Task8Deterministic')][string] $Kind)
    $file = Get-Item -LiteralPath $Path -Force
    Assert-StageACondition (-not $file.PSIsContainer -and (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0) -and $file.Length -gt 0 -and $file.Length -le $script:StageAMaximumTrxBytes)
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    Assert-StageACondition ($hash.Length -eq 32)
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = $script:StageAMaximumTrxBytes
    $memory = New-Object System.IO.MemoryStream(,$bytes)
    $reader = [System.Xml.XmlReader]::Create($memory, $settings)
    $document = New-Object System.Xml.XmlDocument
    $document.XmlResolver = $null
    try { $document.Load($reader) }
    finally { $reader.Dispose(); $memory.Dispose() }
    Assert-StageACondition ($document.DocumentElement.LocalName -ceq 'TestRun' -and $document.DocumentElement.NamespaceURI -ceq $script:StageATrxNamespace)
    $manager = New-Object System.Xml.XmlNamespaceManager($document.NameTable)
    $manager.AddNamespace('t', $script:StageATrxNamespace)
    $resultNodes = @($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $manager))
    $expectedCount = if ($Kind -ceq 'Deterministic') { 174 } else { 3 }
    Assert-StageACondition ($resultNodes.Count -eq $expectedCount)
    $resultNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    $resultIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $executionIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $results = @()
    foreach ($node in $resultNodes) {
        $name = [string]$node.GetAttribute('testName')
        $testId = [string]$node.GetAttribute('testId')
        $executionId = [string]$node.GetAttribute('executionId')
        Assert-StageACondition ($node.GetAttribute('outcome') -ceq 'Passed')
        $null = [guid]::Parse($testId); $null = [guid]::Parse($executionId)
        Assert-StageACondition (-not [string]::IsNullOrWhiteSpace($name) -and $resultNames.Add($name) -and $resultIds.Add($testId) -and $executionIds.Add($executionId))
        $results += [pscustomobject]@{ Name = $name; TestId = $testId; ExecutionId = $executionId }
    }
    if ($Kind -ceq 'Task8Deterministic') {
        $task8Names = @(
            'ArtifactStringShape_RejectsPathsAndFreeTextWithGenericDiagnostics',
            'CaptureInterval_ThirtySecondsPlusOneTickIsOutsideBoundary',
            'StableFileIdentityAndProcessTreeCleanup_AreFailClosed'
        )
        foreach ($task8Name in $task8Names) { Assert-StageACondition ($resultNames.Contains($task8Name)) }
    }
    $definitionNodes = @($document.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $manager))
    Assert-StageACondition ($definitionNodes.Count -eq $expectedCount)
    $definitions = @{}
    $expectedAssembly = if ($Kind -ceq 'Deterministic') { 'HardwareInspection.LlmFitSpike.Tests.dll' } else { 'HardwareInspection.LlmFitSpike.IntegrationTests.dll' }
    foreach ($node in $definitionNodes) {
        $testId = [string]$node.GetAttribute('id')
        $execution = $node.SelectSingleNode('t:Execution', $manager)
        $method = $node.SelectSingleNode('t:TestMethod', $manager)
        $name = [string]$node.GetAttribute('name')
        Assert-StageACondition ($null -ne $execution -and $null -ne $method -and -not $definitions.ContainsKey($testId))
        $null = [guid]::Parse($testId); $null = [guid]::Parse([string]$execution.GetAttribute('id'))
        Assert-StageACondition ($name -ceq [string]$method.GetAttribute('name'))
        Assert-StageACondition ([System.IO.Path]::GetFileName([string]$node.GetAttribute('storage')) -ieq $expectedAssembly)
        Assert-StageACondition ([System.IO.Path]::GetFileName([string]$method.GetAttribute('codeBase')) -ieq $expectedAssembly)
        if ($Kind -ceq 'Deterministic') {
            Assert-StageACondition ([string]$method.GetAttribute('className') -clike 'HardwareInspection.LlmFitSpike.Tests.*')
        } else {
            $expectedTask8Class = 'HardwareInspection.LlmFitSpike.IntegrationTests.LlmFit' + [char]67 + 'andidateIntegrationTests'
            Assert-StageACondition ([string]$method.GetAttribute('className') -ceq $expectedTask8Class)
        }
        $definitions[$testId] = [pscustomobject]@{ Name = $name; ExecutionId = [string]$execution.GetAttribute('id') }
    }
    $entryNodes = @($document.SelectNodes('/t:TestRun/t:TestEntries/t:TestEntry', $manager))
    Assert-StageACondition ($entryNodes.Count -eq $expectedCount)
    $entries = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entryNodes) {
        $entryTestId = [string]$entry.GetAttribute('testId')
        $entryExecutionId = [string]$entry.GetAttribute('executionId')
        Assert-StageACondition ($entries.Add($entryTestId + '|' + $entryExecutionId))
    }
    foreach ($result in $results) {
        Assert-StageACondition ($definitions.ContainsKey($result.TestId))
        $definition = $definitions[$result.TestId]
        Assert-StageACondition ($definition.Name -ceq $result.Name -and $definition.ExecutionId -ceq $result.ExecutionId -and $entries.Contains($result.TestId + '|' + $result.ExecutionId))
    }
    $summaryNodes = @($document.SelectNodes('/t:TestRun/t:ResultSummary', $manager))
    Assert-StageACondition ($summaryNodes.Count -eq 1 -and $summaryNodes[0].GetAttribute('outcome') -ceq 'Completed')
    $counterNodes = @($document.SelectNodes('/t:TestRun/t:ResultSummary/t:Counters', $manager))
    Assert-StageACondition ($counterNodes.Count -eq 1)
    $counters = $counterNodes[0]
    foreach ($name in @('total','executed','passed','failed','error','timeout','aborted','inconclusive','passedButRunAborted','notExecuted','notRunnable','disconnected','warning','completed','inProgress','pending')) {
        $attribute = $counters.Attributes[$name]
        Assert-StageACondition ($null -ne $attribute -and $attribute.Value -cmatch '\A[0-9]+\z')
    }
    Assert-StageACondition ([int]$counters.GetAttribute('total') -eq $expectedCount -and [int]$counters.GetAttribute('executed') -eq $expectedCount -and [int]$counters.GetAttribute('passed') -eq $expectedCount)
    foreach ($name in @('failed','error','timeout','aborted','inconclusive','passedButRunAborted','notExecuted','notRunnable','disconnected','warning','completed','inProgress','pending')) { Assert-StageACondition ([int]$counters.GetAttribute($name) -eq 0) }
    return [pscustomobject]@{ Passed = $expectedCount; NonPassing = 0 }
}

function Write-StageAAtomicUtf8 {
    param([string] $Path, [string] $Text)
    $parent = [System.IO.Path]::GetDirectoryName($Path)
    Assert-StageACondition (-not [string]::IsNullOrWhiteSpace($parent))
    $parent = Test-StageANormalExistingPath $parent $true
    Assert-StageACondition (-not (Test-Path -LiteralPath $Path))
    $temporary = Join-Path $parent ('.stagea-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $stream = New-Object System.IO.FileStream($temporary, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $writer = New-Object System.IO.StreamWriter($stream, (New-Object System.Text.UTF8Encoding($false)))
            try { $writer.Write($Text); $writer.Flush(); $stream.Flush($true) }
            finally { $writer.Dispose() }
        } finally { $stream.Dispose() }
        $parent = Test-StageANormalExistingPath $parent $true
        Assert-StageACondition (-not (Test-Path -LiteralPath $Path))
        [System.IO.File]::Move($temporary, $Path)
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}

function Assert-StageASummaryPrivacy {
    param([string] $Json, [string] $Markdown, [string] $Sha)
    $expectedJson = '{"schemaVersion":"1.0","evaluatedSha":"' + $Sha + '","deterministicPassed":174,"task8DeterministicPassed":3,"nonPassing":0}'
    $expectedMarkdown = "# Hardware Inspection Intel Stage A`n`n- Evaluated SHA: $Sha`n- Deterministic passed: 174`n- Task8 deterministic passed: 3`n- Non-passing: 0`n"
    Assert-StageACondition ($Json -ceq $expectedJson)
    Assert-StageACondition ($Markdown -ceq $expectedMarkdown)
    foreach ($text in @($Json, $Markdown)) {
        Assert-StageACondition ($text -notmatch '(?i)(?:[a-z]:\\|\\\\|/home/|/users/|stdout|stderr|\.trx|<\?xml|<testrun|\b(?:candidate|llmfit|json|cpu|gpu|hostname|computername|username|ip|mac)\b)')
    }
}

function Test-StageAResidualProcesses {
    $residual = @(Get-Process | Where-Object { $_.ProcessName -match '(?i)(llmfit|fake.*tool)' })
    Assert-StageACondition ($residual.Count -eq 0)
    $listeners = @(Get-NetTCPConnection -ErrorAction Stop | Where-Object { $_.State -eq 'Listen' -and $_.LocalPort -eq 8787 })
    Assert-StageACondition ($listeners.Count -eq 0)
}

function Invoke-HardwareInspectionIntelRunnerStageAInternal {
    foreach ($name in [System.Environment]::GetEnvironmentVariables().Keys) {
        $environmentName = [string]$name
        Assert-StageACondition (-not ($environmentName.StartsWith('GIT_', [System.StringComparison]::OrdinalIgnoreCase) -or $environmentName.StartsWith('GRANITE_LLMFIT_', [System.StringComparison]::OrdinalIgnoreCase)))
    }
    $evaluated = Test-StageANormalExistingPath $EvaluatedRoot $true
    $workRoot = Test-StageANormalExistingPath $LocalWorkRoot $true
    Test-StageADisjointPaths $workRoot $evaluated
    $summaryJson = Get-StageAOutputPath $SummaryJsonPath $evaluated
    $summaryMarkdown = Get-StageAOutputPath $SummaryMarkdownPath $evaluated
    Assert-StageACondition ($summaryJson -ine $summaryMarkdown)
    Assert-StageACondition ($ApprovedSha -cmatch '\A[0-9a-f]{40}\z' -and $ApprovedSha -cne ('0' * 40))
    $gitDirectory = Join-Path $evaluated '.git'
    $null = Test-StageANormalExistingPath $gitDirectory $true
    $git = @(Get-Command git.exe -CommandType Application | Select-Object -First 1)
    Assert-StageACondition ($git.Count -eq 1)
    $blockedDirectory = Join-Path $evaluated 'third-party\bin\llmfit\v1.1.9\win-x64'
    Assert-StageACondition (-not (Test-Path -LiteralPath $blockedDirectory))
    Test-StageAResidualProcesses
    $runDirectory = Join-Path $workRoot ('stagea-' + [guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($runDirectory) | Out-Null
    $runDirectory = Test-StageANormalExistingPath $runDirectory $true
    $gitTopLog = Join-Path $runDirectory 'git-top'
    $gitDirLog = Join-Path $runDirectory 'git-dir'
    $gitHeadLog = Join-Path $runDirectory 'git-head'
    $gitStatusLog = Join-Path $runDirectory 'git-status'
    $gitDiffLog = Join-Path $runDirectory 'git-diff'
    $gitDiffCachedLog = Join-Path $runDirectory 'git-diff-cached'
    $topLevel = Test-StageANormalExistingPath (Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'rev-parse','--show-toplevel') $gitTopLog 30 $true $true) $true
    $absoluteGitDirectory = Test-StageANormalExistingPath (Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'rev-parse','--absolute-git-dir') $gitDirLog 30 $true $true) $true
    $head = Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'rev-parse','HEAD') $gitHeadLog 30 $true $true
    Assert-StageACondition ($topLevel -ieq $evaluated -and $absoluteGitDirectory -ieq $gitDirectory -and $head -ceq $ApprovedSha)
    $null = Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'status','--porcelain=v1','--untracked-files=all') $gitStatusLog 30
    Assert-StageACondition ([string]::IsNullOrEmpty((Get-Content -LiteralPath ($gitStatusLog + '.stdout') -Raw)))
    $null = Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'diff','--quiet') $gitDiffLog 30
    $null = Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'diff','--cached','--quiet') $gitDiffCachedLog 30
    $dotnet = @(Get-Command dotnet.exe -CommandType Application | Select-Object -First 1)
    Assert-StageACondition ($dotnet.Count -eq 1)
    $projects = @(
        'tools\HardwareInspection.LlmFitSpike.Tests\HardwareInspection.LlmFitSpike.Tests.csproj',
        'tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj'
    )
    foreach ($project in $projects) {
        $projectPath = Join-Path $evaluated $project
        Assert-StageACondition (Test-Path -LiteralPath $projectPath -PathType Leaf)
        $name = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
        $null = Invoke-StageAProcess $dotnet[0].Source @('restore',$projectPath,'--runtime','win-x64','-p:Configuration=Release') (Join-Path $runDirectory ($name + '.restore.log')) 300
        $null = Invoke-StageAProcess $dotnet[0].Source @('build',$projectPath,'--configuration','Release','--runtime','win-x64','--no-restore') (Join-Path $runDirectory ($name + '.build.log')) 300
    }
    $resultsDirectory = Join-Path $runDirectory 'results'
    [System.IO.Directory]::CreateDirectory($resultsDirectory) | Out-Null
    $deterministicProject = Join-Path $evaluated $projects[0]
    $task8Project = Join-Path $evaluated $projects[1]
    $null = Invoke-StageAProcess $dotnet[0].Source @('test',$deterministicProject,'--configuration','Release','--runtime','win-x64','--no-restore','--no-build','--filter','TestCategory=Deterministic','--minimum-expected-tests','174','--results-directory',$resultsDirectory,'--report-trx','--report-trx-filename','deterministic.trx','--no-ansi') (Join-Path $runDirectory 'deterministic.log') 300
    $null = Invoke-StageAProcess $dotnet[0].Source @('test',$task8Project,'--configuration','Release','--runtime','win-x64','--no-restore','--no-build','--filter','TestCategory=Task8Deterministic','--minimum-expected-tests','3','--results-directory',$resultsDirectory,'--report-trx','--report-trx-filename','task8.trx','--no-ansi') (Join-Path $runDirectory 'task8.log') 300
    Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)
    $deterministic = Read-HardwareInspectionIntelRunnerStageATrx (Join-Path $resultsDirectory 'deterministic.trx') 'Deterministic'
    $task8 = Read-HardwareInspectionIntelRunnerStageATrx (Join-Path $resultsDirectory 'task8.trx') 'Task8Deterministic'
    Test-StageAResidualProcesses
    Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)
    $json = '{"schemaVersion":"1.0","evaluatedSha":"' + $ApprovedSha + '","deterministicPassed":174,"task8DeterministicPassed":3,"nonPassing":0}'
    $markdown = "# Hardware Inspection Intel Stage A`n`n- Evaluated SHA: $ApprovedSha`n- Deterministic passed: 174`n- Task8 deterministic passed: 3`n- Non-passing: 0`n"
    Assert-StageASummaryPrivacy $json $markdown $ApprovedSha
    Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)
    $script:StageAPendingEvaluatedRoot = $evaluated
    $script:StageAPendingSummaryJsonPath = $summaryJson
    $script:StageAPendingSummaryMarkdownPath = $summaryMarkdown
    $script:StageAPendingSummaryJson = $json
    $script:StageAPendingSummaryMarkdown = $markdown
}

if ($MyInvocation.InvocationName -ne '.') {
    $primaryFailure = $null
    $unregisterFailure = $null
    $cleanupFailure = $null
    $cancellationInstalled = $false
    $completionWon = $false
    $stageAFailed = $false
    try {
        try {
            Initialize-StageARuntime
            [HardwareInspection.StageA.CancellationState]::Install()
            $cancellationInstalled = $true
            Invoke-HardwareInspectionIntelRunnerStageAInternal
        }
        catch { $primaryFailure = $_ }
        finally {
            try { Stop-StageAOwnedProcesses; Test-StageAResidualProcesses }
            catch { $cleanupFailure = $_ }
            if ($null -eq $primaryFailure -and
                $null -eq $cleanupFailure -and
                -not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested) {
                try {
                    Assert-StageACondition (-not [string]::IsNullOrWhiteSpace($script:StageAPendingEvaluatedRoot))
                    $publishJsonPath = Get-StageAOutputPath $script:StageAPendingSummaryJsonPath $script:StageAPendingEvaluatedRoot
                    $publishMarkdownPath = Get-StageAOutputPath $script:StageAPendingSummaryMarkdownPath $script:StageAPendingEvaluatedRoot
                    Assert-StageACondition ($publishJsonPath -ine $publishMarkdownPath)
                    Assert-StageASummaryPrivacy $script:StageAPendingSummaryJson $script:StageAPendingSummaryMarkdown $ApprovedSha
                    Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)
                    Write-StageAAtomicUtf8 $publishJsonPath $script:StageAPendingSummaryJson
                    Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)
                    Write-StageAAtomicUtf8 $publishMarkdownPath $script:StageAPendingSummaryMarkdown
                    Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)
                    $completionWon = [HardwareInspection.StageA.CancellationState]::TryComplete()
                }
                catch { $primaryFailure = $_ }
            }
            if ($cancellationInstalled) {
                try { [HardwareInspection.StageA.CancellationState]::Remove() }
                catch { $unregisterFailure = $_ }
                finally { $cancellationInstalled = $false }
            }
            $script:StageAPendingEvaluatedRoot = $null
            $script:StageAPendingSummaryJsonPath = $null
            $script:StageAPendingSummaryMarkdownPath = $null
            $script:StageAPendingSummaryJson = $null
            $script:StageAPendingSummaryMarkdown = $null
        }
        if ($null -ne $primaryFailure -or
            $null -ne $unregisterFailure -or
            $null -ne $cleanupFailure -or
            -not $completionWon) {
            $stageAFailed = $true
        }
    }
    catch { $stageAFailed = $true }
    if ($stageAFailed) {
        [Console]::Error.WriteLine($script:StageAFailure)
        exit 1
    }
}
