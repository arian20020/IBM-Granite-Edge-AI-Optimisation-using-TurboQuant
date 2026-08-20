import json
import math
import tempfile
import unittest
from dataclasses import FrozenInstanceError, replace
from pathlib import Path

from granite_turbovec.benchmark import (
    BenchmarkEvidence,
    ColdWarmTimings,
    GateCriterion,
    GateMetrics,
    GateResult,
    RankingMetrics,
    RouteEvidence,
    StorageEvidence,
    TimingSummary,
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
from granite_turbovec.text_pipeline import chunk_document, discover_documents


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

    def test_even_sample_median_averages_the_middle_pair(self):
        result = aggregate_timings([1, 1, 1, 100, 100, 100])
        self.assertEqual(50.5, result.median_seconds)
        self.assertEqual(100, result.p95_seconds)

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

    def test_derived_twenty_query_hit_boundary_tolerates_float_rounding_only(self):
        exact = [list(range(index * 10, index * 10 + 10)) for index in range(20)]
        relevant = [{row[0]} if index < 18 else set() for index, row in enumerate(exact)]
        passing = [row.copy() for row in exact]
        passing[17] = passing[17][1:6] + passing[17][0:1] + passing[17][6:]
        failing = [row.copy() for row in passing]
        failing[16] = failing[16][1:6] + failing[16][0:1] + failing[16][6:]
        timing = summarize_cold_warm([1], [1] * 5)

        def evidence(candidate):
            return build_matched_evidence(
                exact, candidate, relevant,
                baseline_timings=timing, candidate_timings=timing,
                float32_vector_bytes=400, candidate_persisted_bytes=100,
                requested_backend="turbovec", actual_backend="turbovec",
                requested_provider="cpu", actual_provider="CPUExecutionProvider",
            )

        passing_hit = next(item for item in evidence(passing).gate.criteria if item.name == "hit_at_5_delta")
        failing_hit = next(item for item in evidence(failing).gate.criteria if item.name == "hit_at_5_delta")
        self.assertAlmostEqual(-0.05, passing_hit.value)
        self.assertTrue(passing_hit.passed)
        self.assertFalse(failing_hit.passed)


class FixtureAndEvidenceTests(unittest.TestCase):
    def test_fixture_corpus_exercises_default_recall_at_ten_pipeline(self):
        fixture = load_evaluation_fixture(FIXTURE)
        documents = discover_documents(FIXTURE.parent / "knowledge")
        chunks = [chunk for document in documents for chunk in chunk_document(document)]
        self.assertGreaterEqual(len(chunks), fixture.top_k)
        self.assertEqual(len(chunks), len({chunk.chunk_id for chunk in chunks}))
        by_source = {document.relative_path: document.text.casefold() for document in documents}
        keyed_terms = {
            ("q01", "granite.txt"): ("granite", "language models"),
            ("q02", "granite.txt"): ("open source",),
            ("q03", "retrieval.md"): ("retrieval", "relevant chunks"),
            ("q04", "retrieval.md"): ("relevant chunks",),
            ("q05", "granite.txt"): ("granite",),
            ("q05", "retrieval.md"): ("retrieval",),
        }
        for query in fixture.queries:
            for source in query.relevant_sources:
                self.assertIn(source, by_source)
                for term in keyed_terms[(query.id, source)]:
                    self.assertIn(term, by_source[source])
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

        with self.assertRaises(ResearchError) as context:
            build_matched_evidence(
                baseline, baseline, [{0}],
                baseline_timings=timings_candidate,
                candidate_timings=timings_candidate,
                float32_vector_bytes=40,
                candidate_persisted_bytes=0,
                requested_backend="turbovec",
                actual_backend="turbovec",
                requested_provider="cpu",
                actual_provider="CPUExecutionProvider",
            )
        self.assertEqual("storage-bytes-invalid", context.exception.code)

    def test_checked_in_fixture_is_valid_and_useful(self):
        fixture = load_evaluation_fixture(FIXTURE)
        self.assertEqual(1, fixture.schema_version)
        self.assertEqual(10, fixture.top_k)
        self.assertGreaterEqual(len(fixture.queries), 6)
        self.assertEqual(len(fixture.queries), len({query.id for query in fixture.queries}))
        self.assertTrue(any(not query.relevant_sources for query in fixture.queries))
        sources = {source for query in fixture.queries for source in query.relevant_sources}
        self.assertIn("granite.txt", sources)
        self.assertIn("retrieval.md", sources)
        raw = json.loads(FIXTURE.read_text(encoding="utf-8"))
        self.assertTrue(all(set(query) == {"id", "text", "relevant_sources"} for query in raw["queries"]))

    def test_fixture_loader_rejects_unknown_duplicate_and_unsafe_paths(self):
        valid = FIXTURE.read_text(encoding="utf-8")
        invalid_payloads = [
            valid.replace('"schema_version": 1', '"schema_version": 1, "unknown": 2', 1),
            valid.replace('"schema_version": 1', '"schema_version": 1, "schema_version": 1', 1),
            valid.replace('"granite.txt"', '"../private.txt"', 1),
            valid.replace('"granite.txt"', '"C:/private.txt"', 1),
            valid.replace('"granite.txt"', '"C:private.txt"', 1),
            valid.replace('"granite.txt"', '"./granite.txt"', 1),
            valid.replace('"granite.txt"', '"folder//granite.txt"', 1),
            valid.replace('"granite.txt"', '"folder/./granite.txt"', 1),
            valid.replace('"granite.txt"', '"folder\\\\granite.txt"', 1),
            valid.replace('"granite.txt"', '"folder/granite.txt."', 1),
            valid.replace('"granite.txt"', '"folder/granite.txt "', 1),
            valid.replace('"granite.txt"', '"folder/granite.txt:ads"', 1),
            valid.replace('"granite.txt"', '"CON.txt"', 1),
            valid.replace('"granite.txt"', '"CON .txt"', 1),
            valid.replace('"granite.txt"', '"folder/Lpt1.md"', 1),
            valid.replace('"granite.txt"', '"//server/share.txt"', 1),
            valid.replace('"granite.txt"', '"granite.json"', 1),
            valid.replace('"granite.txt"', '"folder/\u0001granite.txt"', 1),
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "evaluation.json"
            for payload in invalid_payloads:
                path.write_text(payload, encoding="utf-8")
                with self.assertRaises(ResearchError) as context:
                    load_evaluation_fixture(path)
                self.assertEqual("evaluation-fixture-invalid", context.exception.code)

    def test_fixture_sources_are_casefold_unique(self):
        payload = {
            "schema_version": 1,
            "top_k": 10,
            "queries": [{"id": "q01", "text": "x", "relevant_sources": ["A.txt", "a.TXT"]}],
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "evaluation.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaises(ResearchError) as context:
                load_evaluation_fixture(path)
        self.assertEqual("evaluation-fixture-invalid", context.exception.code)

    def test_fixture_loader_enforces_query_identity_length_and_count_caps(self):
        base = {
            "schema_version": 1,
            "top_k": 10,
            "queries": [{"id": "q01", "text": "x", "relevant_sources": []}],
        }
        invalid = []
        wrong_top_k = dict(base)
        wrong_top_k["top_k"] = 9
        invalid.append(wrong_top_k)
        duplicate = dict(base)
        duplicate["queries"] = base["queries"] * 2
        invalid.append(duplicate)
        too_long = json.loads(json.dumps(base))
        too_long["queries"][0]["text"] = "x" * 501
        invalid.append(too_long)
        long_id = json.loads(json.dumps(base))
        long_id["queries"][0]["id"] = "q001"
        invalid.append(long_id)
        too_many = dict(base)
        too_many["queries"] = [
            {"id": f"q{index:02d}", "text": "x", "relevant_sources": []}
            for index in range(100)
        ]
        invalid.append(too_many)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "evaluation.json"
            for payload in invalid:
                path.write_text(json.dumps(payload), encoding="utf-8")
                with self.assertRaises(ResearchError) as context:
                    load_evaluation_fixture(path)
                self.assertEqual("evaluation-fixture-invalid", context.exception.code)

    def test_fixture_loader_rejects_legacy_per_query_field_names(self):
        payload = {
            "schema_version": 1,
            "top_k": 10,
            "queries": [{"query_id": "q01", "question": "legacy", "relevant_sources": []}],
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "evaluation.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaises(ResearchError) as context:
                load_evaluation_fixture(path)
        self.assertEqual("evaluation-fixture-invalid", context.exception.code)

    def test_evidence_serialization_and_markdown_are_deterministic_and_private(self):
        rankings = [list(range(10))]
        timings = summarize_cold_warm([0.2], [0.1] * 5)
        evidence = build_matched_evidence(
            rankings, rankings, [{0}],
            baseline_timings=timings, candidate_timings=timings,
            float32_vector_bytes=100, candidate_persisted_bytes=20,
            requested_backend="turbovec", actual_backend="turbovec",
            requested_provider="fastembed", actual_provider="fastembed",
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

        with self.assertRaises(ResearchError) as context:
            replace(evidence.baseline, route="private/path")
        self.assertEqual("benchmark-evidence-invalid", context.exception.code)

        with self.assertRaises(ResearchError) as context:
            replace(evidence.baseline.metrics, recall_at_k=math.nan)
        self.assertEqual("benchmark-evidence-invalid", context.exception.code)

    def test_forged_evidence_contradictions_fail_at_construction(self):
        rankings = [list(range(10))]
        timing = summarize_cold_warm([1], [1] * 5)
        evidence = build_matched_evidence(
            rankings, rankings, [{0}],
            baseline_timings=timing, candidate_timings=timing,
            float32_vector_bytes=100, candidate_persisted_bytes=20,
            requested_backend="turbovec", actual_backend="turbovec",
            requested_provider="cpu", actual_provider="CPUExecutionProvider",
        )

        contradictions = [
            lambda: TimingSummary(0, 1, 1, 1, 1),
            lambda: TimingSummary(1, -1, 1, 0, 1),
            lambda: RankingMetrics(1.01, 1, 1),
            lambda: ColdWarmTimings(None, evidence.baseline.timings.warm),
            lambda: replace(evidence.baseline, metrics=None),
            lambda: replace(evidence.baseline, requested_backend="C:private"),
            lambda: replace(evidence, baseline=replace(evidence.baseline, query_count=2)),
            lambda: replace(evidence.baseline, top_k=0),
            lambda: StorageEvidence(100, 0, 0),
            lambda: GateCriterion("recall_at_10", 0.9, 0.85, "<=", True),
            lambda: GateCriterion("recall_at_10", 0.9, 0.85, ">=", False),
            lambda: GateResult(True, evidence.gate.criteria[:-1]),
            lambda: GateResult(False, evidence.gate.criteria),
            lambda: replace(evidence, query_count=2),
            lambda: replace(evidence, baseline=None),
            lambda: replace(
                evidence,
                top_k=5,
                baseline=replace(evidence.baseline, top_k=5),
                candidate=replace(evidence.candidate, top_k=5),
            ),
            lambda: replace(evidence, candidate=replace(evidence.candidate, route="float32")),
            lambda: replace(evidence, gate=evaluate_gate(GateMetrics(0.9, 1, 0, 0.2, 1))),
        ]
        for index, forge in enumerate(contradictions):
            with self.subTest(index=index):
                with self.assertRaises(ResearchError) as context:
                    forge()
                self.assertEqual("benchmark-evidence-invalid", context.exception.code)


if __name__ == "__main__":
    unittest.main()
