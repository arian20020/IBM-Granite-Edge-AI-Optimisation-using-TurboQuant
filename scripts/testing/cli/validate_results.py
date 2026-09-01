"""Validate an existing final-results collection without publishing receipts."""

from __future__ import annotations

from collections.abc import Sequence

from . import build_results


def build_parser():
    return build_results.build_parser(include_validate_only=False)


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    forwarded = [
        "--route",
        args.route,
        "--output-root",
        str(args.output_root),
        "--validate-only",
    ]
    return build_results.main(forwarded)


if __name__ == "__main__":
    raise SystemExit(main())
