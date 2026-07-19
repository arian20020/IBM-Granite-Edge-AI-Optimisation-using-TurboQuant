"""Rebuild the crash-resume state from terminal WB-03 evidence."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path

try:
    from scripts.testing.reconcile_animehacker_workbook import validate_recovery
except ModuleNotFoundError:  # Direct `python scripts/testing/...py` execution.
    from reconcile_animehacker_workbook import validate_recovery


def finalize_state(runtime_root: Path, recovery_root: Path, quality_root: Path,
                   adjudication: Path, workbook_text: str) -> dict:
    """Write terminal state only after full recovered-evidence reconciliation."""
    validate_recovery(recovery_root, quality_root, adjudication, workbook_text)
    attempts = {}
    for test_id in ("AH-01", "AH-02", "AH-03", "AH-04", "AH-05", "AH-08"):
        summary = runtime_root / test_id / "summary.json"
        if not summary.is_file():
            raise FileNotFoundError(summary)
        attempts[test_id] = {"status": "complete", "reconciled": True,
                             "summary": str(summary.resolve())}
    for test_id in ("AH-06", "AH-07"):
        attempts[test_id] = {"status": "safety-classified", "reconciled": True,
                             "evidence": str((runtime_root / test_id).resolve())}
    ah09 = recovery_root / "AH-09" / "summary.json"
    ah10 = recovery_root / "AH-10" / "wrapper-execution.json"
    attempts["AH-09"] = {"status": "complete", "reconciled": True, "summary": str(ah09.resolve())}
    attempts["AH-10"] = {"status": "safety-classified", "reconciled": True, "evidence": str(ah10.resolve())}
    payload = {"schema_version": 2, "attempts": attempts}
    temporary = runtime_root / "state.json.tmp"
    temporary.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")
    os.replace(temporary, runtime_root / "state.json")
    return payload


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime-root", type=Path, required=True)
    parser.add_argument("--recovery-root", type=Path, required=True)
    parser.add_argument("--quality-root", type=Path, required=True)
    parser.add_argument("--adjudication", type=Path, required=True)
    parser.add_argument("--workbook", type=Path, required=True)
    args = parser.parse_args()
    finalize_state(args.runtime_root, args.recovery_root, args.quality_root,
                   args.adjudication, args.workbook.read_text(encoding="utf-8-sig"))
    print("runtime state finalized: 10/10 terminal rows")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
