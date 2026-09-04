"""Deterministic, generated and redistributable retrieval evaluation data."""

from __future__ import annotations

import hashlib
import json
from typing import Iterable

from .dataset_schema import FrozenChunk, FrozenDataset, FrozenQuery


SUPPORTED_SCALES = (30, 1_000, 10_000, 100_000)
REQUIRED_DOMAINS = frozenset({
    "ordinary_prose", "technical", "education", "healthcare",
    "headings_sections", "tables", "near_duplicates", "unicode",
})
REQUIRED_QUERY_KINDS = frozenset({
    "direct_factual", "paraphrased", "multi_term", "near_duplicate",
    "multiple_relevant", "absent_answer", "citation_sensitive", "difficult_semantic",
})


def _sha_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def _canonical_hash(value: object) -> str:
    payload = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return _sha_text(payload)


def _chunk_text(domain: str, ordinal: int) -> str:
    code = f"GEAI-{ordinal:06d}"
    cohort = ordinal % 97
    measurement = 100 + ((ordinal * 37) % 8_900)
    templates = {
        "ordinary_prose": (
            f"Community record {code}. The Riverside workshop opened in cohort {cohort} and "
            f"catalogued {measurement} reusable items. Volunteers meet on the second Tuesday."
        ),
        "technical": (
            f"Technical note {code}: worker {cohort} processes bounded batches of {measurement} bytes. "
            "It validates the checksum before atomic publication and rejects incomplete output."
        ),
        "education": (
            f"Education brief {code}. Lesson group {cohort} uses {measurement} practice tokens, "
            "followed by retrieval, explanation, and a source-checking exercise."
        ),
        "healthcare": (
            f"Non-clinical healthcare training record {code}. Simulation cohort {cohort} logs "
            f"{measurement} hand-hygiene observations; no patient or treatment data is included."
        ),
        "headings_sections": (
            f"SECTION {cohort}: CONTROLLED PROCEDURE {code}\n\nPurpose: verify {measurement} ordered records. "
            "Method: inspect identity, execute the check, and preserve the terminal result."
        ),
        "tables": (
            f"Table record {code}\nCategory | Cohort | Count\nEvaluation | {cohort} | {measurement}\n"
            "The count belongs only to this synthetic evaluation row."
        ),
        "near_duplicates": (
            f"Disambiguation memo {code}. Variant {ordinal % 11} belongs to cohort {cohort}; its exact "
            f"control value is {measurement}, not {measurement + 1}."
        ),
        "unicode": (
            f"Unicode record {code}: café, naïve, résumé, Ελληνικά, 日本語 and عربى are preserved. "
            f"Cohort {cohort} has synthetic value {measurement}."
        ),
    }
    return templates[domain]


def _make_chunks(scale: int) -> tuple[FrozenChunk, ...]:
    domains = tuple(sorted(REQUIRED_DOMAINS))
    chunks: list[FrozenChunk] = []
    for ordinal in range(scale):
        domain = domains[ordinal % len(domains)]
        source = f"synthetic-{domain}-{ordinal // 80:05d}"
        page = (ordinal % 40) + 1
        text = _chunk_text(domain, ordinal)
        content_hash = _sha_text(text)
        chunk_id = _sha_text(f"{source}\n{page}\n{ordinal}\n{text}")
        chunks.append(FrozenChunk(chunk_id, content_hash, ordinal, source, page, domain, text))
    return tuple(chunks)


def _query_text(kind: str, primary: FrozenChunk, secondary: FrozenChunk, serial: int) -> str:
    code = f"GEAI-{primary.ordinal:06d}"
    second_code = f"GEAI-{secondary.ordinal:06d}"
    if kind == "direct_factual":
        return f"What synthetic value is recorded for {code}?"
    if kind == "paraphrased":
        return f"Summarise the controlled fact associated with record {code}."
    if kind == "multi_term":
        return f"Find {code}, its cohort, domain, and recorded measurement."
    if kind == "near_duplicate":
        return f"Distinguish record {code} from adjacent similarly worded variants."
    if kind == "multiple_relevant":
        return f"Compare the facts in {code} and {second_code}."
    if kind == "citation_sensitive":
        return f"Which source page contains {code}? Return the source and page evidence."
    if kind == "difficult_semantic":
        return f"Locate the passage whose unique controlled identifier implies {code.lower()} without relying on its topic alone."
    return f"Which record states that the fictional Zephyr clinic issued absent code ZX-{serial:04d}?"


def _make_queries(chunks: tuple[FrozenChunk, ...]) -> tuple[tuple[FrozenQuery, ...], dict[str, dict[str, int]]]:
    kinds = tuple(sorted(REQUIRED_QUERY_KINDS))
    queries: list[FrozenQuery] = []
    relevance: dict[str, dict[str, int]] = {}
    for kind_index, kind in enumerate(kinds):
        for serial in range(16):
            primary = chunks[(kind_index * 17 + serial * 7) % len(chunks)]
            secondary = chunks[(primary.ordinal + max(1, len(chunks) // 3)) % len(chunks)]
            query_id = f"q-{kind_index:02d}-{serial:02d}"
            split = "development" if serial < 4 else "evaluation"
            queries.append(FrozenQuery(query_id, split, kind, _query_text(kind, primary, secondary, serial)))
            if kind == "absent_answer":
                relevance[query_id] = {}
            elif kind == "multiple_relevant":
                relevance[query_id] = {primary.chunk_id: 3, secondary.chunk_id: 2}
            else:
                relevance[query_id] = {primary.chunk_id: 3}
    return tuple(queries), relevance


def _chunk_records(chunks: Iterable[FrozenChunk]) -> list[dict[str, object]]:
    return [
        {
            "chunk_id": item.chunk_id,
            "content_sha256": item.content_sha256,
            "ordinal": item.ordinal,
            "source_document_id": item.source_document_id,
            "page_number": item.page_number,
            "domain": item.domain,
            "text": item.text,
        }
        for item in chunks
    ]


def build_frozen_dataset(scale: int) -> FrozenDataset:
    if scale not in SUPPORTED_SCALES:
        raise ValueError(f"unsupported corpus scale: {scale}")
    chunks = _make_chunks(scale)
    queries, relevance = _make_queries(chunks)
    query_records = [query.__dict__ for query in queries]
    return FrozenDataset(
        scale=scale,
        chunks=chunks,
        queries=queries,
        relevance=relevance,
        corpus_sha256=_canonical_hash(_chunk_records(chunks)),
        query_sha256=_canonical_hash(query_records),
        relevance_sha256=_canonical_hash(relevance),
    )
