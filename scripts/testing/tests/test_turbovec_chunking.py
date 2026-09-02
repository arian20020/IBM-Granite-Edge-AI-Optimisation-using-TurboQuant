import hashlib
import json
import unittest
from pathlib import Path

from scripts.testing.turbovec.chunking import PageText, WordFixtureTokenCounter, chunk_pages


ROOT = Path(__file__).resolve().parents[3]
PROTOCOL = ROOT / "experiments/protocols/turbovec"


def canonical_hash(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()).hexdigest()


class ChunkingTests(unittest.TestCase):
    def test_chunk_ids_are_stable_and_page_aware(self):
        pages = (PageText(1, "alpha " * 600), PageText(2, "beta " * 300))
        first = chunk_pages("a" * 64, pages, WordFixtureTokenCounter())
        self.assertEqual(first, chunk_pages("a" * 64, pages, WordFixtureTokenCounter()))
        self.assertTrue(all(chunk.token_count <= 768 for chunk in first))
        self.assertTrue(all(chunk.end_page - chunk.start_page <= 1 for chunk in first))

    def test_empty_pages_preserve_following_page_numbers(self):
        chunks = chunk_pages("b" * 64, (PageText(1, ""), PageText(2, "retrievable fact")), WordFixtureTokenCounter())
        self.assertEqual(2, chunks[0].start_page)

    def test_normalizes_unicode_newlines_and_horizontal_space(self):
        chunks = chunk_pages("c" * 64, (PageText(1, "Cafe\u0301\t fact\r\n\r\nNext   fact"),), WordFixtureTokenCounter())
        self.assertEqual("Café fact\n\nNext fact", chunks[0].text)

    def test_controlled_corpus_and_relevance_counts_and_hashes(self):
        corpus = json.loads((PROTOCOL / "corpus-v1.json").read_text(encoding="utf-8"))
        queries = json.loads((PROTOCOL / "queries-v1.json").read_text(encoding="utf-8"))
        relevance = json.loads((PROTOCOL / "relevance-v1.json").read_text(encoding="utf-8"))
        self.assertEqual(30, len(corpus["documents"]))
        self.assertEqual({"pdf": 10, "txt": 10, "md": 10}, {kind: sum(d["format"] == kind for d in corpus["documents"]) for kind in ("pdf", "txt", "md")})
        self.assertEqual(120, len(queries["queries"]))
        self.assertEqual({"direct": 60, "terminology": 30, "multi_detail": 15, "negative": 15}, {kind: sum(q["kind"] == kind for q in queries["queries"]) for kind in ("direct", "terminology", "multi_detail", "negative")})
        self.assertEqual(corpus["canonical_sha256"], canonical_hash(corpus["documents"]))
        self.assertEqual(queries["canonical_sha256"], canonical_hash(queries["queries"]))
        self.assertEqual(relevance["canonical_sha256"], canonical_hash(relevance["judgements"]))
        query_ids = {q["query_id"] for q in queries["queries"]}
        relevant_queries = {r["query_id"] for r in relevance["judgements"]}
        self.assertTrue(relevant_queries <= query_ids)
        self.assertFalse({q["query_id"] for q in queries["queries"] if q["kind"] == "negative"} & relevant_queries)
        grade3 = {r["query_id"] for r in relevance["judgements"] if r["grade"] == 3}
        self.assertTrue({q["query_id"] for q in queries["queries"] if q["kind"] != "negative"} <= grade3)


if __name__ == "__main__":
    unittest.main()
