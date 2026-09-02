from __future__ import annotations

import unittest
from pathlib import PureWindowsPath

from scripts.testing.workbook05.workspace_policy import evaluate_workspace_path


class WorkspacePolicyTests(unittest.TestCase):
    def test_controlled_root_is_allowed(self) -> None:
        decision = evaluate_workspace_path(PureWindowsPath(r"C:\wb05"))

        self.assertTrue(decision.permitted)
        self.assertEqual(r"C:\wb05", decision.canonical_path)
        self.assertEqual((), decision.reasons)

    def test_child_path_is_allowed_and_casing_is_normalised(self) -> None:
        decision = evaluate_workspace_path(
            PureWindowsPath(r"c:\WB05\source\route-a\openvino")
        )

        self.assertTrue(decision.permitted)
        self.assertEqual(
            r"C:\wb05\source\route-a\openvino",
            decision.canonical_path,
        )

    def test_parent_traversal_is_rejected_before_normalisation(self) -> None:
        decision = evaluate_workspace_path(
            PureWindowsPath(r"C:\wb05\source\..\..\escape")
        )

        self.assertFalse(decision.permitted)
        self.assertIn("parent traversal", " ".join(decision.reasons).lower())

    def test_other_drive_is_rejected(self) -> None:
        decision = evaluate_workspace_path(PureWindowsPath(r"D:\wb05"))

        self.assertFalse(decision.permitted)
        self.assertIn("drive c:", " ".join(decision.reasons).lower())

    def test_similar_prefix_is_not_a_child(self) -> None:
        decision = evaluate_workspace_path(PureWindowsPath(r"C:\wb05-escape"))

        self.assertFalse(decision.permitted)

    def test_unc_path_is_rejected(self) -> None:
        decision = evaluate_workspace_path(
            PureWindowsPath(r"\\server\share\wb05")
        )

        self.assertFalse(decision.permitted)
        self.assertIn("unc", " ".join(decision.reasons).lower())

    def test_device_path_is_rejected(self) -> None:
        decision = evaluate_workspace_path(
            PureWindowsPath(r"\\?\C:\wb05")
        )

        self.assertFalse(decision.permitted)
        self.assertIn("device", " ".join(decision.reasons).lower())

    def test_relative_path_is_rejected(self) -> None:
        decision = evaluate_workspace_path(PureWindowsPath(r"wb05\source"))

        self.assertFalse(decision.permitted)
        self.assertIn("absolute", " ".join(decision.reasons).lower())


if __name__ == "__main__":
    unittest.main()
