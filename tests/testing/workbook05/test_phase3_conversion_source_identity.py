from __future__ import annotations

import unittest

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    ConversionPackage,
    _package_records,
)


def _packages(
    *,
    optimum_intel_version: str,
) -> tuple[ConversionPackage, ...]:
    """Return the complete reviewed conversion package catalogue."""

    return (
        ConversionPackage(
            name="optimum-intel",
            version=optimum_intel_version,
            source_identity=OPTIMUM_INTEL_COMMIT,
        ),
        ConversionPackage(
            name="optimum",
            version="2.3.0",
            source_identity=OPTIMUM_COMMIT,
        ),
        ConversionPackage(
            name="transformers",
            version="5.5.0",
            source_identity="sha256:" + "1" * 64,
        ),
        ConversionPackage(
            name="huggingface-hub",
            version="1.21.0",
            source_identity="sha256:" + "2" * 64,
        ),
        ConversionPackage(
            name="nncf",
            version="3.2.0",
            source_identity="sha256:" + "3" * 64,
        ),
        ConversionPackage(
            name="openvino",
            version="2026.2.1",
            source_identity="sha256:" + "4" * 64,
        ),
        ConversionPackage(
            name="openvino-tokenizers",
            version="2026.2.1.0",
            source_identity="sha256:" + "5" * 64,
        ),
    )


class ConversionSourceIdentityRegressionTests(unittest.TestCase):
    """Keep conversion evidence aligned with the exact pinned source version."""

    def test_exact_optimum_intel_source_version_is_accepted(self) -> None:
        records = _package_records(
            _packages(optimum_intel_version="2.2.0.dev0")
        )

        optimum_intel = next(
            record
            for record in records
            if record["name"] == "optimum-intel"
        )
        self.assertEqual("2.2.0.dev0", optimum_intel["version"])
        self.assertEqual(
            OPTIMUM_INTEL_COMMIT,
            optimum_intel["source_identity"],
        )

    def test_matching_git_derived_version_suffix_is_accepted(self) -> None:
        prefix = OPTIMUM_INTEL_COMMIT[:12]
        records = _package_records(
            _packages(optimum_intel_version=f"2.2.0.dev0+{prefix}")
        )

        optimum_intel = next(
            record
            for record in records
            if record["name"] == "optimum-intel"
        )
        self.assertEqual(
            f"2.2.0.dev0+{prefix}",
            optimum_intel["version"],
        )

    def test_unrelated_git_derived_version_suffix_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "reviewed version"):
            _package_records(
                _packages(optimum_intel_version="2.2.0.dev0+deadbee")
            )

    def test_superseded_optimum_intel_version_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "reviewed version"):
            _package_records(
                _packages(optimum_intel_version="2.3.0.dev0")
            )


if __name__ == "__main__":
    unittest.main()
