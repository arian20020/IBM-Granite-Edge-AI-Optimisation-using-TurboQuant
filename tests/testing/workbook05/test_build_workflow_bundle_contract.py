from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = REPOSITORY_ROOT / ".github/workflows/workbook-05-documented-build.yml"
METADATA_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Write-Workbook05BuildBundleMetadata.ps1"
)


class DocumentedBuildBundleWorkflowContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.metadata = METADATA_PATH.read_text(encoding="utf-8")

    def test_every_collector_binds_bundle_json_to_run_id_and_attempt(self) -> None:
        self.assertGreaterEqual(
            self.workflow.count("Write-Workbook05BuildBundleMetadata.ps1"),
            4,
        )
        self.assertGreaterEqual(self.workflow.count("bundle.json"), 4)
        self.assertIn("github.run_id", self.workflow)
        self.assertIn("github.run_attempt", self.workflow)

    def test_route_b_metadata_requires_accepted_digest_bound_br8_candidate(self) -> None:
        for token in (
            "br8_prerequisite",
            "accepted = $Br8Accepted",
            "status = $Br8Status",
            "source_commit = $Br8SourceCommit",
            "artifact_digest = $Br8ArtifactDigest",
            "ExecutableCandidate",
            "^sha256:[0-9a-f]{64}$",
            "Write-Wb05Manifest",
        ):
            with self.subTest(token=token):
                self.assertIn(token, self.metadata)


if __name__ == "__main__":
    unittest.main()
