"""Selection, identifiers, and bounded process execution for AtomicBot retests."""

from __future__ import annotations

import json
import subprocess
import time
from pathlib import Path

from scripts.testing.atomicbot.matrix import TestCase


def build_server_command(case: TestCase, server: Path, model: Path, port: int) -> list[str]:
    gpu_layers = {"cpu": 0, "vulkan-partial": 1, "vulkan-full": 999}[case.backend]
    command = [
        str(server), "-m", str(model), "-c", str(case.context), "-t", "8", "-tb", "8",
        "-b", "512", "-ub", "512", "-ctk", case.turbo_type, "-ctv", case.turbo_type,
        "-ngl", str(gpu_layers), "--host", "127.0.0.1", "--port", str(port), "-np", "1",
        "--cache-ram", "0", "--fit", "off", "--offline", "--no-webui",
        "--log-colors", "off", "-lv", "4",
    ]
    if case.backend.startswith("vulkan"):
        command.extend(("--device", "Vulkan0"))
    return command


def select_cases(cases: list[TestCase], *, only: set[str] | None = None,
                 start_at: str | None = None, skip: set[str] | None = None,
                 resume_state: dict | None = None) -> list[TestCase]:
    known = {case.test_id for case in cases}
    for requested in (only or set()) | (skip or set()) | ({start_at} if start_at else set()):
        if requested not in known:
            raise ValueError(f"unknown test id: {requested}")
    start_index = next((index for index, case in enumerate(cases)
                        if case.test_id == start_at), 0) if start_at else 0
    attempts = (resume_state or {}).get("attempts", {})
    selected = []
    for case in cases[start_index:]:
        if only is not None and case.test_id not in only:
            continue
        if case.test_id in (skip or set()):
            continue
        prior = attempts.get(case.test_id, {})
        if prior.get("status") == "complete" and prior.get("reconciled") is True:
            continue
        selected.append(case)
    return selected


def next_run_id(test_id: str, date: str, root: Path) -> str:
    prefix = f"{test_id}-{date}-R"
    numbers = []
    if root.exists():
        for path in root.iterdir():
            if path.name.startswith(prefix):
                suffix = path.name[len(prefix):]
                if suffix.isdigit():
                    numbers.append(int(suffix))
    return f"{prefix}{max(numbers, default=0) + 1:04d}"


def ensure_replace_allowed(attempt: dict | None, replace_attempt: bool) -> None:
    if attempt and attempt.get("status") == "complete" and not replace_attempt:
        raise ValueError("completed attempt requires --replace-attempt")


def run_timed_command(command: list[str], output_dir: Path, *, timeout_seconds: float,
                      cwd: Path | None = None, environment: dict | None = None) -> dict:
    output_dir.mkdir(parents=True, exist_ok=True)
    started = time.time()
    record = {"command": command, "cwd": str(cwd) if cwd else None,
              "started_unix": started, "timeout_seconds": timeout_seconds}
    (output_dir / "command.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
    stdout_path = output_dir / "stdout.txt"
    stderr_path = output_dir / "stderr.txt"
    cleanup_attempted = False
    with stdout_path.open("wb") as stdout, stderr_path.open("wb") as stderr:
        process = subprocess.Popen(command, cwd=cwd, env=environment, stdout=stdout,
                                   stderr=stderr, creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        try:
            exit_code = process.wait(timeout=timeout_seconds)
            status = "passed" if exit_code == 0 else "failed"
        except subprocess.TimeoutExpired:
            cleanup_attempted = True
            subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            try:
                exit_code = process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                exit_code = process.wait()
            status = "timeout"
    result = {**record, "status": status, "exit_code": exit_code,
              "cleanup_attempted": cleanup_attempted, "finished_unix": time.time()}
    (output_dir / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    return result
