import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.campaigns.openvino.conversion import (
    ConversionSpec,
    validate_artifact_manifest,
    validate_conversion,
)


class OfficialOpenVINOConversionTests(unittest.TestCase):
    def fixture(self, **overrides):
        value = {
            "test_id": "OV-C01", "source_model": "ibm-granite/granite-4.1-3b",
            "source_revision": "a" * 40, "precision": "f16",
            "command": ["optimum-cli", "export", "openvino"],
            "tool_versions": {"optimum-intel": "2.0.0", "openvino": "2026.2.1"},
            "files": {"openvino_model.xml": "b" * 64,
                      "openvino_model.bin": "c" * 64,
                      "tokenizer.json": "d" * 64, "config.json": "e" * 64},
            "load_probe": {"device": "CPU", "loaded": True, "generated_tokens": 4},
        }
        value.update(overrides)
        return value

    def test_conversion_requires_model_tokenizer_config_hashes_and_load_probe(self):
        for mutation, message in (
            ({"files": {"openvino_model.xml": "b" * 64,
                         "openvino_model.bin": "c" * 64,
                         "config.json": "e" * 64}}, "tokenizer hash"),
            ({"load_probe": {"device": "CPU", "loaded": False}}, "load probe"),
        ):
            broken = self.fixture(**mutation)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                validate_conversion(ConversionSpec(**broken))

    def test_revision_command_versions_and_precision_are_mandatory(self):
        for field in ("source_revision", "command", "tool_versions", "precision"):
            broken = self.fixture(**{field: "" if field != "command" else []})
            with self.subTest(field=field), self.assertRaises(ValueError):
                validate_conversion(ConversionSpec(**broken))

    def test_sourced_memory_gate_is_terminal_without_fake_artifacts(self):
        result = validate_conversion(ConversionSpec(
            test_id="OV-C04", source_model="ibm-granite/granite-4.1-8b",
            source_revision="f" * 40, precision="f16", command=[], tool_versions={},
            files={}, load_probe={}, terminal_classification="memory-gate-not-run",
            terminal_evidence={"available_ram_bytes": 1024, "required_floor_bytes": 2048},
        ))
        self.assertEqual(result["status"], "memory-gate-not-run")

    def test_terminal_gate_rejects_missing_or_nonbinding_evidence(self):
        with self.assertRaisesRegex(ValueError, "terminal evidence"):
            validate_conversion(ConversionSpec(
                test_id="OV-C04", source_model="ibm-granite/granite-4.1-8b",
                source_revision="f" * 40, precision="f16", command=[], tool_versions={},
                files={}, load_probe={}, terminal_classification="memory-gate-not-run",
                terminal_evidence={},
            ))


class OfficialOpenVINOArtifactTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.artifact = self.root / "model"
        self.artifact.mkdir()
        contents = {
            "openvino_model.xml": '<net><data element_type="i4"/></net>',
            "openvino_model.bin": "packed model",
            "openvino_tokenizer.xml": "<net/>",
            "openvino_tokenizer.bin": "tokenizer IR",
            "openvino_detokenizer.xml": "<net/>",
            "openvino_detokenizer.bin": "detokenizer IR",
            "tokenizer.json": '{"version":"1.0"}',
            "tokenizer_config.json": '{"model_max_length":4096}',
            "config.json": '{"model_type":"granite"}',
            "generation_config.json": '{"do_sample":false}',
            "openvino_config.json": '{"optimum_version":"2.1.0"}',
            "README.md": "---\nlicense: apache-2.0\n---\nConverted model.\n",
        }
        for name, content in contents.items():
            (self.artifact / name).write_text(content, encoding="utf-8")
        self.log = self.root / "load-probe.log"
        self.log.write_text("CPU generation passed\n", encoding="utf-8")
        self.output = "Granite probe output"
        self.files = []
        for path in sorted(self.artifact.iterdir()):
            if path.is_file():
                self.files.append({
                    "path": path.name,
                    "size_bytes": path.stat().st_size,
                    "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                })
        self.files.sort(key=lambda item: item["path"])
        canonical = json.dumps(
            self.files, sort_keys=True, separators=(",", ":")
        ).encode("utf-8")
        self.inventory_sha256 = hashlib.sha256(canonical).hexdigest()
        self.manifest = {
            "schema_version": 1,
            "status": "load-proven",
            "artifact_id": "granite-4.1-3b-u4-openvino",
            "artifact_root": str(self.artifact),
            "model": {
                "family": "granite-4.1",
                "parameter_scale": "3b",
                "precision": "u4",
                "source_repository": "ibm-granite/granite-4.1-3b",
                "source_revision": "a" * 40,
                "artifact_repository": "publisher/granite-4.1-3b-int4-ov",
                "artifact_revision": "b" * 40,
            },
            "conversion": {
                "kind": "published-preconverted",
                "command": [
                    "optimum-cli", "export", "openvino", "--weight-format", "int4"
                ],
                "tool_versions": {
                    "optimum-intel": "2.1.0.dev0",
                    "transformers": "5.5.0",
                },
                "provenance_path": "README.md",
                "provenance_sha256": next(
                    item["sha256"] for item in self.files if item["path"] == "README.md"
                ),
            },
            "files": self.files,
            "inventory_sha256": self.inventory_sha256,
            "precision_proof": {
                "path": "openvino_model.xml",
                "sha256": next(
                    item["sha256"]
                    for item in self.files
                    if item["path"] == "openvino_model.xml"
                ),
                "element_type": "i4",
                "element_type_count": 1,
            },
            "license": {
                "spdx": "Apache-2.0",
                "path": "README.md",
                "sha256": next(
                    item["sha256"] for item in self.files if item["path"] == "README.md"
                ),
            },
            "load_probe": {
                "status": "passed",
                "device_requested": "CPU",
                "device_actual": "CPU",
                "fallback": False,
                "model_path": str(self.artifact),
                "command": ["probe.exe", "--model", str(self.artifact), "--device", "CPU"],
                "generated_tokens": 4,
                "output": self.output,
                "output_sha256": hashlib.sha256(self.output.encode("utf-8")).hexdigest(),
                "artifact_inventory_sha256": self.inventory_sha256,
                "runtime_build_manifest_sha256": "c" * 64,
                "exit_code": 0,
                "cleanup_process_count": 0,
                "log_path": str(self.log),
                "log_sha256": hashlib.sha256(self.log.read_bytes()).hexdigest(),
            },
        }
        self.manifest_path = self.root / "artifact-manifest.json"

    def refresh_inventory(self):
        self.files = []
        for path in sorted(self.artifact.iterdir()):
            if path.is_file():
                self.files.append({
                    "path": path.name,
                    "size_bytes": path.stat().st_size,
                    "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                })
        self.files.sort(key=lambda item: item["path"])
        canonical = json.dumps(
            self.files, sort_keys=True, separators=(",", ":")
        ).encode("utf-8")
        self.inventory_sha256 = hashlib.sha256(canonical).hexdigest()
        self.manifest["files"] = self.files
        self.manifest["inventory_sha256"] = self.inventory_sha256
        self.manifest["load_probe"]["artifact_inventory_sha256"] = (
            self.inventory_sha256
        )

    def make_local_fp16_manifest(self):
        (self.artifact / "openvino_model.xml").write_text(
            '<net><data element_type="f16"/></net>',
            encoding="utf-8",
        )
        (self.artifact / "openvino_config.json").unlink()
        (self.artifact / "README.md").unlink()
        provenance = self.root / "conversion-summary.json"
        provenance.write_text(
            '{"source_revision":"' + ("a" * 40) + '","exit_code":0}',
            encoding="utf-8",
        )
        license_path = self.root / "source-README.md"
        license_path.write_text(
            "---\nlicense: apache-2.0\n---\nIBM Granite source snapshot.\n",
            encoding="utf-8",
        )
        self.refresh_inventory()
        xml_record = next(
            item for item in self.files if item["path"] == "openvino_model.xml"
        )
        self.manifest["artifact_id"] = "granite-4.1-3b-f16-openvino-local"
        self.manifest["model"].update({
            "precision": "f16",
            "artifact_repository": "local-conversion",
            "artifact_revision": self.inventory_sha256,
        })
        self.manifest["conversion"].update({
            "kind": "local-conversion",
            "command": [
                "optimum-cli", "export", "openvino",
                "-m", "ibm-granite/granite-4.1-3b",
                str(self.artifact),
            ],
            "provenance_path": str(provenance),
            "provenance_sha256": hashlib.sha256(
                provenance.read_bytes()
            ).hexdigest(),
        })
        self.manifest["precision_proof"] = {
            "path": "openvino_model.xml",
            "sha256": xml_record["sha256"],
            "element_type": "f16",
            "element_type_count": 1,
        }
        self.manifest["license"] = {
            "spdx": "Apache-2.0",
            "path": str(license_path),
            "sha256": hashlib.sha256(license_path.read_bytes()).hexdigest(),
        }
        return self.manifest

    def make_controlled_worker_probe(self):
        spec_path = self.root / "load-probe-spec.json"
        spec = {
            "schema": "official-openvino-wb04-worker-spec/v1",
            "controlled_test_id": "WB04-U4-LOAD-PROBE",
            "role": "pilot",
            "context": "diagnostic-short-prompt",
            "model_path": str(self.artifact),
            "device": "CPU",
            "max_new_tokens": 4,
        }
        spec_path.write_text(
            json.dumps(spec, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        command = [
            "python.exe",
            "-m",
            "scripts.testing.campaigns.openvino.measurement_worker",
            "--spec",
            str(spec_path.resolve()),
        ]
        self.log.write_text("controlled worker stdout\n", encoding="utf-8")
        attempt_path = self.root / "attempt.json"
        attempt = {
            "schema": "official-openvino-wb04-governed-run/v1",
            "valid": True,
            "validation_errors": [],
            "exit_code": 0,
            "timed_out": False,
            "low_memory_stop": False,
            "cleanup_process_count": 0,
            "command": command,
            "output_sha256": hashlib.sha256(
                self.output.encode("utf-8")
            ).hexdigest(),
            "stdout_sha256": hashlib.sha256(self.log.read_bytes()).hexdigest(),
            "worker": {
                "schema": "official-openvino-wb04-worker/v1",
                "controlled_test_id": spec["controlled_test_id"],
                "role": spec["role"],
                "context": spec["context"],
                "model_path": str(self.artifact),
                "device": "CPU",
                "num_generated_tokens": 4,
                "output": self.output,
                "output_valid": True,
            },
        }
        attempt_path.write_text(
            json.dumps(attempt, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        self.manifest["load_probe"].update({
            "command": command,
            "spec_path": str(spec_path.resolve()),
            "spec_sha256": hashlib.sha256(spec_path.read_bytes()).hexdigest(),
            "attempt_path": str(attempt_path.resolve()),
            "attempt_sha256": hashlib.sha256(attempt_path.read_bytes()).hexdigest(),
            "log_path": str(self.log.resolve()),
            "log_sha256": hashlib.sha256(self.log.read_bytes()).hexdigest(),
        })
        return spec_path, attempt_path

    def rewrite_probe_json(self, path, hash_field, value):
        path.write_text(
            json.dumps(value, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        self.manifest["load_probe"][hash_field] = hashlib.sha256(
            path.read_bytes()
        ).hexdigest()

    def tearDown(self):
        self.temporary.cleanup()

    def validate(self, manifest=None, *, expected_precision="u4"):
        manifest = self.manifest if manifest is None else manifest
        self.manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        return validate_artifact_manifest(
            self.manifest_path,
            expected_precision=expected_precision,
        )

    def test_accepts_hash_bound_precision_proven_cpu_generation_artifact(self):
        result = self.validate()
        self.assertTrue(result["accepted"])
        self.assertEqual(result["precision"], "u4")
        self.assertEqual(result["inventory_sha256"], self.inventory_sha256)

    def test_accepts_spec_bound_controlled_measurement_worker_probe(self):
        self.make_controlled_worker_probe()
        result = self.validate()
        self.assertTrue(result["accepted"])
        self.assertEqual(result["generated_tokens"], 4)

    def test_spec_bound_probe_rejects_tampered_non_object_or_duplicate_json(self):
        spec_path, _ = self.make_controlled_worker_probe()
        original = spec_path.read_text(encoding="utf-8")

        spec_path.write_text(original + " ", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "spec hash mismatch"):
            self.validate()

        self.rewrite_probe_json(spec_path, "spec_sha256", ["not", "an", "object"])
        with self.assertRaisesRegex(ValueError, "spec must be a JSON object"):
            self.validate()

        duplicate = (
            '{"schema":"official-openvino-wb04-worker-spec/v1",'
            '"controlled_test_id":"WB04-U4-LOAD-PROBE","role":"pilot",'
            '"context":"diagnostic-short-prompt",'
            f'"model_path":{json.dumps(str(self.artifact))},'
            '"device":"CPU","device":"GPU","max_new_tokens":4}'
        )
        spec_path.write_text(duplicate, encoding="utf-8")
        self.manifest["load_probe"]["spec_sha256"] = hashlib.sha256(
            spec_path.read_bytes()
        ).hexdigest()
        with self.assertRaisesRegex(ValueError, "duplicate JSON key"):
            self.validate()

    def test_spec_bound_probe_rejects_command_model_or_device_substitution(self):
        for mutation, message in (
            ("command", "resolved spec path"),
            ("model", "spec model path"),
            ("device", "spec device"),
        ):
            with self.subTest(mutation=mutation):
                spec_path, _ = self.make_controlled_worker_probe()
                spec = json.loads(spec_path.read_text(encoding="utf-8"))
                if mutation == "command":
                    self.manifest["load_probe"]["command"][-1] = spec_path.name
                elif mutation == "model":
                    other_model = self.root / "other-model"
                    other_model.mkdir(exist_ok=True)
                    spec["model_path"] = str(other_model)
                    self.rewrite_probe_json(spec_path, "spec_sha256", spec)
                else:
                    spec["device"] = "GPU"
                    self.rewrite_probe_json(spec_path, "spec_sha256", spec)
                with self.assertRaisesRegex(ValueError, message):
                    self.validate()

    def test_spec_bound_probe_rejects_generation_contract_mismatch(self):
        spec_path, _ = self.make_controlled_worker_probe()
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
        spec["max_new_tokens"] = 3
        self.rewrite_probe_json(spec_path, "spec_sha256", spec)
        with self.assertRaisesRegex(ValueError, "generation contract"):
            self.validate()

    def test_spec_bound_probe_rejects_unbound_or_invalid_attempt_evidence(self):
        for mutation, message in (
            ("missing_hash", "attempt hash"),
            ("invalid", "valid governed run"),
            ("command", "attempt command binding"),
            ("output", "attempt output hash"),
            ("log", "attempt log binding"),
        ):
            with self.subTest(mutation=mutation):
                _, attempt_path = self.make_controlled_worker_probe()
                attempt = json.loads(attempt_path.read_text(encoding="utf-8"))
                if mutation == "missing_hash":
                    self.manifest["load_probe"].pop("attempt_sha256")
                elif mutation == "invalid":
                    attempt["valid"] = False
                    self.rewrite_probe_json(
                        attempt_path, "attempt_sha256", attempt
                    )
                elif mutation == "command":
                    attempt["command"][-1] = "substituted-spec.json"
                    self.rewrite_probe_json(
                        attempt_path, "attempt_sha256", attempt
                    )
                elif mutation == "output":
                    attempt["output_sha256"] = "0" * 64
                    self.rewrite_probe_json(
                        attempt_path, "attempt_sha256", attempt
                    )
                else:
                    self.log.write_text(
                        "substituted controlled worker stdout\n",
                        encoding="utf-8",
                    )
                    self.manifest["load_probe"]["log_sha256"] = hashlib.sha256(
                        self.log.read_bytes()
                    ).hexdigest()
                with self.assertRaisesRegex(ValueError, message):
                    self.validate()

    def test_direct_probe_requires_exact_model_and_cpu_command_options(self):
        for command, message in (
            (
                ["probe", "unrelated", str(self.artifact), "--device", "CPU"],
                "exactly one --model",
            ),
            (
                [
                    "probe",
                    "--model",
                    str(self.artifact),
                    "--device",
                    "CPU",
                    "--device",
                    "CPU",
                ],
                "exactly one --device",
            ),
            (
                ["probe", "--model", str(self.artifact), "--device", "GPU"],
                "device must be CPU",
            ),
        ):
            broken = copy.deepcopy(self.manifest)
            broken["load_probe"]["command"] = command
            with self.subTest(command=command), self.assertRaisesRegex(
                ValueError, message
            ):
                self.validate(broken)

    def test_rejects_missing_tampered_or_uninventoried_required_file(self):
        for mutate, message in (
            (
                lambda manifest: manifest.update(
                    files=[
                        item for item in manifest["files"]
                        if item["path"] != "openvino_tokenizer.bin"
                    ]
                ),
                "required artifact files",
            ),
            (
                lambda manifest: manifest["files"][0].update(sha256="0" * 64),
                "artifact hash",
            ),
            (
                lambda manifest: manifest["files"].append({
                    "path": "../escape.bin",
                    "size_bytes": 1,
                    "sha256": "0" * 64,
                }),
                "within artifact root",
            ),
        ):
            broken = copy.deepcopy(self.manifest)
            mutate(broken)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(broken)

    def test_rejects_precision_label_without_matching_ir_evidence(self):
        for mutate, message in (
            (
                lambda manifest: manifest["precision_proof"].update(
                    element_type="i8"
                ),
                "precision proof",
            ),
            (
                lambda manifest: manifest["precision_proof"].update(
                    element_type_count=2
                ),
                "element type count",
            ),
            (
                lambda manifest: manifest["model"].update(precision="f16"),
                "expected precision",
            ),
        ):
            broken = copy.deepcopy(self.manifest)
            mutate(broken)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(broken)

    def test_rejects_incomplete_provenance_or_license_evidence(self):
        for mutate, message in (
            (
                lambda manifest: manifest["conversion"].update(tool_versions={}),
                "tool versions",
            ),
            (
                lambda manifest: manifest["model"].update(artifact_revision="latest"),
                "artifact revision",
            ),
            (
                lambda manifest: manifest["license"].update(spdx="unknown"),
                "license",
            ),
        ):
            broken = copy.deepcopy(self.manifest)
            mutate(broken)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(broken)

    def test_rejects_unbound_failed_or_fallback_load_probe(self):
        for mutate, message in (
            (
                lambda manifest: manifest["load_probe"].update(
                    artifact_inventory_sha256="0" * 64
                ),
                "inventory",
            ),
            (
                lambda manifest: manifest["load_probe"].update(fallback=True),
                "fallback",
            ),
            (
                lambda manifest: manifest["load_probe"].update(generated_tokens=0),
                "generated tokens",
            ),
            (
                lambda manifest: manifest["load_probe"].update(
                    output_sha256="0" * 64
                ),
                "output hash",
            ),
            (
                lambda manifest: manifest["load_probe"].update(
                    cleanup_process_count=1
                ),
                "cleanup",
            ),
        ):
            broken = copy.deepcopy(self.manifest)
            mutate(broken)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(broken)

    def test_accepts_real_u8_ir_element_type_for_int8_weights(self):
        (self.artifact / "openvino_model.xml").write_text(
            '<net><data element_type="u8"/></net>',
            encoding="utf-8",
        )
        self.refresh_inventory()
        xml_record = next(
            item for item in self.files if item["path"] == "openvino_model.xml"
        )
        self.manifest["model"]["precision"] = "u8"
        self.manifest["precision_proof"].update({
            "element_type": "u8",
            "element_type_count": 1,
            "sha256": xml_record["sha256"],
        })
        result = self.validate(expected_precision="u8")
        self.assertEqual(result["precision"], "u8")

    def test_accepts_local_fp16_inventory_identity_and_external_evidence(self):
        manifest = self.make_local_fp16_manifest()
        result = self.validate(manifest, expected_precision="f16")
        self.assertTrue(result["accepted"])
        self.assertEqual(result["inventory_sha256"], self.inventory_sha256)
        broken = copy.deepcopy(manifest)
        broken["model"]["artifact_revision"] = "0" * 64
        with self.assertRaisesRegex(
            ValueError, "artifact revision.*inventory"
        ):
            self.validate(broken, expected_precision="f16")

    def test_published_artifact_requires_inventoried_readme(self):
        (self.artifact / "README.md").unlink()
        evidence = self.artifact / "PUBLISHED_PROVENANCE.txt"
        evidence.write_text(
            "license: apache-2.0\nExact published conversion evidence.\n",
            encoding="utf-8",
        )
        self.refresh_inventory()
        evidence_record = next(
            item
            for item in self.files
            if item["path"] == "PUBLISHED_PROVENANCE.txt"
        )
        self.manifest["conversion"].update({
            "provenance_path": evidence_record["path"],
            "provenance_sha256": evidence_record["sha256"],
        })
        self.manifest["license"].update({
            "path": evidence_record["path"],
            "sha256": evidence_record["sha256"],
        })
        with self.assertRaisesRegex(ValueError, "required artifact files"):
            self.validate()

    def test_published_artifact_rejects_external_provenance_and_license(self):
        external_provenance = self.root / "published-conversion.json"
        external_provenance.write_text(
            '{"conversion":"published"}',
            encoding="utf-8",
        )
        external_license = self.root / "published-source-README.md"
        external_license.write_text(
            "---\nlicense: apache-2.0\n---\nPublished source.\n",
            encoding="utf-8",
        )
        for mutate, message in (
            (
                lambda manifest: manifest["conversion"].update({
                    "provenance_path": str(external_provenance),
                    "provenance_sha256": hashlib.sha256(
                        external_provenance.read_bytes()
                    ).hexdigest(),
                }),
                "provenance path",
            ),
            (
                lambda manifest: manifest["license"].update({
                    "path": str(external_license),
                    "sha256": hashlib.sha256(
                        external_license.read_bytes()
                    ).hexdigest(),
                }),
                "license path",
            ),
        ):
            broken = copy.deepcopy(self.manifest)
            mutate(broken)
            with self.subTest(message=message), self.assertRaisesRegex(
                ValueError, message
            ):
                self.validate(broken)

    def test_u8_precision_rejects_i8_ir_evidence(self):
        (self.artifact / "openvino_model.xml").write_text(
            '<net><data element_type="i8"/></net>',
            encoding="utf-8",
        )
        self.refresh_inventory()
        xml_record = next(
            item for item in self.files if item["path"] == "openvino_model.xml"
        )
        self.manifest["model"]["precision"] = "u8"
        self.manifest["precision_proof"].update({
            "element_type": "i8",
            "element_type_count": 1,
            "sha256": xml_record["sha256"],
        })
        with self.assertRaisesRegex(ValueError, "precision proof"):
            self.validate(expected_precision="u8")

    def test_local_external_evidence_rejects_hash_mismatches(self):
        manifest = self.make_local_fp16_manifest()
        for mutate, message in (
            (
                lambda value: value["conversion"].update(
                    provenance_sha256="0" * 64
                ),
                "conversion provenance hash mismatch",
            ),
            (
                lambda value: value["license"].update(sha256="0" * 64),
                "license hash mismatch",
            ),
        ):
            broken = copy.deepcopy(manifest)
            mutate(broken)
            with self.subTest(message=message), self.assertRaisesRegex(
                ValueError, message
            ):
                self.validate(broken, expected_precision="f16")

    def test_accepts_local_u4_conversion_with_hash_bound_evidence(self):
        manifest = copy.deepcopy(self.manifest)
        manifest["model"].update({
            "artifact_repository": "local-conversion",
            "artifact_revision": self.inventory_sha256,
        })
        manifest["conversion"]["kind"] = "local-conversion"
        result = self.validate(manifest, expected_precision="u4")
        self.assertTrue(result["accepted"])
        self.assertEqual(result["precision"], "u4")


if __name__ == "__main__":
    unittest.main()
