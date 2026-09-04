"""Independent structural and arithmetic validation of formal scale evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from .schedule import CONFIGURATIONS


def _hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _load(path: Path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as error:
        raise ValueError(f"invalid JSON: {path.name}") from error


def validate_scale_run(run_directory: Path) -> dict[str, object]:
    root = Path(run_directory).resolve()
    terminal = _load(root / "terminal.json")
    if terminal != {"schema_version": "2.0", "status": "completed", "files": terminal.get("files")}:
        raise ValueError("terminal record is not closed")
    expected_names = {"benchmark.json", "evaluation.json", "summary.json"}
    records = terminal["files"]
    if {item.get("name") for item in records} != expected_names or len(records) != len(expected_names):
        raise ValueError("terminal file set mismatch")
    for record in records:
        name = record["name"]
        path = root / name
        if not path.is_file() or path.stat().st_size != record.get("bytes") or _hash(path) != record.get("sha256"):
            raise ValueError(f"terminal hash or byte mismatch: {name}")
    benchmark = _load(root / "benchmark.json")
    evaluation = _load(root / "evaluation.json")
    summary = _load(root / "summary.json")
    if summary.get("campaign_id") != "turbovec-production-scale-final-evaluation-v2" or summary.get("experiment_id") != "EXP-TV-COMP-001":
        raise ValueError("campaign identity mismatch")
    raw_repetitions = benchmark.get("repetitions", [])
    evaluated = evaluation.get("repetitions", [])
    if len(raw_repetitions) != summary.get("requested_repetitions") or len(evaluated) != len(raw_repetitions):
        raise ValueError("repetition arithmetic mismatch")
    allowed_orders = {CONFIGURATIONS[offset:] + CONFIGURATIONS[:offset] for offset in range(len(CONFIGURATIONS))}
    valid_count = 0
    for raw, derived in zip(raw_repetitions, evaluated, strict=True):
        order = tuple(raw.get("configuration_order", ()))
        if order not in allowed_orders or set(raw.get("configurations", {})) != set(CONFIGURATIONS):
            raise ValueError("configuration schedule mismatch")
        if raw.get("repetition") != derived.get("repetition") or raw.get("seed") != derived.get("seed") or raw.get("configuration_order") != derived.get("configuration_order"):
            raise ValueError("raw/derived repetition identity mismatch")
        query_order = raw.get("query_order", [])
        if sorted(query_order) != list(range(128)):
            raise ValueError("query permutation mismatch")
        hashes = {row.get("embedding_matrix_sha256") for row in raw["configurations"].values()}
        query_hashes = {row.get("query_matrix_sha256") for row in raw["configurations"].values()}
        if len(hashes) != 1 or len(query_hashes) != 1 or any(len(str(item)) != 64 for item in hashes | query_hashes):
            raise ValueError("embedding identity mismatch")
        for row in raw["configurations"].values():
            if row.get("warmup_batches") != summary.get("warmup_batches") or len(row.get("warm_query_latency_ms", [])) != summary.get("measured_batches"):
                raise ValueError("warmup or measured batch arithmetic mismatch")
        if raw.get("valid") is True:
            valid_count += 1
    if valid_count != summary.get("valid_repetitions"):
        raise ValueError("valid repetition arithmetic mismatch")
    hashes = [summary.get("embedding_manifest_sha256"), summary.get("readiness_decision_sha256"), *summary.get("dataset", {}).values()]
    if any(not isinstance(item, str) or len(item) != 64 for item in hashes):
        raise ValueError("input hash is invalid")
    text = "\n".join(path.read_text(encoding="utf-8") for path in root.glob("*.json"))
    if "PRIVATE_SENTINEL" in text or "C:\\\\Users\\\\" in text:
        raise ValueError("private path or sentinel detected")
    return {"status": "valid", "scale": summary["scale"], "valid_repetitions": valid_count}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-directory", type=Path, required=True)
    result = validate_scale_run(parser.parse_args(argv).run_directory)
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
