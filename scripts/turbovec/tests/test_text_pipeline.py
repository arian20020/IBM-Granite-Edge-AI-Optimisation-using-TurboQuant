import hashlib
import os
import subprocess
import tempfile
import unittest
from dataclasses import FrozenInstanceError
from pathlib import Path
from unittest import mock

import granite_turbovec.text_pipeline as text_pipeline
from granite_turbovec.contracts import Chunk, Document, ResearchError
from granite_turbovec.text_pipeline import (
    _derive_chunk_id,
    chunk_document,
    discover_documents,
    validate_extension,
)


FIXTURE_ROOT = Path(__file__).parent / "fixtures" / "knowledge"


class InitialTextPipelineContractTests(unittest.TestCase):
    def test_discovery_returns_only_supported_fixture_files_in_stable_order(self) -> None:
        documents = discover_documents(FIXTURE_ROOT, max_files=8, max_bytes=1_000_000)

        self.assertEqual(
            ["granite.txt", "retrieval.md"],
            [document.relative_path for document in documents],
        )

    def test_chunking_is_bounded_deterministic_and_uses_unsigned_64_bit_ids(self) -> None:
        document = Document("notes.md", "alpha beta gamma delta epsilon")

        first = chunk_document(document, max_chars=18, overlap_chars=6)
        second = chunk_document(document, max_chars=18, overlap_chars=6)

        self.assertGreater(len(first), 1)
        self.assertEqual(first, second)
        self.assertTrue(all(len(chunk.text) <= 18 for chunk in first))
        self.assertTrue(all(0 <= chunk.chunk_id <= (2**64 - 1) for chunk in first))

    def test_invalid_utf8_and_unsupported_direct_files_use_fixed_error_codes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            invalid_utf8 = root / "invalid.txt"
            unsupported = root / "data.json"
            invalid_utf8.write_bytes(b"\xff\xfe")
            unsupported.write_text("{}", encoding="utf-8")

            with self.assertRaises(ResearchError) as invalid_context:
                discover_documents(invalid_utf8)
            with self.assertRaises(ResearchError) as unsupported_context:
                discover_documents(unsupported)

        self.assertEqual("input-invalid-utf8", invalid_context.exception.code)
        self.assertEqual("input-unsupported-type", unsupported_context.exception.code)


class ContractTests(unittest.TestCase):
    def test_contracts_are_frozen_and_research_error_is_privacy_safe(self) -> None:
        document = Document("notes.md", "text")
        chunk = Chunk(1, "notes.md", 0, 4, "text")
        error = ResearchError("input-read-failed")

        with self.assertRaises(FrozenInstanceError):
            document.text = "changed"  # type: ignore[misc]
        with self.assertRaises(FrozenInstanceError):
            chunk.end = 3  # type: ignore[misc]
        self.assertEqual("input-read-failed", str(error))
        self.assertEqual("input-read-failed", error.code)


class DiscoveryTests(unittest.TestCase):
    def test_direct_supported_file_uses_its_filename_and_hashes_original_bytes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = Path(temporary_directory) / "BOM.MD"
            original = b"\xef\xbb\xbfhello\r\nworld"
            path.write_bytes(original)

            documents = discover_documents(path)

        self.assertEqual(1, len(documents))
        self.assertEqual("BOM.MD", documents[0].relative_path)
        self.assertEqual("hello\r\nworld", documents[0].text)
        self.assertEqual(hashlib.sha256(original).hexdigest(), documents[0].sha256)

    @unittest.skipUnless(os.name == "nt", "NTFS case-insensitive identity is Windows-specific")
    def test_direct_file_uses_filesystem_casing_for_stable_identity(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            actual = Path(temporary_directory) / "ActualCase.TXT"
            actual.write_text("same file", encoding="utf-8")

            canonical = discover_documents(actual)
            differently_cased = discover_documents(actual.with_name("actualcase.txt"))

        self.assertEqual("ActualCase.TXT", canonical[0].relative_path)
        self.assertEqual(canonical, differently_cased)
        self.assertEqual(
            chunk_document(canonical[0]),
            chunk_document(differently_cased[0]),
        )

    def test_nested_relative_paths_are_portable_and_case_insensitively_sorted(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            (root / "nested").mkdir()
            (root / "z.TXT").write_text("z", encoding="utf-8")
            (root / "nested" / "Alpha.md").write_text("a", encoding="utf-8")
            (root / "beta.txt").write_text("b", encoding="utf-8")

            documents = discover_documents(root)

        self.assertEqual(
            ["beta.txt", "nested/Alpha.md", "z.TXT"],
            [document.relative_path for document in documents],
        )

    def test_directory_without_supported_files_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            Path(temporary_directory, "data.json").write_text("{}", encoding="utf-8")

            with self.assertRaises(ResearchError) as context:
                discover_documents(temporary_directory)

        self.assertEqual("input-empty", context.exception.code)

    def test_missing_and_invalid_limit_inputs_use_fixed_codes(self) -> None:
        with self.assertRaises(ResearchError) as missing:
            discover_documents(Path("definitely-missing-turbovec-input"))
        self.assertEqual("input-not-found", missing.exception.code)

        for kwargs in (
            {"max_files": 0},
            {"max_files": True},
            {"max_bytes": 0},
            {"max_total_bytes": -1},
        ):
            with self.subTest(kwargs=kwargs):
                with self.assertRaises(ResearchError) as invalid:
                    discover_documents(FIXTURE_ROOT, **kwargs)
                self.assertEqual("input-invalid-parameters", invalid.exception.code)

    def test_empty_and_malformed_paths_are_invalid_parameters(self) -> None:
        for value in ("", "bad\0path"):
            with self.subTest(value=repr(value)):
                with self.assertRaises(ResearchError) as context:
                    discover_documents(value)
                self.assertEqual("input-invalid-parameters", context.exception.code)

    def test_caller_file_count_cap_and_hard_file_count_cap_are_enforced(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            for index in range(9):
                (root / f"{index:02}.txt").write_text("x", encoding="utf-8")
            with self.assertRaises(ResearchError) as caller_cap:
                discover_documents(root, max_files=8, max_bytes=1_000_000)

            for index in range(9, 65):
                (root / f"{index:02}.txt").write_text("x", encoding="utf-8")
            with self.assertRaises(ResearchError) as hard_cap:
                discover_documents(root, max_files=10_000)

        self.assertEqual("input-file-count-limit", caller_cap.exception.code)
        self.assertEqual("input-file-count-limit", hard_cap.exception.code)

    def test_per_file_and_aggregate_byte_caps_are_enforced(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            (root / "large.txt").write_bytes(b"12345")
            with self.assertRaises(ResearchError) as per_file:
                discover_documents(root, max_bytes=4)

            (root / "large.txt").write_bytes(b"1234")
            (root / "other.md").write_bytes(b"5678")
            with self.assertRaises(ResearchError) as aggregate:
                discover_documents(root, max_bytes=4, max_total_bytes=7)

        self.assertEqual("input-file-size-limit", per_file.exception.code)
        self.assertEqual("input-total-size-limit", aggregate.exception.code)

    def test_requested_limits_cannot_exceed_hard_caps(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = Path(temporary_directory) / "large.txt"
            with path.open("wb") as output:
                output.truncate((8 * 1024 * 1024) + 1)

            with self.assertRaises(ResearchError) as context:
                discover_documents(path, max_bytes=100 * 1024 * 1024)

        self.assertEqual("input-file-size-limit", context.exception.code)

    def test_requested_total_limit_cannot_exceed_hard_cap(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            for index in range(5):
                with (root / f"{index}.txt").open("wb") as output:
                    output.truncate(7 * 1024 * 1024)

            with self.assertRaises(ResearchError) as context:
                discover_documents(root, max_total_bytes=100 * 1024 * 1024)

        self.assertEqual("input-total-size-limit", context.exception.code)

    def test_case_insensitive_sort_has_case_sensitive_tiebreaker(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            upper = root / "upper.txt"
            lower = root / "lower.txt"
            upper.write_text("upper", encoding="utf-8")
            lower.write_text("lower", encoding="utf-8")
            candidates = [
                ("same.TXT", lower, lower.lstat()),
                ("Same.txt", upper, upper.lstat()),
            ]

            with mock.patch(
                "granite_turbovec.text_pipeline._discover_directory",
                return_value=(candidates, str(root)),
            ):
                documents = discover_documents(root)

        self.assertEqual(["Same.txt", "same.TXT"], [d.relative_path for d in documents])

    def test_read_failure_does_not_disclose_private_path(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            private_name = "private-customer-folder"
            path = Path(temporary_directory) / f"{private_name}.txt"
            path.write_text("secret", encoding="utf-8")

            if os.name == "nt":
                original_handle_open = text_pipeline._validated_windows_handle

                def deny_target(candidate: Path, *, read: bool, allowed_root: str | None):
                    if candidate == path and read:
                        raise PermissionError("private operating system detail")
                    return original_handle_open(candidate, read=read, allowed_root=allowed_root)

                patcher = mock.patch.object(text_pipeline, "_validated_windows_handle", deny_target)
            else:
                original_open = Path.open

                def deny_path(candidate: Path, *args: object, **kwargs: object):
                    if candidate == path:
                        raise PermissionError("private operating system detail")
                    return original_open(candidate, *args, **kwargs)

                patcher = mock.patch.object(Path, "open", deny_path)

            with patcher:
                with self.assertRaises(ResearchError) as context:
                    discover_documents(path)

        self.assertEqual("input-read-failed", context.exception.code)
        self.assertNotIn(private_name, str(context.exception))

    def test_file_mutation_between_discovery_and_open_fails_as_a_race(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = Path(temporary_directory) / "changing.txt"
            path.write_text("before", encoding="utf-8")
            mutated = False
            if os.name == "nt":
                original_handle_open = text_pipeline._validated_windows_handle

                def mutate_target(candidate: Path, *, read: bool, allowed_root: str | None):
                    nonlocal mutated
                    if candidate == path and read and not mutated:
                        mutated = True
                        candidate.write_bytes(b"after-growth")
                    return original_handle_open(candidate, read=read, allowed_root=allowed_root)

                patcher = mock.patch.object(text_pipeline, "_validated_windows_handle", mutate_target)
            else:
                original_open = Path.open

                def mutate_path(candidate: Path, *args: object, **kwargs: object):
                    nonlocal mutated
                    if candidate == path and not mutated:
                        mutated = True
                        with original_open(candidate, "wb") as output:
                            output.write(b"after-growth")
                    return original_open(candidate, *args, **kwargs)

                patcher = mock.patch.object(Path, "open", mutate_path)

            with patcher:
                with self.assertRaises(ResearchError) as context:
                    discover_documents(path)

        self.assertEqual("input-race-detected", context.exception.code)

    def test_supported_symlink_file_is_rejected_and_symlink_directory_is_not_traversed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            outside = root / "outside"
            scan = root / "scan"
            outside.mkdir()
            scan.mkdir()
            target = outside / "secret.txt"
            target.write_text("secret", encoding="utf-8")
            file_link = scan / "linked.txt"
            directory_link = scan / "linked-directory"
            try:
                file_link.symlink_to(target)
                directory_link.symlink_to(outside, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"symbolic links unavailable: {error.winerror or error.errno}")

            with self.assertRaises(ResearchError) as linked_file:
                discover_documents(scan)

            file_link.unlink()
            with self.assertRaises(ResearchError) as no_supported_files:
                discover_documents(scan)

        self.assertEqual("input-unsafe-link", linked_file.exception.code)
        self.assertEqual("input-empty", no_supported_files.exception.code)

    def test_direct_symlink_directory_is_rejected_as_unsafe(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            target = root / "target"
            target.mkdir()
            (target / "secret.txt").write_text("secret", encoding="utf-8")
            link = root / "linked-directory"
            try:
                link.symlink_to(target, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"symbolic links unavailable: {error.winerror or error.errno}")

            with self.assertRaises(ResearchError) as context:
                discover_documents(link)

        self.assertEqual("input-unsafe-link", context.exception.code)

    @unittest.skipUnless(os.name == "nt", "junction swap validation is Windows-specific")
    def test_directory_replaced_during_enumeration_cannot_return_outside_content(self) -> None:
        with tempfile.TemporaryDirectory() as root_directory, tempfile.TemporaryDirectory() as outside_directory:
            root = Path(root_directory)
            outside = Path(outside_directory)
            inner = root / "inner"
            parked = root / "parked"
            inner.mkdir()
            (inner / "inside.txt").write_text("inside", encoding="utf-8")
            (outside / "outside.txt").write_text("outside-secret", encoding="utf-8")
            original_scandir = os.scandir
            replaced = False

            def replace_then_enumerate(path: object):
                nonlocal replaced
                if not replaced and isinstance(path, (str, os.PathLike)) and Path(path) == inner:
                    replaced = True
                    inner.rename(parked)
                    if os.name == "nt":
                        result = subprocess.run(
                            ["cmd", "/c", "mklink", "/J", str(inner), str(outside)],
                            capture_output=True,
                            check=False,
                        )
                        if result.returncode != 0:
                            self.skipTest("junction creation unavailable")
                    else:
                        inner.symlink_to(outside, target_is_directory=True)
                return original_scandir(path)

            with mock.patch("granite_turbovec.text_pipeline.os.scandir", side_effect=replace_then_enumerate):
                with self.assertRaises(ResearchError) as context:
                    discover_documents(root)

        self.assertIn(context.exception.code, {"input-race-detected", "input-unsafe-link"})
        self.assertNotIn("outside", str(context.exception))

    def test_validate_extension_accepts_only_txt_and_md(self) -> None:
        for supported in ("note.txt", "NOTE.MD", "nested/path/file.TxT"):
            with self.subTest(supported=supported):
                validate_extension(supported)

        for unsupported in ("note", "note.json", "note.md.exe", ".md"):
            with self.subTest(unsupported=unsupported):
                with self.assertRaises(ResearchError) as context:
                    validate_extension(unsupported)
                self.assertEqual("input-unsupported-type", context.exception.code)


class ChunkingTests(unittest.TestCase):
    def test_prefers_paragraph_then_newline_then_whitespace_boundaries(self) -> None:
        cases = (
            ("a" * 20 + "\n\n" + "b" * 20, 22),
            ("a" * 20 + "\n" + "b" * 20, 21),
            ("a" * 20 + " " + "b" * 20, 21),
        )
        for text, expected_end in cases:
            with self.subTest(text=text):
                chunks = chunk_document(Document("note.md", text), max_chars=30, overlap_chars=5)
                self.assertEqual(expected_end, chunks[0].end)

    def test_hard_boundaries_progress_and_preserve_exact_coordinates(self) -> None:
        text = "x" * 55
        chunks = chunk_document(Document("note.txt", text), max_chars=20, overlap_chars=4)

        self.assertGreater(len(chunks), 1)
        for previous, current in zip(chunks, chunks[1:]):
            self.assertGreater(current.start, previous.start)
            self.assertLess(current.start, previous.end)
        for chunk in chunks:
            self.assertLessEqual(len(chunk.text), 20)
            self.assertEqual(text[chunk.start : chunk.end], chunk.text)

    def test_outer_whitespace_is_trimmed_with_source_coordinates_and_empty_input_has_no_chunks(self) -> None:
        self.assertEqual([], chunk_document(Document("empty.md", "")))
        self.assertEqual([], chunk_document(Document("blank.md", " \r\n\t ")))

        chunks = chunk_document(Document("note.md", "  alpha  "), max_chars=20, overlap_chars=2)
        self.assertEqual([(2, 7, "alpha")], [(c.start, c.end, c.text) for c in chunks])

    def test_crlf_unicode_and_repeated_text_preserve_coordinates_and_unique_ids(self) -> None:
        text = "écho\r\nécho\r\nécho\r\nécho"
        chunks = chunk_document(Document("unicode.md", text), max_chars=11, overlap_chars=4)

        self.assertGreater(len(chunks), 1)
        self.assertEqual(len(chunks), len({chunk.chunk_id for chunk in chunks}))
        for chunk in chunks:
            self.assertEqual(text[chunk.start : chunk.end], chunk.text)

    def test_chunk_id_matches_documented_sha256_derivation(self) -> None:
        relative_path = "folder/note.md"
        text = "hello"
        payload = f"{relative_path}\0{2}\0{7}\0{text}".encode("utf-8")
        expected = int.from_bytes(hashlib.sha256(payload).digest()[:8], "big", signed=False)

        self.assertEqual(expected, _derive_chunk_id(relative_path, 2, 7, text))

    def test_invalid_chunk_parameters_use_fixed_code(self) -> None:
        document = Document("note.md", "text")
        for max_chars, overlap_chars in ((0, 0), (10, -1), (10, 10), (10, 11), (1_000_001, 0)):
            with self.subTest(max_chars=max_chars, overlap_chars=overlap_chars):
                with self.assertRaises(ResearchError) as context:
                    chunk_document(document, max_chars=max_chars, overlap_chars=overlap_chars)
                self.assertEqual("chunk-invalid-parameters", context.exception.code)

    def test_chunk_id_collision_fails_closed(self) -> None:
        document = Document("note.md", "one two three four five six seven")
        with mock.patch("granite_turbovec.text_pipeline._derive_chunk_id", return_value=1):
            with self.assertRaises(ResearchError) as context:
                chunk_document(document, max_chars=12, overlap_chars=3)

        self.assertEqual("chunk-id-collision", context.exception.code)

    def test_chunk_count_limit_allows_boundary_and_rejects_one_more(self) -> None:
        with mock.patch("granite_turbovec.text_pipeline.MAX_CHUNKS_PER_DOCUMENT", 3):
            chunks = chunk_document(
                Document("bounded.txt", "x" * 42),
                max_chars=18,
                overlap_chars=6,
            )
            with self.assertRaises(ResearchError) as context:
                chunk_document(
                    Document("too-many.txt", "x" * 43),
                    max_chars=18,
                    overlap_chars=6,
                )

        self.assertEqual(3, len(chunks))
        self.assertEqual("chunk-limit-exceeded", context.exception.code)


if __name__ == "__main__":
    unittest.main()
