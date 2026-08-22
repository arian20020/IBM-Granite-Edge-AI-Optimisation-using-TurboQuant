"""Write invalid JSONL while returning zero for validator-negative tests."""

from __future__ import annotations

import argparse
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workspace", type=Path, required=True)
    args = parser.parse_args()
    args.workspace.mkdir(parents=True, exist_ok=True)
    (args.workspace / "events.jsonl").write_text("{not-json}\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
