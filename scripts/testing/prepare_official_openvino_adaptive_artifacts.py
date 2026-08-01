"""Build a hash-bound WB-04 adaptive artifact inventory without inference."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.testing.official_openvino.artifact_inventory import prepare_adaptive_artifacts


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--u4-manifest", type=Path, required=True)
    parser.add_argument("--u8-manifest", type=Path, required=True)
    parser.add_argument("--fp16-manifest", type=Path)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--launch-reserve-mib", type=int, default=4096)
    parser.add_argument("--emergency-floor-mib", type=int, default=2048)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        outcome = prepare_adaptive_artifacts(
            u4_manifest=args.u4_manifest,
            u8_manifest=args.u8_manifest,
            fp16_manifest=args.fp16_manifest,
            output_root=args.output_root,
            launch_reserve_mib=args.launch_reserve_mib,
            emergency_floor_mib=args.emergency_floor_mib,
        )
    except (OSError, RuntimeError, ValueError) as error:
        print(f"artifact inventory failed: {error}", file=sys.stderr)
        return 1
    print(outcome.inventory_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
