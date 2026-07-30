import copy
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from scripts.testing.official_openvino.expected_rejections import (
    generate_expected_rejection_evidence,
    validate_expected_rejection_evidence,
    write_expected_rejection_evidence,
)


ROOT = Path(__file__).resolve().parents[3]
MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"
CLI = ROOT / "scripts/testing/generate_official_openvino_expected_rejections.py"
SCHEMA = "official-openvino-wb04-expected-rejection-evidence/v1"
EXPECTED_PROBE_IDS = {
    "OV-B11-QJL",
    "OV-B11-POLAR",
    *(f"OV-TQS-{index:02d}" for index in range(5, 13)),
    "OV-TQ-18",
    "OV-TQ-19",
    "OV-TQ-20",
}


def canonical_bytes(value: object) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        )
        + "\n"
    ).encode("utf-8")


class OfficialOpenVINOExpectedRejectionTests(unittest.TestCase):
    def test_generates_exact_fail_closed_probe_set_without_metrics(self):
        payload = generate_expected_rejection_evidence(MATRIX)
        self.assertEqual(payload["schema"], SCHEMA)
        self.assertEqual(payload["probe_count"], 13)
        self.assertEqual(
            {probe["probe_id"] for probe in payload["probes"]},
            EXPECTED_PROBE_IDS,
        )
        self.assertEqual(
            set(payload["controlled_test_ids"]),
            {
                "OV-B11",
                *(f"OV-TQS-{index:02d}" for index in range(5, 13)),
                "OV-TQ-18",
                "OV-TQ-19",
                "OV-TQ-20",
            },
        )
        self.assertIs(payload["generation_not_launched"], True)
        self.assertEqual(payload["cleanup_process_count"], 0)
        self.assertRegex(payload["matrix_sha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(payload["controller_sha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(payload["aggregate_sha256"], r"^[0-9a-f]{64}$")

        encoded = json.dumps(payload, sort_keys=True)
        for forbidden in (
            '"metrics"',
            '"quality"',
            '"metric_value"',
            '"mean_score"',
            '"numeric_result"',
        ):
            self.assertNotIn(forbidden, encoded)
        for probe in payload["probes"]:
            self.assertEqual(probe["status"], "passed: expected-rejection")
            self.assertEqual(probe["expected_outcome"], "expected-rejection")
            self.assertIs(probe["generation_not_launched"], True)
            self.assertEqual(probe["cleanup_process_count"], 0)
            self.assertEqual(probe["expected_exception"], probe["observed_exception"])
            self.assertEqual(probe["observed_exception"]["type"], "ValueError")
            self.assertRegex(probe["probe_sha256"], r"^[0-9a-f]{64}$")

        by_id = {probe["probe_id"]: probe for probe in payload["probes"]}
        self.assertEqual(
            by_id["OV-TQS-05"]["observed_exception"]["message"],
            "unsupported runtime value algorithm: SCALAR; "
            "expected exact uppercase STANDARD, TBQ3, or TBQ4",
        )
        self.assertEqual(
            by_id["OV-TQS-06"]["observed_exception"]["message"],
            "unsupported runtime key algorithm: SCALAR; "
            "expected exact uppercase STANDARD, TBQ3, or TBQ4",
        )
        self.assertEqual(
            by_id["OV-TQ-18"]["observed_exception"]["message"],
            "project TurboQuant is supported only on CPU",
        )
        self.assertEqual(
            by_id["OV-TQ-19"]["observed_exception"]["message"],
            "unsupported runtime key algorithm: QJL; "
            "expected exact uppercase STANDARD, TBQ3, or TBQ4",
        )
        self.assertEqual(
            by_id["OV-TQ-20"]["observed_exception"]["message"],
            "unsupported runtime key algorithm: POLAR; "
            "expected exact uppercase STANDARD, TBQ3, or TBQ4",
        )
        self.assertEqual(
            by_id["OV-B11-QJL"]["observed_exception"]["message"],
            "unsupported runtime key algorithm: QJL; "
            "expected exact uppercase STANDARD, TBQ3, or TBQ4",
        )
        self.assertEqual(
            by_id["OV-B11-POLAR"]["observed_exception"]["message"],
            "unsupported runtime value algorithm: POLAR; "
            "expected exact uppercase STANDARD, TBQ3, or TBQ4",
        )
        self.assertTrue(
            validate_expected_rejection_evidence(payload, MATRIX)["accepted"]
        )

    def test_b11_probes_preserve_one_exact_case_and_contract(self):
        payload = generate_expected_rejection_evidence(MATRIX)
        by_id = {probe["probe_id"]: probe for probe in payload["probes"]}
        qjl = by_id["OV-B11-QJL"]
        polar = by_id["OV-B11-POLAR"]
        self.assertEqual(qjl["matrix_case"], polar["matrix_case"])
        self.assertEqual(qjl["execution_contract"], polar["execution_contract"])
        self.assertEqual(qjl["matrix_case"]["test_id"], "OV-B11")
        self.assertEqual(qjl["matrix_case"]["k_algorithm"], "qjl")
        self.assertEqual(qjl["matrix_case"]["v_algorithm"], "polar")
        self.assertEqual(
            (
                qjl["runtime_property_input"]["key_algorithm"],
                qjl["runtime_property_input"]["value_algorithm"],
            ),
            ("QJL", "STANDARD"),
        )
        self.assertEqual(
            (
                polar["runtime_property_input"]["key_algorithm"],
                polar["runtime_property_input"]["value_algorithm"],
            ),
            ("STANDARD", "POLAR"),
        )

    def test_generation_and_both_hash_layers_are_deterministic(self):
        first = generate_expected_rejection_evidence(MATRIX)
        second = generate_expected_rejection_evidence(MATRIX)
        self.assertEqual(first, second)
        for probe in first["probes"]:
            unhashed = dict(probe)
            digest = unhashed.pop("probe_sha256")
            self.assertEqual(
                digest,
                hashlib.sha256(canonical_bytes(unhashed)).hexdigest(),
            )
        unhashed_payload = dict(first)
        aggregate = unhashed_payload.pop("aggregate_sha256")
        self.assertEqual(
            aggregate,
            hashlib.sha256(canonical_bytes(unhashed_payload)).hexdigest(),
        )

    def test_validator_rejects_missing_duplicate_tampered_or_non_rejection_data(self):
        original = generate_expected_rejection_evidence(MATRIX)
        mutations = {}

        missing = copy.deepcopy(original)
        missing["probes"].pop()
        mutations["missing probe"] = missing

        duplicate = copy.deepcopy(original)
        duplicate["probes"].append(copy.deepcopy(duplicate["probes"][0]))
        mutations["duplicate probe"] = duplicate

        wrong_error = copy.deepcopy(original)
        wrong_error["probes"][0]["observed_exception"]["message"] = "changed"
        mutations["changed exception"] = wrong_error

        launched = copy.deepcopy(original)
        launched["probes"][0]["generation_not_launched"] = False
        mutations["generation launched"] = launched

        dirty_cleanup = copy.deepcopy(original)
        dirty_cleanup["probes"][0]["cleanup_process_count"] = 1
        mutations["nonzero cleanup"] = dirty_cleanup

        metrics = copy.deepcopy(original)
        metrics["probes"][0]["metrics"] = {"ttft_ms": 0}
        mutations["manufactured metrics"] = metrics

        extra_top_level = copy.deepcopy(original)
        extra_top_level["note"] = "uncontrolled"
        mutations["extra aggregate field"] = extra_top_level

        matrix_hash = copy.deepcopy(original)
        matrix_hash["matrix_sha256"] = "0" * 64
        mutations["matrix hash"] = matrix_hash

        probe_hash = copy.deepcopy(original)
        probe_hash["probes"][0]["probe_sha256"] = "0" * 64
        mutations["probe hash"] = probe_hash

        aggregate_hash = copy.deepcopy(original)
        aggregate_hash["aggregate_sha256"] = "0" * 64
        mutations["aggregate hash"] = aggregate_hash

        for label, payload in mutations.items():
            with self.subTest(label=label):
                with self.assertRaises(ValueError):
                    validate_expected_rejection_evidence(payload, MATRIX)

    def test_atomic_writer_uses_canonical_byte_identical_json(self):
        payload = generate_expected_rejection_evidence(MATRIX)
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "nested" / "expected-rejections.json"
            write_expected_rejection_evidence(output, payload)
            first = output.read_bytes()
            self.assertEqual(first, canonical_bytes(payload))
            write_expected_rejection_evidence(output, payload)
            self.assertEqual(output.read_bytes(), first)

    def test_cli_writes_valid_byte_identical_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "expected-rejections.json"
            command = [
                sys.executable,
                str(CLI),
                "--matrix",
                str(MATRIX),
                "--output",
                str(output),
            ]
            first = subprocess.run(
                command,
                cwd=ROOT,
                capture_output=True,
                check=False,
                text=True,
            )
            self.assertEqual(first.returncode, 0, first.stderr)
            first_bytes = output.read_bytes()
            summary = json.loads(first.stdout)
            self.assertTrue(summary["accepted"])
            self.assertEqual(summary["probe_count"], 13)
            second = subprocess.run(
                command,
                cwd=ROOT,
                capture_output=True,
                check=False,
                text=True,
            )
            self.assertEqual(second.returncode, 0, second.stderr)
            self.assertEqual(output.read_bytes(), first_bytes)
            payload = json.loads(first_bytes)
            self.assertTrue(
                validate_expected_rejection_evidence(payload, MATRIX)["accepted"]
            )


if __name__ == "__main__":
    unittest.main()
