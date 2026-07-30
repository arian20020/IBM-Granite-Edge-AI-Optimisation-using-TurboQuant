"""Generate formal U8 Granite 3B WB-04 worker-spec templates."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.campaign_spec import (
    generate_formal_u8_granite3b_specs,
)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", dest="matrix_path", type=Path, required=True)
    parser.add_argument("--build-root", type=Path, required=True)
    parser.add_argument("--model-path", type=Path, required=True)
    parser.add_argument("--u4-model-path", type=Path)
    parser.add_argument("--cache-root", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    try:
        result = generate_formal_u8_granite3b_specs(
            matrix_path=args.matrix_path,
            build_root=args.build_root,
            model_path=args.model_path,
            u4_model_path=args.u4_model_path,
            cache_root=args.cache_root,
            output_root=args.output_root,
        )
    except ValueError as error:
        parser.error(str(error))
    print(json.dumps(result, sort_keys=True, allow_nan=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
