"""Final completeness and evidence reconciliation for WB-03."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import statistics
from datetime import datetime
from pathlib import Path


EXPECTED_IDS = tuple([f"AH-B{i:02d}" for i in range(1, 9)] +
                     [f"AH-{i:02d}" for i in range(1, 11)])


def validate_markdown(text: str) -> None:
    if re.search(r"\bN/A\b", text):
        raise ValueError("literal N/A is forbidden")
    blank_cells = []
    for number, line in enumerate(text.splitlines(), 1):
        if line.startswith("|") and re.search(r"\|\s*\|", line):
            blank_cells.append(number)
    if blank_cells:
        raise ValueError(f"blank workbook cells: {blank_cells}")
    for test_id in EXPECTED_IDS:
        if not re.search(rf"\|\s*{re.escape(test_id)}\s*\|", text):
            raise ValueError(f"missing test id: {test_id}")


def validate_runtime(root: Path) -> dict:
    runnable = ("AH-01", "AH-02", "AH-03", "AH-04", "AH-05", "AH-08")
    metrics = ("peak_working_set_mb", "peak_private_bytes_mb", "available_ram_min_mb",
               "kv_mb", "ttft_ms", "prompt_tps", "decode_tps",
               "generation_duration_ms", "gpu_memory_peak_mb", "cpu_percent", "gpu_percent")
    result = {}
    for test_id in runnable:
        path = root / test_id / "summary.json"
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
        if len(payload.get("samples", [])) != 3:
            raise ValueError(f"{test_id} does not have three samples")
        missing = [field for field in metrics if payload.get("aggregate", {}).get(field) is None]
        if missing:
            raise ValueError(f"{test_id} missing runtime metrics: {missing}")
        result[test_id] = str(path)
    return result


def validate_quality(root: Path, adjudication: Path) -> dict:
    payload = json.loads(adjudication.read_text(encoding="utf-8-sig"))
    for test_id in ("AH-01", "AH-02", "AH-03", "AH-04", "AH-05", "AH-08"):
        if test_id not in payload:
            raise ValueError(f"missing quality adjudication: {test_id}")
        if len(payload[test_id].get("prompts", {})) != 6:
            raise ValueError(f"incomplete quality prompts: {test_id}")
        for number in range(1, 7):
            response = root / test_id / f"P{number}-response.txt"
            raw = root / test_id / f"P{number}.json"
            if not response.is_file() or not raw.is_file():
                raise ValueError(f"missing quality evidence: {test_id} P{number}")
            raw_payload = json.loads(raw.read_text(encoding="utf-8-sig"))
            # The runner hashes the model payload before Windows text-file newline
            # translation. Read in text mode to recover that canonical LF payload.
            response_text = response.read_text(encoding="utf-8")
            response_hash = hashlib.sha256(response_text.encode("utf-8")).hexdigest()
            if raw_payload.get("output") != response_text:
                raise ValueError(f"quality response content mismatch: {test_id} P{number}")
            if raw_payload.get("output_sha256") != response_hash:
                raise ValueError(f"quality response hash mismatch: {test_id} P{number}")
    return {test_id: payload[test_id]["mean_score"] for test_id in payload}


def validate_recovery(runtime_root: Path, quality_root: Path, adjudication: Path,
                      workbook_text: str | None = None) -> dict:
    """Validate recovered AH-09 runtime/quality and AH-10 terminal safety evidence."""
    summary_path = runtime_root / "AH-09" / "summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
    samples = summary.get("samples", [])
    if len(samples) != 3 or not all(sample.get("valid") is True for sample in samples):
        raise ValueError("AH-09 does not have three valid samples")
    if summary.get("cache") != "tq3_0" or summary.get("backend") != "sycl-partial":
        raise ValueError("AH-09 cache/backend mismatch")
    metrics = ("peak_working_set_mb", "peak_private_bytes_mb", "available_ram_min_mb",
               "kv_mb", "ttft_ms", "prompt_tps", "decode_tps",
               "generation_duration_ms", "gpu_memory_peak_mb")
    aggregate = summary.get("aggregate", {})
    for metric in metrics:
        value = aggregate.get(metric)
        if not isinstance(value, dict) or any(value.get(key) is None for key in ("min", "max", "mean", "median")):
            raise ValueError(f"AH-09 missing runtime metric: {metric}")
        if any(sample.get(metric if metric != "peak_working_set_mb" else "peak_working_set_mb") is None
               for sample in samples):
            raise ValueError(f"AH-09 missing sample runtime metric: {metric}")
        sample_values = [sample[metric] for sample in samples]
        expected = {"min": min(sample_values), "max": max(sample_values),
                    "mean": statistics.fmean(sample_values), "median": statistics.median(sample_values)}
        if any(abs(value[key] - expected[key]) > 1e-9 for key in expected):
            raise ValueError(f"AH-09 aggregate metric mismatch: {metric}")
    for metric in ("cpu_percent", "gpu_percent"):
        value = aggregate.get(metric)
        if not isinstance(value, dict) or any(value.get(key) is None for key in ("mean", "median", "peak", "sample_count")):
            raise ValueError(f"AH-09 missing runtime metric: {metric}")
        if any(not isinstance(sample.get("utilization", {}).get(metric), dict)
               or any(sample["utilization"][metric].get(key) is None for key in ("mean", "median", "peak", "sample_count"))
               for sample in samples):
            raise ValueError(f"AH-09 missing sample utilization: {metric}")
    activations = summary.get("activation", [])
    expected_activation = {"actual_device": "CPU", "kv_mb": 70.0,
                           "offloaded_layers": 1, "total_layers": 41}
    if len(activations) != 3 or any(any(item.get(key) != value for key, value in expected_activation.items())
                                    for item in activations):
        raise ValueError("AH-09 activation mismatch: CPU KV 70 MiB and 1/41 offload required")
    quality = json.loads(adjudication.read_text(encoding="utf-8-sig"))
    prompts = quality.get("AH-09", {}).get("prompts", {})
    if set(prompts) != {f"P{i}" for i in range(1, 7)}:
        raise ValueError("AH-09 does not have six quality records")
    for number in range(1, 7):
        prompt = f"P{number}"
        response = quality_root / "AH-09" / f"{prompt}-response.txt"
        raw = quality_root / "AH-09" / f"{prompt}.json"
        raw_payload = json.loads(raw.read_text(encoding="utf-8-sig"))
        response_text = response.read_text(encoding="utf-8")
        if raw_payload.get("output") != response_text:
            raise ValueError(f"quality response content mismatch: AH-09 {prompt}")
        if raw_payload.get("output_sha256") != hashlib.sha256(response_text.encode()).hexdigest():
            raise ValueError(f"quality response hash mismatch: AH-09 {prompt}")
    scores = [prompts[f"P{i}"].get("final_score") for i in range(1, 7)]
    expected_mean = 6.058333333333334
    if any(score is None for score in scores) or abs(sum(scores) / 6 - expected_mean) > 1e-12 \
            or abs(quality["AH-09"].get("mean_score", -1) - expected_mean) > 1e-12:
        raise ValueError("AH-09 quality mean mismatch")
    if workbook_text is not None and not re.search(r"\|\s*Mean\s*\|\s*6\.0583\s*\|", workbook_text):
        raise ValueError("workbook does not report AH-09 mean 6.0583")

    ah10 = runtime_root / "AH-10"
    wrapper_path, preflight_path, cleanup_path = (ah10 / name for name in
        ("wrapper-execution.json", "preflight.json", "post-stop-cleanup.json"))
    wrapper = json.loads(wrapper_path.read_text(encoding="utf-8-sig"))
    preflight = json.loads(preflight_path.read_text(encoding="utf-8-sig"))
    cleanup = json.loads(cleanup_path.read_text(encoding="utf-8-sig"))
    if (wrapper.get("schema_version") != 2 or wrapper.get("evidence_kind") != "wrapper-execution"
            or preflight.get("schema_version") != 2 or preflight.get("evidence_kind") != "preflight"
            or cleanup.get("schema_version") != 2 or cleanup.get("evidence_kind") != "post-stop-cleanup"
            or wrapper.get("preflight_file") != preflight_path.name
            or wrapper.get("cleanup_file") != cleanup_path.name
            or wrapper.get("pilot_directory") != "pilot"
            or preflight.get("minimum_available_ram_mib") != 2048
            or cleanup.get("minimum_available_ram_mib") != 2048):
        raise ValueError("AH-10 lacks sourced schema-v2 memory-gate evidence")
    command = json.loads((ah10 / "pilot" / "command.json").read_text(encoding="utf-8-sig"))
    environment = json.loads((ah10 / "environment.json").read_text(encoding="utf-8-sig"))
    if command.get("environment", {}).get("ONEAPI_DEVICE_SELECTOR") != "level_zero:0" \
            or environment.get("ONEAPI_DEVICE_SELECTOR") != "level_zero:0" \
            or command.get("minimum_available_ram_mb") != 2048.0:
        raise ValueError("AH-10 did not select Level Zero at the 2048 MiB floor")
    measurement = json.loads((ah10 / "pilot" / "measurement.json").read_text(encoding="utf-8-sig"))
    if wrapper.get("controller_exit_code") != 1 or measurement.get("valid") is not False \
            or measurement.get("exit_code") != 1 \
            or measurement.get("request_error") != "emergency stop: available RAM crossed configured floor" \
            or any(measurement.get(field) is not None for field in ("ttft_ms", "prompt_tps", "decode_tps")):
        raise ValueError("AH-10 controller did not safety-stop before request")
    events = [json.loads(line) for line in (ah10 / "pilot" / "events.jsonl").read_text(encoding="utf-8").splitlines()]
    if not any(event.get("kind") == "emergency_stop" and event.get("minimum_available_ram_mb") == 2048.0
               for event in events):
        raise ValueError("AH-10 emergency-stop event missing")
    count_fields = ("llama_process_count", "runtime_controller_process_count", "measurement_controller_process_count")
    if any(preflight.get(field) != 0 or cleanup.get(field) != 0 for field in count_fields):
        raise ValueError("AH-10 process counts are nonzero")
    if cleanup.get("captured_after_synchronous_controller_return") is not True \
            or datetime.fromisoformat(cleanup["timestamp"]) <= datetime.fromisoformat(wrapper["controller_returned_at"]) \
            or cleanup.get("available_physical_ram_mib", 0) <= 2048:
        raise ValueError("AH-10 cleanup linkage or recovered RAM invalid")
    if (ah10 / "summary.json").exists() or (quality_root / "AH-10").exists() or "AH-10" in quality:
        raise ValueError("AH-10 must not have summary or quality evidence")
    return {
        "AH-09": {"status": "complete", "runtime": str(summary_path),
                  "quality_mean": quality["AH-09"]["mean_score"] if "mean_score" in quality["AH-09"] else None},
        "AH-10": {"status": "safety-classified", "wrapper": str(runtime_root / "AH-10" / "wrapper-execution.json"),
                  "preflight": str(runtime_root / "AH-10" / "preflight.json")},
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workbook", type=Path, required=True)
    parser.add_argument("--runtime-root", type=Path, required=True)
    parser.add_argument("--quality-root", type=Path, required=True)
    parser.add_argument("--adjudication", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--recovery-runtime-root", type=Path)
    parser.add_argument("--recovery-quality-root", type=Path)
    parser.add_argument("--recovery-adjudication", type=Path)
    args = parser.parse_args()
    validate_markdown(args.workbook.read_text(encoding="utf-8-sig"))
    result = {"runtime": validate_runtime(args.runtime_root),
              "quality": validate_quality(args.quality_root, args.adjudication)}
    if args.recovery_runtime_root or args.recovery_quality_root or args.recovery_adjudication:
        if not all((args.recovery_runtime_root, args.recovery_quality_root, args.recovery_adjudication)):
            raise ValueError("all recovery evidence arguments are required together")
        result["recovery"] = validate_recovery(
            args.recovery_runtime_root, args.recovery_quality_root, args.recovery_adjudication,
            args.workbook.read_text(encoding="utf-8-sig"))
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True), encoding="utf-8")
    print("WB-03 reconciliation passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
