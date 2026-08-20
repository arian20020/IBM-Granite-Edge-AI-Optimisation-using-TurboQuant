"""Canonical, privacy-safe manifests and fail-closed index promotion."""

from __future__ import annotations

import ctypes
import hashlib
import json
import os
import re
import shutil
import stat
import sys
import uuid
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

from .contracts import ResearchError


_HASH = re.compile(r"[0-9a-f]{64}\Z")
_OPERATION_ID = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,63}\Z")
_UTC = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?Z\Z")
_MAX_MANIFEST_BYTES = 4 * 1024 * 1024
_REPARSE_POINT = 0x400
_UINT64_MAX = (1 << 64) - 1
_WINDOWS_RESERVED = {
    "CON",
    "PRN",
    "AUX",
    "NUL",
    *(f"COM{number}" for number in range(1, 10)),
    *(f"LPT{number}" for number in range(1, 10)),
}


@dataclass(frozen=True)
class IndexIdentity:
    schema_version: int
    chunking_algorithm: str
    chunking_version: int
    chunk_max_chars: int
    chunk_overlap: int
    embedding_model: str
    embedding_model_manifest_sha256: str
    embedding_model_license: str
    dimension: int
    requested_backend: str
    actual_backend: str
    index_format: str
    bit_width: int | None
    turbovec_version: str | None
    turbovec_source_commit: str | None
    turbovec_wheel_sha256: str | None
    turbovec_license: str | None
    dependency_lock_sha256: str
    requested_provider: str
    actual_provider: str


@dataclass(frozen=True)
class SourceRecord:
    relative_path: str
    sha256: str


@dataclass(frozen=True)
class ChunkRecord:
    chunk_id: int
    relative_path: str
    start: int
    end: int


@dataclass(frozen=True)
class ArtifactRecord:
    filename: str
    sha256: str
    size: int
    count: int
    magic: str
    version: int
    dimension: int


@dataclass(frozen=True)
class IndexManifest:
    identity: IndexIdentity
    sources: tuple[SourceRecord, ...]
    chunks: tuple[ChunkRecord, ...]
    created_utc: str
    artifacts: tuple[ArtifactRecord, ...]


def canonical_json(value: IndexManifest) -> str:
    """Return the only accepted serialization for a schema-v1 manifest."""
    manifest = _validate_manifest(value)
    try:
        return json.dumps(
            asdict(manifest),
            ensure_ascii=False,
            allow_nan=False,
            separators=(",", ":"),
            sort_keys=True,
        )
    except (TypeError, ValueError):
        raise ResearchError("index-manifest-invalid") from None


def canonical_sha256(value: IndexManifest) -> str:
    return hashlib.sha256(canonical_json(value).encode("utf-8")).hexdigest()


def write_manifest_atomic(
    path: str | Path,
    value: IndexManifest,
    *,
    operation_id: str | None = None,
) -> None:
    payload = canonical_json(value).encode("utf-8")
    target = Path(path)
    operation = _validated_operation_id(operation_id)
    temporary = target.with_name(f"{target.name}.tmp-{operation}")
    created = False
    temporary_identity = None
    try:
        parent = target.parent
        parent_identity = _create_plain_directory(parent)
        if target.name != "manifest.json":
            raise ResearchError("index-path-invalid")
        _require_safe_leaf(target, may_not_exist=True)
        _require_safe_leaf(temporary, may_not_exist=True, must_not_exist=True)
        with temporary.open("xb") as output:
            created = True
            opened = os.fstat(output.fileno())
            temporary_identity = (opened.st_dev, opened.st_ino)
            output.write(payload)
            output.flush()
            os.fsync(output.fileno())
        _require_same_file(temporary, temporary_identity)
        _require_same_directory(parent, parent_identity)
        os.replace(temporary, target)
        created = False
        _fsync_directory(parent)
    except ResearchError:
        if created and temporary_identity is not None:
            _unlink_owned_file(temporary, temporary_identity)
        raise
    except Exception:
        if created and temporary_identity is not None:
            _unlink_owned_file(temporary, temporary_identity)
        raise ResearchError("index-manifest-write-failed") from None


def load_and_validate_manifest(
    path: str | Path,
    expected_identity: IndexIdentity | IndexManifest,
) -> IndexManifest:
    try:
        raw = _bounded_read(Path(path), _MAX_MANIFEST_BYTES)
        text = raw.decode("utf-8", errors="strict")
        parsed = json.loads(
            text,
            object_pairs_hook=_unique_object,
            parse_constant=lambda _: _reject_json_constant(),
        )
        loaded = _manifest_from_mapping(parsed)
        if text != canonical_json(loaded):
            raise ResearchError("index-manifest-corrupt")
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-manifest-corrupt") from None

    expected = (
        _validate_manifest(expected_identity).identity
        if isinstance(expected_identity, IndexManifest)
        else _validate_identity(expected_identity)
    )
    _compare_identity(loaded.identity, expected)
    if isinstance(expected_identity, IndexManifest):
        expected_manifest = _validate_manifest(expected_identity)
        if loaded.sources != expected_manifest.sources or loaded.chunks != expected_manifest.chunks:
            raise ResearchError("index-source-mismatch")
    return loaded


def promote_staged_index(
    destination: str | Path,
    manifest: IndexManifest,
    writer: Callable[[Path], None],
    artifact_validator: Callable[[Path, ArtifactRecord, IndexManifest], object],
    *,
    operation_id: str | None = None,
) -> Path:
    """Build, validate, then atomically publish a new sibling index directory."""
    expected = _validate_manifest(manifest)
    operation = _validated_operation_id(operation_id)
    target = Path(destination)
    parent = target.parent
    staging = parent / f"{target.name}.staging-{operation}"
    owned = False
    staging_identity = None
    try:
        parent_identity = _create_plain_directory(parent)
        _validate_destination_name(target)
        _require_safe_leaf(target, may_not_exist=True, must_not_exist=True)
        _require_safe_leaf(staging, may_not_exist=True, must_not_exist=True)
        staging.mkdir()
        owned = True
        staging_identity = _require_plain_directory(staging)
        writer(staging)
        _require_same_directory(parent, parent_identity)
        _require_same_directory(staging, staging_identity)

        write_manifest_atomic(
            staging / "manifest.json", expected, operation_id=operation
        )
        loaded = load_and_validate_manifest(staging / "manifest.json", expected)
        _validate_artifacts(staging, loaded, artifact_validator)
        _require_same_directory(parent, parent_identity)
        _require_same_directory(staging, staging_identity)
        _rename_directory_no_replace(staging, target)
        owned = False
        _fsync_directory(parent)
        return target
    except ResearchError:
        if owned and staging_identity is not None:
            _remove_owned_staging(staging, staging_identity)
        raise
    except Exception:
        if owned and staging_identity is not None:
            _remove_owned_staging(staging, staging_identity)
        raise ResearchError("index-promotion-failed") from None


def _validate_manifest(value: object) -> IndexManifest:
    if type(value) is not IndexManifest:
        raise ResearchError("index-manifest-invalid")
    identity = _validate_identity(value.identity)
    if (
        type(value.sources) is not tuple
        or type(value.chunks) is not tuple
        or type(value.artifacts) is not tuple
    ):
        raise ResearchError("index-manifest-invalid")
    sources = tuple(_validate_source(item) for item in value.sources)
    chunks = tuple(_validate_chunk(item) for item in value.chunks)
    artifacts = tuple(_validate_artifact(item) for item in value.artifacts)
    _validate_utc(value.created_utc)
    if not sources or not chunks or not artifacts:
        raise ResearchError("index-manifest-invalid")
    source_paths = [item.relative_path for item in sources]
    if len(source_paths) != len(set(source_paths)):
        raise ResearchError("index-manifest-invalid")
    source_set = set(source_paths)
    chunk_ids = [item.chunk_id for item in chunks]
    if len(chunk_ids) != len(set(chunk_ids)):
        raise ResearchError("index-manifest-invalid")
    if any(item.relative_path not in source_set for item in chunks):
        raise ResearchError("index-manifest-invalid")
    artifact_names = [item.filename for item in artifacts]
    if len(artifact_names) != len(set(artifact_names)):
        raise ResearchError("index-manifest-invalid")
    if any(
        item.count != len(chunks) or item.dimension != identity.dimension
        for item in artifacts
    ):
        raise ResearchError("index-manifest-invalid")
    return IndexManifest(identity, sources, chunks, value.created_utc, artifacts)


def _validate_identity(value: object) -> IndexIdentity:
    if type(value) is not IndexIdentity:
        raise ResearchError("index-manifest-invalid")
    if type(value.schema_version) is not int or value.schema_version != 1:
        raise ResearchError("index-manifest-corrupt")
    _text(value.chunking_algorithm)
    _positive_int(value.chunking_version)
    _positive_int(value.chunk_max_chars)
    _nonnegative_int(value.chunk_overlap)
    if value.chunk_overlap >= value.chunk_max_chars:
        raise ResearchError("index-manifest-invalid")
    _text(value.embedding_model)
    _hash(value.embedding_model_manifest_sha256)
    _text(value.embedding_model_license)
    _positive_int(value.dimension)
    _text(value.requested_backend)
    _text(value.actual_backend)
    _text(value.index_format)
    _hash(value.dependency_lock_sha256)
    _text(value.requested_provider)
    _text(value.actual_provider)
    if value.requested_backend not in ("float32", "turbovec"):
        raise ResearchError("index-manifest-invalid")
    if value.actual_backend == "float32":
        if (
            re.fullmatch(r"float32-[a-z0-9-]+-v[1-9][0-9]*", value.index_format)
            is None
            or value.bit_width is not None
            or any(
            item is not None
            for item in (
                value.turbovec_version,
                value.turbovec_source_commit,
                value.turbovec_wheel_sha256,
                value.turbovec_license,
            )
            )
        ):
            raise ResearchError("index-manifest-invalid")
    elif value.actual_backend == "turbovec":
        if (
            re.fullmatch(r"turbovec-[a-z0-9-]+-v[1-9][0-9]*", value.index_format)
            is None
            or type(value.bit_width) is not int
            or value.bit_width not in (2, 4)
        ):
            raise ResearchError("index-manifest-invalid")
        _text(value.turbovec_version)
        _commit(value.turbovec_source_commit)
        _hash(value.turbovec_wheel_sha256)
        _text(value.turbovec_license)
    else:
        raise ResearchError("index-manifest-invalid")
    return value


def _validate_source(value: object) -> SourceRecord:
    if type(value) is not SourceRecord:
        raise ResearchError("index-manifest-invalid")
    _relative_path(value.relative_path)
    _hash(value.sha256)
    return value


def _validate_chunk(value: object) -> ChunkRecord:
    if type(value) is not ChunkRecord:
        raise ResearchError("index-manifest-invalid")
    _uint64(value.chunk_id)
    _relative_path(value.relative_path)
    _nonnegative_int(value.start)
    _positive_int(value.end)
    if value.end <= value.start:
        raise ResearchError("index-manifest-invalid")
    return value


def _validate_artifact(value: object) -> ArtifactRecord:
    if type(value) is not ArtifactRecord:
        raise ResearchError("index-manifest-invalid")
    _artifact_filename(value.filename)
    _hash(value.sha256)
    _nonnegative_int(value.size)
    _nonnegative_int(value.count)
    _text(value.magic)
    _positive_int(value.version)
    _positive_int(value.dimension)
    return value


def _manifest_from_mapping(value: object) -> IndexManifest:
    root = _object(value, {"identity", "sources", "chunks", "created_utc", "artifacts"})
    identity_value = _object(root["identity"], set(IndexIdentity.__dataclass_fields__))
    identity = IndexIdentity(**identity_value)
    sources_value = _array(root["sources"])
    chunks_value = _array(root["chunks"])
    artifacts_value = _array(root["artifacts"])
    result = IndexManifest(
        identity=identity,
        sources=tuple(
            SourceRecord(**_object(item, set(SourceRecord.__dataclass_fields__)))
            for item in sources_value
        ),
        chunks=tuple(
            ChunkRecord(**_object(item, set(ChunkRecord.__dataclass_fields__)))
            for item in chunks_value
        ),
        created_utc=root["created_utc"],
        artifacts=tuple(
            ArtifactRecord(**_object(item, set(ArtifactRecord.__dataclass_fields__)))
            for item in artifacts_value
        ),
    )
    try:
        return _validate_manifest(result)
    except ResearchError:
        raise ResearchError("index-manifest-corrupt") from None


def _compare_identity(actual: IndexIdentity, expected: IndexIdentity) -> None:
    groups = (
        (
            (
                "embedding_model",
                "embedding_model_manifest_sha256",
                "embedding_model_license",
            ),
            "index-embedding-mismatch",
        ),
        (("dimension",), "index-dimension-mismatch"),
        (
            (
                "chunking_algorithm",
                "chunking_version",
                "chunk_max_chars",
                "chunk_overlap",
            ),
            "index-chunking-mismatch",
        ),
        (("requested_backend", "actual_backend"), "index-backend-mismatch"),
        (("index_format",), "index-format-mismatch"),
        (("bit_width",), "index-bit-width-mismatch"),
        (
            (
                "turbovec_version",
                "turbovec_source_commit",
                "turbovec_wheel_sha256",
                "turbovec_license",
                "dependency_lock_sha256",
                "requested_provider",
                "actual_provider",
            ),
            "index-package-mismatch",
        ),
    )
    for fields, code in groups:
        if any(getattr(actual, field) != getattr(expected, field) for field in fields):
            raise ResearchError(code)


def _validate_artifacts(staging: Path, manifest: IndexManifest, validator) -> None:
    if not callable(validator):
        raise ResearchError("index-artifact-invalid")
    expected_names = {"manifest.json", *(artifact.filename for artifact in manifest.artifacts)}
    try:
        actual_names = {entry.name for entry in os.scandir(staging)}
    except Exception:
        raise ResearchError("index-artifact-invalid") from None
    if actual_names != expected_names:
        raise ResearchError("index-artifact-invalid")
    for artifact in manifest.artifacts:
        path = staging / artifact.filename
        try:
            _require_plain_file(path)
            digest, size = _hash_file(path)
            if size != artifact.size or digest != artifact.sha256:
                raise ResearchError("index-artifact-mismatch")
            result = validator(path, artifact, manifest)
            if result is False:
                raise ResearchError("index-artifact-invalid")
            final_digest, final_size = _hash_file(path)
            if final_size != artifact.size or final_digest != artifact.sha256:
                raise ResearchError("index-artifact-mismatch")
        except ResearchError:
            raise
        except Exception:
            raise ResearchError("index-artifact-invalid") from None


def _hash_file(path: Path) -> tuple[str, int]:
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as stream:
        opened = os.fstat(stream.fileno())
        current = path.stat(follow_symlinks=False)
        if not stat.S_ISREG(opened.st_mode) or (
            opened.st_dev,
            opened.st_ino,
        ) != (current.st_dev, current.st_ino):
            raise ResearchError("index-artifact-invalid")
        while True:
            block = stream.read(1024 * 1024)
            if not block:
                break
            size += len(block)
            digest.update(block)
    return digest.hexdigest(), size


def _bounded_read(path: Path, maximum: int) -> bytes:
    _require_no_reparse_chain(path.parent)
    _require_plain_file(path)
    try:
        with path.open("rb") as stream:
            opened = os.fstat(stream.fileno())
            current = path.stat(follow_symlinks=False)
            if (opened.st_dev, opened.st_ino) != (current.st_dev, current.st_ino):
                raise ResearchError("index-manifest-corrupt")
            data = stream.read(maximum + 1)
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-manifest-corrupt") from None
    if len(data) > maximum:
        raise ResearchError("index-manifest-corrupt")
    return data


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ResearchError("index-manifest-corrupt")
        result[key] = value
    return result


def _reject_json_constant():
    raise ResearchError("index-manifest-corrupt")


def _object(value: object, fields: set[str]) -> dict:
    if type(value) is not dict or set(value) != fields:
        raise ResearchError("index-manifest-corrupt")
    return value


def _array(value: object) -> list:
    if type(value) is not list:
        raise ResearchError("index-manifest-corrupt")
    return value


def _text(value: object) -> str:
    if (
        type(value) is not str
        or not value
        or len(value) > 512
        or any(ord(char) < 32 for char in value)
    ):
        raise ResearchError("index-manifest-invalid")
    return value


def _hash(value: object) -> str:
    if type(value) is not str or _HASH.fullmatch(value) is None:
        raise ResearchError("index-manifest-invalid")
    return value


def _commit(value: object) -> str:
    if type(value) is not str or re.fullmatch(r"[0-9a-f]{7,64}", value) is None:
        raise ResearchError("index-manifest-invalid")
    return value


def _positive_int(value: object) -> int:
    if type(value) is not int or value <= 0 or value > _UINT64_MAX:
        raise ResearchError("index-manifest-invalid")
    return value


def _nonnegative_int(value: object) -> int:
    if type(value) is not int or value < 0 or value > _UINT64_MAX:
        raise ResearchError("index-manifest-invalid")
    return value


def _uint64(value: object) -> int:
    if type(value) is not int or not 0 <= value <= _UINT64_MAX:
        raise ResearchError("index-manifest-invalid")
    return value


def _relative_path(value: object) -> str:
    _text(value)
    assert isinstance(value, str)
    if (
        "\\" in value
        or ":" in value
        or value.startswith("/")
        or value.endswith("/")
        or any(part in ("", ".", "..") for part in value.split("/"))
    ):
        raise ResearchError("index-manifest-invalid")
    for part in value.split("/"):
        _safe_windows_segment(part)
    return value


def _artifact_filename(value: object) -> str:
    _text(value)
    assert isinstance(value, str)
    if value in (".", "..") or "/" in value or "\\" in value or ":" in value:
        raise ResearchError("index-manifest-invalid")
    _safe_windows_segment(value)
    return value


def _safe_windows_segment(value: str) -> None:
    if value.endswith((" ", ".")) or any(char in '<>"|?*' for char in value):
        raise ResearchError("index-manifest-invalid")
    if value.split(".", 1)[0].upper() in _WINDOWS_RESERVED:
        raise ResearchError("index-manifest-invalid")


def _validate_utc(value: object) -> str:
    if type(value) is not str or _UTC.fullmatch(value) is None:
        raise ResearchError("index-manifest-invalid")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError:
        raise ResearchError("index-manifest-invalid") from None
    if parsed.tzinfo != timezone.utc:
        raise ResearchError("index-manifest-invalid")
    return value


def _validated_operation_id(value: str | None) -> str:
    result = uuid.uuid4().hex if value is None else value
    if (
        type(result) is not str
        or result in (".", "..")
        or result.endswith((" ", "."))
        or _OPERATION_ID.fullmatch(result) is None
    ):
        raise ResearchError("index-operation-id-invalid")
    return result


def _is_reparse(info: os.stat_result) -> bool:
    return bool(getattr(info, "st_file_attributes", 0) & _REPARSE_POINT)


def _require_plain_directory(path: Path) -> tuple[int, int]:
    _require_no_reparse_chain(path)
    try:
        info = path.stat(follow_symlinks=False)
    except Exception:
        raise ResearchError("index-path-invalid") from None
    if not stat.S_ISDIR(info.st_mode) or _is_reparse(info):
        raise ResearchError("index-path-invalid")
    return info.st_dev, info.st_ino


def _create_plain_directory(path: Path) -> tuple[int, int]:
    """Create missing components only after validating every existing ancestor."""
    absolute = path.absolute()
    missing = []
    current = absolute
    while True:
        try:
            current.stat(follow_symlinks=False)
            break
        except FileNotFoundError:
            missing.append(current)
            parent = current.parent
            if parent == current:
                raise ResearchError("index-path-invalid")
            current = parent
        except Exception:
            raise ResearchError("index-path-invalid") from None
    _require_plain_directory(current)
    for component in reversed(missing):
        try:
            component.mkdir()
        except FileExistsError:
            pass
        except Exception:
            raise ResearchError("index-path-invalid") from None
        _require_plain_directory(component)
    return _require_plain_directory(absolute)


def _require_same_directory(path: Path, identity: tuple[int, int]) -> None:
    if _require_plain_directory(path) != identity:
        raise ResearchError("index-path-invalid")


def _require_plain_file(path: Path) -> None:
    try:
        info = path.stat(follow_symlinks=False)
    except Exception:
        raise ResearchError("index-manifest-corrupt") from None
    if not stat.S_ISREG(info.st_mode) or _is_reparse(info):
        raise ResearchError("index-manifest-corrupt")


def _require_same_file(path: Path, identity: tuple[int, int]) -> None:
    _require_plain_file(path)
    info = path.stat(follow_symlinks=False)
    if (info.st_dev, info.st_ino) != identity:
        raise ResearchError("index-path-invalid")


def _require_safe_leaf(
    path: Path, *, may_not_exist: bool, must_not_exist: bool = False
) -> None:
    try:
        info = path.stat(follow_symlinks=False)
    except FileNotFoundError:
        if may_not_exist:
            return
        raise ResearchError("index-path-invalid") from None
    except Exception:
        raise ResearchError("index-path-invalid") from None
    if must_not_exist or _is_reparse(info) or not stat.S_ISREG(info.st_mode):
        raise ResearchError("index-path-invalid")


def _validate_destination_name(path: Path) -> None:
    if (
        path.name in ("", ".", "..")
        or ".." in path.parts
        or path.parent / path.name != path
    ):
        raise ResearchError("index-path-invalid")
    try:
        _safe_windows_segment(path.name)
    except ResearchError:
        raise ResearchError("index-path-invalid") from None


def _unlink_owned_file(path: Path, identity: tuple[int, int]) -> None:
    try:
        info = path.stat(follow_symlinks=False)
        if (
            stat.S_ISREG(info.st_mode)
            and not _is_reparse(info)
            and (info.st_dev, info.st_ino) == identity
        ):
            path.unlink()
    except Exception:
        pass


def _remove_owned_staging(path: Path, identity: tuple[int, int]) -> None:
    try:
        info = path.stat(follow_symlinks=False)
        if (
            stat.S_ISDIR(info.st_mode)
            and not _is_reparse(info)
            and (info.st_dev, info.st_ino) == identity
        ):
            shutil.rmtree(path)
    except Exception:
        pass


def _require_no_reparse_chain(path: Path) -> None:
    """Reject any existing symlink/junction in an intended directory chain."""
    try:
        absolute = path.absolute()
        for candidate in reversed((absolute, *absolute.parents)):
            info = candidate.stat(follow_symlinks=False)
            if _is_reparse(info) or stat.S_ISLNK(info.st_mode):
                raise ResearchError("index-path-invalid")
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-path-invalid") from None


def _fsync_directory(path: Path) -> None:
    try:
        descriptor = os.open(path, os.O_RDONLY)
        try:
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
    except OSError:
        pass


def _rename_directory_no_replace(source: Path, destination: Path) -> None:
    if sys.platform == "win32":
        ctypes.set_last_error(0)
        move_file = ctypes.windll.kernel32.MoveFileW
        move_file.argtypes = [ctypes.c_wchar_p, ctypes.c_wchar_p]
        move_file.restype = ctypes.c_int
        if not move_file(str(source.absolute()), str(destination.absolute())):
            if ctypes.get_last_error() in (80, 183):
                raise ResearchError("index-destination-exists")
            raise ResearchError("index-promotion-failed")
        return
    if sys.platform.startswith("linux"):
        library = ctypes.CDLL(None, use_errno=True)
        rename_at_two = getattr(library, "renameat2", None)
        if rename_at_two is None:
            raise ResearchError("index-promotion-unsupported")
        rename_at_two.argtypes = [
            ctypes.c_int,
            ctypes.c_char_p,
            ctypes.c_int,
            ctypes.c_char_p,
            ctypes.c_uint,
        ]
        rename_at_two.restype = ctypes.c_int
        result = rename_at_two(
            -100,
            os.fsencode(source.absolute()),
            -100,
            os.fsencode(destination.absolute()),
            1,
        )
        if result == 0:
            return
        if ctypes.get_errno() == 17:
            raise ResearchError("index-destination-exists")
        raise ResearchError("index-promotion-failed")
    raise ResearchError("index-promotion-unsupported")
