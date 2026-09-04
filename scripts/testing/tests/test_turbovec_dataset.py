import unittest

from scripts.testing.turbovec.dataset import (
    REQUIRED_DOMAINS,
    REQUIRED_QUERY_KINDS,
    SUPPORTED_SCALES,
    build_frozen_dataset,
)


class TurboVecDatasetTests(unittest.TestCase):
    def test_required_scales_have_unique_genuine_chunks_and_provenance(self):
        self.assertEqual((30, 1_000, 10_000, 100_000), SUPPORTED_SCALES)
        for scale in (30, 1_000, 10_000):
            dataset = build_frozen_dataset(scale)
            self.assertEqual(scale, len(dataset.chunks))
            self.assertEqual(scale, len({chunk.chunk_id for chunk in dataset.chunks}))
            self.assertEqual(scale, len({chunk.content_sha256 for chunk in dataset.chunks}))
            self.assertEqual(scale, len({chunk.text for chunk in dataset.chunks}))
            self.assertTrue(all(chunk.source_document_id for chunk in dataset.chunks))
            self.assertTrue(all(chunk.page_number >= 1 for chunk in dataset.chunks))
            self.assertTrue(all(len(chunk.content_sha256) == 64 for chunk in dataset.chunks))
            self.assertTrue(REQUIRED_DOMAINS.issubset({chunk.domain for chunk in dataset.chunks}))

    def test_generation_is_byte_stable(self):
        first = build_frozen_dataset(1_000)
        second = build_frozen_dataset(1_000)
        self.assertEqual(first.corpus_sha256, second.corpus_sha256)
        self.assertEqual(first.query_sha256, second.query_sha256)
        self.assertEqual(first.relevance_sha256, second.relevance_sha256)
        self.assertEqual(first.chunks, second.chunks)
        self.assertEqual(first.queries, second.queries)

    def test_queries_are_independent_split_complete_and_relevance_closed(self):
        dataset = build_frozen_dataset(1_000)
        chunk_ids = {chunk.chunk_id for chunk in dataset.chunks}
        development = {query.query_id for query in dataset.queries if query.split == "development"}
        evaluation = {query.query_id for query in dataset.queries if query.split == "evaluation"}
        self.assertTrue(development)
        self.assertTrue(evaluation)
        self.assertFalse(development & evaluation)
        self.assertTrue(REQUIRED_QUERY_KINDS.issubset({query.kind for query in dataset.queries}))
        self.assertEqual({query.query_id for query in dataset.queries}, set(dataset.relevance))
        for query in dataset.queries:
            judgements = dataset.relevance[query.query_id]
            self.assertTrue(set(judgements).issubset(chunk_ids))
            self.assertTrue(all(grade in (1, 2, 3) for grade in judgements.values()))
            if query.kind == "absent_answer":
                self.assertEqual({}, judgements)
            else:
                self.assertTrue(judgements)

    def test_scale_rejection_is_fail_closed(self):
        with self.assertRaisesRegex(ValueError, "unsupported corpus scale"):
            build_frozen_dataset(999)


if __name__ == "__main__":
    unittest.main()
