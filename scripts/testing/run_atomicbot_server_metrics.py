"""Collect loaded-server TTFT, process-tree RAM, and KV allocation for WB-02."""

from __future__ import annotations

import argparse
import json
import statistics
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.atomicbot.matrix import load_matrix
from scripts.testing.atomicbot.runner import build_server_command
from scripts.testing.atomicbot.state import checkpoint


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--cpu-build", type=Path, required=True)
    parser.add_argument("--vulkan-build", type=Path, required=True)
    parser.add_argument("--gemma-model", type=Path, required=True)
    parser.add_argument("--granite3-model", type=Path, required=True)
    parser.add_argument("--granite8-model", type=Path, required=True)
    parser.add_argument("--only", action="append")
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--base-port", type=int, default=18200)
    parser.add_argument("--include-guarded", action="store_true")
    parser.add_argument("--minimum-available-ram-mb", type=float, default=0)
    parser.add_argument("--pilot-only", action="store_true")
    parser.add_argument("--measurement-tokens", type=int, default=16)
    parser.add_argument("--ignore-eos", action="store_true")
    return parser.parse_args()


def aggregate(samples: list[dict]) -> dict:
    fields = ("peak_ram_mb", "kv_mb", "ttft_ms")
    result = {field: {"median": statistics.median(sample[field] for sample in samples),
                    "min": min(sample[field] for sample in samples),
                    "max": max(sample[field] for sample in samples)} for field in fields}
    for device in ("cpu_percent", "gpu_percent"):
        values = [sample["utilization"][device][stat]
                  for sample in samples if sample.get("utilization", {}).get(device)
                  for stat in ("mean",)]
        medians = [sample["utilization"][device]["median"] for sample in samples
                   if sample.get("utilization", {}).get(device)]
        peaks = [sample["utilization"][device]["peak"] for sample in samples
                 if sample.get("utilization", {}).get(device)]
        result[device] = {
            "mean": statistics.fmean(values),
            "median": statistics.median(medians),
            "peak": max(peaks),
            "sample_count": sum(sample["utilization"][device]["sample_count"]
                                for sample in samples
                                if sample.get("utilization", {}).get(device)),
        } if values else None
    return result


def read_valid_measurement(path: Path) -> dict | None:
    if not path.exists():
        return None
    try:
        measurement = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    return measurement if measurement.get("valid") is True else None


def main() -> int:
    args = parse_args()
    args.output_root.mkdir(parents=True, exist_ok=True)
    models = {"gemma-3-1b": args.gemma_model, "granite-4.1-3b": args.granite3_model,
              "granite-4.1-8b": args.granite8_model}
    selected = set(args.only or ())
    cases = [case for case in load_matrix(args.matrix)
             if case.backend != "build" and (case.guard == "none" or args.include_guarded)
             and (not selected or case.test_id in selected)]
    collector = ROOT / "scripts/testing/measure_llama_server.py"
    state = {"revision": 1, "rows": {case.test_id: "pending" for case in cases}}
    for case_index, case in enumerate(cases):
        case_root = args.output_root / case.test_id
        summary_path = case_root / "server-metrics-summary.json"
        if args.resume and summary_path.exists():
            state["rows"][case.test_id] = "complete"
            checkpoint(args.output_root / "state.json", state)
            continue
        state["rows"][case.test_id] = "running"
        checkpoint(args.output_root / "state.json", state)
        server = (args.cpu_build if case.backend == "cpu" else args.vulkan_build) / "bin/llama-server.exe"
        model = models[case.model_id]
        if not server.is_file() or not model.is_file():
            raise FileNotFoundError(server if not server.is_file() else model)
        environment_path = case_root / "environment.json"
        case_root.mkdir(parents=True, exist_ok=True)
        environment_path.write_text(json.dumps(
            {"GGML_VK_VISIBLE_DEVICES": "0"} if case.backend.startswith("vulkan") else {}, indent=2
        ), encoding="utf-8")
        measurements = []
        labels = ("pilot",) if args.pilot_only else ("pilot", "warmup", "sample-1", "sample-2", "sample-3")
        for sample_index, label in enumerate(labels):
            output = case_root / label
            measurement_path = output / "measurement.json"
            measurement = read_valid_measurement(measurement_path) if args.resume else None
            if measurement is None:
                port = args.base_port + case_index * len(labels) + sample_index
                server_command = build_server_command(case, server, model, port)
                command = [sys.executable, str(collector), "--output-dir", str(output),
                           "--sample-id", f"{case.test_id}-{label}", "--port", str(port),
                           "--prompt", "Reply with OK only.", "--environment-json",
                           str(environment_path), "--timeout-seconds", "900", "--", *server_command]
                marker = command.index("--")
                command[marker:marker] = ["--measurement-tokens", str(args.measurement_tokens)]
                if args.ignore_eos:
                    marker = command.index("--")
                    command[marker:marker] = ["--ignore-eos"]
                if args.minimum_available_ram_mb:
                    marker = command.index("--")
                    command[marker:marker] = ["--minimum-available-ram-mb", str(args.minimum_available_ram_mb)]
                completed = subprocess.run(command, text=True, capture_output=True, timeout=960)
                if completed.returncode != 0:
                    raise RuntimeError(f"{case.test_id} {label} failed: {completed.stderr}\n{completed.stdout}")
                measurement = json.loads(measurement_path.read_text(encoding="utf-8"))
            if not measurement.get("valid"):
                raise RuntimeError(f"invalid measurement: {measurement_path}")
            if label.startswith("sample-"):
                measurements.append(measurement)
        if args.pilot_only:
            state["rows"][case.test_id] = "pilot-complete"
            checkpoint(args.output_root / "state.json", state)
            print(json.dumps({"test_id": case.test_id, "pilot": "valid",
                              "peak_ram_mb": measurement["peak_ram_mb"],
                              "kv_mb": measurement["kv_mb"], "ttft_ms": measurement["ttft_ms"]}), flush=True)
            continue
        summary = {"test_id": case.test_id, "backend": case.backend, "context": case.context,
                   "cache": case.turbo_type, "samples": measurements,
                   "aggregate": aggregate(measurements),
                   "ttft_definition": "HTTP request initiation to first streamed generated token"}
        summary_path.write_text(json.dumps(summary, indent=2, sort_keys=True), encoding="utf-8")
        state["rows"][case.test_id] = "complete"
        checkpoint(args.output_root / "state.json", state)
        print(json.dumps({"test_id": case.test_id, **summary["aggregate"]}, sort_keys=True), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
