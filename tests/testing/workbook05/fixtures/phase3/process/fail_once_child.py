"""Fail once per workspace, then complete deterministically."""

from __future__ import annotations

import argparse
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workspace", type=Path, required=True)
    args = parser.parse_args()
    args.workspace.mkdir(parents=True, exist_ok=True)
    marker = args.workspace / "first-attempt-observed.txt"
    if not marker.exists():
        marker.write_text("failed once\n", encoding="utf-8")
        return 75
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
