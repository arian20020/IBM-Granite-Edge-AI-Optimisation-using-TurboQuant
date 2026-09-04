"""Fail-closed formal scale runner using frozen embedding artifacts."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
from typing import Mapping

import numpy as np

from .artifacts import load_embedding_artifact
from .benchmark import run_scale_benchmark
from .dataset import build_frozen_dataset
from .evaluation import evaluate_repetition, summarize_repetitions
from .schedule import build_schedule


def _hash(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _write(path: Path, value: object) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        stream.write("\n")
        stream.flush()
        os.fsync(stream.fileno())


def _metadata_bytes(dataset) -> int:
    records = [
        {"chunk_id": chunk.chunk_id, "content_sha256": chunk.content_sha256,
         "source_document_id": chunk.source_document_id, "page_number": chunk.page_number}
        for chunk in dataset.chunks
    ]
    return len(json.dumps(records, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8"))


def run_formal_scale(
    artifact_root: Path,
    readiness_root: Path,
    output_root: Path,
    *,
    scale: int,
    seed: int,
    repetitions: int = 5,
    warmup_batches: int = 5,
    measured_batches: int = 30,
    enforce_recovery: bool = True,
    identity: Mapping[str, object] | None = None,
) -> Path:
    readiness_path = Path(readiness_root) / "decision.json"
    readiness = json.loads(readiness_path.read_text(encoding="utf-8"))
    if readiness.get("ready") is not True or float(readiness.get("coverage_seconds", 0)) < 60:
        raise ValueError("formal measurement requires a passing 60-second readiness gate")
    dataset = build_frozen_dataset(scale)
    documents, queries, artifact_manifest = load_embedding_artifact(Path(artifact_root), dataset)
    destination = Path(output_root)
    if destination.exists():
        raise FileExistsError(destination)
    destination.mkdir(parents=True)
    try:
        benchmark = run_scale_benchmark(
            documents, queries, np.arange(scale, dtype=np.uint64),
            scale=scale, schedule=build_schedule(seed=seed, repetitions=repetitions, query_count=len(queries)),
            output_root=destination / "scratch", warmup_batches=warmup_batches,
            measured_batches=measured_batches, shared_document_metadata_bytes=_metadata_bytes(dataset),
            enforce_recovery=enforce_recovery,
        )
        (destination / "scratch").rmdir()
        evaluated = []
        for raw in benchmark["repetitions"]:
            quality = evaluate_repetition(dataset, raw)
            evaluated.append({
                "repetition": raw["repetition"], "seed": raw["seed"],
                "configuration_order": raw["configuration_order"],
                "valid": quality["valid"], "invalidation_reasons": raw["invalidation_reasons"],
                "evaluation_query_counts": quality["evaluation_query_counts"],
                "configurations": quality["configurations"],
            })
        valid_count = sum(item["valid"] is True for item in evaluated)
        summary = {
            "schema_version": "2.0", "campaign_id": "turbovec-production-scale-final-evaluation-v2",
            "experiment_id": "EXP-TV-COMP-001", "scale": scale, "seed": seed,
            "requested_repetitions": repetitions, "valid_repetitions": valid_count,
            "warmup_batches": warmup_batches, "measured_batches": measured_batches,
            "dataset": {"corpus_sha256": dataset.corpus_sha256, "query_sha256": dataset.query_sha256,
                        "relevance_sha256": dataset.relevance_sha256},
            "embedding_manifest_sha256": _hash(Path(artifact_root) / "manifest.json"),
            "readiness_decision_sha256": _hash(readiness_path), "identity": dict(identity or {}),
            "statistics": summarize_repetitions(evaluated),
        }
        _write(destination / "benchmark.json", benchmark)
        _write(destination / "evaluation.json", {"schema_version": "2.0", "repetitions": evaluated})
        _write(destination / "summary.json", summary)
        files = []
        for name in ("benchmark.json", "evaluation.json", "summary.json"):
            path = destination / name
            files.append({"name": name, "bytes": path.stat().st_size, "sha256": _hash(path)})
        _write(destination / "terminal.json", {"schema_version": "2.0", "status": "completed", "files": files})
        return destination
    except BaseException as error:
        _write(destination / "failure.json", {"schema_version": "2.0", "status": "failed", "error_type": type(error).__name__, "message": str(error)})
        raise
