"""Run, preflight, resume, or inspect the guarded OpenVINO boundary campaign."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.measure_official_openvino import run_measurement_sequence
from scripts.testing.campaigns.openvino.adaptive_quality import (
    capture_isolated_quality_campaign,
)
from scripts.testing.campaigns.openvino.format_boundary import (
    BoundaryCampaignConfig,
    boundary_campaign_exit_code,
    durable_row_lines,
    load_boundary_status,
    run_boundary_preflight,
    run_boundary_campaign,
)
from scripts.testing.campaigns.openvino.owned_process_guard import available_ram_bytes


def _repository_defaults() -> dict[str, Path]:
    matrix = (
        ROOT
        / "experiments/manifests/official-openvino/"
        "adaptive-format-comparison-matrix-v1.json"
    )
    metadata = json.loads(matrix.read_text(encoding="utf-8"))
    build_identity = metadata.get("build_identity")
    if not isinstance(build_identity, dict) or not isinstance(
        build_identity.get("path"), str
    ):
        raise ValueError("committed comparison matrix build identity is missing")
    provenance = (ROOT / build_identity["path"]).resolve()
    build = json.loads(provenance.read_text(encoding="utf-8"))
    configure = build.get("configure")
    if not isinstance(configure, dict) or not isinstance(
        configure.get("build_directory"), str
    ):
        raise ValueError("committed build provenance configure identity is missing")
    python = ROOT / ".venv-official-openvino-turboquant-py313/Scripts/python.exe"
    site_packages = ROOT / ".venv-official-openvino-turboquant-py313/Lib/site-packages"
    prompt = (
        ROOT
        / "experiments/granite_turboquant_intel/prompts/"
        "compact-feasibility-prompt-set-v2.json"
    )
    return {
        "manifest": ROOT / "experiments/manifests/official-openvino/format-boundary-matrix-v1.json",
        "campaign_root": ROOT / "experiments/raw-results/openvino-format-boundary/2026-08-02",
        "comparison_matrix_path": matrix,
        "build_root": Path(configure["build_directory"]),
        "python_executable": python,
        "python_site_packages": site_packages,
        "openvino_libraries": site_packages / "openvino/libs",
        "sampler_script": ROOT / "scripts/testing/collect_openvino_runtime_utilization.ps1",
        "prompt_set": prompt,
        "rendered_root": prompt.parent / "rendered-v2",
        "rubric": ROOT / "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json",
    }


def _parser() -> argparse.ArgumentParser:
    defaults = _repository_defaults()
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, default=defaults["manifest"])
    parser.add_argument("--campaign-root", type=Path, default=defaults["campaign_root"])
    parser.add_argument("--matrix", dest="comparison_matrix_path", type=Path, default=defaults["comparison_matrix_path"])
    parser.add_argument("--build-root", type=Path, default=defaults["build_root"])
    parser.add_argument("--python-executable", type=Path, default=defaults["python_executable"])
    parser.add_argument("--python-site-packages", type=Path, default=defaults["python_site_packages"])
    parser.add_argument("--openvino-libraries", type=Path, default=defaults["openvino_libraries"])
    parser.add_argument("--sampler-script", type=Path, default=defaults["sampler_script"])
    parser.add_argument("--prompt-set", type=Path, default=defaults["prompt_set"])
    parser.add_argument("--rendered-root", type=Path, default=defaults["rendered_root"])
    parser.add_argument("--rubric", type=Path, default=defaults["rubric"])
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--preflight", action="store_true")
    mode.add_argument("--resume", action="store_true")
    mode.add_argument("--status", action="store_true")
    return parser


def _config(args: argparse.Namespace) -> BoundaryCampaignConfig:
    campaign_root = Path(args.campaign_root)
    has_state = (campaign_root / "campaign-state.json").is_file()
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
        resume=bool(args.status or ((args.resume or args.preflight) and has_state)),
    )


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    config = _config(args)
    try:
        if args.preflight:
            receipt = run_boundary_preflight(config)
            print(json.dumps(receipt, sort_keys=True, separators=(",", ":")))
            return 0
        if args.status:
            state = load_boundary_status(config)
        else:
            run_boundary_preflight(config)
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
