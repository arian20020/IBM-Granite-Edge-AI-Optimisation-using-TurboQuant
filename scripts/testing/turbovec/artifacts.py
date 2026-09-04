"""Atomic, hash-bound embedding artifacts shared by every retrieval format."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
from typing import Mapping, Sequence
import uuid

import numpy as np

from .dataset_schema import FrozenDataset
from .embedding import DIMENSION, MAX_BATCH, EmbeddingProvider, validate_embeddings


DOCUMENT_FILE = "documents.f32"
QUERY_FILE = "queries.f32"
MANIFEST_FILE = "manifest.json"


def _hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _write_json(path: Path, value: object) -> None:
    payload = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n"
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        stream.write(payload)
        stream.flush()
        os.fsync(stream.fileno())


def _write_matrix(path: Path, values: np.ndarray) -> None:
    matrix = np.ascontiguousarray(values, dtype="<f4")
    with path.open("wb") as stream:
        stream.write(matrix.tobytes(order="C"))
        stream.flush()
        os.fsync(stream.fileno())


def _embed_batches(provider: EmbeddingProvider, texts: Sequence[str], batch_size: int, query: bool) -> np.ndarray:
    rows: list[np.ndarray] = []
    for start in range(0, len(texts), batch_size):
        batch = texts[start:start + batch_size]
        values = provider.embed_queries(batch) if query else provider.embed_documents(batch)
        rows.append(validate_embeddings(values, len(batch)))
    return np.ascontiguousarray(np.concatenate(rows), dtype="<f4")


def generate_embedding_artifact(
    provider: EmbeddingProvider,
    dataset: FrozenDataset,
    target: Path,
    model_identity: Mapping[str, object],
    *,
    batch_size: int,
) -> Path:
    if batch_size < 1 or batch_size > MAX_BATCH:
        raise ValueError(f"batch size must be between 1 and {MAX_BATCH}")
    destination = Path(target)
    if destination.exists():
        raise FileExistsError(destination.name)
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = destination.parent / f".staging-{destination.name}"
    if staging.exists():
        raise FileExistsError(staging.name)
    staging.mkdir()
    try:
        document_values = _embed_batches(provider, [chunk.text for chunk in dataset.chunks], batch_size, False)
        query_values = _embed_batches(provider, [query.text for query in dataset.queries], batch_size, True)
        _write_matrix(staging / DOCUMENT_FILE, document_values)
        _write_matrix(staging / QUERY_FILE, query_values)
        files = []
        for name, rows in ((DOCUMENT_FILE, len(document_values)), (QUERY_FILE, len(query_values))):
            path = staging / name
            files.append({"name": name, "bytes": path.stat().st_size, "sha256": _hash_file(path), "rows": rows})
        manifest = {
            "schema_version": "2.0",
            "scale": dataset.scale,
            "dimension": DIMENSION,
            "batch_size": batch_size,
            "model": dict(model_identity),
            "inputs": {
                "corpus_sha256": dataset.corpus_sha256,
                "query_sha256": dataset.query_sha256,
                "relevance_sha256": dataset.relevance_sha256,
            },
            "files": files,
        }
        _write_json(staging / MANIFEST_FILE, manifest)
        staging.rename(destination)
        return destination
    except BaseException as error:
        failure = destination.parent / f".failed-{destination.name}-{uuid.uuid4().hex}"
        if staging.exists():
            _write_json(staging / "failure.json", {"schema_version": "2.0", "status": "failed", "error_type": type(error).__name__})
            staging.rename(failure)
        raise


def load_embedding_artifact(target: Path, dataset: FrozenDataset) -> tuple[np.ndarray, np.ndarray, dict[str, object]]:
    root = Path(target)
    try:
        manifest = json.loads((root / MANIFEST_FILE).read_text(encoding="utf-8"))
    except Exception as error:
        raise ValueError("invalid embedding artifact manifest") from error
    allowed = {"schema_version", "scale", "dimension", "batch_size", "model", "inputs", "files"}
    if set(manifest) != allowed or manifest["schema_version"] != "2.0":
        raise ValueError("invalid embedding artifact manifest")
    if manifest["scale"] != dataset.scale or manifest["dimension"] != DIMENSION:
        raise ValueError("embedding artifact dataset shape mismatch")
    expected_inputs = {
        "corpus_sha256": dataset.corpus_sha256,
        "query_sha256": dataset.query_sha256,
        "relevance_sha256": dataset.relevance_sha256,
    }
    if manifest["inputs"] != expected_inputs:
        raise ValueError("embedding artifact input identity mismatch")
    records = {record["name"]: record for record in manifest["files"]}
    if set(records) != {DOCUMENT_FILE, QUERY_FILE}:
        raise ValueError("embedding artifact file set mismatch")
    for name, expected_rows in ((DOCUMENT_FILE, dataset.scale), (QUERY_FILE, len(dataset.queries))):
        path = root / name
        record = records[name]
        if (
            not path.is_file()
            or record.get("rows") != expected_rows
            or path.stat().st_size != record.get("bytes")
            or _hash_file(path) != record.get("sha256")
            or path.stat().st_size != expected_rows * DIMENSION * 4
        ):
            raise ValueError("artifact file integrity mismatch")
    documents = np.fromfile(root / DOCUMENT_FILE, dtype="<f4").reshape(dataset.scale, DIMENSION)
    queries = np.fromfile(root / QUERY_FILE, dtype="<f4").reshape(len(dataset.queries), DIMENSION)
    for values in (documents, queries):
        if not np.isfinite(values).all() or not np.allclose(np.linalg.norm(values, axis=1), 1.0, atol=1e-4):
            raise ValueError("embedding artifact values are invalid")
    return np.ascontiguousarray(documents), np.ascontiguousarray(queries), manifest
