from __future__ import annotations

import csv
import hashlib
import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from scripts.testing.workbook05.phase3.c1_resume import (
    PRIOR_ARTIFACT_DIGEST,
    PRIOR_ARTIFACT_NAME,
    PRIOR_HEAD_SHA,
    PRIOR_RUN_ATTEMPT,
    PRIOR_RUN_ID,
    validate_prior_failure_bundle,
    validate_retained_source,
)
from scripts.testing.workbook05.phase3.hashing import sha256_file, sha256_tree


# These constants describe the one failed live C1 attempt that is eligible for
# controlled source reuse. The production module must not infer a different run.
EXPECTED_REVISION = "c0650403e44e78ec0262dab1c90914c65b196c4e"
EXPECTED_SOURCE_DIRECTORY = r"C:\w5m\sources\granite41-3b-c0650403"
EXPECTED_PARTIAL_CONVERSION = (
    r"C:\w5m\converted\granite41-3b-int4a-g128-r100-c0650403"
)
EXPECTED_STAGES = [
    "prerequisite-verification",
    "path-root-verification",
    "disk-preflight",
    "immutable-revision-resolution",
    "source-snapshot-download",
    "source-file-hash-inventory",
    "conversion-new-output-directory",
]


def _write_json(path: Path, value: object) -> None:
    """Write deterministic UTF-8 JSON used by the synthetic prior artifact."""

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _write_source_inventory(path: Path, rows: list[dict[str, object]]) -> None:
    """Write the same portable CSV shape retained by the real failed attempt."""

    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(
            stream,
            fieldnames=("relative_path", "size_bytes", "sha256"),
            lineterminator="\n",
        )
        writer.writeheader()
        writer.writerows(rows)


def _identity(path: Path, relative_path: str) -> dict[str, object]:
    """Return one source-file identity exactly as the C1 records represent it."""

    file_path = path.joinpath(*relative_path.split("/"))
    return {
        "relative_path": relative_path,
        "size_bytes": file_path.stat().st_size,
        "sha256": sha256_file(file_path),
    }


def _create_source_tree(source: Path) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    """Create a tiny model-shaped source tree for deterministic rehash tests."""

    source.mkdir(parents=True)
    (source / "config.json").write_text(
        json.dumps(
            {
                "num_hidden_layers": 40,
                "num_key_value_heads": 8,
                "max_position_embeddings": 131072,
            },
            sort_keys=True,
        ),
        encoding="utf-8",
        newline="\n",
    )
    (source / "model.safetensors").write_bytes(b"synthetic-model-bytes")
    (source / "tokenizer.json").write_text(
        '{"version":"1.0"}\n',
        encoding="utf-8",
        newline="\n",
    )

    model_rows = [
        _identity(source, "config.json"),
        _identity(source, "model.safetensors"),
    ]
    tokenizer_rows = [_identity(source, "tokenizer.json")]
    return model_rows, tokenizer_rows


def _aggregate(source: Path, rows: list[dict[str, object]]) -> str:
    """Calculate the canonical tree hash used by the live asset-lock code."""

    return sha256_tree(
        source,
        [source.joinpath(*str(row["relative_path"]).split("/")) for row in rows],
    )


def _create_prior_bundle(bundle: Path, source: Path) -> None:
    """Create the minimum truthful shape of failed run 32410714130."""

    bundle.mkdir(parents=True)
    model_rows, tokenizer_rows = _create_source_tree(source)
    all_rows = sorted(
        model_rows + tokenizer_rows,
        key=lambda row: str(row["relative_path"]).casefold(),
    )

    # Bind the prior artifact to the immutable Granite source and its hashes.
    resolved = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "resolved-model",
        "route_id": "route-a-merged-openvino",
        "repository": "ibm-granite/granite-4.1-3b",
        "requested_revision": "main",
        "resolved_revision": EXPECTED_REVISION,
        "license": "apache-2.0",
        "source_directory": EXPECTED_SOURCE_DIRECTORY,
        "declared_model_metadata": {
            "parameter_family": "3B",
            "layers": 40,
            "kv_heads": 8,
            "declared_sequence_length": 131072,
            "observed_runtime_capability": False,
        },
        "model_files": model_rows,
        "tokenizer_files": tokenizer_rows,
        "aggregate_model_sha256": _aggregate(source, model_rows),
        "aggregate_tokenizer_sha256": _aggregate(source, tokenizer_rows),
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }
    _write_json(bundle / "resolved-model.json", resolved)
    _write_source_inventory(bundle / "source-files.csv", all_rows)

    # Record the exact accepted dependency and Route A prerequisite boundaries.
    _write_json(
        bundle / "dependency-acceptance-proof.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "record_type": "dependency-acceptance-proof",
            "route_id": "route-a-merged-openvino",
            "status": "Passed",
            "workflow_run_id": "32211117536",
            "run_attempt": 1,
            "artifact_id": "9350956534",
            "artifact_sha256": (
                "b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21"
            ),
            "decision_sha256": (
                "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49"
            ),
            "decision_status": "Passed",
            "model_download_authorised": False,
            "granite_model_test_authorised": False,
            "activation_claim_authorised": False,
            "packed_storage_claim_authorised": False,
            "performance_claim_authorised": False,
            "quality_claim_authorised": False,
        },
    )
    _write_json(
        bundle / "prerequisite-proof.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "record_type": "prerequisite-proof",
            "route_id": "route-a-merged-openvino",
            "status": "Passed",
            "granite_model_test_authorised": False,
            "activation_claim_authorised": False,
            "packed_storage_claim_authorised": False,
            "performance_claim_authorised": False,
            "quality_claim_authorised": False,
        },
    )

    # Preserve the failed conversion command and the resource-watchdog reason.
    _write_json(
        bundle / "commands" / "conversion.json",
        {
            "file_path": r"C:\w5c\dependency-preflight-32211117536-1\workspace\environment\Scripts\optimum-cli.exe",
            "arguments": [
                "export",
                "openvino",
                "--model",
                EXPECTED_SOURCE_DIRECTORY,
                "--task",
                "text-generation-with-past",
                "--weight-format",
                "int4",
                "--group-size",
                "128",
                "--ratio",
                "1.0",
                EXPECTED_PARTIAL_CONVERSION,
            ],
            "exit_code": -1,
            "safety_stop_triggered": True,
        },
    )
    _write_json(
        bundle / "commands" / "conversion-native.resources.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "record_type": "build-resource-summary",
            "route_id": "route-a-merged-openvino",
            "component": "assets",
            "command_id": "conversion-native",
            "sample_interval_seconds": 2,
            "sample_count": 162,
            "peak_working_set_bytes": 9271861248,
            "peak_private_bytes": 8732930048,
            "minimum_available_memory_bytes": 1080770560,
            "maximum_commit_percent": 41,
            "heartbeat_timeout_seconds": 900,
            "safety_stop_triggered": True,
            "safety_stop_reason": (
                "Available memory remained below 1.5 GiB for 10 seconds."
            ),
        },
    )
    _write_json(
        bundle / "failure.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "route_id": "route-a-merged-openvino",
            "run_id": PRIOR_RUN_ID,
            "run_attempt": PRIOR_RUN_ATTEMPT,
            "status": "Failed",
            "completed_stages": EXPECTED_STAGES,
            "message": (
                "Granite 4.1 3B OpenVINO conversion triggered the resource "
                "watchdog: Available memory remained below 1.5 GiB for 10 seconds."
            ),
            "model_execution_authorised": False,
            "activation_claim_authorised": False,
            "packed_storage_claim_authorised": False,
            "performance_claim_authorised": False,
            "quality_claim_authorised": False,
        },
    )


class Phase3C1ResumeBundleValidationTests(unittest.TestCase):
    def test_constants_bind_only_the_independently_verified_failed_attempt(self) -> None:
        """The recovery path must not expose operator-selected prior identities."""

        self.assertEqual("32410714130", PRIOR_RUN_ID)
        self.assertEqual(1, PRIOR_RUN_ATTEMPT)
        self.assertEqual(
            "workbook-05-phase3-assets-32410714130-1",
            PRIOR_ARTIFACT_NAME,
        )
        self.assertEqual(
            "sha256:33d3ca32236d6da973c21c5d95ba36d088c5ed7384d93eb3f353dbd081671257",
            PRIOR_ARTIFACT_DIGEST,
        )
        self.assertEqual(
            "80946fc2e06767a8aeff878deab7d31f10fd6676",
            PRIOR_HEAD_SHA,
        )

    def test_exact_prior_conversion_watchdog_bundle_is_accepted(self) -> None:
        """A genuine conversion-stage interruption is eligible for source reuse."""

        with TemporaryDirectory() as directory:
            root = Path(directory)
            bundle = root / "bundle"
            source = root / "source"
            _create_prior_bundle(bundle, source)

            result = validate_prior_failure_bundle(bundle)

            self.assertTrue(result.valid, msg="\n".join(result.issues))
            self.assertEqual(0, len(result.issues))

    def test_success_shaped_or_wrong_stage_prior_artifact_is_rejected(self) -> None:
        """Recovery cannot relabel a success or a different failure as this attempt."""

        with TemporaryDirectory() as directory:
            root = Path(directory)
            bundle = root / "bundle"
            source = root / "source"
            _create_prior_bundle(bundle, source)

            # A success-only record must never appear in the failed prerequisite.
            _write_json(bundle / "asset-lock.json", {"status": "Candidate"})
            issues = validate_prior_failure_bundle(bundle).issues
            self.assertTrue(any("success-only" in issue for issue in issues))

            # The exact completed-stage boundary is part of the accepted identity.
            (bundle / "asset-lock.json").unlink()
            failure = json.loads((bundle / "failure.json").read_text(encoding="utf-8"))
            failure["completed_stages"] = EXPECTED_STAGES[:-1]
            _write_json(bundle / "failure.json", failure)
            issues = validate_prior_failure_bundle(bundle).issues
            self.assertTrue(any("completed stages" in issue for issue in issues))

    def test_retained_source_must_match_every_path_size_digest_and_tree_hash(self) -> None:
        """The already downloaded source is reused only after complete rehashing."""

        with TemporaryDirectory() as directory:
            root = Path(directory)
            bundle = root / "bundle"
            source = root / "source"
            _create_prior_bundle(bundle, source)

            accepted = validate_retained_source(
                bundle,
                source,
                expected_source_directory=str(source.resolve()),
            )
            self.assertTrue(accepted.valid, msg="\n".join(accepted.issues))

            # Changing one byte must invalidate the retained source prerequisite.
            (source / "model.safetensors").write_bytes(b"changed-model-bytes")
            changed = validate_retained_source(
                bundle,
                source,
                expected_source_directory=str(source.resolve()),
            )
            self.assertFalse(changed.valid)
            self.assertTrue(any("digest" in issue for issue in changed.issues))

    def test_retained_source_rejects_unrecorded_files(self) -> None:
        """An extra local file cannot silently enter the immutable source lock."""

        with TemporaryDirectory() as directory:
            root = Path(directory)
            bundle = root / "bundle"
            source = root / "source"
            _create_prior_bundle(bundle, source)
            (source / "unexpected.txt").write_text("drift\n", encoding="utf-8")

            result = validate_retained_source(
                bundle,
                source,
                expected_source_directory=str(source.resolve()),
            )

            self.assertFalse(result.valid)
            self.assertTrue(any("unexpected.txt" in issue for issue in result.issues))


if __name__ == "__main__":
    unittest.main()
