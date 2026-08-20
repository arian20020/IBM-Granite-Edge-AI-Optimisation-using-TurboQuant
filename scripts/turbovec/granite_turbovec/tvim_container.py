"""Bounded envelope for opaque TurboVec 1.0.0 payloads.

The upstream ``IdMapIndex.write`` output is treated as opaque: the CLI does
not rely on it exposing a stable, self-describing header suitable for a safe
pre-deserialization check. ``GTVI`` supplies that check outside the payload.
"""

from __future__ import annotations

import contextlib
import hashlib
import os
import stat
import struct
import tempfile
from pathlib import Path
from typing import Iterator

from .contracts import ResearchError


MAGIC = b"GTVI"
VERSION = 1
_HEADER = struct.Struct(">4sBBHIQQ32s")
MAX_PAYLOAD_BYTES = 16 * 1024 * 1024 * 1024
MAX_COUNT = 6_400_000
MAX_DIMENSION = 65_536


def write_container(raw_path: str | Path, destination: str | Path, *, bits: int, dimension: int, count: int) -> None:
    _validate_expected(bits, dimension, count)
    raw = Path(raw_path); target = Path(destination)
    digest = hashlib.sha256(); length = 0
    try:
        with raw.open("rb") as source:
            opened = os.fstat(source.fileno()); current = raw.stat(follow_symlinks=False)
            if not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (current.st_dev, current.st_ino) or bool(getattr(current, "st_file_attributes", 0) & 0x400):
                raise ResearchError("index-artifact-invalid")
            while block := source.read(1024 * 1024):
                length += len(block)
                if length > MAX_PAYLOAD_BYTES:
                    raise ResearchError("index-artifact-invalid")
                digest.update(block)
            header = _HEADER.pack(MAGIC, VERSION, bits, 0, dimension, count, length, digest.digest())
            source.seek(0)
            with target.open("xb") as output:
                output.write(header)
                while block := source.read(1024 * 1024):
                    output.write(block)
                output.flush(); os.fsync(output.fileno())
            final = raw.stat(follow_symlinks=False)
            if (opened.st_dev, opened.st_ino) != (final.st_dev, final.st_ino) or final.st_size != length:
                raise ResearchError("index-artifact-invalid")
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-write-failed") from None


@contextlib.contextmanager
def extract_validated_payload(path: str | Path, *, bits: int, dimension: int, count: int) -> Iterator[Path]:
    _validate_expected(bits, dimension, count)
    source_path = Path(path)
    temporary = None
    try:
        with source_path.open("rb") as source:
            opened = os.fstat(source.fileno()); current = source_path.stat(follow_symlinks=False)
            if not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (current.st_dev, current.st_ino) or bool(getattr(current, "st_file_attributes", 0) & 0x400):
                raise ResearchError("index-artifact-invalid")
            header_bytes = source.read(_HEADER.size)
            if len(header_bytes) != _HEADER.size:
                raise ResearchError("index-artifact-invalid")
            magic, version, actual_bits, reserved, actual_dimension, actual_count, payload_length, expected_hash = _HEADER.unpack(header_bytes)
            if (
                magic != MAGIC or version != VERSION or reserved != 0
                or actual_bits != bits or actual_dimension != dimension or actual_count != count
                or payload_length > MAX_PAYLOAD_BYTES
            ):
                raise ResearchError("index-artifact-invalid")
            try:
                file_size = source_path.stat().st_size
            except OSError:
                raise ResearchError("index-artifact-invalid") from None
            if file_size != _HEADER.size + payload_length:
                raise ResearchError("index-artifact-invalid")
            temporary = tempfile.TemporaryDirectory(prefix="granite-tvim-")
            extracted = Path(temporary.name) / "payload.tvim"
            digest = hashlib.sha256(); remaining = payload_length
            with extracted.open("xb") as output:
                while remaining:
                    block = source.read(min(1024 * 1024, remaining))
                    if not block:
                        raise ResearchError("index-artifact-invalid")
                    output.write(block); digest.update(block); remaining -= len(block)
                output.flush(); os.fsync(output.fileno())
            if source.read(1) or digest.digest() != expected_hash:
                raise ResearchError("index-artifact-invalid")
            final = source_path.stat(follow_symlinks=False)
            if (opened.st_dev, opened.st_ino) != (final.st_dev, final.st_ino) or final.st_size != file_size:
                raise ResearchError("index-artifact-invalid")
    except ResearchError:
        if temporary is not None:
            temporary.cleanup()
        raise
    except Exception:
        if temporary is not None:
            temporary.cleanup()
        raise ResearchError("index-artifact-invalid") from None
    try:
        yield extracted
    finally:
        if temporary is not None:
            temporary.cleanup()


def _validate_expected(bits: int, dimension: int, count: int) -> None:
    if type(bits) is not int or bits not in (2, 4) or type(dimension) is not int or not 1 <= dimension <= MAX_DIMENSION or type(count) is not int or not 1 <= count <= MAX_COUNT:
        raise ResearchError("index-artifact-invalid")
