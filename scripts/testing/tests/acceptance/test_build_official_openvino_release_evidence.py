from __future__ import annotations

import hashlib
import json
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[4]


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _canonical_sha256(value: object) -> str:
    return hashlib.sha256(
        (
            json.dumps(
                value,
                ensure_ascii=False,
                allow_nan=False,
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
    ).hexdigest()


def _copy_builder_sources(destination: Path) -> Path:
    paths = [
        Path("experiments/manifests/official-openvino/retest-matrix.json"),
        Path(
            "experiments/granite_turboquant_intel/manifests/environments/"
            "ENV-20260730-INTEL-LAPTOP-01-WB04/machine-manifest.json"
        ),
        Path(
            "experiments/raw-results/openvino-turboquant/2026-07-30/"
            "expected-rejections/scalar-semantic-rejections.json"
        ),
        Path(
            "experiments/raw-results/openvino-turboquant/2026-07-30/"
            "expected-rejections/property-expected-rejections-current.json"
        ),
        Path(
            ".superpowers/sdd/2026-07-19-openvino-turboquant-recovery/"
            "wb04-static-sections-draft.md"
        ),
    ]
    globs = (
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "artifacts/*/artifact-manifest.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "campaign-specs-dbbb784/**/spec.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-512/measurement-summary.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-TQ-14/context-512/measurement-summary.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-14/context-2048/measurement-summary.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/**/attempt.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/**/spec.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/**/sequence-receipt.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime/OV-TQ-13/context-2048/campaign-identity.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/**/attempt.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/**/spec.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/**/sequence-receipt.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "runtime-frozen-85ed31e/OV-03/context-4096/campaign-identity.json",
        "experiments/raw-results/openvino-turboquant/2026-07-30/"
        "diagnostics/ov-06-gpu-calibration-attempt-*/run/attempt.json",
        "experiments/raw-results/openvino-turboquant/2026-07-31/"
        "quality-bound-input-attempt-*/OV-TQ-14/context-512/governed/"
        "guard-evidence.json",
        "experiments/raw-results/openvino-turboquant/2026-07-31/"
        "quality-bound-input-attempt-*/OV-TQ-14/context-512/governed/"
        "worker-spec.json",
    )
    for pattern in globs:
        paths.extend(path.relative_to(REPO_ROOT) for path in REPO_ROOT.glob(pattern))
    for relative_path in sorted(set(paths)):
        target = destination / relative_path
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(REPO_ROOT / relative_path, target)
    return (
        destination
        / ".superpowers/sdd/2026-07-19-openvino-turboquant-recovery/"
        "wb04-static-sections-draft.md"
    )


class StaticSectionParsingTests(unittest.TestCase):
    def test_openvino_raw_evidence_is_byte_preserved_and_whitespace_exempt(self):
        evidence = (
            "experiments/raw-results/openvino-turboquant/2026-07-30/runtime/"
            "OV-TQ-13/context-512/measurement-summary.json"
        )
        result = subprocess.run(
            ["git", "check-attr", "text", "whitespace", "--", evidence],
            cwd=REPO_ROOT,
            check=True,
            capture_output=True,
            text=True,
        )

        self.assertIn(f"{evidence}: text: unset", result.stdout)
        self.assertIn(
            f"{evidence}: whitespace: -blank-at-eol,-blank-at-eof",
            result.stdout,
        )

    def test_static_draft_has_four_concise_nonblank_tables(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            parse_static_sections,
        )

        draft_path = (
            REPO_ROOT
            / ".superpowers/sdd/2026-07-19-openvino-turboquant-recovery/"
            "wb04-static-sections-draft.md"
        )
        sections = parse_static_sections(
            draft_path.read_text(encoding="utf-8")
        )

        self.assertEqual(set(sections), {1, 2, 3, 4})
        for section in sections.values():
            lines = section.splitlines()
            table_count = sum(
                1
                for index, line in enumerate(lines[:-1])
                if line.startswith("|")
                and lines[index + 1].startswith("|")
                and all(character in "|-: " for character in lines[index + 1])
            )
            self.assertEqual(table_count, 1)
            self.assertNotRegex(section, r"(?m)^\|.*\|\s*N/A\s*\|")
            self.assertFalse(
                any(
                    not cell.strip()
                    for line in lines
                    if line.startswith("|")
                    for cell in line.strip("|").split("|")
                )
            )
        self.assertIn("OV-C02", sections[3])
        self.assertIn("OV-C03", sections[3])
        for test_id in (
            "OV-04",
            "OV-05",
            "OV-TQ-01",
            "OV-TQ-02",
            "OV-TQ-18",
            "OV-TQ-19",
            "OV-TQ-20",
        ):
            self.assertIn(test_id, sections[4])

    def test_static_sections_preserve_the_approved_build_diagnostic_and_control_allocation(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            parse_static_sections,
        )

        draft_path = (
            REPO_ROOT
            / ".superpowers/sdd/2026-07-19-openvino-turboquant-recovery/"
            "wb04-static-sections-draft.md"
        )
        sections = parse_static_sections(draft_path.read_text(encoding="utf-8"))

        section_2_ids = set(re.findall(r"\bOV-[A-Z0-9-]+\b", sections[2]))
        self.assertEqual(
            section_2_ids,
            {"OV-B01", "OV-B02", "OV-B03", "OV-B05", "OV-B06", "OV-B07"},
        )
        for phrase in (
            "505/505",
            "46 tests",
            "46/46",
            "CPU plugin",
            "CPU functional binary",
            "`query_state()`",
            "invalid-destination",
            "fail-closed",
        ):
            self.assertIn(phrase, sections[2])

        for test_id, artifact in (("OV-C02", "U8"), ("OV-C03", "U4")):
            row = next(
                line for line in sections[3].splitlines() if f"| {test_id} |" in line
            )
            for phrase in (
                artifact,
                "valid output",
                "7 input tokens",
                "4 generated tokens",
                "`fallback=false`",
                "cleanup 0",
                "f32/f32",
                "Diagnostic only",
                "not a formal benchmark",
            ):
                self.assertIn(phrase, row)

        section_4_rows = [
            line
            for line in sections[4].splitlines()[2:]
            if line.startswith("|")
        ]
        self.assertEqual(len(section_4_rows), 3)
        expected_rows = (
            (
                "Scalar-state controls",
                ("OV-04/4096", "OV-05/4096", "OV-TQ-01/4096", "OV-TQ-02/4096"),
            ),
            (
                "Property-boundary controls",
                tuple(f"OV-TQS-{number:02d}" for number in range(5, 13)),
            ),
            (
                "Device/codec controls",
                ("OV-TQ-18/1024", "OV-TQ-19/256", "OV-TQ-20/256", "OV-B11"),
            ),
        )
        for row, (label, identifiers) in zip(section_4_rows, expected_rows):
            self.assertIn(label, row)
            for identifier in identifiers:
                self.assertIn(identifier, row)
        self.assertIn("not benchmark or quality results", sections[4])

    def test_parse_static_sections_returns_only_complete_release_sections(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            parse_static_sections,
        )

        required = (1, 2, 3, 4)
        draft = "\n\n".join(
            f"# {number}. Section {number}\n\nbody {number}"
            for number in required
        )

        self.assertEqual(
            parse_static_sections(draft),
            {number: f"body {number}" for number in required},
        )

    def test_parse_static_sections_rejects_a_missing_or_duplicate_section(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            parse_static_sections,
        )

        required = (1, 2, 3, 4)
        missing = "\n\n".join(
            f"# {number}. Section {number}\n\nbody {number}"
            for number in required
            if number != 4
        )
        duplicate = (
            "\n\n".join(
                f"# {number}. Section {number}\n\nbody {number}"
                for number in required
            )
            + "\n\n# 4. Section 4 repeated\n\nsecond body"
        )

        with self.assertRaisesRegex(ValueError, "sections 1-4"):
            parse_static_sections(missing)
        with self.assertRaisesRegex(ValueError, "duplicate section 4"):
            parse_static_sections(duplicate)


class ReleaseEvidenceBuildTests(unittest.TestCase):
    def test_build_identity_static_and_full_evidence_contract_is_reproducible(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            build_release_evidence,
        )

        workbook = (
            REPO_ROOT
            / "docs/testing/workbooks/text-templates/"
            "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md"
        )
        register = REPO_ROOT / "docs/testing/Workbook-Revision-Register.csv"
        controlled_manifest = (
            REPO_ROOT / "docs/testing/workbooks/Controlled-Workbook-Manifest.csv"
        )
        docx = (
            REPO_ROOT
            / "docs/testing/workbooks/generated/"
            "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx"
        )
        protected_hashes = {
            workbook: _sha256(workbook),
            register: _sha256(register),
            controlled_manifest: _sha256(controlled_manifest),
            docx: _sha256(docx),
        }
        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            output_root = root / "release"
            release_input = root / "reconciliation-input.json"
            kwargs = {
                "repo_root": REPO_ROOT,
                "output_root": output_root,
                "release_input_path": release_input,
                "static_draft_path": (
                    REPO_ROOT
                    / ".superpowers/sdd/"
                    "2026-07-19-openvino-turboquant-recovery/"
                    "wb04-static-sections-draft.md"
                ),
            }

            first = build_release_evidence(**kwargs)
            first_bytes = {
                path.relative_to(root).as_posix(): path.read_bytes()
                for path in sorted(root.rglob("*.json"))
            }
            second = build_release_evidence(**kwargs)
            second_bytes = {
                path.relative_to(root).as_posix(): path.read_bytes()
                for path in sorted(root.rglob("*.json"))
            }

            self.assertEqual(first, second)
            self.assertEqual(first_bytes, second_bytes)
            release = json.loads(release_input.read_text(encoding="utf-8"))
            self.assertEqual(
                release["schema"], "official-openvino-wb04-release-input/v2"
            )
            self.assertEqual(release["workbook_version"], "1.8")
            self.assertEqual(release["revision_id"], "WR-036")
            path_fields: list[tuple[str, str]] = []

            def collect_path_fields(value: object) -> None:
                if isinstance(value, dict):
                    for field, nested in value.items():
                        if (
                            (field == "path" or field.endswith("_path"))
                            and isinstance(nested, str)
                        ):
                            path_fields.append((field, nested))
                        elif field == "inventory_roots" and isinstance(
                            nested, list
                        ):
                            path_fields.extend(
                                (field, path)
                                for path in nested
                                if isinstance(path, str)
                            )
                        collect_path_fields(nested)
                elif isinstance(value, list):
                    for nested in value:
                        collect_path_fields(nested)

            collect_path_fields(release)
            for generated_path in output_root.rglob("*.json"):
                collect_path_fields(
                    json.loads(generated_path.read_text(encoding="utf-8"))
                )
            self.assertTrue(path_fields)
            self.assertFalse(
                [
                    (field, path)
                    for field, path in path_fields
                    if Path(path).is_absolute()
                ],
                "release evidence must contain only repository-relative paths",
            )
            measured = {
                ("OV-TQ-13", 512),
                ("OV-TQ-14", 512),
                ("OV-TQ-14", 2048),
            }
            direct = {
                (row["test_id"], row["context_tokens"])
                for row in release["terminal_records"]
            }
            envelope = set()
            for decision in release["resource_envelope_decisions"]:
                anchor = decision["blocked_anchor"]
                envelope.add((anchor["test_id"], anchor["context_tokens"]))
                envelope.update(
                    (row["test_id"], row["context_tokens"])
                    for row in decision["classified_rows"]
                )
            expected = {
                (row["test_id"], row["context_tokens"])
                for row in release["expected_rejection_records"]
            }
            self.assertEqual(len(measured | direct | envelope | expected), 36)
            self.assertFalse(
                (measured & direct)
                or (measured & envelope)
                or (measured & expected)
                or (direct & envelope)
                or (direct & expected)
                or (envelope & expected)
            )
            self.assertEqual(len(direct), 10)
            self.assertEqual(
                direct,
                {
                    ("OV-01", 1024),
                    ("OV-02", 2048),
                    ("OV-03", 4096),
                    ("OV-06", 4096),
                    ("OV-07", 2048),
                    ("OV-08", 4096),
                    ("OV-09", 4096),
                    ("OV-10", 4096),
                    ("OV-TQ-16", 4096),
                    ("OV-TQ-17", 4096),
                },
            )
            self.assertEqual(len(envelope), 16)
            self.assertEqual(
                envelope,
                {
                    ("OV-TQ-13", 2048),
                    *((f"OV-TQ-{number:02d}", 4096) for number in range(3, 13)),
                    ("OV-TQ-13", 4096),
                    ("OV-TQ-13", 8192),
                    ("OV-TQ-14", 4096),
                    ("OV-TQ-14", 8192),
                    ("OV-TQ-15", 4096),
                },
            )
            self.assertEqual(
                expected,
                {
                    ("OV-04", 4096),
                    ("OV-05", 4096),
                    ("OV-TQ-01", 4096),
                    ("OV-TQ-02", 4096),
                    ("OV-TQ-18", 1024),
                    ("OV-TQ-19", 256),
                    ("OV-TQ-20", 256),
                },
            )
            self.assertEqual(release["quality_records"], [])
            self.assertEqual(len(release["quality_terminal_records"]), 28)
            self.assertEqual(
                set(release["static_section_bodies"]),
                {"1", "2", "3", "4"},
            )
            inventory = json.loads(
                (output_root / "artifact-spec-inventory.json").read_text(
                    encoding="utf-8"
                )
            )
            claimed_inventory_sha = inventory.pop("inventory_sha256")
            self.assertEqual(claimed_inventory_sha, _canonical_sha256(inventory))
            decision = release["resource_envelope_decisions"][0]
            claimed_decision_sha = decision["decision_sha256"]
            self.assertEqual(
                claimed_decision_sha,
                _canonical_sha256(
                    {
                        field: value
                        for field, value in decision.items()
                        if field != "decision_sha256"
                    }
                ),
            )
            self.assertEqual(first["runtime_row_count"], 36)
            self.assertEqual(first["quality_row_count"], 33)

        self.assertEqual(
            protected_hashes,
            {path: _sha256(path) for path in protected_hashes},
        )

    def test_build_rejects_a_changed_reviewed_source_before_replacing_outputs(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            build_release_evidence,
        )

        # Keep the copied repository root short enough for the deepest
        # immutable Windows provenance paths.
        with tempfile.TemporaryDirectory() as directory:
            fixture = Path(directory)
            draft = _copy_builder_sources(fixture)
            release_input = fixture / "reconciliation-input.json"
            release_input.write_bytes(b"previous controlled release\n")
            matrix = (
                fixture
                / "experiments/manifests/official-openvino/retest-matrix.json"
            )
            matrix.write_bytes(matrix.read_bytes() + b" ")

            with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
                build_release_evidence(
                    repo_root=fixture,
                    output_root=fixture / "release",
                    release_input_path=release_input,
                    static_draft_path=draft,
                )

            self.assertEqual(
                release_input.read_bytes(), b"previous controlled release\n"
            )
            self.assertFalse((fixture / "release").exists())

    def test_build_rejects_quality_spec_bytes_not_bound_by_the_guard(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            _validate_quality_guards,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            fixture = Path(directory)
            for attempt in ("attempt-001", "attempt-002"):
                relative_root = Path(
                    "experiments/raw-results/openvino-turboquant/2026-07-31"
                ) / f"quality-bound-input-{attempt}/OV-TQ-14/context-512/governed"
                target_root = fixture / relative_root
                target_root.mkdir(parents=True, exist_ok=True)
                source_root = REPO_ROOT / relative_root
                for name in ("guard-evidence.json", "worker-spec.json"):
                    shutil.copy2(source_root / name, target_root / name)
            worker_spec = (
                fixture
                / "experiments/raw-results/openvino-turboquant/2026-07-31/"
                "quality-bound-input-attempt-001/OV-TQ-14/context-512/"
                "governed/worker-spec.json"
            )
            worker_spec.write_bytes(worker_spec.read_bytes() + b" ")

            with self.assertRaisesRegex(
                ValueError, "source evidence SHA-256 mismatch"
            ):
                _validate_quality_guards(fixture)

    def test_every_generated_source_reference_matches_the_source_bytes(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            build_release_evidence,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            release_input = root / "reconciliation-input.json"
            build_release_evidence(
                repo_root=REPO_ROOT,
                output_root=root / "release",
                release_input_path=release_input,
                static_draft_path=(
                    REPO_ROOT
                    / ".superpowers/sdd/"
                    "2026-07-19-openvino-turboquant-recovery/"
                    "wb04-static-sections-draft.md"
                ),
            )

            for path in sorted(root.rglob("*.json")):
                payload = json.loads(path.read_text(encoding="utf-8"))
                references = []
                if isinstance(payload.get("source_evidence"), list):
                    references.extend(payload["source_evidence"])
                if path.name == "artifact-spec-inventory.json":
                    references.extend(payload["artifact_entries"])
                    references.extend(payload["spec_entries"])
                if "source_aggregate_path" in payload:
                    references.append(
                        {
                            "path": payload["source_aggregate_path"],
                            "sha256": payload["source_aggregate_sha256"],
                        }
                    )
                for reference in references:
                    source = REPO_ROOT / reference["path"]
                    self.assertTrue(source.is_file(), reference)
                    self.assertEqual(_sha256(source), reference["sha256"])

    def test_generated_release_passes_strict_finalizer_check_only(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            build_release_evidence,
        )
        from scripts.testing.tools.finalize_official_openvino_workbook import (
            finalize_release,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            release_input = root / "reconciliation-input.json"
            destination = root / "must-not-be-written.md"
            build_release_evidence(
                repo_root=REPO_ROOT,
                output_root=root / "release",
                release_input_path=release_input,
                static_draft_path=(
                    REPO_ROOT
                    / ".superpowers/sdd/"
                    "2026-07-19-openvino-turboquant-recovery/"
                    "wb04-static-sections-draft.md"
                ),
            )

            report = finalize_release(
                release_input_path=release_input,
                destination=destination,
                check_only=True,
            )

            self.assertEqual(report["runtime_row_count"], 36)
            self.assertEqual(report["measured_row_count"], 3)
            self.assertEqual(report["terminal_row_count"], 26)
            self.assertEqual(report["expected_rejection_row_count"], 7)
            self.assertEqual(report["quality_row_count"], 33)
            self.assertEqual(report["presentation_controlled_id_count"], 60)
            self.assertEqual(report["presentation_metric_table_count"], 3)
            self.assertEqual(report["presentation_measured_row_count"], 3)
            self.assertFalse(destination.exists())

    def test_cli_builds_the_same_release_contract(self):
        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            completed = subprocess.run(
                [
                    sys.executable,
                    "scripts/testing/tools/build_official_openvino_release_evidence.py",
                    "--output-root",
                    str(root / "release"),
                    "--release-input",
                    str(root / "reconciliation-input.json"),
                ],
                cwd=REPO_ROOT,
                text=True,
                capture_output=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            report = json.loads(completed.stdout)
            self.assertEqual(report["runtime_row_count"], 36)
            self.assertEqual(report["quality_row_count"], 33)
            self.assertTrue((root / "reconciliation-input.json").is_file())

    def test_cli_default_release_evidence_path_is_not_git_ignored(self):
        from scripts.testing.tools.build_official_openvino_release_evidence import (
            _parse_args,
        )

        default_output = _parse_args([]).output_root / "probe.json"
        ignored = subprocess.run(
            [
                "git",
                "check-ignore",
                "--quiet",
                "--no-index",
                str(default_output),
            ],
            cwd=REPO_ROOT,
            check=False,
        )

        self.assertEqual(
            ignored.returncode,
            1,
            f"default evidence path is ignored: {default_output}",
        )


if __name__ == "__main__":
    unittest.main()
