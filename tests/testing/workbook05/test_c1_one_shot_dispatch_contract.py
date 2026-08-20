"""Contracts for the temporary, exact-main C1 workflow dispatcher.

This test is intentionally added before the dispatcher implementation.  The first
CI run must therefore fail because the reviewed workflow and trigger record do not
yet exist.  The implementation commit may add only those two control-plane files.
"""

from __future__ import annotations

import json
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github/workflows/workbook-05-c1-one-shot-dispatch.yml"
)
TRIGGER_PATH = (
    REPOSITORY_ROOT
    / ".github/workbook05-c1-live-trigger.json"
)

EXPECTED_REPOSITORY = (
    "arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant"
)
EXPECTED_MAIN_PARENT = "1dc0a0480f3d8ead58dd6c0430b10aa8a2bb305f"
EXPECTED_DECISION_SHA256 = (
    "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49"
)
EXPECTED_NONCE = "wb05-c1-live-20260820-a"


class C1OneShotDispatchContractTests(unittest.TestCase):
    """Require one tightly bounded automatic dispatch from exact main."""

    def test_trigger_record_is_exact_and_owner_authorised(self) -> None:
        """The dispatcher must consume one immutable, reviewable authority record."""

        self.assertTrue(
            TRIGGER_PATH.is_file(),
            "The one-shot live C1 trigger record has not been implemented.",
        )
        payload = json.loads(TRIGGER_PATH.read_text(encoding="utf-8"))
        self.assertEqual(
            {
                "schema_version": "1.0",
                "record_type": "workbook05-c1-one-shot-dispatch",
                "repository": EXPECTED_REPOSITORY,
                "authorised_main_parent": EXPECTED_MAIN_PARENT,
                "operation": "live-asset-lock",
                "confirm_live_asset_lock": True,
                "accepted_dependency_preflight_sha256": (
                    EXPECTED_DECISION_SHA256
                ),
                "one_time_nonce": EXPECTED_NONCE,
                "owner_authorised": True,
            },
            payload,
        )

    def test_workflow_is_main_only_path_scoped_and_not_manually_reusable(self) -> None:
        """Adding the trigger to main must be the only event that can run it."""

        self.assertTrue(
            WORKFLOW_PATH.is_file(),
            "The one-shot live C1 dispatcher workflow has not been implemented.",
        )
        text = WORKFLOW_PATH.read_text(encoding="utf-8")

        required_fragments = (
            "name: Workbook 05 C1 one-shot dispatcher",
            "branches:\n      - main",
            "paths:\n      - '.github/workbook05-c1-live-trigger.json'",
            "actions: write",
            "contents: read",
            "cancel-in-progress: false",
            f"github.repository == '{EXPECTED_REPOSITORY}'",
            "persist-credentials: false",
            "fetch-depth: 2",
        )
        for fragment in required_fragments:
            self.assertIn(fragment, text)

        self.assertNotIn("workflow_dispatch:", text)
        self.assertNotIn("pull_request:", text)
        self.assertNotIn("schedule:", text)

    def test_workflow_validates_identity_before_dispatching_exact_inputs(self) -> None:
        """The GitHub API request must be data-bound, not a broad reusable hook."""

        self.assertTrue(WORKFLOW_PATH.is_file())
        text = WORKFLOW_PATH.read_text(encoding="utf-8")

        required_fragments = (
            EXPECTED_MAIN_PARENT,
            EXPECTED_DECISION_SHA256,
            EXPECTED_NONCE,
            "workbook-05-phase3-assets.yml/dispatches",
            "inputs[operation]=live-asset-lock",
            "inputs[confirm_live_asset_lock]=true",
            (
                "inputs[accepted_dependency_preflight_sha256]="
                + EXPECTED_DECISION_SHA256
            ),
            "GH_TOKEN: ${{ github.token }}",
            "git rev-parse HEAD^1",
        )
        for fragment in required_fragments:
            self.assertIn(fragment, text)

        # The dispatcher may start the existing reviewed workflow only.  It must
        # not download a model, invoke Python, or run the C1 scripts itself.
        forbidden_fragments = (
            "huggingface",
            "optimum-cli",
            "openvino",
            "Invoke-Workbook05Phase3AssetLockLive.ps1",
            "Remove-Item -Recurse",
        )
        lowered = text.casefold()
        for fragment in forbidden_fragments:
            self.assertNotIn(fragment.casefold(), lowered)


if __name__ == "__main__":
    unittest.main()
