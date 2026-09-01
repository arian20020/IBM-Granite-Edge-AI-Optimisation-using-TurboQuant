"""Run supported AtomicBot testing commands."""

from __future__ import annotations

from collections.abc import Sequence

from . import build_mode_parser, forwarded_args, invoke_legacy_main


run_atomicbot_retest_main = "scripts.testing.run_atomicbot_retest:main"
run_atomicbot_server_metrics_main = "scripts.testing.run_atomicbot_server_metrics:main"
run_atomicbot_full_quality_main = "scripts.testing.run_atomicbot_full_quality:main"

MODE_HANDLERS = {
    "retest": "run_atomicbot_retest_main",
    "server-metrics": "run_atomicbot_server_metrics_main",
    "full-quality": "run_atomicbot_full_quality_main",
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
