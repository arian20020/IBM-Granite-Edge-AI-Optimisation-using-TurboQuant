"""Final completeness and evidence reconciliation for WB-03."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
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


def validate_recovery(runtime_root: Path, quality_root: Path, adjudication: Path) -> dict:
    """Validate recovered AH-09 runtime/quality and AH-10 terminal safety evidence."""
    summary_path = runtime_root / "AH-09" / "summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
    samples = summary.get("samples", [])
    if len(samples) != 3 or not all(sample.get("valid") is True for sample in samples):
        raise ValueError("AH-09 does not have three valid samples")
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
    wrapper = json.loads((runtime_root / "AH-10" / "wrapper-execution.json").read_text(encoding="utf-8-sig"))
    preflight = json.loads((runtime_root / "AH-10" / "preflight.json").read_text(encoding="utf-8-sig"))
    if (wrapper.get("schema_version") != 2 or wrapper.get("evidence_kind") != "wrapper-execution"
            or preflight.get("schema_version") != 2 or preflight.get("evidence_kind") != "preflight"
            or preflight.get("minimum_available_ram_mib") != 2048):
        raise ValueError("AH-10 lacks sourced schema-v2 memory-gate evidence")
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
            args.recovery_runtime_root, args.recovery_quality_root, args.recovery_adjudication)
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True), encoding="utf-8")
    print("WB-03 reconciliation passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
