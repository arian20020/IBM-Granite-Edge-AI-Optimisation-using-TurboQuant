from __future__ import annotations

import csv
import hashlib
import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from scripts.testing.workbook05.hash_manifest import write_hash_manifest
from scripts.testing.workbook05.phase3.asset_bundle_validation import (
    validate_asset_bundle,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "manifests"
    / "templates"
    / "workbook05"
)


def _write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _dependency_preflight() -> dict[str, object]:
    packages = [
        {
            "name": "optimum-intel",
            "version": "2.3.0.dev0",
            "source_identity": (
                "a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0"
            ),
            "direct": True,
        },
        {
            "name": "optimum",
            "version": "2.3.0",
            "source_identity": (
                "982e495540364f95da1e4b6f62d2d4e5907d08fd"
            ),
            "direct": True,
        },
    ]
    for index, (name, version) in enumerate(
        (
            ("transformers", "5.5.0"),
            ("huggingface-hub", "1.21.0"),
            ("nncf", "3.2.0"),
            ("openvino", "2026.2.1"),
            ("openvino-tokenizers", "2026.2.1.0"),
            ("requests", "2.33.0"),
        ),
        start=1,
    ):
        packages.append(
            {
                "name": name,
                "version": version,
                "source_identity": "sha256:" + str(index) * 64,
                "direct": name != "requests",
            }
        )

    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "conversion-dependency-preflight",
        "route_id": "route-a-merged-openvino",
        "generated_at_utc": "2026-08-14T12:00:00Z",
        "workspace": {
            "root": r"C:\w5c\dependency-preflight-31800000000-1",
            "normal_local_directory": True,
            "fresh": True,
        },
        "python": {
            "version": "3.12.10",
            "executable_path": (
                r"C:\w5c\dependency-preflight-31800000000-1"
                r"\venv\Scripts\python.exe"
            ),
            "executable_sha256": "a" * 64,
        },
        "pip": {
            "version": "25.2",
            "executable_path": (
                r"C:\w5c\dependency-preflight-31800000000-1"
                r"\venv\Scripts\pip.exe"
            ),
            "executable_sha256": "b" * 64,
        },
        "source_trees": [
            {
                "name": "optimum-intel",
                "repository": "huggingface/optimum-intel",
                "origin": (
                    "https://github.com/huggingface/"
                    "optimum-intel.git"
                ),
                "commit": (
                    "a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0"
                ),
                "clean": True,
                "aggregate_sha256": "c" * 64,
            },
            {
                "name": "optimum",
                "repository": "huggingface/optimum",
                "origin": (
                    "https://github.com/huggingface/optimum.git"
                ),
                "commit": (
                    "982e495540364f95da1e4b6f62d2d4e5907d08fd"
                ),
                "clean": True,
                "aggregate_sha256": "d" * 64,
            },
        ],
        "direct_requirements": [
            (
                "optimum-intel @ git+https://github.com/huggingface/"
                "optimum-intel.git@"
                "a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0"
            ),
            (
                "optimum @ git+https://github.com/huggingface/"
                "optimum.git@"
                "982e495540364f95da1e4b6f62d2d4e5907d08fd"
            ),
            "transformers==5.5.0",
            "huggingface-hub==1.21.0",
            "nncf==3.2.0",
            "openvino==2026.2.1",
            "openvino-tokenizers==2026.2.1.0",
        ],
        "lock": {
            "path": "locks/requirements.phase3-assets.txt",
            "sha256": "e" * 64,
            "generator": "pip==25.2 resolver report",
            "normal_distribution_count": 6,
            "all_normal_artifacts_hashed": True,
            "vcs_sources_bound_separately": True,
        },
        "packages": packages,
        "checks": [
            {"name": "resolver", "status": "Passed", "exit_code": 0},
            {"name": "install", "status": "Passed", "exit_code": 0},
            {"name": "imports", "status": "Passed", "exit_code": 0},
            {"name": "cli_help", "status": "Passed", "exit_code": 0},
            {
                "name": "no_model_compatibility",
                "status": "Passed",
                "exit_code": 0,
            },
            {
                "name": "remote_code_disabled",
                "status": "Passed",
                "exit_code": None,
            },
        ],
        "import_modules": [
            "optimum",
            "optimum.intel",
            "transformers",
            "nncf",
            "openvino",
        ],
        "cli_help_exit_code": 0,
        "no_model_compatibility_exit_code": 0,
        "status": "Passed",
        "reasons": [
            "Exact clean Windows dependency preflight passed."
        ],
        "model_download_authorised": False,
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }


def _write_inventory(path: Path, rows: list[dict[str, object]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(
            stream,
            fieldnames=["relative_path", "size_bytes", "sha256"],
            lineterminator="\n",
        )
        writer.writeheader()
        writer.writerows(rows)


def _create_valid_bundle(bundle: Path) -> None:
    prerequisite = json.loads(
        (
            TEMPLATE_ROOT
            / "phase3-prerequisite-proof-template.json"
        ).read_text(encoding="utf-8")
    )
    prerequisite["reasons"] = [
        "Exact retained Runtime and GenAI revalidated."
    ]

    dependency = _dependency_preflight()
    _write_json(bundle / "dependency-preflight.json", dependency)
    dependency_digest = _sha256(bundle / "dependency-preflight.json")

    source_model = {
        "relative_path": "model.safetensors.index.json",
        "size_bytes": 19,
        "sha256": "1" * 64,
    }
    source_tokenizer = {
        "relative_path": "tokenizer.json",
        "size_bytes": 23,
        "sha256": "2" * 64,
    }
    converted = {
        "relative_path": "openvino_model.xml",
        "size_bytes": 31,
        "sha256": "3" * 64,
    }

    asset = json.loads(
        (TEMPLATE_ROOT / "model-asset-lock-template.json").read_text(
            encoding="utf-8"
        )
    )
    asset.update(
        {
            "source_files": [source_model],
            "tokenizer_files": [source_tokenizer],
            "conversion_record_path": "conversion-record.json",
            "converted_files": [converted],
            "reasons": [
                "Immutable source and conversion identities recorded."
            ],
        }
    )

    conversion = json.loads(
        (
            TEMPLATE_ROOT
            / "model-conversion-record-template.json"
        ).read_text(encoding="utf-8")
    )
    conversion["dependency_preflight"] = {
        "status": "Passed",
        "record_path": "dependency-preflight.json",
        "record_sha256": dependency_digest,
    }
    conversion["conversion"]["arguments"] = [
        "export",
        "openvino",
        "--model",
        asset["source_directory"],
        "--task",
        "text-generation-with-past",
        "--weight-format",
        "int4",
        "--group-size",
        "128",
        "--ratio",
        "1.0",
        asset["converted_directory"],
    ]
    conversion["output"] = {
        "directory": asset["converted_directory"],
        "files": [converted],
        "aggregate_sha256": "4" * 64,
    }
    conversion["process"].update(
        {
            "stdout_path": "logs/conversion.stdout.txt",
            "stderr_path": "logs/conversion.stderr.txt",
        }
    )
    conversion["reasons"] = [
        "Conversion completed and was recorded as Candidate."
    ]

    disk = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "disk-preflight",
        "status": "Passed",
        "failure_ids": [],
        "reasons": [],
        "free_bytes": 53_687_091_200,
        "minimum_free_bytes_before_granite_3b": 53_687_091_200,
        "inventories": {},
        "candidate_stale_workspaces": [],
        "deletion_authorised": False,
        "deletion_performed": False,
        "model_download_authorised": False,
        "granite_8b_download_authorised": False,
    }
    command = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "conversion-command",
        "executable_path": (
            r"C:\w5c\dependency-preflight-31800000000-1"
            r"\venv\Scripts\optimum-cli.exe"
        ),
        "arguments": conversion["conversion"]["arguments"],
        "shell": False,
    }

    _write_json(bundle / "prerequisite-proof.json", prerequisite)
    _write_json(bundle / "asset-lock.json", asset)
    _write_json(bundle / "conversion-record.json", conversion)
    _write_json(bundle / "disk-preflight.json", disk)
    _write_json(bundle / "commands" / "conversion.json", command)

    (bundle / "logs").mkdir(parents=True, exist_ok=True)
    (bundle / "logs" / "conversion.stdout.txt").write_text(
        "fixture conversion stdout\n",
        encoding="utf-8",
    )
    (bundle / "logs" / "conversion.stderr.txt").write_text(
        "",
        encoding="utf-8",
    )
    (bundle / "summary.md").write_text(
        "# C1 fixture\n\nNo scientific claim is authorised.\n",
        encoding="utf-8",
    )

    _write_inventory(
        bundle / "source-files.csv",
        [source_model, source_tokenizer],
    )
    _write_inventory(
        bundle / "converted-files.csv",
        [converted],
    )
    write_hash_manifest(bundle, bundle / "manifest.sha256")


class Phase3AssetBundleValidationTests(unittest.TestCase):
    def _issues_after(self, mutate) -> list:
        with TemporaryDirectory() as directory:
            bundle = Path(directory) / "bundle"
            bundle.mkdir()
            _create_valid_bundle(bundle)
            mutate(bundle)
            return validate_asset_bundle(bundle, REPOSITORY_ROOT)

    def test_valid_text_only_bundle_passes(self) -> None:
        self.assertEqual([], self._issues_after(lambda bundle: None))

    def test_changed_manifest_or_missing_prerequisite_is_rejected(
        self,
    ) -> None:
        cases = (
            lambda bundle: (bundle / "summary.md").write_text(
                "changed\n",
                encoding="utf-8",
            ),
            lambda bundle: (
                bundle / "prerequisite-proof.json"
            ).unlink(),
        )
        for mutate in cases:
            with self.subTest(mutate=mutate):
                codes = {
                    issue.code for issue in self._issues_after(mutate)
                }
                self.assertTrue(
                    {"HASH_MISMATCH", "REQUIRED_PATH_MISSING"} & codes
                )

    def test_traversal_secret_and_forbidden_payload_are_rejected(
        self,
    ) -> None:
        def traversal(bundle: Path) -> None:
            path = bundle / "conversion-record.json"
            record = json.loads(path.read_text(encoding="utf-8"))
            record["process"]["stdout_path"] = "../outside.txt"
            _write_json(path, record)
            write_hash_manifest(bundle, bundle / "manifest.sha256")

        def secret(bundle: Path) -> None:
            (bundle / "summary.md").write_text(
                "HF_TOKEN=not-a-real-token\n",
                encoding="utf-8",
            )
            write_hash_manifest(bundle, bundle / "manifest.sha256")

        def payload(bundle: Path) -> None:
            (bundle / "model.safetensors").write_bytes(b"payload")
            write_hash_manifest(bundle, bundle / "manifest.sha256")

        for mutate, code in (
            (traversal, "UNSAFE_EVIDENCE_PATH"),
            (secret, "SECRET_PATTERN"),
            (payload, "FORBIDDEN_PAYLOAD"),
        ):
            with self.subTest(code=code):
                self.assertIn(
                    code,
                    {
                        issue.code
                        for issue in self._issues_after(mutate)
                    },
                )

    def test_wrong_model_identity_or_false_claim_is_rejected(
        self,
    ) -> None:
        def wrong_identity(bundle: Path) -> None:
            path = bundle / "asset-lock.json"
            record = json.loads(path.read_text(encoding="utf-8"))
            record["source"]["repository"] = "other/model"
            _write_json(path, record)
            write_hash_manifest(bundle, bundle / "manifest.sha256")

        def false_claim(bundle: Path) -> None:
            path = bundle / "asset-lock.json"
            record = json.loads(path.read_text(encoding="utf-8"))
            record["performance_claim_authorised"] = True
            _write_json(path, record)
            write_hash_manifest(bundle, bundle / "manifest.sha256")

        for mutate, code in (
            (wrong_identity, "MODEL_IDENTITY"),
            (false_claim, "SCIENTIFIC_CLAIM"),
        ):
            with self.subTest(code=code):
                self.assertIn(
                    code,
                    {
                        issue.code
                        for issue in self._issues_after(mutate)
                    },
                )

    def test_positive_asset_with_failed_conversion_is_rejected(
        self,
    ) -> None:
        def mutate(bundle: Path) -> None:
            asset_path = bundle / "asset-lock.json"
            asset = json.loads(asset_path.read_text(encoding="utf-8"))
            asset["status"] = "Accepted"
            _write_json(asset_path, asset)

            conversion_path = bundle / "conversion-record.json"
            conversion = json.loads(
                conversion_path.read_text(encoding="utf-8")
            )
            conversion["status"] = "Failed"
            conversion["process"]["exit_code"] = 1
            _write_json(conversion_path, conversion)
            write_hash_manifest(bundle, bundle / "manifest.sha256")

        self.assertIn(
            "CONVERSION_RELATIONSHIP",
            {issue.code for issue in self._issues_after(mutate)},
        )


if __name__ == "__main__":
    unittest.main()
