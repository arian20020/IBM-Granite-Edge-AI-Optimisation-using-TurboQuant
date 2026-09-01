"""Generate create-only WB-04 scalar semantic rejection evidence."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.testing.campaigns.openvino.scalar_semantic_rejections import (  # noqa: E402
    generate_scalar_semantic_rejection_evidence,
    validate_scalar_semantic_rejection_evidence,
    write_scalar_semantic_rejection_evidence,
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--u8-spec", type=Path, required=True)
    parser.add_argument("--u8-attempt", type=Path, required=True)
    parser.add_argument("--u4-spec", type=Path, required=True)
    parser.add_argument("--u4-attempt", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    paths = {
        "u8_spec_path": args.u8_spec,
        "u8_attempt_path": args.u8_attempt,
        "u4_spec_path": args.u4_spec,
        "u4_attempt_path": args.u4_attempt,
    }
    payload = generate_scalar_semantic_rejection_evidence(args.matrix, **paths)
    write_scalar_semantic_rejection_evidence(
        args.output, payload, matrix_path=args.matrix, **paths
    )
    reloaded = json.loads(args.output.resolve().read_text(encoding="utf-8"))
    summary = validate_scalar_semantic_rejection_evidence(
        reloaded, args.matrix, **paths
    )
    print(json.dumps(summary, ensure_ascii=False, separators=(",", ":"), sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
