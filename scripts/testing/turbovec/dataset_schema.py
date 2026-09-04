"""Closed in-memory contracts for the production-scale TurboVec corpus."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Mapping


@dataclass(frozen=True)
class FrozenChunk:
    chunk_id: str
    content_sha256: str
    ordinal: int
    source_document_id: str
    page_number: int
    domain: str
    text: str


@dataclass(frozen=True)
class FrozenQuery:
    query_id: str
    split: str
    kind: str
    text: str


@dataclass(frozen=True)
class FrozenDataset:
    scale: int
    chunks: tuple[FrozenChunk, ...]
    queries: tuple[FrozenQuery, ...]
    relevance: Mapping[str, Mapping[str, int]]
    corpus_sha256: str
    query_sha256: str
    relevance_sha256: str
