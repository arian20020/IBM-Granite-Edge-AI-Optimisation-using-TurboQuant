"""Build and validate immutable adaptive-comparison release evidence."""

from __future__ import annotations

import argparse
import json
import os
import sys
import uuid
from collections.abc import Sequence
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.comparison_reconcile import (
    build_comparison_release_input,
    build_quality_capture_index,
    load_comparison_release_input,
    reconcile_comparison_release,
    validate_closed_campaign,
    validate_complete_release,
)


def _read_json(path: Path, label: str) -> dict[str, object]:
    try:
        value = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise RuntimeError(f"published {label} is unreadable") from error
    if not isinstance(value, dict):
        raise RuntimeError(f"published {label} is not an object")
    return value


def _publish_fresh(staged: Path, destination: Path, *, label: str) -> None:
    """Publish one immutable file without replacing an existing authority."""

    if destination.exists():
        raise FileExistsError(f"{label} destination already exists: {destination}")
    try:
        os.link(staged, destination)
    except FileExistsError as error:
        raise FileExistsError(
            f"{label} destination was claimed during publication: {destination}"
        ) from error
    if destination.read_bytes() != staged.read_bytes():
        raise RuntimeError(f"published {label} bytes do not match its staged evidence")


def _parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build hash-bound adaptive OpenVINO comparison release evidence"
    )
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--campaign-state", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--quality-capture-index", type=Path)
    required = parser.add_mutually_exclusive_group()
    required.add_argument("--require-ladder-closed", action="store_true")
    required.add_argument("--require-complete", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    output = Path(args.output).resolve()
    capture_output = (
        Path(args.quality_capture_index).resolve()
        if args.quality_capture_index is not None
        else None
    )
    if capture_output == output:
        raise ValueError("release input and quality capture index outputs must differ")
    destinations = [(output, "release input")]
    if capture_output is not None:
        destinations.append((capture_output, "quality capture index"))
    for destination, label in destinations:
        if destination.exists():
            raise FileExistsError(f"{label} destination already exists: {destination}")
    output.parent.mkdir(parents=True, exist_ok=True)
    if capture_output is not None:
        capture_output.parent.mkdir(parents=True, exist_ok=True)
    nonce = uuid.uuid4().hex
    staged_output = output.with_name(f".{output.name}.{nonce}.stage")
    staged_capture = (
        capture_output.with_name(f".{capture_output.name}.{nonce}.stage")
        if capture_output is not None
        else None
    )
    try:
        payload = build_comparison_release_input(
            args.matrix,
            args.campaign_state,
            staged_output,
        )
        captures_payload = (
            build_quality_capture_index(args.campaign_state, staged_capture)
            if staged_capture is not None
            else None
        )
        if args.require_ladder_closed or args.require_complete:
            release = reconcile_comparison_release(staged_output)
            if args.require_complete:
                validate_complete_release(release)
            else:
                validate_closed_campaign(release)
        if staged_capture is not None and capture_output is not None:
            _publish_fresh(staged_capture, capture_output, label="quality capture index")
            if _read_json(capture_output, "quality capture index") != captures_payload:
                raise RuntimeError("published quality capture index failed revalidation")
        _publish_fresh(staged_output, output, label="release input")
        if _read_json(output, "release input") != payload:
            raise RuntimeError("published release input failed byte revalidation")
        load_comparison_release_input(output)
    finally:
        for staged in (staged_output, staged_capture):
            if staged is not None and staged.exists():
                staged.unlink()
    print(
        json.dumps(
            {
                "schema": payload["schema"],
                "release_input_sha256": payload["release_input_sha256"],
                "step_count": len(payload["steps"]),
            },
            sort_keys=True,
            separators=(",", ":"),
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
