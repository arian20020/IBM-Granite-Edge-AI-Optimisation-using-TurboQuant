"""Run supported OpenVINO testing commands."""

from __future__ import annotations

from collections.abc import Sequence

from . import build_mode_parser, forwarded_args, invoke_legacy_main


run_official_openvino_retest_main = "scripts.testing.run_official_openvino_retest:main"
run_official_openvino_quality_main = "scripts.testing.run_official_openvino_quality:main"
run_openvino_reference_capability_main = "scripts.testing.run_openvino_reference_capability:main"
run_official_openvino_diagnostics_main = "scripts.testing.run_official_openvino_diagnostics:main"
run_official_openvino_adaptive_quality_main = "scripts.testing.run_official_openvino_adaptive_quality:main"
run_official_openvino_adaptive_comparison_main = "scripts.testing.run_official_openvino_adaptive_comparison:main"
run_official_openvino_format_boundary_main = "scripts.testing.run_official_openvino_format_boundary:main"

MODE_HANDLERS = {
    "retest": "run_official_openvino_retest_main",
    "quality": "run_official_openvino_quality_main",
    "reference-capability": "run_openvino_reference_capability_main",
    "diagnostics": "run_official_openvino_diagnostics_main",
    "adaptive-quality": "run_official_openvino_adaptive_quality_main",
    "adaptive-comparison": "run_official_openvino_adaptive_comparison_main",
    "format-boundary": "run_official_openvino_format_boundary_main",
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
