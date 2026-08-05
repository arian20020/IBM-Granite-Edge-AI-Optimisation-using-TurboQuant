from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
WORKFLOW = ROOT / ".github/workflows/workbook-05-preflight.yml"


class WorkflowContractTests(unittest.TestCase):
    def test_workflow_is_read_only_and_targets_the_exact_runner(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("contents: read", text)
        self.assertIn("actions: read", text)
        self.assertNotIn("contents: write", text)
        self.assertNotIn("pull-requests: write", text)
        for label in (
            "self-hosted",
            "Windows",
            "X64",
            "workbook05",
            "intel-target",
        ):
            self.assertIn(f"- {label}", text)
        self.assertIn("testing/workbook-05-two-route-memory-frontier", text)

    def test_actions_are_pinned_to_immutable_shas(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(
            "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
            text,
        )
        self.assertIn(
            "actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f",
            text,
        )
        self.assertIn(
            "actions/download-artifact@974686ed5098c7f9c9289ec946b9058e496a2561",
            text,
        )
        self.assertIn(
            "actions/setup-python@bfe8cc55a7890e3d6672eda6460ef37bfcc70755",
            text,
        )

    def test_pull_request_paths_cover_controlled_document_inputs(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")

        # The test suite reads these documents directly. A pull request that
        # changes one of them must therefore run the same preflight gate rather
        # than bypassing validation because only Python files changed earlier.
        required_paths = (
            "docs/superpowers/plans/2026-08-03-workbook-05-preflight-scaffolding.md",
            "docs/superpowers/specs/2026-08-03-workbook-05-two-route-memory-frontier-design.md",
            "docs/testing/**",
            "experiments/granite_turboquant_intel/manifests/templates/workbook05/**",
        )
        for required_path in required_paths:
            self.assertIn(
                f"- '{required_path}'",
                text,
                f"Pull requests changing {required_path} must run the preflight.",
            )

    def test_manual_dispatch_is_limited_to_reviewed_refs(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")

        # A read-only repository token does not make arbitrary branch code safe
        # on a self-hosted machine. Manual execution is therefore restricted to
        # main or the exact reviewed campaign branch.
        self.assertIn("github.event_name == 'workflow_dispatch' &&", text)
        self.assertIn("github.ref == 'refs/heads/main'", text)
        self.assertIn(
            "github.ref == 'refs/heads/testing/workbook-05-two-route-memory-frontier'",
            text,
        )
        self.assertNotIn("github.event_name == 'workflow_dispatch' ||", text)

    def test_each_checkout_limits_the_windows_worktree_to_preflight_inputs(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")
        checkout_action = (
            "uses: actions/checkout@"
            "9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0"
        )

        # Both the Intel collection job and the hosted validation job run on
        # Windows. Splitting at the pinned action lets this test inspect each
        # checkout step independently rather than accepting one safe checkout
        # and one accidental full-repository checkout.
        checkout_sections = text.split(checkout_action)[1:]
        self.assertEqual(2, len(checkout_sections))

        # These are the complete repository inputs consumed by the workflow's
        # scripts and tests. Deep research and historical log trees are omitted
        # because their paths can exceed the legacy Windows path boundary.
        required_sparse_roots = (
            ".github/workflows",
            "docs/superpowers",
            "docs/testing",
            "experiments/granite_turboquant_intel/configurations/workbook05",
            "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1",
            "experiments/granite_turboquant_intel/manifests/templates/workbook05",
            "experiments/granite_turboquant_intel/schemas/workbook05",
            "scripts/testing",
            "tests/testing/workbook05",
        )

        for checkout_number, checkout_section in enumerate(
            checkout_sections,
            start=1,
        ):
            # Stop at the following workflow step so required settings and paths
            # must belong to this checkout rather than appearing elsewhere.
            checkout_step = checkout_section.split("\n      - name:", 1)[0]
            self.assertIn(
                "sparse-checkout: |",
                checkout_step,
                f"Checkout {checkout_number} must use sparse checkout.",
            )
            self.assertIn(
                "persist-credentials: false",
                checkout_step,
                (
                    f"Checkout {checkout_number} must not retain the job token "
                    "in Git configuration."
                ),
            )
            for required_root in required_sparse_roots:
                self.assertIn(
                    required_root,
                    checkout_step,
                    f"Checkout {checkout_number} is missing {required_root}.",
                )

    def test_workflow_contains_no_repository_write_command(self) -> None:
        lowered = WORKFLOW.read_text(encoding="utf-8").lower()
        for forbidden in (
            "git push",
            "gh pr create",
            "gh pr edit",
            "create-pull-request",
        ):
            self.assertNotIn(forbidden, lowered)


if __name__ == "__main__":
    unittest.main()
