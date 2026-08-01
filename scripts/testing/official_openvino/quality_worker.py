"""Governed, score-free P1-P6 OpenVINO quality-capture worker."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
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
    bindings = snapshot.get("bindings")
    if not isinstance(bindings, Mapping) or not bindings:
        raise ValueError("quality prompt worker bindings are invalid")
    for field, value in bindings.items():
        if not isinstance(field, str) or not field.strip():
            raise ValueError("quality prompt worker binding name is invalid")
        if field.endswith("_sha256"):
            _require_sha256(value, field)
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
    snapshot["bindings"] = dict(bindings)
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


def _load_spec(path: Path) -> dict[str, Any]:
    source = Path(path)
    try:
        value = json.loads(source.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"quality worker spec is unreadable: {source}") from error
    if not isinstance(value, dict):
        raise ValueError("quality worker spec must contain an object")
    return value


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
    spec = _load_spec(args.spec)
    result = (
        execute_quality_prompt_worker(spec)
        if spec.get("schema") == PROMPT_SPEC_SCHEMA
        else execute_quality_worker(spec)
    )
    _atomic_write_result(args.result, result)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
