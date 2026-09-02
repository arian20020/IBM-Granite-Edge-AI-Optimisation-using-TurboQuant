"""Granite embedding adapters for the isolated feasibility campaign."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
from typing import Protocol, Sequence

import numpy as np

from .provenance import sha256_file


DIMENSION = 384
MAX_BATCH = 16
MAX_AGGREGATE_TOKENS = 12_288


class EmbeddingProvider(Protocol):
    def embed_documents(self, texts: Sequence[str]) -> np.ndarray: ...
    def embed_queries(self, texts: Sequence[str]) -> np.ndarray: ...


def validate_embeddings(values: object, expected_rows: int, expected_dim: int = DIMENSION) -> np.ndarray:
    array = np.asarray(values, dtype=np.float32)
    if array.shape != (expected_rows, expected_dim):
        raise ValueError(f"embedding shape must be ({expected_rows}, {expected_dim})")
    if not np.isfinite(array).all(): raise ValueError("embeddings must be finite")
    norms = np.linalg.norm(array, axis=1)
    if (norms <= 0).any(): raise ValueError("zero embeddings are not allowed")
    return np.ascontiguousarray(array)


class NormalizingEmbeddingProvider:
    def __init__(self, provider: EmbeddingProvider): self._provider = provider
    def _normalize(self, values: object, rows: int) -> np.ndarray:
        array = validate_embeddings(values, rows)
        return np.ascontiguousarray(array / np.linalg.norm(array, axis=1, keepdims=True), dtype=np.float32)
    def embed_documents(self, texts: Sequence[str]) -> np.ndarray: return self._normalize(self._provider.embed_documents(texts), len(texts))
    def embed_queries(self, texts: Sequence[str]) -> np.ndarray: return self._normalize(self._provider.embed_queries(texts), len(texts))


class DeterministicEmbeddingProvider:
    """Unit-test provider. It is deliberately unavailable to measured runs."""
    def __init__(self, measured: bool = False):
        if measured: raise ValueError("deterministic test provider is forbidden for measured runs")
    @staticmethod
    def _embed(texts: Sequence[str], prefix: str) -> np.ndarray:
        rows = []
        for text in texts:
            material = bytearray(); counter = 0
            while len(material) < DIMENSION * 4:
                material.extend(hashlib.sha256(f"{prefix}:{counter}:{text}".encode()).digest()); counter += 1
            raw = np.frombuffer(bytes(material[:DIMENSION * 4]), dtype=np.uint32).astype(np.float32)
            rows.append((raw / np.float32(2**32)) - np.float32(0.5))
        return NormalizingEmbeddingProvider(FakeArrayProvider(np.asarray(rows, np.float32))).embed_documents(texts)
    def embed_documents(self, texts: Sequence[str]) -> np.ndarray: return self._embed(texts, "passage")
    def embed_queries(self, texts: Sequence[str]) -> np.ndarray: return self._embed(texts, "query")


class FakeArrayProvider:
    def __init__(self, values: np.ndarray): self.values = values
    def embed_documents(self, texts: Sequence[str]) -> np.ndarray: return self.values[:len(texts)]
    def embed_queries(self, texts: Sequence[str]) -> np.ndarray: return self.values[:len(texts)]


class OpenVinoGraniteEmbeddingProvider:
    def __init__(self, model_root: Path, device: str = "CPU"):
        if device != "CPU": raise ValueError("controlled device must be CPU")
        lock_model_assets(model_root)
        import openvino_genai  # type: ignore
        self._pipeline = openvino_genai.TextEmbeddingPipeline(str(Path(model_root).resolve()), device)
        self.requested_device = device; self.actual_device = "CPU"
    @staticmethod
    def _check_batch(texts: Sequence[str]) -> None:
        if not texts or len(texts) > MAX_BATCH: raise ValueError("embedding batch size out of range")
        if sum(len(text.split()) for text in texts) > MAX_AGGREGATE_TOKENS: raise ValueError("aggregate token limit exceeded")
    def embed_documents(self, texts: Sequence[str]) -> np.ndarray:
        self._check_batch(texts); return NormalizingEmbeddingProvider(FakeArrayProvider(np.asarray(self._pipeline.embed_documents(list(texts)), np.float32))).embed_documents(texts)
    def embed_queries(self, texts: Sequence[str]) -> np.ndarray:
        self._check_batch(texts); values = [self._pipeline.embed_query(text) for text in texts]
        return NormalizingEmbeddingProvider(FakeArrayProvider(np.asarray(values, np.float32))).embed_queries(texts)


def _is_reparse(path: Path) -> bool:
    return path.is_symlink() or bool(getattr(path.stat(), "st_file_attributes", 0) & 0x400)


def lock_model_assets(model_root: Path) -> dict[str, object]:
    root = Path(model_root).resolve(strict=True)
    if not root.is_dir() or _is_reparse(root): raise ValueError("model root must be a regular directory without reparse points")
    metadata = root / "snapshot.json"
    if not metadata.is_file(): raise ValueError("model snapshot metadata is missing")
    revision = str(json.loads(metadata.read_text(encoding="utf-8-sig")).get("revision", ""))
    if not re.fullmatch(r"[0-9a-f]{40,64}", revision): raise ValueError("model revision must be immutable")
    records = []
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if path.is_dir(): continue
        if not path.is_file() or _is_reparse(path): raise ValueError("model asset contains a link or non-file")
        records.append({"path": path.relative_to(root).as_posix(), "bytes": path.stat().st_size, "sha256": sha256_file(path)})
    digest = hashlib.sha256(json.dumps(records, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    return {"repository": "ibm-granite/granite-embedding-small-english-r2", "revision": revision, "dimension": DIMENSION, "tree_sha256": digest, "files": records}
