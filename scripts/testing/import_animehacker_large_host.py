"""Validate a completed large-host WB-03 evidence package before workbook import."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import asdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.campaigns.animehacker.large_host import ALLOWED_TEST_IDS, validate_terminal_row


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def validate_run(run_root: Path, selected: tuple[str, ...]):
    manifest = read_json(run_root / "manifest.json")
    adjudications = read_json(run_root / "quality-adjudications.json")
    rows = []
    for test_id in selected:
        runtime = read_json(run_root / "runtime" / test_id / "summary.json")
        adjudicated = adjudications[test_id]["prompts"]
        quality = [dict(adjudicated[f"P{i}"]["raw"],
                        score=adjudicated[f"P{i}"]["final_score"]) for i in range(1, 7)]
        cleanup = read_json(run_root / "cleanup" / f"{test_id}.json")
        rows.append(validate_terminal_row(test_id, runtime, quality, manifest, cleanup))
    return rows


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-root", type=Path, required=True)
    parser.add_argument("--only", action="append", required=True, choices=sorted(ALLOWED_TEST_IDS))
    args = parser.parse_args()
    rows = validate_run(args.run_root, tuple(args.only))
    output = args.run_root / "import-ready.json"
    output.write_text(json.dumps([asdict(row) for row in rows], indent=2), encoding="utf-8")
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
