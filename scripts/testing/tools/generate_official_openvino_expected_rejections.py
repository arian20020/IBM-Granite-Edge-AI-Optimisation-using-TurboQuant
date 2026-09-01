"""Write and validate deterministic WB-04 expected-rejection evidence."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.testing.campaigns.openvino.expected_rejections import (  # noqa: E402
    generate_expected_rejection_evidence,
    validate_expected_rejection_evidence,
    write_expected_rejection_evidence,
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    payload = generate_expected_rejection_evidence(args.matrix)
    write_expected_rejection_evidence(args.output, payload)
    reloaded = json.loads(args.output.resolve().read_text(encoding="utf-8"))
    summary = validate_expected_rejection_evidence(reloaded, args.matrix)
    print(
        json.dumps(
            summary,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
