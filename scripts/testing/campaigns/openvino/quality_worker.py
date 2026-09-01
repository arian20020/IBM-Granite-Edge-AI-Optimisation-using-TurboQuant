"""Governed, score-free P1-P6 OpenVINO quality-capture worker."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import tempfile
from collections.abc import Mapping, Sequence
from copy import deepcopy
from dataclasses import dataclass
from pathlib import Path
from types import MappingProxyType
from typing import Any


SPEC_SCHEMA = "official-openvino-wb04-quality-worker-spec/v1"
RESULT_SCHEMA = "official-openvino-wb04-quality-worker-result/v1"
PROMPT_SPEC_SCHEMA = "official-openvino-adaptive-quality-prompt-spec/v1"
PROMPT_RESULT_SCHEMA = "official-openvino-adaptive-quality-prompt-result/v1"
QUALITY_WORKER_SPEC_PATH_ENV = (
    "OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_PATH"
)
QUALITY_WORKER_SPEC_SHA256_ENV = (
    "OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_SHA256"
)
_QUALITY_WORKER_AUTHORITY_ENV_KEYS = frozenset(
    {
        QUALITY_WORKER_SPEC_PATH_ENV.casefold(),
        QUALITY_WORKER_SPEC_SHA256_ENV.casefold(),
    }
)
_FIXED_GENERATION_SETTINGS = (
    ("max_new_tokens", 256),
    ("do_sample", False),
    ("rng_seed", 42),
    ("apply_chat_template", False),
)
GENERATION_SETTINGS = MappingProxyType(dict(_FIXED_GENERATION_SETTINGS))
_EXPECTED_PROMPTS = (
    ("P1", "turn_1"),
    ("P2", "turn_1"),
    ("P3", "turn_1"),
    ("P4", "turn_1"),
    ("P5", "turn_1"),
    ("P6", "turn_1"),
    ("P6", "turn_2"),
)
_FORBIDDEN_FIELD_TOKENS = (
    "score",
    "rubric",
    "rank",
    "private-label",
    "codec",
    "performance-target",
)
_UNSET = object()


@dataclass(frozen=True)
class _Prompt:
    prompt_id: str
    turn_id: str
    prompt: str


@dataclass(frozen=True)
class _NormalizedSpec:
    model_path: str
    device: str
    properties: Mapping[str, Any]
    generation_settings: Mapping[str, Any]
    prompts: tuple[_Prompt, ...]


def _canonical_json(value: Any) -> bytes:
    try:
        return (
            json.dumps(
                value,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
    except (TypeError, ValueError) as error:
        raise ValueError("quality worker value must be canonical JSON") from error


def _sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def _field_is_forbidden(value: str) -> bool:
    normalized = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "-", value)
    normalized = re.sub(r"[^a-z0-9]+", "-", normalized.casefold()).strip("-")
    return any(token in normalized for token in _FORBIDDEN_FIELD_TOKENS)


def _reject_forbidden_fields(value: Any) -> None:
    if isinstance(value, Mapping):
        for key, item in value.items():
            if not isinstance(key, str):
                raise ValueError("quality worker field names must be strings")
            if _field_is_forbidden(key):
                raise ValueError(f"forbidden quality worker field: {key}")
            _reject_forbidden_fields(item)
    elif isinstance(value, Sequence) and not isinstance(
        value, (str, bytes, bytearray)
    ):
        for item in value:
            _reject_forbidden_fields(item)


def _require_nonblank_text(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"quality worker {field} must be non-blank text")
    return value


def _detached_snapshot(value: Any) -> Any:
    """Copy caller-owned mappings and sequences before validating the spec."""

    if isinstance(value, Mapping):
        return {
            key: _detached_snapshot(value[key])
            for key in value
        }
    if isinstance(value, Sequence) and not isinstance(
        value, (str, bytes, bytearray)
    ):
        items = [_detached_snapshot(item) for item in value]
        return tuple(items) if isinstance(value, tuple) else items
    return deepcopy(value)


def _validate_spec(spec: Mapping[str, Any]) -> _NormalizedSpec:
    if not isinstance(spec, Mapping):
        raise ValueError("quality worker spec must be an object")
    snapshot = _detached_snapshot(spec)
    _reject_forbidden_fields(snapshot)
    required = {
        "schema",
        "model_path",
        "device",
        "properties",
        "generation_settings",
        "prompts",
    }
    if set(snapshot) != required:
        raise ValueError("quality worker spec fields are invalid")
    if snapshot["schema"] != SPEC_SCHEMA:
        raise ValueError("quality worker spec schema is invalid")
    model_path = _require_nonblank_text(snapshot["model_path"], "model path")
    device = _require_nonblank_text(snapshot["device"], "device")
    properties = snapshot["properties"]
    if not isinstance(properties, Mapping) or any(
        not isinstance(key, str) or not key.strip() for key in properties
    ):
        raise ValueError("quality worker properties must be an object")
    normalized_properties = MappingProxyType(dict(properties))
    settings = snapshot["generation_settings"]
    if (
        not isinstance(settings, Mapping)
        or set(settings) != {field for field, _ in _FIXED_GENERATION_SETTINGS}
    ):
        raise ValueError("quality worker generation settings are not frozen")
    if any(
        type(settings[field]) is not type(expected) or settings[field] != expected
        for field, expected in _FIXED_GENERATION_SETTINGS
    ):
        raise ValueError("quality worker generation settings are not frozen")
    normalized_settings = MappingProxyType(dict(settings))
    prompts = snapshot["prompts"]
    if not isinstance(prompts, list) or len(prompts) != len(_EXPECTED_PROMPTS):
        raise ValueError("quality worker requires seven ordered prompts")
    normalized_prompts: list[_Prompt] = []
    for entry, expected in zip(prompts, _EXPECTED_PROMPTS, strict=True):
        if not isinstance(entry, Mapping) or set(entry) != {
            "prompt_id",
            "turn_id",
            "prompt",
        }:
            raise ValueError("quality worker prompts are invalid")
        prompt_id = entry["prompt_id"]
        turn_id = entry["turn_id"]
        prompt = entry["prompt"]
        if (
            (prompt_id, turn_id) != expected
            or not isinstance(prompt, str)
            or not prompt.strip()
        ):
            raise ValueError("quality worker requires seven ordered prompts")
        normalized_prompts.append(
            _Prompt(prompt_id=prompt_id, turn_id=turn_id, prompt=prompt)
        )
    return _NormalizedSpec(
        model_path=model_path,
        device=device,
        properties=normalized_properties,
        generation_settings=normalized_settings,
        prompts=tuple(normalized_prompts),
    )


def _outcome(
    *,
    turn_id: str,
    prompt: str | None,
    status: str,
    output: str | None = None,
    failure: BaseException | None = None,
) -> dict[str, Any]:
    result = {
        "turn_id": turn_id,
        "raw_prompt": prompt,
        "raw_prompt_sha256": _sha256_text(prompt) if prompt is not None else None,
        "status": status,
        "raw_output": output,
        "raw_output_sha256": _sha256_text(output) if output is not None else None,
        "failure_type": None,
        "failure_message": None,
    }
    if failure is not None:
        result["failure_type"] = type(failure).__name__
        result["failure_message"] = str(failure)
    return result


def _generate(
    pipeline: Any,
    config: Any,
    *,
    turn_id: str,
    prompt: str,
) -> dict[str, Any]:
    try:
        output = pipeline.generate(prompt, config)
        if not isinstance(output, str):
            raise TypeError("OpenVINO generation output must be text")
    except Exception as error:
        return _outcome(
            turn_id=turn_id,
            prompt=prompt,
            status="failed",
            failure=error,
        )
    return _outcome(
        turn_id=turn_id,
        prompt=prompt,
        status="complete",
        output=output,
    )


def execute_quality_worker(spec: Mapping[str, Any]) -> dict[str, Any]:
    """Capture the seven frozen quality turns without applying any judgment."""

    normalized = _validate_spec(spec)
    import openvino_genai as ov_genai

    pipeline = ov_genai.LLMPipeline(
        normalized.model_path,
        normalized.device,
        **dict(normalized.properties),
    )
    config = ov_genai.GenerationConfig()
    for field, value in normalized.generation_settings.items():
        setattr(config, field, value)

    outcomes: list[dict[str, Any]] = []
    for entry in normalized.prompts[:6]:
        outcomes.append(
            _generate(
                pipeline,
                config,
                turn_id=f"{entry.prompt_id}-{entry.turn_id.replace('_', '-')}",
                prompt=entry.prompt,
            )
        )

    p6_turn_one = outcomes[-1]
    p6_turn_one_prompt = normalized.prompts[-2].prompt
    p6_turn_two_prompt = normalized.prompts[-1].prompt
    p6_prompt = (
        f"User: {p6_turn_one_prompt.strip()}\n"
        f"Assistant: {p6_turn_one['raw_output'] or ''}\n"
        f"User: {p6_turn_two_prompt.strip()}"
    )
    outcomes.append(
        _generate(
            pipeline,
            config,
            turn_id="P6-turn-2",
            prompt=p6_prompt,
        )
    )

    result: dict[str, Any] = {
        "schema": RESULT_SCHEMA,
        "outcomes": outcomes,
    }
    result["worker_result_sha256"] = hashlib.sha256(
        _canonical_json(result)
    ).hexdigest()
    return result


def _require_sha256(value: Any, field: str) -> str:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in "0123456789abcdef" for character in value)
    ):
        raise ValueError(f"quality prompt worker {field} must be a SHA-256")
    return value


def _identity_sha256(value: Any) -> str:
    try:
        raw = json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        ).encode("utf-8")
    except (TypeError, ValueError) as error:
        raise ValueError("quality prompt worker binding is not canonical JSON") from error
    return hashlib.sha256(raw).hexdigest()


def _reopen_bound_file(bindings: Mapping[str, Any], stem: str) -> Path:
    path_value = bindings.get(f"{stem}_path")
    if not isinstance(path_value, str) or not path_value.strip():
        raise ValueError(f"quality prompt worker {stem} path is invalid")
    source = Path(path_value).resolve()
    if str(source) != path_value or not source.is_file():
        raise ValueError(f"quality prompt worker {stem} file is missing")
    expected = _require_sha256(bindings.get(f"{stem}_sha256"), f"{stem}_sha256")
    if hashlib.sha256(source.read_bytes()).hexdigest() != expected:
        raise ValueError(f"quality prompt worker {stem} hash mismatch")
    return source


def _load_bound_object(path: Path, label: str) -> dict[str, Any]:
    try:
        value = json.loads(Path(path).read_bytes())
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError(f"quality prompt worker {label} is invalid") from error
    if not isinstance(value, dict):
        raise ValueError(f"quality prompt worker {label} must be an object")
    return value


def _bound_reference(value: Any, *, parent: Path, label: str) -> Path:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"quality prompt worker {label} path is invalid")
    candidate = Path(value)
    return (
        (parent / candidate).resolve()
        if not candidate.is_absolute()
        else candidate.resolve()
    )


def _require_expected_binding(
    bindings: Mapping[str, Any],
    reopened: Mapping[str, Path],
    stem: str,
    *,
    path: Path,
    sha256: Any = _UNSET,
) -> None:
    if reopened[stem] != Path(path).resolve():
        raise ValueError(f"quality prompt worker {stem} path is not expected")
    if sha256 is not _UNSET and bindings[f"{stem}_sha256"] != _require_sha256(
        sha256, f"expected {stem}_sha256"
    ):
        raise ValueError(f"quality prompt worker {stem} hash is not expected")


def _validate_expected_prompt_bindings(
    bindings: Mapping[str, Any],
    reopened: Mapping[str, Path],
    reopened_samples: list[dict[str, str]],
    *,
    prompt_id: str,
    model_path: str,
    device: str,
    properties: Mapping[str, Any],
    private_controller: Mapping[str, Any],
) -> None:
    summary_path = reopened["runtime_summary"]
    campaign_root = summary_path.parent
    if summary_path.name != "measurement-summary.json":
        raise ValueError("quality prompt worker runtime_summary path is not expected")
    _require_expected_binding(
        bindings,
        reopened,
        "attempt_sequence",
        path=campaign_root / "attempt-sequence.json",
    )

    summary = _load_bound_object(summary_path, "runtime summary")
    sequence = _load_bound_object(
        reopened["attempt_sequence"], "attempt sequence"
    )
    identity_path = campaign_root / "campaign-identity.json"
    if not identity_path.is_file():
        raise ValueError("quality prompt worker campaign identity is missing")
    identity_document = _load_bound_object(identity_path, "campaign identity")
    identity = identity_document.get("identity")
    if not isinstance(identity, Mapping):
        raise ValueError("quality prompt worker campaign identity is invalid")
    campaign_sha256 = _identity_sha256(identity)
    if (
        identity_document.get("campaign_identity_sha256") != campaign_sha256
        or summary.get("campaign_identity_sha256") != campaign_sha256
        or sequence.get("campaign_identity_sha256") != campaign_sha256
    ):
        raise ValueError("quality prompt worker campaign identity hash mismatch")
    summary_reference = _bound_reference(
        sequence.get("measurement_summary_path"),
        parent=campaign_root,
        label="attempt sequence runtime_summary",
    )
    if (
        summary_reference != summary_path
        or sequence.get("measurement_summary_sha256")
        != bindings["runtime_summary_sha256"]
    ):
        raise ValueError("quality prompt worker runtime_summary binding is invalid")

    expected_samples: list[dict[str, str]] = []
    sources = summary.get("sources")
    if not isinstance(sources, list) or len(sources) != 3:
        raise ValueError("quality prompt worker runtime raw samples are invalid")
    for source in sources:
        if not isinstance(source, Mapping):
            raise ValueError("quality prompt worker runtime raw sample is invalid")
        path_value = source.get("path")
        expected_hash = _require_sha256(
            source.get("sha256"), "expected raw sample sha256"
        )
        expected_path = _bound_reference(
            path_value,
            parent=campaign_root,
            label="runtime raw sample",
        )
        expected_samples.append(
            {"path": str(expected_path), "sha256": expected_hash}
        )
    if reopened_samples != expected_samples:
        raise ValueError("quality prompt worker raw sample paths are not expected")

    test_id = private_controller["test_id"]
    context_tokens = private_controller["context_tokens"]
    if (
        summary.get("test_id") != test_id
        or summary.get("context_tokens") != context_tokens
    ):
        raise ValueError("quality prompt worker private controller binding is invalid")
    pilot = sequence.get("pilot")
    if not isinstance(pilot, Mapping):
        raise ValueError("quality prompt worker pilot binding is invalid")
    _require_expected_binding(
        bindings,
        reopened,
        "pilot_spec",
        path=_bound_reference(
            pilot.get("spec_path"),
            parent=campaign_root,
            label="pilot spec",
        ),
        sha256=pilot.get("spec_file_sha256"),
    )

    matrix = identity.get("matrix")
    model = identity.get("model")
    build = identity.get("build")
    config = identity.get("config")
    runtime = identity.get("runtime")
    if not all(
        isinstance(item, Mapping)
        for item in (matrix, model, build, config, runtime)
    ):
        raise ValueError("quality prompt worker campaign identity sections are invalid")
    matrix_case = matrix.get("case")
    validated_artifact = model.get("validated_artifact")
    python_identity = runtime.get("python_executable")
    if (
        not isinstance(matrix_case, Mapping)
        or not isinstance(validated_artifact, Mapping)
        or not isinstance(python_identity, Mapping)
    ):
        raise ValueError("quality prompt worker campaign identity detail is invalid")
    if (
        matrix_case.get("test_id") != test_id
        or identity.get("context") != context_tokens
    ):
        raise ValueError("quality prompt worker matrix row binding is invalid")
    _require_expected_binding(
        bindings,
        reopened,
        "matrix",
        path=_bound_reference(
            matrix.get("path"), parent=campaign_root, label="matrix"
        ),
        sha256=matrix.get("file_sha256"),
    )
    _require_expected_binding(
        bindings,
        reopened,
        "artifact_manifest",
        path=_bound_reference(
            model.get("artifact_manifest_path"),
            parent=campaign_root,
            label="artifact manifest",
        ),
        sha256=model.get("artifact_manifest_sha256"),
    )
    _require_expected_binding(
        bindings,
        reopened,
        "build_provenance",
        path=_bound_reference(
            build.get("provenance_path"),
            parent=campaign_root,
            label="build provenance",
        ),
        sha256=build.get("provenance_sha256"),
    )
    expected_model_path = _bound_reference(
        validated_artifact.get("artifact_root"),
        parent=campaign_root,
        label="model",
    )
    if Path(model_path).resolve() != expected_model_path:
        raise ValueError("quality prompt worker model path is not expected")
    if (
        dict(bindings["build_identity"]) != dict(build)
        or dict(bindings["runtime_property"]) != dict(config)
        or device != config.get("device")
        or dict(properties) != config.get("properties")
    ):
        raise ValueError("quality prompt worker runtime identity is invalid")

    source_root = Path(__file__).resolve().parents[4]
    fixed_repository_bindings = {
        "prompt_set": (
            source_root
            / "experiments"
            / "granite_turboquant_intel"
            / "prompts"
            / "fixed-feasibility-prompt-set-v1.json"
        ),
        "rubric": (
            source_root
            / "experiments"
            / "granite_turboquant_intel"
            / "rubrics"
            / "quality-rubric-v1.json"
        ),
        "quality_worker": Path(__file__).resolve(),
    }
    for stem, expected_path in fixed_repository_bindings.items():
        _require_expected_binding(
            bindings,
            reopened,
            stem,
            path=expected_path,
        )

    index = _load_bound_object(reopened["spec_index"], "spec index")
    adaptive_spec = _load_bound_object(
        reopened["adaptive_runtime_spec"], "adaptive runtime spec"
    )
    if index.get("schema") == "official-openvino-adaptive-comparison-spec-index/v1":
        entries = index.get("runtime_specs")
        matches = (
            [
                entry
                for entry in entries
                if isinstance(entry, Mapping)
                and entry.get("test_id") == test_id
                and entry.get("context_tokens") == context_tokens
            ]
            if isinstance(entries, list)
            else []
        )
        if len(matches) != 1:
            raise ValueError("quality prompt worker adaptive spec-index row is invalid")
        expected_adaptive_path = _bound_reference(
            matches[0].get("path"),
            parent=reopened["spec_index"].parent,
            label="adaptive runtime spec",
        )
        if (
            len(expected_adaptive_path.parents) < 3
            or reopened["spec_index"]
            != expected_adaptive_path.parents[2] / "spec-index.json"
        ):
            raise ValueError("quality prompt worker spec_index path is not expected")
        _require_expected_binding(
            bindings,
            reopened,
            "adaptive_runtime_spec",
            path=expected_adaptive_path,
            sha256=matches[0].get("sha256"),
        )
        _require_expected_binding(
            bindings,
            reopened,
            "artifact_inventory",
            path=_bound_reference(
                index.get("artifact_inventory_path"),
                parent=reopened["spec_index"].parent,
                label="artifact inventory",
            ),
            sha256=index.get("artifact_inventory_sha256"),
        )
        if index.get("matrix_sha256") != bindings["matrix_sha256"]:
            raise ValueError("quality prompt worker spec-index matrix hash mismatch")
    else:
        if reopened["spec_index"] != reopened["adaptive_runtime_spec"]:
            raise ValueError("quality prompt worker spec_index path is not expected")
        _require_expected_binding(
            bindings,
            reopened,
            "artifact_inventory",
            path=reopened["artifact_manifest"],
            sha256=bindings["artifact_manifest_sha256"],
        )
    adaptive_context = adaptive_spec.get(
        "context_tokens", adaptive_spec.get("context")
    )
    if (
        adaptive_spec.get("controlled_test_id") != test_id
        or adaptive_context != context_tokens
        or _bound_reference(
            adaptive_spec.get("model_path"),
            parent=reopened["adaptive_runtime_spec"].parent,
            label="adaptive model",
        )
        != expected_model_path
    ):
        raise ValueError("quality prompt worker adaptive runtime spec is invalid")
    if "artifact_manifest_path" in adaptive_spec and (
        _bound_reference(
            adaptive_spec.get("artifact_manifest_path"),
            parent=reopened["adaptive_runtime_spec"].parent,
            label="adaptive artifact manifest",
        )
        != reopened["artifact_manifest"]
        or adaptive_spec.get("artifact_manifest_sha256")
        != bindings["artifact_manifest_sha256"]
    ):
        raise ValueError("quality prompt worker adaptive artifact binding is invalid")

    command = bindings["command"]
    python_path = _bound_reference(
        python_identity.get("path"),
        parent=campaign_root,
        label="Python executable",
    )
    expected_prefix = [
        str(python_path),
        "-m",
        "scripts.testing.campaigns.openvino.quality_worker",
        "--spec",
    ]
    if (
        len(command) != 7
        or command[:4] != expected_prefix
        or command[5:6] != ["--result"]
    ):
        raise ValueError("quality prompt worker command is not expected")
    spec_path = Path(command[4]).resolve()
    result_path = Path(command[6]).resolve()
    if (
        str(spec_path) != command[4]
        or str(result_path) != command[6]
        or spec_path.name != "worker-spec.json"
        or result_path.name != "worker-result.json"
        or spec_path.parent != result_path.parent
        or spec_path.parent.name
        not in {prompt_id, f"{prompt_id}-recovery-001"}
    ):
        raise ValueError("quality prompt worker command paths are not expected")


def _validate_prompt_bindings(
    bindings: Any,
    *,
    prompt_id: str,
    model_path: str,
    device: str,
    properties: Mapping[str, Any],
    private_controller: Mapping[str, Any],
) -> dict[str, Any]:
    fields = {
        "runtime_summary_path",
        "runtime_summary_sha256",
        "raw_samples",
        "attempt_sequence_path",
        "attempt_sequence_sha256",
        "adaptive_runtime_spec_path",
        "adaptive_runtime_spec_sha256",
        "pilot_spec_path",
        "pilot_spec_sha256",
        "spec_index_path",
        "spec_index_sha256",
        "artifact_inventory_path",
        "artifact_inventory_sha256",
        "matrix_path",
        "matrix_sha256",
        "artifact_manifest_path",
        "artifact_manifest_sha256",
        "prompt_set_path",
        "prompt_set_sha256",
        "rubric_path",
        "rubric_sha256",
        "build_provenance_path",
        "build_provenance_sha256",
        "quality_worker_path",
        "quality_worker_sha256",
        "build_identity",
        "build_identity_sha256",
        "runtime_property",
        "runtime_property_sha256",
        "command",
        "command_sha256",
    }
    if not isinstance(bindings, Mapping) or set(bindings) != fields:
        raise ValueError("quality prompt worker bindings are invalid")
    snapshot = dict(bindings)
    reopened: dict[str, Path] = {}
    for stem in (
        "runtime_summary",
        "attempt_sequence",
        "adaptive_runtime_spec",
        "pilot_spec",
        "spec_index",
        "artifact_inventory",
        "matrix",
        "artifact_manifest",
        "prompt_set",
        "rubric",
        "build_provenance",
        "quality_worker",
    ):
        source = _reopen_bound_file(snapshot, stem)
        reopened[stem] = source
        if stem == "quality_worker" and source != Path(__file__).resolve():
            raise ValueError("quality prompt worker source binding is invalid")
    raw_samples = snapshot.get("raw_samples")
    if not isinstance(raw_samples, list) or len(raw_samples) != 3:
        raise ValueError("quality prompt worker raw sample bindings are invalid")
    reopened_samples: list[dict[str, str]] = []
    seen_paths: set[Path] = set()
    for sample in raw_samples:
        if not isinstance(sample, Mapping) or set(sample) != {"path", "sha256"}:
            raise ValueError("quality prompt worker raw sample binding is invalid")
        path_value = sample.get("path")
        if not isinstance(path_value, str) or not path_value.strip():
            raise ValueError("quality prompt worker raw sample path is invalid")
        source = Path(path_value).resolve()
        expected = _require_sha256(sample.get("sha256"), "raw sample sha256")
        if (
            str(source) != path_value
            or source in seen_paths
            or not source.is_file()
            or hashlib.sha256(source.read_bytes()).hexdigest() != expected
        ):
            raise ValueError("quality prompt worker raw sample hash mismatch")
        seen_paths.add(source)
        reopened_samples.append({"path": path_value, "sha256": expected})
    build_identity = snapshot.get("build_identity")
    runtime_property = snapshot.get("runtime_property")
    command = snapshot.get("command")
    if not isinstance(build_identity, Mapping) or not build_identity:
        raise ValueError("quality prompt worker build identity is invalid")
    if not isinstance(runtime_property, Mapping) or not runtime_property:
        raise ValueError("quality prompt worker runtime property is invalid")
    if (
        not isinstance(command, list)
        or not command
        or any(not isinstance(item, str) or not item for item in command)
    ):
        raise ValueError("quality prompt worker command is invalid")
    for field, value in (
        ("build identity", build_identity),
        ("runtime property", runtime_property),
        ("command", command),
    ):
        if snapshot.get(f"{field.replace(' ', '_')}_sha256") != _identity_sha256(value):
            raise ValueError(f"quality prompt worker {field} hash mismatch")
    snapshot["raw_samples"] = reopened_samples
    snapshot["build_identity"] = dict(build_identity)
    snapshot["runtime_property"] = dict(runtime_property)
    snapshot["command"] = list(command)
    _validate_expected_prompt_bindings(
        snapshot,
        reopened,
        reopened_samples,
        prompt_id=prompt_id,
        model_path=model_path,
        device=device,
        properties=properties,
        private_controller=private_controller,
    )
    return snapshot


def _validate_prompt_worker_spec(spec: Mapping[str, Any]) -> dict[str, Any]:
    if not isinstance(spec, Mapping):
        raise ValueError("quality prompt worker spec must be an object")
    snapshot = _detached_snapshot(spec)
    required = {
        "schema",
        "prompt_id",
        "model_path",
        "device",
        "properties",
        "generation_settings",
        "turns",
        "private_controller",
        "bindings",
    }
    if set(snapshot) != required or snapshot.get("schema") != PROMPT_SPEC_SCHEMA:
        raise ValueError("quality prompt worker spec fields are invalid")
    prompt_id = snapshot.get("prompt_id")
    if prompt_id not in {"P1", "P2", "P3", "P4", "P5", "P6"}:
        raise ValueError("quality prompt worker prompt id is invalid")
    _require_nonblank_text(snapshot.get("model_path"), "model path")
    _require_nonblank_text(snapshot.get("device"), "device")
    properties = snapshot.get("properties")
    if not isinstance(properties, Mapping) or any(
        not isinstance(key, str) or not key.strip() for key in properties
    ):
        raise ValueError("quality prompt worker properties are invalid")
    settings = snapshot.get("generation_settings")
    if (
        not isinstance(settings, Mapping)
        or set(settings) != {field for field, _ in _FIXED_GENERATION_SETTINGS}
        or any(
            type(settings[field]) is not type(expected)
            or settings[field] != expected
            for field, expected in _FIXED_GENERATION_SETTINGS
        )
    ):
        raise ValueError("quality prompt worker generation settings are not frozen")
    controller = snapshot.get("private_controller")
    if (
        not isinstance(controller, Mapping)
        or set(controller) != {"test_id", "context_tokens"}
        or not isinstance(controller.get("test_id"), str)
        or not controller["test_id"].strip()
        or isinstance(controller.get("context_tokens"), bool)
        or not isinstance(controller.get("context_tokens"), int)
        or controller["context_tokens"] <= 0
    ):
        raise ValueError("quality prompt worker private controller is invalid")
    bindings = _validate_prompt_bindings(
        snapshot.get("bindings"),
        prompt_id=prompt_id,
        model_path=snapshot["model_path"],
        device=snapshot["device"],
        properties=properties,
        private_controller=controller,
    )
    turns = snapshot.get("turns")
    expected_ids = (
        ("P6-turn-1", "P6-turn-2") if prompt_id == "P6" else (prompt_id,)
    )
    if not isinstance(turns, list) or len(turns) != len(expected_ids):
        raise ValueError("quality prompt worker turns are invalid")
    normalized_turns = []
    for index, (turn, turn_id) in enumerate(zip(turns, expected_ids, strict=True)):
        expected_fields = {"turn_id", "prompt"}
        if prompt_id == "P6" and index == 1:
            expected_fields.add("history_source_turn_id")
        if not isinstance(turn, Mapping) or set(turn) != expected_fields:
            raise ValueError("quality prompt worker turns are invalid")
        if turn.get("turn_id") != turn_id:
            raise ValueError("quality prompt worker turns are not ordered")
        _require_nonblank_text(turn.get("prompt"), "turn prompt")
        if index == 1 and turn.get("history_source_turn_id") != "P6-turn-1":
            raise ValueError("quality prompt worker history binding is invalid")
        normalized_turns.append(dict(turn))
    snapshot["properties"] = dict(properties)
    snapshot["generation_settings"] = dict(settings)
    snapshot["private_controller"] = dict(controller)
    snapshot["bindings"] = bindings
    snapshot["turns"] = normalized_turns
    return snapshot


def execute_quality_prompt_worker(spec: Mapping[str, Any]) -> dict[str, Any]:
    """Execute one isolated prompt (or P6 conversation) without scoring it."""

    normalized = _validate_prompt_worker_spec(spec)
    import openvino_genai as ov_genai

    pipeline = ov_genai.LLMPipeline(
        normalized["model_path"],
        normalized["device"],
        **normalized["properties"],
    )
    config = ov_genai.GenerationConfig()
    for field, value in normalized["generation_settings"].items():
        setattr(config, field, value)

    outcomes: list[dict[str, Any]] = []
    first_turn = normalized["turns"][0]
    outcomes.append(
        _generate(
            pipeline,
            config,
            turn_id=first_turn["turn_id"],
            prompt=first_turn["prompt"],
        )
    )
    if normalized["prompt_id"] == "P6":
        second_turn = normalized["turns"][1]
        first_output = outcomes[0]["raw_output"] or ""
        conversation = (
            f"User: {first_turn['prompt'].strip()}\n"
            f"Assistant: {first_output}\n"
            f"User: {second_turn['prompt'].strip()}"
        )
        second = _generate(
            pipeline,
            config,
            turn_id=second_turn["turn_id"],
            prompt=conversation,
        )
        second["history_source_sha256"] = _sha256_text(first_output)
        outcomes.append(second)

    unsigned: dict[str, Any] = {
        "schema": PROMPT_RESULT_SCHEMA,
        "prompt_id": normalized["prompt_id"],
        "worker_spec_sha256": hashlib.sha256(
            _canonical_json(normalized)
        ).hexdigest(),
        "outcomes": outcomes,
    }
    return {
        **unsigned,
        "worker_result_sha256": hashlib.sha256(
            _canonical_json(unsigned)
        ).hexdigest(),
    }


def _load_spec_document(path: Path) -> tuple[dict[str, Any], bytes]:
    source = Path(path)
    try:
        raw = source.read_bytes()
        value = json.loads(raw.decode("utf-8-sig"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError(f"quality worker spec is unreadable: {source}") from error
    if not isinstance(value, dict):
        raise ValueError("quality worker spec must contain an object")
    return value, raw


def _load_spec(path: Path) -> dict[str, Any]:
    value, _raw = _load_spec_document(path)
    return value


def _require_prompt_worker_guard_authority(
    *,
    spec_path: Path,
    result_path: Path,
    spec_bytes: bytes,
) -> tuple[Path, Path]:
    authority_path_value = os.environ.get(QUALITY_WORKER_SPEC_PATH_ENV)
    authority_sha256 = _require_sha256(
        os.environ.get(QUALITY_WORKER_SPEC_SHA256_ENV),
        "guard authority SHA-256",
    )
    if not isinstance(authority_path_value, str) or not authority_path_value:
        raise ValueError("quality prompt worker guard authority path is missing")
    authority_path = Path(authority_path_value)
    try:
        anchored_spec_path = authority_path.resolve(strict=True)
        actual_spec_path = Path(spec_path).resolve(strict=True)
    except (OSError, RuntimeError) as error:
        raise ValueError(
            "quality prompt worker spec path authority is invalid"
        ) from error
    if (
        not authority_path.is_absolute()
        or str(anchored_spec_path) != authority_path_value
        or not anchored_spec_path.is_file()
        or actual_spec_path != anchored_spec_path
    ):
        raise ValueError("quality prompt worker spec path authority mismatch")
    if hashlib.sha256(spec_bytes).hexdigest() != authority_sha256:
        raise ValueError("quality prompt worker guard authority SHA-256 mismatch")
    canonical_result_path = anchored_spec_path.parent / "worker-result.json"
    try:
        actual_result_path = Path(result_path).resolve()
    except (OSError, RuntimeError) as error:
        raise ValueError(
            "quality prompt worker result path authority is invalid"
        ) from error
    if actual_result_path != canonical_result_path:
        raise ValueError("quality prompt worker result path authority mismatch")
    return anchored_spec_path, canonical_result_path


def _validate_prompt_worker_invocation(
    spec: Mapping[str, Any],
    *,
    spec_path: Path,
    result_path: Path,
    spec_bytes: bytes,
) -> Path:
    anchored_spec_path, canonical_result_path = (
        _require_prompt_worker_guard_authority(
            spec_path=spec_path,
            result_path=result_path,
            spec_bytes=spec_bytes,
        )
    )
    normalized = _validate_prompt_worker_spec(spec)
    command = normalized["bindings"]["command"]
    expected = tuple(Path(command[index]).resolve() for index in (0, 4, 6))
    actual = (
        Path(sys.executable).resolve(),
        anchored_spec_path,
        canonical_result_path,
    )
    if actual != expected:
        raise ValueError(
            "quality prompt worker invocation does not match bound command"
        )
    return canonical_result_path


def _atomic_write_result(path: Path, result: Mapping[str, Any]) -> None:
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="wb",
            delete=False,
            dir=destination.parent,
            prefix=f".{destination.name}.",
            suffix=".tmp",
        ) as handle:
            temporary = Path(handle.name)
            handle.write(_canonical_json(result))
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, destination)
        temporary = None
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--spec", type=Path, required=True)
    parser.add_argument("--result", type=Path, required=True)
    args = parser.parse_args(argv)
    spec, spec_bytes = _load_spec_document(args.spec)
    publication_path = args.result
    if spec.get("schema") == PROMPT_SPEC_SCHEMA:
        publication_path = _validate_prompt_worker_invocation(
            spec,
            spec_path=args.spec,
            result_path=args.result,
            spec_bytes=spec_bytes,
        )
        result = execute_quality_prompt_worker(spec)
    else:
        if any(
            key.casefold() in _QUALITY_WORKER_AUTHORITY_ENV_KEYS
            for key in os.environ
        ):
            _anchored_spec_path, publication_path = (
                _require_prompt_worker_guard_authority(
                    spec_path=args.spec,
                    result_path=args.result,
                    spec_bytes=spec_bytes,
                )
            )
        result = execute_quality_worker(spec)
    _atomic_write_result(publication_path, result)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
