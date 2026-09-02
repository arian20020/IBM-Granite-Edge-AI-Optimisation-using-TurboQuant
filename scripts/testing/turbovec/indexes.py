"""Matched Exact FP32 and TurboVec index adapters."""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import re

import numpy as np

from .provenance import sha256_file


@dataclass(frozen=True)
class SearchBatch:
    scores: np.ndarray
    ids: np.ndarray


def stable_uint64_id(chunk_sha256: str) -> int:
    if not re.fullmatch(r"[0-9a-f]{64}", chunk_sha256): raise ValueError("chunk ID must be lowercase SHA-256")
    return int.from_bytes(bytes.fromhex(chunk_sha256)[:8], "big", signed=False)


def _vectors(values: object, dim: int, allow_empty: bool = False) -> np.ndarray:
    if not isinstance(values, np.ndarray) or values.dtype != np.float32 or values.ndim != 2 or values.shape[1] != dim:
        raise ValueError("vectors must be a two-dimensional FP32 matrix with the controlled dimension")
    if not values.flags.c_contiguous: raise ValueError("vectors must be C-contiguous")
    if not allow_empty and values.shape[0] == 0: raise ValueError("vector matrix cannot be empty")
    if not np.isfinite(values).all() or (np.linalg.norm(values, axis=1) <= 0).any(): raise ValueError("vectors must be finite and nonzero")
    return values


def _ids(values: object, rows: int) -> np.ndarray:
    if not isinstance(values, np.ndarray) or values.dtype != np.uint64 or values.shape != (rows,): raise ValueError("IDs must be uint64 and row-matched")
    if len(set(map(int, values))) != rows: raise ValueError("duplicate IDs are not allowed")
    return values


def _write_metadata(root: Path, implementation: str, dim: int, bits: int | None, files: list[Path], ids: set[int]) -> None:
    records = [{"name": p.name, "bytes": p.stat().st_size, "sha256": sha256_file(p)} for p in files]
    payload = {"schema_version": "1.0", "implementation": implementation, "dimension": dim, "bit_width": bits, "count": len(ids), "files": records}
    (root / "metadata.json").write_text(json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")


def _load_metadata(root: Path, implementation: str) -> dict:
    try: payload = json.loads((root / "metadata.json").read_text(encoding="utf-8"))
    except Exception as error: raise ValueError("invalid index metadata") from error
    if payload.get("implementation") != implementation: raise ValueError("index implementation mismatch")
    for record in payload.get("files", []):
        path = root / record["name"]
        if not path.is_file() or path.stat().st_size != record["bytes"] or sha256_file(path) != record["sha256"]: raise ValueError("index file integrity mismatch")
    return payload


class ExactIndex:
    def __init__(self, dim: int = 384):
        self.dim = dim; self._vectors = np.empty((0, dim), np.float32); self._ids = np.empty((0,), np.uint64)
    def add(self, vectors: np.ndarray, ids: np.ndarray) -> None:
        values = _vectors(vectors, self.dim); identifiers = _ids(ids, len(values))
        if set(map(int, identifiers)) & set(map(int, self._ids)): raise ValueError("duplicate existing ID")
        self._vectors = np.ascontiguousarray(np.concatenate((self._vectors, values))); self._ids = np.concatenate((self._ids, identifiers))
    def remove(self, identifier: int) -> bool:
        positions = np.flatnonzero(self._ids == np.uint64(identifier))
        if not len(positions): return False
        keep = np.ones(len(self._ids), dtype=bool); keep[positions[0]] = False
        self._vectors = np.ascontiguousarray(self._vectors[keep]); self._ids = self._ids[keep]; return True
    def search(self, queries: np.ndarray, k: int) -> SearchBatch:
        query = _vectors(queries, self.dim, allow_empty=True)
        if k <= 0: raise ValueError("k must be positive")
        limit = min(k, len(self._ids))
        if len(query) == 0: return SearchBatch(np.empty((0, limit), np.float32), np.empty((0, limit), np.uint64))
        scores = query @ self._vectors.T; output_ids = np.empty((len(query), limit), np.uint64); output_scores = np.empty((len(query), limit), np.float32)
        for row in range(len(query)):
            order = np.lexsort((self._ids, -scores[row]))[:limit]; output_ids[row] = self._ids[order]; output_scores[row] = scores[row, order]
        return SearchBatch(output_scores, output_ids)
    def save(self, root: Path) -> None:
        root.mkdir(parents=True, exist_ok=True); vp=root/"vectors.npy"; ip=root/"ids.npy"; np.save(vp,self._vectors,allow_pickle=False); np.save(ip,self._ids,allow_pickle=False)
        _write_metadata(root,"exact",self.dim,None,[vp,ip],set(map(int,self._ids)))
    @classmethod
    def load(cls, root: Path):
        payload=_load_metadata(root,"exact"); result=cls(int(payload["dimension"])); vectors=np.load(root/"vectors.npy",allow_pickle=False); ids=np.load(root/"ids.npy",allow_pickle=False); result.add(vectors,ids)
        if len(ids)!=payload["count"]: raise ValueError("index count mismatch")
        return result


class TurboVecIndex:
    def __init__(self, dim: int = 384, bit_width: int = 4):
        if bit_width not in (2,3,4): raise ValueError("TurboVec bit width must be 2, 3, or 4")
        import turbovec
        self.dim=dim; self.bit_width=bit_width; self._index=turbovec.IdMapIndex(dim=dim,bit_width=bit_width); self._ids:set[int]=set()
    def add(self, vectors: np.ndarray, ids: np.ndarray) -> None:
        values=_vectors(vectors,self.dim); identifiers=_ids(ids,len(values)); incoming=set(map(int,identifiers))
        if incoming & self._ids: raise ValueError("duplicate existing ID")
        self._index.add_with_ids(values,identifiers); self._ids |= incoming
    def remove(self, identifier: int) -> bool:
        removed=bool(self._index.remove(int(identifier)))
        if removed:self._ids.discard(int(identifier))
        return removed
    def search(self, queries: np.ndarray, k: int) -> SearchBatch:
        query=_vectors(queries,self.dim,allow_empty=True)
        if k<=0: raise ValueError("k must be positive")
        limit=min(k,len(self._ids))
        if len(query)==0:return SearchBatch(np.empty((0,limit),np.float32),np.empty((0,limit),np.uint64))
        scores,ids=self._index.search(query,limit); scores=np.asarray(scores,np.float32); ids=np.asarray(ids,np.uint64)
        if scores.shape!=(len(query),limit) or ids.shape!=scores.shape or not np.isfinite(scores).all() or not set(map(int,ids.flat))<=self._ids: raise ValueError("invalid TurboVec search result")
        return SearchBatch(np.ascontiguousarray(scores),np.ascontiguousarray(ids))
    def save(self, root: Path) -> None:
        root.mkdir(parents=True,exist_ok=True); index_path=root/f"index-{self.bit_width}.tvim"; ids_path=root/"ids.npy"; self._index.write(str(index_path)); np.save(ids_path,np.asarray(sorted(self._ids),np.uint64),allow_pickle=False)
        _write_metadata(root,"turbovec",self.dim,self.bit_width,[index_path,ids_path],self._ids)
    @classmethod
    def load(cls, root: Path):
        payload=_load_metadata(root,"turbovec"); result=cls(int(payload["dimension"]),int(payload["bit_width"])); import turbovec
        result._index=turbovec.IdMapIndex.load(str(root/f"index-{result.bit_width}.tvim")); result._ids=set(map(int,np.load(root/"ids.npy",allow_pickle=False)))
        if len(result._ids)!=payload["count"] or len(result._index)!=payload["count"]: raise ValueError("index count mismatch")
        return result
