import hashlib
import json
import os
import tempfile
import threading
import unittest
from dataclasses import replace
from pathlib import Path
from unittest import mock

from granite_turbovec.contracts import ResearchError
from granite_turbovec.manifest import (
    ArtifactRecord,
    ChunkRecord,
    IndexIdentity,
    IndexManifest,
    SourceRecord,
    canonical_json,
    canonical_sha256,
    load_and_validate_manifest,
    promote_staged_index,
    write_manifest_atomic,
)


HASH_A = "a" * 64
HASH_B = "b" * 64


def identity(**changes):
    value = IndexIdentity(
        schema_version=1,
        chunking_algorithm="bounded-text",
        chunking_version=1,
        chunk_max_chars=800,
        chunk_overlap=80,
        embedding_model="BAAI/bge-small-en-v1.5",
        embedding_model_manifest_sha256=HASH_A,
        embedding_model_license="MIT",
        dimension=384,
        requested_backend="turbovec",
        actual_backend="turbovec",
        index_format="turbovec-id-map-v1",
        bit_width=4,
        turbovec_version="1.0.0",
        turbovec_source_commit="ccab9f325e6ce2a270a87daf01ae4e443bcf2d49",
        turbovec_wheel_sha256=HASH_B,
        turbovec_license="MIT",
        dependency_lock_sha256=HASH_A,
        requested_provider="CPUExecutionProvider",
        actual_provider="CPUExecutionProvider",
    )
    return replace(value, **changes)


def manifest(**changes):
    value = IndexManifest(
        identity=identity(),
        sources=(SourceRecord("knowledge/granite.txt", HASH_A),),
        chunks=(ChunkRecord(101, "knowledge/granite.txt", 0, 14),),
        created_utc="2026-08-20T12:00:00Z",
        artifacts=(ArtifactRecord("index.tv", HASH_B, 4, 1, "TVEC", 1, 384),),
    )
    return replace(value, **changes)


def promotable_manifest():
    payload = b"TVEC"
    return manifest(
        artifacts=(ArtifactRecord("index.tv", hashlib.sha256(payload).hexdigest(), len(payload), 1, "TVEC", 1, 384),)
    )


def write_index(staging):
    (staging / "index.tv").write_bytes(b"TVEC")


def validate_index(path, artifact, loaded):
    return (
        path.read_bytes().startswith(artifact.magic.encode("ascii"))
        and artifact.version == 1
        and artifact.dimension == loaded.identity.dimension
        and artifact.count == len(loaded.chunks)
    )


class CanonicalManifestTests(unittest.TestCase):
    def test_atomic_roundtrip_is_canonical_and_hashable(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            expected = manifest()
            write_manifest_atomic(path, expected, operation_id="build-01")
            loaded = load_and_validate_manifest(path, expected.identity)
            self.assertEqual(expected, loaded)
            self.assertEqual(canonical_json(expected), path.read_text(encoding="utf-8"))
            self.assertEqual(64, len(canonical_sha256(expected)))
            self.assertFalse((path.parent / "manifest.json.tmp-build-01").exists())

    def test_embedding_identity_mismatch_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            write_manifest_atomic(path, manifest(), operation_id="build-01")
            with self.assertRaises(ResearchError) as context:
                load_and_validate_manifest(
                    path, identity(embedding_model="private/other-model")
                )
        self.assertEqual("index-embedding-mismatch", context.exception.code)

    def test_canonical_json_contains_hashes_but_no_content_or_absolute_path(self):
        value = manifest()
        payload = canonical_json(value)
        self.assertIn(HASH_A, payload)
        self.assertIn(HASH_B, payload)
        self.assertNotIn("secret sentence", payload)
        self.assertNotIn("C:\\Users", payload)

    def test_canonical_json_is_deterministic_compact_and_preserves_unicode(self):
        value = manifest(
            sources=(SourceRecord("knowledge/granité.txt", HASH_A),),
            chunks=(ChunkRecord(101, "knowledge/granité.txt", 0, 14),),
        )
        first = canonical_json(value)
        self.assertEqual(first, canonical_json(value))
        self.assertIn("granité.txt", first)
        self.assertNotIn("\\u00e9", first)
        self.assertNotIn(": ", first)
        self.assertEqual(canonical_sha256(value), canonical_sha256(value))

    def test_identity_mismatch_matrix_has_specific_fixed_codes(self):
        cases = (
            ({"embedding_model_manifest_sha256": HASH_B}, "index-embedding-mismatch"),
            ({"dimension": 768}, "index-dimension-mismatch"),
            ({"chunk_max_chars": 900}, "index-chunking-mismatch"),
            ({"requested_backend": "float32"}, "index-backend-mismatch"),
            ({"index_format": "turbovec-id-map-v2"}, "index-format-mismatch"),
            ({"bit_width": 2}, "index-bit-width-mismatch"),
            ({"turbovec_version": "1.0.1"}, "index-package-mismatch"),
            ({"actual_provider": "OtherProvider"}, "index-package-mismatch"),
        )
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            write_manifest_atomic(path, manifest(), operation_id="matrix")
            for changes, expected in cases:
                with self.subTest(expected=expected), self.assertRaises(ResearchError) as context:
                    load_and_validate_manifest(path, identity(**changes))
                self.assertEqual(expected, context.exception.code)

    def test_expected_manifest_also_binds_ordered_source_and_chunk_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            write_manifest_atomic(path, manifest(), operation_id="source")
            expected = manifest(sources=(SourceRecord("knowledge/granite.txt", HASH_B),))
            with self.assertRaises(ResearchError) as context:
                load_and_validate_manifest(path, expected)
        self.assertEqual("index-source-mismatch", context.exception.code)

    def test_strict_parser_rejects_duplicate_unknown_missing_bool_and_nonfinite_fields(self):
        base = canonical_json(manifest())
        corruptions = (
            base.replace('"schema_version":1', '"schema_version":1,"schema_version":1'),
            base.replace('"schema_version":1', '"schema_version":1,"unknown":1'),
            base.replace(',"schema_version":1', ""),
            base.replace('"dimension":384', '"dimension":true'),
            base.replace('"dimension":384', '"dimension":NaN'),
        )
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            for number, payload in enumerate(corruptions):
                with self.subTest(number=number):
                    path.write_text(payload, encoding="utf-8")
                    with self.assertRaises(ResearchError) as context:
                        load_and_validate_manifest(path, identity())
                    self.assertEqual("index-manifest-corrupt", context.exception.code)

    def test_bounded_invalid_utf8_and_oversized_inputs_are_private_corruption_errors(self):
        private = "secret sentence C:\\Users\\Arian"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            for payload in (b"\xff" + private.encode(), b"{" + b"x" * (4 * 1024 * 1024 + 1)):
                path.write_bytes(payload)
                with self.assertRaises(ResearchError) as context:
                    load_and_validate_manifest(path, identity())
                self.assertEqual("index-manifest-corrupt", str(context.exception))
                self.assertNotIn(private, str(context.exception))

    def test_validation_rejects_invalid_paths_hashes_ids_offsets_counts_and_combinations(self):
        invalid = (
            replace(manifest(), sources=(SourceRecord("C:/Users/private.txt", HASH_A),)),
            replace(manifest(), sources=(SourceRecord("../private.txt", HASH_A),)),
            replace(manifest(), sources=(SourceRecord("knowledge/x.txt", "A" * 64),), chunks=(ChunkRecord(1, "knowledge/x.txt", 0, 1),)),
            replace(manifest(), chunks=(ChunkRecord(True, "knowledge/granite.txt", 0, 14),)),
            replace(manifest(), chunks=(ChunkRecord(101, "knowledge/granite.txt", 14, 14),)),
            replace(manifest(), chunks=(ChunkRecord(101, "knowledge/granite.txt", 0, 14), ChunkRecord(101, "knowledge/granite.txt", 14, 15))),
            replace(manifest(), artifacts=(ArtifactRecord("../index.tv", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("NUL.txt", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("index.tv", HASH_B, 4, 2, "TVEC", 1, 384),)),
            replace(manifest(), identity=replace(identity(), bit_width=None)),
            replace(manifest(), identity=replace(identity(), schema_version=2)),
            replace(manifest(), identity=replace(identity(), requested_backend="unknown")),
            replace(manifest(), identity=replace(identity(), index_format="float32-npy-v1")),
        )
        for number, value in enumerate(invalid):
            with self.subTest(number=number), self.assertRaises(ResearchError):
                canonical_json(value)

    def test_atomic_write_replaces_regular_target_and_cleans_temp_on_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            path.write_text("old", encoding="utf-8")
            write_manifest_atomic(path, manifest(), operation_id="replace")
            self.assertEqual(canonical_json(manifest()), path.read_text(encoding="utf-8"))
            with mock.patch("granite_turbovec.manifest.os.replace", side_effect=OSError("private")):
                with self.assertRaises(ResearchError) as context:
                    write_manifest_atomic(path, manifest(), operation_id="failure")
            self.assertEqual("index-manifest-write-failed", context.exception.code)
            self.assertFalse((path.parent / "manifest.json.tmp-failure").exists())
            self.assertEqual(canonical_json(manifest()), path.read_text(encoding="utf-8"))

    def test_atomic_write_cleans_temp_when_durable_flush_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            with mock.patch("granite_turbovec.manifest.os.fsync", side_effect=OSError("private")):
                with self.assertRaises(ResearchError):
                    write_manifest_atomic(path, manifest(), operation_id="flush")
            self.assertFalse((path.parent / "manifest.json.tmp-flush").exists())
            self.assertFalse(path.exists())

    def test_noncanonical_json_and_manifest_behind_ancestor_link_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            real = root / "real"
            real.mkdir()
            path = real / "manifest.json"
            path.write_text(canonical_json(manifest()) + "\n", encoding="utf-8")
            with self.assertRaises(ResearchError):
                load_and_validate_manifest(path, identity())

            path.write_text(canonical_json(manifest()), encoding="utf-8")
            link = root / "link"
            try:
                link.symlink_to(real, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"directory symlinks unavailable: {error}")
            with self.assertRaises(ResearchError):
                load_and_validate_manifest(link / "manifest.json", identity())

    def test_operation_id_cannot_escape_manifest_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ResearchError) as context:
                write_manifest_atomic(Path(directory) / "manifest.json", manifest(), operation_id="../escape")
        self.assertEqual("index-operation-id-invalid", context.exception.code)
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ResearchError):
                write_manifest_atomic(Path(directory) / "manifest.json", manifest(), operation_id="ambiguous.")


class StagedPromotionTests(unittest.TestCase):
    def test_validated_staging_is_promoted_with_manifest_written_last(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"
            result = promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="success")
            self.assertEqual(target, result)
            self.assertEqual(b"TVEC", (target / "index.tv").read_bytes())
            self.assertEqual(promotable_manifest(), load_and_validate_manifest(target / "manifest.json", promotable_manifest()))

    def test_existing_destination_is_unchanged_and_writer_is_not_called(self):
        called = False
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"
            target.mkdir()
            (target / "marker").write_text("keep", encoding="utf-8")
            def writer(_):
                nonlocal called
                called = True
            with self.assertRaises(ResearchError):
                promote_staged_index(target, promotable_manifest(), writer, validate_index, operation_id="exists")
            self.assertFalse(called)
            self.assertEqual("keep", (target / "marker").read_text(encoding="utf-8"))

    def test_failed_artifact_validation_removes_only_owned_staging(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            unrelated = root / "unrelated"
            unrelated.mkdir()
            target = root / "knowledge-index"
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), write_index, lambda *_: False, operation_id="bad")
            self.assertEqual("index-artifact-invalid", context.exception.code)
            self.assertFalse((root / "knowledge-index.staging-bad").exists())
            self.assertTrue(unrelated.exists())
            self.assertFalse(target.exists())

    def test_artifact_hash_or_size_mismatch_is_rejected_before_adapter_validation(self):
        calls = 0
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"
            def bad_writer(staging):
                (staging / "index.tv").write_bytes(b"WRONG")
            def validator(*_):
                nonlocal calls
                calls += 1
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), bad_writer, validator, operation_id="hash")
            self.assertEqual("index-artifact-mismatch", context.exception.code)
            self.assertEqual(0, calls)
            self.assertFalse(target.exists())

    def test_undeclared_files_and_validator_mutation_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "extra-index"
            def extra_writer(staging):
                write_index(staging)
                (staging / "undeclared.bin").write_bytes(b"extra")
            with self.assertRaises(ResearchError):
                promote_staged_index(target, promotable_manifest(), extra_writer, validate_index, operation_id="extra")
            self.assertFalse(target.exists())

            target = root / "mutated-index"
            def mutating_validator(path, *_):
                path.write_bytes(b"EVIL")
                return True
            with self.assertRaises(ResearchError):
                promote_staged_index(target, promotable_manifest(), write_index, mutating_validator, operation_id="mutate")
            self.assertFalse(target.exists())

    def test_concurrent_promotion_has_one_winner_and_loser_cannot_overwrite(self):
        barrier = threading.Barrier(2)
        results = []
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"
            def writer(staging):
                write_index(staging)
                barrier.wait(timeout=5)
            def run(operation):
                try:
                    promote_staged_index(target, promotable_manifest(), writer, validate_index, operation_id=operation)
                    results.append("ok")
                except ResearchError as error:
                    results.append(error.code)
            threads = [threading.Thread(target=run, args=(f"race-{number}",)) for number in range(2)]
            for thread in threads:
                thread.start()
            for thread in threads:
                thread.join(timeout=10)
            self.assertEqual(1, results.count("ok"))
            self.assertEqual(1, len([item for item in results if item != "ok"]))
            self.assertEqual(b"TVEC", (target / "index.tv").read_bytes())

    def test_symlink_artifact_is_rejected_when_platform_allows_creation(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            outside = root / "outside.tv"
            outside.write_bytes(b"TVEC")
            target = root / "knowledge-index"
            def writer(staging):
                try:
                    (staging / "index.tv").symlink_to(outside)
                except OSError as error:
                    raise unittest.SkipTest(f"symlinks unavailable: {error}")
            with self.assertRaises(ResearchError):
                promote_staged_index(target, promotable_manifest(), writer, validate_index, operation_id="link")
            self.assertEqual(b"TVEC", outside.read_bytes())
            self.assertFalse(target.exists())

    def test_reparse_or_symlink_in_parent_chain_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            real = root / "real"
            (real / "sub").mkdir(parents=True)
            link = root / "link"
            try:
                link.symlink_to(real, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"directory symlinks unavailable: {error}")
            with self.assertRaises(ResearchError):
                promote_staged_index(link / "sub" / "index", promotable_manifest(), write_index, validate_index, operation_id="parent-link")
            self.assertFalse((real / "sub" / "index").exists())

    def test_parent_creation_does_not_write_through_existing_ancestor_link(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            real = root / "real"
            real.mkdir()
            link = root / "link"
            try:
                link.symlink_to(real, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"directory symlinks unavailable: {error}")
            with self.assertRaises(ResearchError):
                promote_staged_index(link / "new" / "index", promotable_manifest(), write_index, validate_index, operation_id="no-follow")
            self.assertFalse((real / "new").exists())

    def test_cleanup_never_deletes_directory_swapped_in_for_owned_staging(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            replacement = root / "knowledge-index.staging-swap"
            moved = root / "moved-owned-staging"

            def writer(staging):
                staging.rename(moved)
                staging.mkdir()
                (staging / "unrelated-marker").write_text("keep", encoding="utf-8")

            with self.assertRaises(ResearchError):
                promote_staged_index(target, promotable_manifest(), writer, validate_index, operation_id="swap")
            self.assertEqual("keep", (replacement / "unrelated-marker").read_text(encoding="utf-8"))
            self.assertFalse(target.exists())


if __name__ == "__main__":
    unittest.main()
