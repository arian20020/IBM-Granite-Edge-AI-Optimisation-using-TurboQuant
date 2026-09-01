"""Run the frozen P1-P6 quality screen for validated WB-03 runtime rows."""

from __future__ import annotations

import argparse
import atexit
import hashlib
import json
import os
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
if str(ROOT / "scripts" / "testing") not in sys.path:
    sys.path.insert(0, str(ROOT / "scripts" / "testing"))

from scripts.testing.campaigns.animehacker.matrix import load_matrix
from scripts.testing.campaigns.animehacker.runner import build_server_command
from scripts.testing.run_animehacker_retest import FORMAL_FIELDS
from scripts.testing.run_atomicbot_full_quality import acquire_runner_lock, call_with_deadline
from scripts.testing.run_atomicbot_quality_retest import render_prompt, request, wait_ready


def args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--prompt-set", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--runtime-root", type=Path, required=True)
    parser.add_argument("--cpu-build", type=Path, required=True)
    parser.add_argument("--sycl-build", type=Path, required=True)
    parser.add_argument("--diagnostic-model", type=Path, required=True)
    parser.add_argument("--granite3-model", type=Path, required=True)
    parser.add_argument("--granite8-model", type=Path, required=True)
    parser.add_argument("--only", action="append")
    parser.add_argument("--context", type=int, default=16384)
    parser.add_argument("--base-port", type=int, default=19800)
    parser.add_argument("--deadline-seconds", type=float, default=900)
    return parser.parse_args()


def quality_case_ids() -> frozenset[str]:
    return frozenset({"AH-01", "AH-02", "AH-03", "AH-04", "AH-05", "AH-06", "AH-07",
                      "AH-08", "AH-09", "AH-10"})


def runtime_summary_is_complete(test_id: str, summary: object) -> bool:
    if not isinstance(summary, dict) or summary.get("test_id") != test_id:
        return False
    samples = summary.get("samples")
    if not isinstance(samples, list) or len(samples) != 3:
        return False
    for sample in samples:
        if not isinstance(sample, dict) or sample.get("valid") is not True:
            return False
        if sample.get("missing") or sample.get("extended_missing"):
            return False
        if any(sample.get(field) is None for field in FORMAL_FIELDS):
            return False
        utilization = sample.get("utilization")
        if not isinstance(utilization, dict):
            return False
        for device in ("cpu_percent", "gpu_percent"):
            item = utilization.get(device)
            if not isinstance(item, dict) or any(item.get(field) is None for field in
                                                 ("mean", "median", "peak", "sample_count")):
                return False
    aggregate = summary.get("aggregate")
    if not isinstance(aggregate, dict):
        return False
    for field in FORMAL_FIELDS:
        item = aggregate.get(field)
        if not isinstance(item, dict) or any(item.get(stat) is None for stat in
                                             ("mean", "median", "min", "max")):
            return False
    for device in ("cpu_percent", "gpu_percent"):
        item = aggregate.get(device)
        if not isinstance(item, dict) or any(item.get(stat) is None for stat in
                                             ("mean", "median", "peak", "sample_count")):
            return False
    activation = summary.get("activation")
    return (isinstance(activation, list) and len(activation) == 3 and
            all(isinstance(item, dict) and item.get("activated") is True
                for item in activation))


def require_recovered_runtime_summaries(cases: list, runtime_root: Path) -> None:
    for case in cases:
        if case.test_id not in {"AH-06", "AH-07", "AH-09", "AH-10"}:
            continue
        summary_path = runtime_root / case.test_id / "summary.json"
        try:
            summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError):
            summary = None
        if not runtime_summary_is_complete(case.test_id, summary):
            raise RuntimeError(f"{case.test_id} requires complete runtime evidence: {summary_path}")


def main() -> int:
    a = args()
    prompt_set = json.loads(a.prompt_set.read_text(encoding="utf-8-sig"))
    models = {"gemma-1b-q4-k-m": a.diagnostic_model,
              "granite-3b-bf16": a.granite3_model,
              "granite-8b-q8-0": a.granite8_model}
    selected = set(a.only or ())
    cases = [case for case in load_matrix(a.matrix) if case.test_id in quality_case_ids() and
             (not selected or case.test_id in selected)]
    require_recovered_runtime_summaries(cases, a.runtime_root)
    a.output_root.mkdir(parents=True, exist_ok=True)
    _handle, release = acquire_runner_lock(a.output_root)
    atexit.register(release)
    for index, case in enumerate(cases):
        row = a.output_root / case.test_id
        row.mkdir(parents=True, exist_ok=True)
        complete_path = row / "complete.json"
        if complete_path.is_file():
            prior = json.loads(complete_path.read_text(encoding="utf-8"))
            if len(prior.get("statuses", {})) == 6:
                print(f"{case.test_id} resumed complete", flush=True)
                continue
        server = (a.cpu_build if case.backend == "cpu" else a.sycl_build) / "bin/llama-server.exe"
        port = a.base_port + index
        quality_case = case.__class__(**{**case.__dict__, "context": a.context})
        command = build_server_command(quality_case, server, models[case.model_id], port)
        (row / "command.json").write_text(json.dumps({"command": command,
            "environment": {key: os.environ.get(key) for key in
                            ("ONEAPI_DEVICE_SELECTOR", "GGML_SYCL_ENABLE_FLASH_ATTN")}}, indent=2), encoding="utf-8")
        stdout = (row / "server-stdout.log").open("ab")
        stderr = (row / "server-stderr.log").open("ab")
        process = subprocess.Popen(command, stdout=stdout, stderr=stderr, env=os.environ.copy(),
                                   creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        statuses = {}
        try:
            wait_ready(port, process, 300)
            prompts = sorted(prompt_set["prompts"], key=lambda item: item["prompt_id"] == "P5")
            for prompt in prompts:
                prompt_id = prompt["prompt_id"]
                record_path = row / f"{prompt_id}.json"
                if record_path.is_file():
                    record = json.loads(record_path.read_text(encoding="utf-8"))
                    if record.get("status") == "complete":
                        statuses[prompt_id] = "complete"
                        continue
                started = time.monotonic()
                try:
                    if prompt_id == "P6":
                        first = call_with_deadline(lambda: request(port, [{"role": "user",
                            "content": prompt["turns"][0]["content"]}], 32), a.deadline_seconds)
                        messages = [{"role": "user", "content": prompt["turns"][0]["content"]},
                                    {"role": "assistant", "content": first},
                                    {"role": "user", "content": prompt["turns"][2]["content"]}]
                        output = call_with_deadline(lambda: request(port, messages, 32), a.deadline_seconds)
                        record = {"prompt_id": prompt_id, "status": "complete",
                                  "turn_1": first, "output": output}
                    else:
                        rendered = render_prompt(prompt, a.prompt_set.parent)
                        output = call_with_deadline(lambda: request(port, [{"role": "user",
                            "content": rendered}]), a.deadline_seconds)
                        record = {"prompt_id": prompt_id, "status": "complete", "output": output}
                except Exception as exc:
                    output = ""
                    record = {"prompt_id": prompt_id, "status": "failed", "output": "",
                              "error": f"{type(exc).__name__}: {exc}"}
                record.update({"test_id": case.test_id, "model_id": case.model_id,
                               "backend": case.backend, "cache": case.cache,
                               "quality_context": a.context,
                               "elapsed_seconds": round(time.monotonic() - started, 3),
                               "output_sha256": hashlib.sha256(output.encode()).hexdigest()})
                record_path.write_text(json.dumps(record, indent=2), encoding="utf-8")
                (row / f"{prompt_id}-response.txt").write_text(output, encoding="utf-8")
                statuses[prompt_id] = record["status"]
                print(f"{case.test_id} {prompt_id} {record['status']}", flush=True)
                if record["status"] != "complete":
                    raise RuntimeError(f"{case.test_id} {prompt_id} failed; evidence preserved")
            complete_path.write_text(json.dumps({"test_id": case.test_id,
                "statuses": statuses}, indent=2), encoding="utf-8")
        finally:
            subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            try:
                process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                process.kill(); process.wait()
            stdout.close(); stderr.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
