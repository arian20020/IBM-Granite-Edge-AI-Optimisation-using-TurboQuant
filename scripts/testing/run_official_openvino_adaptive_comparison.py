"""CLI for the guarded resumable adaptive OpenVINO comparison."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Callable, Mapping

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.adaptive_campaign import (
    AdaptiveCampaignConfig,
    CONTEXTS,
    campaign_status,
    preflight_adaptive_campaign,
    run_adaptive_campaign,
)
from scripts.testing.official_openvino.owned_process_guard import (
    available_ram_bytes,
)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--spec-root", type=Path, required=True)
    parser.add_argument("--campaign-root", type=Path, required=True)
    parser.add_argument("--build-root", type=Path, required=True)
    parser.add_argument("--build-provenance", type=Path, required=True)
    parser.add_argument("--python-executable", type=Path, required=True)
    parser.add_argument("--python-site-packages", type=Path, required=True)
    parser.add_argument("--openvino-libraries", type=Path, required=True)
    parser.add_argument("--sampler-script", type=Path, required=True)
    parser.add_argument("--reference-boundary-index", type=Path)
    parser.add_argument("--max-context", type=int, choices=CONTEXTS, default=8192)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--publish-checkpoints", action="store_true")
    parser.add_argument("--preflight-only", action="store_true")
    return parser


def _quality_callback() -> Callable[..., Mapping[str, Any]]:
    try:
        from scripts.testing.official_openvino.adaptive_quality import (
            capture_isolated_quality_campaign,
        )
    except ImportError as error:  # Task 5 is deliberately a later dependency.
        raise RuntimeError("adaptive quality callback is not installed") from error
    return capture_isolated_quality_campaign


def _publication_callback() -> Callable[[Path, bool], Mapping[str, Any]]:
    try:
        from scripts.testing.publish_official_openvino_comparison import (
            publish_reconciled_checkpoint,
        )
    except ImportError as error:  # Task 9 is deliberately a later dependency.
        raise RuntimeError("comparison publication callback is not installed") from error
    return publish_reconciled_checkpoint


def _config(args: argparse.Namespace) -> AdaptiveCampaignConfig:
    return AdaptiveCampaignConfig(
        matrix_path=args.matrix,
        spec_root=args.spec_root,
        campaign_root=args.campaign_root,
        build_root=args.build_root,
        build_provenance_path=args.build_provenance,
        python_executable=args.python_executable,
        python_site_packages=args.python_site_packages,
        openvino_libraries=args.openvino_libraries,
        sampler_script=args.sampler_script,
        reference_boundary_index=args.reference_boundary_index,
        max_context=args.max_context,
    )


def main(argv: list[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    config = _config(args)
    state_path = Path(config.campaign_root).resolve() / "adaptive-campaign-state.json"
    if state_path.exists() and not args.resume:
        parser.error("existing adaptive campaign state requires --resume")
    if args.resume and not state_path.is_file():
        parser.error("--resume requires an existing adaptive campaign state")

    if args.preflight_only:
        state = preflight_adaptive_campaign(
            config,
            available_ram=available_ram_bytes,
        )
    else:
        quality = _quality_callback()
        publication = _publication_callback() if args.publish_checkpoints else None
        state = run_adaptive_campaign(
            config,
            run_quality=quality,
            publish_checkpoint=publication,
            available_ram=available_ram_bytes,
        )
    print(
        json.dumps(
            campaign_status(config, state),
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
