from __future__ import annotations

import unittest

from scripts.testing.workbook05.phase3 import conversion


_EXPECTED_OPTIMUM_CONSTRAINTS: tuple[str, ...] = (
    "transformers>=4.29",
    "torch>=1.11",
    "packaging",
    "numpy",
    "huggingface_hub>=0.8.0",
)

_EXPECTED_NORMAL_REQUIREMENT_INPUT: tuple[str, ...] = (
    "transformers==5.5.0",
    "huggingface-hub==1.21.0",
    "nncf==3.2.0",
    "openvino==2026.2.1",
    "openvino-tokenizers==2026.2.1.0",
    "torch>=2.1",
    "safetensors<0.8.0",
    "setuptools",
    "requests>=2.33,<3.0",
    "packaging",
    "numpy",
    "wheel",
)


class ConversionRequirementCatalogueTests(unittest.TestCase):
    """Keep acquisition and conversion on one reviewed requirement catalogue."""

    def test_conversion_exports_exact_optimum_source_constraints(self) -> None:
        self.assertEqual(
            _EXPECTED_OPTIMUM_CONSTRAINTS,
            getattr(conversion, "REVIEWED_OPTIMUM_CONSTRAINTS", None),
        )

    def test_conversion_exports_complete_normal_requirement_input(self) -> None:
        self.assertEqual(
            _EXPECTED_NORMAL_REQUIREMENT_INPUT,
            getattr(conversion, "REVIEWED_NORMAL_REQUIREMENT_INPUT", None),
        )


if __name__ == "__main__":
    unittest.main()
