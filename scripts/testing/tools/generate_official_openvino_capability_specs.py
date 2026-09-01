"""Generate exact-context, row-bound WB-04 TurboQuant capability specs."""

from __future__ import annotations

import argparse
import json
import os
import sys
import tempfile
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.campaigns.openvino.workload import build_context_workload


EXPECTED_IDS = tuple(f"OV-TQS-{number:02d}" for number in range(1, 5))
SPEC_SCHEMA = "official-openvino-wb04-worker-spec/v1"


def _directory(path: Path, label: str) -> Path:
    resolved = Path(path).resolve()
    if not resolved.is_dir():
        raise ValueError(f"{label} directory does not exist: {resolved}")
    return resolved


def _read_matrix(path: Path) -> list[dict[str, Any]]:
    try:
        value = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"matrix is unreadable: {path}") from error
    cases = value.get("cases") if isinstance(value, dict) else None
    if not isinstance(cases, list):
        raise ValueError("matrix cases must be a list")
    selected = {
        case.get("test_id"): case
        for case in cases
        if isinstance(case, dict) and case.get("test_id") in EXPECTED_IDS
    }
    if set(selected) != set(EXPECTED_IDS):
        raise ValueError("matrix must contain exactly OV-TQS-01 through OV-TQS-04")
    return [selected[test_id] for test_id in EXPECTED_IDS]


def _validate_case(case: dict[str, Any]) -> tuple[int, str, str]:
    if (
        case.get("phase") != "capability"
        or case.get("expected_outcome") != "pass"
        or case.get("requested_device") != "CPU"
        or case.get("norm_correction") is not True
        or case.get("contexts") != [256]
    ):
        raise ValueError(f"{case.get('test_id')} capability contract is invalid")
    key = case.get("runtime_key_algorithm")
    value = case.get("runtime_value_algorithm")
    if key not in {"TBQ3", "TBQ4"} or value not in {"TBQ3", "TBQ4"}:
        raise ValueError(f"{case.get('test_id')} algorithm pair is invalid")
    expected_precision = {"TBQ3": "u3", "TBQ4": "u4"}
    if (
        case.get("key_cache_precision") != expected_precision[key]
        or case.get("value_cache_precision") != expected_precision[value]
    ):
        raise ValueError(f"{case.get('test_id')} precision pair is invalid")
    return 256, key, value


def _write_json_atomic(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = (
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            indent=2,
            sort_keys=True,
        )
        + "\n"
    ).encode("utf-8")
    descriptor, temporary_name = tempfile.mkstemp(
        dir=path.parent,
        prefix=f".{path.name}.",
        suffix=".tmp",
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(encoded)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise


def generate_capability_specs(
    *,
    matrix_path: Path,
    model_path: Path,
    cache_root: Path,
    output_root: Path,
) -> dict[str, str]:
    """Write one deterministic context-256 pilot spec for each positive sweep row."""

    model = _directory(model_path, "model")
    cache = _directory(cache_root, "cache root")
    output = Path(output_root).resolve()
    result: dict[str, str] = {}
    for case in _read_matrix(matrix_path):
        context, key, value = _validate_case(case)
        test_id = str(case["test_id"])
        workload = build_context_workload(context)
        cache_name = (
            f"{test_id.casefold().replace('-', '_')}-"
            f"{key.casefold()}-{value.casefold()}-context-{context}"
        )
        spec = {
            "schema": SPEC_SCHEMA,
            "controlled_test_id": test_id,
            "context": context,
            "expected_input_tokens": workload["expected_input_tokens"],
            "prompt": workload["prompt"],
            "prompt_sha256": workload["prompt_sha256"],
            "model_path": str(model),
            "device": "CPU",
            "properties": {
                "ATTENTION_BACKEND": "SDPA",
                "CACHE_DIR": str((cache / cache_name).resolve()),
                "ENABLE_CPU_PINNING": False,
                "INFERENCE_NUM_THREADS": 1,
                "NUM_STREAMS": 1,
                "PERFORMANCE_HINT": "LATENCY",
                "TURBOQUANT_KEY_ALGORITHM": key,
                "TURBOQUANT_NORM_CORRECTION": True,
                "TURBOQUANT_VALUE_ALGORITHM": value,
            },
            "max_new_tokens": 4,
            "ignore_eos": True,
            "apply_chat_template": False,
            "seed": 42,
            "role": "pilot",
        }
        destination = output / test_id / f"context-{context}" / "spec.json"
        _write_json_atomic(destination, spec)
        result[test_id] = str(destination)
    return result


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matrix", dest="matrix_path", type=Path, required=True)
    parser.add_argument("--model-path", type=Path, required=True)
    parser.add_argument("--cache-root", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        result = generate_capability_specs(
            matrix_path=args.matrix_path,
            model_path=args.model_path,
            cache_root=args.cache_root,
            output_root=args.output_root,
        )
    except ValueError as error:
        raise SystemExit(f"capability spec generation refused: {error}") from error
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
