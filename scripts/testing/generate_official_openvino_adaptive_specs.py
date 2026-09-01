"""CLI for generating adaptive comparison worker specs without inference."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.campaigns.openvino.adaptive_campaign_spec import (
    generate_adaptive_format_comparison_specs,
)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--build-root", type=Path, required=True)
    parser.add_argument("--artifact-inventory", type=Path, required=True)
    parser.add_argument("--cache-root", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    try:
        result = generate_adaptive_format_comparison_specs(
            matrix_path=args.matrix,
            build_root=args.build_root,
            artifact_inventory_path=args.artifact_inventory,
            cache_root=args.cache_root,
            output_root=args.output_root,
        )
    except ValueError as error:
        parser.error(str(error))
    print(json.dumps(result, sort_keys=True, allow_nan=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
