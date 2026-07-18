import tempfile
import unittest
from pathlib import Path


class AnimehackerSourceAuditTests(unittest.TestCase):
    def test_readme_or_flag_strings_do_not_prove_implementation(self):
        from scripts.testing.audit_animehacker_source import audit_source

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "README.md").write_text(
                "Use --cache-type-k tq3_0 for complete TurboQuant support", encoding="utf-8"
            )
            result = audit_source(root)
            self.assertFalse(result["tq3_0"]["implemented"])
            self.assertEqual(result["tq3_0"]["implementation_depth"], "documentation-only")

    def test_type_registration_and_cpu_functions_prove_cpu_implementation(self):
        from scripts.testing.audit_animehacker_source import audit_source

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write(root, "ggml/include/ggml.h", "GGML_TYPE_TQ3_0 = 41")
            self._write(
                root,
                "ggml/src/quants.c",
                "quantize_row_tq3_0_ref(); dequantize_row_tq3_0();",
            )
            self._write(root, "ggml/src/ggml.c", 'type_name = "tq3_0";')
            result = audit_source(root)
            self.assertTrue(result["tq3_0"]["implemented"])
            self.assertIn("cpu", result["tq3_0"]["implementation_backends"])
            self.assertGreaterEqual(len(result["tq3_0"]["source_evidence"]), 3)

    def test_backend_support_requires_tq3_specific_backend_code(self):
        from scripts.testing.audit_animehacker_source import audit_source

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write(root, "ggml/include/ggml.h", "GGML_TYPE_TQ3_0 = 41")
            self._write(root, "ggml/src/quants.c", "quantize_row_tq3_0_ref(); dequantize_row_tq3_0();")
            self._write(root, "ggml/src/ggml.c", 'type_name = "tq3_0";')
            self._write(root, "ggml/src/ggml-sycl/backend.cpp", "SYCL Level Zero backend")
            result = audit_source(root)
            self.assertFalse(result["backends"]["sycl"]["tq3_0_specific"])

            self._write(
                root,
                "ggml/src/ggml-sycl/tq3.cpp",
                "GGML_TYPE_TQ3_0 quantize_row_tq3_0 dequantize_row_tq3_0",
            )
            result = audit_source(root)
            self.assertTrue(result["backends"]["sycl"]["tq3_0_specific"])

    def test_qjl_is_not_claimed_from_comments_or_struct_field_names(self):
        from scripts.testing.audit_animehacker_source import audit_source

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write(
                root,
                "ggml/src/ggml-common.h",
                "// QJL residual correction\nuint8_t qr[4];\nggml_half gamma;",
            )
            result = audit_source(root)
            self.assertEqual(result["qjl"]["status"], "not-proven")

    def test_limitations_report_is_explicit_about_unproven_routes(self):
        from scripts.testing.audit_animehacker_source import render_limitations

        audit = {
            "tq3_0": {"implementation_depth": "source-implemented"},
            "qjl": {"status": "not-proven"},
            "backends": {
                "sycl": {"tq3_0_specific": True},
                "vulkan": {"tq3_0_specific": False},
            },
        }
        report = render_limitations(audit)
        self.assertIn("QJL: not proven", report)
        self.assertIn("Vulkan TQ3_0: not proven", report)
        self.assertIn("runtime verification is still required", report)

    def _write(self, root: Path, relative: str, text: str) -> None:
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
