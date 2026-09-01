"""Run GTQ-PROMPTS-v1 for every formal AtomicBot runtime row."""

from __future__ import annotations

import argparse
import atexit
import hashlib
import json
import os
import queue
import subprocess
import sys
import threading
import time
import urllib.request
from pathlib import Path

import msvcrt

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
if str(ROOT / "scripts" / "testing") not in sys.path:
    sys.path.insert(0, str(ROOT / "scripts" / "testing"))

from scripts.testing.campaigns.atomicbot.matrix import load_matrix
from scripts.testing.campaigns.llama_cpp.measure_run import available_ram_bytes
from scripts.testing.tools.run_atomicbot_quality_retest import render_prompt, request, wait_ready


def call_with_deadline(call, timeout_seconds: float):
    """Run a blocking HTTP call with a real wall-clock deadline.

    urllib's timeout is a per-socket-operation timeout, so a model that keeps
    streaming can otherwise run forever.  A daemon thread lets the caller tear
    down llama-server when the overall deadline expires.
    """
    result: queue.Queue[tuple[bool, object]] = queue.Queue(maxsize=1)

    def invoke() -> None:
        try:
            result.put((True, call()))
        except BaseException as exc:
            result.put((False, exc))

    threading.Thread(target=invoke, daemon=True).start()
    deadline_utc = time.time() + timeout_seconds
    while True:
        remaining = deadline_utc - time.time()
        if remaining <= 0:
            raise TimeoutError(f"wall-clock deadline of {timeout_seconds:g}s exceeded")
        try:
            ok, value = result.get(timeout=min(1.0, remaining))
            break
        except queue.Empty:
            # Recheck absolute time every second. Unlike one long Windows wait,
            # time.time() advances while the laptop is asleep.
            continue
    if ok:
        return value
    raise value


def acquire_runner_lock(output_root: Path):
    """Hold an OS lock so only one writer can control a result directory."""
    lock_path = output_root / ".runner.lock"
    handle = lock_path.open("a+b")
    if lock_path.stat().st_size == 0:
        handle.write(b"0")
        handle.flush()
    handle.seek(0)
    try:
        msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
    except OSError as exc:
        handle.close()
        raise RuntimeError(f"another quality runner owns {output_root}") from exc

    def release() -> None:
        if not handle.closed:
            handle.seek(0)
            msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            handle.close()

    return handle, release


TERMINAL_STATUSES = {"complete", "timeout", "safety-blocked"}


def completion_is_terminal(completion: dict) -> bool:
    statuses = completion.get("statuses", {})
    return len(statuses) == 6 and set(statuses.values()) <= TERMINAL_STATUSES


def args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--matrix", type=Path, required=True)
    p.add_argument("--prompt-set", type=Path, required=True)
    p.add_argument("--output-root", type=Path, required=True)
    p.add_argument("--cpu-build", type=Path, required=True)
    p.add_argument("--vulkan-build", type=Path, required=True)
    p.add_argument("--gemma-model", type=Path, required=True)
    p.add_argument("--granite3-model", type=Path, required=True)
    p.add_argument("--granite8-model", type=Path, required=True)
    p.add_argument("--only", action="append")
    p.add_argument("--minimum-available-ram-mb", type=float, default=256)
    p.add_argument("--base-port", type=int, default=19700)
    p.add_argument("--prompt-deadline-seconds", type=float, default=2400)
    p.add_argument("--quality-context", type=int, default=16384)
    p.add_argument("--exclude-prompt", action="append")
    p.add_argument("--only-prompt", action="append")
    p.add_argument("--retry-safety-blocked", action="store_true")
    return p.parse_args()


def command(case, server: Path, model: Path, port: int, context: int) -> list[str]:
    ngl = {"cpu": 0, "vulkan-partial": 1, "vulkan-full": 999}[case.backend]
    cmd = [str(server), "-m", str(model), "-c", str(context), "-t", "8", "-tb", "8",
           "-ctk", case.turbo_type, "-ctv", case.turbo_type, "-ngl", str(ngl),
           "--host", "127.0.0.1", "--port", str(port), "-np", "1", "--cache-ram", "0",
           "--fit", "off", "--offline", "--no-webui", "-fa", "on", "-lv", "4",
           "--log-colors", "off"]
    if case.backend.startswith("vulkan"):
        cmd += ["--device", "Vulkan0"]
    return cmd


def main() -> int:
    a = args()
    prompt_set = json.loads(a.prompt_set.read_text(encoding="utf-8"))
    models = {"gemma-3-1b": a.gemma_model, "granite-4.1-3b": a.granite3_model,
              "granite-4.1-8b": a.granite8_model}
    selected = set(a.only or ())
    cases = [c for c in load_matrix(a.matrix) if c.backend != "build" and
             (not selected or c.test_id in selected)]
    a.output_root.mkdir(parents=True, exist_ok=True)
    _lock_handle, release_lock = acquire_runner_lock(a.output_root)
    atexit.register(release_lock)
    for index, case in enumerate(cases):
        row = a.output_root / case.test_id
        row.mkdir(parents=True, exist_ok=True)
        completion_path = row / "complete.json"
        if completion_path.is_file():
            completion = json.loads(completion_path.read_text(encoding="utf-8"))
            if completion_is_terminal(completion):
                print(f"{case.test_id} resumed complete", flush=True); continue
        free = available_ram_bytes()
        if free is not None and free < 6144 * 1024 * 1024 and "8b" in case.model_id:
            raise RuntimeError(f"{case.test_id}: less than 6144 MiB available before 8B quality run")
        build = a.cpu_build if case.backend == "cpu" else a.vulkan_build
        server = build / "bin/llama-server.exe"
        port = a.base_port + index
        env = os.environ.copy()
        if case.backend.startswith("vulkan"):
            env["GGML_VK_VISIBLE_DEVICES"] = "0"
        out = (row / "server-stdout.log").open("ab")
        err = (row / "server-stderr.log").open("ab")
        proc = subprocess.Popen(command(case, server, models[case.model_id], port, a.quality_context),
                                stdout=out, stderr=err, env=env)
        emergency = threading.Event()
        stop = threading.Event()

        def monitor() -> None:
            while not stop.wait(0.1) and proc.poll() is None:
                free_now = available_ram_bytes()
                if free_now is not None and free_now < a.minimum_available_ram_mb * 1024 * 1024:
                    emergency.set()
                    subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"], capture_output=True)
                    return

        thread = threading.Thread(target=monitor, daemon=True); thread.start()
        try:
            prompt_filter = set(a.only_prompt or ())
            prompt_exclusions = set(a.exclude_prompt or ())
            # Put the very long P5 last. If it hits its deadline, all shorter
            # evidence is already durable and the next row can start cleanly.
            prompts = [p for p in prompt_set["prompts"]
                       if (not prompt_filter or p["prompt_id"] in prompt_filter)
                       and p["prompt_id"] not in prompt_exclusions]
            prompts = sorted(prompts, key=lambda p: p["prompt_id"] == "P5")
            try:
                wait_ready(port, proc, 300)
            except RuntimeError:
                if not emergency.is_set():
                    raise
                for prompt in prompts:
                    pid = prompt["prompt_id"]
                    output = ""
                    record = {"prompt_id": pid, "status": "safety-blocked", "output": output,
                              "error": f"available RAM fell below {a.minimum_available_ram_mb:g} MiB during server startup",
                              "test_id": case.test_id, "model_id": case.model_id,
                              "backend": case.backend, "cache": case.turbo_type,
                              "quality_context": a.quality_context, "elapsed_seconds": 0.0,
                              "output_sha256": hashlib.sha256(b"").hexdigest()}
                    (row / f"{pid}.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
                    (row / f"{pid}-response.txt").write_text(output, encoding="utf-8")
                    print(f"{case.test_id} {pid} safety-blocked", flush=True)
                prompts = []
            for prompt in prompts:
                pid = prompt["prompt_id"]
                path = row / f"{pid}.json"
                if path.is_file():
                    prior = json.loads(path.read_text(encoding="utf-8"))
                    if prior.get("status") in TERMINAL_STATUSES and not (
                            a.retry_safety_blocked and prior.get("status") == "safety-blocked"):
                        print(f"{case.test_id} {pid} resumed", flush=True); continue
                started = time.monotonic()
                try:
                    if pid == "P6":
                        first = request(port, [{"role": "user", "content": prompt["turns"][0]["content"]}], 32)
                        messages = [{"role": "user", "content": prompt["turns"][0]["content"]},
                                    {"role": "assistant", "content": first},
                                    {"role": "user", "content": prompt["turns"][2]["content"]}]
                        output = call_with_deadline(
                            lambda: request(port, messages, 32), a.prompt_deadline_seconds)
                        record = {"prompt_id": pid, "status": "complete", "turn_1": first, "output": output}
                    else:
                        output = call_with_deadline(
                            lambda: request(port, [{"role": "user", "content": render_prompt(prompt, a.prompt_set.parent)}]),
                            a.prompt_deadline_seconds)
                        record = {"prompt_id": pid, "status": "complete", "output": output}
                except TimeoutError as exc:
                    output = ""; record = {"prompt_id": pid, "status": "timeout", "output": "",
                                           "error": f"{type(exc).__name__}: {exc}",
                                           "timeout_seconds": a.prompt_deadline_seconds}
                except Exception as exc:
                    output = ""; record = {"prompt_id": pid, "status": "safety-blocked" if emergency.is_set() else "failed",
                                           "output": "", "error": f"{type(exc).__name__}: {exc}"}
                record.update({"test_id": case.test_id, "model_id": case.model_id, "backend": case.backend,
                               "cache": case.turbo_type, "quality_context": a.quality_context,
                               "elapsed_seconds": round(time.monotonic() - started, 3),
                               "output_sha256": hashlib.sha256(output.encode()).hexdigest()})
                path.write_text(json.dumps(record, indent=2), encoding="utf-8")
                (row / f"{pid}-response.txt").write_text(output, encoding="utf-8")
                print(f"{case.test_id} {pid} {record['status']} {record['elapsed_seconds']}s", flush=True)
                if emergency.is_set():
                    break
            prompt_records = [json.loads((row / f"P{i}.json").read_text(encoding="utf-8"))
                              for i in range(1, 7) if (row / f"P{i}.json").is_file()]
            statuses = {r["prompt_id"]: r["status"] for r in prompt_records}
            if len(prompt_records) == 6 and set(statuses.values()) <= TERMINAL_STATUSES:
                completion_path.write_text(json.dumps({"test_id": case.test_id,
                    "statuses": statuses, "emergency_stop": emergency.is_set()}, indent=2), encoding="utf-8")
            elif completion_path.exists():
                completion_path.unlink()
        finally:
            stop.set()
            subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"], capture_output=True)
            try: proc.wait(20)
            except subprocess.TimeoutExpired: proc.kill(); proc.wait()
            thread.join(2); out.close(); err.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
