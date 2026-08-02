"""CLI for isolated adaptive OpenVINO quality capture and recovery."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.adaptive_campaign import (
    CANDIDATE_ORDER,
    CONTEXTS,
    MAX_GUARDED_ATTEMPTS,
    RUNTIME_FLOOR_MIB,
    START_RESERVE_MIB,
    STATE_SCHEMA,
    _directory_sha256,
    _validate_state,
    build_ladder,
    save_state_atomically,
)
from scripts.testing.official_openvino import adaptive_quality
from scripts.testing.official_openvino.adaptive_quality import (
    capture_isolated_quality_campaign,
    quality_campaign_input_from_recovery,
)
from scripts.testing.official_openvino.guarded_build import run_guarded_command
from scripts.testing.official_openvino.quality_campaign import _strict_object
from scripts.testing.official_openvino.quality_campaign import (
    load_accepted_quality_campaign,
)


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _sha256_json(value: Any) -> str:
    return hashlib.sha256(
        json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        ).encode("utf-8")
    ).hexdigest()


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime-summary", type=Path)
    parser.add_argument("--matrix", type=Path)
    parser.add_argument("--prompt-set", type=Path)
    parser.add_argument("--rubric", type=Path)
    parser.add_argument("--model-path", type=Path)
    parser.add_argument("--build-root", type=Path)
    parser.add_argument("--python-executable", type=Path)
    parser.add_argument("--python-site-packages", type=Path)
    parser.add_argument("--openvino-libraries", type=Path)
    parser.add_argument("--sampler-script", type=Path)
    parser.add_argument("--output-root", type=Path)
    parser.add_argument("--timeout-seconds", type=float, default=1800.0)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--campaign-state", type=Path)
    parser.add_argument("--resume-quality-blocked", action="store_true")
    return parser


def _load(path: Path, label: str) -> dict[str, Any]:
    source = Path(path).resolve()
    if not source.is_file():
        raise ValueError(f"{label} is missing")
    return _strict_object(source.read_bytes(), source=source)


def _validate_recovery_against_step(
    recovery: Mapping[str, Any],
    *,
    test_id: str,
    context: int,
    step: Mapping[str, Any],
    bindings: Mapping[str, Any],
) -> None:
    expected_output = (
        Path(str(bindings["quality_output_root"])) / test_id / str(context)
    ).resolve()
    expected_summary = (
        Path(str(step.get("evidence_path"))).resolve().parent
        / "measurement-summary.json"
    ).resolve()
    if (
        recovery.get("test_id") != test_id
        or recovery.get("context_tokens") != context
        or recovery.get("runtime_evidence_path") != step.get("evidence_path")
        or recovery.get("runtime_evidence_sha256") != step.get("evidence_sha256")
        or recovery.get("matrix") != bindings.get("matrix_path")
        or recovery.get("matrix_sha256") != bindings.get("matrix_sha256")
        or recovery.get("spec_index_path") != bindings.get("spec_index_path")
        or recovery.get("spec_index_sha256") != bindings.get("spec_index_sha256")
        or recovery.get("artifact_inventory_path")
        != bindings.get("artifact_inventory_path")
        or recovery.get("artifact_inventory_sha256")
        != bindings.get("artifact_inventory_sha256")
        or recovery.get("build_root") != bindings.get("build_root")
        or recovery.get("build_provenance_path")
        != bindings.get("build_provenance_path")
        or recovery.get("build_provenance_sha256")
        != bindings.get("build_provenance_sha256")
        or recovery.get("sampler_script") != bindings.get("sampler_script_path")
        or recovery.get("sampler_script_sha256")
        != bindings.get("sampler_script_sha256")
        or recovery.get("prompt_set")
        != bindings.get("quality_prompt_set_path")
        or recovery.get("prompt_set_sha256")
        != bindings.get("quality_prompt_set_sha256")
        or recovery.get("rubric") != bindings.get("quality_rubric_path")
        or recovery.get("rubric_sha256")
        != bindings.get("quality_rubric_sha256")
        or recovery.get("runtime_summary") != str(expected_summary)
        or recovery.get("output_root") != str(expected_output)
        or recovery.get("timeout_seconds")
        != bindings.get("quality_timeout_seconds")
    ):
        raise ValueError("quality recovery differs from adaptive campaign state")
    load_accepted_quality_campaign(quality_campaign_input_from_recovery(recovery))


def _validate_campaign_state(path: Path) -> tuple[dict[str, Any], list[tuple[str, int, dict[str, Any]]]]:
    state_path = Path(path).resolve()
    raw_state = _load(state_path, "adaptive campaign state")
    if raw_state.get("schema") != STATE_SCHEMA:
        raise ValueError("adaptive campaign state schema is invalid")
    if raw_state.get("policy") != {
        "contexts": list(CONTEXTS),
        "candidate_order": list(CANDIDATE_ORDER),
        "start_reserve_mib": START_RESERVE_MIB,
        "runtime_floor_mib": RUNTIME_FLOOR_MIB,
        "max_guarded_attempts": MAX_GUARDED_ATTEMPTS,
    }:
        raise ValueError("adaptive campaign quality recovery policy drift")
    state = _validate_state(raw_state)
    bindings = state.get("bindings")
    if not isinstance(bindings, Mapping):
        raise ValueError("adaptive campaign bindings are missing")
    required_bindings = {
        "matrix_path",
        "matrix_sha256",
        "spec_index_path",
        "spec_index_sha256",
        "artifact_inventory_path",
        "artifact_inventory_sha256",
        "build_root",
        "build_root_sha256",
        "build_provenance_path",
        "build_provenance_sha256",
        "sampler_script_path",
        "sampler_script_sha256",
        "quality_prompt_set_path",
        "quality_prompt_set_sha256",
        "quality_rubric_path",
        "quality_rubric_sha256",
        "quality_output_root",
        "quality_timeout_seconds",
        "reference_boundary_index_path",
        "reference_boundary_index_sha256",
    }
    if set(bindings) != required_bindings:
        raise ValueError("adaptive campaign bindings are incomplete")
    for path_field, hash_field in (
        ("matrix_path", "matrix_sha256"),
        ("spec_index_path", "spec_index_sha256"),
        ("artifact_inventory_path", "artifact_inventory_sha256"),
        ("build_provenance_path", "build_provenance_sha256"),
        ("sampler_script_path", "sampler_script_sha256"),
        ("quality_prompt_set_path", "quality_prompt_set_sha256"),
        ("quality_rubric_path", "quality_rubric_sha256"),
    ):
        source = Path(str(bindings[path_field])).resolve()
        if not source.is_file() or _sha256_file(source) != bindings[hash_field]:
            raise ValueError(f"adaptive campaign {path_field} hash drift")
    build_root = Path(str(bindings["build_root"])).resolve()
    if (
        not build_root.is_dir()
        or _directory_sha256(build_root) != bindings["build_root_sha256"]
    ):
        raise ValueError("adaptive campaign build root hash drift")
    expected_quality_root = (state_path.parent / "quality").resolve()
    if (
        bindings.get("quality_output_root") != str(expected_quality_root)
        or type(bindings.get("quality_timeout_seconds")) is not float
        or bindings.get("quality_timeout_seconds") != 1800.0
    ):
        raise ValueError("adaptive campaign quality output or timeout binding drift")
    reference_path = bindings.get("reference_boundary_index_path")
    reference_hash = bindings.get("reference_boundary_index_sha256")
    if (reference_path is None) != (reference_hash is None):
        raise ValueError("adaptive campaign reference boundary binding is invalid")
    if reference_path is not None:
        source = Path(str(reference_path)).resolve()
        if not source.is_file() or _sha256_file(source) != reference_hash:
            raise ValueError("adaptive campaign reference boundary hash drift")
    recoveries: list[tuple[str, int, dict[str, Any]]] = []
    for test_id, context in build_ladder(Path(str(bindings["matrix_path"]))):
        step = state["steps"].get(f"{test_id}:{context}")
        if not isinstance(step, Mapping):
            continue
        if step.get("quality_status") != "quality-blocked":
            continue
        if step.get("runtime_status") != "passed":
            raise ValueError("quality-blocked step does not have passed runtime")
        recovery = step.get("quality_recovery")
        if not isinstance(recovery, Mapping):
            raise ValueError("quality-blocked step has no recovery arguments")
        expected_step_fields = {
            "test_id",
            "context_tokens",
            "runtime_status",
            "quality_status",
            "attempt_count",
            "failure_fingerprint",
            "evidence_path",
            "evidence_sha256",
            "attempts",
            "quality_recovery",
        }
        if "quality_result" in step:
            expected_step_fields.add("quality_result")
        if set(step) != expected_step_fields:
            raise ValueError("quality-blocked adaptive row subtree is invalid")
        _validate_recovery_against_step(
            recovery,
            test_id=test_id,
            context=context,
            step=step,
            bindings=bindings,
        )
        recoveries.append((test_id, context, dict(recovery)))
    return state, recoveries


def _direct_recovery(args: argparse.Namespace) -> dict[str, Any]:
    summary_path = Path(args.runtime_summary).resolve()
    summary = _load(summary_path, "accepted runtime summary")
    if summary.get("status") != "passed":
        raise ValueError("runtime summary status is not passed")
    if (
        summary.get("fallback_count") != 0
        or summary.get("residual_owned_process_count") != 0
        or summary.get("cleanup_process_count") != 0
    ):
        raise ValueError("runtime summary has fallback or survivor evidence")
    raw_samples = summary.get("raw_samples")
    if not isinstance(raw_samples, list) or len(raw_samples) != 3:
        raise ValueError("runtime summary raw samples are incomplete")
    for sample in raw_samples:
        if not isinstance(sample, Mapping):
            raise ValueError("runtime summary raw sample is invalid")
        source = Path(str(sample.get("path"))).resolve()
        if not source.is_file() or _sha256_file(source) != sample.get("sha256"):
            raise ValueError("runtime summary raw sample hash mismatch")
    measurement_path = Path(str(summary.get("measurement_summary_path"))).resolve()
    runtime_evidence = Path(str(summary.get("runtime_evidence_path"))).resolve()
    if (
        not measurement_path.is_file()
        or _sha256_file(measurement_path)
        != summary.get("measurement_summary_sha256")
        or not runtime_evidence.is_file()
        or _sha256_file(runtime_evidence)
        != summary.get("runtime_evidence_sha256")
    ):
        raise ValueError("runtime summary native evidence hash mismatch")

    def wrapper_bound_file(path_field: str, hash_field: str) -> Path:
        source = Path(str(summary.get(path_field))).resolve()
        if not source.is_file() or _sha256_file(source) != summary.get(hash_field):
            raise ValueError(f"runtime summary {path_field} hash mismatch")
        return source

    adaptive_runtime_spec = wrapper_bound_file(
        "adaptive_runtime_spec_path", "adaptive_runtime_spec_sha256"
    )
    spec_index = wrapper_bound_file("spec_index_path", "spec_index_sha256")
    artifact_inventory = wrapper_bound_file(
        "artifact_inventory_path", "artifact_inventory_sha256"
    )
    sequence = _load(runtime_evidence, "runtime attempt sequence")
    pilot = sequence.get("pilot")
    if not isinstance(pilot, Mapping) or not isinstance(
        pilot.get("spec_path"), str
    ):
        raise ValueError("runtime attempt sequence pilot binding is missing")
    pilot_spec = (measurement_path.parent / pilot["spec_path"]).resolve()
    if (
        not pilot_spec.is_file()
        or _sha256_file(pilot_spec) != pilot.get("spec_file_sha256")
    ):
        raise ValueError("runtime attempt sequence pilot spec hash mismatch")
    identity = _load(
        measurement_path.parent / "campaign-identity.json",
        "native campaign identity",
    )
    native = identity.get("identity")
    if not isinstance(native, Mapping):
        raise ValueError("native campaign identity is invalid")
    model = native.get("model")
    build = native.get("build")
    if not isinstance(model, Mapping) or not isinstance(build, Mapping):
        raise ValueError("native artifact or build identity is missing")
    artifact_manifest = Path(str(model.get("artifact_manifest_path"))).resolve()
    build_provenance = Path(str(build.get("provenance_path"))).resolve()
    if (
        not artifact_manifest.is_file()
        or _sha256_file(artifact_manifest)
        != model.get("artifact_manifest_sha256")
        or not build_provenance.is_file()
        or _sha256_file(build_provenance) != build.get("provenance_sha256")
    ):
        raise ValueError("native artifact or build evidence hash mismatch")
    unsigned = {
        "schema": "official-openvino-adaptive-quality-recovery/v1",
        "test_id": summary.get("test_id"),
        "context_tokens": summary.get("context_tokens"),
        "runtime_evidence_path": str(runtime_evidence),
        "runtime_evidence_sha256": summary.get("runtime_evidence_sha256"),
        "runtime_summary": str(measurement_path),
        "runtime_summary_sha256": summary.get("measurement_summary_sha256"),
        "adaptive_runtime_spec_path": str(adaptive_runtime_spec),
        "adaptive_runtime_spec_sha256": _sha256_file(adaptive_runtime_spec),
        "pilot_spec_path": str(pilot_spec),
        "pilot_spec_sha256": _sha256_file(pilot_spec),
        "spec_index_path": str(spec_index),
        "spec_index_sha256": _sha256_file(spec_index),
        "artifact_inventory_path": str(artifact_inventory),
        "artifact_inventory_sha256": _sha256_file(artifact_inventory),
        "matrix": str(Path(args.matrix).resolve()),
        "matrix_sha256": _sha256_file(args.matrix),
        "artifact_manifest_path": str(artifact_manifest),
        "artifact_manifest_sha256": _sha256_file(artifact_manifest),
        "prompt_set": str(Path(args.prompt_set).resolve()),
        "prompt_set_sha256": _sha256_file(args.prompt_set),
        "rubric": str(Path(args.rubric).resolve()),
        "rubric_sha256": _sha256_file(args.rubric),
        "model_path": str(Path(args.model_path).resolve()),
        "build_root": str(Path(args.build_root).resolve()),
        "build_provenance_path": str(build_provenance),
        "build_provenance_sha256": _sha256_file(build_provenance),
        "python_executable": str(Path(args.python_executable).resolve()),
        "python_site_packages": str(Path(args.python_site_packages).resolve()),
        "openvino_libraries": str(Path(args.openvino_libraries).resolve()),
        "sampler_script": str(Path(args.sampler_script).resolve()),
        "sampler_script_sha256": _sha256_file(args.sampler_script),
        "output_root": str(Path(args.output_root).resolve()),
        "timeout_seconds": float(args.timeout_seconds),
    }
    return {
        **unsigned,
        "quality_recovery_sha256": _sha256_json(unsigned),
    }


def _compact(result: Mapping[str, Any]) -> dict[str, Any]:
    return {
        "capture_summary_path": result["capture_summary_path"],
        "capture_summary_sha256": result["capture_summary_sha256"],
        "status": result["status"],
    }


def _require_preserved_low_memory_cleanup_proof(
    accepted: Any,
    failed: Any,
) -> None:
    primary_root = Path(failed.prompt_root).resolve()
    terminal_path = primary_root / adaptive_quality.TERMINAL_GUARD_FILENAME
    preserved_path = primary_root / "guard-evidence.json"
    terminal = _load(terminal_path, "quality prompt terminal guard")
    preserved_raw = preserved_path.read_bytes()
    preserved = _strict_object(preserved_raw, source=preserved_path)
    if (
        terminal.get("failure_stage") != "guard-evidence-reopen"
        or terminal.get("cleanup_process_count") != -1
        or terminal.get("active_pids") != []
        or terminal.get("preserved_guard_evidence_path") != str(preserved_path)
        or terminal.get("preserved_guard_evidence_sha256")
        != hashlib.sha256(preserved_raw).hexdigest()
        or preserved_raw != adaptive_quality._canonical_identity_bytes(preserved)
    ):
        raise ValueError(
            "quality prompt recovery lacks an exact preserved guard proof"
        )
    command = [
        str(accepted.python_executable),
        "-m",
        "scripts.testing.official_openvino.quality_worker",
        "--spec",
        str((primary_root / "worker-spec.json").resolve()),
        "--result",
        str((primary_root / "worker-result.json").resolve()),
    ]
    guard_status, cleanup = adaptive_quality._validate_guard(
        preserved,
        command=command,
        campaign=accepted,
        prompt_root=primary_root,
        timeout_seconds=accepted.timeout_seconds,
        spec_sha256=failed.worker_spec_sha256,
        log_sha256=_sha256_file(primary_root / "worker.log"),
    )
    observed = preserved.get("observed_available_ram_bytes")
    configured_floor = preserved.get("configured_minimum_available_ram_bytes")
    if (
        guard_status != "failed"
        or cleanup != 0
        or preserved.get("valid") is not False
        or preserved.get("low_memory_stop") is not True
        or preserved.get("timed_out") is not False
        or preserved.get("termination_reason") != "minimum_available_ram"
        or not isinstance(observed, Mapping)
        or isinstance(configured_floor, bool)
        or not isinstance(configured_floor, int)
        or isinstance(observed.get("minimum"), bool)
        or not isinstance(observed.get("minimum"), int)
        or observed["minimum"] >= configured_floor
    ):
        raise ValueError(
            "quality prompt recovery is not an isolated low-memory stop"
        )


def _resume_cleanup_proven_quality_campaign(
    campaign_input: Any,
    *,
    run_command: Callable[..., Mapping[str, Any]] = run_guarded_command,
) -> dict[str, Any]:
    """Append one recovery when preserved low-memory guard proves zero survivors."""

    source = (
        quality_campaign_input_from_recovery(campaign_input)
        if isinstance(campaign_input, Mapping)
        else campaign_input
    )
    accepted = load_accepted_quality_campaign(source)
    root = Path(accepted.output_root).resolve()
    if not root.is_dir():
        return capture_isolated_quality_campaign(
            source,
            resume=False,
            run_command=run_command,
        )
    evidence_paths = adaptive_quality._input_evidence_paths(source)
    adaptive_quality._validate_root_entries(root)
    latest_path, latest_value, results = adaptive_quality._validate_summary_history(
        accepted,
        root,
        evidence_paths=evidence_paths,
    )
    failed_prompt = next(
        (
            prompt_id
            for prompt_id in adaptive_quality.PROMPT_IDS
            if prompt_id in results and results[prompt_id].status != "passed"
        ),
        None,
    )
    if latest_path is None or latest_value is None or failed_prompt is None:
        return capture_isolated_quality_campaign(
            source,
            resume=True,
            run_command=run_command,
        )
    failed = results[failed_prompt]
    primary_root = root / failed_prompt
    recovery_root = root / f"{failed_prompt}-recovery-001"
    if (
        failed.prompt_root != primary_root
        or failed.cleanup_process_count != -1
        or recovery_root.exists()
    ):
        return capture_isolated_quality_campaign(
            source,
            resume=True,
            run_command=run_command,
        )

    _require_preserved_low_memory_cleanup_proof(accepted, failed)

    recovered = adaptive_quality._run_governed_quality_prompt(
        accepted,
        failed_prompt,
        recovery_root,
        accepted.timeout_seconds,
        run_command=run_command,
        evidence_paths=evidence_paths,
    )
    updated_results = dict(results)
    updated_results[failed_prompt] = recovered
    recovery_number = len(
        list(root.glob("capture-summary-recovery-*.json"))
    ) + 1
    summary_path = root / f"capture-summary-recovery-{recovery_number:03d}.json"
    summary = adaptive_quality._summary(
        accepted,
        updated_results,
        summary_path=summary_path,
        previous_summary_path=latest_path,
    )
    adaptive_quality._write_fresh(
        summary_path,
        adaptive_quality._canonical_json(summary),
    )
    validated_path, persisted, _loaded = (
        adaptive_quality._validate_summary_history(
            accepted,
            root,
            evidence_paths=evidence_paths,
        )
    )
    if validated_path != summary_path or persisted is None:
        raise RuntimeError("quality recovery summary publication failed")
    if recovered.status != "passed" or recovered.cleanup_process_count != 0:
        return persisted
    return capture_isolated_quality_campaign(
        source,
        resume=True,
        run_command=run_command,
    )


def main(argv: Sequence[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    campaign_mode = args.campaign_state is not None or args.resume_quality_blocked
    if campaign_mode:
        if args.campaign_state is None or not args.resume_quality_blocked:
            parser.error("--campaign-state requires --resume-quality-blocked")
        if args.runtime_summary is not None or args.model_path is not None or args.output_root is not None:
            parser.error(
                "--campaign-state --resume-quality-blocked is mutually exclusive "
                "with --runtime-summary, --model-path, and --output-root"
            )
        state_path = Path(args.campaign_state).resolve()
        state, recoveries = _validate_campaign_state(state_path)
        captures = []
        for test_id, context, recovery in recoveries:
            result = _resume_cleanup_proven_quality_campaign(
                recovery,
            )
            step = state["steps"][f"{test_id}:{context}"]
            step["quality_status"] = result["status"]
            step["quality_result"] = result
            captures.append(_compact(result))
            save_state_atomically(state_path, state)
        print(json.dumps({"captures": captures}, sort_keys=True, separators=(",", ":")))
        return 0

    required = (
        "runtime_summary",
        "matrix",
        "prompt_set",
        "rubric",
        "model_path",
        "build_root",
        "python_executable",
        "python_site_packages",
        "openvino_libraries",
        "sampler_script",
        "output_root",
    )
    missing = [f"--{field.replace('_', '-')}" for field in required if getattr(args, field) is None]
    if missing:
        parser.error("explicit quality capture requires " + ", ".join(missing))
    recovery = _direct_recovery(args)
    result = capture_isolated_quality_campaign(recovery, resume=args.resume)
    print(json.dumps(_compact(result), sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
