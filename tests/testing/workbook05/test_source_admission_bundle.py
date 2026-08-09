from __future__ import annotations

import ast
import hashlib
import json
import shutil
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.hash_manifest import write_hash_manifest
from scripts.testing.workbook05.measurement_controls import capture_measurement_controls
from scripts.testing.workbook05.source_admission_bundle_validation import (
    main,
    validate_source_admission_bundle,
)


ROOT = Path(__file__).resolve().parents[3]
VALIDATOR_PATH = (
    ROOT
    / "scripts/testing/workbook05/source_admission_bundle_validation.py"
)
STEP_ORDER = (
    "preflight-validation",
    "workspace-validation",
    "measurement-control-capture",
    "route-a-runtime-verification",
    "route-a-genai-verification",
    "route-b-verification",
    "document-capture",
    "capability-inspection",
    "route-b-cmake-audit",
    "route-a-configure-probe",
    "route-decisions",
    "hashes",
)
ROUTE_A_ID = "route-a-merged-openvino"
ROUTE_B_ID = "route-b-experimental-qjl-polar"


def _write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _load_json(path: Path) -> dict[str, object]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise AssertionError(f"Expected an object in {path}")
    return value


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class SourceAdmissionBundleValidationTests(unittest.TestCase):
    def _copy_repository_contracts(self, destination: Path) -> Path:
        repository = destination / "repository"
        shutil.copytree(
            ROOT / "experiments/granite_turboquant_intel",
            repository / "experiments/granite_turboquant_intel",
        )
        shutil.copytree(ROOT / "docs", repository / "docs")
        return repository

    def _source_report(
        self,
        *,
        route_id: str,
        source_role: str,
        repository_full_name: str,
        origin_url: str,
        commit: str,
        command_path: str,
    ) -> dict[str, object]:
        return {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "phase_id": "phase-1-source-admission",
            "route_id": route_id,
            "source_role": source_role,
            "repository_full_name": repository_full_name,
            "expected_origin_url": origin_url,
            "actual_origin_url": origin_url,
            "expected_commit": commit,
            "actual_commit": commit,
            "working_tree_clean": True,
            "submodules_complete": True,
            "submodules": [
                {
                    "path": "thirdparty/example",
                    "url": "https://github.com/example/example.git",
                    "commit": "1" * 40,
                    "status": "clean",
                }
            ],
            "command_records": [command_path],
            "decision": "Passed",
            "decision_reason": (
                "The exact origin, commit, clean tree, and recursive "
                "submodules were verified."
            ),
        }

    def _capability_report(
        self,
        *,
        route_id: str,
        capability_id: str,
        classification: str,
        decision: str,
        excerpt_path: str,
    ) -> dict[str, object]:
        return {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "phase_id": "phase-1-source-admission",
            "route_id": route_id,
            "findings": [
                {
                    "capability_id": capability_id,
                    "classification": classification,
                    "source_path": "src/example.cpp",
                    "matched_tokens": ["TURBO"],
                    "missing_tokens": [],
                    "contradictory_tokens": (
                        ["not yet supported"]
                        if classification == "Contradictory"
                        else []
                    ),
                    "source_sha256": "2" * 64,
                    "excerpt_path": excerpt_path,
                }
            ],
            "decision": decision,
            "decision_reason": (
                "The required merged-route controls are present in source."
                if decision == "Passed"
                else "The experimental route remains contradictory."
            ),
        }

    def _mark_route_a_admitted(self, record: dict[str, object]) -> None:
        evidence = {
            "RA-P01-document-capture": (
                "commands/"
                "route-a-merged-openvino-documented-commands.json"
            ),
            "RA-P02-runtime-source-tree": (
                "routes/route-a/source-tree-runtime.json"
            ),
            "RA-P03-genai-source-tree": (
                "routes/route-a/source-tree-genai.json"
            ),
            "RA-P04-runtime-submodules": (
                "routes/route-a/source-tree-runtime.json"
            ),
            "RA-P05-genai-submodules": (
                "routes/route-a/source-tree-genai.json"
            ),
            "RA-P06-source-capabilities": (
                "routes/route-a/source-capabilities.json"
            ),
            "RA-P07-toolchain": "preflight/preflight-report.json",
            "RA-P08-configure-probe": (
                "routes/route-a/configure-probe.json"
            ),
            "RA-P09-generated-metadata": (
                "routes/route-a/configure/CMakeCache.txt"
            ),
        }
        record["admission_status"] = "Admitted"
        record["decision_reason"] = (
            "The exact Route A source, toolchain, and configure evidence passed."
        )
        for proof in record["proofs"]:  # type: ignore[index]
            proof["status"] = "Passed"
            proof["evidence_path"] = evidence[proof["proof_id"]]

    def _mark_route_b_blocked(self, record: dict[str, object]) -> None:
        evidence = {
            "RB-P01-document-capture": (
                "commands/"
                "route-b-experimental-qjl-polar-documented-commands.json"
            ),
            "RB-P02-runtime-source-tree": (
                "routes/route-b/source-tree-runtime.json"
            ),
            "RB-P03-runtime-submodules": (
                "routes/route-b/source-tree-runtime.json"
            ),
            "RB-P04-qjl-source-paths": (
                "routes/route-b/source-capabilities.json"
            ),
            "RB-P05-polar-source-paths": (
                "routes/route-b/source-capabilities.json"
            ),
            "RB-P06-encode-decode-source-paths": (
                "routes/route-b/source-capabilities.json"
            ),
            "RB-P07-independent-kv-source-paths": (
                "routes/route-b/source-capabilities.json"
            ),
        }
        record["admission_status"] = "Blocked"
        record["decision_reason"] = (
            "RB-SRC-001 is confirmed, so Route B remains blocked."
        )
        for proof in record["proofs"]:  # type: ignore[index]
            proof_id = proof["proof_id"]
            if proof_id in evidence:
                proof["status"] = "Passed"
                proof["evidence_path"] = evidence[proof_id]
            else:
                proof["status"] = "Blocked"
                proof["evidence_path"] = (
                    "routes/route-b/cmake-test-discovery.json"
                )

    def _create_valid_bundle(
        self,
        root: Path,
        repository: Path,
    ) -> Path:
        bundle = root / "bundle"
        bundle.mkdir(parents=True)

        preflight_template = (
            repository
            / "experiments/granite_turboquant_intel/manifests/"
            "templates/workbook05/preflight-report-template.json"
        )
        preflight = _load_json(preflight_template)
        preflight["overall_status"] = "Passed"
        _write_json(bundle / "preflight/preflight-report.json", preflight)

        _write_json(
            bundle / "workspace/workspace-validation.json",
            {
                "schema_version": "1.0",
                "campaign_id": "GTQ-WB05-MF-v1",
                "phase_id": "phase-1-source-admission",
                "permitted": True,
                "canonical_path": r"C:\wb05",
                "root_created": False,
                "known_directories": [],
                "existing_data_preserved": True,
            },
        )

        configuration = (
            repository
            / "experiments/granite_turboquant_intel/configurations/"
            "workbook05/measurement-controls.json"
        )
        measurement_path = (
            bundle / "measurement/measurement-controls.json"
        )
        capture_measurement_controls(
            repository,
            configuration,
            measurement_path,
        )

        campaign_root = (
            repository
            / "experiments/granite_turboquant_intel/manifests/"
            "campaigns/GTQ-WB05-MF-v1"
        )
        (bundle / "controls").mkdir(parents=True, exist_ok=True)
        shutil.copyfile(
            campaign_root / "campaign-manifest.json",
            bundle / "controls/campaign-manifest.json",
        )

        route_a = _load_json(campaign_root / "route-a-source-admission.json")
        route_b = _load_json(campaign_root / "route-b-source-admission.json")
        self._mark_route_a_admitted(route_a)
        self._mark_route_b_blocked(route_b)
        _write_json(
            bundle / "controls/route-a-source-admission.json",
            route_a,
        )
        _write_json(
            bundle / "controls/route-b-source-admission.json",
            route_b,
        )

        route_a_command = (
            "routes/route-a/commands/runtime-source.command.json"
        )
        route_a_genai_command = (
            "routes/route-a/commands/genai-source.command.json"
        )
        route_b_command = (
            "routes/route-b/commands/runtime-source.command.json"
        )
        for command_path in (
            route_a_command,
            route_a_genai_command,
            route_b_command,
        ):
            _write_json(
                bundle / command_path,
                {
                    "command_id": Path(command_path).stem,
                    "argv": ["git", "status"],
                    "exit_code": 0,
                },
            )

        _write_json(
            bundle / "routes/route-a/source-tree-runtime.json",
            self._source_report(
                route_id=ROUTE_A_ID,
                source_role="runtime",
                repository_full_name="openvinotoolkit/openvino",
                origin_url=(
                    "https://github.com/openvinotoolkit/openvino.git"
                ),
                commit=(
                    "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
                ),
                command_path=route_a_command,
            ),
        )
        _write_json(
            bundle / "routes/route-a/source-tree-genai.json",
            self._source_report(
                route_id=ROUTE_A_ID,
                source_role="genai-compatibility-candidate",
                repository_full_name="openvinotoolkit/openvino.genai",
                origin_url=(
                    "https://github.com/openvinotoolkit/openvino.genai.git"
                ),
                commit=(
                    "bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0"
                ),
                command_path=route_a_genai_command,
            ),
        )
        _write_json(
            bundle / "routes/route-b/source-tree-runtime.json",
            self._source_report(
                route_id=ROUTE_B_ID,
                source_role="experimental-runtime",
                repository_full_name="EgorDuplensky/openvino",
                origin_url=(
                    "https://github.com/EgorDuplensky/openvino.git"
                ),
                commit=(
                    "1827f6458d049de11c1a8203c793af67c99935dc"
                ),
                command_path=route_b_command,
            ),
        )

        route_a_excerpt = (
            bundle / "routes/route-a/excerpts/RA-CAP-001.txt"
        )
        route_b_excerpt = (
            bundle / "routes/route-b/excerpts/RB-CAP-001.txt"
        )
        route_a_excerpt.parent.mkdir(parents=True, exist_ok=True)
        route_b_excerpt.parent.mkdir(parents=True, exist_ok=True)
        route_a_excerpt.write_text("TURBO\n", encoding="utf-8")
        route_b_excerpt.write_text(
            "TURBO\nnot yet supported\n",
            encoding="utf-8",
        )
        _write_json(
            bundle / "routes/route-a/source-capabilities.json",
            self._capability_report(
                route_id=ROUTE_A_ID,
                capability_id="RA-CAP-001",
                classification="Present in source",
                decision="Passed",
                excerpt_path="excerpts/RA-CAP-001.txt",
            ),
        )
        _write_json(
            bundle / "routes/route-b/source-capabilities.json",
            self._capability_report(
                route_id=ROUTE_B_ID,
                capability_id="RB-CAP-001",
                classification="Contradictory",
                decision="Candidate",
                excerpt_path="excerpts/RB-CAP-001.txt",
            ),
        )

        cmake_template = (
            repository
            / "experiments/granite_turboquant_intel/manifests/"
            "templates/workbook05/cmake-test-discovery-report-template.json"
        )
        _write_json(
            bundle / "routes/route-b/cmake-test-discovery.json",
            _load_json(cmake_template),
        )
        generated_metadata = (
            bundle
            / "routes/route-b/cmake/route-b-generated-target.txt"
        )
        generated_metadata.parent.mkdir(parents=True, exist_ok=True)
        generated_metadata.write_text("", encoding="utf-8")

        cache_path = (
            bundle / "routes/route-a/configure/CMakeCache.txt"
        )
        cache_path.parent.mkdir(parents=True, exist_ok=True)
        cache_path.write_text(
            "\n".join(
                (
                    "CMAKE_HOME_DIRECTORY:INTERNAL="
                    "C:/wb05/source/route-a/openvino",
                    "CMAKE_GENERATOR:INTERNAL=Visual Studio 17 2022",
                    "CMAKE_GENERATOR_PLATFORM:INTERNAL=x64",
                    "ENABLE_INTEL_GPU:BOOL=OFF",
                    "",
                )
            ),
            encoding="utf-8",
            newline="\n",
        )
        (
            bundle
            / "routes/route-a/configure/route-a-configure.stdout.txt"
        ).write_text("configured\n", encoding="utf-8")
        (
            bundle
            / "routes/route-a/configure/route-a-configure.stderr.txt"
        ).write_text("", encoding="utf-8")
        configure_template = (
            repository
            / "experiments/granite_turboquant_intel/manifests/"
            "templates/workbook05/configure-probe-report-template.json"
        )
        configure = _load_json(configure_template)
        configure["stdout_path"] = (
            "configure/route-a-configure.stdout.txt"
        )
        configure["stderr_path"] = (
            "configure/route-a-configure.stderr.txt"
        )
        configure["cmake_cache_sha256"] = _sha256(cache_path)
        _write_json(
            bundle / "routes/route-a/configure-probe.json",
            configure,
        )

        for route_id, name in (
            (ROUTE_A_ID, "route-a-merged-openvino"),
            (ROUTE_B_ID, "route-b-experimental-qjl-polar"),
        ):
            _write_json(
                bundle / f"commands/{name}-documented-commands.json",
                {
                    "schema_version": "1.0",
                    "campaign_id": "GTQ-WB05-MF-v1",
                    "route_id": route_id,
                    "execution_allowed": False,
                    "documents": [],
                    "commands": [],
                },
            )

        summary_template = (
            repository
            / "experiments/granite_turboquant_intel/manifests/"
            "templates/workbook05/source-admission-summary-template.json"
        )
        summary = _load_json(summary_template)
        summary["measurement_controls_sha256"] = _sha256(
            measurement_path
        )
        _write_json(
            bundle / "summary/source-admission-summary.json",
            summary,
        )
        (
            bundle / "summary/source-admission-summary.md"
        ).write_text(
            "# Workbook 05 Source-Admission Summary\n\n"
            "- Checkpoint: **Passed**\n",
            encoding="utf-8",
            newline="\n",
        )

        checkpoint = _load_json(campaign_root / "checkpoint.json")
        for step in checkpoint["steps"]:  # type: ignore[index]
            if step["step_id"] == "phase-1-source-admission":
                step["status"] = "Passed"
                step["evidence_sha256"] = "3" * 64
        _write_json(
            bundle / "checkpoint/checkpoint.json",
            checkpoint,
        )

        _write_json(
            bundle / "orchestration-report.json",
            {
                "schema_version": "1.0",
                "campaign_id": "GTQ-WB05-MF-v1",
                "phase_id": "phase-1-source-admission",
                "outcome": "Completed",
                "route_a_status": "Admitted",
                "route_b_status": "Blocked",
                "checkpoint_status": "Passed",
                "ordered_steps": list(STEP_ORDER),
                "completed_steps_before_hash": list(STEP_ORDER[:-1]),
                "step_results_before_hash": [],
            },
        )
        for step_id in STEP_ORDER:
            result = {
                "StepId": step_id,
                "Kind": (
                    "ScientificBlocker"
                    if step_id == "route-b-cmake-audit"
                    else "Success"
                ),
                "Status": (
                    "Blocked"
                    if step_id == "route-b-cmake-audit"
                    else "Passed"
                ),
                "Reason": "Synthetic controlled evidence.",
            }
            if step_id == "route-decisions":
                result.update(
                    {
                        "RouteAStatus": "Admitted",
                        "RouteBStatus": "Blocked",
                        "CheckpointStatus": "Passed",
                    }
                )
            _write_json(bundle / f"steps/{step_id}.json", result)

        write_hash_manifest(
            bundle,
            bundle / "hash-manifest.sha256",
        )
        return bundle

    def _issue_codes(
        self,
        bundle: Path,
        repository: Path,
    ) -> set[str]:
        return {
            issue.code
            for issue in validate_source_admission_bundle(
                bundle,
                repository,
            )
        }

    def test_valid_bundle_has_no_issues(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            bundle = self._create_valid_bundle(root, repository)
            issues = validate_source_admission_bundle(
                bundle,
                repository,
            )
        self.assertEqual([], issues)

    def test_changed_file_breaks_the_hash_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            bundle = self._create_valid_bundle(root, repository)
            (
                bundle / "summary/source-admission-summary.md"
            ).write_text("tampered\n", encoding="utf-8")
            codes = self._issue_codes(bundle, repository)
        self.assertIn("HASH_MISMATCH", codes)

    def test_false_admission_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            bundle = self._create_valid_bundle(root, repository)
            path = bundle / "controls/route-a-source-admission.json"
            record = _load_json(path)
            record["proofs"][0]["status"] = "Pending"  # type: ignore[index]
            record["proofs"][0]["evidence_path"] = ""  # type: ignore[index]
            _write_json(path, record)
            write_hash_manifest(
                bundle,
                bundle / "hash-manifest.sha256",
            )
            codes = self._issue_codes(bundle, repository)
        self.assertIn("FALSE_ADMISSION", codes)

    def test_wrong_origin_commit_and_incomplete_submodules_are_rejected(
        self,
    ) -> None:
        mutations = (
            (
                "actual_origin_url",
                "https://github.com/example/wrong.git",
                "SOURCE_PROVENANCE_MISMATCH",
            ),
            (
                "actual_commit",
                "f" * 40,
                "SOURCE_PROVENANCE_MISMATCH",
            ),
            (
                "submodules_complete",
                False,
                "SUBMODULE_INCOMPLETE",
            ),
        )
        for field, value, expected_code in mutations:
            with self.subTest(field=field):
                with tempfile.TemporaryDirectory() as temporary_directory:
                    root = Path(temporary_directory)
                    repository = self._copy_repository_contracts(root)
                    bundle = self._create_valid_bundle(
                        root,
                        repository,
                    )
                    path = (
                        bundle
                        / "routes/route-a/source-tree-runtime.json"
                    )
                    report = _load_json(path)
                    report[field] = value
                    _write_json(path, report)
                    write_hash_manifest(
                        bundle,
                        bundle / "hash-manifest.sha256",
                    )
                    codes = self._issue_codes(
                        bundle,
                        repository,
                    )
                self.assertIn(expected_code, codes)

    def test_route_b_cannot_be_admitted_with_confirmed_blocker(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            bundle = self._create_valid_bundle(root, repository)
            record_path = (
                bundle / "controls/route-b-source-admission.json"
            )
            record = _load_json(record_path)
            record["admission_status"] = "Admitted"
            for proof in record["proofs"]:  # type: ignore[index]
                proof["status"] = "Passed"
                if not proof["evidence_path"]:
                    proof["evidence_path"] = (
                        "routes/route-b/cmake-test-discovery.json"
                    )
            _write_json(record_path, record)

            summary_path = (
                bundle / "summary/source-admission-summary.json"
            )
            summary = _load_json(summary_path)
            summary["route_decisions"][ROUTE_B_ID][  # type: ignore[index]
                "status"
            ] = "Admitted"
            _write_json(summary_path, summary)
            write_hash_manifest(
                bundle,
                bundle / "hash-manifest.sha256",
            )
            codes = self._issue_codes(bundle, repository)
        self.assertIn("ROUTE_B_BLOCKER_CONFLICT", codes)

    def test_route_a_admission_requires_valid_configure_cache(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            bundle = self._create_valid_bundle(root, repository)
            (
                bundle / "routes/route-a/configure/CMakeCache.txt"
            ).unlink()
            write_hash_manifest(
                bundle,
                bundle / "hash-manifest.sha256",
            )
            codes = self._issue_codes(bundle, repository)
        self.assertIn("ROUTE_A_CONFIGURE_INVALID", codes)

    def test_invalid_measurement_control_hash_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            bundle = self._create_valid_bundle(root, repository)
            path = bundle / "measurement/measurement-controls.json"
            report = _load_json(path)
            report["controls"][0]["sha256"] = "0" * 64  # type: ignore[index]
            _write_json(path, report)

            summary_path = (
                bundle / "summary/source-admission-summary.json"
            )
            summary = _load_json(summary_path)
            summary["measurement_controls_sha256"] = _sha256(path)
            _write_json(summary_path, summary)
            write_hash_manifest(
                bundle,
                bundle / "hash-manifest.sha256",
            )
            codes = self._issue_codes(bundle, repository)
        self.assertIn("CONTROL_HASH_MISMATCH", codes)

    def test_permissive_measured_run_schema_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            schema_path = (
                repository
                / "experiments/granite_turboquant_intel/schemas/"
                "workbook05/measured-run-manifest.schema.json"
            )
            schema = _load_json(schema_path)
            schema["additionalProperties"] = True
            _write_json(schema_path, schema)
            bundle = self._create_valid_bundle(root, repository)
            codes = self._issue_codes(bundle, repository)
        self.assertIn("MEASURED_RUN_SCHEMA_PERMISSIVE", codes)

    def test_unsafe_evidence_path_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            bundle = self._create_valid_bundle(root, repository)
            path = bundle / "summary/source-admission-summary.json"
            summary = _load_json(path)
            summary["route_decisions"][ROUTE_A_ID][  # type: ignore[index]
                "evidence_paths"
            ][0] = "../escape"
            _write_json(path, summary)
            write_hash_manifest(
                bundle,
                bundle / "hash-manifest.sha256",
            )
            codes = self._issue_codes(bundle, repository)
        self.assertIn("UNSAFE_EVIDENCE_PATH", codes)

    def test_secrets_and_forbidden_payload_suffixes_are_rejected(
        self,
    ) -> None:
        forbidden_suffixes = (
            ".exe",
            ".dll",
            ".lib",
            ".zip",
            ".whl",
            ".gguf",
            ".safetensors",
            ".onnx",
            ".bin",
            ".xml",
        )
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            bundle = self._create_valid_bundle(root, repository)
            (bundle / "leak.txt").write_text(
                "github_pat_0123456789abcdef",
                encoding="utf-8",
            )
            for index, suffix in enumerate(forbidden_suffixes):
                (bundle / f"payload-{index}{suffix}").write_bytes(b"MZ")
            write_hash_manifest(
                bundle,
                bundle / "hash-manifest.sha256",
            )
            codes = self._issue_codes(bundle, repository)
        self.assertIn("SECRET_PATTERN", codes)
        self.assertIn("FORBIDDEN_PAYLOAD", codes)

    def test_cli_always_writes_markdown_report(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            repository = self._copy_repository_contracts(root)
            bundle = self._create_valid_bundle(root, repository)
            (
                bundle / "summary/source-admission-summary.md"
            ).write_text("tampered\n", encoding="utf-8")
            report_path = root / "validation/report.md"
            exit_code = main(
                [
                    "--bundle-root",
                    str(bundle),
                    "--repository-root",
                    str(repository),
                    "--report",
                    str(report_path),
                ]
            )
            report = report_path.read_text(encoding="utf-8")
        self.assertEqual(1, exit_code)
        self.assertTrue(
            report.startswith(
                "# Workbook 05 source-admission artifact validation"
            )
        )
        self.assertIn("HASH_MISMATCH", report)

    def test_validator_has_no_payload_execution_code_path(self) -> None:
        tree = ast.parse(
            VALIDATOR_PATH.read_text(encoding="utf-8"),
            filename=str(VALIDATOR_PATH),
        )
        forbidden_imports = {
            "ctypes",
            "importlib",
            "runpy",
            "subprocess",
        }
        imported = {
            alias.name.split(".", 1)[0]
            for node in ast.walk(tree)
            if isinstance(node, ast.Import)
            for alias in node.names
        }
        imported.update(
            node.module.split(".", 1)[0]
            for node in ast.walk(tree)
            if isinstance(node, ast.ImportFrom)
            and node.module is not None
        )
        forbidden_calls = {
            node.func.id
            for node in ast.walk(tree)
            if isinstance(node, ast.Call)
            and isinstance(node.func, ast.Name)
            and node.func.id in {
                "__import__",
                "compile",
                "eval",
                "exec",
            }
        }
        self.assertEqual(set(), forbidden_imports & imported)
        self.assertEqual(set(), forbidden_calls)


if __name__ == "__main__":
    unittest.main()
