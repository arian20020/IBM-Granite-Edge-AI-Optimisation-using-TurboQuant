"""Index lifecycle qualification isolated from the production application."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import shutil
from typing import Callable, Protocol

import numpy as np

from .indexes import SearchBatch


class RetrievalIndex(Protocol):
    def add(self, vectors: np.ndarray, ids: np.ndarray) -> None: ...
    def search(self, queries: np.ndarray, k: int) -> SearchBatch: ...
    def save(self, root: Path) -> None: ...


@dataclass(frozen=True)
class LifecycleResult:
    build_completed: bool
    query_completed: bool
    save_completed: bool
    reload_completed: bool
    post_reload_equal: bool
    corruption_rejected: bool
    cleanup_completed: bool
    terminal_status: str


def _remove_owned(path: Path) -> None:
    if path.exists():
        shutil.rmtree(path)


def execute_lifecycle(
    factory: Callable[[], RetrievalIndex],
    vectors: np.ndarray,
    ids: np.ndarray,
    queries: np.ndarray,
    target: Path,
    *,
    stop_before_publish: str | None = None,
) -> LifecycleResult:
    if stop_before_publish not in (None, "cancelled", "interrupted"):
        raise ValueError("unsupported lifecycle stop mode")
    destination = Path(target).resolve()
    staging = destination.parent / f".staging-{destination.name}"
    corrupt = destination.parent / f".corrupt-{destination.name}"
    if destination.exists() or staging.exists() or corrupt.exists():
        raise FileExistsError(destination.name)
    destination.parent.mkdir(parents=True, exist_ok=True)
    cleanup_completed = False
    try:
        index = factory()
        index.add(vectors, ids)
        before = index.search(queries, min(10, len(ids)))
        index.save(staging)
        if stop_before_publish is not None:
            _remove_owned(staging)
            cleanup_completed = not staging.exists() and not destination.exists()
            return LifecycleResult(True, True, False, False, False, False, cleanup_completed, stop_before_publish)
        staging.rename(destination)
        index_type = type(index)
        del index
        reopened = index_type.load(destination)
        after = reopened.search(queries, min(10, len(ids)))
        equal = np.array_equal(before.ids, after.ids) and np.allclose(before.scores, after.scores, atol=1e-6)
        shutil.copytree(destination, corrupt)
        corrupt_target = next(path for path in corrupt.iterdir() if path.name != "metadata.json")
        with corrupt_target.open("ab") as stream:
            stream.write(b"corrupt")
        corruption_rejected = False
        try:
            index_type.load(corrupt)
        except ValueError:
            corruption_rejected = True
        del reopened
        _remove_owned(corrupt)
        _remove_owned(destination)
        cleanup_completed = not destination.exists() and not corrupt.exists() and not staging.exists()
        return LifecycleResult(True, True, True, True, equal, corruption_rejected, cleanup_completed, "completed")
    except BaseException:
        _remove_owned(staging)
        _remove_owned(corrupt)
        _remove_owned(destination)
        raise
