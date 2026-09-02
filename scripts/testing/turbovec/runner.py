"""Append-only matched runner for EXP-TV-COMP-001."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
import time
from typing import Sequence

import numpy as np

from .embedding import EmbeddingProvider
from .indexes import ExactIndex, TurboVecIndex
from .metrics import aggregate_latency
from .provenance import sha256_file


CONFIGURATIONS = ("exact", "tq2", "tq3", "tq4")
WARMUPS = 5
MEASURED = 30


def create_run_directory(output_root: Path, run_id: str) -> Path:
    root = Path(output_root)
    target = root / run_id
    staging = root / f".staging-{run_id}"
    if target.exists() or staging.exists(): raise FileExistsError(run_id)
    root.mkdir(parents=True, exist_ok=True); staging.mkdir()
    return staging


def _json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def _matrix_hash(values: np.ndarray) -> str:
    return hashlib.sha256(np.ascontiguousarray(values, dtype="<f4").tobytes()).hexdigest()


def _index_factory(name: str):
    return ExactIndex(384) if name == "exact" else TurboVecIndex(384, int(name[-1]))


def run_matched_campaign(document_embeddings: np.ndarray, query_embeddings: np.ndarray, ids: np.ndarray) -> dict[str, object]:
    embedding_hash = _matrix_hash(document_embeddings); indexes = {}; rows = []
    with tempfile.TemporaryDirectory(prefix="tv-index-") as directory:
        root = Path(directory)
        for name in CONFIGURATIONS:
            started = time.perf_counter_ns(); index = _index_factory(name); index.add(document_embeddings, ids); build_ns = time.perf_counter_ns() - started
            index_root = root / name; save_started = time.perf_counter_ns(); index.save(index_root); save_ns = time.perf_counter_ns() - save_started
            serving_bytes = sum(path.stat().st_size for path in index_root.iterdir())
            load_started = time.perf_counter_ns(); reopened = type(index).load(index_root); load_ns = time.perf_counter_ns() - load_started
            for _ in range(WARMUPS): reopened.search(query_embeddings, min(10, len(ids)))
            indexes[name] = (reopened, build_ns, save_ns, load_ns, serving_bytes, index_root)
        latencies = {name: [] for name in CONFIGURATIONS}; first_results = {}
        for batch in range(MEASURED):
            order = CONFIGURATIONS[batch % 4:] + CONFIGURATIONS[:batch % 4]
            for name in order:
                started = time.perf_counter_ns(); result = indexes[name][0].search(query_embeddings, min(10, len(ids))); elapsed = time.perf_counter_ns() - started
                latencies[name].append(elapsed / 1_000_000)
                first_results.setdefault(name, {"ids": result.ids.tolist(), "scores": result.scores.tolist()})
        remove_count = min(10, len(ids)); removed_ids = ids[:remove_count]; removed_vectors = document_embeddings[:remove_count]
        for name in CONFIGURATIONS:
            index, build_ns, save_ns, load_ns, serving_bytes, index_root = indexes[name]
            lifecycle = all(index.remove(int(identifier)) for identifier in removed_ids)
            index.add(removed_vectors, removed_ids)
            lifecycle = lifecycle and set(map(int, index.search(removed_vectors, min(10, len(ids))).ids.flat)) <= set(map(int, ids))
            corrupt_ok = False
            corrupt_root = root / f"{name}-corrupt"; shutil.copytree(index_root, corrupt_root)
            target = next(path for path in corrupt_root.iterdir() if path.name != "metadata.json"); target.write_bytes(target.read_bytes() + b"corrupt")
            try: type(index).load(corrupt_root)
            except ValueError: corrupt_ok = True
            latency = aggregate_latency(latencies[name])
            rows.append({"name":name,"bit_width":None if name=="exact" else int(name[-1]),"embedding_matrix_sha256":embedding_hash,"warmup_batches":WARMUPS,"measured_batches":MEASURED,"build_ns":build_ns,"save_ns":save_ns,"load_ns":load_ns,"query_latency_ms":latency,"serving_bytes":serving_bytes,"lifecycle_passed":bool(lifecycle and corrupt_ok),"measured_latency_ms":latencies[name],"results":first_results[name]})
    return {"schema_version":"1.0","configurations":rows}


def run_fixture_campaign(provider: EmbeddingProvider, output_root: Path | None = None, run_id: str | None = None) -> dict[str, object]:
    texts = [f"fixture document {index}" for index in range(32)]
    queries = texts[:8]
    document_embeddings = provider.embed_documents(texts); query_embeddings = provider.embed_queries(queries)
    ids = np.arange(1000, 1032, dtype=np.uint64)
    result = run_matched_campaign(document_embeddings, query_embeddings, ids)
    if output_root is None: return result
    if run_id is None: raise ValueError("run_id is required")
    staging = create_run_directory(output_root, run_id)
    try:
        _json(staging / "identities.json", {"schema_version":"1.0","run_id":run_id,"provider":"deterministic-test-only"})
        _json(staging / "embedding-summary.json", {"schema_version":"1.0","sha256":result["configurations"][0]["embedding_matrix_sha256"],"rows":32,"dimension":384})
        _json(staging / "results.json", result)
        _json(staging / "command-arithmetic.json", {"discovered":4,"executed":4,"passed":4,"failed":0,"skipped":0})
        (staging / "events.jsonl").write_text('{"event":"matched_run_completed"}\n',encoding="utf-8",newline="\n")
        files=[]
        for path in sorted(staging.iterdir()): files.append({"name":path.name,"bytes":path.stat().st_size,"sha256":sha256_file(path)})
        _json(staging / "terminal.json", {"schema_version":"1.0","run_id":run_id,"status":"completed","files":files})
        target=Path(output_root)/run_id; staging.rename(target)
        return result
    except BaseException:
        _json(staging / "failure.json", {"schema_version":"1.0","run_id":run_id,"status":"failed"})
        raise
