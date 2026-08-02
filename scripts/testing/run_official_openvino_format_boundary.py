"""Run the serial, guarded OpenVINO format-boundary campaign.

The CLI deliberately performs no path guessing for mutable row specifications:
WB-04 publishes one immutable measurement spec and one immutable quality
recovery input per boundary row under ``--spec-root``.  Build/environment
inputs are explicit so the command line is itself reproducible evidence.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Mapping


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.measure_official_openvino import run_measurement_sequence
from scripts.testing.official_openvino.adaptive_quality import (
    capture_isolated_quality_campaign,
)
from scripts.testing.official_openvino.format_boundary import (
    BoundaryCampaignConfig,
    run_boundary_campaign,
)
from scripts.testing.official_openvino.owned_process_guard import available_ram_bytes


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--campaign-root", type=Path, required=True)
    parser.add_argument("--prompt-set", type=Path, required=True)
    parser.add_argument("--rubric", type=Path, required=True)
    parser.add_argument("--spec-root", type=Path, required=True)
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--build-root", type=Path, required=True)
    parser.add_argument("--build-provenance", type=Path, required=True)
    parser.add_argument("--python-executable", type=Path, required=True)
    parser.add_argument("--python-site-packages", type=Path, required=True)
    parser.add_argument("--openvino-libraries", type=Path, required=True)
    parser.add_argument("--sampler-script", type=Path, required=True)
    parser.add_argument("--resume", action="store_true")
    return parser


def _file(path: Path, label: str) -> Path:
    source = Path(path).resolve()
    if not source.is_file():
        raise ValueError(f"{label} is missing: {source}")
    return source


def _directory(path: Path, label: str) -> Path:
    source = Path(path).resolve()
    if not source.is_dir():
        raise ValueError(f"{label} is missing: {source}")
    return source


def _load_recovery(path: Path) -> Mapping[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"quality recovery is invalid: {path}") from error
    if not isinstance(value, dict):
        raise ValueError(f"quality recovery is not an object: {path}")
    return value


def _callbacks(args: argparse.Namespace):
    spec_root = _directory(args.spec_root, "row specification root")
    matrix = _file(args.matrix, "retest matrix")
    build_root = _directory(args.build_root, "build root")
    provenance = _file(args.build_provenance, "build provenance")
    python = _file(args.python_executable, "Python executable")
    site_packages = _directory(args.python_site_packages, "Python site-packages")
    libraries = _directory(args.openvino_libraries, "OpenVINO libraries")
    sampler = _file(args.sampler_script, "sampler script")

    def measure(case, *, campaign_root: Path, timeout_seconds: float,
                launch_minimum_available_ram_mib: int,
                emergency_minimum_available_ram_mib: int, **_: Any) -> Mapping[str, Any]:
        spec = _file(spec_root / case.internal_id / "runtime-spec.json", "row runtime spec")
        if case.artifact_manifest_path is None:
            raise ValueError("boundary row has no artifact manifest")
        return run_measurement_sequence(
            spec_path=spec, campaign_root=campaign_root, matrix_path=matrix,
            artifact_manifest_path=_file(case.artifact_manifest_path, "artifact manifest"),
            build_provenance_path=provenance, build_root=build_root, repo_root=ROOT,
            python_executable=python, python_site_packages=site_packages,
            openvino_libraries=libraries, sampler_script=sampler,
            timeout_seconds=timeout_seconds,
            launch_minimum_available_ram_mib=launch_minimum_available_ram_mib,
            emergency_minimum_available_ram_mib=emergency_minimum_available_ram_mib,
        )

    def quality(case, _runtime: Mapping[str, Any], *, campaign_root: Path,
                resume: bool, **_: Any) -> Mapping[str, Any]:
        recovery = _load_recovery(spec_root / case.internal_id / "quality-recovery.json")
        if Path(str(recovery.get("output_root", ""))).resolve() != campaign_root.resolve():
            raise ValueError("quality recovery output root does not match boundary row")
        return capture_isolated_quality_campaign(recovery, resume=resume)

    return measure, quality


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        config = BoundaryCampaignConfig(
            repository_root=ROOT, campaign_root=Path(args.campaign_root),
            manifest_path=_file(args.manifest, "boundary manifest"),
            prompt_set_path=_file(args.prompt_set, "compact prompt set"),
            rubric_path=_file(args.rubric, "quality rubric"), resume=args.resume,
        )
        measure, quality = _callbacks(args)
        state = run_boundary_campaign(
            config, run_measurement=measure, run_quality=quality,
            available_ram=available_ram_bytes,
        )
    except (OSError, ValueError, RuntimeError) as error:
        print(f"configuration error: {error}", file=sys.stderr)
        return 2
    for lane in ("cpu_lane", "gpu_lane"):
        value = state[lane]
        print(f"{lane}: {value['status']} accepted={value['accepted_count']}")
    reasons = {state[lane].get("reason_code") for lane in ("cpu_lane", "gpu_lane")}
    return 3 if any(reason and "cleanup" in reason for reason in reasons) else 0


if __name__ == "__main__":
    raise SystemExit(main())
