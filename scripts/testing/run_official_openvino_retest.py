"""Reconcile every WB-04 runtime row to measured or sourced terminal state."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.matrix import execution_contract, load_matrix
from scripts.testing.official_openvino.quality import terminal_quality_record
from scripts.testing.official_openvino.runner import terminal_runtime_record, validate_runtime_records


def write(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> int:
    matrix = load_matrix(ROOT / "experiments/manifests/official-openvino/retest-matrix.json")
    runtime_cases = [case for case in matrix if case.phase in {"baseline", "formal"}]
    conversion_source = "experiments/raw-results/official-openvino/2026-07-19/conversion/conversion-results.json"
    codec_source = "experiments/raw-results/official-openvino/2026-07-19/diagnostics/source-audit.json"
    diagnostic_source = "experiments/raw-results/official-openvino/2026-07-19/diagnostics/official-api-probes.json"
    records = []
    for case in runtime_cases:
        if case.test_id == "OV-01":
            reason, source = "diagnostic IR has no token-generation workload", diagnostic_source
        elif case.phase == "formal" and case.test_id not in {"OV-TQ-01", "OV-TQ-02"}:
            reason, source = "official source has no TurboQuant/TBQ3/TBQ4 codec", codec_source
        else:
            reason, source = "conversion memory gate", conversion_source
        records.append(terminal_runtime_record(case.test_id, reason, source))
    expected_ids = {case.test_id for case in runtime_cases}
    expected_rejections = {
        case.test_id
        for case in runtime_cases
        if execution_contract(case).expected_outcome == "expected-rejection"
    }
    audit = validate_runtime_records(
        records,
        expected_ids,
        expected_rejections=expected_rejections,
    )
    root = ROOT / "experiments/raw-results/official-openvino/2026-07-19/runtime"
    write(root / "runtime-results.json", {"records": records, "reconciliation": audit})

    quality = []
    for case in runtime_cases:
        if case.quality_required:
            runtime = next(row for row in records if row["test_id"] == case.test_id)
            quality.append(terminal_quality_record(case.test_id, runtime["reason"], runtime["source"]))
    write(root / "quality-results.json", {"records": quality, "required_count": len(quality),
                                           "scored_count": 0, "terminal_count": len(quality)})
    print(json.dumps({
        "runtime_rows": len(records),
        "quality_rows": len(quality),
        "accepted": audit["accepted"],
        "failed": audit["failed_count"],
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
