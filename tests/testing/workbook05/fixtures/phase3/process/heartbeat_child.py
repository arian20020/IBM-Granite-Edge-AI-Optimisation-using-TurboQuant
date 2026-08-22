"""Exercise heartbeat update and intentionally stale modes."""

from __future__ import annotations

import argparse
import os
import time
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workspace", type=Path, required=True)
    parser.add_argument("--mode", choices=("update", "stall"), required=True)
    parser.add_argument("--duration-seconds", type=float, default=1.0)
    args = parser.parse_args()
    args.workspace.mkdir(parents=True, exist_ok=True)
    heartbeat = args.workspace / "heartbeat.txt"
    heartbeat.write_text("0\n", encoding="utf-8")
    if args.mode == "stall":
        stale = time.time() - 3600
        os.utime(heartbeat, (stale, stale))

    deadline = time.monotonic() + max(0.0, args.duration_seconds)
    sequence = 0
    while time.monotonic() < deadline:
        if args.mode == "update":
            sequence += 1
            heartbeat.write_text(f"{sequence}\n", encoding="utf-8")
        time.sleep(0.1)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
