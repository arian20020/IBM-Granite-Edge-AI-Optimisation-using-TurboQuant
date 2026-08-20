"""Bounded, deterministic text discovery and chunking.

Document hashes cover the original bounded bytes, including an optional UTF-8
BOM. The decoded document text omits that BOM. Symbolic-link and reparse-point
files are rejected; link-like directories are never traversed.
"""

from __future__ import annotations

import hashlib
import math
import os
import stat
from pathlib import Path
from typing import Final

from .contracts import Chunk, Document, ResearchError


HARD_MAX_FILES: Final = 64
HARD_MAX_BYTES_PER_FILE: Final = 8 * 1024 * 1024
HARD_MAX_TOTAL_BYTES: Final = 32 * 1024 * 1024
MAX_CHUNK_CHARS: Final = 1_000_000
_SUPPORTED_EXTENSIONS: Final = frozenset({".txt", ".md"})
_FILE_ATTRIBUTE_REPARSE_POINT: Final = 0x400


def validate_extension(name: str | os.PathLike[str]) -> None:
    """Reject a name unless its final extension is exactly .txt or .md."""

    try:
        candidate = Path(name)
    except (TypeError, ValueError):
        raise ResearchError("input-unsupported-type") from None
    if candidate.name in {"", candidate.suffix} or candidate.suffix.casefold() not in _SUPPORTED_EXTENSIONS:
        raise ResearchError("input-unsupported-type")


def discover_documents(
    input_path: str | os.PathLike[str],
    *,
    max_files: int = HARD_MAX_FILES,
    max_bytes: int = HARD_MAX_BYTES_PER_FILE,
    max_total_bytes: int = HARD_MAX_TOTAL_BYTES,
) -> list[Document]:
    """Discover and strictly decode one approved file or directory tree.

    Caller limits may tighten, but never expand, the built-in hard caps.
    Unsupported regular files inside a directory are ignored. Link-like
    directories are ignored without traversal, while supported link-like files
    fail closed.
    """

    file_limit = _bounded_limit(max_files, HARD_MAX_FILES)
    byte_limit = _bounded_limit(max_bytes, HARD_MAX_BYTES_PER_FILE)
    total_limit = _bounded_limit(max_total_bytes, HARD_MAX_TOTAL_BYTES)

    try:
        raw_input = os.fspath(input_path)
    except TypeError:
        raise ResearchError("input-invalid-parameters") from None
    if not isinstance(raw_input, str) or raw_input == "" or "\0" in raw_input:
        raise ResearchError("input-invalid-parameters")
    try:
        root = Path(raw_input)
    except (TypeError, ValueError):
        raise ResearchError("input-invalid-parameters") from None

    try:
        root_metadata = root.lstat()
    except ValueError:
        raise ResearchError("input-invalid-parameters") from None
    except FileNotFoundError:
        raise ResearchError("input-not-found") from None
    except OSError:
        raise ResearchError("input-read-failed") from None

    candidates: list[tuple[str, Path, os.stat_result]]
    if _is_link_like(root, root_metadata):
        raise ResearchError("input-unsafe-link")
    if stat.S_ISREG(root_metadata.st_mode):
        validate_extension(root.name)
        candidates = [(root.name, root, root_metadata)]
    elif stat.S_ISDIR(root_metadata.st_mode):
        candidates = _discover_directory(root, file_limit)
    else:
        raise ResearchError("input-not-file-or-directory")

    candidates.sort(key=lambda item: (item[0].casefold(), item[0]))
    if not candidates:
        raise ResearchError("input-empty")
    if len(candidates) > file_limit:
        raise ResearchError("input-file-count-limit")

    declared_total = 0
    for _, _, metadata in candidates:
        if metadata.st_size > byte_limit:
            raise ResearchError("input-file-size-limit")
        declared_total += metadata.st_size
        if declared_total > total_limit:
            raise ResearchError("input-total-size-limit")

    documents: list[Document] = []
    actual_total = 0
    for relative_path, path, metadata in candidates:
        content = _read_bounded_file(path, metadata, byte_limit)
        actual_total += len(content)
        if actual_total > total_limit:
            raise ResearchError("input-total-size-limit")
        try:
            text = content.decode("utf-8-sig", errors="strict")
        except UnicodeDecodeError:
            raise ResearchError("input-invalid-utf8") from None
        documents.append(
            Document(
                relative_path=relative_path,
                text=text,
                sha256=hashlib.sha256(content).hexdigest(),
            )
        )
    return documents


def _bounded_limit(value: int, hard_maximum: int) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ResearchError("input-invalid-parameters")
    return min(value, hard_maximum)


def _is_link_like(path: Path, metadata: os.stat_result) -> bool:
    attributes = getattr(metadata, "st_file_attributes", 0)
    return stat.S_ISLNK(metadata.st_mode) or bool(attributes & _FILE_ATTRIBUTE_REPARSE_POINT)


def _discover_directory(root: Path, file_limit: int) -> list[tuple[str, Path, os.stat_result]]:
    candidates: list[tuple[str, Path, os.stat_result]] = []
    pending = [root]
    while pending:
        directory = pending.pop()
        try:
            with os.scandir(directory) as entries:
                snapshot = list(entries)
        except OSError:
            raise ResearchError("input-read-failed") from None

        for entry in snapshot:
            path = Path(entry.path)
            try:
                metadata = entry.stat(follow_symlinks=False)
            except OSError:
                raise ResearchError("input-read-failed") from None

            relative_path = "/".join(path.relative_to(root).parts)
            link_like = _is_link_like(path, metadata)
            if link_like:
                if path.suffix.casefold() in _SUPPORTED_EXTENSIONS:
                    raise ResearchError("input-unsafe-link")
                continue
            if stat.S_ISDIR(metadata.st_mode):
                pending.append(path)
            elif stat.S_ISREG(metadata.st_mode) and path.suffix.casefold() in _SUPPORTED_EXTENSIONS:
                candidates.append((relative_path, path, metadata))
                if len(candidates) > file_limit:
                    raise ResearchError("input-file-count-limit")
    return candidates


def _same_metadata(left: os.stat_result, right: os.stat_result) -> bool:
    if left.st_size != right.st_size or left.st_mtime_ns != right.st_mtime_ns:
        return False
    # Windows DirEntry metadata can omit device/inode identity (both are zero),
    # whereas fstat on the opened handle supplies them. Compare when available.
    if left.st_dev and right.st_dev and left.st_dev != right.st_dev:
        return False
    if left.st_ino and right.st_ino and left.st_ino != right.st_ino:
        return False
    return True


def _read_bounded_file(path: Path, discovered: os.stat_result, byte_limit: int) -> bytes:
    try:
        with path.open("rb") as source:
            opened = os.fstat(source.fileno())
            if not stat.S_ISREG(opened.st_mode) or not _same_metadata(opened, discovered):
                raise ResearchError("input-race-detected")
            content = source.read(byte_limit + 1)
            after = os.fstat(source.fileno())
    except ResearchError:
        raise
    except OSError:
        raise ResearchError("input-read-failed") from None

    if len(content) > byte_limit:
        raise ResearchError("input-file-size-limit")
    if not _same_metadata(after, opened) or len(content) != opened.st_size:
        raise ResearchError("input-race-detected")
    return content


def chunk_document(
    document: Document,
    *,
    max_chars: int = 1200,
    overlap_chars: int = 200,
) -> list[Chunk]:
    """Split a document into stable coordinate-preserving overlapping chunks."""

    if (
        isinstance(max_chars, bool)
        or not isinstance(max_chars, int)
        or max_chars <= 0
        or max_chars > MAX_CHUNK_CHARS
        or isinstance(overlap_chars, bool)
        or not isinstance(overlap_chars, int)
        or overlap_chars < 0
        or overlap_chars >= max_chars
    ):
        raise ResearchError("chunk-invalid-parameters")

    text = document.text
    content_start = 0
    while content_start < len(text) and text[content_start].isspace():
        content_start += 1
    content_end = len(text)
    while content_end > content_start and text[content_end - 1].isspace():
        content_end -= 1
    if content_start == content_end:
        return []

    chunks: list[Chunk] = []
    identifiers: set[int] = set()
    start = content_start
    while start < content_end:
        hard_end = min(start + max_chars, content_end)
        end = hard_end if hard_end == content_end else _preferred_boundary(text, start, hard_end)
        if end <= start:
            end = hard_end

        chunk_text = text[start:end]
        identifier = _derive_chunk_id(document.relative_path, start, end, chunk_text)
        if identifier in identifiers:
            raise ResearchError("chunk-id-collision")
        identifiers.add(identifier)
        chunks.append(Chunk(identifier, document.relative_path, start, end, chunk_text))

        if end == content_end:
            break
        next_start = max(content_start, end - overlap_chars)
        start = next_start if next_start > start else start + 1
    return chunks


def _preferred_boundary(text: str, start: int, hard_end: int) -> int:
    threshold = start + math.ceil((hard_end - start) * 0.60)

    paragraph_end = -1
    for marker in ("\n\n", "\r\n\r\n"):
        marker_start = text.rfind(marker, threshold, hard_end)
        if marker_start >= threshold:
            paragraph_end = max(paragraph_end, marker_start + len(marker))
    if paragraph_end > start:
        return paragraph_end

    newline = text.rfind("\n", threshold, hard_end)
    if newline >= threshold:
        return newline + 1

    for index in range(hard_end - 1, threshold - 1, -1):
        if text[index].isspace():
            return index + 1
    return hard_end


def _derive_chunk_id(relative_path: str, start: int, end: int, text: str) -> int:
    payload = f"{relative_path}\0{start}\0{end}\0{text}".encode("utf-8")
    return int.from_bytes(hashlib.sha256(payload).digest()[:8], "big", signed=False)
