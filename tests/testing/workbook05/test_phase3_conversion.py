from __future__ import annotations

import json
import unittest
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_DIRECT_REQUIREMENTS,
    REVIEWED_OPTIMUM_INTEL_CONSTRAINTS,
    ConversionFile,
    ConversionPackage,
    ConversionRequest,
    build_optimum_argument_list,
    collect_conversion_record,
    validate_dependency_candidate,
    validate_optimum_intel_constraints,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
REQUIREMENTS_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "requirements.phase3-assets.in"
)
SCHEMA_PATH = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "schemas"
    / "workbook05"
    / "model-conversion-record.schema.json"
)


def _request(**overrides: object) -> ConversionRequest:
    values: dict[str, object] = {
        "optimum_cli": Path(r"C:\w5c\tools\Scripts\optimum-cli.exe"),
        "source_directory": Path(r"C:\w5m\sources\granite41-3b-bef400f9"),
        "output_directory": Path(
            r"C:\w5m\converted\granite41-3b-int4a-g128-r100-bef400f9"
        ),
        "weight_format": "int4",
        "symmetric": False,
        "group_size": 128,
        "ratio": 1.0,
        "task": "text-generation-with-past",
        "data_free": True,
        "trust_remote_code": False,
    }
    values.update(overrides)
    return ConversionRequest(**values)


def _packages() -> tuple[ConversionPackage, ...]:
    return (
        ConversionPackage(
            name="optimum-intel",
            version="2.3.0.dev0",
            source_identity=OPTIMUM_INTEL_COMMIT,
        ),
        ConversionPackage(
            name="optimum",
            version="2.3.0",
            source_identity=OPTIMUM_COMMIT,
        ),
        ConversionPackage(
            name="transformers",
            version="5.5.0",
            source_identity="sha256:" + "1" * 64,
        ),
        ConversionPackage(
            name="huggingface-hub",
            version="1.21.0",
            source_identity="sha256:" + "2" * 64,
        ),
        ConversionPackage(
            name="nncf",
            version="3.2.0",
            source_identity="sha256:" + "3" * 64,
        ),
        ConversionPackage(
            name="openvino",
            version="2026.2.1",
            source_identity="sha256:" + "4" * 64,
        ),
        ConversionPackage(
            name="openvino-tokenizers",
            version="2026.2.1.0",
            source_identity="sha256:" + "5" * 64,
        ),
    )


def _files() -> tuple[ConversionFile, ...]:
    return (
        ConversionFile(
            relative_path="config.json",
            size_bytes=11,
            sha256="1" * 64,
        ),
        ConversionFile(
            relative_path="openvino_model.bin",
            size_bytes=12,
            sha256="2" * 64,
        ),
        ConversionFile(
            relative_path="openvino_model.xml",
            size_bytes=13,
            sha256="3" * 64,
        ),
        ConversionFile(
            relative_path="tokenizer.json",
            size_bytes=14,
            sha256="4" * 64,
        ),
        ConversionFile(
            relative_path="tokenizer_config.json",
            size_bytes=15,
            sha256="5" * 64,
        ),
    )


def _record(**overrides: object) -> dict[str, object]:
    values: dict[str, object] = {
        "request": _request(),
        "conversion_id": "CONV-WB05-GRANITE41-3B-INT4A-G128-R100-ATTEMPT-1",
        "asset_id": "MODEL-WB05-GRANITE41-3B-INT4A-G128-R100",
        "source_repository": "ibm-granite/granite-4.1-3b",
        "resolved_revision": "a" * 40,
        "aggregate_model_sha256": "b" * 64,
        "aggregate_tokenizer_sha256": "c" * 64,
        "dependency_preflight_status": "Passed",
        "dependency_preflight_record_path": (
            "records/conversion-dependency-preflight.json"
        ),
        "dependency_preflight_record_sha256": "d" * 64,
        "python_version": "3.12.10",
        "executable_sha256": "e" * 64,
        "packages": _packages(),
        "started_at_utc": "2026-08-14T00:00:00Z",
        "completed_at_utc": "2026-08-14T00:01:00Z",
        "exit_code": 0,
        "stdout_path": "logs/conversion.stdout.txt",
        "stderr_path": "logs/conversion.stderr.txt",
        "output_files": _files(),
        "aggregate_output_sha256": "f" * 64,
        "required_output_paths": (
            "config.json",
            "openvino_model.bin",
            "openvino_model.xml",
            "tokenizer.json",
            "tokenizer_config.json",
        ),
        "unrecorded_output_paths": (),
    }
    values.update(overrides)
    return collect_conversion_record(**values)


class Phase3ConversionTests(unittest.TestCase):
    """Prove the reviewed conversion command and provenance stay fail closed."""

    def test_direct_dependency_input_matches_reviewed_candidate_exactly(self) -> None:
        lines = tuple(
            line.strip()
            for line in REQUIREMENTS_PATH.read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.lstrip().startswith("#")
        )
        self.assertEqual(REVIEWED_DIRECT_REQUIREMENTS, lines)
        validate_dependency_candidate(lines)

    def test_pinned_optimum_intel_constraints_match_reviewed_source(self) -> None:
        validate_optimum_intel_constraints(REVIEWED_OPTIMUM_INTEL_CONSTRAINTS)
        drifted = tuple(
            "transformers>=4.51,<6.0"
            if value == "transformers>=4.51,<5.6"
            else value
            for value in REVIEWED_OPTIMUM_INTEL_CONSTRAINTS
        )
        with self.assertRaisesRegex(ValueError, "declared constraints"):
            validate_optimum_intel_constraints(drifted)

    def test_superseded_dependency_combination_is_rejected(self) -> None:
        superseded = (
            "huggingface-hub==1.24.0",
            "optimum-intel==2.0.0",
            "nncf==3.2.0",
            "transformers==5.14.1",
        )
        with self.assertRaisesRegex(ValueError, "reviewed direct set"):
            validate_dependency_candidate(superseded)

    def test_int4_asymmetric_command_is_an_argument_array(self) -> None:
        request = _request()
        arguments = build_optimum_argument_list(request)

        self.assertEqual("export", arguments[0])
        self.assertEqual("openvino", arguments[1])
        self.assertEqual(str(request.output_directory), arguments[-1])
        self.assertIn("--weight-format", arguments)
        self.assertIn("int4", arguments)
        self.assertIn("--group-size", arguments)
        self.assertIn("128", arguments)
        self.assertIn("--ratio", arguments)
        self.assertIn("1.0", arguments)
        self.assertNotIn("--sym", arguments)
        self.assertNotIn("--trust-remote-code", arguments)
        self.assertNotIn("--dataset", arguments)
        self.assertFalse(any(" ".join(arguments) == value for value in arguments))

    def test_command_builder_rejects_drift_from_approved_candidate(self) -> None:
        drifts = (
            {"symmetric": True},
            {"weight_format": "int8"},
            {"group_size": 64},
            {"ratio": 0.8},
            {"task": "text-generation"},
            {"data_free": False},
            {"trust_remote_code": True},
        )
        for drift in drifts:
            with self.subTest(drift=drift):
                with self.assertRaises(ValueError):
                    build_optimum_argument_list(_request(**drift))

    def test_source_and_output_paths_must_be_separate_controlled_windows_paths(
        self,
    ) -> None:
        invalid_requests = (
            _request(source_directory=Path("relative/source")),
            _request(output_directory=Path(r"D:\outside\converted")),
            _request(
                output_directory=Path(r"C:\w5m\sources\granite41-3b-bef400f9")
            ),
            _request(
                output_directory=Path(
                    r"C:\w5m\sources\granite41-3b-bef400f9\converted"
                )
            ),
            _request(optimum_cli=Path(r"C:\other\optimum-cli.exe")),
        )
        for request in invalid_requests:
            with self.subTest(request=request):
                with self.assertRaises(ValueError):
                    build_optimum_argument_list(request)

    def test_candidate_record_passes_the_closed_schema_and_keeps_claims_false(
        self,
    ) -> None:
        record = _record()
        schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
        errors = list(
            Draft202012Validator(
                schema,
                format_checker=FormatChecker(),
            ).iter_errors(record)
        )

        self.assertEqual([], [error.message for error in errors])
        self.assertEqual("Candidate", record["status"])
        self.assertFalse(record["conversion"]["trust_remote_code"])
        for claim in (
            "granite_model_test_authorised",
            "activation_claim_authorised",
            "packed_storage_claim_authorised",
            "performance_claim_authorised",
            "quality_claim_authorised",
        ):
            self.assertFalse(record[claim])

    def test_nonzero_exit_is_failed_and_never_candidate(self) -> None:
        record = _record(exit_code=7, output_files=())
        self.assertEqual("Failed", record["status"])
        self.assertIn("exit code 7", " ".join(record["reasons"]))

    def test_zero_exit_with_missing_required_output_is_failed(self) -> None:
        record = _record(
            output_files=tuple(
                item
                for item in _files()
                if item.relative_path != "openvino_model.bin"
            )
        )
        self.assertEqual("Failed", record["status"])
        self.assertIn("missing required output", " ".join(record["reasons"]))

    def test_unrecorded_output_is_integrity_failure(self) -> None:
        record = _record(
            unrecorded_output_paths=("unexpected.bin",),
        )
        self.assertEqual("IntegrityFailure", record["status"])
        self.assertIn("unrecorded output", " ".join(record["reasons"]))

    def test_duplicate_package_or_output_identity_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "duplicate package"):
            _record(packages=_packages() + (_packages()[0],))
        with self.assertRaisesRegex(ValueError, "duplicate output"):
            _record(output_files=_files() + (_files()[0],))

    def test_conversion_record_requires_passed_dependency_preflight(self) -> None:
        with self.assertRaisesRegex(ValueError, "dependency preflight"):
            _record(dependency_preflight_status="Blocked")

    def test_normal_package_source_identity_must_be_a_wheel_digest(self) -> None:
        packages = list(_packages())
        packages[2] = ConversionPackage(
            name="transformers",
            version="5.5.0",
            source_identity="pypi:transformers==5.5.0",
        )
        with self.assertRaisesRegex(
            ValueError,
            "wheel or source-distribution digest",
        ):
            _record(packages=tuple(packages))

    def test_dependency_preflight_path_must_be_portable(self) -> None:
        for value in (
            "../outside.json",
            r"records\windows.json",
            "/absolute.json",
            "C:/absolute.json",
        ):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    _record(dependency_preflight_record_path=value)


if __name__ == "__main__":
    unittest.main()
