"""Matched, counterbalanced and repetition-complete TurboVec benchmarks."""

from __future__ import annotations

import hashlib
import math
from pathlib import Path
import shutil
import time
from typing import Callable, Iterable

import numpy as np

from .indexes import ExactIndex, TurboVecIndex
from .schedule import CONFIGURATIONS, RepetitionSchedule
from .storage import StorageComponents


ValidityProbe = Callable[[str, str], tuple[bool, str]]


def _matrix_hash(values: np.ndarray) -> str:
    return hashlib.sha256(np.ascontiguousarray(values, dtype="<f4").tobytes()).hexdigest()


def _index(name: str):
    return ExactIndex() if name == "exact" else TurboVecIndex(bit_width=int(name[-1]))


def _percentile(values: list[float], proportion: float) -> float:
    ordered = sorted(values)
    return ordered[max(0, math.ceil(len(ordered) * proportion) - 1)]


def _working_set() -> int:
    try:
        import psutil  # type: ignore
        return int(psutil.Process().memory_info().rss)
    except ImportError:
        return 0


def _storage(root: Path, shared_document_metadata_bytes: int) -> dict[str, int]:
    vector = index_metadata = serialization = auxiliary = 0
    for path in root.iterdir():
        size = path.stat().st_size
        if path.name.startswith("vectors.") or path.name.startswith("index-"):
            vector += size
        elif path.name == "ids.npy":
            auxiliary += size
        elif path.name == "metadata.json":
            index_metadata += size
        else:
            serialization += size
    return StorageComponents(
        vector, index_metadata, shared_document_metadata_bytes, serialization, auxiliary
    ).to_dict()


def _run_configuration(
    name: str,
    documents: np.ndarray,
    queries: np.ndarray,
    ids: np.ndarray,
    root: Path,
    *,
    warmup_batches: int,
    measured_batches: int,
    shared_document_metadata_bytes: int,
) -> dict[str, object]:
    baseline_memory = _working_set()
    peak_memory = baseline_memory
    started = time.perf_counter_ns()
    index = _index(name)
    index.add(documents, ids)
    build_ns = time.perf_counter_ns() - started
    peak_memory = max(peak_memory, _working_set())
    reference = index.search(queries, min(10, len(ids)))
    index_root = root / "index"
    started = time.perf_counter_ns()
    index.save(index_root)
    save_ns = time.perf_counter_ns() - started
    storage = _storage(index_root, shared_document_metadata_bytes)
    index_type = type(index)
    del index
    started = time.perf_counter_ns()
    reopened = index_type.load(index_root)
    load_ns = time.perf_counter_ns() - started
    peak_memory = max(peak_memory, _working_set())
    started = time.perf_counter_ns()
    cold = reopened.search(queries, min(10, len(ids)))
    cold_ms = (time.perf_counter_ns() - started) / 1_000_000
    save_reload_integrity = np.array_equal(reference.ids, cold.ids) and np.allclose(reference.scores, cold.scores, atol=1e-6)
    for _ in range(warmup_batches):
        reopened.search(queries, min(10, len(ids)))
    latencies = []
    measured = None
    for _ in range(measured_batches):
        started = time.perf_counter_ns()
        measured = reopened.search(queries, min(10, len(ids)))
        latencies.append((time.perf_counter_ns() - started) / 1_000_000)
        peak_memory = max(peak_memory, _working_set())
    corrupt_root = root / "corrupt"
    shutil.copytree(index_root, corrupt_root)
    corrupt_target = next(path for path in corrupt_root.iterdir() if path.name != "metadata.json")
    with corrupt_target.open("ab") as stream:
        stream.write(b"corrupt")
    corruption_rejected = False
    try:
        index_type.load(corrupt_root)
    except ValueError:
        corruption_rejected = True
    del reopened
    shutil.rmtree(corrupt_root)
    shutil.rmtree(index_root)
    cleanup_completed = not corrupt_root.exists() and not index_root.exists()
    if measured is None:
        raise ValueError("at least one measured batch is required")
    return {
        "name": name,
        "embedding_matrix_sha256": _matrix_hash(documents),
        "query_matrix_sha256": _matrix_hash(queries),
        "build_ns": build_ns,
        "save_ns": save_ns,
        "load_ns": load_ns,
        "cold_query_latency_ms": cold_ms,
        "warmup_batches": warmup_batches,
        "warm_query_latency_ms": latencies,
        "latency_summary_ms": {
            "p50": _percentile(latencies, 0.50),
            "p95": _percentile(latencies, 0.95),
            "p99": _percentile(latencies, 0.99),
            "throughput_queries_per_second": len(queries) * 1000.0 / (sum(latencies) / len(latencies)),
        },
        "storage": storage,
        "memory": {
            "baseline_process_working_set_bytes": baseline_memory,
            "peak_process_working_set_bytes": peak_memory,
            "incremental_peak_process_working_set_bytes": max(0, peak_memory - baseline_memory),
        },
        "lifecycle": {
            "save_reload_integrity": bool(save_reload_integrity),
            "corruption_rejected": corruption_rejected,
            "cleanup_completed": cleanup_completed,
        },
        "result_ids": measured.ids.tolist(),
        "result_scores": measured.scores.tolist(),
    }


def run_scale_benchmark(
    document_embeddings: np.ndarray,
    query_embeddings: np.ndarray,
    ids: np.ndarray,
    *,
    scale: int,
    schedule: Iterable[RepetitionSchedule],
    output_root: Path,
    warmup_batches: int = 5,
    measured_batches: int = 30,
    shared_document_metadata_bytes: int,
    validity_probe: ValidityProbe | None = None,
) -> dict[str, object]:
    if len(document_embeddings) != scale or len(ids) != scale or len(query_embeddings) == 0:
        raise ValueError("benchmark input shape mismatch")
    if warmup_batches < 0 or measured_batches <= 0:
        raise ValueError("invalid warmup or measured batch count")
    probe = validity_probe or (lambda _name, _phase: (True, ""))
    root = Path(output_root)
    root.mkdir(parents=True, exist_ok=True)
    repetitions = []
    for item in schedule:
        repetition_root = root / f"repetition-{item.repetition:02d}"
        if repetition_root.exists():
            raise FileExistsError(repetition_root.name)
        repetition_root.mkdir()
        ordered_queries = np.ascontiguousarray(query_embeddings[list(item.query_order)], dtype=np.float32)
        configurations: dict[str, object] = {}
        reasons: list[str] = []
        try:
            for name in item.configuration_order:
                before_ok, before_reason = probe(name, "before")
                if not before_ok:
                    reasons.append(before_reason or f"{name} before-state invalid")
                format_root = repetition_root / name
                format_root.mkdir()
                configurations[name] = _run_configuration(
                    name, document_embeddings, ordered_queries, ids, format_root,
                    warmup_batches=warmup_batches, measured_batches=measured_batches,
                    shared_document_metadata_bytes=shared_document_metadata_bytes,
                )
                after_ok, after_reason = probe(name, "after")
                if not after_ok:
                    reasons.append(after_reason or f"{name} after-state invalid")
            repetitions.append({
                "repetition": item.repetition,
                "seed": item.seed,
                "configuration_order": item.configuration_order,
                "query_order": item.query_order,
                "valid": not reasons,
                "invalidation_reasons": tuple(dict.fromkeys(reasons)),
                "configurations": configurations,
            })
        finally:
            if repetition_root.exists():
                shutil.rmtree(repetition_root)
    return {"schema_version": "2.0", "scale": scale, "repetitions": repetitions}
