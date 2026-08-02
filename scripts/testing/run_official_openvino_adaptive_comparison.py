"""CLI for the guarded resumable adaptive OpenVINO comparison."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import tempfile
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
from scripts.testing.official_openvino.comparison_reconcile import (
    build_comparison_release_input,
    load_comparison_release_input,
)
from scripts.testing.publish_official_openvino_comparison import (
    publish_reconciled_checkpoint,
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
    parser.add_argument("--evidence-commit")
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


def _publication_callback(
    config: AdaptiveCampaignConfig,
    evidence_commit: str,
) -> Callable[[Path, bool], Mapping[str, Any]]:
    """Bind the mutable state callback to immutable Task 7 release inputs."""

    if (
        not isinstance(evidence_commit, str)
        or len(evidence_commit) != 40
        or any(character not in "0123456789abcdef" for character in evidence_commit)
    ):
        raise ValueError("evidence commit must be a 40-character lowercase Git SHA")
    campaign_root = Path(config.campaign_root).resolve()
    expected_state = campaign_root / "adaptive-campaign-state.json"

    def publish_immutable_bytes(path: Path, value: bytes, label: str) -> None:
        descriptor, staged_name = tempfile.mkstemp(
            dir=path.parent,
            prefix=f".{path.name}.",
            suffix=".stage",
        )
        staged = Path(staged_name)
        try:
            with os.fdopen(descriptor, "wb") as handle:
                handle.write(value)
                handle.flush()
                os.fsync(handle.fileno())
            try:
                os.link(staged, path)
            except FileExistsError:
                if path.read_bytes() != value:
                    raise ValueError(f"existing immutable {label} differs")
            if path.read_bytes() != value:
                raise RuntimeError(f"immutable {label} publication failed")
        finally:
            staged.unlink(missing_ok=True)

    def publish(state_path: Path, final: bool) -> Mapping[str, Any]:
        actual_state = Path(state_path).resolve()
        if actual_state != expected_state:
            raise ValueError("checkpoint publisher requires the exact campaign state path")
        if not actual_state.is_file():
            raise ValueError("exact campaign state file is missing")
        if not isinstance(final, bool):
            raise ValueError("checkpoint final flag must be boolean")
        state_bytes = actual_state.read_bytes()
        state_sha256 = hashlib.sha256(state_bytes).hexdigest()
        state_snapshot = (
            campaign_root / f"adaptive-campaign-state-{state_sha256}.json"
        ).resolve()
        publish_immutable_bytes(state_snapshot, state_bytes, "campaign state snapshot")
        release_directory = campaign_root / "release-inputs"
        release_directory.mkdir(parents=True, exist_ok=True)
        release_input = (
            release_directory
            / f"comparison-release-input-{state_sha256}.json"
        ).resolve()
        descriptor, staged_name = tempfile.mkstemp(
            dir=release_directory,
            prefix=f".{release_input.name}.",
            suffix=".stage",
        )
        os.close(descriptor)
        staged = Path(staged_name)
        staged.unlink()
        try:
            build_comparison_release_input(
                config.matrix_path,
                state_snapshot,
                staged,
            )
            staged_bytes = staged.read_bytes()
            publish_immutable_bytes(
                release_input,
                staged_bytes,
                "comparison release input",
            )
            load_comparison_release_input(release_input)
        finally:
            staged.unlink(missing_ok=True)
        return publish_reconciled_checkpoint(
            release_input,
            repo_root=ROOT,
            require_complete=final,
            evidence_commit=evidence_commit,
        )

    return publish


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
    if args.publish_checkpoints and args.evidence_commit is None:
        parser.error("--publish-checkpoints requires --evidence-commit")
    if not args.publish_checkpoints and args.evidence_commit is not None:
        parser.error("--evidence-commit requires --publish-checkpoints")
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
        publication = (
            _publication_callback(config, args.evidence_commit)
            if args.publish_checkpoints
            else None
        )
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
