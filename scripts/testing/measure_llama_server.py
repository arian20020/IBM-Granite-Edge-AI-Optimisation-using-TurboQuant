"""Measure loaded-runtime TTFT through llama-server's streaming endpoint."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from pathlib import Path

from measure_llama_run import available_ram_bytes, process_tree_memory_bytes
from parse_llama_measurement import summarize_measurement


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--sample-id", required=True)
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--prompt", required=True)
    parser.add_argument("--environment-json")
    parser.add_argument("--timeout-seconds", type=float, default=600)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.command and args.command[0] == "--":
        args.command = args.command[1:]
    if not args.command:
        parser.error("server command required after --")
    return args


def main() -> int:
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    if args.environment_json:
        environment.update(json.loads(Path(args.environment_json).read_text(encoding="utf-8-sig")))

    events: list[dict] = []
    event_lock = threading.Lock()
    stderr_data = bytearray()
    stdout_data = bytearray()
    run_start = time.perf_counter_ns()
    stop_sampling = threading.Event()

    def elapsed_ms(start_ns=run_start) -> float:
        return (time.perf_counter_ns() - start_ns) / 1_000_000

    def add(event: dict) -> None:
        with event_lock:
            events.append(event)

    process = subprocess.Popen(
        args.command,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=environment,
        creationflags=subprocess.CREATE_NEW_PROCESS_GROUP,
    )
    add({"kind": "start", "elapsed_ms": 0.0, "pid": process.pid})

    def read_stream(stream, target: bytearray, kind: str) -> None:
        while True:
            line = stream.readline()
            if not line:
                return
            target.extend(line)
            if kind == "stderr":
                add({"kind": "stderr", "elapsed_ms": elapsed_ms(),
                     "text": line.decode("utf-8", errors="replace").rstrip("\r\n")})

    def sample_memory() -> None:
        while not stop_sampling.is_set() and process.poll() is None:
            values = process_tree_memory_bytes(process.pid)
            available = available_ram_bytes()
            if values is not None:
                add({"kind": "memory", "elapsed_ms": elapsed_ms(),
                     "working_set_bytes": values[0], "private_bytes": values[1],
                     "available_ram_bytes": available})
            stop_sampling.wait(0.1)

    threads = [
        threading.Thread(target=read_stream, args=(process.stdout, stdout_data, "stdout"), daemon=True),
        threading.Thread(target=read_stream, args=(process.stderr, stderr_data, "stderr"), daemon=True),
        threading.Thread(target=sample_memory, daemon=True),
    ]
    for thread in threads:
        thread.start()

    health_url = f"http://127.0.0.1:{args.port}/health"
    deadline = time.monotonic() + args.timeout_seconds
    ready = False
    while time.monotonic() < deadline and process.poll() is None:
        try:
            with urllib.request.urlopen(health_url, timeout=1) as response:
                if response.status == 200:
                    ready = True
                    break
        except (OSError, urllib.error.URLError):
            time.sleep(0.2)

    request_error = None
    if ready:
        body = json.dumps({"prompt": args.prompt, "n_predict": 16, "temperature": 0,
                           "top_p": 1, "seed": 42, "stream": True,
                           "return_tokens": True}).encode("utf-8")
        request = urllib.request.Request(
            f"http://127.0.0.1:{args.port}/completion", data=body,
            headers={"Content-Type": "application/json"}, method="POST"
        )
        request_start = time.perf_counter_ns()
        try:
            with urllib.request.urlopen(request, timeout=args.timeout_seconds) as response:
                while True:
                    line = response.readline()
                    if not line:
                        break
                    if line.startswith(b"data:"):
                        payload = json.loads(line[5:].strip())
                        if payload.get("content") or payload.get("tokens"):
                            add({"kind": "first_response_byte",
                                 "elapsed_ms": (time.perf_counter_ns() - request_start) / 1_000_000,
                                 "signal": "first-generated-token"})
                            break
        except Exception as exc:  # preserved in evidence and invalidates the sample
            request_error = repr(exc)
    else:
        request_error = "server did not become ready"

    stop_sampling.set()
    subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        exit_code = process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        process.kill(); exit_code = process.wait()
    for thread in threads[:2]:
        thread.join(timeout=5)
    add({"kind": "exit", "elapsed_ms": elapsed_ms(),
         "exit_code": 0 if ready and request_error is None else exit_code})
    events.sort(key=lambda event: event["elapsed_ms"])
    summary = summarize_measurement(events)
    summary.update({"sample_id": args.sample_id, "request_error": request_error,
                    "ttft_definition": "HTTP request initiation to first streamed generated token"})

    (output_dir / "stdout.txt").write_bytes(stdout_data)
    (output_dir / "stderr.txt").write_bytes(stderr_data)
    (output_dir / "events.jsonl").write_text(
        "".join(json.dumps(event, sort_keys=True) + "\n" for event in events), encoding="utf-8")
    (output_dir / "measurement.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True), encoding="utf-8")
    print(json.dumps(summary, sort_keys=True))
    return 0 if summary["valid"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
