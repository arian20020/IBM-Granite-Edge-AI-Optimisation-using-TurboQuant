"""Launch one llama command and capture raw memory, KV, and TTFT evidence."""

from __future__ import annotations

import argparse
import ctypes
import json
import os
import subprocess
import sys
import threading
import time
from ctypes import wintypes
from pathlib import Path

from parse_llama_measurement import summarize_measurement


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


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--sample-id", required=True)
    parser.add_argument("--timeout-seconds", type=float, default=3600)
    parser.add_argument("--environment-json")
    parser.add_argument("--response-after-text-file")
    parser.add_argument("--minimum-available-ram-mb", type=float, default=0)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.command and args.command[0] == "--":
        args.command = args.command[1:]
    if not args.command:
        parser.error("a command is required after --")
    return args


def main() -> int:
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    if args.environment_json:
        environment.update(
            json.loads(Path(args.environment_json).read_text(encoding="utf-8-sig"))
        )

    start_ns = time.perf_counter_ns()
    events: list[dict] = []
    event_lock = threading.Lock()
    stdout_data = bytearray()
    stderr_data = bytearray()
    first_stdout = threading.Event()
    marker = None
    if args.response_after_text_file:
        marker = (
            Path(args.response_after_text_file)
            .read_text(encoding="utf-8-sig")
            .strip()
            .encode("utf-8")
        )
        if not marker:
            raise ValueError("response marker file must not be empty")

    def elapsed_ms() -> float:
        return (time.perf_counter_ns() - start_ns) / 1_000_000

    def add_event(event: dict) -> None:
        with event_lock:
            events.append(event)

    process = subprocess.Popen(
        args.command,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=environment,
        creationflags=subprocess.CREATE_NEW_PROCESS_GROUP,
    )
    add_event({"kind": "start", "elapsed_ms": 0.0, "pid": process.pid})

    def read_stdout() -> None:
        marker_window = bytearray()
        marker_seen = marker is None
        while True:
            chunk = process.stdout.read(1)
            if not chunk:
                return
            stdout_data.extend(chunk)
            if not marker_seen:
                marker_window.extend(chunk)
                if len(marker_window) > len(marker):
                    del marker_window[0 : len(marker_window) - len(marker)]
                marker_seen = bytes(marker_window) == marker
                continue
            if not first_stdout.is_set() and not chunk.isspace():
                first_stdout.set()
                add_event({"kind": "first_response_byte", "elapsed_ms": elapsed_ms()})

    def read_stderr() -> None:
        while True:
            line = process.stderr.readline()
            if not line:
                return
            stderr_data.extend(line)
            add_event(
                {
                    "kind": "stderr",
                    "elapsed_ms": elapsed_ms(),
                    "text": line.decode("utf-8", errors="replace").rstrip("\r\n"),
                }
            )

    stdout_thread = threading.Thread(target=read_stdout, daemon=True)
    stderr_thread = threading.Thread(target=read_stderr, daemon=True)
    stdout_thread.start()
    stderr_thread.start()

    deadline = time.monotonic() + args.timeout_seconds
    timed_out = False
    emergency_stopped = False
    while process.poll() is None:
        value = process_tree_working_set_bytes(process.pid)
        if value is not None:
            add_event(
                {"kind": "memory", "elapsed_ms": elapsed_ms(), "private_bytes": value}
            )
        available = available_ram_bytes()
        if (args.minimum_available_ram_mb > 0 and available is not None and
                available < args.minimum_available_ram_mb * 1024 * 1024):
            emergency_stopped = True
            add_event({"kind": "emergency_stop", "elapsed_ms": elapsed_ms(),
                       "available_ram_bytes": available,
                       "minimum_available_ram_mb": args.minimum_available_ram_mb})
            subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            break
        if time.monotonic() >= deadline:
            timed_out = True
            process.kill()
            break
        time.sleep(0.1)

    exit_code = process.wait()
    stdout_thread.join(timeout=5)
    stderr_thread.join(timeout=5)
    add_event(
        {
            "kind": "exit",
            "elapsed_ms": elapsed_ms(),
            "exit_code": exit_code,
            "timed_out": timed_out,
        }
    )
    events.sort(key=lambda event: event["elapsed_ms"])
    summary = summarize_measurement(events)
    summary.update({"sample_id": args.sample_id, "timed_out": timed_out,
                    "emergency_stopped": emergency_stopped})

    (output_dir / "stdout.txt").write_bytes(stdout_data)
    (output_dir / "stderr.txt").write_bytes(stderr_data)
    (output_dir / "events.jsonl").write_text(
        "".join(json.dumps(event, sort_keys=True) + "\n" for event in events),
        encoding="utf-8",
    )
    (output_dir / "command.json").write_text(
        json.dumps({"command": args.command, "sample_id": args.sample_id}, indent=2),
        encoding="utf-8",
    )
    (output_dir / "measurement.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True), encoding="utf-8"
    )
    print(json.dumps(summary, sort_keys=True))
    return 0 if summary["valid"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
