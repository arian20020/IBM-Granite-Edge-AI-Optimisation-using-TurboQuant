"""Run supported upstream llama.cpp measurement commands."""

from __future__ import annotations

from collections.abc import Sequence

from . import build_mode_parser, forwarded_args, invoke_legacy_main


measure_run_main = "scripts.testing.campaigns.llama_cpp.measure_run:main"
measure_server_main = "scripts.testing.campaigns.llama_cpp.measure_server:main"

MODE_HANDLERS = {
    "measure-run": "measure_run_main",
    "measure-server": "measure_server_main",
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
