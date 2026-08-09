from __future__ import annotations

import json
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
EXPECTED_RUNTIME_COMMIT = "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
EXPECTED_GENAI_COMMIT = "bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0"
INCOMPATIBLE_GENAI_COMMIT = "05e5c7670b597746f858946974d11f38e3baf42f"

# These are the active executable or machine-readable pin surfaces. Historical
# plans and specifications are intentionally excluded because they remain an
# audit trail of the earlier, disproved compatibility assumption.
ACTIVE_GENAI_PIN_FILES = (
    Path("scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1"),
    Path(".github/workflows/workbook-05-documented-build.yml"),
    Path(
        "experiments/granite_turboquant_intel/configurations/workbook05/"
        "source-admission-settings.json"
    ),
    Path(
        "experiments/granite_turboquant_intel/configurations/workbook05/"
        "pinned-document-sources.json"
    ),
    Path(
        "experiments/granite_turboquant_intel/manifests/campaigns/"
        "GTQ-WB05-MF-v1/route-a-source-admission.json"
    ),
    Path("scripts/testing/workbook05/source_admission_bundle_validation.py"),
)


class RouteAGenAICompatibilityContractTests(unittest.TestCase):
    """Freeze the source-matched Runtime/GenAI handoff across active controls."""

    def test_every_active_surface_uses_the_source_matched_genai_commit(self) -> None:
        for relative_path in ACTIVE_GENAI_PIN_FILES:
            with self.subTest(path=str(relative_path)):
                text = (REPOSITORY_ROOT / relative_path).read_text(encoding="utf-8")
                self.assertIn(EXPECTED_GENAI_COMMIT, text)
                self.assertNotIn(INCOMPATIBLE_GENAI_COMMIT, text)

    def test_runtime_pin_remains_the_accepted_2026_3_source(self) -> None:
        # The repair changes only the GenAI compatibility candidate. The already
        # accepted Runtime source must not drift while fixing the upper layer.
        settings_path = (
            REPOSITORY_ROOT
            / "experiments/granite_turboquant_intel/configurations/workbook05/"
            "source-admission-settings.json"
        )
        settings = json.loads(settings_path.read_text(encoding="utf-8"))
        route_a = settings["routes"]["route-a-merged-openvino"]

        self.assertEqual(EXPECTED_RUNTIME_COMMIT, route_a["runtime"]["commit"])
        self.assertEqual(EXPECTED_GENAI_COMMIT, route_a["genai"]["commit"])

    def test_genai_documents_are_pinned_to_the_same_candidate(self) -> None:
        # README and BUILD.md must describe the exact source that the build script
        # fetches; otherwise instructions and executable provenance can diverge.
        document_path = (
            REPOSITORY_ROOT
            / "experiments/granite_turboquant_intel/configurations/workbook05/"
            "pinned-document-sources.json"
        )
        document_config = json.loads(document_path.read_text(encoding="utf-8"))
        genai_documents = [
            document
            for document in document_config["documents"]
            if document["repository_full_name"] == "openvinotoolkit/openvino.genai"
        ]

        self.assertEqual(2, len(genai_documents))
        self.assertEqual(
            {EXPECTED_GENAI_COMMIT},
            {document["commit"] for document in genai_documents},
        )


if __name__ == "__main__":
    unittest.main()
