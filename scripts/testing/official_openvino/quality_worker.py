"""Governed, score-free P1-P6 OpenVINO quality-capture worker."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import tempfile
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any


SPEC_SCHEMA = "official-openvino-wb04-quality-worker-spec/v1"
RESULT_SCHEMA = "official-openvino-wb04-quality-worker-result/v1"
GENERATION_SETTINGS = {
    "max_new_tokens": 256,
    "do_sample": False,
    "rng_seed": 42,
    "apply_chat_template": False,
}
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
    normalized = value.casefold().replace("_", "-")
    return any(token in normalized for token in _FORBIDDEN_FIELD_TOKENS)


def _reject_forbidden_fields(value: Any) -> None:
    if isinstance(value, Mapping):
        for key, item in value.items():
            if not isinstance(key, str):
                raise ValueError("quality worker field names must be strings")
            if _field_is_forbidden(key):
                raise ValueError(f"forbidden quality worker field: {key}")
            _reject_forbidden_fields(item)
    elif isinstance(value, list):
        for item in value:
            _reject_forbidden_fields(item)


def _require_nonblank_text(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"quality worker {field} must be non-blank text")
    return value


def _validate_spec(spec: Mapping[str, Any]) -> list[dict[str, str]]:
    if not isinstance(spec, Mapping):
        raise ValueError("quality worker spec must be an object")
    _reject_forbidden_fields(spec)
    required = {
        "schema",
        "model_path",
        "device",
        "properties",
        "generation_settings",
        "prompts",
    }
    if set(spec) != required:
        raise ValueError("quality worker spec fields are invalid")
    if spec["schema"] != SPEC_SCHEMA:
        raise ValueError("quality worker spec schema is invalid")
    _require_nonblank_text(spec["model_path"], "model path")
    _require_nonblank_text(spec["device"], "device")
    properties = spec["properties"]
    if not isinstance(properties, Mapping) or any(
        not isinstance(key, str) or not key.strip() for key in properties
    ):
        raise ValueError("quality worker properties must be an object")
    if spec["generation_settings"] != GENERATION_SETTINGS:
        raise ValueError("quality worker generation settings are not frozen")
    prompts = spec["prompts"]
    if not isinstance(prompts, list) or len(prompts) != len(_EXPECTED_PROMPTS):
        raise ValueError("quality worker requires seven ordered prompts")
    normalized: list[dict[str, str]] = []
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
        if (prompt_id, turn_id) != expected or not isinstance(prompt, str) or not prompt.strip():
            raise ValueError("quality worker requires seven ordered prompts")
        normalized.append(
            {"prompt_id": prompt_id, "turn_id": turn_id, "prompt": prompt}
        )
    return normalized


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

    prompts = _validate_spec(spec)
    import openvino_genai as ov_genai

    pipeline = ov_genai.LLMPipeline(
        spec["model_path"],
        spec["device"],
        **dict(spec["properties"]),
    )
    config = ov_genai.GenerationConfig()
    for field, value in GENERATION_SETTINGS.items():
        setattr(config, field, value)

    outcomes: list[dict[str, Any]] = []
    for entry in prompts[:6]:
        outcomes.append(
            _generate(
                pipeline,
                config,
                turn_id=f"{entry['prompt_id']}-{entry['turn_id'].replace('_', '-')}",
                prompt=entry["prompt"],
            )
        )

    p6_turn_one = outcomes[-1]
    p6_turn_two = prompts[-1]
    if p6_turn_one["status"] == "complete":
        p6_prompt = (
            f"User: {prompts[-2]['prompt'].strip()}\n"
            f"Assistant: {p6_turn_one['raw_output']}\n"
            f"User: {p6_turn_two['prompt'].strip()}"
        )
        outcomes.append(
            _generate(
                pipeline,
                config,
                turn_id="P6-turn-2",
                prompt=p6_prompt,
            )
        )
    else:
        outcomes.append(
            _outcome(
                turn_id="P6-turn-2",
                prompt=None,
                status="skipped",
                failure=RuntimeError("P6 turn 1 did not produce an output"),
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
    _atomic_write_result(args.result, execute_quality_worker(_load_spec(args.spec)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
