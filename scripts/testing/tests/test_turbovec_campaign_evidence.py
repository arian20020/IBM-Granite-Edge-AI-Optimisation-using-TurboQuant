import json
from pathlib import Path
import tempfile
import unittest

from scripts.testing.turbovec.validate_campaign import validate_scale_run


class CampaignEvidenceTests(unittest.TestCase):
    def _fixture(self, root: Path) -> Path:
        run = root / "run"
        run.mkdir()
        benchmark = {"schema_version": "2.0", "scale": 30, "repetitions": [{"repetition": 1, "seed": 9, "configuration_order": ["exact", "tq2", "tq3", "tq4"], "query_order": list(range(128)), "valid": True, "invalidation_reasons": [], "configurations": {name: {"warmup_batches": 5, "warm_query_latency_ms": list(range(30)), "embedding_matrix_sha256": "a" * 64, "query_matrix_sha256": "b" * 64} for name in ("exact", "tq2", "tq3", "tq4")}}]}
        evaluation = {"schema_version": "2.0", "repetitions": [{"repetition": 1, "seed": 9, "configuration_order": ["exact", "tq2", "tq3", "tq4"], "valid": True, "invalidation_reasons": [], "evaluation_query_counts": {"total": 96, "answerable": 84, "absent_answer": 12}, "configurations": {name: {} for name in ("exact", "tq2", "tq3", "tq4")}}]}
        summary = {"schema_version": "2.0", "campaign_id": "turbovec-production-scale-final-evaluation-v2", "experiment_id": "EXP-TV-COMP-001", "scale": 30, "requested_repetitions": 1, "valid_repetitions": 1, "warmup_batches": 5, "measured_batches": 30, "dataset": {"corpus_sha256": "c" * 64, "query_sha256": "d" * 64, "relevance_sha256": "e" * 64}, "embedding_manifest_sha256": "f" * 64, "readiness_decision_sha256": "0" * 64, "statistics": {}}
        for name, value in (("benchmark.json", benchmark), ("evaluation.json", evaluation), ("summary.json", summary)):
            (run / name).write_text(json.dumps(value) + "\n", encoding="utf-8")
        import hashlib
        files = [{"name": name, "bytes": (run / name).stat().st_size, "sha256": hashlib.sha256((run / name).read_bytes()).hexdigest()} for name in ("benchmark.json", "evaluation.json", "summary.json")]
        (run / "terminal.json").write_text(json.dumps({"schema_version": "2.0", "status": "completed", "files": files}) + "\n", encoding="utf-8")
        return run

    def test_validator_accepts_closed_evidence_and_rejects_mutations(self):
        with tempfile.TemporaryDirectory() as directory:
            run = self._fixture(Path(directory))
            result = validate_scale_run(run)
            self.assertEqual(1, result["valid_repetitions"])
            benchmark = json.loads((run / "benchmark.json").read_text(encoding="utf-8"))
            benchmark["repetitions"][0]["configuration_order"] = ["exact", "tq2", "tq4", "tq3"]
            (run / "benchmark.json").write_text(json.dumps(benchmark) + "\n", encoding="utf-8")
            with self.assertRaises(ValueError):
                validate_scale_run(run)


if __name__ == "__main__":
    unittest.main()
