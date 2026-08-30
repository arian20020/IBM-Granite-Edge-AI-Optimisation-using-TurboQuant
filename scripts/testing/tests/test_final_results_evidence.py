import hashlib
import sys
from pathlib import Path

import pytest


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.evidence import (
    build_evidence_record,
    hash_file,
    repo_relative,
    validate_sha256_manifest,
    write_sha256_manifest,
)


def test_repo_relative_returns_portable_path_and_rejects_outside_path(tmp_path):
    inside = tmp_path / "evidence" / "a.json"
    inside.parent.mkdir()
    inside.write_text("{}", encoding="utf-8")

    assert repo_relative(tmp_path, inside) == "evidence/a.json"
    with pytest.raises(ValueError, match="repository"):
        repo_relative(tmp_path, tmp_path.parent / "outside.json")


def test_repo_relative_rejects_symlink_resolution_escape(tmp_path):
    outside = tmp_path.parent / "evidence-outside.txt"
    outside.write_text("outside", encoding="utf-8")
    link = tmp_path / "evidence.txt"
    try:
        link.symlink_to(outside)
    except (OSError, NotImplementedError):
        pytest.skip("symlinks are unavailable")

    with pytest.raises(ValueError, match="repository"):
        repo_relative(tmp_path, link)


def test_hash_file_streams_sha256_and_requires_a_file(tmp_path):
    path = tmp_path / "large.bin"
    payload = b"abc" * (1024 * 1024 // 3 + 1)
    path.write_bytes(payload)

    assert hash_file(path) == hashlib.sha256(payload).hexdigest()
    with pytest.raises(FileNotFoundError):
        hash_file(tmp_path / "missing.bin")


def test_build_evidence_record_derives_stable_id_and_file_metadata(tmp_path):
    path = tmp_path / "evidence" / "a.json"
    path.parent.mkdir()
    path.write_text("{}", encoding="utf-8")
    digest = hash_file(path)

    record = build_evidence_record(
        repo_root=tmp_path,
        path=path,
        route_id="route-1",
        campaign_id="campaign-1",
        role="raw-result",
        source_label="source export",
    )

    assert record.relative_path == "evidence/a.json"
    assert record.sha256 == digest
    assert record.size_bytes == path.stat().st_size
    assert record.evidence_id == f"route-1-{digest[:12]}"


def test_build_evidence_record_uses_full_digest_for_id_collision(tmp_path):
    path = tmp_path / "result.json"
    path.write_text("{}", encoding="utf-8")
    digest = hash_file(path)

    first = build_evidence_record(
        tmp_path,
        path,
        "route-1",
        "campaign-1",
        "result",
    )
    second = build_evidence_record(
        tmp_path,
        path,
        "route-1",
        "campaign-1",
        "result",
        existing_evidence_ids={first.evidence_id},
    )

    assert first.evidence_id == f"route-1-{digest[:12]}"
    assert second.evidence_id == f"route-1-{digest}"


def test_build_evidence_record_rejects_duplicate_full_evidence_id(tmp_path):
    path = tmp_path / "result.json"
    path.write_text("{}", encoding="utf-8")
    record = build_evidence_record(tmp_path, path, "route-1", "campaign-1", "result")

    with pytest.raises(ValueError, match="duplicate"):
        build_evidence_record(
            tmp_path,
            path,
            "route-1",
            "campaign-1",
            "result",
            existing_evidence_ids={record.evidence_id, f"route-1-{hash_file(path)}"},
        )


def test_build_evidence_record_rejects_missing_and_outside_files(tmp_path):
    with pytest.raises(FileNotFoundError):
        build_evidence_record(
            tmp_path, tmp_path / "missing.json", "route-1", "campaign-1", "result"
        )
    outside = tmp_path.parent / "outside.json"
    outside.write_text("{}", encoding="utf-8")
    with pytest.raises(ValueError, match="repository"):
        build_evidence_record(tmp_path, outside, "route-1", "campaign-1", "result")


def test_write_sha256_manifest_sorts_paths_and_uses_posix_separators(tmp_path):
    first = tmp_path / "evidence" / "z.json"
    second = tmp_path / "evidence" / "a.json"
    first.parent.mkdir()
    first.write_text("z", encoding="utf-8")
    second.write_text("a", encoding="utf-8")
    manifest = tmp_path / "manifest-sha256.txt"

    write_sha256_manifest(tmp_path, (first, second), manifest)

    expected = (
        f"{hash_file(second)}  evidence/a.json\n"
        f"{hash_file(first)}  evidence/z.json\n"
    )
    assert manifest.read_text(encoding="utf-8") == expected


def test_write_sha256_manifest_rejects_duplicate_relative_paths(tmp_path):
    path = tmp_path / "evidence.json"
    path.write_text("{}", encoding="utf-8")

    with pytest.raises(ValueError, match="duplicate"):
        write_sha256_manifest(tmp_path, (path, path), tmp_path / "manifest.txt")


def test_validate_sha256_manifest_reports_missing_and_incorrect_hashes(tmp_path):
    path = tmp_path / "evidence.json"
    path.write_text("{}", encoding="utf-8")
    manifest = tmp_path / "manifest.txt"
    manifest.write_text(
        f"{'0' * 64}  evidence.json\n"
        f"{'1' * 64}  missing.json\n",
        encoding="utf-8",
        newline="\n",
    )

    errors = validate_sha256_manifest(tmp_path, manifest)

    assert errors == [
        "evidence.json: hash mismatch",
        "missing.json: file not found",
    ]


def test_validate_sha256_manifest_rejects_malformed_and_nonportable_lines(tmp_path):
    manifest = tmp_path / "manifest.txt"
    manifest.write_text(
        "not-a-checksum\n" + "a" * 64 + "  C:\\secret.json\n",
        encoding="utf-8",
        newline="\n",
    )

    errors = validate_sha256_manifest(tmp_path, manifest)

    assert errors == [
        "line 1: malformed checksum entry",
        "line 2: non-portable path",
    ]


def test_write_sha256_manifest_rejects_output_aliasing_an_input(tmp_path):
    source = tmp_path / "evidence.json"
    source.write_text("{}", encoding="utf-8")

    with pytest.raises(ValueError, match="input"):
        write_sha256_manifest(tmp_path, (source,), source)


def test_write_sha256_manifest_rejects_output_outside_root(tmp_path):
    source = tmp_path / "evidence.json"
    source.write_text("{}", encoding="utf-8")

    with pytest.raises(ValueError, match="repository"):
        write_sha256_manifest(tmp_path, (source,), tmp_path.parent / "manifest.txt")


def test_write_sha256_manifest_rejects_different_existing_bytes(tmp_path):
    source = tmp_path / "evidence.json"
    source.write_text("{}", encoding="utf-8")
    manifest = tmp_path / "manifest.txt"
    manifest.write_text("different\n", encoding="utf-8", newline="\n")

    with pytest.raises(FileExistsError):
        write_sha256_manifest(tmp_path, (source,), manifest)
    assert manifest.read_text(encoding="utf-8") == "different\n"


def test_write_sha256_manifest_accepts_identical_existing_bytes_idempotently(tmp_path):
    source = tmp_path / "evidence.json"
    source.write_text("{}", encoding="utf-8")
    manifest = tmp_path / "manifest.txt"
    write_sha256_manifest(tmp_path, (source,), manifest)
    original_bytes = manifest.read_bytes()
    original_mtime = manifest.stat().st_mtime_ns

    write_sha256_manifest(tmp_path, (source,), manifest)

    assert manifest.read_bytes() == original_bytes
    assert manifest.stat().st_mtime_ns == original_mtime


def test_validate_sha256_manifest_diagnoses_unsorted_entries(tmp_path):
    first = tmp_path / "evidence" / "a.json"
    second = tmp_path / "evidence" / "z.json"
    first.parent.mkdir()
    first.write_text("a", encoding="utf-8")
    second.write_text("z", encoding="utf-8")
    manifest = tmp_path / "manifest.txt"
    manifest.write_text(
        f"{hash_file(second)}  evidence/z.json\n"
        f"{hash_file(first)}  evidence/a.json\n",
        encoding="utf-8",
        newline="\n",
    )

    assert validate_sha256_manifest(tmp_path, manifest) == [
        "manifest: entries are not sorted"
    ]


def test_validate_sha256_manifest_diagnoses_crlf_bytes(tmp_path):
    source = tmp_path / "evidence.json"
    source.write_text("{}", encoding="utf-8")
    manifest = tmp_path / "manifest.txt"
    manifest.write_bytes(
        f"{hash_file(source)}  evidence.json\r\n".encode("ascii")
    )

    assert validate_sha256_manifest(tmp_path, manifest) == [
        "manifest: CRLF line endings are not canonical"
    ]


def test_validate_sha256_manifest_diagnoses_missing_final_newline(tmp_path):
    source = tmp_path / "evidence.json"
    source.write_text("{}", encoding="utf-8")
    manifest = tmp_path / "manifest.txt"
    manifest.write_bytes(f"{hash_file(source)}  evidence.json".encode("ascii"))

    assert validate_sha256_manifest(tmp_path, manifest) == [
        "manifest: missing final newline"
    ]


def test_validate_sha256_manifest_rejects_noncanonical_path_form(tmp_path):
    manifest = tmp_path / "manifest.txt"
    manifest.write_text(
        f"{'a' * 64}  evidence//result.json\n", encoding="ascii", newline="\n"
    )

    assert validate_sha256_manifest(tmp_path, manifest) == [
        "line 1: non-portable path"
    ]


def test_validate_sha256_manifest_diagnoses_malformed_separator(tmp_path):
    manifest = tmp_path / "manifest.txt"
    manifest.write_bytes(f"{'a' * 64} evidence.json\n".encode("ascii"))

    assert validate_sha256_manifest(tmp_path, manifest) == [
        "line 1: malformed checksum entry"
    ]
