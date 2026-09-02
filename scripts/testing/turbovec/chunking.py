"""Deterministic project-authored granite-chunker-v1 implementation."""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import re
import unicodedata
from typing import Protocol, Sequence


CHUNKER_VERSION = "granite-chunker-v1"
TARGET_TOKENS = 512
MAX_TOKENS = 768
OVERLAP_TOKENS = 64


@dataclass(frozen=True)
class PageText:
    page: int
    text: str


@dataclass(frozen=True)
class Chunk:
    chunk_id: str
    ordinal: int
    text: str
    start_page: int
    end_page: int
    token_count: int


class TokenCounter(Protocol):
    def count(self, text: str) -> int: ...


class WordFixtureTokenCounter:
    def count(self, text: str) -> int:
        return len(text.split())


def normalize_text(text: str) -> str:
    text = unicodedata.normalize("NFC", text.replace("\r\n", "\n").replace("\r", "\n"))
    paragraphs = [re.sub(r"[\t \f\v]+", " ", part).strip() for part in re.split(r"\n\s*\n", text)]
    return "\n\n".join(part for part in paragraphs if part)


def _split_to_limit(text: str, counter: TokenCounter) -> list[str]:
    if counter.count(text) <= MAX_TOKENS:
        return [text]
    words = text.split()
    parts: list[str] = []
    start = 0
    while start < len(words):
        end = min(start + MAX_TOKENS, len(words))
        while end > start and counter.count(" ".join(words[start:end])) > MAX_TOKENS:
            end -= 1
        if end == start:
            raise ValueError("token counter cannot produce a bounded chunk")
        parts.append(" ".join(words[start:end]))
        if end == len(words):
            break
        start = max(start + 1, end - OVERLAP_TOKENS)
    return parts


def chunk_pages(document_sha256: str, pages: Sequence[PageText], tokenizer: TokenCounter) -> tuple[Chunk, ...]:
    if not re.fullmatch(r"[0-9a-f]{64}", document_sha256):
        raise ValueError("document SHA-256 must be lowercase hexadecimal")
    if any(page.page <= 0 for page in pages) or len({p.page for p in pages}) != len(pages):
        raise ValueError("page ordinals must be unique positive integers")
    chunks: list[Chunk] = []
    ordinal = 0
    for page in pages:
        normalized = normalize_text(page.text)
        if not normalized:
            continue
        paragraphs = normalized.split("\n\n")
        pending = ""
        for paragraph in paragraphs:
            candidate = paragraph if not pending else pending + "\n\n" + paragraph
            if pending and tokenizer.count(candidate) > TARGET_TOKENS:
                for part in _split_to_limit(pending, tokenizer):
                    chunks.append(_make_chunk(document_sha256, ordinal, part, page.page, tokenizer)); ordinal += 1
                pending = paragraph
            else:
                pending = candidate
        if pending:
            for part in _split_to_limit(pending, tokenizer):
                chunks.append(_make_chunk(document_sha256, ordinal, part, page.page, tokenizer)); ordinal += 1
    return tuple(chunks)


def _make_chunk(document_sha256: str, ordinal: int, text: str, page: int, counter: TokenCounter) -> Chunk:
    normalized = normalize_text(text)
    digest = hashlib.sha256(f"{document_sha256}\n{CHUNKER_VERSION}\n{ordinal}\n{normalized}".encode("utf-8")).hexdigest()
    count = counter.count(normalized)
    if not normalized or count > MAX_TOKENS:
        raise ValueError("invalid chunk")
    return Chunk(digest, ordinal, normalized, page, page, count)
