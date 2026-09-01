"""Command-line entry point for the controlled AtomicBot retest."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import asdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.campaigns.atomicbot.matrix import load_matrix
from scripts.testing.campaigns.atomicbot.runner import select_cases
from scripts.testing.campaigns.atomicbot.state import load_state


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--state", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--only", action="append")
    parser.add_argument("--from", dest="start_at")
    parser.add_argument("--skip", action="append")
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--replace-attempt", action="store_true")
    parser.add_argument("--pilot", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    state = load_state(args.state) if args.resume and args.state and args.state.exists() else None
    cases = select_cases(load_matrix(args.matrix), only=set(args.only) if args.only else None,
                         start_at=args.start_at, skip=set(args.skip or ()), resume_state=state)
    payload = {"matrix": str(args.matrix), "mode": "dry-run" if args.dry_run else "planned",
               "cases": [asdict(case) for case in cases]}
    print(json.dumps(payload, indent=2))
    if not args.dry_run:
        print("execution requires configured external paths; use --dry-run for inspection",
              file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
