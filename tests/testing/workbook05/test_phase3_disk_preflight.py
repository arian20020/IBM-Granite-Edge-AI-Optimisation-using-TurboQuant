from __future__ import annotations

import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from scripts.testing.workbook05.phase3.disk_preflight import (
    MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B,
    collect_disk_preflight,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SETTINGS_PATH = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "configurations"
    / "workbook05"
    / "phase3-asset-lock-settings.json"
)


class Phase3DiskPreflightTests(unittest.TestCase):
    """Prove that C1 records disk state without deleting any workspace."""

    @staticmethod
    def _make_roots(base: Path) -> tuple[Path, Path, Path, Path]:
        """Create the four controlled roots used by the preflight tests."""

        accepted_root = base / "w5a"
        model_root = base / "w5m"
        probe_root = base / "w5c"
        run_root = base / "w5r"
        for root in (accepted_root, model_root, probe_root, run_root):
            root.mkdir()
        return accepted_root, model_root, probe_root, run_root

    def test_granite_download_is_blocked_below_fifty_gib(self) -> None:
        """One byte below the approved threshold must fail closed."""

        with TemporaryDirectory() as directory:
            _, model_root, probe_root, run_root = self._make_roots(Path(directory))
            result = collect_disk_preflight(
                model_root=model_root,
                probe_root=probe_root,
                run_root=run_root,
                free_bytes=MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B - 1,
            )

        self.assertEqual("Blocked", result["status"])
        self.assertIn("DISK_BELOW_50_GIB", result["failure_ids"])

    def test_exact_threshold_passes_without_authorising_deletion(self) -> None:
        """Exactly 50 GiB is sufficient, but cleanup remains forbidden."""

        with TemporaryDirectory() as directory:
            _, model_root, probe_root, run_root = self._make_roots(Path(directory))
            result = collect_disk_preflight(
                model_root=model_root,
                probe_root=probe_root,
                run_root=run_root,
                free_bytes=MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B,
            )

        self.assertEqual("Passed", result["status"])
        self.assertEqual([], result["failure_ids"])
        self.assertFalse(result["deletion_authorised"])
        self.assertFalse(result["deletion_performed"])

    def test_disk_preflight_does_not_authorise_model_download(self) -> None:
        """Passing one preflight must not authorise the multi-gate acquisition."""

        with TemporaryDirectory() as directory:
            _, model_root, probe_root, run_root = self._make_roots(Path(directory))
            result = collect_disk_preflight(
                model_root=model_root,
                probe_root=probe_root,
                run_root=run_root,
                free_bytes=MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B,
            )

        self.assertNotIn("granite_3b_download_authorised", result)
        self.assertFalse(result["granite_8b_download_authorised"])

    def test_inventory_records_file_counts_and_total_bytes_for_all_four_roots(self) -> None:
        """The evidence must inventory accepted, model, probe and run storage."""

        with TemporaryDirectory() as directory:
            accepted_root, model_root, probe_root, run_root = self._make_roots(
                Path(directory)
            )
            (accepted_root / "runtime.bin").write_bytes(b"AA")
            (model_root / "model.bin").write_bytes(b"BBB")
            nested_probe = probe_root / "nested"
            nested_probe.mkdir()
            (nested_probe / "probe.log").write_bytes(b"CCCC")
            (run_root / "run.json").write_bytes(b"DDDDD")

            result = collect_disk_preflight(
                model_root=model_root,
                probe_root=probe_root,
                run_root=run_root,
                free_bytes=MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B,
            )

        inventories = result["inventories"]
        self.assertEqual(1, inventories["accepted_root"]["file_count"])
        self.assertEqual(2, inventories["accepted_root"]["total_bytes"])
        self.assertEqual(1, inventories["model_root"]["file_count"])
        self.assertEqual(3, inventories["model_root"]["total_bytes"])
        self.assertEqual(1, inventories["probe_root"]["file_count"])
        self.assertEqual(4, inventories["probe_root"]["total_bytes"])
        self.assertEqual(1, inventories["run_root"]["file_count"])
        self.assertEqual(5, inventories["run_root"]["total_bytes"])

    def test_run_identity_workspace_is_listed_but_never_deleted(self) -> None:
        """A run-shaped directory is evidence for review, not cleanup authority."""

        with TemporaryDirectory() as directory:
            _, model_root, probe_root, run_root = self._make_roots(Path(directory))
            candidate = probe_root / "phase2-31661571860-1"
            candidate.mkdir()
            (candidate / "record.json").write_text("{}", encoding="utf-8")
            ordinary = probe_root / "notes"
            ordinary.mkdir()

            result = collect_disk_preflight(
                model_root=model_root,
                probe_root=probe_root,
                run_root=run_root,
                free_bytes=MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B,
            )

            # Check existence before TemporaryDirectory removes the test fixture.
            self.assertTrue(candidate.is_dir())
            self.assertTrue((candidate / "record.json").is_file())
            self.assertTrue(ordinary.is_dir())

        candidates = result["candidate_stale_workspaces"]
        self.assertEqual(1, len(candidates))
        self.assertEqual("31661571860", candidates[0]["run_id"])
        self.assertEqual(1, candidates[0]["run_attempt"])
        self.assertEqual("probe_root", candidates[0]["root_role"])
        self.assertTrue(candidates[0]["owner_review_required"])
        self.assertFalse(candidates[0]["automatic_deletion_authorised"])

    def test_missing_roots_are_recorded_without_being_invented(self) -> None:
        """Absent future roots remain explicit zero-sized observations."""

        with TemporaryDirectory() as directory:
            base = Path(directory)
            accepted_root = base / "w5a"
            accepted_root.mkdir()
            model_root = base / "w5m"
            probe_root = base / "w5c"
            run_root = base / "w5r"

            result = collect_disk_preflight(
                model_root=model_root,
                probe_root=probe_root,
                run_root=run_root,
                free_bytes=MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B,
            )

        self.assertTrue(result["inventories"]["accepted_root"]["exists"])
        for role in ("model_root", "probe_root", "run_root"):
            with self.subTest(role=role):
                self.assertFalse(result["inventories"][role]["exists"])
                self.assertEqual(0, result["inventories"][role]["file_count"])
                self.assertEqual(0, result["inventories"][role]["total_bytes"])

    def test_settings_freeze_exact_roots_threshold_and_model_scope(self) -> None:
        """The repository settings must preserve the approved C1 boundary."""

        settings = json.loads(SETTINGS_PATH.read_text(encoding="utf-8-sig"))
        self.assertEqual(
            {
                "schema_version": "1.0",
                "campaign_id": "GTQ-WB05-MF-v1",
                "model_root": r"C:\w5m",
                "probe_root": r"C:\w5c",
                "run_root": r"C:\w5r",
                "minimum_free_bytes_before_granite_3b": 53687091200,
                "granite_3b_repository": "ibm-granite/granite-4.1-3b",
                "granite_8b_repository": "ibm-granite/granite-4.1-8b",
                "granite_8b_download_authorised": False,
            },
            settings,
        )


if __name__ == "__main__":
    unittest.main()
