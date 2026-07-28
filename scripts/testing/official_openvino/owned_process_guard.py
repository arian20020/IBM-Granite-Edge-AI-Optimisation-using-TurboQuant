"""Own Windows process trees and expose shared RAM/process sampling primitives."""

from __future__ import annotations

import ctypes
import math
import os
import subprocess
import time
from ctypes import wintypes
from typing import Any

PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
PROCESS_VM_READ = 0x0010
TH32CS_SNAPPROCESS = 0x00000002


class PROCESS_MEMORY_COUNTERS_EX(ctypes.Structure):
    _fields_ = [
        ("cb", wintypes.DWORD),
        ("PageFaultCount", wintypes.DWORD),
        ("PeakWorkingSetSize", ctypes.c_size_t),
        ("WorkingSetSize", ctypes.c_size_t),
        ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
        ("QuotaPagedPoolUsage", ctypes.c_size_t),
        ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
        ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
        ("PagefileUsage", ctypes.c_size_t),
        ("PeakPagefileUsage", ctypes.c_size_t),
        ("PrivateUsage", ctypes.c_size_t),
    ]


class PROCESSENTRY32W(ctypes.Structure):
    _fields_ = [
        ("dwSize", wintypes.DWORD), ("cntUsage", wintypes.DWORD),
        ("th32ProcessID", wintypes.DWORD), ("th32DefaultHeapID", ctypes.c_size_t),
        ("th32ModuleID", wintypes.DWORD), ("cntThreads", wintypes.DWORD),
        ("th32ParentProcessID", wintypes.DWORD), ("pcPriClassBase", ctypes.c_long),
        ("dwFlags", wintypes.DWORD), ("szExeFile", wintypes.WCHAR * 260),
    ]


def working_set_bytes(pid: int) -> int | None:
    """Return current Windows physical working set for one process."""
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    psapi = ctypes.WinDLL("psapi", use_last_error=True)
    kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL
    psapi.GetProcessMemoryInfo.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(PROCESS_MEMORY_COUNTERS_EX),
        wintypes.DWORD,
    ]
    psapi.GetProcessMemoryInfo.restype = wintypes.BOOL
    handle = kernel32.OpenProcess(
        PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, False, pid
    )
    if not handle:
        return None
    try:
        counters = PROCESS_MEMORY_COUNTERS_EX()
        counters.cb = ctypes.sizeof(counters)
        ok = psapi.GetProcessMemoryInfo(
            handle, ctypes.byref(counters), counters.cb
        )
        # Working set is the resident RAM attributed to the process. PrivateUsage
        # is commit charge and is not a peak-RAM measure on Windows.
        return int(counters.WorkingSetSize) if ok else None
    finally:
        kernel32.CloseHandle(handle)


def process_memory_bytes(pid: int) -> tuple[int, int] | None:
    """Return current physical working set and committed private bytes."""
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    psapi = ctypes.WinDLL("psapi", use_last_error=True)
    kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    psapi.GetProcessMemoryInfo.argtypes = [
        wintypes.HANDLE, ctypes.POINTER(PROCESS_MEMORY_COUNTERS_EX), wintypes.DWORD
    ]
    psapi.GetProcessMemoryInfo.restype = wintypes.BOOL
    handle = kernel32.OpenProcess(
        PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, False, pid
    )
    if not handle:
        return None
    try:
        counters = PROCESS_MEMORY_COUNTERS_EX()
        counters.cb = ctypes.sizeof(counters)
        if not psapi.GetProcessMemoryInfo(handle, ctypes.byref(counters), counters.cb):
            return None
        return int(counters.WorkingSetSize), int(counters.PrivateUsage)
    finally:
        kernel32.CloseHandle(handle)


def process_tree_pids(root_pid: int) -> set[int]:
    """Return the root PID and every currently live descendant on Windows."""
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
    kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel32.Process32FirstW.argtypes = [wintypes.HANDLE, ctypes.POINTER(PROCESSENTRY32W)]
    kernel32.Process32NextW.argtypes = [wintypes.HANDLE, ctypes.POINTER(PROCESSENTRY32W)]
    snapshot = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    if snapshot == wintypes.HANDLE(-1).value:
        return {root_pid}
    parents: dict[int, int] = {}
    try:
        entry = PROCESSENTRY32W(); entry.dwSize = ctypes.sizeof(entry)
        ok = kernel32.Process32FirstW(snapshot, ctypes.byref(entry))
        while ok:
            parents[int(entry.th32ProcessID)] = int(entry.th32ParentProcessID)
            ok = kernel32.Process32NextW(snapshot, ctypes.byref(entry))
    finally:
        kernel32.CloseHandle(snapshot)
    tree = {root_pid}
    changed = True
    while changed:
        changed = False
        for pid, parent in parents.items():
            if parent in tree and pid not in tree:
                tree.add(pid); changed = True
    return tree


def process_tree_working_set_bytes(root_pid: int) -> int | None:
    values = [working_set_bytes(pid) for pid in process_tree_pids(root_pid)]
    available = [value for value in values if value is not None]
    return sum(available) if available else None


def process_tree_memory_bytes(root_pid: int) -> tuple[int, int] | None:
    values = [process_memory_bytes(pid) for pid in process_tree_pids(root_pid)]
    available = [value for value in values if value is not None]
    if not available:
        return None
    return sum(value[0] for value in available), sum(value[1] for value in available)


class MEMORYSTATUSEX(ctypes.Structure):
    _fields_ = [
        ("dwLength", wintypes.DWORD), ("dwMemoryLoad", wintypes.DWORD),
        ("ullTotalPhys", ctypes.c_ulonglong), ("ullAvailPhys", ctypes.c_ulonglong),
        ("ullTotalPageFile", ctypes.c_ulonglong), ("ullAvailPageFile", ctypes.c_ulonglong),
        ("ullTotalVirtual", ctypes.c_ulonglong), ("ullAvailVirtual", ctypes.c_ulonglong),
        ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
    ]


def available_ram_bytes() -> int | None:
    status = MEMORYSTATUSEX()
    status.dwLength = ctypes.sizeof(status)
    return int(status.ullAvailPhys) if ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)) else None


JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000
JOB_OBJECT_BASIC_PROCESS_ID_LIST = 3
JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9
JOB_OBJECT_CPU_RATE_CONTROL_INFORMATION = 15
JOB_OBJECT_CPU_RATE_CONTROL_ENABLE = 0x1
JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP = 0x4
PROCESS_TERMINATE = 0x0001
PROCESS_SET_QUOTA = 0x0100
PROCESS_SET_INFORMATION = 0x0200
ERROR_MORE_DATA = 234
CREATE_SUSPENDED = 0x00000004
TH32CS_SNAPTHREAD = 0x00000004
THREAD_SUSPEND_RESUME = 0x0002
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value


class JOBOBJECT_BASIC_LIMIT_INFORMATION(ctypes.Structure):
    _fields_ = [
        ("PerProcessUserTimeLimit", ctypes.c_int64),
        ("PerJobUserTimeLimit", ctypes.c_int64),
        ("LimitFlags", wintypes.DWORD),
        ("MinimumWorkingSetSize", ctypes.c_size_t),
        ("MaximumWorkingSetSize", ctypes.c_size_t),
        ("ActiveProcessLimit", wintypes.DWORD),
        ("Affinity", ctypes.c_size_t),
        ("PriorityClass", wintypes.DWORD),
        ("SchedulingClass", wintypes.DWORD),
    ]


class IO_COUNTERS(ctypes.Structure):
    _fields_ = [
        ("ReadOperationCount", ctypes.c_ulonglong),
        ("WriteOperationCount", ctypes.c_ulonglong),
        ("OtherOperationCount", ctypes.c_ulonglong),
        ("ReadTransferCount", ctypes.c_ulonglong),
        ("WriteTransferCount", ctypes.c_ulonglong),
        ("OtherTransferCount", ctypes.c_ulonglong),
    ]


class JOBOBJECT_EXTENDED_LIMIT_INFORMATION(ctypes.Structure):
    _fields_ = [
        ("BasicLimitInformation", JOBOBJECT_BASIC_LIMIT_INFORMATION),
        ("IoInfo", IO_COUNTERS),
        ("ProcessMemoryLimit", ctypes.c_size_t),
        ("JobMemoryLimit", ctypes.c_size_t),
        ("PeakProcessMemoryUsed", ctypes.c_size_t),
        ("PeakJobMemoryUsed", ctypes.c_size_t),
    ]


class JOBOBJECT_CPU_RATE_CONTROL_INFORMATION(ctypes.Structure):
    _fields_ = [
        ("ControlFlags", wintypes.DWORD),
        ("CpuRate", wintypes.DWORD),
    ]


class THREADENTRY32(ctypes.Structure):
    _fields_ = [
        ("dwSize", wintypes.DWORD),
        ("cntUsage", wintypes.DWORD),
        ("th32ThreadID", wintypes.DWORD),
        ("th32OwnerProcessID", wintypes.DWORD),
        ("tpBasePri", wintypes.LONG),
        ("tpDeltaPri", wintypes.LONG),
        ("dwFlags", wintypes.DWORD),
    ]


def _kernel32():
    if os.name != "nt":
        raise RuntimeError("Windows Job Objects are required")
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
    kernel32.CreateJobObjectW.restype = wintypes.HANDLE
    kernel32.SetInformationJobObject.argtypes = [
        wintypes.HANDLE,
        ctypes.c_int,
        ctypes.c_void_p,
        wintypes.DWORD,
    ]
    kernel32.SetInformationJobObject.restype = wintypes.BOOL
    kernel32.AssignProcessToJobObject.argtypes = [wintypes.HANDLE, wintypes.HANDLE]
    kernel32.AssignProcessToJobObject.restype = wintypes.BOOL
    kernel32.QueryInformationJobObject.argtypes = [
        wintypes.HANDLE,
        ctypes.c_int,
        ctypes.c_void_p,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
    ]
    kernel32.QueryInformationJobObject.restype = wintypes.BOOL
    kernel32.TerminateJobObject.argtypes = [wintypes.HANDLE, wintypes.UINT]
    kernel32.TerminateJobObject.restype = wintypes.BOOL
    kernel32.OpenProcess.argtypes = [
        wintypes.DWORD,
        wintypes.BOOL,
        wintypes.DWORD,
    ]
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.SetProcessAffinityMask.argtypes = [
        wintypes.HANDLE,
        ctypes.c_size_t,
    ]
    kernel32.SetProcessAffinityMask.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL
    kernel32.CreateToolhelp32Snapshot.argtypes = [
        wintypes.DWORD,
        wintypes.DWORD,
    ]
    kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel32.Thread32First.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(THREADENTRY32),
    ]
    kernel32.Thread32First.restype = wintypes.BOOL
    kernel32.Thread32Next.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(THREADENTRY32),
    ]
    kernel32.Thread32Next.restype = wintypes.BOOL
    kernel32.OpenThread.argtypes = [
        wintypes.DWORD,
        wintypes.BOOL,
        wintypes.DWORD,
    ]
    kernel32.OpenThread.restype = wintypes.HANDLE
    kernel32.ResumeThread.argtypes = [wintypes.HANDLE]
    kernel32.ResumeThread.restype = wintypes.DWORD
    return kernel32


def _windows_error(action: str) -> OSError:
    code = ctypes.get_last_error()
    return OSError(code, f"{action} failed: {ctypes.FormatError(code).strip()}")


def _resume_suspended_process(pid: int) -> None:
    """Resume the sole primary thread after the process joins its Job Object."""
    kernel32 = _kernel32()
    snapshot = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)
    if snapshot == INVALID_HANDLE_VALUE:
        raise _windows_error("CreateToolhelp32Snapshot(threads)")
    thread_ids: list[int] = []
    try:
        entry = THREADENTRY32()
        entry.dwSize = ctypes.sizeof(entry)
        more = kernel32.Thread32First(snapshot, ctypes.byref(entry))
        while more:
            if int(entry.th32OwnerProcessID) == pid:
                thread_ids.append(int(entry.th32ThreadID))
            entry.dwSize = ctypes.sizeof(entry)
            more = kernel32.Thread32Next(snapshot, ctypes.byref(entry))
    finally:
        kernel32.CloseHandle(snapshot)

    if len(thread_ids) != 1:
        raise RuntimeError(
            f"suspended process {pid} exposed {len(thread_ids)} primary threads"
        )
    thread_handle = kernel32.OpenThread(
        THREAD_SUSPEND_RESUME, False, thread_ids[0]
    )
    if not thread_handle:
        raise _windows_error(f"OpenThread({thread_ids[0]})")
    try:
        previous_suspend_count = kernel32.ResumeThread(thread_handle)
        if previous_suspend_count == 0xFFFFFFFF:
            raise _windows_error(f"ResumeThread({thread_ids[0]})")
        if previous_suspend_count != 1:
            raise RuntimeError(
                f"thread {thread_ids[0]} had unexpected suspend count "
                f"{previous_suspend_count}"
            )
    finally:
        kernel32.CloseHandle(thread_handle)


def _set_process_affinity_mask(pid: int, affinity_mask: int) -> None:
    """Apply a controlled CPU affinity before a suspended workload resumes."""
    if affinity_mask <= 0:
        raise ValueError("affinity_mask must be positive")
    kernel32 = _kernel32()
    process_handle = kernel32.OpenProcess(
        PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION,
        False,
        pid,
    )
    if not process_handle:
        raise _windows_error(f"OpenProcess({pid}, affinity)")
    try:
        if not kernel32.SetProcessAffinityMask(
            process_handle, ctypes.c_size_t(affinity_mask)
        ):
            raise _windows_error(f"SetProcessAffinityMask({pid})")
    finally:
        kernel32.CloseHandle(process_handle)


class KillOnCloseJob:
    """One Windows Job Object with governed termination and PID-list queries."""

    def __init__(self, name: str):
        self._kernel32 = _kernel32()
        self._handle = self._kernel32.CreateJobObjectW(None, name)
        if not self._handle:
            raise _windows_error("CreateJobObjectW")
        limits = JOBOBJECT_EXTENDED_LIMIT_INFORMATION()
        limits.BasicLimitInformation.LimitFlags = (
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        )
        if not self._kernel32.SetInformationJobObject(
            self._handle,
            JOB_OBJECT_EXTENDED_LIMIT_INFORMATION,
            ctypes.byref(limits),
            ctypes.sizeof(limits),
        ):
            error = _windows_error("SetInformationJobObject")
            self.close()
            raise error

    def assign_pid(self, pid: int) -> None:
        process_handle = self._kernel32.OpenProcess(
            PROCESS_TERMINATE
            | PROCESS_SET_QUOTA
            | PROCESS_QUERY_LIMITED_INFORMATION,
            False,
            pid,
        )
        if not process_handle:
            raise _windows_error(f"OpenProcess({pid})")
        try:
            if not self._kernel32.AssignProcessToJobObject(
                self._handle, process_handle
            ):
                raise _windows_error(f"AssignProcessToJobObject({pid})")
        finally:
            self._kernel32.CloseHandle(process_handle)

    def set_cpu_rate_hard_cap(self, cpu_rate: int) -> None:
        if not 1 <= cpu_rate <= 10000:
            raise ValueError("CPU rate must be within [1, 10000]")
        control = JOBOBJECT_CPU_RATE_CONTROL_INFORMATION()
        control.ControlFlags = (
            JOB_OBJECT_CPU_RATE_CONTROL_ENABLE
            | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP
        )
        control.CpuRate = cpu_rate
        if not self._kernel32.SetInformationJobObject(
            self._handle,
            JOB_OBJECT_CPU_RATE_CONTROL_INFORMATION,
            ctypes.byref(control),
            ctypes.sizeof(control),
        ):
            raise _windows_error("SetInformationJobObject(CPU rate)")

    def active_pids(self) -> list[int]:
        capacity = 64
        while capacity <= 4096:
            class PROCESS_ID_LIST(ctypes.Structure):
                _fields_ = [
                    ("NumberOfAssignedProcesses", wintypes.DWORD),
                    ("NumberOfProcessIdsInList", wintypes.DWORD),
                    ("ProcessIdList", ctypes.c_size_t * capacity),
                ]

            process_ids = PROCESS_ID_LIST()
            returned = wintypes.DWORD()
            ok = self._kernel32.QueryInformationJobObject(
                self._handle,
                JOB_OBJECT_BASIC_PROCESS_ID_LIST,
                ctypes.byref(process_ids),
                ctypes.sizeof(process_ids),
                ctypes.byref(returned),
            )
            if not ok:
                error_code = ctypes.get_last_error()
                if error_code == ERROR_MORE_DATA:
                    capacity *= 2
                    continue
                raise _windows_error("QueryInformationJobObject")
            assigned = int(process_ids.NumberOfAssignedProcesses)
            listed = int(process_ids.NumberOfProcessIdsInList)
            if assigned > capacity or listed > capacity or assigned != listed:
                capacity *= 2
                continue
            return [int(process_ids.ProcessIdList[index]) for index in range(listed)]
        raise RuntimeError("Job Object PID query exceeded the 4096-process limit")

    def terminate(self, exit_code: int = 1) -> None:
        if not self._kernel32.TerminateJobObject(self._handle, exit_code):
            raise _windows_error("TerminateJobObject")

    def close(self) -> None:
        if getattr(self, "_handle", None):
            if not self._kernel32.CloseHandle(self._handle):
                raise _windows_error("CloseHandle(JobObject)")
            self._handle = None


def _append_error(errors: list[str], message: str) -> None:
    if message not in errors:
        errors.append(message)


def _taskkill(pid: int) -> dict:
    command = ["taskkill.exe", "/PID", str(pid), "/T", "/F"]
    try:
        completed = subprocess.run(
            command,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=False,
            timeout=30,
        )
        return {
            "command": command,
            "exit_code": completed.returncode,
            "stdout": completed.stdout,
            "stderr": completed.stderr,
            "error": None,
        }
    except Exception as error:
        return {
            "command": command,
            "exit_code": None,
            "stdout": "",
            "stderr": "",
            "error": f"{type(error).__name__}: {error}",
        }


def _wait_process(
    process: Any, timeout: float, label: str, errors: list[str]
) -> bool:
    """Wait without allowing a cleanup failure to escape resource closure."""
    try:
        process.wait(timeout=timeout)
        return True
    except Exception as error:
        _append_error(
            errors, f"{label} wait failed: {type(error).__name__}: {error}"
        )
        return False


def _close_run_resources(
    file_handles: list[Any], jobs: list[Any], errors: list[str]
) -> None:
    """Attempt every close independently, even after an earlier close fails."""
    for handle in file_handles:
        try:
            handle.close()
        except Exception as error:
            _append_error(
                errors,
                f"artifact handle close failed: {type(error).__name__}: {error}",
            )
    for job in jobs:
        if job is None:
            continue
        try:
            job.close()
        except Exception as error:
            _append_error(
                errors, f"Job Object close failed: {type(error).__name__}: {error}"
            )


def _cleanup_job(
    job: KillOnCloseJob | None,
    process: subprocess.Popen | None,
    label: str,
    errors: list[str],
    emergency_actions: list[dict],
    *,
    cleanup_timeout_seconds: float = 10.0,
) -> tuple[dict, bool]:
    if (
        not math.isfinite(cleanup_timeout_seconds)
        or cleanup_timeout_seconds <= 0
    ):
        raise ValueError("cleanup_timeout_seconds must be finite and positive")
    evidence = {
        "setup_ok": job is not None and process is not None,
        "query_ok": False,
        "terminate_job_called": False,
        "queried_active_process_count_after_cleanup": None,
        "survivor_pids_after_cleanup": None,
    }
    emergency = False
    if job is None:
        _append_error(errors, f"{label} Job Object setup failed")
        return evidence, emergency
    try:
        active = job.active_pids()
        evidence["query_ok"] = True
        if active:
            job.terminate(1)
            evidence["terminate_job_called"] = True
        deadline = time.monotonic() + cleanup_timeout_seconds
        survivors = active
        while survivors and time.monotonic() < deadline:
            time.sleep(0.05)
            survivors = job.active_pids()
        evidence["queried_active_process_count_after_cleanup"] = len(survivors)
        evidence["survivor_pids_after_cleanup"] = sorted(survivors)
        if survivors:
            _append_error(errors, f"{label} Job Object cleanup left survivors")
            if process is not None:
                action = _taskkill(process.pid)
                action["reason"] = f"{label} Job Object cleanup fallback"
                emergency_actions.append(action)
                emergency = True
                try:
                    survivors = job.active_pids()
                    evidence["queried_active_process_count_after_cleanup"] = len(
                        survivors
                    )
                    evidence["survivor_pids_after_cleanup"] = sorted(survivors)
                except OSError as error:
                    evidence["query_ok"] = False
                    _append_error(
                        errors,
                        f"{label} Job Object query failed after taskkill: {error}",
                    )
    except (OSError, RuntimeError) as error:
        evidence["query_ok"] = False
        _append_error(errors, f"{label} Job Object query/cleanup failed: {error}")
        if process is not None and process.poll() is None:
            action = _taskkill(process.pid)
            action["reason"] = f"{label} Job Object query failure fallback"
            emergency_actions.append(action)
            emergency = True
    return evidence, emergency



__all__ = [
    "CREATE_SUSPENDED",
    "KillOnCloseJob",
    "_append_error",
    "_cleanup_job",
    "_close_run_resources",
    "_resume_suspended_process",
    "_set_process_affinity_mask",
    "_taskkill",
    "_wait_process",
    "available_ram_bytes",
    "process_memory_bytes",
    "process_tree_memory_bytes",
    "process_tree_pids",
    "process_tree_working_set_bytes",
    "working_set_bytes",
]
