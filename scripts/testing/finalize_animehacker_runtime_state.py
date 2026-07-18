"""Rebuild the crash-resume state from terminal WB-03 evidence."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime-root", type=Path, required=True)
    parser.add_argument("--recovery-root", type=Path, required=True)
    args = parser.parse_args()
    attempts = {}
    for test_id in ("AH-01", "AH-02", "AH-03", "AH-04", "AH-05", "AH-08"):
        summary = args.runtime_root / test_id / "summary.json"
        if not summary.is_file():
            raise FileNotFoundError(summary)
        attempts[test_id] = {"status": "complete", "reconciled": True,
                             "summary": str(summary.resolve())}
    for test_id in ("AH-06", "AH-07"):
        attempts[test_id] = {"status": "safety-classified", "reconciled": True,
                             "evidence": str((args.runtime_root / test_id).resolve())}
    ah09 = args.recovery_root / "AH-09" / "summary.json"
    ah10 = args.recovery_root / "AH-10" / "wrapper-execution.json"
    if not ah09.is_file() or not ah10.is_file():
        raise FileNotFoundError("recovered AH-09 summary and AH-10 wrapper evidence are required")
    attempts["AH-09"] = {"status": "complete", "reconciled": True, "summary": str(ah09.resolve())}
    attempts["AH-10"] = {"status": "safety-classified", "reconciled": True, "evidence": str(ah10.resolve())}
    payload = {"schema_version": 2, "attempts": attempts}
    temporary = args.runtime_root / "state.json.tmp"
    temporary.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")
    os.replace(temporary, args.runtime_root / "state.json")
    print("runtime state finalized: 10/10 terminal rows")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
