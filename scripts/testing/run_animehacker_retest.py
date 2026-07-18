"""Crash-resumable WB-03 runtime measurement controller."""

from __future__ import annotations

import argparse
import hashlib
import json
import statistics
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.animehacker.activation import classify_activation
from scripts.testing.animehacker.matrix import load_matrix
from scripts.testing.animehacker.runner import build_server_command, format_runtime_prompt, select_cases
from scripts.testing.animehacker.state import checkpoint


FORMAL_FIELDS = ("peak_working_set_mb", "peak_private_bytes_mb", "available_ram_min_mb",
                 "kv_mb", "ttft_ms", "prompt_tps", "decode_tps",
                 "generation_duration_ms", "gpu_memory_peak_mb")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--cpu-build", type=Path, required=True)
    parser.add_argument("--sycl-build", type=Path, required=True)
    parser.add_argument("--diagnostic-model", type=Path, required=True)
    parser.add_argument("--granite3-model", type=Path, required=True)
    parser.add_argument("--granite8-model", type=Path, required=True)
    parser.add_argument("--only", action="append")
    parser.add_argument("--start-at")
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--include-guarded", action="store_true")
    parser.add_argument("--pilot-only", action="store_true")
    parser.add_argument("--minimum-available-ram-mb", type=float, default=3072)
    parser.add_argument("--measurement-tokens", type=int, default=32)
    parser.add_argument("--base-port", type=int, default=19300)
    return parser.parse_args()


def read_json(path: Path) -> dict | None:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return None


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def aggregate(samples: list[dict]) -> dict:
    if len(samples) != 3:
        raise ValueError("exactly three formal samples are required")
    result = {}
    for field in FORMAL_FIELDS:
        values = [sample.get(field) for sample in samples]
        if any(value is None for value in values):
            raise ValueError(f"missing formal metric: {field}")
        result[field] = {"mean": statistics.fmean(values), "median": statistics.median(values),
                         "min": min(values), "max": max(values)}
    for device in ("cpu_percent", "gpu_percent"):
        summaries = [sample.get("utilization", {}).get(device) for sample in samples]
        if any(not item for item in summaries):
            raise ValueError(f"missing utilization metric: {device}")
        result[device] = {
            "mean": statistics.fmean(item["mean"] for item in summaries),
            "median": statistics.median(item["median"] for item in summaries),
            "peak": max(item["peak"] for item in summaries),
            "sample_count": sum(item["sample_count"] for item in summaries),
        }
    return result


def main() -> int:
    args = parse_args()
    fixed_prompt = "Explain edge AI quantization in a detailed technical paragraph."
    args.output_root.mkdir(parents=True, exist_ok=True)
    state_path = args.output_root / "state.json"
    # Preserve terminal checkpoints even when deliberately rerunning selected rows.
    state = read_json(state_path)
    if state is None:
        state = {"schema_version": 1, "attempts": {}}
    models = {
        "gemma-1b-q4-k-m": args.diagnostic_model,
        "granite-3b-bf16": args.granite3_model,
        "granite-8b-q8-0": args.granite8_model,
    }
    cases = [item for item in load_matrix(args.matrix) if item.phase == "runtime"]
    if not args.include_guarded:
        cases = [item for item in cases if item.guard == "none"]
    cases = select_cases(cases, only=set(args.only) if args.only else None,
                         start_at=args.start_at, resume_state=state if args.resume else None)
    collector = ROOT / "scripts/testing/measure_animehacker_server.py"
    for case_index, case in enumerate(cases):
        case_root = args.output_root / case.test_id
        case_root.mkdir(parents=True, exist_ok=True)
        server = (args.cpu_build if case.backend == "cpu" else args.sycl_build) / "bin/llama-server.exe"
        model = models[case.model_id]
        for required in (server, model):
            if not required.is_file():
                raise FileNotFoundError(required)
        model_record = {"path": str(model.resolve()), "bytes": model.stat().st_size,
                        "sha256": sha256_file(model)}
        (case_root / "model.json").write_text(json.dumps(model_record, indent=2), encoding="utf-8")
        environment = ({"ONEAPI_DEVICE_SELECTOR": "level_zero:0"}
                       if case.backend == "sycl-partial" else {})
        environment_path = case_root / "environment.json"
        environment_path.write_text(json.dumps(environment, indent=2), encoding="utf-8")
        labels = ("pilot",) if args.pilot_only else ("pilot", "warmup", "sample-1", "sample-2", "sample-3")
        formal = []
        activations = []
        state["attempts"][case.test_id] = {"status": "running", "reconciled": False}
        checkpoint(state_path, state)
        for sample_index, label in enumerate(labels):
            output = case_root / label
            measurement_path = output / "measurement.json"
            measurement = read_json(measurement_path) if args.resume else None
            if measurement is None or not measurement.get("valid"):
                port = args.base_port + case_index * 10 + sample_index
                server_command = build_server_command(case, server, model, port)
                command = [sys.executable, str(collector), "--output-dir", str(output),
                           "--sample-id", f"{case.test_id}-{label}", "--port", str(port),
                           "--prompt", format_runtime_prompt(case, fixed_prompt),
                           "--timeout-seconds", "900",
                           "--minimum-available-ram-mb", str(args.minimum_available_ram_mb),
                           "--measurement-tokens", str(args.measurement_tokens),
                           "--ignore-eos",
                           "--environment-json", str(environment_path), "--", *server_command]
                output.mkdir(parents=True, exist_ok=True)
                (output / "command.json").write_text(json.dumps({
                    "controller_command": command, "server_command": server_command,
                    "environment": environment, "minimum_available_ram_mb": args.minimum_available_ram_mb,
                }, indent=2), encoding="utf-8")
                completed = subprocess.run(command, capture_output=True, text=True, timeout=960)
                (output / "controller-stdout.txt").write_text(completed.stdout, encoding="utf-8")
                (output / "controller-stderr.txt").write_text(completed.stderr, encoding="utf-8")
                measurement = read_json(measurement_path)
                if completed.returncode or measurement is None or not measurement.get("valid"):
                    raise RuntimeError(f"{case.test_id} {label} invalid; see {output}")
            stderr = (output / "stderr.txt").read_text(encoding="utf-8", errors="replace")
            # Reconstruction from the frozen row is independently checked against runtime evidence.
            activation_command = build_server_command(case, server, model,
                                                      args.base_port + case_index * 10 + sample_index)
            activation = classify_activation(activation_command, stderr,
                                             expected_cache=case.cache, backend=case.backend)
            (output / "activation.json").write_text(json.dumps(activation, indent=2), encoding="utf-8")
            if not activation.get("activated"):
                raise RuntimeError(f"{case.test_id} {label} activation unproven: {activation['reason']}")
            activations.append(activation)
            if label.startswith("sample-"):
                missing = [field for field in FORMAL_FIELDS if measurement.get(field) is None]
                if missing:
                    raise RuntimeError(f"{case.test_id} {label} missing metrics: {missing}")
                formal.append(measurement)
        if args.pilot_only:
            state["attempts"][case.test_id] = {"status": "pilot-complete", "reconciled": False}
            checkpoint(state_path, state)
            continue
        summary = {"test_id": case.test_id, "model": model_record, "cache": case.cache,
                   "backend": case.backend, "context": case.context, "samples": formal,
                   "aggregate": aggregate(formal), "activation": activations[-3:]}
        (case_root / "summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True), encoding="utf-8")
        state["attempts"][case.test_id] = {"status": "complete", "reconciled": True,
                                                   "summary": str((case_root / 'summary.json').resolve())}
        checkpoint(state_path, state)
        print(json.dumps({"test_id": case.test_id, "aggregate": summary["aggregate"]}), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
