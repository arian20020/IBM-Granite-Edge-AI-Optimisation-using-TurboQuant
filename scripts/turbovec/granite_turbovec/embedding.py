"""Offline-only text embedding adapter for controlled TurboVec research."""

from __future__ import annotations

from pathlib import Path
from typing import Any, Iterable, Protocol, Sequence, runtime_checkable

from .contracts import ResearchError


MODEL_IDENTITY = "BAAI/bge-small-en-v1.5"
MODEL_DIMENSION = 384


@runtime_checkable
class Embedder(Protocol):
    @property
    def dimension(self) -> int: ...

    @property
    def model_identity(self) -> str: ...

    def embed_documents(self, texts: Sequence[str]) -> Any: ...

    def embed_queries(self, texts: Sequence[str]) -> Any: ...


class FastEmbedder:
    """A fail-closed FastEmbed adapter that never downloads a model."""

    def __init__(self, cache_root: str | Path, *, fastembed_module: Any = None) -> None:
        try:
            cache = Path(cache_root)
        except (TypeError, ValueError, OSError):
            raise ResearchError("embedding-cache-invalid") from None
        try:
            cache_is_directory = cache.is_dir()
        except OSError:
            raise ResearchError("embedding-cache-invalid") from None
        if not cache_is_directory:
            raise ResearchError("embedding-cache-invalid")

        if fastembed_module is None:
            try:
                import fastembed as fastembed_module  # type: ignore[no-redef]
            except Exception:
                raise ResearchError("embedding-dependency-unavailable") from None

        try:
            self._model = fastembed_module.TextEmbedding(
                model_name=MODEL_IDENTITY,
                cache_dir=str(cache.resolve()),
                providers=["CPUExecutionProvider"],
                local_files_only=True,
            )
        except Exception:
            raise ResearchError("embedding-model-load-failed") from None

        candidate_dimension = getattr(self._model, "dimension", MODEL_DIMENSION)
        self._dimension = candidate_dimension if type(candidate_dimension) is int and candidate_dimension > 0 else MODEL_DIMENSION
        self._providers = tuple(_actual_providers(self._model))

    @property
    def dimension(self) -> int:
        return self._dimension

    @property
    def model_identity(self) -> str:
        return MODEL_IDENTITY

    @property
    def providers(self) -> list[str]:
        return list(self._providers)

    def embed_documents(self, texts: Sequence[str]):
        return self._embed(texts, query=False)

    def embed_queries(self, texts: Sequence[str]):
        return self._embed(texts, query=True)

    def _embed(self, texts: Sequence[str], *, query: bool):
        import numpy as np

        if isinstance(texts, (str, bytes)):
            raise ResearchError("embedding-input-invalid")
        try:
            values = list(texts)
        except (TypeError, ValueError):
            raise ResearchError("embedding-input-invalid") from None
        if any(not isinstance(text, str) for text in values):
            raise ResearchError("embedding-input-invalid")
        if not values:
            return np.empty((0, self._dimension), dtype=np.float32)

        try:
            output: Iterable[Any] = self._model.query_embed(values) if query else self._model.embed(values)
            rows = list(output)
        except ResearchError:
            raise
        except Exception:
            raise ResearchError("embedding-failed") from None
        if len(rows) != len(values):
            raise ResearchError("embedding-row-count-invalid")

        normalized = []
        for row in rows:
            try:
                array = np.asarray(row)
            except Exception:
                raise ResearchError("embedding-type-invalid") from None
            if array.ndim != 1:
                raise ResearchError("embedding-shape-invalid")
            if array.shape[0] != self._dimension:
                raise ResearchError("embedding-dimension-invalid")
            if array.dtype.kind != "f":
                raise ResearchError("embedding-type-invalid")
            if not np.isfinite(array).all() or bool(
                (np.abs(array) > np.finfo(np.float32).max).any()
            ):
                raise ResearchError("embedding-nonfinite")
            converted = np.asarray(array, dtype=np.float32)
            if not np.isfinite(converted).all():
                raise ResearchError("embedding-nonfinite")
            normalized.append(converted)
        return np.ascontiguousarray(np.stack(normalized), dtype=np.float32)


def _actual_providers(model: Any) -> list[str]:
    """Read providers only from a concrete ONNX session when one is exposed."""

    candidates = [model]
    seen: set[int] = set()
    for _ in range(4):
        next_candidates = []
        for candidate in candidates:
            if id(candidate) in seen:
                continue
            seen.add(id(candidate))
            try:
                get_providers = getattr(candidate, "get_providers", None)
            except Exception:
                continue
            if callable(get_providers):
                try:
                    providers = get_providers()
                except Exception:
                    return []
                return [str(provider) for provider in providers]
            for name in ("model", "_model", "session", "ort_session", "_session"):
                try:
                    child = getattr(candidate, name, None)
                except Exception:
                    continue
                if child is not None:
                    next_candidates.append(child)
        candidates = next_candidates
    return []
