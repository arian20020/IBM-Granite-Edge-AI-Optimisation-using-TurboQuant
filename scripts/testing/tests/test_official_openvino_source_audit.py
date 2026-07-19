import tempfile
import unittest
from pathlib import Path

from scripts.testing.official_openvino.source_audit import (
    CodecBoundary,
    audit_codec_boundary,
    classify_activation,
)


class OfficialOpenVINOSourceAuditTests(unittest.TestCase):
    def test_turbo_claim_requires_enum_properties_and_runtime_allocation(self):
        source = CodecBoundary(
            turbo_enum=True,
            key_property=True,
            value_property=True,
            u3_precision=True,
            u4_precision=True,
            norm_switch=True,
            packed_storage=True,
            cpu_sdpa_path=True,
            qjl_implementation=False,
            polar_implementation=False,
            matches=(),
        )
        runtime = {"accepted": True, "allocation_proven": False,
                   "actual_device": "CPU", "fallback": False}
        self.assertFalse(classify_activation(source, runtime).activated)
        runtime["allocation_proven"] = True
        self.assertTrue(classify_activation(source, runtime).activated)

    def test_independent_controls_norm_packing_and_cpu_path_are_required(self):
        complete = dict(turbo_enum=True, key_property=True, value_property=True,
                        u3_precision=True, u4_precision=True, norm_switch=True,
                        packed_storage=True, cpu_sdpa_path=True,
                        qjl_implementation=False, polar_implementation=False,
                        matches=())
        for field in ("key_property", "value_property", "norm_switch",
                      "packed_storage", "cpu_sdpa_path"):
            broken = dict(complete); broken[field] = False
            with self.subTest(field=field):
                self.assertFalse(classify_activation(
                    CodecBoundary(**broken),
                    {"accepted": True, "allocation_proven": True,
                     "actual_device": "CPU", "fallback": False},
                ).activated)

    def test_qjl_and_polar_names_outside_cache_codec_do_not_prove_support(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "unrelated.py").write_text(
                "def polar_complex_operator(): pass\n# QJL mentioned in a comment\n",
                encoding="utf-8",
            )
            result = audit_codec_boundary([root])
        self.assertFalse(result.qjl_implementation)
        self.assertFalse(result.polar_implementation)

    def test_gpu_fallback_cannot_be_classified_as_gpu_activation(self):
        source = CodecBoundary(True, True, True, True, True, True, True, True,
                               False, False, ())
        activation = classify_activation(
            source,
            {"accepted": True, "allocation_proven": True,
             "actual_device": "CPU", "requested_device": "GPU", "fallback": True},
        )
        self.assertFalse(activation.activated)
        self.assertEqual(activation.classification, "fallback-not-activated")


if __name__ == "__main__":
    unittest.main()
