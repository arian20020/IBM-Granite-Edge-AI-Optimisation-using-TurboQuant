import copy
import hashlib
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

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
RESULT_MARKER = "OPENVINO_WB04_RESULT_JSON="
EXPECTED_FORMAL_METRICS = [
    "available_ram_min_mb",
    "cpu_percent",
    "decode_tps",
    "generation_duration_ms",
    "gpu_memory_peak_mb",
    "gpu_percent",
    "kv_mb",
    "load_ms",
    "peak_private_mb",
    "peak_working_set_mb",
    "prompt_tps",
    "tpot_ms",
    "ttft_ms",
]


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


def generate_paths(
    matrix: Path,
    u8_spec: Path,
    u8_attempt: Path,
    u4_spec: Path,
    u4_attempt: Path,
) -> dict:
    return generate_scalar_semantic_rejection_evidence(
        matrix,
        u8_spec_path=u8_spec,
        u8_attempt_path=u8_attempt,
        u4_spec_path=u4_spec,
        u4_attempt_path=u4_attempt,
    )


def rewrite_attempt_streams(
    path: Path,
    *,
    mutate_worker=None,
    mutate_activation=None,
) -> None:
    attempt = json.loads(path.read_text(encoding="utf-8"))
    if mutate_worker is not None:
        mutate_worker(attempt["worker"])
    if mutate_activation is not None:
        mutate_activation(attempt["activation"])
    stdout_path = path.parent / "stdout.txt"
    stderr_path = path.parent / "stderr.txt"
    stdout_path.write_text(
        RESULT_MARKER
        + json.dumps(
            attempt["worker"],
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
        newline="\n",
    )
    stderr_path.write_text(
        json.dumps(
            attempt["activation"],
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
        newline="\n",
    )
    attempt["stdout_sha256"] = hashlib.sha256(stdout_path.read_bytes()).hexdigest()
    attempt["stderr_sha256"] = hashlib.sha256(stderr_path.read_bytes()).hexdigest()
    attempt["output_sha256"] = hashlib.sha256(
        attempt["worker"]["output"].encode("utf-8")
    ).hexdigest()
    attempt["telemetry_sha256"] = hashlib.sha256(
        json.dumps(
            attempt["activation"],
            allow_nan=False,
            ensure_ascii=True,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("utf-8")
    ).hexdigest()
    path.write_bytes(canonical_bytes(attempt))


def rehash_payload(payload: dict) -> None:
    for probe in payload["probes"]:
        unhashed_probe = dict(probe)
        unhashed_probe.pop("probe_sha256")
        probe["probe_sha256"] = hashlib.sha256(
            canonical_bytes(unhashed_probe)
        ).hexdigest()
    unhashed_payload = dict(payload)
    unhashed_payload.pop("aggregate_sha256")
    payload["aggregate_sha256"] = hashlib.sha256(
        canonical_bytes(unhashed_payload)
    ).hexdigest()


class ScalarSemanticRejectionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.python_config_directory = tempfile.TemporaryDirectory()
        config_path = Path(cls.python_config_directory.name) / "pyvenv.cfg"
        version = ".".join(str(part) for part in sys.version_info[:3])
        config_path.write_text(
            f"version = {version}\n"
            f"executable = {Path(sys.executable).resolve()}\n",
            encoding="utf-8",
            newline="\n",
        )
        cls.python_config_environment = mock.patch.dict(
            os.environ,
            {"OPENVINO_WB04_PYTHON_CONFIG": str(config_path.resolve())},
        )
        cls.python_config_environment.start()

    @classmethod
    def tearDownClass(cls):
        cls.python_config_environment.stop()
        cls.python_config_directory.cleanup()

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
        expected_precision = {
            "OV-04": "u8",
            "OV-05": "u4",
            "OV-TQ-01": "u8",
            "OV-TQ-02": "u4",
        }
        expected_description = {
            "OV-04": "Granite 3B U8 scalar",
            "OV-05": "Granite 3B U4 scalar",
            "OV-TQ-01": "U8 control",
            "OV-TQ-02": "U4 control",
        }
        for probe in payload["probes"]:
            precision = expected_precision[probe["controlled_test_id"]]
            matrix_case = probe["matrix_case"]
            contract = probe["execution_contract"]
            self.assertEqual(
                matrix_case["description"],
                expected_description[probe["controlled_test_id"]],
            )
            self.assertEqual(
                matrix_case["required_metrics"],
                EXPECTED_FORMAL_METRICS,
            )
            self.assertEqual(matrix_case["k_algorithm"], "scalar")
            self.assertEqual(matrix_case["v_algorithm"], "scalar")
            self.assertEqual(matrix_case["weight_precision"], precision)
            self.assertEqual(matrix_case["k_precision"], precision)
            self.assertEqual(matrix_case["v_precision"], precision)
            self.assertEqual(matrix_case["key_cache_precision"], precision)
            self.assertEqual(matrix_case["value_cache_precision"], precision)
            self.assertEqual(matrix_case["runtime_key_algorithm"], "STANDARD")
            self.assertEqual(matrix_case["runtime_value_algorithm"], "STANDARD")
            self.assertIs(matrix_case["norm_correction"], False)
            self.assertEqual(
                matrix_case["attention_path"],
                "not-produced-by-expected-rejection",
            )
            self.assertEqual(matrix_case["execution_route"], "expected-rejection")
            self.assertEqual(matrix_case["expected_outcome"], "expected-rejection")
            self.assertIs(matrix_case["suitable_host_required"], False)
            self.assertIs(
                matrix_case["numeric_generation_metrics_expected"],
                False,
            )
            self.assertEqual(contract["runtime_key_algorithm"], "STANDARD")
            self.assertEqual(contract["runtime_value_algorithm"], "STANDARD")
            self.assertIs(contract["norm_correction"], False)
            self.assertEqual(
                contract["attention_path"],
                "not-produced-by-expected-rejection",
            )
            self.assertEqual(contract["execution_route"], "expected-rejection")
            self.assertEqual(contract["expected_outcome"], "expected-rejection")
            self.assertIs(contract["suitable_host_required"], False)
            self.assertIs(
                contract["requires_actual_cache_precision_proof"],
                False,
            )
            self.assertIs(
                contract["numeric_generation_metrics_expected"],
                False,
            )
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

    def test_rejects_matrix_semantic_and_precision_mapping_drift(self):
        mutations = {
            "u8 row relabelled u4": (
                "OV-04",
                lambda case: case.update(
                    weight_precision="u4",
                    k_precision="u4",
                    v_precision="u4",
                    key_cache_precision="u4",
                    value_cache_precision="u4",
                ),
            ),
            "scalar labels replaced by standard": (
                "OV-04",
                lambda case: case.update(
                    k_algorithm="standard",
                    v_algorithm="standard",
                ),
            ),
            "baseline description drift": (
                "OV-05",
                lambda case: case.update(description="changed"),
            ),
            "formal description drift": (
                "OV-TQ-01",
                lambda case: case.update(description="changed"),
            ),
            "formal required metrics drift": (
                "OV-TQ-02",
                lambda case: case.update(required_metrics=[]),
            ),
        }
        for label, (test_id, mutate) in mutations.items():
            with self.subTest(label=label), tempfile.TemporaryDirectory() as directory:
                matrix = Path(directory) / "matrix.json"
                matrix_payload = json.loads(MATRIX.read_text(encoding="utf-8"))
                target = next(
                    case for case in matrix_payload["cases"]
                    if case["test_id"] == test_id
                )
                mutate(target)
                matrix.write_bytes(canonical_bytes(matrix_payload))
                with self.assertRaises(ValueError):
                    generate_paths(
                        matrix, U8_SPEC, U8_ATTEMPT, U4_SPEC, U4_ATTEMPT
                    )

    def test_rejects_owned_bool_integer_smuggling_after_hash_recalculation(self):
        mutations = {
            "probe zero byte bool": lambda payload: payload["probes"][0].update(
                actual_persistent_payload_bytes=False
            ),
            "matrix case false integer": lambda payload: payload["probes"][0][
                "matrix_case"
            ].update(norm_correction=0),
            "contract false integer": lambda payload: payload["probes"][0][
                "execution_contract"
            ].update(suitable_host_required=0),
        }
        for label, mutate in mutations.items():
            with self.subTest(label=label):
                candidate = copy.deepcopy(generate())
                mutate(candidate)
                rehash_payload(candidate)
                with self.assertRaises(ValueError):
                    validate_scalar_semantic_rejection_evidence(
                        candidate,
                        MATRIX,
                        u8_spec_path=U8_SPEC,
                        u8_attempt_path=U8_ATTEMPT,
                        u4_spec_path=U4_SPEC,
                        u4_attempt_path=U4_ATTEMPT,
                    )

    def test_rejects_existing_non_python_executable_substitution(self):
        with tempfile.TemporaryDirectory() as directory:
            u8_spec, u8_attempt, u4_spec, u4_attempt = copied_evidence(
                Path(directory)
            )
            rewrite_json(
                u8_attempt,
                lambda value: value["command"].__setitem__(
                    0, str(MATRIX.resolve())
                ),
            )
            with self.assertRaises(ValueError):
                generate_paths(
                    MATRIX, u8_spec, u8_attempt, u4_spec, u4_attempt
                )

    def test_rejects_existing_same_name_fake_python_executable(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            u8_spec, u8_attempt, u4_spec, u4_attempt = copied_evidence(root)
            fake_python = root / "python.exe"
            shutil.copyfile(MATRIX, fake_python)
            rewrite_json(
                u8_attempt,
                lambda value: value["command"].__setitem__(
                    0, str(fake_python.resolve())
                ),
            )
            with self.assertRaises(ValueError):
                generate_paths(
                    MATRIX, u8_spec, u8_attempt, u4_spec, u4_attempt
                )

    def test_reads_each_bound_raw_artifact_once(self):
        tracked = {
            path.resolve(): 0
            for path in (MATRIX, U8_SPEC, U8_ATTEMPT, U4_SPEC, U4_ATTEMPT)
        }
        tracked.update({
            U8_ATTEMPT.parent.joinpath("stdout.txt").resolve(): 0,
            U8_ATTEMPT.parent.joinpath("stderr.txt").resolve(): 0,
            U4_ATTEMPT.parent.joinpath("stdout.txt").resolve(): 0,
            U4_ATTEMPT.parent.joinpath("stderr.txt").resolve(): 0,
        })
        original_open = Path.open

        def counting_open(path, *args, **kwargs):
            mode = args[0] if args else kwargs.get("mode", "r")
            resolved = path.resolve()
            if "r" in mode and resolved in tracked:
                tracked[resolved] += 1
                if resolved == MATRIX.resolve() and tracked[resolved] > 1:
                    return (
                        io.BytesIO(b"{}")
                        if "b" in mode
                        else io.StringIO("{}")
                    )
            return original_open(path, *args, **kwargs)

        with mock.patch.object(Path, "open", counting_open):
            generate()
        self.assertEqual(set(tracked.values()), {1}, tracked)

    def test_rejects_bomless_utf16_json(self):
        with tempfile.TemporaryDirectory() as directory:
            u8_spec, u8_attempt, u4_spec, u4_attempt = copied_evidence(
                Path(directory)
            )
            spec = json.loads(u8_spec.read_text(encoding="utf-8"))
            u8_spec.write_bytes(
                json.dumps(spec, ensure_ascii=False).encode("utf-16-le")
            )
            with self.assertRaises(ValueError):
                generate_paths(
                    MATRIX, u8_spec, u8_attempt, u4_spec, u4_attempt
                )

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

            u8_spec.write_text(
                '{"schema":"official-openvino-wb04-worker-spec/v1",'
                '"value":NaN}',
                encoding="utf-8",
            )
            with self.assertRaises(ValueError):
                generate_scalar_semantic_rejection_evidence(
                    MATRIX, u8_spec_path=u8_spec, u8_attempt_path=u8_attempt,
                    u4_spec_path=u4_spec, u4_attempt_path=u4_attempt,
                )

            u8_spec.write_bytes(b"\xff")
            with self.assertRaises(ValueError):
                generate_scalar_semantic_rejection_evidence(
                    MATRIX, u8_spec_path=u8_spec, u8_attempt_path=u8_attempt,
                    u4_spec_path=u4_spec, u4_attempt_path=u4_attempt,
                )

            u8_spec.write_bytes(b"[]")
            with self.assertRaises(ValueError):
                generate_scalar_semantic_rejection_evidence(
                    MATRIX, u8_spec_path=u8_spec, u8_attempt_path=u8_attempt,
                    u4_spec_path=u4_spec, u4_attempt_path=u4_attempt,
                )

    def test_rejects_governed_job_worker_and_activation_boundary_mutations(self):
        mutations = (
            (
                "exit code bool",
                "attempt",
                lambda value: value.update(exit_code=False),
            ),
            (
                "sampler setup integer",
                "attempt",
                lambda value: value["sampler_job"].update(setup_ok=1),
            ),
            (
                "workload query false",
                "attempt",
                lambda value: value["workload_job"].update(query_ok=False),
            ),
            (
                "workload survivor count bool",
                "attempt",
                lambda value: value["workload_job"].update(
                    queried_active_process_count_after_cleanup=False
                ),
            ),
            (
                "worker schema",
                "worker",
                lambda value: value.update(schema="wrong"),
            ),
            (
                "worker model",
                "worker",
                lambda value: value.update(model_path="wrong-model"),
            ),
            (
                "worker output validity integer",
                "worker",
                lambda value: value.update(output_valid=1),
            ),
            (
                "worker empty output",
                "worker",
                lambda value: value.update(output=""),
            ),
            (
                "worker generated token bool",
                "worker",
                lambda value: value.update(num_generated_tokens=True),
            ),
            (
                "requested key precision",
                "activation",
                lambda value: value.update(
                    requested_key_cache_precision="u4"
                ),
            ),
            (
                "reported value precision",
                "activation",
                lambda value: value.update(
                    activated_value_cache_precision="u4"
                ),
            ),
            (
                "observed value concrete state",
                "activation",
                lambda value: value.update(
                    observed_value_state_precision="u8"
                ),
            ),
            (
                "fallback integer",
                "activation",
                lambda value: value.update(fallback=0),
            ),
        )
        for label, boundary, mutate in mutations:
            with self.subTest(label=label), tempfile.TemporaryDirectory() as directory:
                u8_spec, u8_attempt, u4_spec, u4_attempt = copied_evidence(
                    Path(directory)
                )
                if boundary == "attempt":
                    rewrite_json(u8_attempt, mutate)
                elif boundary == "worker":
                    rewrite_attempt_streams(
                        u8_attempt,
                        mutate_worker=mutate,
                    )
                else:
                    rewrite_attempt_streams(
                        u8_attempt,
                        mutate_activation=mutate,
                    )
                with self.assertRaises(ValueError):
                    generate_paths(
                        MATRIX, u8_spec, u8_attempt, u4_spec, u4_attempt
                    )

    def test_reaches_allocation_and_each_hash_reconciliation_boundary(self):
        with tempfile.TemporaryDirectory() as directory:
            u8_spec, u8_attempt, u4_spec, u4_attempt = copied_evidence(
                Path(directory)
            )
            rewrite_attempt_streams(
                u8_attempt,
                mutate_activation=lambda value: value.update(
                    actual_persistent_payload_bytes=1
                ),
            )
            with self.assertRaisesRegex(ValueError, "payload byte"):
                generate_paths(
                    MATRIX, u8_spec, u8_attempt, u4_spec, u4_attempt
                )

        hash_mutations = (
            ("stdout", "stdout_sha256", "stdout bytes"),
            ("stderr", "stderr_sha256", "stderr bytes"),
            ("output", "output_sha256", "output SHA-256"),
            ("telemetry", "telemetry_sha256", "telemetry SHA-256"),
        )
        for label, field, message in hash_mutations:
            with self.subTest(label=label), tempfile.TemporaryDirectory() as directory:
                u8_spec, u8_attempt, u4_spec, u4_attempt = copied_evidence(
                    Path(directory)
                )
                rewrite_attempt_streams(u8_attempt)
                rewrite_json(
                    u8_attempt,
                    lambda value, target=field: value.update(
                        {target: "0" * 64}
                    ),
                )
                with self.assertRaisesRegex(ValueError, message):
                    generate_paths(
                        MATRIX, u8_spec, u8_attempt, u4_spec, u4_attempt
                    )

    def test_rejects_duplicate_json_in_hash_bound_worker_stream(self):
        with tempfile.TemporaryDirectory() as directory:
            u8_spec, u8_attempt, u4_spec, u4_attempt = copied_evidence(
                Path(directory)
            )
            attempt = json.loads(u8_attempt.read_text(encoding="utf-8"))
            stdout = u8_attempt.parent / "stdout.txt"
            worker = json.dumps(
                attempt["worker"],
                ensure_ascii=False,
                separators=(",", ":"),
                sort_keys=True,
            )
            worker = worker[:-1] + ',"schema":"duplicate"}'
            stdout.write_text(
                RESULT_MARKER + worker + "\n",
                encoding="utf-8",
                newline="\n",
            )
            attempt["stdout_sha256"] = hashlib.sha256(
                stdout.read_bytes()
            ).hexdigest()
            u8_attempt.write_bytes(canonical_bytes(attempt))
            with self.assertRaises(ValueError):
                generate_paths(
                    MATRIX, u8_spec, u8_attempt, u4_spec, u4_attempt
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

    def test_rejects_every_owned_aggregate_and_probe_type_boundary(self):
        mutations = (
            ("aggregate path integer", lambda p: p.update(matrix_path=1)),
            ("aggregate count bool", lambda p: p.update(probe_count=True)),
            (
                "aggregate ID container tuple",
                lambda p: p.update(
                    controlled_test_ids=tuple(p["controlled_test_ids"])
                ),
            ),
            (
                "aggregate cleanup bool",
                lambda p: p.update(cleanup_process_count=False),
            ),
            (
                "probe ID integer",
                lambda p: p["probes"][0].update(probe_id=1),
            ),
            (
                "probe case list",
                lambda p: p["probes"][0].update(matrix_case=[]),
            ),
            (
                "probe precision integer",
                lambda p: p["probes"][0].update(
                    requested_cache_precision=8
                ),
            ),
            (
                "probe standard bytes bool",
                lambda p: p["probes"][0].update(
                    actual_persistent_standard_bytes=True
                ),
            ),
            (
                "probe generation integer",
                lambda p: p["probes"][0].update(generation_launched=1),
            ),
            (
                "probe numeric acceptance integer",
                lambda p: p["probes"][0].update(
                    numeric_generation_metrics_accepted=0
                ),
            ),
            (
                "probe source hash shape",
                lambda p: p["probes"][0].update(spec_sha256="ABC"),
            ),
        )
        for label, mutate in mutations:
            with self.subTest(label=label):
                candidate = copy.deepcopy(generate())
                mutate(candidate)
                try:
                    rehash_payload(candidate)
                except (KeyError, TypeError):
                    pass
                with self.assertRaises(ValueError):
                    validate_scalar_semantic_rejection_evidence(
                        candidate,
                        MATRIX,
                        u8_spec_path=U8_SPEC,
                        u8_attempt_path=U8_ATTEMPT,
                        u4_spec_path=U4_SPEC,
                        u4_attempt_path=U4_ATTEMPT,
                    )


if __name__ == "__main__":
    unittest.main()
