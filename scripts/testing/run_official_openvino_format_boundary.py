"""Run, preflight, resume, or inspect the guarded OpenVINO boundary campaign."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.measure_official_openvino import run_measurement_sequence
from scripts.testing.official_openvino.adaptive_quality import (
    capture_isolated_quality_campaign,
)
from scripts.testing.official_openvino.format_boundary import (
    BoundaryCampaignConfig,
    boundary_campaign_exit_code,
    durable_row_lines,
    load_boundary_status,
    prepare_boundary_projection,
    run_boundary_campaign,
)
from scripts.testing.official_openvino.owned_process_guard import available_ram_bytes


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--campaign-root", type=Path, required=True)
    parser.add_argument("--matrix", dest="comparison_matrix_path", type=Path, required=True)
    parser.add_argument("--build-root", type=Path, required=True)
    parser.add_argument("--python-executable", type=Path, required=True)
    parser.add_argument("--python-site-packages", type=Path, required=True)
    parser.add_argument("--openvino-libraries", type=Path, required=True)
    parser.add_argument("--sampler-script", type=Path, required=True)
    parser.add_argument("--prompt-set", type=Path, required=True)
    parser.add_argument("--rendered-root", type=Path, required=True)
    parser.add_argument("--rubric", type=Path, required=True)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--preflight", action="store_true")
    mode.add_argument("--resume", action="store_true")
    mode.add_argument("--status", action="store_true")
    return parser


def _config(args: argparse.Namespace) -> BoundaryCampaignConfig:
    return BoundaryCampaignConfig(
        repository_root=ROOT,
        campaign_root=Path(args.campaign_root),
        manifest_path=Path(args.manifest),
        comparison_matrix_path=Path(args.comparison_matrix_path),
        build_root=Path(args.build_root),
        python_executable=Path(args.python_executable),
        python_site_packages=Path(args.python_site_packages),
        openvino_libraries=Path(args.openvino_libraries),
        sampler_script=Path(args.sampler_script),
        prompt_set_path=Path(args.prompt_set),
        rendered_root=Path(args.rendered_root),
        rubric_path=Path(args.rubric),
        resume=bool(args.resume or args.status),
    )


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    config = _config(args)
    try:
        if args.preflight:
            projection = prepare_boundary_projection(config)
            print(
                "preflight "
                f"projection-index={projection.projection_index.path} "
                f"sha256={projection.projection_index.sha256}"
            )
            return 0
        if args.status:
            state = load_boundary_status(config)
        else:
            state = run_boundary_campaign(
                config,
                run_measurement=run_measurement_sequence,
                run_quality=capture_isolated_quality_campaign,
                available_ram=available_ram_bytes,
            )
    except (OSError, TypeError, ValueError, RuntimeError) as error:
        print(f"configuration error: {error}", file=sys.stderr)
        return 2
    for line in durable_row_lines(config, state):
        print(line)
    return boundary_campaign_exit_code(state)


if __name__ == "__main__":
    raise SystemExit(main())
