import json
import math
import tempfile
import unittest
from dataclasses import FrozenInstanceError
from pathlib import Path

from granite_turbovec.benchmark import (
    BenchmarkEvidence,
    GateMetrics,
    RouteEvidence,
    StorageEvidence,
    aggregate_timings,
    build_matched_evidence,
    calculate_ratio,
    canonical_json,
    compare_matched_routes,
    evaluate_gate,
    evaluate_rankings,
    load_evaluation_fixture,
    render_markdown,
    summarize_cold_warm,
)
from granite_turbovec.contracts import ResearchError


FIXTURE = Path(__file__).parent / "fixtures" / "evaluation.json"


class RankingMetricTests(unittest.TestCase):
    def test_calculates_required_example(self):
        result = evaluate_rankings(
            [[10, 20, 30], [40, 50, 60]],
            [[10, 30, 99], [50, 40, 60]],
            [{10}, {40}],
            3,
        )
        # The requested set-intersection formula yields 2/3 + 3/3, averaged.
        self.assertAlmostEqual(5 / 6, result.recall_at_k)
        self.assertEqual(0.75, result.mrr)
        self.assertEqual(1.0, result.hit_at_k)

    def test_no_answer_is_explicitly_scored_zero_for_judged_metrics(self):
        result = evaluate_rankings([[1, 2]], [[1, 2]], [set()], 2)
        self.assertEqual(1.0, result.recall_at_k)
        self.assertEqual(0.0, result.mrr)
        self.assertEqual(0.0, result.hit_at_k)

    def test_matched_comparison_includes_baseline_and_ratios(self):
        result = compare_matched_routes(
            exact=[[10, 20], [40, 50]],
            candidate=[[20, 10], [40, 99]],
            relevant=[{10}, {40}],
            k=2,
            exact_route="float32",
            candidate_route="turbovec-4bit",
        )
        self.assertEqual(1.0, result.exact.mrr)
        self.assertEqual(0.75, result.candidate.mrr)
        self.assertEqual(0.75, result.mrr_ratio)
        self.assertEqual(0.0, result.hit_delta)
        self.assertEqual(2, result.query_count)
        self.assertEqual(2, result.top_k)

    def test_rejects_mismatches_invalid_ids_duplicates_and_bounds(self):
        invalid = [
            ([], [], [], 1, "rankings-empty"),
            ([[1]], [[1], [2]], [{1}], 1, "rankings-query-count-mismatch"),
            ([[1]], [[1]], [], 1, "rankings-relevance-count-mismatch"),
            ([[1]], [[1]], [{1}], 0, "rankings-k-invalid"),
            ([[1]], [[1]], [{1}], True, "rankings-k-invalid"),
            ([[1]], [[1]], [{1}], 2, "rankings-k-out-of-bounds"),
            ([[1, 1]], [[1, 2]], [{1}], 2, "ranking-id-duplicate"),
            ([[True]], [[1]], [{1}], 1, "ranking-id-invalid"),
            ([[1]], [["1"]], [{1}], 1, "ranking-id-invalid"),
            ([[1]], [[1]], [{True}], 1, "relevance-id-invalid"),
        ]
        for exact, candidate, relevant, k, code in invalid:
            with self.subTest(code=code), self.assertRaises(ResearchError) as context:
                evaluate_rankings(exact, candidate, relevant, k)
            self.assertEqual(code, context.exception.code)

    def test_zero_baseline_ratio_fails_closed(self):
        with self.assertRaises(ResearchError) as context:
            compare_matched_routes([[1]], [[1]], [set()], 1)
        self.assertEqual("metric-baseline-zero", context.exception.code)

        with self.assertRaises(ResearchError) as context:
            calculate_ratio(1, 0)
        self.assertEqual("metric-baseline-zero", context.exception.code)


class TimingTests(unittest.TestCase):
    def test_nearest_rank_p95_and_summary(self):
        result = aggregate_timings([0.01, 0.02, 0.03, 0.04, 0.50])
        self.assertEqual(5, result.count)
        self.assertEqual(0.03, result.median_seconds)
        self.assertEqual(0.50, result.p95_seconds)
        self.assertEqual(0.01, result.min_seconds)
        self.assertEqual(0.50, result.max_seconds)

    def test_cold_and_warm_are_separate_and_release_requires_five_warm(self):
        summary = summarize_cold_warm([0.8], [0.1, 0.2, 0.3, 0.4, 0.5])
        self.assertEqual(1, summary.cold.count)
        self.assertEqual(5, summary.warm.count)
        with self.assertRaises(ResearchError) as context:
            summarize_cold_warm([0.8], [0.1] * 4)
        self.assertEqual("timing-warm-count-insufficient", context.exception.code)
        self.assertEqual(1, aggregate_timings([0.1], require_release_evidence=False).count)

    def test_rejects_nonfinite_negative_bool_or_empty(self):
        for samples in ([], [-1.0], [math.nan], [math.inf], [True]):
            with self.subTest(samples=samples), self.assertRaises(ResearchError) as context:
                aggregate_timings(samples)
            self.assertEqual("timing-samples-invalid", context.exception.code)


class GateTests(unittest.TestCase):
    def test_exact_boundaries_pass(self):
        result = evaluate_gate(GateMetrics(0.85, 0.90, -0.05, 0.25, 1.0))
        self.assertTrue(result.passed)
        self.assertTrue(all(item.passed for item in result.criteria))

    def test_each_individual_threshold_failure_fails_overall(self):
        cases = [
            GateMetrics(0.849, 1, 0, 0.1, 0.5),
            GateMetrics(1, 0.899, 0, 0.1, 0.5),
            GateMetrics(1, 1, -0.051, 0.1, 0.5),
            GateMetrics(1, 1, 0, 0.251, 0.5),
            GateMetrics(1, 1, 0, 0.1, 1.001),
        ]
        for metrics in cases:
            with self.subTest(metrics=metrics):
                self.assertFalse(evaluate_gate(metrics).passed)

    def test_invalid_metrics_fail_with_fixed_code(self):
        for metrics in (
            GateMetrics(math.nan, 1, 0, 0.1, 0.5),
            GateMetrics(1.1, 1, 0, 0.1, 0.5),
            GateMetrics(1, -0.1, 0, 0.1, 0.5),
            GateMetrics(1, 1, -1.1, 0.1, 0.5),
            GateMetrics(1, 1, 0, True, 0.5),
        ):
            with self.assertRaises(ResearchError) as context:
                evaluate_gate(metrics)
            self.assertEqual("gate-metrics-invalid", context.exception.code)


class FixtureAndEvidenceTests(unittest.TestCase):
    def test_builds_gate_inputs_from_one_matched_run(self):
        baseline = [list(range(10)), list(range(10, 20))]
        candidate = [list(range(10)), list(range(10, 20))]
        relevant = [{0}, {10}]
        baseline_timing = summarize_cold_warm([0.2], [0.1] * 5)
        candidate_timing = summarize_cold_warm([0.2], [0.08] * 5)
        evidence = build_matched_evidence(
            baseline,
            candidate,
            relevant,
            baseline_timings=baseline_timing,
            candidate_timings=candidate_timing,
            float32_vector_bytes=400,
            candidate_persisted_bytes=100,
            requested_backend="turbovec",
            actual_backend="turbovec",
            requested_provider="cpu",
            actual_provider="CPUExecutionProvider",
        )
        self.assertTrue(evidence.gate.passed)
        self.assertEqual(1.0, evidence.gate.criteria[0].value)
        self.assertAlmostEqual(0.8, evidence.gate.criteria[-1].value)
        self.assertEqual(0.25, evidence.storage.size_ratio)

    def test_evidence_build_fails_closed_on_zero_baseline_latency(self):
        baseline = [list(range(10))]
        timings_zero = summarize_cold_warm([0], [0] * 5)
        timings_candidate = summarize_cold_warm([0.1], [0.1] * 5)
        with self.assertRaises(ResearchError) as context:
            build_matched_evidence(
                baseline, baseline, [{0}],
                baseline_timings=timings_zero,
                candidate_timings=timings_candidate,
                float32_vector_bytes=40,
                candidate_persisted_bytes=10,
                requested_backend="turbovec",
                actual_backend="turbovec",
                requested_provider="cpu",
                actual_provider="CPUExecutionProvider",
            )
        self.assertEqual("metric-baseline-zero", context.exception.code)

    def test_checked_in_fixture_is_valid_and_useful(self):
        fixture = load_evaluation_fixture(FIXTURE)
        self.assertEqual(1, fixture.schema_version)
        self.assertEqual(10, fixture.top_k)
        self.assertGreaterEqual(len(fixture.queries), 6)
        self.assertEqual(len(fixture.queries), len({query.query_id for query in fixture.queries}))
        self.assertTrue(any(not query.relevant_sources for query in fixture.queries))
        sources = {source for query in fixture.queries for source in query.relevant_sources}
        self.assertIn("granite.txt", sources)
        self.assertIn("retrieval.md", sources)

    def test_fixture_loader_rejects_unknown_duplicate_and_unsafe_paths(self):
        valid = FIXTURE.read_text(encoding="utf-8")
        invalid_payloads = [
            valid.replace('"schema_version": 1', '"schema_version": 1, "unknown": 2', 1),
            valid.replace('"schema_version": 1', '"schema_version": 1, "schema_version": 1', 1),
            valid.replace('"granite.txt"', '"../private.txt"', 1),
            valid.replace('"granite.txt"', '"C:/private.txt"', 1),
            valid.replace('"granite.txt"', '"C:private.txt"', 1),
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "evaluation.json"
            for payload in invalid_payloads:
                path.write_text(payload, encoding="utf-8")
                with self.assertRaises(ResearchError) as context:
                    load_evaluation_fixture(path)
                self.assertEqual("evaluation-fixture-invalid", context.exception.code)

    def test_fixture_loader_enforces_query_identity_length_and_count_caps(self):
        base = {
            "schema_version": 1,
            "top_k": 10,
            "queries": [{"query_id": "q01", "question": "x", "relevant_sources": []}],
        }
        invalid = []
        duplicate = dict(base)
        duplicate["queries"] = base["queries"] * 2
        invalid.append(duplicate)
        too_long = json.loads(json.dumps(base))
        too_long["queries"][0]["question"] = "x" * 501
        invalid.append(too_long)
        too_many = dict(base)
        too_many["queries"] = [
            {"query_id": f"q{index:03d}", "question": "x", "relevant_sources": []}
            for index in range(257)
        ]
        invalid.append(too_many)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "evaluation.json"
            for payload in invalid:
                path.write_text(json.dumps(payload), encoding="utf-8")
                with self.assertRaises(ResearchError) as context:
                    load_evaluation_fixture(path)
                self.assertEqual("evaluation-fixture-invalid", context.exception.code)

    def test_evidence_serialization_and_markdown_are_deterministic_and_private(self):
        comparison = compare_matched_routes(
            [[1, 2, 3, 4, 5]], [[1, 2, 3, 4, 5]], [{1}], 5,
            exact_route="float32", candidate_route="turbovec-4bit",
        )
        timings = summarize_cold_warm([0.2], [0.1] * 5)
        gate = evaluate_gate(GateMetrics(1, 1, 0, 0.2, 1))
        evidence = BenchmarkEvidence(
            schema_version=1,
            query_count=1,
            top_k=5,
            baseline=RouteEvidence("float32", "float32", "float32", "fastembed", "fastembed", comparison.exact, timings),
            candidate=RouteEvidence("turbovec-4bit", "turbovec", "turbovec", "fastembed", "fastembed", comparison.candidate, timings),
            storage=StorageEvidence(100, 20, 0.2),
            gate=gate,
        )
        first = canonical_json(evidence)
        second = canonical_json(evidence)
        self.assertEqual(first, second)
        payload = json.loads(first)
        self.assertEqual(1, payload["schema_version"])
        self.assertEqual("turbovec-4bit", payload["candidate"]["route"])
        self.assertNotIn("__dataclass_fields__", first)
        markdown = render_markdown(evidence)
        self.assertEqual(markdown, render_markdown(evidence))
        for private in ("C:\\Users\\Arian", "query text", "document content"):
            self.assertNotIn(private, first)
            self.assertNotIn(private, markdown)
        with self.assertRaises(FrozenInstanceError):
            evidence.schema_version = 2

        invalid_route = RouteEvidence(
            "private/path", "float32", "float32", "cpu", "cpu",
            comparison.exact, timings,
        )
        invalid = BenchmarkEvidence(1, 1, 5, invalid_route, evidence.candidate, evidence.storage, gate)
        with self.assertRaises(ResearchError) as context:
            canonical_json(invalid)
        self.assertEqual("benchmark-evidence-invalid", context.exception.code)

        nonfinite_route = RouteEvidence(
            "float32", "float32", "float32", "cpu", "cpu",
            type(comparison.exact)(math.nan, 1, 1), timings,
        )
        nonfinite = BenchmarkEvidence(1, 1, 5, nonfinite_route, evidence.candidate, evidence.storage, gate)
        with self.assertRaises(ResearchError) as context:
            render_markdown(nonfinite)
        self.assertEqual("benchmark-evidence-invalid", context.exception.code)


if __name__ == "__main__":
    unittest.main()
