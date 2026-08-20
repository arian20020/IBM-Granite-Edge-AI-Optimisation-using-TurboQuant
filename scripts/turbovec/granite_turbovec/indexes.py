"""Validated exact and TurboVec index adapters."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from .contracts import ResearchError


def validate_vectors(
    vectors: Any,
    *,
    dimension: int | None = None,
    expected_count: int | None = None,
    allow_empty: bool = False,
):
    import numpy as np

    try:
        array = np.asarray(vectors)
    except Exception:
        raise ResearchError("vectors-type-invalid") from None
    if array.ndim != 2:
        raise ResearchError("vectors-shape-invalid")
    if array.dtype != np.dtype(np.float32):
        raise ResearchError("vector-dtype-invalid")
    if dimension is not None and (type(dimension) is not int or dimension <= 0 or array.shape[1] != dimension):
        raise ResearchError("vectors-dimension-invalid")
    if expected_count is not None and array.shape[0] != expected_count:
        raise ResearchError("vectors-count-invalid")
    if array.shape[0] == 0 and not allow_empty:
        raise ResearchError("vectors-empty")
    converted = np.array(array, dtype=np.float32, order="C", copy=True)
    if not np.isfinite(converted).all():
        raise ResearchError("vectors-nonfinite")
    return converted


def validate_ids(ids: Any, *, expected_count: int | None = None):
    import numpy as np

    try:
        array = np.asarray(ids)
    except Exception:
        raise ResearchError("ids-type-invalid") from None
    if array.ndim != 1:
        raise ResearchError("ids-shape-invalid")
    if expected_count is not None and array.shape[0] != expected_count:
        raise ResearchError("ids-count-invalid")
    if array.shape[0] == 0:
        raise ResearchError("ids-empty")
    if array.dtype.kind not in "ui":
        raise ResearchError("ids-type-invalid")
    if array.dtype.kind == "i" and bool((array < 0).any()):
        raise ResearchError("ids-type-invalid")
    try:
        converted = np.array(array, dtype=np.uint64, order="C", copy=True)
    except (TypeError, ValueError, OverflowError):
        raise ResearchError("ids-type-invalid") from None
    if np.unique(converted).shape[0] != converted.shape[0]:
        raise ResearchError("ids-duplicate")
    return converted


def _validate_dimension(dimension: Any) -> int:
    if type(dimension) is not int or dimension <= 0:
        raise ResearchError("index-dimension-invalid")
    return dimension


def _validate_k(k: Any, corpus_size: int) -> int:
    if type(k) is not int or k <= 0 or k > corpus_size:
        raise ResearchError("search-k-invalid")
    return k


def _validate_queries(queries: Any, dimension: int):
    try:
        return validate_vectors(queries, dimension=dimension)
    except ResearchError as error:
        mapping = {
            "vectors-shape-invalid": "query-shape-invalid",
            "vectors-dimension-invalid": "query-dimension-invalid",
            "vectors-empty": "query-empty",
            "vectors-nonfinite": "query-nonfinite",
            "vector-dtype-invalid": "query-type-invalid",
        }
        raise ResearchError(mapping.get(error.code, "query-invalid")) from None


class Float32Index:
    def __init__(self, vectors: Any, ids: Any) -> None:
        validated_vectors = validate_vectors(vectors)
        validated_ids = validate_ids(ids, expected_count=validated_vectors.shape[0])
        self._vectors = validated_vectors
        self._vectors.setflags(write=False)
        self._ids = validated_ids
        self._ids.setflags(write=False)
        self.dimension = validated_vectors.shape[1]

    def search(self, queries: Any, k: int):
        import numpy as np

        query_matrix = _validate_queries(queries, self.dimension)
        selected_k = _validate_k(k, self._vectors.shape[0])
        scores = query_matrix @ self._vectors.T
        if not np.isfinite(scores).all():
            raise ResearchError("index-search-result-invalid")

        result_scores = np.empty((query_matrix.shape[0], selected_k), dtype=np.float32)
        result_ids = np.empty((query_matrix.shape[0], selected_k), dtype=np.uint64)
        for row_number, row_scores in enumerate(scores):
            order = np.lexsort((self._ids, -row_scores))[:selected_k]
            result_scores[row_number] = row_scores[order]
            result_ids[row_number] = self._ids[order]
        return result_scores, result_ids


class TurboVecIndex:
    def __init__(self, dimension: int, *, bits: int, turbovec_module: Any = None) -> None:
        self.dimension = _validate_dimension(dimension)
        if type(bits) is not int or bits not in (2, 4):
            raise ResearchError("index-bits-invalid")
        self.bits = bits
        module = _load_turbovec(turbovec_module)
        self._module = module
        try:
            self._index = module.IdMapIndex(dim=self.dimension, bit_width=bits)
        except Exception:
            raise ResearchError("index-create-failed") from None
        self._count = 0

    def add_with_ids(self, vectors: Any, ids: Any) -> None:
        matrix = validate_vectors(vectors, dimension=self.dimension)
        stable_ids = validate_ids(ids, expected_count=matrix.shape[0])
        try:
            self._index.add_with_ids(matrix, stable_ids)
        except Exception:
            raise ResearchError("index-add-failed") from None
        try:
            self._count = len(self._index)
        except Exception:
            self._count += matrix.shape[0]

    def search(self, queries: Any, k: int):
        import numpy as np

        matrix = _validate_queries(queries, self.dimension)
        selected_k = _validate_k(k, self._count)
        try:
            result = self._index.search(matrix, selected_k)
        except Exception:
            raise ResearchError("index-search-failed") from None
        if not isinstance(result, tuple) or len(result) != 2:
            raise ResearchError("index-search-result-invalid")
        scores, ids = result
        try:
            score_array = np.asarray(scores)
            id_array = np.asarray(ids)
        except Exception:
            raise ResearchError("index-search-result-invalid") from None
        expected_shape = (matrix.shape[0], selected_k)
        if score_array.shape != expected_shape or id_array.shape != expected_shape:
            raise ResearchError("index-search-result-invalid")
        if score_array.dtype.kind != "f" or id_array.dtype.kind not in "ui":
            raise ResearchError("index-search-result-invalid")
        if id_array.dtype.kind == "i" and bool((id_array < 0).any()):
            raise ResearchError("index-search-result-invalid")
        normalized_scores = np.array(score_array, dtype=np.float32, order="C", copy=True)
        normalized_ids = np.array(id_array, dtype=np.uint64, order="C", copy=True)
        if not np.isfinite(normalized_scores).all():
            raise ResearchError("index-search-result-invalid")
        return normalized_scores, normalized_ids

    def write(self, path: str | Path) -> None:
        try:
            self._index.write(str(Path(path)), durable=True)
        except Exception:
            raise ResearchError("index-write-failed") from None

    @classmethod
    def load(cls, path: str | Path, *, turbovec_module: Any = None) -> "TurboVecIndex":
        module = _load_turbovec(turbovec_module)
        try:
            backend = module.IdMapIndex.load(str(Path(path)))
            dimension = int(backend.dim)
            bits = int(backend.bit_width)
            count = len(backend)
        except Exception:
            raise ResearchError("index-load-failed") from None
        if dimension <= 0 or bits not in (2, 4) or count < 0:
            raise ResearchError("index-load-failed")
        result = cls.__new__(cls)
        result.dimension = dimension
        result.bits = bits
        result._module = module
        result._index = backend
        result._count = count
        return result


def _load_turbovec(module: Any):
    if module is not None:
        return module
    try:
        import turbovec
    except Exception:
        raise ResearchError("index-dependency-unavailable") from None
    return turbovec
