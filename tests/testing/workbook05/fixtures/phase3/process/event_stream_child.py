"""Emit a deterministic first-token event stream without loading a model."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workspace", type=Path, required=True)
    args = parser.parse_args()
    args.workspace.mkdir(parents=True, exist_ok=True)
    events = [
        {"event": "started", "sequence": 1},
        {"event": "first_token", "sequence": 2},
        {"event": "token", "sequence": 3, "token_index": 1},
        {"event": "completed", "sequence": 4},
    ]
    (args.workspace / "events.jsonl").write_text(
        "".join(json.dumps(event, sort_keys=True) + "\n" for event in events),
        encoding="utf-8",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
