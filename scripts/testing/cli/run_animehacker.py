"""Run supported animehacker testing commands."""

from __future__ import annotations

from collections.abc import Sequence

from . import build_mode_parser, forwarded_args, invoke_legacy_main


run_animehacker_retest_main = "scripts.testing.run_animehacker_retest:main"
run_animehacker_quality_main = "scripts.testing.run_animehacker_quality:main"
run_animehacker_large_host_main = "scripts.testing.run_animehacker_large_host:main"

MODE_HANDLERS = {
    "retest": "run_animehacker_retest_main",
    "quality": "run_animehacker_quality_main",
    "large-host": "run_animehacker_large_host_main",
}


def build_parser():
    return build_mode_parser(__doc__, MODE_HANDLERS)


def main(argv: Sequence[str] | None = None) -> int:
    parsed = build_parser().parse_args(argv)
    return invoke_legacy_main(
        globals()[MODE_HANDLERS[parsed.mode]],
        forwarded_args(parsed),
    )


if __name__ == "__main__":
    raise SystemExit(main())
