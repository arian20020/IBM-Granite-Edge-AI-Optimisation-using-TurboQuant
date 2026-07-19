import unittest

from scripts.testing.official_openvino.conversion import (
    ConversionSpec,
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


if __name__ == "__main__":
    unittest.main()
