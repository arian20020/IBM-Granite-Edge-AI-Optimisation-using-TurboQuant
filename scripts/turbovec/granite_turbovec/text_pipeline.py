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
from dataclasses import dataclass
from pathlib import Path
from typing import Final

if os.name == "nt":
    import ctypes
    import msvcrt
    import ntpath
    from ctypes import wintypes

from .contracts import Chunk, Document, ResearchError


HARD_MAX_FILES: Final = 64
HARD_MAX_BYTES_PER_FILE: Final = 8 * 1024 * 1024
HARD_MAX_TOTAL_BYTES: Final = 32 * 1024 * 1024
MAX_CHUNK_CHARS: Final = 1_000_000
MAX_CHUNKS_PER_DOCUMENT: Final = 100_000
_SUPPORTED_EXTENSIONS: Final = frozenset({".txt", ".md"})
_FILE_ATTRIBUTE_REPARSE_POINT: Final = 0x400


@dataclass(frozen=True)
class _WindowsPathInfo:
    volume_serial: int
    file_index: int
    attributes: int
    final_path: str


if os.name == "nt":
    _FILE_READ_ATTRIBUTES: Final = 0x80
    _GENERIC_READ: Final = 0x80000000
    _FILE_SHARE_ALL: Final = 0x7
    _OPEN_EXISTING: Final = 3
    _FILE_FLAG_BACKUP_SEMANTICS: Final = 0x02000000
    _FILE_FLAG_OPEN_REPARSE_POINT: Final = 0x00200000
    _INVALID_HANDLE_VALUE: Final = ctypes.c_void_p(-1).value

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

    _kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    _kernel32.CreateFileW.argtypes = [
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    ]
    _kernel32.CreateFileW.restype = wintypes.HANDLE
    _kernel32.GetFileInformationByHandle.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(_ByHandleFileInformation),
    ]
    _kernel32.GetFileInformationByHandle.restype = wintypes.BOOL
    _kernel32.GetFinalPathNameByHandleW.argtypes = [
        wintypes.HANDLE,
        wintypes.LPWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
    ]
    _kernel32.GetFinalPathNameByHandleW.restype = wintypes.DWORD
    _kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    _kernel32.CloseHandle.restype = wintypes.BOOL


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
    allowed_root: str
    if _is_link_like(root, root_metadata):
        raise ResearchError("input-unsafe-link")
    if stat.S_ISREG(root_metadata.st_mode):
        validate_extension(root.name)
        if os.name == "nt":
            handle, path_information = _validated_windows_handle(
                root,
                read=False,
                allowed_root=None,
            )
            try:
                relative_path = _windows_actual_name(path_information.final_path)
                allowed_root = path_information.final_path
            finally:
                _close_windows_handle(handle)
        else:
            relative_path = root.name
            allowed_root = os.path.realpath(root)
        candidates = [(relative_path, root, root_metadata)]
    elif stat.S_ISDIR(root_metadata.st_mode):
        candidates, allowed_root = _discover_directory(root, file_limit)
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
        content = _read_bounded_file(path, metadata, byte_limit, allowed_root)
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


if os.name == "nt":

    def _open_windows_path(path: Path, *, read: bool) -> int:
        desired_access = _GENERIC_READ if read else _FILE_READ_ATTRIBUTES
        handle = _kernel32.CreateFileW(
            str(path),
            desired_access,
            _FILE_SHARE_ALL,
            None,
            _OPEN_EXISTING,
            _FILE_FLAG_BACKUP_SEMANTICS | _FILE_FLAG_OPEN_REPARSE_POINT,
            None,
        )
        if handle == _INVALID_HANDLE_VALUE:
            error = ctypes.get_last_error()
            code = "input-race-detected" if error in {2, 3} else "input-read-failed"
            raise ResearchError(code)
        return handle


    def _close_windows_handle(handle: int) -> None:
        _kernel32.CloseHandle(handle)


    def _get_windows_path_info(handle: int) -> _WindowsPathInfo:
        information = _ByHandleFileInformation()
        if not _kernel32.GetFileInformationByHandle(handle, ctypes.byref(information)):
            raise ResearchError("input-race-detected")
        buffer = ctypes.create_unicode_buffer(32_768)
        length = _kernel32.GetFinalPathNameByHandleW(handle, buffer, len(buffer), 0)
        if length == 0 or length >= len(buffer):
            raise ResearchError("input-race-detected")
        return _WindowsPathInfo(
            volume_serial=information.volume_serial_number,
            file_index=(information.file_index_high << 32) | information.file_index_low,
            attributes=information.file_attributes,
            final_path=buffer.value,
        )


    def _normalize_windows_final_path(path: str) -> str:
        if path.startswith("\\\\?\\UNC\\"):
            path = "\\\\" + path[8:]
        elif path.startswith("\\\\?\\"):
            path = path[4:]
        return ntpath.normcase(ntpath.normpath(path))


    def _windows_path_is_beneath(path: str, root: str) -> bool:
        normalized_path = _normalize_windows_final_path(path)
        normalized_root = _normalize_windows_final_path(root)
        try:
            return ntpath.commonpath((normalized_path, normalized_root)) == normalized_root
        except ValueError:
            return False


    def _windows_actual_name(final_path: str) -> str:
        path = final_path[4:] if final_path.startswith("\\\\?\\") else final_path
        return ntpath.basename(path)


    def _validated_windows_handle(
        path: Path,
        *,
        read: bool,
        allowed_root: str | None,
    ) -> tuple[int, _WindowsPathInfo]:
        handle = _open_windows_path(path, read=read)
        try:
            information = _get_windows_path_info(handle)
            if information.attributes & _FILE_ATTRIBUTE_REPARSE_POINT:
                raise ResearchError("input-unsafe-link")
            if allowed_root is not None and not _windows_path_is_beneath(
                information.final_path,
                allowed_root,
            ):
                raise ResearchError("input-unsafe-link")
            return handle, information
        except ResearchError:
            _close_windows_handle(handle)
            raise


def _same_windows_identity(left: _WindowsPathInfo, right: _WindowsPathInfo) -> bool:
    return left.volume_serial == right.volume_serial and left.file_index == right.file_index


def _directory_snapshot(directory: Path, allowed_root: str | None) -> tuple[list[str], str]:
    """Enumerate a directory without accepting entries before identity revalidation."""

    if os.name == "nt":
        original_handle, before = _validated_windows_handle(
            directory,
            read=False,
            allowed_root=allowed_root,
        )
        root = before.final_path if allowed_root is None else allowed_root
        try:
            # Python 3.12 cannot pass a Windows directory handle to scandir.
            # Hold the no-follow handle, collect names only, then reopen the
            # path and compare volume/file identity before accepting any name.
            try:
                with os.scandir(directory) as entries:
                    names = [entry.name for entry in entries]
            except OSError:
                raise ResearchError("input-race-detected") from None

            after_original = _get_windows_path_info(original_handle)
            replacement_handle, after_path = _validated_windows_handle(
                directory,
                read=False,
                allowed_root=root,
            )
            try:
                if not _same_windows_identity(before, after_original) or not _same_windows_identity(
                    before,
                    after_path,
                ):
                    raise ResearchError("input-race-detected")
            finally:
                _close_windows_handle(replacement_handle)
        finally:
            _close_windows_handle(original_handle)
        return names, root

    flags = os.O_RDONLY
    flags |= getattr(os, "O_DIRECTORY", 0)
    flags |= getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(directory, flags)
    except OSError:
        raise ResearchError("input-race-detected") from None
    try:
        before = os.fstat(descriptor)
        canonical = os.path.realpath(directory)
        root = canonical if allowed_root is None else allowed_root
        try:
            if os.path.commonpath((canonical, root)) != root:
                raise ResearchError("input-unsafe-link")
        except ValueError:
            raise ResearchError("input-unsafe-link") from None
        with os.scandir(descriptor) as entries:
            names = [entry.name for entry in entries]
        after = os.fstat(descriptor)
        if (before.st_dev, before.st_ino) != (after.st_dev, after.st_ino):
            raise ResearchError("input-race-detected")
        return names, root
    except ResearchError:
        raise
    except OSError:
        raise ResearchError("input-race-detected") from None
    finally:
        os.close(descriptor)


def _revalidate_directory_for_queue(
    directory: Path,
    discovered: os.stat_result,
    allowed_root: str,
) -> None:
    if os.name == "nt":
        handle, information = _validated_windows_handle(
            directory,
            read=False,
            allowed_root=allowed_root,
        )
        try:
            if discovered.st_ino and discovered.st_ino != information.file_index:
                raise ResearchError("input-race-detected")
        finally:
            _close_windows_handle(handle)
        return

    flags = os.O_RDONLY | getattr(os, "O_DIRECTORY", 0) | getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(directory, flags)
    except OSError:
        raise ResearchError("input-race-detected") from None
    try:
        opened = os.fstat(descriptor)
        if not _same_metadata(opened, discovered):
            raise ResearchError("input-race-detected")
        canonical = os.path.realpath(directory)
        try:
            if os.path.commonpath((canonical, allowed_root)) != allowed_root:
                raise ResearchError("input-unsafe-link")
        except ValueError:
            raise ResearchError("input-unsafe-link") from None
    finally:
        os.close(descriptor)


def _discover_directory(
    root: Path,
    file_limit: int,
) -> tuple[list[tuple[str, Path, os.stat_result]], str]:
    candidates: list[tuple[str, Path, os.stat_result]] = []
    pending = [root]
    allowed_root: str | None = None
    while pending:
        directory = pending.pop()
        names, allowed_root = _directory_snapshot(directory, allowed_root)

        for name in names:
            path = directory / name
            try:
                metadata = path.lstat()
            except OSError:
                raise ResearchError("input-race-detected") from None

            relative_path = "/".join(path.relative_to(root).parts)
            link_like = _is_link_like(path, metadata)
            if link_like:
                if path.suffix.casefold() in _SUPPORTED_EXTENSIONS:
                    raise ResearchError("input-unsafe-link")
                continue
            if stat.S_ISDIR(metadata.st_mode):
                if allowed_root is None:
                    raise ResearchError("input-race-detected")
                _revalidate_directory_for_queue(path, metadata, allowed_root)
                pending.append(path)
            elif stat.S_ISREG(metadata.st_mode) and path.suffix.casefold() in _SUPPORTED_EXTENSIONS:
                candidates.append((relative_path, path, metadata))
                if len(candidates) > file_limit:
                    raise ResearchError("input-file-count-limit")
    if allowed_root is None:
        raise ResearchError("input-race-detected")
    return candidates, allowed_root


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


def _read_bounded_file(
    path: Path,
    discovered: os.stat_result,
    byte_limit: int,
    allowed_root: str,
) -> bytes:
    windows_handle: int | None = None
    try:
        if os.name == "nt":
            windows_handle, _ = _validated_windows_handle(
                path,
                read=True,
                allowed_root=allowed_root,
            )
            try:
                descriptor = msvcrt.open_osfhandle(windows_handle, os.O_RDONLY | os.O_BINARY)
            except OSError:
                _close_windows_handle(windows_handle)
                windows_handle = None
                raise
            windows_handle = None  # descriptor now owns and closes the Win32 handle
            source_context = os.fdopen(descriptor, "rb")
        else:
            source_context = path.open("rb")

        with source_context as source:
            opened = os.fstat(source.fileno())
            if not stat.S_ISREG(opened.st_mode) or not _same_metadata(opened, discovered):
                raise ResearchError("input-race-detected")
            if os.name != "nt":
                descriptor_path = Path(f"/proc/self/fd/{source.fileno()}")
                opened_path = os.path.realpath(descriptor_path if descriptor_path.exists() else path)
                try:
                    if os.path.commonpath((opened_path, allowed_root)) != allowed_root:
                        raise ResearchError("input-unsafe-link")
                except ValueError:
                    raise ResearchError("input-unsafe-link") from None
            content = source.read(byte_limit + 1)
            after = os.fstat(source.fileno())
    except ResearchError:
        raise
    except OSError:
        raise ResearchError("input-read-failed") from None
    finally:
        if windows_handle is not None:
            _close_windows_handle(windows_handle)

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
        if len(chunks) >= MAX_CHUNKS_PER_DOCUMENT:
            raise ResearchError("chunk-limit-exceeded")
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
