"""Canonical, privacy-safe manifests and fail-closed index promotion."""

from __future__ import annotations

import ctypes
import errno
import hashlib
import json
import os
import re
import stat
import sys
import uuid
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

if os.name == "nt":
    from ctypes import wintypes

    _KERNEL32 = ctypes.WinDLL("kernel32", use_last_error=True)
    _INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value
    _DELETE = 0x00010000
    _GENERIC_READ = 0x80000000
    _GENERIC_WRITE = 0x40000000
    _FILE_READ_ATTRIBUTES = 0x0080
    _FILE_TRAVERSE = 0x0020
    _SYNCHRONIZE = 0x00100000
    _FILE_SHARE_ALL = 0x00000007
    _FILE_SHARE_READ_WRITE = 0x00000003
    _CREATE_NEW = 1
    _OPEN_EXISTING = 3
    _FILE_ATTRIBUTE_NORMAL = 0x00000080
    _FILE_FLAG_WRITE_THROUGH = 0x80000000
    _FILE_FLAG_BACKUP_SEMANTICS = 0x02000000
    _FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000
    _FILE_RENAME_INFO_CLASS = 3
    _FILE_DISPOSITION_INFO_CLASS = 4

    class _ByHandleFileInformation(ctypes.Structure):
        _fields_ = [
            ("file_attributes", wintypes.DWORD),
            ("creation_time", wintypes.FILETIME),
            ("last_access_time", wintypes.FILETIME),
            ("last_write_time", wintypes.FILETIME),
            ("volume_serial_number", wintypes.DWORD),
            ("file_size_high", wintypes.DWORD),
            ("file_size_low", wintypes.DWORD),
            ("number_of_links", wintypes.DWORD),
            ("file_index_high", wintypes.DWORD),
            ("file_index_low", wintypes.DWORD),
        ]

    class _FileRenameInfo(ctypes.Structure):
        _fields_ = [
            ("replace_if_exists", wintypes.BOOLEAN),
            ("root_directory", wintypes.HANDLE),
            ("file_name_length", wintypes.DWORD),
            ("file_name", wintypes.WCHAR * 1),
        ]

    class _FileDispositionInfo(ctypes.Structure):
        _fields_ = [("delete_file", wintypes.BOOLEAN)]

from .contracts import ResearchError


_HASH = re.compile(r"[0-9a-f]{64}\Z")
_OPERATION_ID = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,63}\Z")
_UTC = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?Z\Z")
_MAX_MANIFEST_BYTES = 4 * 1024 * 1024
_MAX_ARTIFACT_BYTES = 16 * 1024 * 1024 * 1024
_MAX_ARTIFACT_COUNT = 6_400_000
_MAX_STAGING_ENTRIES = 128
_MAX_STAGING_BYTES = _MAX_ARTIFACT_BYTES
_MAX_RETAINED_STAGING = 3
_MAX_PARENT_SCAN_ENTRIES = 1024
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
    python_version: str


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
    route: str | None = None
    backend: str | None = None
    index_format: str | None = None
    bit_width: int | None = None


@dataclass(frozen=True)
class IndexManifest:
    identity: IndexIdentity
    sources: tuple[SourceRecord, ...]
    chunks: tuple[ChunkRecord, ...]
    created_utc: str
    artifacts: tuple[ArtifactRecord, ...]


@dataclass(frozen=True)
class _SlotReservation:
    path: Path
    identity: tuple[int, int]
    handle: object
    index: int
    operation_id: str


@dataclass
class _SlotClaim:
    path: Path
    identity: tuple[int, int]
    handle: object
    operation_id: str


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
        if text not in (canonical_json(loaded), _legacy_canonical_json(loaded)):
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
    manifest: IndexManifest | None,
    writer: Callable[[Path], None],
    artifact_validator: Callable[[Path, ArtifactRecord, IndexManifest], object],
    *,
    operation_id: str | None = None,
    manifest_factory: Callable[[Path], IndexManifest] | None = None,
) -> Path:
    """Build, validate, then atomically publish a new sibling index directory."""
    if (manifest is None) == (manifest_factory is None):
        raise ResearchError("index-manifest-invalid")
    expected = _validate_manifest(manifest) if manifest is not None else None
    operation = _validated_operation_id(operation_id)
    target = Path(destination)
    parent = target.parent
    staging = None
    slot = None
    owned = False
    staging_identity = None
    promotion_handle = None
    parent_handle = None
    try:
        parent_identity = _create_plain_directory(parent)
        _validate_destination_name(target)
        _require_destination_absent(target)
        slot = _reserve_slot(target, operation, parent_identity)
        staging = parent / (
            f"{target.name}.slot-{slot.index}.staging-{operation}"
        )
        _require_safe_leaf(staging, may_not_exist=True, must_not_exist=True)
        staging.mkdir()
        owned = True
        staging_identity = _require_plain_directory(staging)
        writer(staging)
        _scan_staging_bounded(staging)
        if manifest_factory is not None:
            try:
                expected = _validate_manifest(manifest_factory(staging))
            except ResearchError:
                raise
            except Exception:
                raise ResearchError("index-manifest-invalid") from None
        if expected is None:
            raise ResearchError("index-manifest-invalid")
        _require_same_directory(parent, parent_identity)
        _require_same_directory(staging, staging_identity)
        parent_handle = _open_validated_directory_handle(parent, parent_identity)
        promotion_handle = _open_validated_directory_handle(
            staging, staging_identity, delete_access=True, write_through=True
        )

        _fsync_declared_artifacts(staging, expected)

        write_manifest_atomic(
            staging / "manifest.json", expected, operation_id=operation
        )
        loaded = load_and_validate_manifest(staging / "manifest.json", expected)
        _validate_artifacts(staging, loaded, artifact_validator)
        _fsync_declared_artifacts(staging, loaded)
        _fsync_directory(staging)
        _fsync_directory(parent)
        _require_same_directory(parent, parent_identity)
        _require_same_directory(staging, staging_identity)
        _validate_open_handle_matches_path(promotion_handle, staging)
        _validate_open_handle_matches_path(parent_handle, parent)
        _promotion_race_hook(staging)
        _promote_validated_handle(
            promotion_handle, parent_handle, target, staging_identity
        )
        owned = False
        _validate_open_handle_matches_path(promotion_handle, target)
        _fsync_directory(parent)
        _release_slot(slot, parent_handle, target)
        return target
    except ResearchError:
        if owned and staging_identity is not None and not _quarantine_owned_staging(
            staging, staging_identity, parent, parent_identity
        ):
            raise ResearchError("index-quarantine-failed") from None
        raise
    except Exception:
        if owned and staging_identity is not None and not _quarantine_owned_staging(
            staging, staging_identity, parent, parent_identity
        ):
            raise ResearchError("index-quarantine-failed") from None
        raise ResearchError("index-promotion-failed") from None
    finally:
        _close_native_handle(promotion_handle)
        _close_native_handle(parent_handle)
        if slot is not None:
            _close_native_handle(slot.handle)


def list_retained_staging(destination: str | Path) -> tuple[Path, ...]:
    """List retained staging/quarantine entries for deliberate manual maintenance."""
    target = Path(destination)
    _validate_destination_name(target)
    parent = target.parent
    if not parent.exists():
        return ()
    _require_plain_directory(parent)
    return _retained_staging_entries(target)


def _reserve_slot(
    target: Path,
    operation_id: str,
    parent_identity: tuple[int, int],
) -> _SlotReservation:
    for index in range(_MAX_RETAINED_STAGING):
        claim = _acquire_slot_claim(
            target, index, operation_id, parent_identity
        )
        if claim is None:
            continue
        reservation = None
        try:
            _slot_transition_claim_hook(claim.path, "reserve")
            reservation = _reserve_claimed_slot(target, index, operation_id)
        except ResearchError as error:
            if error.code not in (
                "index-destination-exists",
                "index-path-invalid",
                "index-promotion-failed",
                "index-maintenance-required",
            ):
                raise
            reservation = None
        except Exception:
            reservation = None
        finally:
            try:
                _release_slot_claim(claim)
            except Exception:
                if reservation is not None:
                    _close_native_handle(reservation.handle)
                raise
        if reservation is not None:
            return reservation
    raise ResearchError("index-maintenance-required")


def _reserve_claimed_slot(
    target: Path, index: int, operation_id: str
) -> _SlotReservation | None:
    slot = target.parent / f"{target.name}.slot-{index}"
    available = slot.with_name(f"{slot.name}.available")
    if _path_entry_exists(slot):
        return None

    available_handle = None
    parent_handle = None
    keep_handle = False
    try:
        available_identity = _require_plain_directory(available)
        _validate_available_slot(available, available_identity)
        parent_identity = _require_plain_directory(target.parent)
        parent_handle = _open_validated_directory_handle(
            target.parent, parent_identity
        )
        available_handle = _open_validated_directory_handle(
            available,
            available_identity,
            delete_access=True,
            write_through=True,
        )
        _slot_acquire_race_hook(available)
        _promote_validated_handle(
            available_handle, parent_handle, slot, available_identity
        )
        _validate_open_handle_matches_path(available_handle, slot)
        _write_slot_owner(slot, available_identity, operation_id)
        keep_handle = True
        return _SlotReservation(
            slot,
            available_identity,
            available_handle,
            index,
            operation_id,
        )
    except ResearchError as error:
        if error.code not in (
            "index-destination-exists",
            "index-path-invalid",
            "index-promotion-failed",
            "index-maintenance-required",
        ):
            raise
    finally:
        _close_native_handle(parent_handle)
        if available_handle is not None and not keep_handle:
            _close_native_handle(available_handle)

    if _path_entry_exists(available) or _path_entry_exists(slot):
        return None
    try:
        slot.mkdir()
    except FileExistsError:
        return None
    except Exception:
        raise ResearchError("index-maintenance-required") from None
    try:
        identity = _require_plain_directory(slot)
        _write_slot_owner(slot, identity, operation_id, first_write=True)
        _fsync_directory(target.parent)
        handle = _open_validated_directory_handle(
            slot, identity, delete_access=True, write_through=True
        )
        return _SlotReservation(slot, identity, handle, index, operation_id)
    except ResearchError:
        # A partial/stale slot is deliberately retained and consumes capacity.
        raise
    except Exception:
        # A partial/stale slot is deliberately retained and consumes capacity.
        raise ResearchError("index-maintenance-required") from None


def _release_slot(
    slot: _SlotReservation,
    parent_handle,
    target: Path,
) -> None:
    _validate_open_handle_matches_path(parent_handle, target.parent)
    parent_identity = _require_plain_directory(target.parent)
    claim = _acquire_slot_claim(
        target, slot.index, slot.operation_id, parent_identity
    )
    if claim is None:
        raise ResearchError("index-maintenance-required")
    try:
        _slot_transition_claim_hook(claim.path, "release")
        _validate_open_handle_matches_path(slot.handle, slot.path)
        _slot_release_race_hook(slot.path)
        released = target.parent / f"{target.name}.slot-{slot.index}.available"
        _promote_validated_handle(
            slot.handle, parent_handle, released, slot.identity
        )
        _fsync_directory(target.parent)
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-maintenance-required") from None
    finally:
        _release_slot_claim(claim)


def _slot_release_race_hook(path: Path) -> None:
    """Test seam after slot validation and before handle-bound release."""


def _slot_acquire_race_hook(path: Path) -> None:
    """Test seam after available-slot validation and before handle-bound acquire."""


def _slot_transition_claim_hook(path: Path, phase: str) -> None:
    """Test seam while the exclusive slot-transition claim is held."""


def _acquire_slot_claim(
    target: Path,
    index: int,
    operation_id: str,
    parent_identity: tuple[int, int],
) -> _SlotClaim | None:
    if os.name != "nt":
        raise ResearchError("index-promotion-unsupported")
    claim_path = target.parent / f"{target.name}.slot-{index}.claim"
    _require_same_directory(target.parent, parent_identity)
    _require_safe_leaf(claim_path, may_not_exist=True)
    payload = json.dumps(
        {"operation_id": operation_id, "schema_version": 1},
        ensure_ascii=False,
        allow_nan=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    create_file = _KERNEL32.CreateFileW
    create_file.argtypes = [
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    ]
    create_file.restype = wintypes.HANDLE
    ctypes.set_last_error(0)
    handle = create_file(
        str(claim_path.absolute()),
        _GENERIC_READ
        | _GENERIC_WRITE
        | _DELETE
        | _FILE_READ_ATTRIBUTES
        | _SYNCHRONIZE,
        _FILE_SHARE_READ_WRITE,
        None,
        _CREATE_NEW,
        _FILE_ATTRIBUTE_NORMAL | _FILE_FLAG_WRITE_THROUGH,
        None,
    )
    if handle == _INVALID_HANDLE_VALUE:
        if ctypes.get_last_error() in (80, 183):
            return None
        raise ResearchError("index-maintenance-required")
    try:
        write_file = _KERNEL32.WriteFile
        write_file.argtypes = [
            wintypes.HANDLE,
            wintypes.LPCVOID,
            wintypes.DWORD,
            ctypes.POINTER(wintypes.DWORD),
            wintypes.LPVOID,
        ]
        write_file.restype = wintypes.BOOL
        written = wintypes.DWORD()
        buffer = ctypes.create_string_buffer(payload)
        ctypes.set_last_error(0)
        if not write_file(
            handle,
            buffer,
            len(payload),
            ctypes.byref(written),
            None,
        ) or written.value != len(payload):
            raise ResearchError("index-maintenance-required")
        flush = _KERNEL32.FlushFileBuffers
        flush.argtypes = [wintypes.HANDLE]
        flush.restype = wintypes.BOOL
        ctypes.set_last_error(0)
        if not flush(handle):
            raise ResearchError("index-maintenance-required")
        identity = _native_handle_identity(handle)
        _require_same_directory(target.parent, parent_identity)
        _validate_open_handle_matches_path(handle, claim_path)
        _fsync_directory(target.parent)
        return _SlotClaim(claim_path, identity, handle, operation_id)
    except ResearchError:
        # Closing without disposition deliberately preserves an uncertain claim.
        _close_native_handle(handle)
        raise
    except Exception:
        # Closing without disposition deliberately preserves an uncertain claim.
        _close_native_handle(handle)
        raise ResearchError("index-maintenance-required") from None


def _release_slot_claim(claim: _SlotClaim) -> None:
    if claim.handle is None:
        raise ResearchError("index-maintenance-required")
    try:
        _validate_open_handle_matches_path(claim.handle, claim.path)
        if _native_handle_identity(claim.handle) != claim.identity:
            raise ResearchError("index-maintenance-required")
        disposition = _FileDispositionInfo(True)
        set_information = _KERNEL32.SetFileInformationByHandle
        set_information.argtypes = [
            wintypes.HANDLE,
            ctypes.c_int,
            wintypes.LPVOID,
            wintypes.DWORD,
        ]
        set_information.restype = wintypes.BOOL
        ctypes.set_last_error(0)
        if not set_information(
            claim.handle,
            _FILE_DISPOSITION_INFO_CLASS,
            ctypes.byref(disposition),
            ctypes.sizeof(disposition),
        ):
            raise ResearchError("index-maintenance-required")
    except Exception:
        # No path-based deletion fallback: an uncertain claim remains stale.
        _close_native_handle(claim.handle)
        claim.handle = None
        raise ResearchError("index-maintenance-required") from None

    close_handle = _KERNEL32.CloseHandle
    close_handle.argtypes = [wintypes.HANDLE]
    close_handle.restype = wintypes.BOOL
    ctypes.set_last_error(0)
    if not close_handle(claim.handle):
        claim.handle = None
        raise ResearchError("index-maintenance-required")
    claim.handle = None
    _fsync_directory(claim.path.parent)
    if _path_entry_exists(claim.path):
        raise ResearchError("index-maintenance-required")


def _validate_available_slot(path: Path, identity: tuple[int, int]) -> None:
    _require_same_directory(path, identity)
    try:
        with os.scandir(path) as entries:
            names = [entry.name for entry in entries]
        if names != ["owner.json"]:
            raise ResearchError("index-maintenance-required")
        owner = path / "owner.json"
        info = owner.stat(follow_symlinks=False)
        if not stat.S_ISREG(info.st_mode) or info.st_size > 512:
            raise ResearchError("index-maintenance-required")
        raw = _bounded_read(owner, 512)
        parsed = json.loads(
            raw.decode("utf-8", errors="strict"),
            object_pairs_hook=_unique_object,
            parse_constant=lambda _: _reject_json_constant(),
        )
        if type(parsed) is not dict or set(parsed) != {
            "operation_id",
            "schema_version",
        }:
            raise ResearchError("index-maintenance-required")
        if (
            parsed["schema_version"] != 1
            or type(parsed["schema_version"]) is not int
        ):
            raise ResearchError("index-maintenance-required")
        _validated_operation_id(parsed["operation_id"])
        canonical = json.dumps(
            parsed,
            ensure_ascii=False,
            allow_nan=False,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("utf-8")
        if raw != canonical:
            raise ResearchError("index-maintenance-required")
        _require_same_directory(path, identity)
    except Exception:
        raise ResearchError("index-maintenance-required") from None


def _write_slot_owner(
    path: Path,
    identity: tuple[int, int],
    operation_id: str,
    *,
    first_write: bool = False,
) -> None:
    payload = json.dumps(
        {"operation_id": operation_id, "schema_version": 1},
        ensure_ascii=False,
        allow_nan=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    owner = path / "owner.json"
    temporary = path / f"owner.json.tmp-{operation_id}"
    temporary_identity = None
    created = False
    try:
        _require_same_directory(path, identity)
        _require_safe_leaf(temporary, may_not_exist=True, must_not_exist=True)
        with temporary.open("xb") as stream:
            created = True
            opened = os.fstat(stream.fileno())
            temporary_identity = (opened.st_dev, opened.st_ino)
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        _require_same_file(temporary, temporary_identity)
        _require_same_directory(path, identity)
        if first_write:
            _require_safe_leaf(owner, may_not_exist=True, must_not_exist=True)
        os.replace(temporary, owner)
        created = False
        _require_same_directory(path, identity)
        _fsync_directory(path)
    except ResearchError:
        if created and temporary_identity is not None:
            _unlink_owned_file(temporary, temporary_identity)
        raise
    except Exception:
        if created and temporary_identity is not None:
            _unlink_owned_file(temporary, temporary_identity)
        raise ResearchError("index-maintenance-required") from None


def _retained_staging_entries(target: Path) -> tuple[Path, ...]:
    legacy_prefix = f"{target.name}.staging-".casefold()
    released_prefix = f"{target.name}.released-slot-".casefold()
    slot_prefixes = {
        index: f"{target.name}.slot-{index}".casefold()
        for index in range(_MAX_RETAINED_STAGING)
    }
    legacy = []
    slots = {}
    claims = {}
    trees = {index: [] for index in range(_MAX_RETAINED_STAGING)}
    scanned = 0
    try:
        with os.scandir(target.parent) as entries:
            for entry in entries:
                scanned += 1
                if scanned > _MAX_PARENT_SCAN_ENTRIES:
                    raise ResearchError("index-maintenance-required")
                folded = entry.name.casefold()
                path = target.parent / entry.name
                if folded.startswith(legacy_prefix) or folded.startswith(
                    released_prefix
                ):
                    legacy.append(path)
                    continue
                for index, prefix in slot_prefixes.items():
                    if folded == prefix:
                        slots[index] = path
                    elif folded == prefix + ".claim":
                        claims[index] = path
                    elif folded.startswith(prefix + ".staging-"):
                        trees[index].append(path)
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-maintenance-required") from None
    retained = list(legacy)
    for index in range(_MAX_RETAINED_STAGING):
        if index in claims:
            retained.append(claims[index])
        if trees[index]:
            retained.extend(trees[index])
        elif index in slots:
            retained.append(slots[index])
    return tuple(sorted(retained, key=lambda item: (item.name.casefold(), item.name)))


def _scan_staging_bounded(staging: Path) -> None:
    pending = [staging]
    entries = 0
    total_bytes = 0
    try:
        while pending:
            directory = pending.pop()
            with os.scandir(directory) as children:
                for child in children:
                    entries += 1
                    if entries > _MAX_STAGING_ENTRIES:
                        raise ResearchError("index-staging-limit-exceeded")
                    info = child.stat(follow_symlinks=False)
                    if stat.S_ISREG(info.st_mode):
                        total_bytes += info.st_size
                        if total_bytes > _MAX_STAGING_BYTES:
                            raise ResearchError("index-staging-limit-exceeded")
                    elif stat.S_ISDIR(info.st_mode) and not _is_reparse(info):
                        pending.append(Path(child.path))
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-staging-limit-exceeded") from None


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
    if len(artifact_names) != len({name.casefold() for name in artifact_names}):
        raise ResearchError("index-manifest-invalid")
    if any(
        item.count != len(chunks) or item.dimension != identity.dimension
        for item in artifacts
    ):
        raise ResearchError("index-manifest-invalid")
    if identity.actual_backend == "matched-suite":
        if any(item.route is None for item in artifacts):
            raise ResearchError("index-manifest-invalid")
        route_records = {item.route: item for item in artifacts if item.route != "metadata"}
        if set(route_records) - {"float32", "2bit", "4bit"} or "float32" not in route_records:
            raise ResearchError("index-manifest-invalid")
        if not ({"2bit", "4bit"} & set(route_records)):
            raise ResearchError("index-manifest-invalid")
    if any(item.backend == "turbovec" for item in artifacts) and (
        identity.turbovec_version is None
        or identity.turbovec_source_commit is None
        or identity.turbovec_wheel_sha256 is None
        or identity.turbovec_license is None
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
    if type(value.python_version) is not str or re.fullmatch(
        r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)",
        value.python_version,
    ) is None:
        raise ResearchError("index-manifest-invalid")
    if value.requested_backend not in ("float32", "turbovec", "matched-suite"):
        raise ResearchError("index-manifest-invalid")
    if value.requested_backend != value.actual_backend:
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
    elif value.actual_backend == "matched-suite":
        if value.index_format != "matched-suite-v1" or value.bit_width is not None:
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
    if value.size > _MAX_ARTIFACT_BYTES or value.count > _MAX_ARTIFACT_COUNT:
        raise ResearchError("index-manifest-invalid")
    _text(value.magic)
    _positive_int(value.version)
    _positive_int(value.dimension)
    route_fields = (value.route, value.backend, value.index_format, value.bit_width)
    if any(item is not None for item in route_fields):
        if value.route == "float32":
            if value.filename != "vectors-float32.npy" or value.backend != "float32" or value.index_format != "float32-npy-v1" or value.bit_width is not None:
                raise ResearchError("index-manifest-invalid")
        elif value.route in ("2bit", "4bit"):
            bits = int(value.route[0])
            if value.filename != f"index-{value.route}.tvim" or value.backend != "turbovec" or value.index_format != "gtvi-turbovec-v1" or value.bit_width != bits:
                raise ResearchError("index-manifest-invalid")
        elif value.route == "metadata":
            if value.backend != "metadata" or type(value.index_format) is not str or not value.index_format or value.bit_width is not None:
                raise ResearchError("index-manifest-invalid")
        else:
            raise ResearchError("index-manifest-invalid")
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
        artifacts=tuple(_artifact_from_mapping(item) for item in artifacts_value),
    )
    try:
        return _validate_manifest(result)
    except ResearchError:
        raise ResearchError("index-manifest-corrupt") from None


def _artifact_from_mapping(value: object) -> ArtifactRecord:
    full = set(ArtifactRecord.__dataclass_fields__)
    legacy = full - {"route", "backend", "index_format", "bit_width"}
    if type(value) is not dict:
        raise ResearchError("index-manifest-corrupt")
    fields = set(value)
    if fields == legacy:
        return ArtifactRecord(**value)
    if fields == full:
        return ArtifactRecord(**value)
    raise ResearchError("index-manifest-corrupt")


def _legacy_canonical_json(value: IndexManifest) -> str:
    payload = asdict(value)
    for artifact in payload["artifacts"]:
        if any(artifact[field] is not None for field in ("route", "backend", "index_format", "bit_width")):
            return ""
        for field in ("route", "backend", "index_format", "bit_width"):
            artifact.pop(field)
    return json.dumps(payload, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True)


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
        (("python_version",), "index-python-mismatch"),
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
            digest, size = _hash_file(path, artifact.size)
            if size != artifact.size or digest != artifact.sha256:
                raise ResearchError("index-artifact-mismatch")
            result = validator(path, artifact, manifest)
            if result is not True:
                raise ResearchError("index-artifact-invalid")
            final_digest, final_size = _hash_file(path, artifact.size)
            if final_size != artifact.size or final_digest != artifact.sha256:
                raise ResearchError("index-artifact-mismatch")
        except ResearchError:
            raise
        except Exception:
            raise ResearchError("index-artifact-invalid") from None


def _hash_file(path: Path, expected_size: int) -> tuple[str, int]:
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
        remaining = expected_size
        while remaining:
            block = stream.read(min(1024 * 1024, remaining))
            if not block:
                raise ResearchError("index-artifact-mismatch")
            size += len(block)
            remaining -= len(block)
            digest.update(block)
        if stream.read(1):
            raise ResearchError("index-artifact-mismatch")
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
    folded = value.casefold()
    if (
        folded == "manifest.json"
        or folded.startswith("manifest.json.tmp-")
        or ".staging-" in folded
        or ".quarantine-" in folded
        or ".tmp-" in folded
        or ".slot-" in folded
        or ".released-slot-" in folded
    ):
        raise ResearchError("index-manifest-invalid")
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
    folded = path.name.casefold()
    if any(
        marker in folded
        for marker in (
            ".staging-",
            ".quarantine-",
            ".tmp-",
            ".slot-",
            ".released-slot-",
        )
    ):
        raise ResearchError("index-path-invalid")


def _require_destination_absent(path: Path) -> None:
    try:
        path.stat(follow_symlinks=False)
    except FileNotFoundError:
        return
    except Exception:
        raise ResearchError("index-path-invalid") from None
    raise ResearchError("index-destination-exists")


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


def _quarantine_owned_staging(
    path: Path,
    identity: tuple[int, int],
    parent: Path,
    parent_identity: tuple[int, int],
) -> bool:
    """Move known-owned staging aside; never recursively delete by path."""
    try:
        _require_same_directory(parent, parent_identity)
        _require_same_directory(path, identity)
        _quarantine_race_hook(path)
        _require_same_directory(parent, parent_identity)
        _require_same_directory(path, identity)
        quarantine = parent / f"{path.name}.quarantine-{uuid.uuid4().hex}"
        _require_safe_leaf(quarantine, may_not_exist=True, must_not_exist=True)
        _rename_directory_no_replace(path, quarantine)
        _require_same_directory(quarantine, identity)
        _fsync_directory(parent)
        return True
    except Exception:
        return False


def _quarantine_race_hook(path: Path) -> None:
    """Test seam immediately before the final ownership check and rename."""


def _promotion_race_hook(path: Path) -> None:
    """Test seam after final validation and immediately before handle rename."""


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


def _fsync_declared_artifacts(staging: Path, manifest: IndexManifest) -> None:
    for artifact in manifest.artifacts:
        _fsync_file(staging / artifact.filename)


def _fsync_file(path: Path) -> None:
    try:
        _require_plain_file(path)
        with path.open("r+b") as stream:
            os.fsync(stream.fileno())
    except ResearchError:
        raise
    except Exception:
        raise ResearchError("index-durability-failed") from None


def _fsync_directory(path: Path) -> None:
    if os.name == "nt":
        _flush_windows_directory(path)
        return
    try:
        descriptor = os.open(path, os.O_RDONLY)
        try:
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
    except OSError as error:
        unsupported = {errno.EINVAL, errno.EBADF}
        if hasattr(errno, "ENOTSUP"):
            unsupported.add(errno.ENOTSUP)
        if hasattr(errno, "EOPNOTSUPP"):
            unsupported.add(errno.EOPNOTSUPP)
        if error.errno not in unsupported:
            raise ResearchError("index-durability-failed") from None


def _open_validated_directory_handle(
    path: Path,
    identity: tuple[int, int],
    *,
    delete_access: bool = False,
    write_through: bool = False,
):
    if os.name != "nt":
        raise ResearchError("index-promotion-unsupported")
    _require_same_directory(path, identity)
    access = _FILE_READ_ATTRIBUTES | _FILE_TRAVERSE | _SYNCHRONIZE
    if delete_access:
        access |= _DELETE
    if write_through:
        access |= _GENERIC_WRITE
    flags = _FILE_FLAG_BACKUP_SEMANTICS | _FILE_FLAG_OPEN_REPARSE_POINT
    if write_through:
        flags |= _FILE_FLAG_WRITE_THROUGH
    handle = _create_windows_directory_handle(path, access, flags)
    try:
        _require_same_directory(path, identity)
        _validate_open_handle_matches_path(handle, path)
        return handle
    except Exception:
        _close_native_handle(handle)
        raise


def _create_windows_directory_handle(
    path: Path,
    access: int,
    flags: int,
    *,
    failure_code: str = "index-promotion-unsupported",
):
    create_file = _KERNEL32.CreateFileW
    create_file.argtypes = [
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    ]
    create_file.restype = wintypes.HANDLE
    ctypes.set_last_error(0)
    handle = create_file(
        str(path.absolute()),
        access,
        _FILE_SHARE_ALL,
        None,
        _OPEN_EXISTING,
        flags,
        None,
    )
    if handle == _INVALID_HANDLE_VALUE:
        error = ctypes.get_last_error()
        if failure_code == "index-directory-fsync-unsupported" and error in (
            1,
            5,
            6,
            50,
        ):
            raise ResearchError("index-directory-fsync-unsupported")
        raise ResearchError(failure_code)
    return handle


def _native_handle_identity(handle) -> tuple[int, int]:
    information = _ByHandleFileInformation()
    get_information = _KERNEL32.GetFileInformationByHandle
    get_information.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(_ByHandleFileInformation),
    ]
    get_information.restype = wintypes.BOOL
    ctypes.set_last_error(0)
    if not get_information(handle, ctypes.byref(information)):
        raise ResearchError("index-promotion-failed")
    file_index = (information.file_index_high << 32) | information.file_index_low
    return information.volume_serial_number, file_index


def _validate_open_handle_matches_path(handle, path: Path) -> None:
    if os.name != "nt" or handle is None:
        raise ResearchError("index-promotion-unsupported")
    probe = _create_windows_directory_handle(
        path,
        _FILE_READ_ATTRIBUTES,
        _FILE_FLAG_BACKUP_SEMANTICS | _FILE_FLAG_OPEN_REPARSE_POINT,
    )
    try:
        if _native_handle_identity(handle) != _native_handle_identity(probe):
            raise ResearchError("index-path-invalid")
    finally:
        _close_native_handle(probe)


def _promote_validated_handle(
    source_handle,
    parent_handle,
    destination: Path,
    expected_identity: tuple[int, int],
) -> None:
    if os.name != "nt":
        raise ResearchError("index-promotion-unsupported")
    _native_handle_identity(parent_handle)
    _require_destination_absent(destination)
    file_name = str(destination.absolute()).encode("utf-16-le")
    offset = _FileRenameInfo.file_name.offset
    buffer = ctypes.create_string_buffer(offset + len(file_name) + 2)
    information = _FileRenameInfo.from_buffer(buffer)
    information.replace_if_exists = False
    information.root_directory = None
    information.file_name_length = len(file_name)
    ctypes.memmove(ctypes.addressof(buffer) + offset, file_name, len(file_name))
    set_information = _KERNEL32.SetFileInformationByHandle
    set_information.argtypes = [
        wintypes.HANDLE,
        ctypes.c_int,
        wintypes.LPVOID,
        wintypes.DWORD,
    ]
    set_information.restype = wintypes.BOOL
    ctypes.set_last_error(0)
    if not set_information(
        source_handle,
        _FILE_RENAME_INFO_CLASS,
        ctypes.byref(buffer),
        offset + len(file_name),
    ):
        error = ctypes.get_last_error()
        if error in (80, 183) or _path_entry_exists(destination):
            raise ResearchError("index-destination-exists")
        raise ResearchError("index-promotion-failed")
    current = destination.stat(follow_symlinks=False)
    if (current.st_dev, current.st_ino) != expected_identity:
        raise ResearchError("index-promotion-failed")


def _path_entry_exists(path: Path) -> bool:
    try:
        path.stat(follow_symlinks=False)
        return True
    except FileNotFoundError:
        return False
    except Exception:
        return False


def _close_native_handle(handle) -> None:
    if os.name == "nt" and handle not in (None, _INVALID_HANDLE_VALUE):
        _KERNEL32.CloseHandle(handle)


def _flush_windows_directory(path: Path) -> None:
    handle = None
    try:
        handle = _create_windows_directory_handle(
            path,
            _GENERIC_WRITE | _FILE_READ_ATTRIBUTES,
            _FILE_FLAG_BACKUP_SEMANTICS
            | _FILE_FLAG_OPEN_REPARSE_POINT
            | _FILE_FLAG_WRITE_THROUGH,
            failure_code="index-directory-fsync-unsupported",
        )
        flush = _KERNEL32.FlushFileBuffers
        flush.argtypes = [wintypes.HANDLE]
        flush.restype = wintypes.BOOL
        ctypes.set_last_error(0)
        if not flush(handle):
            error = ctypes.get_last_error()
            if error not in (1, 5, 6, 50):
                raise ResearchError("index-durability-failed")
    except ResearchError as error:
        if error.code != "index-directory-fsync-unsupported":
            raise
        # Directory flush is not supported uniformly on Windows filesystems.
    finally:
        _close_native_handle(handle)


def _rename_directory_no_replace(source: Path, destination: Path) -> None:
    if sys.platform == "win32":
        ctypes.set_last_error(0)
        move_file = _KERNEL32.MoveFileW
        move_file.argtypes = [ctypes.c_wchar_p, ctypes.c_wchar_p]
        move_file.restype = ctypes.c_int
        if not move_file(str(source.absolute()), str(destination.absolute())):
            if ctypes.get_last_error() in (80, 183) or _path_entry_exists(destination):
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
