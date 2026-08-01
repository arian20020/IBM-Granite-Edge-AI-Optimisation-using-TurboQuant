"""Build an explicit reusable-boundary index from one historical root."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.adaptive_campaign import (
    build_boundary_index,
)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--spec-root", type=Path, required=True)
    parser.add_argument("--historical-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    result = build_boundary_index(
        matrix_path=args.matrix,
        spec_root=args.spec_root,
        historical_root=args.historical_root,
        output_path=args.output,
    )
    print(
        json.dumps(
            {
                "output_path": str(Path(args.output).resolve()),
                "boundary_count": len(result["boundaries"]),
            },
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
