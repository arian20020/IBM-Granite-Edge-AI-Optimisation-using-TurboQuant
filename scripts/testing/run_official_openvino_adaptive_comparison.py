"""CLI for the guarded resumable adaptive OpenVINO comparison."""

from __future__ import annotations

import argparse
import base64
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

from scripts.testing.campaigns.openvino.adaptive_campaign import (
    AdaptiveCampaignConfig,
    CONTEXTS,
    START_RESERVE_MIB,
    campaign_status,
    preflight_adaptive_campaign,
    run_adaptive_campaign,
    validate_adaptive_campaign_snapshot,
)
from scripts.testing.campaigns.openvino.owned_process_guard import (
    available_ram_bytes,
)
from scripts.testing.campaigns.openvino.comparison_reconcile import (
    build_comparison_release_input,
    load_comparison_release_input,
)
from scripts.testing.publish_official_openvino_comparison import (
    publish_reconciled_checkpoint,
)


PREFLIGHT_REPORT_SCHEMA = "official-openvino-adaptive-preflight-report/v1"
JOB_PROBE_SCHEMA = "official-openvino-adaptive-job-probe/v1"
MIB = 1024**2


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
        from scripts.testing.campaigns.openvino.adaptive_quality import (
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
            prefix=".stage-",
            suffix=".tmp",
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
        state_tag = base64.urlsafe_b64encode(
            bytes.fromhex(state_sha256)
        ).decode("ascii").rstrip("=")
        checkpoint_directory = campaign_root / "checkpoints"
        checkpoint_directory.mkdir(parents=True, exist_ok=True)
        state_snapshot = (
            campaign_root / f"s-{state_tag}.json"
        ).resolve()
        publish_immutable_bytes(state_snapshot, state_bytes, "campaign state snapshot")
        release_input = (
            checkpoint_directory
            / f"r-{state_tag}.json"
        ).resolve()
        descriptor, staged_name = tempfile.mkstemp(
            dir=checkpoint_directory,
            prefix=".build-",
            suffix=".tmp",
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


def _reject_json_constant(value: str) -> None:
    raise ValueError(f"non-finite JSON number is forbidden: {value}")


def _object_without_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            raise ValueError(f"duplicate JSON key is forbidden: {key}")
        value[key] = item
    return value


def _load_json(path: Path, label: str) -> dict[str, Any]:
    source = Path(path).resolve()
    if not source.is_file():
        raise ValueError(f"{label} is missing: {source}")
    try:
        value = json.loads(
            source.read_text(encoding="utf-8-sig"),
            object_pairs_hook=_object_without_duplicates,
            parse_constant=_reject_json_constant,
        )
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
        raise ValueError(f"{label} is not canonical valid JSON: {source}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{label} must be a JSON object")
    return value


def _sha256_file(path: Path, label: str) -> str:
    source = Path(path).resolve()
    if not source.is_file():
        raise ValueError(f"{label} is missing: {source}")
    return hashlib.sha256(source.read_bytes()).hexdigest()


def _json_bytes(value: Mapping[str, Any]) -> bytes:
    return (
        json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        )
        + "\n"
    ).encode("utf-8")


def _campaign_child(root: Path, relative: object, label: str) -> Path:
    campaign_root = Path(root).resolve()
    if not isinstance(relative, (str, Path)):
        raise ValueError(f"{label} path is invalid")
    lexical = Path(relative)
    if lexical.is_absolute() or ".." in lexical.parts:
        raise ValueError(f"{label} path escapes campaign root")
    resolved = (campaign_root / lexical).resolve()
    if campaign_root != resolved and campaign_root not in resolved.parents:
        raise ValueError(f"{label} path escapes campaign root")
    return resolved


def _preflight_report_path(config: AdaptiveCampaignConfig) -> Path:
    root = Path(config.campaign_root).resolve()
    return _campaign_child(
        root,
        Path("preflight") / "preflight-report.json",
        "preflight report",
    )


def _exact_report_path(value: object, expected: Path, label: str) -> Path:
    if not isinstance(value, str) or not value:
        raise ValueError(f"{label} path binding drift")
    actual = Path(value).resolve()
    if actual != Path(expected).resolve():
        raise ValueError(f"{label} path binding drift")
    return actual


def _preflight_report_payload(
    config: AdaptiveCampaignConfig,
    state: Mapping[str, Any],
    *,
    observed_available_ram_bytes: int,
) -> dict[str, Any]:
    if (
        isinstance(observed_available_ram_bytes, bool)
        or not isinstance(observed_available_ram_bytes, int)
    ):
        raise ValueError("observed available RAM must be an integer byte count")
    launch_reserve_bytes = START_RESERVE_MIB * MIB
    if observed_available_ram_bytes < launch_reserve_bytes:
        raise ValueError("observed available RAM is below the launch reserve")

    root = Path(config.campaign_root).resolve()
    state_path = _campaign_child(
        root,
        "adaptive-campaign-state.json",
        "campaign state",
    )
    disk_state = _load_json(state_path, "campaign state")
    validated_state = validate_adaptive_campaign_snapshot(config, disk_state)
    if dict(state) != disk_state or dict(state) != validated_state:
        raise ValueError("campaign state memory/disk binding drift")
    state_sha256 = _sha256_file(state_path, "campaign state")

    matrix = Path(config.matrix_path).resolve()
    spec_index = (Path(config.spec_root).resolve() / "spec-index.json").resolve()
    build_provenance = Path(config.build_provenance_path).resolve()
    reference_boundary = (
        Path(config.reference_boundary_index).resolve()
        if config.reference_boundary_index is not None
        else None
    )
    evidence = {
        "matrix": (matrix, _sha256_file(matrix, "matrix")),
        "spec index": (
            spec_index,
            _sha256_file(spec_index, "spec index"),
        ),
        "build provenance": (
            build_provenance,
            _sha256_file(build_provenance, "build provenance"),
        ),
    }
    if reference_boundary is not None:
        evidence["reference boundary index"] = (
            reference_boundary,
            _sha256_file(reference_boundary, "reference boundary index"),
        )

    bindings = disk_state.get("bindings")
    if not isinstance(bindings, Mapping):
        raise ValueError("campaign state bindings are missing")
    binding_fields = {
        "matrix": ("matrix_path", "matrix_sha256"),
        "spec index": ("spec_index_path", "spec_index_sha256"),
        "build provenance": (
            "build_provenance_path",
            "build_provenance_sha256",
        ),
    }
    for label, (path_field, hash_field) in binding_fields.items():
        path, digest = evidence[label]
        if bindings.get(path_field) != str(path) or bindings.get(hash_field) != digest:
            raise ValueError(f"campaign state {label} binding drift")
    expected_reference_path = str(reference_boundary) if reference_boundary else None
    expected_reference_hash = (
        evidence["reference boundary index"][1] if reference_boundary else None
    )
    if (
        bindings.get("reference_boundary_index_path") != expected_reference_path
        or bindings.get("reference_boundary_index_sha256")
        != expected_reference_hash
    ):
        raise ValueError("campaign state reference boundary index binding drift")

    safety_probes = disk_state.get("safety_probes")
    if not isinstance(safety_probes, list) or not safety_probes:
        raise ValueError("zero-survivor safety probe is missing from campaign state")
    probe_entry = safety_probes[-1]
    if not isinstance(probe_entry, Mapping) or probe_entry.get("stage") != "preflight":
        raise ValueError("latest campaign safety probe is not the preflight proof")
    probe_path = _campaign_child(
        root,
        probe_entry.get("receipt_path"),
        "safety probe",
    )
    probe_sha256 = _sha256_file(probe_path, "safety probe")
    if probe_entry.get("receipt_sha256") != probe_sha256:
        raise ValueError("campaign state safety probe hash binding drift")
    probe = _load_json(probe_path, "safety probe")
    active_pids = probe.get("active_pids")
    if (
        probe.get("schema") != JOB_PROBE_SCHEMA
        or probe.get("stage") != "preflight"
        or probe.get("query_ok") is not True
        or active_pids != []
        or probe_entry.get("query_ok") is not True
        or probe_entry.get("active_pids") != []
    ):
        raise ValueError("preflight report requires proof of zero owned survivors")

    status = campaign_status(config, disk_state)
    return {
        "schema": PREFLIGHT_REPORT_SCHEMA,
        "matrix_path": str(matrix),
        "matrix_sha256": evidence["matrix"][1],
        "spec_index_path": str(spec_index),
        "spec_index_sha256": evidence["spec index"][1],
        "reference_boundary_index_path": expected_reference_path,
        "reference_boundary_index_sha256": expected_reference_hash,
        "build_provenance_path": str(build_provenance),
        "build_provenance_sha256": evidence["build provenance"][1],
        "campaign_state_path": str(state_path),
        "campaign_state_sha256": state_sha256,
        "observed_available_ram_bytes": observed_available_ram_bytes,
        "launch_reserve_bytes": launch_reserve_bytes,
        "next_eligible_step": status["next_eligible_step"],
        "campaign_halt": status["campaign_halt"],
        "owned_survivor_count": 0,
        "zero_survivor_safety_probe_path": str(probe_path),
        "zero_survivor_safety_probe_sha256": probe_sha256,
    }


def _validate_preflight_report(
    config: AdaptiveCampaignConfig,
    report_path: Path | None = None,
) -> dict[str, Any]:
    expected_report_path = _preflight_report_path(config)
    actual_report_path = (
        expected_report_path if report_path is None else Path(report_path).resolve()
    )
    if actual_report_path != expected_report_path:
        root = Path(config.campaign_root).resolve()
        if root != actual_report_path and root not in actual_report_path.parents:
            raise ValueError("preflight report path escapes campaign root")
        raise ValueError("preflight report path binding drift")
    report = _load_json(actual_report_path, "preflight report")
    if report.get("schema") != PREFLIGHT_REPORT_SCHEMA:
        raise ValueError("preflight report schema drift")

    root = Path(config.campaign_root).resolve()
    state_path = _campaign_child(
        root,
        "adaptive-campaign-state.json",
        "campaign state",
    )
    _exact_report_path(report.get("campaign_state_path"), state_path, "campaign state")
    state_hash = _sha256_file(state_path, "campaign state")
    if report.get("campaign_state_sha256") != state_hash:
        raise ValueError("campaign state hash binding drift")

    probe_value = report.get("zero_survivor_safety_probe_path")
    if not isinstance(probe_value, str) or not probe_value:
        raise ValueError("safety probe path binding drift")
    probe_path = Path(probe_value).resolve()
    if root != probe_path and root not in probe_path.parents:
        raise ValueError("safety probe path escapes campaign root")

    expected_paths = {
        "matrix": Path(config.matrix_path).resolve(),
        "spec index": (
            Path(config.spec_root).resolve() / "spec-index.json"
        ).resolve(),
        "build provenance": Path(config.build_provenance_path).resolve(),
    }
    if config.reference_boundary_index is not None:
        expected_paths["reference boundary index"] = Path(
            config.reference_boundary_index
        ).resolve()
    path_fields = {
        "matrix": "matrix_path",
        "spec index": "spec_index_path",
        "build provenance": "build_provenance_path",
        "reference boundary index": "reference_boundary_index_path",
    }
    for label, expected_path in expected_paths.items():
        _exact_report_path(report.get(path_fields[label]), expected_path, label)

    observed = report.get("observed_available_ram_bytes")
    state = _load_json(state_path, "campaign state")
    expected = _preflight_report_payload(
        config,
        state,
        observed_available_ram_bytes=observed,
    )
    hash_labels = {
        "matrix_sha256": "matrix",
        "spec_index_sha256": "spec index",
        "reference_boundary_index_sha256": "reference boundary index",
        "build_provenance_sha256": "build provenance",
        "campaign_state_sha256": "campaign state",
        "zero_survivor_safety_probe_sha256": "safety probe",
    }
    for field, label in hash_labels.items():
        if report.get(field) != expected.get(field):
            raise ValueError(f"{label} hash binding drift")
    if report != expected:
        raise ValueError("preflight report is inconsistent with resulting campaign state")
    return report


def _publish_preflight_report(path: Path, report: Mapping[str, Any]) -> bool:
    destination = Path(path).resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    value = _json_bytes(report)
    descriptor, staged_name = tempfile.mkstemp(
        dir=destination.parent,
        prefix=f".{destination.name}.",
        suffix=".stage",
    )
    staged = Path(staged_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(value)
            handle.flush()
            os.fsync(handle.fileno())
        created = False
        try:
            os.link(staged, destination)
            created = True
        except FileExistsError:
            if destination.read_bytes() != value:
                raise ValueError("conflicting existing preflight report")
        if destination.read_bytes() != value:
            raise RuntimeError("atomic preflight report publication failed")
        return created
    finally:
        staged.unlink(missing_ok=True)


def _write_preflight_report(
    config: AdaptiveCampaignConfig,
    state: Mapping[str, Any],
    *,
    observed_available_ram_bytes: int,
) -> dict[str, Any]:
    report = _preflight_report_payload(
        config,
        state,
        observed_available_ram_bytes=observed_available_ram_bytes,
    )
    path = _preflight_report_path(config)
    expected_bytes = _json_bytes(report)
    created = False
    try:
        created = _publish_preflight_report(path, report)
        return _validate_preflight_report(config, path)
    except BaseException as primary_error:
        if created:
            try:
                if path.is_file() and path.read_bytes() == expected_bytes:
                    path.unlink()
            except BaseException as cleanup_error:
                raise BaseExceptionGroup(
                    "preflight report validation and cleanup both failed",
                    [primary_error, cleanup_error],
                ) from None
        raise


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
        report_path = _preflight_report_path(config)

        def reuse_preflight() -> Mapping[str, Any] | None:
            if not report_path.exists():
                return None
            try:
                _validate_preflight_report(config, report_path)
                existing_state = _load_json(state_path, "campaign state")
                return validate_adaptive_campaign_snapshot(config, existing_state)
            except ValueError as error:
                raise ValueError("conflicting existing preflight report") from error

        def finalize_preflight(
            finalized_state: Mapping[str, Any],
            observed_available_ram_bytes: int,
        ) -> None:
            _write_preflight_report(
                config,
                finalized_state,
                observed_available_ram_bytes=observed_available_ram_bytes,
            )

        state = preflight_adaptive_campaign(
            config,
            available_ram=available_ram_bytes,
            reuse_preflight=reuse_preflight,
            finalize_preflight=finalize_preflight,
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
