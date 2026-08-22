"""Bounded normal child used by the Workbook 05 process-harness tests."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workspace", type=Path, required=True)
    parser.add_argument("--echo-arg", action="append", default=[])
    args = parser.parse_args()
    args.workspace.mkdir(parents=True, exist_ok=True)
    (args.workspace / "raw.txt").write_text("NORMAL_OUTPUT\n", encoding="utf-8")
    (args.workspace / "received-arguments.json").write_text(
        json.dumps({"values": args.echo_arg}, indent=2) + "\n",
        encoding="utf-8",
    )
    (args.workspace / "heartbeat.txt").write_text("alive\n", encoding="utf-8")
    print("normal-child-stdout")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
