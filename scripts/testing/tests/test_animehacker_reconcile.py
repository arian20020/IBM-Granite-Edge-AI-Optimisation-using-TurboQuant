import unittest
import json
import tempfile
import shutil
import subprocess
import sys
from pathlib import Path


class AnimehackerReconcileTests(unittest.TestCase):
    @staticmethod
    def _repo_root():
        return Path(__file__).resolve().parents[3]

    def _recovery_fixture(self):
        temporary = tempfile.TemporaryDirectory()
        root = Path(temporary.name)
        source = self._repo_root() / "experiments/raw-results/animehacker-tq3-0/2026-07-18"
        runtime = root / "runtime"
        quality = root / "quality"
        shutil.copytree(source / "runtime-recovery/AH-09", runtime / "AH-09")
        shutil.copytree(source / "runtime-recovery/AH-10", runtime / "AH-10")
        shutil.copytree(source / "quality-recovery/AH-09", quality / "AH-09")
        adjudication = root / "quality-adjudications.json"
        shutil.copy2(source / "quality-recovery/quality-adjudications.json", adjudication)
        workbook = (self._repo_root() / "docs/testing/workbooks/text-templates/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md").read_text(encoding="utf-8")
        return temporary, runtime, quality, adjudication, workbook

    @staticmethod
    def _mutate_json(path, mutation):
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
        mutation(payload)
        path.write_text(json.dumps(payload), encoding="utf-8")

    def test_rejects_blank_and_literal_na_cells(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_markdown

        with self.assertRaisesRegex(ValueError, "blank workbook"):
            validate_markdown("| Field |  |")
        with self.assertRaisesRegex(ValueError, "literal N/A"):
            validate_markdown("| Field | N/A |")

    def test_requires_every_controlled_id(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_markdown

        with self.assertRaisesRegex(ValueError, "missing test id"):
            validate_markdown("| Field | Filled |")

    def test_recovered_ah09_requires_three_samples_and_six_quality_records(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            runtime = root / "runtime"; quality = root / "quality"
            (runtime / "AH-09").mkdir(parents=True)
            (quality / "AH-09").mkdir(parents=True)
            (runtime / "AH-09" / "summary.json").write_text(
                json.dumps({"samples": [{"valid": True}] * 2}), encoding="utf-8")
            adjudication = root / "quality-adjudications.json"
            adjudication.write_text(json.dumps({"AH-09": {"prompts": {}}}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "AH-09 does not have three valid samples"):
                validate_recovery(runtime, quality, adjudication)

    def test_ah10_accepts_sourced_schema_v2_memory_gate_without_quality(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery
        temporary, runtime, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            result = validate_recovery(runtime, quality, adjudication)
            self.assertEqual(result["AH-10"]["status"], "safety-classified")

    def test_rejects_missing_ah09_metric_or_utilization(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery
        temporary, runtime, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            self._mutate_json(runtime / "AH-09/summary.json", lambda p: p["aggregate"].pop("ttft_ms"))
            with self.assertRaisesRegex(ValueError, "AH-09 missing runtime metric"):
                validate_recovery(runtime, quality, adjudication)

    def test_rejects_wrong_ah09_aggregate_metric(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery
        temporary, runtime, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            self._mutate_json(runtime / "AH-09/summary.json", lambda p: p["aggregate"]["ttft_ms"].update(median=9999))
            with self.assertRaisesRegex(ValueError, "AH-09 aggregate metric mismatch"):
                validate_recovery(runtime, quality, adjudication)

    def test_rejects_gpu_kv_or_wrong_offload_activation(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery
        temporary, runtime, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            self._mutate_json(runtime / "AH-09/summary.json", lambda p: p["activation"][0].update(actual_device="GPU", offloaded_layers=2))
            with self.assertRaisesRegex(ValueError, "AH-09 activation mismatch"):
                validate_recovery(runtime, quality, adjudication)

    def test_rejects_wrong_ah09_quality_mean(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery
        temporary, runtime, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            self._mutate_json(adjudication, lambda p: p["AH-09"].update(mean_score=9.0))
            with self.assertRaisesRegex(ValueError, "AH-09 quality mean mismatch"):
                validate_recovery(runtime, quality, adjudication)

    def test_rejects_workbook_without_committed_ah09_mean(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery
        temporary, runtime, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            with self.assertRaisesRegex(ValueError, "workbook does not report AH-09 mean 6.0583"):
                validate_recovery(runtime, quality, adjudication, workbook.replace("6.0583", "6.0000"))

    def test_rejects_successful_non_safety_ah10(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery
        temporary, runtime, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            self._mutate_json(runtime / "AH-10/wrapper-execution.json", lambda p: p.update(controller_exit_code=0))
            with self.assertRaisesRegex(ValueError, "AH-10 controller did not safety-stop"):
                validate_recovery(runtime, quality, adjudication)

    def test_rejects_nonzero_ah10_cleanup_process_counts(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery
        temporary, runtime, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            self._mutate_json(runtime / "AH-10/post-stop-cleanup.json", lambda p: p.update(llama_process_count=1))
            with self.assertRaisesRegex(ValueError, "AH-10 process counts are nonzero"):
                validate_recovery(runtime, quality, adjudication)

    def test_rejects_fabricated_ah10_summary_or_quality(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery
        temporary, runtime, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            (runtime / "AH-10/summary.json").write_text("{}", encoding="utf-8")
            (quality / "AH-10").mkdir()
            with self.assertRaisesRegex(ValueError, "AH-10 must not have summary or quality evidence"):
                validate_recovery(runtime, quality, adjudication)

    def test_finalizer_refuses_unvalidated_recovery_state(self):
        from scripts.testing.tools.finalize_animehacker_runtime_state import finalize_state
        temporary, recovery, quality, adjudication, workbook = self._recovery_fixture()
        with temporary:
            runtime = Path(temporary.name) / "formal-runtime"
            for test_id in ("AH-01", "AH-02", "AH-03", "AH-04", "AH-05", "AH-08"):
                (runtime / test_id).mkdir(parents=True)
                (runtime / test_id / "summary.json").write_text("{}", encoding="utf-8")
            self._mutate_json(recovery / "AH-10/wrapper-execution.json", lambda p: p.update(controller_exit_code=0))
            with self.assertRaisesRegex(ValueError, "AH-10 controller did not safety-stop"):
                finalize_state(runtime, recovery, quality, adjudication, workbook)
            self.assertFalse((runtime / "state.json").exists())

    def test_finalizer_direct_cli_resolves_reconciliation_validator(self):
        script = self._repo_root() / "scripts/testing/tools/finalize_animehacker_runtime_state.py"
        result = subprocess.run([sys.executable, str(script), "--help"], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_workbook_requires_recovered_level_zero_backend_and_final_ah10_values(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery_workbook
        workbook = (self._repo_root() / "docs/testing/workbooks/text-templates/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md").read_text(encoding="utf-8")
        validate_recovery_workbook(workbook)

    def test_workbook_rejects_stale_recovered_backend_or_ah10_measurement(self):
        from scripts.testing.tools.reconcile_animehacker_workbook import validate_recovery_workbook
        good = "| AH-08 | SYCL OpenCL |\n| AH-09 | Level Zero `level_zero:0` |\n| AH-10 | Level Zero `level_zero:0` | 8969.625 MiB peak WS | 70.0 MiB KV |"
        validate_recovery_workbook(good)
        with self.assertRaisesRegex(ValueError, "recovered AH-09/AH-10 must use Level Zero"):
            validate_recovery_workbook(good.replace("AH-09 | Level Zero `level_zero:0`", "AH-09 | SYCL OpenCL"))
        with self.assertRaisesRegex(ValueError, "AH-10 final pilot values missing"):
            validate_recovery_workbook(good.replace("8969.625", "8119.41"))


if __name__ == "__main__":
    unittest.main()
