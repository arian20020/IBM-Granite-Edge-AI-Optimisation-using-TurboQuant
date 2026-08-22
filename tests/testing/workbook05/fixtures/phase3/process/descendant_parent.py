"""Create a bounded parent/descendant tree for Windows termination tests."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
from pathlib import Path


def child_mode() -> int:
    while True:
        time.sleep(1)


def parent_mode(workspace: Path, pid_file: Path) -> int:
    workspace.mkdir(parents=True, exist_ok=True)
    child = subprocess.Popen(
        [sys.executable, str(Path(__file__).resolve()), "--child"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    pid_file.write_text(
        json.dumps({"parent_pid": __import__("os").getpid(), "child_pid": child.pid})
        + "\n",
        encoding="utf-8",
    )
    try:
        while True:
            time.sleep(1)
    finally:
        child.terminate()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--child", action="store_true")
    parser.add_argument("--workspace", type=Path)
    parser.add_argument("--pid-file", type=Path)
    args = parser.parse_args()
    if args.child:
        return child_mode()
    if args.workspace is None or args.pid_file is None:
        parser.error("--workspace and --pid-file are required in parent mode")
    return parent_mode(args.workspace, args.pid_file)


if __name__ == "__main__":
    raise SystemExit(main())
