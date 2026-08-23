from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = REPOSITORY_ROOT / ".github/workflows/workbook-05-documented-build.yml"


class DocumentedBuildExactHeadContractTests(unittest.TestCase):
    """Keep every Phase 2 checkout tied to the immutable reviewed branch head."""

    def test_shared_checkout_mapping_uses_exact_pull_request_head(self) -> None:
        workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

        # A pull_request workflow otherwise checks out GitHub's synthetic merge
        # commit by default. Phase 2 requires the literal reviewed head SHA, while
        # workflow_dispatch should fall back to the dispatched github.sha.
        self.assertIn(
            "ref: ${{ github.event.pull_request.head.sha || github.sha }}",
            workflow,
        )

        # All collector/validator checkouts reuse the same YAML mapping, so one
        # reviewed ref declaration must sit inside the shared sparse-checkout map.
        anchor_position = workflow.index("with: &sparse-checkout")
        ref_position = workflow.index(
            "ref: ${{ github.event.pull_request.head.sha || github.sha }}"
        )
        sparse_position = workflow.index("sparse-checkout: |", anchor_position)
        self.assertGreater(ref_position, anchor_position)
        self.assertLess(ref_position, sparse_position)


if __name__ == "__main__":
    unittest.main()
