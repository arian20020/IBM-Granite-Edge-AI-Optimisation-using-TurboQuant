"""Run the frozen WB-04 P1-P6 screen through an evidence-only worker contract.

The controller never imports OpenVINO or a model runtime.  Each configuration
supplies an external worker command containing ``{request_json}`` and
``{response_json}`` placeholders.  The worker writes exactly
``{"status": "complete", "output": "..."}``, plus ``turn_1`` for P6.
This keeps orchestration, resume, and evidence validation testable without
inference while preserving an exact production execution boundary.

The configuration manifest is schema version 1 with a ``configurations`` list.
Each row provides ``test_id``, opaque ``blind_label``,
``configuration_sha256``, ``runtime_summary_path``, ``executor_command``, and
optionally ``timeout_seconds``.  Private row fields are never copied into the
reviewer-facing scoring bundle.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import subprocess
import tempfile
import time
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from types import MappingProxyType
from typing import Any

from scripts.testing.official_openvino.quality import (
    validate_response_record as validate_scoring_response_record,
)


PROMPT_IDS = tuple(f"P{number}" for number in range(1, 7))
EXPECTED_GENERATION_SETTINGS = {
    "temperature": 0.0,
    "top_p": 1.0,
    "seed": 42,
    "max_output_tokens": 256,
}
FROZEN_PROMPT_SET_SHA256 = (
    "9ba512818e81e0ba8da3ddc89cf040dd3b779d1edc41db23d3a896d778de807f"
)
FROZEN_RENDERED_SHA256S = {
    "P1.txt": "087d2a4181012f0b9596551d0fead99b918264d9c3ce7016f06ce88544aabb42",
    "P2.txt": "274127b4ea8d212959bf51dc6a0b79194a9a68e5967ecb0819c37103ce5d8673",
    "P3.txt": "6ac32adacdcacbecaf25a9496c68efcfd474d53e39a9076708241aedda6bde7d",
    "P4.txt": "839ece076c06161c5bcd06c7260606db5b44dfbecb17f3d075edb2fda179f417",
    "P5-instruction.txt": (
        "5f36749a4b50253b660a2d5b4a8dafb408d2a6db2d034e87cd3d83483e680489"
    ),
    "P6-turn1.txt": (
        "c32b3e9e03fa8a3303945e82fb7a5f1ef29b44833f558111c0f6e6307ded0d62"
    ),
    "P6-turn2-with-history.txt": (
        "c8d4521307d65c685e4e5a995fe0bf4023a042a91071c785ce317d92295b226e"
    ),
}
FROZEN_P5_FIXTURE_SHA256 = (
    "72555318f7ac5ece22987330d761d8ec831e92364b6f317bc4514f1f4240cc1a"
)
FROZEN_RUBRIC_SHA256 = (
    "a36016f66e02c9e28f0938cf81335dc4b522e9f92b7d9bad3031f90b7ef91d90"
)
FROZEN_PROMPT_SHA256S = {
    "P1": "481ddd30bfe6bab2f8bbafca6f4230f281c350fbd01b25ccaa54d33f0649bb2d",
    "P2": "1374573865e824bf16b3147297b06d72118a29cf05ab1f46f9acaf460017bedf",
    "P3": "5780b570fff8fca370f3b2b929ae3fb0f49465f3b19de0478df5a2800f54cab7",
    "P4": "3383c8133179f13f4bdfc6ac892a037e83a799e7a6050a5ad6c3c187315b4190",
    "P5": "8f3c9f57390dcdf24e551385be99f375d49b47ac3f88ddc2002eb8bf89dad36f",
    "P6": "d62ab09f572c39ad7e7134c6d9e568d2f0bedbd207bccf2b095cc53bddb1d1e2",
}
_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_BLIND_LABEL = re.compile(r"^response-[A-Z0-9]{2,24}$")
_IDENTITY_BEARING_LABEL = re.compile(
    r"(?:^|[-_])(?:ov|tbq\d*|turbo|bf16|f(?:p)?(?:16|32)|u[2348]|q[248]|"
    r"int[2348]|baseline|control|reference|standard|optim(?:ized|ised)|"
    r"compressed|uncompressed|cache|cpu|gpu|igpu)"
    r"(?:$|[-_])",
    re.IGNORECASE,
)


@dataclass(frozen=True)
class QualityConfiguration:
    """Private execution metadata which is never copied into scoring inputs."""

    test_id: str
    blind_label: str
    configuration_sha256: str
    runtime_summary_path: Path
    executor_command: tuple[str, ...]
    timeout_seconds: float = 2400.0


QualityExecutor = Callable[[QualityConfiguration, Path, Path], None]


@dataclass(frozen=True)
class QualityRuntimeIdentity:
    """Expected identity of the accepted measurement supplying a quality run."""

    test_id: str
    context_tokens: int
    campaign_identity_sha256: str


@dataclass(frozen=True)
class QualityGenerationRequest:
    """One exact model invocation in the frozen P1-P6 screen."""

    prompt_id: str
    turn_id: str
    raw_prompt: str
    generation_settings: Mapping[str, Any]


QualityGenerator = Callable[
    [QualityGenerationRequest],
    str | Mapping[str, Any],
]


def reject_nulls(value: Any, *, location: str = "$") -> None:
    """Reject JSON null recursively, including inside arbitrary mappings."""

    if value is None:
        raise ValueError(f"null value is prohibited at {location}")
    if isinstance(value, Mapping):
        for key, item in value.items():
            reject_nulls(item, location=f"{location}.{key}")
    elif isinstance(value, (list, tuple)):
        for index, item in enumerate(value):
            reject_nulls(item, location=f"{location}[{index}]")


def _reject_json_constant(value: str) -> None:
    raise ValueError(f"non-finite JSON number is prohibited: {value}")


def _reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def parse_json_bytes_strict(raw: bytes, *, source: str | Path) -> Any:
    try:
        value = json.loads(
            raw.decode("utf-8-sig"),
            parse_constant=_reject_json_constant,
            object_pairs_hook=_reject_duplicate_keys,
        )
    except (UnicodeError, json.JSONDecodeError, ValueError) as exc:
        raise ValueError(f"invalid JSON artifact: {source}: {exc}") from exc
    reject_nulls(value)
    return value


def read_json_strict(path: Path) -> Any:
    try:
        raw = path.read_bytes()
    except OSError as exc:
        raise ValueError(f"invalid JSON artifact: {path}: {exc}") from exc
    return parse_json_bytes_strict(raw, source=path)


def _canonical_json(value: Any) -> bytes:
    reject_nulls(value)
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_text(value: str) -> str:
    return _sha256_bytes(value.encode("utf-8"))


def atomic_write_json(path: Path, value: Any) -> None:
    """Publish one new JSON artifact atomically without replacing evidence."""

    reject_nulls(value)
    if path.exists():
        raise FileExistsError(f"refusing to overwrite evidence: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_name: str | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="\n",
            prefix=f".{path.name}.",
            suffix=".tmp",
            dir=path.parent,
            delete=False,
        ) as temporary:
            temporary_name = temporary.name
            json.dump(
                value,
                temporary,
                ensure_ascii=False,
                allow_nan=False,
                indent=2,
                sort_keys=True,
            )
            temporary.write("\n")
            temporary.flush()
            os.fsync(temporary.fileno())
        try:
            os.link(temporary_name, path)
        except FileExistsError as exc:
            raise FileExistsError(
                f"refusing to overwrite evidence: {path}"
            ) from exc
        os.unlink(temporary_name)
        temporary_name = None
    finally:
        if temporary_name and os.path.exists(temporary_name):
            os.unlink(temporary_name)


def _validate_sha256(value: Any, *, field: str) -> str:
    if not isinstance(value, str) or not _SHA256.fullmatch(value):
        raise ValueError(f"{field} must be a lowercase SHA-256")
    return value


def _validate_blind_label(value: Any) -> str:
    if not isinstance(value, str) or not _BLIND_LABEL.fullmatch(value):
        raise ValueError(
            "blind_label must use the opaque response-[A-Z0-9] form"
        )
    if _IDENTITY_BEARING_LABEL.search(value):
        raise ValueError("blind_label must not reveal a precision, codec, or test identity")
    return value


def _read_utf8_exact(path: Path) -> tuple[str, str]:
    try:
        raw = path.read_bytes()
        text = raw.decode("utf-8-sig")
    except (OSError, UnicodeError) as exc:
        raise ValueError(f"invalid rendered prompt fixture: {path}: {exc}") from exc
    return text, _sha256_bytes(raw)


def load_prompt_contract(prompt_set_path: Path, rendered_root: Path) -> dict[str, Any]:
    """Load and hash the controlling P1-P6 execution contract."""

    prompt_set_bytes = prompt_set_path.read_bytes()
    prompt_set_sha256 = _sha256_bytes(prompt_set_bytes)
    if prompt_set_sha256 != FROZEN_PROMPT_SET_SHA256:
        raise ValueError("frozen prompt-set hash mismatch")
    prompt_set = parse_json_bytes_strict(prompt_set_bytes, source=prompt_set_path)
    if not isinstance(prompt_set, dict):
        raise ValueError("prompt set must be a JSON object")
    if prompt_set.get("prompt_set_id") != "GTQ-PROMPTS-v1":
        raise ValueError("unexpected prompt_set_id")
    settings = prompt_set.get("generation_defaults")
    if settings != EXPECTED_GENERATION_SETTINGS:
        raise ValueError("frozen generation settings do not match the P1-P6 contract")
    prompt_rows = prompt_set.get("prompts")
    if not isinstance(prompt_rows, list):
        raise ValueError("prompt set prompts must be a list")
    indexed = {
        row.get("prompt_id"): row for row in prompt_rows if isinstance(row, dict)
    }
    if set(indexed) != set(PROMPT_IDS) or len(prompt_rows) != len(PROMPT_IDS):
        raise ValueError("prompt set must contain P1-P6 exactly once")

    rendered_names = {
        "P1": ("P1.txt",),
        "P2": ("P2.txt",),
        "P3": ("P3.txt",),
        "P4": ("P4.txt",),
        "P5": ("P5-instruction.txt",),
        "P6": ("P6-turn1.txt", "P6-turn2-with-history.txt"),
    }
    rendered: dict[str, dict[str, tuple[str, str]]] = {}
    for prompt_id, names in rendered_names.items():
        rendered[prompt_id] = {}
        for name in names:
            text, digest = _read_utf8_exact(rendered_root / name)
            if digest != FROZEN_RENDERED_SHA256S[name]:
                raise ValueError(
                    f"frozen rendered prompt hash mismatch: {name}"
                )
            rendered[prompt_id][name] = (text, digest)

    fixture_value = indexed["P5"].get("fixture_path")
    if fixture_value != "fixtures/P5-long-context-v1.txt":
        raise ValueError("P5 must use the frozen long-context fixture")
    fixture_path = (prompt_set_path.parent / fixture_value).resolve()
    try:
        fixture_path.relative_to(prompt_set_path.parent.resolve())
    except ValueError as exc:
        raise ValueError("P5 fixture escapes the prompt-set directory") from exc
    p5_context, p5_fixture_sha256 = _read_utf8_exact(fixture_path)
    if p5_fixture_sha256 != FROZEN_P5_FIXTURE_SHA256:
        raise ValueError("frozen P5 fixture hash mismatch")

    prompts: dict[str, dict[str, Any]] = {}
    for prompt_id in PROMPT_IDS:
        sources = {
            name: digest
            for name, (_text, digest) in rendered[prompt_id].items()
        }
        if prompt_id in {"P1", "P2", "P3", "P4"}:
            name = rendered_names[prompt_id][0]
            execution = {
                "mode": "single_turn",
                "prompt": rendered[prompt_id][name][0],
            }
        elif prompt_id == "P5":
            instruction = rendered[prompt_id]["P5-instruction.txt"][0]
            sources["P5-long-context-v1.txt"] = p5_fixture_sha256
            execution = {
                "mode": "single_turn",
                "prompt": p5_context + instruction,
            }
        else:
            turn_one = rendered[prompt_id]["P6-turn1.txt"][0]
            history = rendered[prompt_id]["P6-turn2-with-history.txt"][0]
            user_lines = [
                line.removeprefix("User: ").strip()
                for line in history.splitlines()
                if line.startswith("User: ")
            ]
            if (
                len(user_lines) != 2
                or not all(user_lines)
                or user_lines[0] != turn_one.strip()
            ):
                raise ValueError("P6 rendered history must contain exactly two user turns")
            execution = {
                "mode": "multi_turn",
                "turn_1_prompt": turn_one,
                "turn_2_prompt": user_lines[1],
                "history_policy": "actual_turn_1_output",
            }
        prompt_hash_input = {
            "prompt_id": prompt_id,
            "execution": execution,
            "rendered_source_sha256s": sources,
        }
        prompts[prompt_id] = {
            **prompt_hash_input,
            "prompt_sha256": _sha256_bytes(_canonical_json(prompt_hash_input)),
        }
        if prompts[prompt_id]["prompt_sha256"] != FROZEN_PROMPT_SHA256S[prompt_id]:
            raise ValueError(f"frozen {prompt_id} execution hash mismatch")

    contract = {
        "prompt_set_id": prompt_set["prompt_set_id"],
        "prompt_set_sha256": prompt_set_sha256,
        "generation_settings": dict(settings),
        "prompts": prompts,
    }
    reject_nulls(contract)
    return contract


def _validate_runtime_identity(identity: QualityRuntimeIdentity) -> None:
    if not isinstance(identity.test_id, str) or not identity.test_id.strip():
        raise ValueError("test_id must be a non-blank string")
    if (
        isinstance(identity.context_tokens, bool)
        or not isinstance(identity.context_tokens, int)
        or identity.context_tokens <= 0
    ):
        raise ValueError("context_tokens must be a positive integer")
    _validate_sha256(
        identity.campaign_identity_sha256,
        field="campaign_identity_sha256",
    )


def _load_accepted_measurement_summary(
    path: Path,
    expected: QualityRuntimeIdentity,
) -> tuple[dict[str, Any], str, str]:
    """Load one accepted measured summary and bind its full runtime identity."""

    _validate_runtime_identity(expected)
    try:
        raw = path.read_bytes()
    except OSError as exc:
        raise RuntimeError(f"accepted measurement summary is unavailable: {path}") from exc
    value = parse_json_bytes_strict(raw, source=path)
    if not isinstance(value, dict):
        raise RuntimeError("accepted measurement summary must be a JSON object")
    required_state = (
        value.get("schema_version") == 1
        and value.get("status") == "measured"
        and value.get("accepted") is True
        and value.get("sample_count") == 3
        and value.get("cleanup_process_count") == 0
    )
    if not required_state:
        raise RuntimeError(
            "quality capture requires an accepted measurement summary with "
            "three samples and zero surviving owned processes"
        )
    expected_fields = {
        "test_id": expected.test_id,
        "context_tokens": expected.context_tokens,
        "campaign_identity_sha256": expected.campaign_identity_sha256,
    }
    for field, expected_value in expected_fields.items():
        if value.get(field) != expected_value:
            raise RuntimeError(
                f"measurement summary {field.replace('_', ' ')} mismatch"
            )
    _validate_sha256(
        value["campaign_identity_sha256"],
        field="campaign_identity_sha256",
    )
    runtime_config_sha256 = _validate_sha256(
        value.get("runtime_config_sha256"),
        field="runtime_config_sha256",
    )
    return dict(value), _sha256_bytes(raw), runtime_config_sha256


def _load_frozen_rubric(path: Path) -> tuple[dict[str, Any], str]:
    try:
        raw = path.read_bytes()
    except OSError as exc:
        raise ValueError(f"frozen quality rubric is unavailable: {path}") from exc
    digest = _sha256_bytes(raw)
    if digest != FROZEN_RUBRIC_SHA256:
        raise ValueError("frozen quality rubric hash mismatch")
    value = parse_json_bytes_strict(raw, source=path)
    if (
        not isinstance(value, dict)
        or value.get("rubric_id") != "GTQ-QUALITY-RUBRIC-v1"
        or value.get("status") != "controlling"
    ):
        raise ValueError("frozen quality rubric identity mismatch")
    return dict(value), digest


def _p6_turn_two_prompt(
    turn_one_prompt: str,
    turn_one_output: str,
    turn_two_prompt: str,
) -> str:
    return (
        f"User: {turn_one_prompt.strip()}\n"
        f"Assistant: {turn_one_output}\n"
        f"User: {turn_two_prompt.strip()}"
    )


def _generation_outcome(
    generate: QualityGenerator,
    request: QualityGenerationRequest,
) -> dict[str, str]:
    try:
        raw = generate(request)
    except Exception as exc:
        partial = getattr(exc, "output", "")
        return {
            "status": "failed",
            "output": partial if isinstance(partial, str) else "",
            "failure": str(exc),
        }
    if isinstance(raw, str):
        return {"status": "complete", "output": raw}
    if not isinstance(raw, Mapping):
        raise ValueError("quality generator must return text or a result mapping")
    result = dict(raw)
    status = result.get("status")
    expected_fields = (
        {"status", "output"}
        if status == "complete"
        else {"status", "output", "failure"}
    )
    if status not in {"complete", "failed"} or set(result) != expected_fields:
        raise ValueError(
            "quality generator result must be complete/output or "
            "failed/output/failure"
        )
    if not isinstance(result["output"], str):
        raise ValueError("quality generator output must be text")
    if status == "failed" and not isinstance(result["failure"], str):
        raise ValueError("quality generator failure must be text")
    return result


def _turn_prompt(turn_id: str, raw_prompt: str) -> dict[str, str]:
    return {
        "turn_id": turn_id,
        "raw_prompt": raw_prompt,
        "raw_prompt_sha256": _sha256_text(raw_prompt),
    }


def _turn_output(turn_id: str, outcome: Mapping[str, str]) -> dict[str, str]:
    output = outcome["output"]
    result = {
        "turn_id": turn_id,
        "status": outcome["status"],
        "output": output,
        "output_sha256": _sha256_text(output),
    }
    if outcome["status"] == "failed":
        result["failure"] = outcome["failure"]
        result["failure_sha256"] = _sha256_text(outcome["failure"])
    return result


def _response_sha256(turn_outputs: Sequence[Mapping[str, Any]]) -> str:
    return _sha256_bytes(_canonical_json(list(turn_outputs)))


def _record_sha256(record: Mapping[str, Any]) -> str:
    unsigned = {
        key: value for key, value in record.items() if key != "record_sha256"
    }
    return _sha256_bytes(_canonical_json(unsigned))


_CAPTURE_RECORD_FIELDS = {
    "schema_version",
    "artifact_type",
    "status",
    "test_id",
    "context_tokens",
    "campaign_identity_sha256",
    "prompt_id",
    "prompt_set_id",
    "prompt_set_sha256",
    "prompt_sha256",
    "raw_prompt",
    "raw_prompt_sha256",
    "rubric_id",
    "rubric_sha256",
    "runtime_summary_sha256",
    "runtime_config_sha256",
    "generation_settings",
    "generation_settings_sha256",
    "turn_prompts",
    "turn_outputs",
    "output",
    "output_sha256",
    "response_sha256",
    "record_sha256",
}
_CAPTURE_P6_FIELDS = {"turn_1", "turn_1_sha256"}
_CAPTURE_FAILURE_FIELDS = {
    "failure",
    "failure_sha256",
    "failed_turn_id",
}


def _build_capture_record(
    *,
    prompt_id: str,
    contract: Mapping[str, Any],
    rubric_sha256: str,
    expected_runtime: QualityRuntimeIdentity,
    runtime_summary_sha256: str,
    runtime_config_sha256: str,
    turn_prompts: Sequence[Mapping[str, str]],
    turn_outputs: Sequence[Mapping[str, str]],
) -> dict[str, Any]:
    final_prompt = turn_prompts[-1]["raw_prompt"]
    final_output = turn_outputs[-1]["output"]
    failed_outputs = [
        output for output in turn_outputs if output["status"] == "failed"
    ]
    status = "failed" if failed_outputs else "complete"
    record: dict[str, Any] = {
        "schema_version": 1,
        "artifact_type": "openvino-quality-response-record",
        "status": status,
        "test_id": expected_runtime.test_id,
        "context_tokens": expected_runtime.context_tokens,
        "campaign_identity_sha256": (
            expected_runtime.campaign_identity_sha256
        ),
        "prompt_id": prompt_id,
        "prompt_set_id": contract["prompt_set_id"],
        "prompt_set_sha256": contract["prompt_set_sha256"],
        "prompt_sha256": _sha256_text(final_prompt),
        "raw_prompt": final_prompt,
        "raw_prompt_sha256": _sha256_text(final_prompt),
        "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
        "rubric_sha256": rubric_sha256,
        "runtime_summary_sha256": runtime_summary_sha256,
        "runtime_config_sha256": runtime_config_sha256,
        "generation_settings": dict(contract["generation_settings"]),
        "generation_settings_sha256": _sha256_bytes(
            _canonical_json(contract["generation_settings"])
        ),
        "turn_prompts": [dict(prompt) for prompt in turn_prompts],
        "turn_outputs": [dict(output) for output in turn_outputs],
        "output": final_output,
        "output_sha256": _sha256_text(final_output),
        "response_sha256": _response_sha256(turn_outputs),
    }
    if prompt_id == "P6":
        first_output = turn_outputs[0]["output"]
        record["turn_1"] = first_output
        record["turn_1_sha256"] = _sha256_text(first_output)
    if status == "failed":
        failed = failed_outputs[0]
        record["failure"] = failed["failure"]
        record["failure_sha256"] = failed["failure_sha256"]
        record["failed_turn_id"] = failed["turn_id"]
    record["record_sha256"] = _record_sha256(record)
    return record


def _validate_capture_record(
    record: Any,
    *,
    prompt_id: str,
    contract: Mapping[str, Any],
    rubric_sha256: str,
    expected_runtime: QualityRuntimeIdentity,
    runtime_summary_sha256: str,
    runtime_config_sha256: str,
) -> dict[str, Any]:
    reject_nulls(record)
    if not isinstance(record, dict):
        raise ValueError("quality response record must be a JSON object")
    fixed = {
        "schema_version": 1,
        "artifact_type": "openvino-quality-response-record",
        "test_id": expected_runtime.test_id,
        "context_tokens": expected_runtime.context_tokens,
        "campaign_identity_sha256": (
            expected_runtime.campaign_identity_sha256
        ),
        "prompt_id": prompt_id,
        "prompt_set_id": contract["prompt_set_id"],
        "prompt_set_sha256": contract["prompt_set_sha256"],
        "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
        "rubric_sha256": rubric_sha256,
        "runtime_summary_sha256": runtime_summary_sha256,
        "runtime_config_sha256": runtime_config_sha256,
        "generation_settings": contract["generation_settings"],
        "generation_settings_sha256": _sha256_bytes(
            _canonical_json(contract["generation_settings"])
        ),
    }
    for field, expected in fixed.items():
        if record.get(field) != expected:
            raise ValueError(f"quality response {field} mismatch")
    if record.get("status") not in {"complete", "failed"}:
        raise ValueError("quality response status is invalid")
    expected_fields = set(_CAPTURE_RECORD_FIELDS)
    if prompt_id == "P6":
        expected_fields.update(_CAPTURE_P6_FIELDS)
    if record["status"] == "failed":
        expected_fields.update(_CAPTURE_FAILURE_FIELDS)
    if set(record) != expected_fields:
        raise ValueError(
            "quality response record has missing or unexpected fields"
        )
    for field in (
        "campaign_identity_sha256",
        "prompt_set_sha256",
        "prompt_sha256",
        "raw_prompt_sha256",
        "rubric_sha256",
        "runtime_summary_sha256",
        "runtime_config_sha256",
        "generation_settings_sha256",
        "output_sha256",
        "response_sha256",
        "record_sha256",
    ):
        _validate_sha256(record.get(field), field=field)
    if _record_sha256(record) != record["record_sha256"]:
        raise ValueError("quality response record hash mismatch")

    turn_prompts = record.get("turn_prompts")
    turn_outputs = record.get("turn_outputs")
    if (
        not isinstance(turn_prompts, list)
        or not isinstance(turn_outputs, list)
        or not turn_prompts
        or len(turn_prompts) != len(turn_outputs)
    ):
        raise ValueError("quality response turn evidence is incomplete")
    expected_turn_ids = (
        ["turn_1", "turn_2"]
        if prompt_id == "P6" and len(turn_prompts) == 2
        else ["turn_1"]
    )
    if len(turn_prompts) != len(expected_turn_ids):
        raise ValueError("quality response has an invalid turn count")
    if record["status"] == "complete" and (
        prompt_id == "P6" and len(turn_prompts) != 2
    ):
        raise ValueError("complete P6 response requires two turns")

    if prompt_id == "P6":
        execution = contract["prompts"]["P6"]["execution"]
        turn_one = record.get("turn_1")
        if not isinstance(turn_one, str):
            raise ValueError("P6 turn 1 output is required")
        expected_prompts = [execution["turn_1_prompt"]]
        if len(turn_prompts) == 2:
            expected_prompts.append(
                _p6_turn_two_prompt(
                    execution["turn_1_prompt"],
                    turn_one,
                    execution["turn_2_prompt"],
                )
            )
        if record.get("turn_1_sha256") != _sha256_text(turn_one):
            raise ValueError("P6 turn 1 output hash mismatch")
    else:
        expected_prompts = [contract["prompts"][prompt_id]["execution"]["prompt"]]

    for index, (prompt, output, turn_id, expected_prompt) in enumerate(
        zip(turn_prompts, turn_outputs, expected_turn_ids, expected_prompts)
    ):
        if not isinstance(prompt, dict) or set(prompt) != {
            "turn_id",
            "raw_prompt",
            "raw_prompt_sha256",
        }:
            raise ValueError(f"quality turn prompt {index} is invalid")
        if (
            prompt["turn_id"] != turn_id
            or prompt["raw_prompt"] != expected_prompt
            or prompt["raw_prompt_sha256"] != _sha256_text(expected_prompt)
        ):
            raise ValueError(f"quality turn prompt {index} identity mismatch")
        if not isinstance(output, dict):
            raise ValueError(f"quality turn output {index} is invalid")
        expected_output_fields = (
            {"turn_id", "status", "output", "output_sha256"}
            if output.get("status") == "complete"
            else {
                "turn_id",
                "status",
                "output",
                "output_sha256",
                "failure",
                "failure_sha256",
            }
        )
        if set(output) != expected_output_fields:
            raise ValueError(f"quality turn output {index} fields are invalid")
        if (
            output["turn_id"] != turn_id
            or output["status"] not in {"complete", "failed"}
            or not isinstance(output["output"], str)
            or output["output_sha256"] != _sha256_text(output["output"])
        ):
            raise ValueError(f"quality turn output {index} identity mismatch")
        if output["status"] == "failed" and (
            not isinstance(output["failure"], str)
            or output["failure_sha256"] != _sha256_text(output["failure"])
        ):
            raise ValueError(f"quality turn output {index} failure mismatch")

    final_prompt = turn_prompts[-1]["raw_prompt"]
    final_output = turn_outputs[-1]["output"]
    if (
        record.get("raw_prompt") != final_prompt
        or record["raw_prompt_sha256"] != _sha256_text(final_prompt)
        or record["prompt_sha256"] != _sha256_text(final_prompt)
        or record.get("output") != final_output
        or record["output_sha256"] != _sha256_text(final_output)
        or record["response_sha256"] != _response_sha256(turn_outputs)
    ):
        raise ValueError("quality response prompt or output hash mismatch")

    failures = [item for item in turn_outputs if item["status"] == "failed"]
    if record["status"] == "complete":
        if failures:
            raise ValueError("complete quality response contains a failed turn")
        validate_scoring_response_record(
            record,
            expected_runtime={
                "test_id": expected_runtime.test_id,
                "context_tokens": expected_runtime.context_tokens,
                "runtime_summary_sha256": runtime_summary_sha256,
                "runtime_config_sha256": runtime_config_sha256,
            },
            expected_prompt_set_sha256=contract["prompt_set_sha256"],
            expected_prompt_sha256=_sha256_text(final_prompt),
        )
    else:
        if len(failures) != 1:
            raise ValueError("failed quality response must preserve one failed turn")
        failed = failures[0]
        for field, expected in {
            "failure": failed["failure"],
            "failure_sha256": failed["failure_sha256"],
            "failed_turn_id": failed["turn_id"],
        }.items():
            if record.get(field) != expected:
                raise ValueError(f"failed quality response {field} mismatch")
    return dict(record)


def _capture_prompt(
    *,
    prompt_id: str,
    contract: Mapping[str, Any],
    generate: QualityGenerator,
    rubric_sha256: str,
    expected_runtime: QualityRuntimeIdentity,
    runtime_summary_sha256: str,
    runtime_config_sha256: str,
) -> dict[str, Any]:
    prompt = contract["prompts"][prompt_id]
    turn_prompts: list[dict[str, str]] = []
    turn_outputs: list[dict[str, str]] = []
    if prompt_id != "P6":
        request = QualityGenerationRequest(
            prompt_id=prompt_id,
            turn_id="turn_1",
            raw_prompt=prompt["execution"]["prompt"],
            generation_settings=MappingProxyType(
                dict(contract["generation_settings"])
            ),
        )
        outcome = _generation_outcome(generate, request)
        turn_prompts.append(_turn_prompt("turn_1", request.raw_prompt))
        turn_outputs.append(_turn_output("turn_1", outcome))
    else:
        execution = prompt["execution"]
        first_request = QualityGenerationRequest(
            prompt_id="P6",
            turn_id="turn_1",
            raw_prompt=execution["turn_1_prompt"],
            generation_settings=MappingProxyType(
                dict(contract["generation_settings"])
            ),
        )
        first_outcome = _generation_outcome(generate, first_request)
        turn_prompts.append(_turn_prompt("turn_1", first_request.raw_prompt))
        turn_outputs.append(_turn_output("turn_1", first_outcome))
        if first_outcome["status"] == "complete":
            second_raw_prompt = _p6_turn_two_prompt(
                execution["turn_1_prompt"],
                first_outcome["output"],
                execution["turn_2_prompt"],
            )
            second_request = QualityGenerationRequest(
                prompt_id="P6",
                turn_id="turn_2",
                raw_prompt=second_raw_prompt,
                generation_settings=MappingProxyType(
                    dict(contract["generation_settings"])
                ),
            )
            second_outcome = _generation_outcome(generate, second_request)
            turn_prompts.append(_turn_prompt("turn_2", second_request.raw_prompt))
            turn_outputs.append(_turn_output("turn_2", second_outcome))
    return _build_capture_record(
        prompt_id=prompt_id,
        contract=contract,
        rubric_sha256=rubric_sha256,
        expected_runtime=expected_runtime,
        runtime_summary_sha256=runtime_summary_sha256,
        runtime_config_sha256=runtime_config_sha256,
        turn_prompts=turn_prompts,
        turn_outputs=turn_outputs,
    )


def _build_capture_summary(
    *,
    records: Mapping[str, Mapping[str, Any]],
    contract: Mapping[str, Any],
    rubric_sha256: str,
    expected_runtime: QualityRuntimeIdentity,
    runtime_summary_sha256: str,
    runtime_config_sha256: str,
) -> dict[str, Any]:
    failure_count = sum(
        record["status"] == "failed" for record in records.values()
    )
    summary: dict[str, Any] = {
        "schema_version": 1,
        "artifact_type": "openvino-quality-capture-summary",
        "status": (
            "captured-with-failures" if failure_count else "captured"
        ),
        "test_id": expected_runtime.test_id,
        "context_tokens": expected_runtime.context_tokens,
        "campaign_identity_sha256": (
            expected_runtime.campaign_identity_sha256
        ),
        "runtime_summary_sha256": runtime_summary_sha256,
        "runtime_config_sha256": runtime_config_sha256,
        "prompt_set_id": contract["prompt_set_id"],
        "prompt_set_sha256": contract["prompt_set_sha256"],
        "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
        "rubric_sha256": rubric_sha256,
        "generation_settings_sha256": _sha256_bytes(
            _canonical_json(contract["generation_settings"])
        ),
        "response_count": len(records),
        "failure_count": failure_count,
        "responses": {
            prompt_id: {
                "status": record["status"],
                "record_sha256": record["record_sha256"],
                "output_sha256": record["output_sha256"],
            }
            for prompt_id, record in records.items()
        },
    }
    summary["capture_sha256"] = _sha256_bytes(_canonical_json(summary))
    return summary


def capture_quality_responses(
    *,
    measurement_summary_path: Path,
    expected_runtime: QualityRuntimeIdentity,
    prompt_set_path: Path,
    rendered_root: Path,
    rubric_path: Path,
    output_root: Path,
    generate: QualityGenerator,
    resume: bool = False,
) -> dict[str, Any]:
    """Capture immutable P1-P6 responses without scoring or judging them."""

    if not callable(generate):
        raise ValueError("generate must be callable")
    _summary, runtime_summary_sha256, runtime_config_sha256 = (
        _load_accepted_measurement_summary(
            measurement_summary_path,
            expected_runtime,
        )
    )
    contract = load_prompt_contract(prompt_set_path, rendered_root)
    _rubric, rubric_sha256 = _load_frozen_rubric(rubric_path)
    if not resume and output_root.exists() and any(output_root.iterdir()):
        raise FileExistsError(
            f"refusing to overwrite quality evidence: {output_root}"
        )

    summary_path = output_root / "capture-summary.json"
    if summary_path.exists():
        if not resume:
            raise FileExistsError(
                f"refusing to overwrite quality evidence: {summary_path}"
            )
        completed_records: dict[str, dict[str, Any]] = {}
        for prompt_id in PROMPT_IDS:
            response_path = output_root / prompt_id / "response.json"
            if not response_path.is_file():
                raise ValueError(
                    "completed quality capture is missing "
                    f"{prompt_id} response evidence"
                )
            completed_records[prompt_id] = _validate_capture_record(
                read_json_strict(response_path),
                prompt_id=prompt_id,
                contract=contract,
                rubric_sha256=rubric_sha256,
                expected_runtime=expected_runtime,
                runtime_summary_sha256=runtime_summary_sha256,
                runtime_config_sha256=runtime_config_sha256,
            )
        expected_summary = _build_capture_summary(
            records=completed_records,
            contract=contract,
            rubric_sha256=rubric_sha256,
            expected_runtime=expected_runtime,
            runtime_summary_sha256=runtime_summary_sha256,
            runtime_config_sha256=runtime_config_sha256,
        )
        persisted_summary = read_json_strict(summary_path)
        if persisted_summary != expected_summary:
            raise ValueError(
                "quality capture summary does not match response records"
            )
        return dict(persisted_summary)

    records: dict[str, dict[str, Any]] = {}
    for prompt_id in PROMPT_IDS:
        response_path = output_root / prompt_id / "response.json"
        if response_path.exists():
            if not resume:
                raise FileExistsError(
                    f"refusing to overwrite quality evidence: {response_path}"
                )
            record = _validate_capture_record(
                read_json_strict(response_path),
                prompt_id=prompt_id,
                contract=contract,
                rubric_sha256=rubric_sha256,
                expected_runtime=expected_runtime,
                runtime_summary_sha256=runtime_summary_sha256,
                runtime_config_sha256=runtime_config_sha256,
            )
        else:
            record = _capture_prompt(
                prompt_id=prompt_id,
                contract=contract,
                generate=generate,
                rubric_sha256=rubric_sha256,
                expected_runtime=expected_runtime,
                runtime_summary_sha256=runtime_summary_sha256,
                runtime_config_sha256=runtime_config_sha256,
            )
            _validate_capture_record(
                record,
                prompt_id=prompt_id,
                contract=contract,
                rubric_sha256=rubric_sha256,
                expected_runtime=expected_runtime,
                runtime_summary_sha256=runtime_summary_sha256,
                runtime_config_sha256=runtime_config_sha256,
            )
            atomic_write_json(response_path, record)
        records[prompt_id] = record

    capture_summary = _build_capture_summary(
        records=records,
        contract=contract,
        rubric_sha256=rubric_sha256,
        expected_runtime=expected_runtime,
        runtime_summary_sha256=runtime_summary_sha256,
        runtime_config_sha256=runtime_config_sha256,
    )
    atomic_write_json(summary_path, capture_summary)
    return capture_summary


def _validate_configuration(configuration: QualityConfiguration) -> None:
    if not isinstance(configuration.test_id, str) or not configuration.test_id:
        raise ValueError("test_id is required")
    _validate_blind_label(configuration.blind_label)
    _validate_sha256(
        configuration.configuration_sha256, field="configuration_sha256"
    )
    if not isinstance(configuration.runtime_summary_path, Path):
        raise ValueError("runtime_summary_path must be a Path")
    if (
        isinstance(configuration.timeout_seconds, bool)
        or not isinstance(configuration.timeout_seconds, (int, float))
        or not math.isfinite(float(configuration.timeout_seconds))
        or float(configuration.timeout_seconds) <= 0
    ):
        raise ValueError("timeout_seconds must be a positive finite number")
    if not isinstance(configuration.executor_command, tuple) or not all(
        isinstance(item, str) and item for item in configuration.executor_command
    ):
        raise ValueError("executor_command must be a tuple of non-empty strings")


def require_runtime_summary(
    test_id: str,
    summary: Mapping[str, Any] | None,
    *,
    configuration_sha256: str | None = None,
) -> dict[str, Any]:
    """Require accepted, cleaned-up runtime evidence for a quality row."""

    valid = (
        isinstance(summary, dict)
        and summary.get("test_id") == test_id
        and summary.get("status") in {"complete", "passed"}
        and summary.get("accepted") is True
        and summary.get("cleanup_process_count") == 0
        and (
            configuration_sha256 is None
            or summary.get("configuration_sha256") == configuration_sha256
        )
    )
    if not valid:
        raise RuntimeError(
            f"{test_id} requires accepted complete runtime evidence "
            "for the identical configuration"
        )
    reject_nulls(summary)
    return dict(summary)


def _load_runtime_summary(
    configuration: QualityConfiguration,
) -> tuple[dict[str, Any], str]:
    runtime_bytes = configuration.runtime_summary_path.read_bytes()
    summary = parse_json_bytes_strict(
        runtime_bytes, source=configuration.runtime_summary_path
    )
    validated = require_runtime_summary(
        configuration.test_id,
        summary if isinstance(summary, dict) else None,
        configuration_sha256=configuration.configuration_sha256,
    )
    digest = _sha256_bytes(runtime_bytes)
    return validated, digest


def build_request_artifact(
    contract: Mapping[str, Any],
    prompt_id: str,
    configuration: QualityConfiguration,
    runtime_evidence_sha256: str,
) -> dict[str, Any]:
    prompt = contract["prompts"][prompt_id]
    request = {
        "schema_version": 1,
        "artifact_type": "openvino-quality-execution-request",
        "blind_label": configuration.blind_label,
        "configuration_sha256": configuration.configuration_sha256,
        "runtime_evidence_sha256": runtime_evidence_sha256,
        "prompt_set_id": contract["prompt_set_id"],
        "prompt_set_sha256": contract["prompt_set_sha256"],
        "prompt_id": prompt_id,
        "prompt_sha256": prompt["prompt_sha256"],
        "rendered_source_sha256s": prompt["rendered_source_sha256s"],
        "generation_settings": contract["generation_settings"],
        "execution": prompt["execution"],
    }
    request["request_sha256"] = _sha256_bytes(_canonical_json(request))
    return request


_REQUEST_FIELDS = {
    "schema_version",
    "artifact_type",
    "blind_label",
    "configuration_sha256",
    "runtime_evidence_sha256",
    "prompt_set_id",
    "prompt_set_sha256",
    "prompt_id",
    "prompt_sha256",
    "rendered_source_sha256s",
    "generation_settings",
    "execution",
    "request_sha256",
}


def validate_request_artifact(
    request: Any,
    *,
    contract: Mapping[str, Any] | None = None,
    expected: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    reject_nulls(request)
    if not isinstance(request, dict) or set(request) != _REQUEST_FIELDS:
        raise ValueError("request artifact has missing or unexpected fields")
    if (
        request["schema_version"] != 1
        or request["artifact_type"] != "openvino-quality-execution-request"
        or request["prompt_id"] not in PROMPT_IDS
    ):
        raise ValueError("invalid request artifact identity")
    _validate_blind_label(request["blind_label"])
    for field in (
        "configuration_sha256",
        "runtime_evidence_sha256",
        "prompt_set_sha256",
        "prompt_sha256",
        "request_sha256",
    ):
        _validate_sha256(request[field], field=field)
    unsigned = {key: value for key, value in request.items() if key != "request_sha256"}
    if _sha256_bytes(_canonical_json(unsigned)) != request["request_sha256"]:
        raise ValueError("request hash mismatch")
    if expected is not None and request != expected:
        raise ValueError("resume request does not match the frozen execution contract")
    if contract is not None:
        prompt = contract["prompts"][request["prompt_id"]]
        controlling = {
            "prompt_set_id": contract["prompt_set_id"],
            "prompt_set_sha256": contract["prompt_set_sha256"],
            "generation_settings": contract["generation_settings"],
            "prompt_sha256": prompt["prompt_sha256"],
            "rendered_source_sha256s": prompt["rendered_source_sha256s"],
            "execution": prompt["execution"],
        }
        for key, value in controlling.items():
            if request.get(key) != value:
                raise ValueError(f"request does not match controlling {key}")
    return request


def _validate_worker_response(worker: Any, prompt_id: str) -> dict[str, str]:
    reject_nulls(worker)
    expected_fields = {"status", "output", "turn_1"} if prompt_id == "P6" else {
        "status",
        "output",
    }
    if not isinstance(worker, dict) or set(worker) != expected_fields:
        raise ValueError(f"{prompt_id} worker response has missing or unexpected fields")
    if worker.get("status") != "complete":
        raise ValueError(f"{prompt_id} worker response is not complete")
    for field in expected_fields - {"status"}:
        if not isinstance(worker[field], str):
            raise ValueError(f"{prompt_id} {field} must be a string")
    return worker


def build_response_artifact(
    request: Mapping[str, Any], worker: Mapping[str, str], elapsed_seconds: float
) -> dict[str, Any]:
    prompt_id = request["prompt_id"]
    validated = _validate_worker_response(worker, prompt_id)
    output = validated["output"]
    if prompt_id == "P6":
        values = (("turn_1", validated["turn_1"]), ("turn_2", output))
    else:
        values = (("turn_1", output),)
    turn_outputs = [
        {
            "turn_id": turn_id,
            "output": value,
            "output_sha256": _sha256_text(value),
        }
        for turn_id, value in values
    ]
    response_sha256 = _sha256_bytes(
        _canonical_json(
            {"prompt_id": prompt_id, "turn_outputs": turn_outputs}
        )
    )
    response = {
        "schema_version": 1,
        "artifact_type": "openvino-quality-response",
        "blind_label": request["blind_label"],
        "prompt_set_id": request["prompt_set_id"],
        "prompt_set_sha256": request["prompt_set_sha256"],
        "prompt_id": prompt_id,
        "prompt_sha256": request["prompt_sha256"],
        "request_sha256": request["request_sha256"],
        "status": "complete",
        "output": output,
        "output_sha256": _sha256_text(output),
        "response_sha256": response_sha256,
        "turn_outputs": turn_outputs,
        "elapsed_seconds": round(float(elapsed_seconds), 6),
    }
    reject_nulls(response)
    return response


_RESPONSE_FIELDS = {
    "schema_version",
    "artifact_type",
    "blind_label",
    "prompt_set_id",
    "prompt_set_sha256",
    "prompt_id",
    "prompt_sha256",
    "request_sha256",
    "status",
    "output",
    "output_sha256",
    "response_sha256",
    "turn_outputs",
    "elapsed_seconds",
}


def validate_response_artifact(
    response: Any, request: Mapping[str, Any]
) -> dict[str, Any]:
    reject_nulls(response)
    if not isinstance(response, dict) or set(response) != _RESPONSE_FIELDS:
        raise ValueError("response artifact has missing or unexpected fields")
    if (
        response["schema_version"] != 1
        or response["artifact_type"] != "openvino-quality-response"
        or response["status"] != "complete"
    ):
        raise ValueError("response artifact is not complete")
    for key in (
        "blind_label",
        "prompt_set_id",
        "prompt_set_sha256",
        "prompt_id",
        "prompt_sha256",
        "request_sha256",
    ):
        if response[key] != request[key]:
            raise ValueError(f"response {key} does not match its request")
    output = response["output"]
    if not isinstance(output, str):
        raise ValueError("response output must be a string")
    _validate_sha256(response["output_sha256"], field="output_sha256")
    if _sha256_text(output) != response["output_sha256"]:
        raise ValueError("response output hash mismatch")
    _validate_sha256(response["response_sha256"], field="response_sha256")
    elapsed = response["elapsed_seconds"]
    if (
        isinstance(elapsed, bool)
        or not isinstance(elapsed, (int, float))
        or not math.isfinite(float(elapsed))
        or float(elapsed) < 0
    ):
        raise ValueError("response elapsed_seconds must be finite and non-negative")
    turns = response["turn_outputs"]
    expected_turn_ids = ("turn_1", "turn_2") if response["prompt_id"] == "P6" else (
        "turn_1",
    )
    if not isinstance(turns, list) or len(turns) != len(expected_turn_ids):
        raise ValueError("response has the wrong turn output count")
    for turn, expected_turn_id in zip(turns, expected_turn_ids):
        if not isinstance(turn, dict) or set(turn) != {
            "turn_id",
            "output",
            "output_sha256",
        }:
            raise ValueError("turn output has missing or unexpected fields")
        if turn["turn_id"] != expected_turn_id:
            raise ValueError("turn output order is invalid")
        if not isinstance(turn["output"], str):
            raise ValueError("turn output must be a string")
        if _sha256_text(turn["output"]) != turn["output_sha256"]:
            raise ValueError("turn output hash mismatch")
    if turns[-1]["output"] != output:
        raise ValueError("final turn output does not match response output")
    expected_response_sha256 = _sha256_bytes(
        _canonical_json(
            {"prompt_id": response["prompt_id"], "turn_outputs": turns}
        )
    )
    if response["response_sha256"] != expected_response_sha256:
        raise ValueError("complete response hash mismatch")
    return response


def build_completion_artifact(
    contract: Mapping[str, Any],
    configuration: QualityConfiguration,
    runtime_evidence_sha256: str,
    requests: Mapping[str, Mapping[str, Any]],
    responses: Mapping[str, Mapping[str, Any]],
) -> dict[str, Any]:
    prompts = {
        prompt_id: {
            "status": "complete",
            "prompt_sha256": contract["prompts"][prompt_id]["prompt_sha256"],
            "request_sha256": requests[prompt_id]["request_sha256"],
            "response_sha256": responses[prompt_id]["response_sha256"],
        }
        for prompt_id in PROMPT_IDS
    }
    return {
        "schema_version": 1,
        "artifact_type": "openvino-quality-configuration-completion",
        "blind_label": configuration.blind_label,
        "configuration_sha256": configuration.configuration_sha256,
        "runtime_evidence_sha256": runtime_evidence_sha256,
        "prompt_set_id": contract["prompt_set_id"],
        "prompt_set_sha256": contract["prompt_set_sha256"],
        "status": "complete",
        "prompts": prompts,
    }


_COMPLETION_FIELDS = {
    "schema_version",
    "artifact_type",
    "blind_label",
    "configuration_sha256",
    "runtime_evidence_sha256",
    "prompt_set_id",
    "prompt_set_sha256",
    "status",
    "prompts",
}


def validate_completion_artifact(
    completion: Any,
    *,
    requests: Mapping[str, Mapping[str, Any]],
    responses: Mapping[str, Mapping[str, Any]],
    expected: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    reject_nulls(completion)
    if not isinstance(completion, dict) or set(completion) != _COMPLETION_FIELDS:
        raise ValueError("completion artifact has missing or unexpected fields")
    if (
        completion["schema_version"] != 1
        or completion["artifact_type"]
        != "openvino-quality-configuration-completion"
        or completion["status"] != "complete"
        or set(completion["prompts"]) != set(PROMPT_IDS)
    ):
        raise ValueError("configuration completion is not a complete P1-P6 record")
    _validate_blind_label(completion["blind_label"])
    _validate_sha256(
        completion["configuration_sha256"], field="configuration_sha256"
    )
    _validate_sha256(
        completion["runtime_evidence_sha256"], field="runtime_evidence_sha256"
    )
    for prompt_id in PROMPT_IDS:
        request = requests[prompt_id]
        response = responses[prompt_id]
        if (
            request["configuration_sha256"]
            != completion["configuration_sha256"]
            or request["runtime_evidence_sha256"]
            != completion["runtime_evidence_sha256"]
        ):
            raise ValueError(
                f"{prompt_id} request configuration identity does not match completion"
            )
        for key in ("blind_label", "prompt_set_id", "prompt_set_sha256"):
            if request[key] != completion[key] or response[key] != completion[key]:
                raise ValueError(
                    f"{prompt_id} {key} does not match configuration completion"
                )
        item = completion["prompts"][prompt_id]
        if not isinstance(item, dict) or set(item) != {
            "status",
            "prompt_sha256",
            "request_sha256",
            "response_sha256",
        }:
            raise ValueError(f"{prompt_id} completion has invalid fields")
        if item != {
            "status": "complete",
            "prompt_sha256": request["prompt_sha256"],
            "request_sha256": request["request_sha256"],
            "response_sha256": response["response_sha256"],
        }:
            raise ValueError(f"{prompt_id} completion does not match prompt evidence")
    if expected is not None and completion != expected:
        raise ValueError("completion does not match the validated P1-P6 evidence")
    return completion


def _write_or_resume_request(
    path: Path, request: Mapping[str, Any], *, resume: bool
) -> dict[str, Any]:
    if path.exists():
        if not resume:
            raise FileExistsError(f"refusing to overwrite evidence: {path}")
        return validate_request_artifact(read_json_strict(path), expected=request)
    atomic_write_json(path, request)
    return dict(request)


def _validate_executor_command(command: Sequence[str]) -> None:
    if not command:
        raise ValueError("executor_command is required for command-line execution")
    request_token = "{request_json}"
    response_token = "{response_json}"
    if not any(request_token in item for item in command):
        raise ValueError("executor_command must contain {request_json}")
    if not any(response_token in item for item in command):
        raise ValueError("executor_command must contain {response_json}")


def subprocess_executor(
    configuration: QualityConfiguration, request_path: Path, response_path: Path
) -> None:
    """Invoke a configured worker using explicit request/response placeholders."""

    _validate_executor_command(configuration.executor_command)
    request_token = "{request_json}"
    response_token = "{response_json}"
    command = [
        item.replace(request_token, str(request_path)).replace(
            response_token, str(response_path)
        )
        for item in configuration.executor_command
    ]
    try:
        result = subprocess.run(
            command,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=float(configuration.timeout_seconds),
            check=False,
        )
    except subprocess.TimeoutExpired as exc:
        raise RuntimeError(
            f"quality worker timed out after {configuration.timeout_seconds:g} seconds"
        ) from exc
    if result.returncode:
        stderr = result.stderr.strip()
        detail = f": {stderr[-500:]}" if stderr else ""
        raise RuntimeError(
            f"quality worker exited {result.returncode}{detail}"
        )


def _load_config_evidence(
    row_root: Path, contract: Mapping[str, Any]
) -> tuple[dict[str, dict[str, Any]], dict[str, dict[str, Any]]]:
    requests: dict[str, dict[str, Any]] = {}
    responses: dict[str, dict[str, Any]] = {}
    for prompt_id in PROMPT_IDS:
        prompt_root = row_root / prompt_id
        request = validate_request_artifact(
            read_json_strict(prompt_root / "request.json"), contract=contract
        )
        response = validate_response_artifact(
            read_json_strict(prompt_root / "response.json"), request
        )
        requests[prompt_id] = request
        responses[prompt_id] = response
    return requests, responses


def run_quality_campaign(
    configurations: Sequence[QualityConfiguration],
    *,
    prompt_set_path: Path,
    rendered_root: Path,
    output_root: Path,
    executor: QualityExecutor = subprocess_executor,
    resume: bool = False,
) -> dict[str, Any]:
    """Execute configurations serially and publish only validated P1-P6 evidence."""

    if not configurations:
        raise ValueError("at least one quality configuration is required")
    contract = load_prompt_contract(prompt_set_path, rendered_root)
    runtime_hashes: dict[str, str] = {}
    labels: set[str] = set()
    test_ids: set[str] = set()
    for configuration in configurations:
        _validate_configuration(configuration)
        if executor is subprocess_executor:
            _validate_executor_command(configuration.executor_command)
        if configuration.blind_label in labels:
            raise ValueError(f"duplicate blind_label: {configuration.blind_label}")
        if configuration.test_id in test_ids:
            raise ValueError(f"duplicate test_id: {configuration.test_id}")
        labels.add(configuration.blind_label)
        test_ids.add(configuration.test_id)
        _summary, runtime_hash = _load_runtime_summary(configuration)
        runtime_hashes[configuration.blind_label] = runtime_hash

    # Preflight overwrite policy for all rows before executing any model request.
    if not resume:
        for configuration in configurations:
            row_root = output_root / configuration.blind_label
            if row_root.exists() and any(row_root.iterdir()):
                raise FileExistsError(
                    f"refusing to overwrite configuration evidence: {row_root}"
                )

    campaign_rows: list[dict[str, Any]] = []
    for configuration in configurations:
        row_root = output_root / configuration.blind_label
        completion_path = row_root / "completion.json"
        runtime_hash = runtime_hashes[configuration.blind_label]
        if completion_path.exists():
            if not resume:
                raise FileExistsError(
                    f"refusing to overwrite evidence: {completion_path}"
                )
            requests, responses = _load_config_evidence(row_root, contract)
            expected_completion = build_completion_artifact(
                contract, configuration, runtime_hash, requests, responses
            )
            validate_completion_artifact(
                read_json_strict(completion_path),
                requests=requests,
                responses=responses,
                expected=expected_completion,
            )
            campaign_rows.append(
                {
                    "blind_label": configuration.blind_label,
                    "status": "complete",
                    "prompt_count": 6,
                }
            )
            continue

        requests: dict[str, dict[str, Any]] = {}
        responses: dict[str, dict[str, Any]] = {}
        for prompt_id in PROMPT_IDS:
            prompt_root = row_root / prompt_id
            request_path = prompt_root / "request.json"
            response_path = prompt_root / "response.json"
            expected_request = build_request_artifact(
                contract, prompt_id, configuration, runtime_hash
            )
            request = _write_or_resume_request(
                request_path, expected_request, resume=resume
            )
            requests[prompt_id] = request
            if response_path.exists():
                if not resume:
                    raise FileExistsError(
                        f"refusing to overwrite evidence: {response_path}"
                    )
                responses[prompt_id] = validate_response_artifact(
                    read_json_strict(response_path), request
                )
                continue

            prompt_root.mkdir(parents=True, exist_ok=True)
            descriptor, staging_name = tempfile.mkstemp(
                prefix=".worker-response.", suffix=".tmp", dir=prompt_root
            )
            os.close(descriptor)
            os.unlink(staging_name)
            staging_path = Path(staging_name)
            started = time.monotonic()
            try:
                executor(configuration, request_path, staging_path)
                if not staging_path.is_file():
                    raise RuntimeError(
                        f"{configuration.blind_label} {prompt_id} worker did not "
                        "write its response artifact"
                    )
                worker = read_json_strict(staging_path)
                response = build_response_artifact(
                    request, worker, time.monotonic() - started
                )
                validate_response_artifact(response, request)
                atomic_write_json(response_path, response)
                responses[prompt_id] = response
            finally:
                if staging_path.exists():
                    staging_path.unlink()

        completion = build_completion_artifact(
            contract, configuration, runtime_hash, requests, responses
        )
        validate_completion_artifact(
            completion,
            requests=requests,
            responses=responses,
            expected=completion,
        )
        atomic_write_json(completion_path, completion)
        campaign_rows.append(
            {
                "blind_label": configuration.blind_label,
                "status": "complete",
                "prompt_count": 6,
            }
        )

    result = {
        "schema_version": 1,
        "status": "complete",
        "configuration_count": len(campaign_rows),
        "configurations": campaign_rows,
    }
    reject_nulls(result)
    return result


def load_configurations(manifest_path: Path) -> list[QualityConfiguration]:
    manifest = read_json_strict(manifest_path)
    if not isinstance(manifest, dict) or set(manifest) != {
        "schema_version",
        "configurations",
    }:
        raise ValueError("configuration manifest has missing or unexpected fields")
    if manifest["schema_version"] != 1 or not isinstance(
        manifest["configurations"], list
    ):
        raise ValueError("invalid configuration manifest")
    configurations: list[QualityConfiguration] = []
    allowed = {
        "test_id",
        "blind_label",
        "configuration_sha256",
        "runtime_summary_path",
        "executor_command",
        "timeout_seconds",
    }
    required = allowed - {"timeout_seconds"}
    for index, row in enumerate(manifest["configurations"]):
        if not isinstance(row, dict) or not required.issubset(row) or not set(
            row
        ).issubset(allowed):
            raise ValueError(f"configuration {index} has missing or unexpected fields")
        command = row["executor_command"]
        if not isinstance(command, list):
            raise ValueError(f"configuration {index} executor_command must be a list")
        runtime_path = Path(row["runtime_summary_path"])
        if not runtime_path.is_absolute():
            runtime_path = manifest_path.parent / runtime_path
        configuration = QualityConfiguration(
            test_id=row["test_id"],
            blind_label=row["blind_label"],
            configuration_sha256=row["configuration_sha256"],
            runtime_summary_path=runtime_path,
            executor_command=tuple(command),
            timeout_seconds=row.get("timeout_seconds", 2400.0),
        )
        _validate_configuration(configuration)
        configurations.append(configuration)
    return configurations


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run frozen official OpenVINO P1-P6 quality requests"
    )
    parser.add_argument("--config-manifest", type=Path, required=True)
    parser.add_argument("--prompt-set", type=Path, required=True)
    parser.add_argument("--rendered-root", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--resume", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    result = run_quality_campaign(
        load_configurations(args.config_manifest),
        prompt_set_path=args.prompt_set,
        rendered_root=args.rendered_root,
        output_root=args.output_root,
        resume=args.resume,
    )
    print(
        f"completed {result['configuration_count']} blind configurations "
        "with P1-P6 evidence"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
