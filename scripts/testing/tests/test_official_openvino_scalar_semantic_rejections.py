import copy
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from scripts.testing.official_openvino.scalar_semantic_rejections import (
    generate_scalar_semantic_rejection_evidence,
    validate_scalar_semantic_rejection_evidence,
    write_scalar_semantic_rejection_evidence,
)


ROOT = Path(__file__).resolve().parents[3]
MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"
U8_SPEC = ROOT / "experiments/raw-results/openvino-turboquant/2026-07-30/diagnostics/u8-standard-load-probe-attempt-002/spec.json"
U8_ATTEMPT = U8_SPEC.parent / "run/attempt.json"
U4_SPEC = ROOT / "experiments/raw-results/openvino-turboquant/2026-07-30/diagnostics/u4-standard-load-probe-attempt-001/spec.json"
U4_ATTEMPT = U4_SPEC.parent / "run/attempt.json"
CLI = ROOT / "scripts/testing/generate_official_openvino_scalar_rejections.py"
SCHEMA = "official-openvino-wb04-scalar-semantic-rejection-evidence/v1"


def canonical_bytes(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=True)
            + "\n").encode("utf-8")


def generate() -> dict:
    return generate_scalar_semantic_rejection_evidence(
        MATRIX, u8_spec_path=U8_SPEC, u8_attempt_path=U8_ATTEMPT,
        u4_spec_path=U4_SPEC, u4_attempt_path=U4_ATTEMPT,
    )


def copied_evidence(directory: Path) -> tuple[Path, Path, Path, Path]:
    paths = []
    for name, spec, attempt in (("u8", U8_SPEC, U8_ATTEMPT), ("u4", U4_SPEC, U4_ATTEMPT)):
        target = directory / name
        shutil.copytree(spec.parent, target)
        rewrite_json(
            target / "run/attempt.json",
            lambda value: value["command"].__setitem__(-1, str((target / "spec.json").resolve())),
        )
        paths.extend((target / "spec.json", target / "run/attempt.json"))
    return tuple(paths)  # type: ignore[return-value]


def rewrite_json(path: Path, mutate) -> None:
    value = json.loads(path.read_text(encoding="utf-8"))
    mutate(value)
    path.write_bytes(canonical_bytes(value))


class ScalarSemanticRejectionTests(unittest.TestCase):
    def test_generates_exact_four_probe_set_with_precision_mappings(self):
        payload = generate()

        self.assertEqual(payload["schema"], SCHEMA)
        self.assertEqual(payload["probe_count"], 4)
        self.assertEqual(
            [(probe["controlled_test_id"], probe["requested_cache_precision"])
             for probe in payload["probes"]],
            [("OV-04", "u8"), ("OV-05", "u4"), ("OV-TQ-01", "u8"), ("OV-TQ-02", "u4")],
        )
        self.assertTrue(validate_scalar_semantic_rejection_evidence(
            payload, MATRIX, u8_spec_path=U8_SPEC, u8_attempt_path=U8_ATTEMPT,
            u4_spec_path=U4_SPEC, u4_attempt_path=U4_ATTEMPT,
        )["accepted"])

    def test_probes_bind_concrete_state_and_no_numeric_metrics(self):
        payload = generate()
        for probe in payload["probes"]:
            self.assertEqual(probe["rejection_kind"], "post-activation-concrete-state-mismatch")
            self.assertEqual(probe["status"], "passed: expected-rejection")
            self.assertEqual(probe["expected_outcome"], "expected-rejection")
            self.assertEqual(probe["reported_cache_precision"], probe["requested_cache_precision"])
            self.assertEqual(probe["observed_key_state_precision"], "f32")
            self.assertEqual(probe["observed_value_state_precision"], "f32")
            self.assertTrue(probe["generation_launched"])
            self.assertFalse(probe["numeric_generation_metrics_accepted"])
            self.assertEqual(probe["metric_outcome"], "not-produced-by-expected-rejection")
            self.assertEqual(probe["cleanup_process_count"], 0)
            self.assertEqual(probe["actual_persistent_payload_bytes"], 0)
            self.assertEqual(probe["actual_persistent_norm_bytes"], 0)
            self.assertEqual(probe["actual_persistent_metadata_bytes"], 0)

    def test_hash_layers_are_canonical_and_deterministic(self):
        payload = generate()
        self.assertEqual(payload, generate())
        for probe in payload["probes"]:
            unhashed = dict(probe)
            digest = unhashed.pop("probe_sha256")
            self.assertEqual(digest, hashlib.sha256(canonical_bytes(unhashed)).hexdigest())
        unhashed = dict(payload)
        digest = unhashed.pop("aggregate_sha256")
        self.assertEqual(digest, hashlib.sha256(canonical_bytes(unhashed)).hexdigest())

    def test_rejects_identity_path_hash_and_strict_json_evidence_tampering(self):
        with tempfile.TemporaryDirectory() as directory:
            u8_spec, u8_attempt, u4_spec, u4_attempt = copied_evidence(Path(directory))
            mutations = {
                "diagnostic identity": (u8_spec, lambda value: value.update(controlled_test_id="wrong")),
                "bound command path": (u8_attempt, lambda value: value["command"].__setitem__(-1, "wrong-spec.json")),
                "stdout hash": (u8_attempt, lambda value: value.update(stdout_sha256="0" * 64)),
                "governed safety": (u8_attempt, lambda value: value.update(valid=False)),
                "cleanup": (u8_attempt, lambda value: value.update(cleanup_process_count=1)),
                "command injection": (u8_attempt, lambda value: value["command"].append("--extra")),
                "worker identity": (u8_attempt, lambda value: value["worker"].update(controlled_test_id="wrong")),
                "activation precision": (u8_attempt, lambda value: value["activation"].update(observed_key_state_precision="u8")),
                "allocation": (u8_attempt, lambda value: value["activation"].update(actual_persistent_payload_bytes=1)),
            }
            for label, (path, mutate) in mutations.items():
                with self.subTest(label=label):
                    pristine = path.read_bytes()
                    rewrite_json(path, mutate)
                    with self.assertRaises(ValueError):
                        generate_scalar_semantic_rejection_evidence(
                            MATRIX, u8_spec_path=u8_spec, u8_attempt_path=u8_attempt,
                            u4_spec_path=u4_spec, u4_attempt_path=u4_attempt,
                        )
                    path.write_bytes(pristine)

            u8_spec.write_bytes(b'\xef\xbb\xbf{}')
            with self.assertRaises(ValueError):
                generate_scalar_semantic_rejection_evidence(
                    MATRIX, u8_spec_path=u8_spec, u8_attempt_path=u8_attempt,
                    u4_spec_path=u4_spec, u4_attempt_path=u4_attempt,
                )

            u8_spec.write_text('{"schema":"one","schema":"two"}', encoding="utf-8")
            with self.assertRaises(ValueError):
                generate_scalar_semantic_rejection_evidence(
                    MATRIX, u8_spec_path=u8_spec, u8_attempt_path=u8_attempt,
                    u4_spec_path=u4_spec, u4_attempt_path=u4_attempt,
                )

    def test_validator_and_writer_fail_closed_and_create_only(self):
        payload = generate()
        mutations = []
        extra = copy.deepcopy(payload); extra["extra"] = True; mutations.append(extra)
        bad_hash = copy.deepcopy(payload); bad_hash["probes"][0]["probe_sha256"] = "0" * 64; mutations.append(bad_hash)
        bad_boolean = copy.deepcopy(payload); bad_boolean["probes"][0]["generation_launched"] = 1; mutations.append(bad_boolean)
        for candidate in mutations:
            with self.assertRaises(ValueError):
                validate_scalar_semantic_rejection_evidence(
                    candidate, MATRIX, u8_spec_path=U8_SPEC, u8_attempt_path=U8_ATTEMPT,
                    u4_spec_path=U4_SPEC, u4_attempt_path=U4_ATTEMPT,
                )
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "nested" / "scalar.json"
            write_scalar_semantic_rejection_evidence(
                output, payload, matrix_path=MATRIX, u8_spec_path=U8_SPEC,
                u8_attempt_path=U8_ATTEMPT, u4_spec_path=U4_SPEC, u4_attempt_path=U4_ATTEMPT,
            )
            self.assertEqual(output.read_bytes(), canonical_bytes(payload))
            with self.assertRaises(FileExistsError):
                write_scalar_semantic_rejection_evidence(
                    output, payload, matrix_path=MATRIX, u8_spec_path=U8_SPEC,
                    u8_attempt_path=U8_ATTEMPT, u4_spec_path=U4_SPEC, u4_attempt_path=U4_ATTEMPT,
                )

    def test_cli_publishes_only_new_valid_destination(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "scalar.json"
            command = [sys.executable, str(CLI), "--matrix", str(MATRIX), "--u8-spec", str(U8_SPEC),
                       "--u8-attempt", str(U8_ATTEMPT), "--u4-spec", str(U4_SPEC),
                       "--u4-attempt", str(U4_ATTEMPT), "--output", str(output)]
            first = subprocess.run(command, cwd=ROOT, text=True, capture_output=True, check=False)
            self.assertEqual(first.returncode, 0, first.stderr)
            self.assertTrue(json.loads(first.stdout)["accepted"])
            second = subprocess.run(command, cwd=ROOT, text=True, capture_output=True, check=False)
            self.assertNotEqual(second.returncode, 0)


if __name__ == "__main__":
    unittest.main()
