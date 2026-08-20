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
    list_retained_staging,
    promote_staged_index,
    write_manifest_atomic,
)
from granite_turbovec import manifest as manifest_module


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
        python_version="3.12.10",
    )
    return replace(value, **changes)


def float_identity(**changes):
    value = replace(
        identity(),
        requested_backend="float32",
        actual_backend="float32",
        index_format="float32-npy-v1",
        bit_width=None,
        turbovec_version=None,
        turbovec_source_commit=None,
        turbovec_wheel_sha256=None,
        turbovec_license=None,
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
            ({"index_format": "turbovec-id-map-v2"}, "index-format-mismatch"),
            ({"bit_width": 2}, "index-bit-width-mismatch"),
            ({"turbovec_version": "1.0.1"}, "index-package-mismatch"),
            ({"actual_provider": "OtherProvider"}, "index-package-mismatch"),
            ({"python_version": "3.13.0"}, "index-python-mismatch"),
        )
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            write_manifest_atomic(path, manifest(), operation_id="matrix")
            for changes, expected in cases:
                with self.subTest(expected=expected), self.assertRaises(ResearchError) as context:
                    load_and_validate_manifest(path, identity(**changes))
                self.assertEqual(expected, context.exception.code)
            with self.assertRaises(ResearchError) as context:
                load_and_validate_manifest(path, float_identity())
            self.assertEqual("index-backend-mismatch", context.exception.code)

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
            replace(manifest(), artifacts=(ArtifactRecord("/index.tv", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("index:stream", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("index.", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("NUL.txt", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("index.tv", HASH_B, 4, 2, "TVEC", 1, 384),)),
            replace(manifest(), identity=replace(identity(), bit_width=None)),
            replace(manifest(), identity=replace(identity(), schema_version=2)),
            replace(manifest(), identity=replace(identity(), requested_backend="unknown")),
            replace(manifest(), identity=replace(identity(), index_format="float32-npy-v1")),
            replace(manifest(), identity=replace(identity(), python_version="3.12")),
            replace(manifest(), identity=replace(identity(), python_version="03.12.10")),
            replace(manifest(), identity=replace(identity(), requested_backend="float32")),
            replace(manifest(), identity=float_identity(requested_backend="turbovec")),
            replace(manifest(), artifacts=(
                ArtifactRecord("index.tv", HASH_B, 4, 1, "TVEC", 1, 384),
                ArtifactRecord("INDEX.TV", HASH_A, 4, 1, "TVEC", 1, 384),
            )),
            replace(manifest(), artifacts=(ArtifactRecord("manifest.json", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("manifest.json.tmp-op", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("index.staging-op", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("index.quarantine-op", HASH_B, 4, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("index.tv", HASH_B, 16 * 1024 * 1024 * 1024 + 1, 1, "TVEC", 1, 384),)),
            replace(manifest(), artifacts=(ArtifactRecord("index.tv", HASH_B, 4, 6_400_001, "TVEC", 1, 384),)),
        )
        for number, value in enumerate(invalid):
            with self.subTest(number=number), self.assertRaises(ResearchError):
                canonical_json(value)

    def test_both_supported_backend_pairs_are_valid(self):
        self.assertIn('"actual_backend":"turbovec"', canonical_json(manifest()))
        value = replace(
            manifest(),
            identity=float_identity(),
            artifacts=(ArtifactRecord("index.npy", HASH_B, 4, 1, "NUMPY", 1, 384),),
        )
        self.assertIn('"actual_backend":"float32"', canonical_json(value))

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
    def test_manifest_factory_runs_after_artifacts_exist_before_publication(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "index"
            base = promotable_manifest()

            def factory(staging):
                self.assertTrue((staging / base.artifacts[0].filename).is_file())
                return base

            result = promote_staged_index(
                target,
                None,
                write_index,
                validate_index,
                operation_id="factory",
                manifest_factory=factory,
            )

            self.assertEqual(target, result)
            self.assertTrue((target / "manifest.json").is_file())

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
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), writer, validate_index, operation_id="exists")
            self.assertEqual("index-destination-exists", context.exception.code)
            self.assertFalse(called)
            self.assertEqual("keep", (target / "marker").read_text(encoding="utf-8"))

    def test_reserved_quarantine_destination_is_rejected_before_writer(self):
        called = False
        with tempfile.TemporaryDirectory() as directory:
            def writer(_):
                nonlocal called
                called = True
            with self.assertRaises(ResearchError):
                promote_staged_index(
                    Path(directory) / "index.quarantine-user",
                    promotable_manifest(),
                    writer,
                    validate_index,
                    operation_id="reserved",
                )
            self.assertFalse(called)

    def test_failed_artifact_validation_quarantines_only_owned_staging(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            unrelated = root / "unrelated"
            unrelated.mkdir()
            target = root / "knowledge-index"
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), write_index, lambda *_: False, operation_id="bad")
            self.assertEqual("index-artifact-invalid", context.exception.code)
            self.assertFalse((root / "knowledge-index.slot-0.staging-bad").exists())
            quarantines = list(root.glob("knowledge-index.slot-*.staging-bad.quarantine-*"))
            self.assertEqual(1, len(quarantines))
            self.assertTrue((quarantines[0] / "index.tv").exists())
            self.assertTrue(unrelated.exists())
            self.assertFalse(target.exists())

    def test_binary_validator_requires_explicit_true(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"
            def raises(*_):
                raise OSError("private")
            for number, validator in enumerate((lambda *_: None, lambda *_: 1, raises)):
                with self.subTest(number=number), self.assertRaises(ResearchError) as context:
                    promote_staged_index(target, promotable_manifest(), write_index, validator, operation_id=f"validator-{number}")
                self.assertEqual("index-artifact-invalid", context.exception.code)
                self.assertFalse(target.exists())

    def test_declared_small_artifact_rejects_extra_byte_without_adapter_call(self):
        calls = 0
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"
            def oversized_writer(staging):
                (staging / "index.tv").write_bytes(b"TVEC" + b"x" * (1024 * 1024))
            def validator(*_):
                nonlocal calls
                calls += 1
                return True
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), oversized_writer, validator, operation_id="extra-byte")
            self.assertEqual("index-artifact-mismatch", context.exception.code)
            self.assertEqual(0, calls)

    def test_hash_reader_reads_only_declared_bytes_plus_one(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "artifact.bin"
            path.write_bytes(b"TVEC" + b"x" * (1024 * 1024))
            real_stream = path.open("rb")

            class ReadSpy:
                def __init__(self, stream):
                    self.stream = stream
                    self.requests = []

                def __enter__(self):
                    return self

                def __exit__(self, *args):
                    self.stream.close()

                def fileno(self):
                    return self.stream.fileno()

                def read(self, size):
                    self.requests.append(size)
                    return self.stream.read(size)

            spy = ReadSpy(real_stream)
            with mock.patch.object(Path, "open", return_value=spy):
                with self.assertRaises(ResearchError):
                    manifest_module._hash_file(path, 4)
            self.assertEqual([4, 1], spy.requests)

    def test_declared_too_large_rejects_before_writer_or_read(self):
        called = False
        too_large = replace(
            promotable_manifest(),
            artifacts=(ArtifactRecord("index.tv", HASH_A, 16 * 1024 * 1024 * 1024 + 1, 1, "TVEC", 1, 384),),
        )
        with tempfile.TemporaryDirectory() as directory:
            def writer(_):
                nonlocal called
                called = True
            with self.assertRaises(ResearchError):
                promote_staged_index(Path(directory) / "index", too_large, writer, validate_index, operation_id="too-large")
            self.assertFalse(called)

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
            self.assertEqual(["index-destination-exists"], [item for item in results if item != "ok"])
            self.assertEqual(b"TVEC", (target / "index.tv").read_bytes())

    def test_promotion_seam_never_publishes_swapped_unvalidated_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            moved = root / "validated-original"

            def swap(staging):
                staging.rename(moved)
                staging.mkdir()
                (staging / "index.tv").write_bytes(b"EVIL")

            with mock.patch("granite_turbovec.manifest._promotion_race_hook", side_effect=swap):
                failure = None
                try:
                    promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="promotion-seam")
                except ResearchError as error:
                    failure = error
            if target.exists():
                self.assertEqual(b"TVEC", (target / "index.tv").read_bytes())
            elif failure is None:
                self.fail("promotion returned success without publishing validated content")
            self.assertNotEqual(b"EVIL", (target / "index.tv").read_bytes() if target.exists() else b"")
            attacker = root / "knowledge-index.slot-0.staging-promotion-seam" / "index.tv"
            self.assertEqual(b"EVIL", attacker.read_bytes())

    def test_durability_failure_never_reports_success(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"
            with mock.patch("granite_turbovec.manifest._fsync_file", side_effect=ResearchError("index-durability-failed")):
                with self.assertRaises(ResearchError) as context:
                    promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="durability")
            self.assertEqual("index-durability-failed", context.exception.code)
            self.assertFalse(target.exists())

        calls = 0
        def fail_first_directory_flush(_):
            nonlocal calls
            calls += 1
            if calls == 1:
                raise ResearchError("index-durability-failed")
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"
            with mock.patch("granite_turbovec.manifest._flush_windows_directory", side_effect=fail_first_directory_flush):
                with self.assertRaises(ResearchError) as context:
                    promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="directory-durability")
            self.assertEqual("index-durability-failed", context.exception.code)
            self.assertFalse(target.exists())

    def test_retention_cap_blocks_repeated_failed_builds_and_lists_quarantines(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"
            for number in range(3):
                with self.assertRaises(ResearchError) as context:
                    promote_staged_index(target, promotable_manifest(), write_index, lambda *_: False, operation_id=f"retained-{number}")
                self.assertEqual("index-artifact-invalid", context.exception.code)
            retained = list_retained_staging(target)
            self.assertEqual(3, len(retained))
            target.mkdir()
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="existing-before-cap")
            self.assertEqual("index-destination-exists", context.exception.code)
            target.rmdir()
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="blocked")
            self.assertEqual("index-maintenance-required", context.exception.code)
            self.assertEqual(retained, list_retained_staging(target))

    def test_atomic_slots_bound_eight_concurrent_failed_writers(self):
        writer_barrier = threading.Barrier(3)
        writer_calls = 0
        results = []
        lock = threading.Lock()
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "knowledge-index"

            def writer(staging):
                nonlocal writer_calls
                write_index(staging)
                with lock:
                    writer_calls += 1
                writer_barrier.wait(timeout=5)

            def run(number):
                try:
                    promote_staged_index(target, promotable_manifest(), writer, lambda *_: False, operation_id=f"parallel-{number}")
                except ResearchError as error:
                    with lock:
                        results.append(error.code)

            threads = [threading.Thread(target=run, args=(number,)) for number in range(8)]
            for thread in threads:
                thread.start()
            for thread in threads:
                thread.join(timeout=10)
            self.assertEqual(3, writer_calls)
            self.assertEqual(3, results.count("index-artifact-invalid"))
            self.assertEqual(5, results.count("index-maintenance-required"))
            self.assertEqual(3, len(list_retained_staging(target)))

    def test_concurrent_success_and_failures_keep_three_fixed_slot_states(self):
        writer_barrier = threading.Barrier(3)
        results = []
        lock = threading.Lock()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"

            def run(number, succeeds):
                def writer(staging):
                    write_index(staging)
                    writer_barrier.wait(timeout=5)

                try:
                    promote_staged_index(
                        target,
                        promotable_manifest(),
                        writer,
                        (lambda *_: succeeds),
                        operation_id=f"mixed-{number}",
                    )
                    result = "success"
                except ResearchError as error:
                    result = error.code
                with lock:
                    results.append(result)

            threads = [
                threading.Thread(target=run, args=(0, True)),
                threading.Thread(target=run, args=(1, False)),
                threading.Thread(target=run, args=(2, False)),
            ]
            for thread in threads:
                thread.start()
            for thread in threads:
                thread.join(timeout=10)

            self.assertEqual(1, results.count("success"))
            self.assertEqual(2, results.count("index-artifact-invalid"))
            controls = [
                child
                for child in root.iterdir()
                if child.name in {
                    f"knowledge-index.slot-{slot}{suffix}"
                    for slot in range(3)
                    for suffix in ("", ".available")
                }
            ]
            self.assertEqual(3, len(controls))
            self.assertEqual(2, len(list_retained_staging(target)))

    def test_success_releases_exact_slot_for_subsequent_build(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="first")
            target.rename(root / "published-first")
            promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="second")
            self.assertEqual(b"TVEC", (target / "index.tv").read_bytes())

    def test_twenty_successes_reuse_fixed_slot_without_retained_failures(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            for number in range(20):
                promote_staged_index(
                    target,
                    promotable_manifest(),
                    write_index,
                    validate_index,
                    operation_id=f"success-{number}",
                )
                target.rename(root / f"published-{number}")
            controls = list(root.glob("knowledge-index.slot-*"))
            self.assertLessEqual(len(controls), 3)
            self.assertEqual(["knowledge-index.slot-0.available"], [item.name for item in controls])
            self.assertEqual((), list_retained_staging(target))

    def test_reused_available_slot_atomically_rewrites_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="owner-first")
            target.rename(root / "published-first")
            available = root / "knowledge-index.slot-0.available"
            self.assertEqual('{"operation_id":"owner-first","schema_version":1}', (available / "owner.json").read_text(encoding="utf-8"))
            with self.assertRaises(ResearchError):
                promote_staged_index(target, promotable_manifest(), write_index, lambda *_: False, operation_id="owner-second")
            active = root / "knowledge-index.slot-0"
            self.assertEqual('{"operation_id":"owner-second","schema_version":1}', (active / "owner.json").read_text(encoding="utf-8"))

    def test_available_acquire_swap_preserves_unowned_replacement(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="seed")
            target.rename(root / "published-seed")
            moved = root / "moved-available"

            def swap(available):
                available.rename(moved)
                available.mkdir()
                (available / "external-marker").write_text("keep", encoding="utf-8")

            with mock.patch("granite_turbovec.manifest._slot_acquire_race_hook", side_effect=swap):
                with self.assertRaises(ResearchError):
                    promote_staged_index(target, promotable_manifest(), write_index, lambda *_: False, operation_id="acquire-swap")
            replacement = root / "knowledge-index.slot-0.available"
            self.assertEqual("keep", (replacement / "external-marker").read_text(encoding="utf-8"))
            self.assertTrue((root / "knowledge-index.slot-0" / "owner.json").exists())

    def test_exclusive_claim_serializes_forced_reuse_contention(self):
        claim_held = threading.Event()
        release_claim = threading.Event()
        writer_calls = []
        results = []
        lock = threading.Lock()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="claim-seed")
            target.rename(root / "published-seed")
            for index in (1, 2):
                marker = root / f"knowledge-index.slot-{index}"
                marker.mkdir()
                (marker / "owner.json").write_text('{"operation_id":"occupied","schema_version":1}', encoding="utf-8")

            def seam(_, phase):
                if phase == "reserve" and not claim_held.is_set():
                    claim_held.set()
                    release_claim.wait(timeout=5)

            def writer(staging):
                with lock:
                    writer_calls.append((staging.name, staging.stat().st_ino))
                write_index(staging)

            def run(operation):
                try:
                    promote_staged_index(target, promotable_manifest(), writer, lambda *_: False, operation_id=operation)
                except ResearchError as error:
                    with lock:
                        results.append(error.code)

            with mock.patch("granite_turbovec.manifest._slot_transition_claim_hook", side_effect=seam):
                first = threading.Thread(target=run, args=("claim-first",))
                first.start()
                self.assertTrue(claim_held.wait(timeout=5))
                second = threading.Thread(target=run, args=("claim-second",))
                second.start()
                second.join(timeout=5)
                second_finished_while_claimed = not second.is_alive()
                release_claim.set()
                first.join(timeout=10)

            self.assertTrue(second_finished_while_claimed)
            self.assertFalse(first.is_alive())
            self.assertEqual(1, len(writer_calls))
            self.assertEqual(1, results.count("index-artifact-invalid"))
            self.assertEqual(1, results.count("index-maintenance-required"))
            slot_zero_quarantines = list(root.glob("knowledge-index.slot-0.staging-*.quarantine-*"))
            self.assertEqual(1, len(slot_zero_quarantines))
            self.assertLessEqual(len(list_retained_staging(target)), 3)

    def test_release_claim_excludes_overlapping_available_acquire(self):
        release_held = threading.Event()
        continue_release = threading.Event()
        second_results = []
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            for index in (1, 2):
                marker = root / f"knowledge-index.slot-{index}"
                marker.mkdir()
                (marker / "owner.json").write_text('{"operation_id":"occupied","schema_version":1}', encoding="utf-8")

            def seam(_, phase):
                if phase == "release":
                    target.rename(root / "published-overlap")
                    release_held.set()
                    continue_release.wait(timeout=5)

            def first_build():
                promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="release-first")

            with mock.patch("granite_turbovec.manifest._slot_transition_claim_hook", side_effect=seam):
                first = threading.Thread(target=first_build)
                first.start()
                self.assertTrue(release_held.wait(timeout=10))
                try:
                    promote_staged_index(target, promotable_manifest(), write_index, lambda *_: False, operation_id="release-overlap")
                except ResearchError as error:
                    second_results.append(error.code)
                continue_release.set()
                first.join(timeout=10)

            self.assertEqual(["index-maintenance-required"], second_results)
            self.assertTrue((root / "knowledge-index.slot-0.available").exists())

    def test_stale_transition_claim_is_fixed_bounded_and_visible(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            for index in range(3):
                (root / f"knowledge-index.slot-{index}.claim").write_text(
                    '{"operation_id":"crashed","schema_version":1}',
                    encoding="utf-8",
                )
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="claim-blocked")
            self.assertEqual("index-maintenance-required", context.exception.code)
            retained = list_retained_staging(target)
            self.assertEqual(3, len(retained))
            self.assertTrue(all(item.name.endswith(".claim") for item in retained))

    def test_stale_slots_block_predictably_and_listing_handles_legacy_over_cap(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            for slot in range(3):
                marker = root / f"knowledge-index.slot-{slot}"
                marker.mkdir()
                (marker / "owner.json").write_text('{"operation_id":"stale","schema_version":1}', encoding="utf-8")
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="blocked-stale")
            self.assertEqual("index-maintenance-required", context.exception.code)

            for number in range(5):
                (root / f"knowledge-index.staging-legacy-{number}").mkdir()
            retained = list_retained_staging(target)
            self.assertGreaterEqual(len(retained), 5)

    def test_slot_release_swap_never_deletes_or_reuses_unowned_marker(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            swapped = root / "swapped-owned-slot"

            def swap(slot):
                slot.rename(swapped)
                slot.mkdir()
                (slot / "external-marker").write_text("keep", encoding="utf-8")

            with mock.patch("granite_turbovec.manifest._slot_release_race_hook", side_effect=swap):
                try:
                    promote_staged_index(target, promotable_manifest(), write_index, validate_index, operation_id="slot-swap")
                except ResearchError:
                    pass
            replacement = root / "knowledge-index.slot-0"
            self.assertEqual("keep", (replacement / "external-marker").read_text(encoding="utf-8"))
            released = root / "knowledge-index.slot-0.available"
            self.assertTrue((released / "owner.json").exists())
            target.rename(root / "published-before-retry")
            used = []

            def retry_writer(staging):
                used.append(staging.name)
                write_index(staging)

            with self.assertRaises(ResearchError):
                promote_staged_index(
                    target,
                    promotable_manifest(),
                    retry_writer,
                    lambda *_: False,
                    operation_id="slot-retry",
                )
            self.assertEqual(1, len(used))
            self.assertIn(".slot-1.", used[0])

    def test_writer_sparse_oversize_is_bounded_and_retained_count_stays_finite(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"

            def sparse_writer(staging):
                with (staging / "undeclared.bin").open("wb") as stream:
                    stream.seek(1024)
                    stream.write(b"x")

            with mock.patch("granite_turbovec.manifest._MAX_STAGING_BYTES", 1024):
                for number in range(3):
                    with self.assertRaises(ResearchError) as context:
                        promote_staged_index(target, promotable_manifest(), sparse_writer, validate_index, operation_id=f"sparse-{number}")
                    self.assertEqual("index-staging-limit-exceeded", context.exception.code)
            self.assertEqual(3, len(list_retained_staging(target)))
            with self.assertRaises(ResearchError) as context:
                promote_staged_index(target, promotable_manifest(), sparse_writer, validate_index, operation_id="sparse-blocked")
            self.assertEqual("index-maintenance-required", context.exception.code)

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
            replacement = root / "knowledge-index.slot-0.staging-swap"
            moved = root / "moved-owned-staging"

            def writer(staging):
                staging.rename(moved)
                staging.mkdir()
                (staging / "unrelated-marker").write_text("keep", encoding="utf-8")

            with self.assertRaises(ResearchError):
                promote_staged_index(target, promotable_manifest(), writer, validate_index, operation_id="swap")
            self.assertEqual("keep", (replacement / "unrelated-marker").read_text(encoding="utf-8"))
            self.assertFalse(target.exists())

    def test_cleanup_swap_at_quarantine_seam_preserves_unowned_content(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "knowledge-index"
            staging = root / "knowledge-index.slot-0.staging-seam"
            original = root / "original-owned"

            def swap(active):
                active.rename(original)
                active.mkdir()
                (active / "external-marker").write_text("keep", encoding="utf-8")

            with mock.patch("granite_turbovec.manifest._quarantine_race_hook", side_effect=swap):
                with self.assertRaises(ResearchError) as context:
                    promote_staged_index(target, promotable_manifest(), write_index, lambda *_: False, operation_id="seam")
            self.assertEqual("index-quarantine-failed", context.exception.code)
            self.assertEqual("keep", (staging / "external-marker").read_text(encoding="utf-8"))
            self.assertTrue((original / "index.tv").exists())
            self.assertFalse(target.exists())


if __name__ == "__main__":
    unittest.main()
