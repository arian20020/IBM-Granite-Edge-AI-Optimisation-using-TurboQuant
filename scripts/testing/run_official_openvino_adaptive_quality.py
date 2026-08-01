"""CLI for isolated adaptive OpenVINO quality capture and recovery."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from collections.abc import Mapping, Sequence
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
from scripts.testing.official_openvino.adaptive_quality import (
    capture_isolated_quality_campaign,
    quality_campaign_input_from_recovery,
)
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
    if (
        recovery.get("test_id") != test_id
        or recovery.get("context_tokens") != context
        or recovery.get("runtime_evidence_path") != step.get("evidence_path")
        or recovery.get("runtime_evidence_sha256") != step.get("evidence_sha256")
        or recovery.get("matrix") != bindings.get("matrix_path")
        or recovery.get("matrix_sha256") != bindings.get("matrix_sha256")
        or recovery.get("build_root") != bindings.get("build_root")
        or recovery.get("sampler_script") != bindings.get("sampler_script_path")
        or recovery.get("sampler_script_sha256")
        != bindings.get("sampler_script_sha256")
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
    }
    if not required_bindings.issubset(bindings):
        raise ValueError("adaptive campaign bindings are incomplete")
    for path_field, hash_field in (
        ("matrix_path", "matrix_sha256"),
        ("spec_index_path", "spec_index_sha256"),
        ("artifact_inventory_path", "artifact_inventory_sha256"),
        ("build_provenance_path", "build_provenance_sha256"),
        ("sampler_script_path", "sampler_script_sha256"),
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
    return {
        "test_id": summary.get("test_id"),
        "context_tokens": summary.get("context_tokens"),
        "runtime_evidence_path": str(runtime_evidence),
        "runtime_evidence_sha256": summary.get("runtime_evidence_sha256"),
        "runtime_summary": str(measurement_path),
        "runtime_summary_sha256": summary.get("measurement_summary_sha256"),
        "matrix": str(Path(args.matrix).resolve()),
        "matrix_sha256": _sha256_file(args.matrix),
        "prompt_set": str(Path(args.prompt_set).resolve()),
        "prompt_set_sha256": _sha256_file(args.prompt_set),
        "rubric": str(Path(args.rubric).resolve()),
        "rubric_sha256": _sha256_file(args.rubric),
        "model_path": str(Path(args.model_path).resolve()),
        "build_root": str(Path(args.build_root).resolve()),
        "python_executable": str(Path(args.python_executable).resolve()),
        "python_site_packages": str(Path(args.python_site_packages).resolve()),
        "openvino_libraries": str(Path(args.openvino_libraries).resolve()),
        "sampler_script": str(Path(args.sampler_script).resolve()),
        "sampler_script_sha256": _sha256_file(args.sampler_script),
        "output_root": str(Path(args.output_root).resolve()),
        "timeout_seconds": float(args.timeout_seconds),
    }


def _compact(result: Mapping[str, Any]) -> dict[str, Any]:
    return {
        "capture_summary_path": result["capture_summary_path"],
        "capture_summary_sha256": result["capture_summary_sha256"],
        "status": result["status"],
    }


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
            output = Path(recovery["output_root"])
            result = capture_isolated_quality_campaign(
                recovery,
                resume=output.exists(),
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
