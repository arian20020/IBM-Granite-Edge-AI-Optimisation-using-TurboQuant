"""Launch one llama command and capture raw memory, KV, and TTFT evidence."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import threading
import time
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[3]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.testing.official_openvino import owned_process_guard  # noqa: E402
from scripts.testing.official_openvino.owned_process_guard import (  # noqa: E402
    available_ram_bytes,
    process_memory_bytes,
    process_tree_memory_bytes,
    process_tree_pids,
    process_tree_working_set_bytes,
    working_set_bytes,
)

from scripts.testing.campaigns.llama_cpp.parse_measurement import (  # noqa: E402
    summarize_measurement,
)


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
