"""One fresh-process OpenVINO GenAI generation for WB-04 measurement."""

from __future__ import annotations

import argparse
import json
import math
import sys
import time
from collections.abc import Mapping
from pathlib import Path
from typing import Any

from .runtime_measurement import RESULT_MARKER, WORKER_SCHEMA


SPEC_SCHEMA = "official-openvino-wb04-worker-spec/v1"
PRECISION_PROPERTIES = frozenset(
    {"KV_CACHE_PRECISION", "KEY_CACHE_PRECISION", "VALUE_CACHE_PRECISION"}
)


def materialize_openvino_properties(
    properties: Mapping[str, Any],
    openvino_module: Any,
) -> dict[str, Any]:
    """Convert only OpenVINO element-type properties from JSON labels."""

    result = dict(properties)
    for name in PRECISION_PROPERTIES & result.keys():
        value = result[name]
        if not isinstance(value, str):
            raise ValueError(f"{name} must use a precision label")
        try:
            result[name] = getattr(openvino_module.Type, value)
        except AttributeError as error:
            raise ValueError(f"{name} precision is unsupported: {value}") from error
    return result


def _positive(value: Any, field: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be numeric")
    result = float(value)
    if not math.isfinite(result) or result <= 0:
        raise ValueError(f"{field} must be finite and positive")
    return result


def extract_performance_metrics(
    metrics: Any,
    *,
    request_ttft_ms: float,
    wall_generation_ms: float,
) -> dict[str, Any]:
    """Extract timing definitions directly from one GenAI result."""

    request_ttft = _positive(request_ttft_ms, "request TTFT")
    generation = _positive(wall_generation_ms, "wall generation duration")
    input_tokens = int(metrics.get_num_input_tokens())
    generated_tokens = int(metrics.get_num_generated_tokens())
    if input_tokens <= 0 or generated_tokens <= 0:
        raise ValueError("OpenVINO token counts must be positive")
    raw_prefill = list(metrics.raw_metrics.token_infer_durations)
    if not raw_prefill:
        raise ValueError("OpenVINO raw token inference durations are absent")
    first_inference_us = _positive(
        float(raw_prefill[0]), "first-token inference duration"
    )
    first_inference_ms = first_inference_us / 1000.0
    perf_ttft = _positive(float(metrics.get_ttft().mean), "OpenVINO TTFT")
    tpot = _positive(float(metrics.get_tpot().mean), "OpenVINO TPOT")
    openvino_generation = _positive(
        float(metrics.get_generate_duration().mean),
        "OpenVINO generation duration",
    )
    openvino_throughput = _positive(
        float(metrics.get_throughput().mean),
        "OpenVINO throughput",
    )
    return {
        "load_ms": _positive(float(metrics.get_load_time()), "OpenVINO load time"),
        "ttft_ms": request_ttft,
        "perf_ttft_ms": perf_ttft,
        "ttft_crosscheck_delta_ms": abs(request_ttft - perf_ttft),
        "prompt_tps": input_tokens / (first_inference_us / 1_000_000.0),
        "prompt_tps_definition": (
            "input token count divided by OpenVINO raw first-token inference "
            "duration (microseconds converted to seconds)"
        ),
        "tpot_ms": tpot,
        "decode_tps": 1000.0 / tpot,
        "generation_duration_ms": generation,
        "openvino_generation_duration_ms": openvino_generation,
        "openvino_throughput_tps": openvino_throughput,
        "num_input_tokens": input_tokens,
        "num_generated_tokens": generated_tokens,
        "first_token_inference_ms": first_inference_ms,
        "ttft_definition": (
            "generate call entry to first streamed generated text callback"
        ),
    }


def generate_decoded_result(
    pipeline: Any,
    prompt: str,
    config: Any,
    streamer: Any,
) -> tuple[Any, str]:
    """Generate a one-item batch so Python preserves ``DecodedResults``.

    The OpenVINO GenAI Python binding converts scalar-prompt results to ``str``.
    A one-item batch follows the same single-request path while retaining the
    result object's performance metrics.
    """

    generation = pipeline.generate([prompt], config, streamer=streamer)
    if not hasattr(generation, "perf_metrics"):
        raise TypeError(
            "OpenVINO generation did not return DecodedResults performance metrics"
        )
    texts = list(getattr(generation, "texts", ()))
    if len(texts) != 1:
        raise RuntimeError(
            "one-item OpenVINO generation must return exactly one decoded text"
        )
    output = str(texts[0])
    if not output.strip():
        raise RuntimeError("generation returned an empty decoded output")
    return generation, output


def _load_spec(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"worker spec is unreadable: {path}") from error
    if not isinstance(value, dict) or value.get("schema") != SPEC_SCHEMA:
        raise ValueError("worker spec schema is invalid")
    required_text = ("model_path", "device", "prompt")
    for field in required_text:
        if not isinstance(value.get(field), str) or not value[field]:
            raise ValueError(f"worker spec {field} is required")
    if not isinstance(value.get("properties"), dict):
        raise ValueError("worker spec properties must be an object")
    max_new_tokens = value.get("max_new_tokens")
    if (
        not isinstance(max_new_tokens, int)
        or isinstance(max_new_tokens, bool)
        or max_new_tokens <= 1
    ):
        raise ValueError("worker max_new_tokens must exceed one")
    return value


def execute(spec: Mapping[str, Any]) -> dict[str, Any]:
    """Load one pipeline and perform one deterministic generation."""

    import openvino as ov
    import openvino_genai as ov_genai

    properties = materialize_openvino_properties(spec["properties"], ov)
    started_load = time.perf_counter_ns()
    pipeline = ov_genai.LLMPipeline(
        spec["model_path"],
        spec["device"],
        **properties,
    )
    ended_load = time.perf_counter_ns()

    config = ov_genai.GenerationConfig()
    config.max_new_tokens = int(spec["max_new_tokens"])
    config.do_sample = False
    config.ignore_eos = bool(spec.get("ignore_eos", True))
    config.rng_seed = int(spec.get("seed", 42))
    config.apply_chat_template = bool(spec.get("apply_chat_template", False))

    chunks: list[str] = []
    first_token_ns: int | None = None

    def stream(chunk: str) -> bool:
        nonlocal first_token_ns
        if first_token_ns is None:
            first_token_ns = time.perf_counter_ns()
        chunks.append(str(chunk))
        return False

    request_started = time.perf_counter_ns()
    generation, output = generate_decoded_result(
        pipeline,
        spec["prompt"],
        config,
        stream,
    )
    request_ended = time.perf_counter_ns()
    if first_token_ns is None:
        raise RuntimeError("streamer did not observe a generated token")

    metrics = extract_performance_metrics(
        generation.perf_metrics,
        request_ttft_ms=(first_token_ns - request_started) / 1_000_000.0,
        wall_generation_ms=(request_ended - request_started) / 1_000_000.0,
    )
    wall_load_ms = (ended_load - started_load) / 1_000_000.0
    result = {
        "schema": WORKER_SCHEMA,
        **metrics,
        "load_ms": wall_load_ms,
        "openvino_load_ms": metrics["load_ms"],
        "output": output,
        "output_valid": True,
        "streamed_chunk_count": len(chunks),
        "model_path": str(Path(spec["model_path"]).resolve()),
        "device": spec["device"],
        "controlled_test_id": spec.get("controlled_test_id"),
        "context": spec.get("context"),
        "role": spec.get("role"),
    }
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--spec", type=Path, required=True)
    args = parser.parse_args(argv)
    result = execute(_load_spec(args.spec))
    print(
        RESULT_MARKER
        + json.dumps(
            result,
            ensure_ascii=True,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        ),
        flush=True,
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(
            json.dumps(
                {
                    "schema": WORKER_SCHEMA,
                    "worker_error": f"{type(error).__name__}: {error}",
                },
                sort_keys=True,
                separators=(",", ":"),
            ),
            file=sys.stderr,
            flush=True,
        )
        raise
