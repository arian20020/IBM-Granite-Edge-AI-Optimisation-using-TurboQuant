import math
import tempfile
import unittest
from pathlib import Path

import numpy as np

from scripts.testing.turbovec.embedding import (
    DeterministicEmbeddingProvider,
    NormalizingEmbeddingProvider,
    lock_model_assets,
    validate_embeddings,
)


class FakeRawProvider:
    def __init__(self, values): self.values = values
    def embed_documents(self, texts): return self.values
    def embed_queries(self, texts): return self.values[:len(texts)]


class EmbeddingTests(unittest.TestCase):
    def test_normalizes_384_finite_values(self):
        provider = NormalizingEmbeddingProvider(FakeRawProvider(np.ones((2, 384), dtype=np.float32)))
        vectors = provider.embed_documents(["a", "b"])
        np.testing.assert_allclose(np.linalg.norm(vectors, axis=1), 1.0, atol=1e-6)

    def test_rejects_wrong_dimension_nan_zero_and_wrong_rows(self):
        for data, rows in ((np.ones((1, 383), np.float32), 1), (np.full((1, 384), np.nan, np.float32), 1), (np.zeros((1, 384), np.float32), 1), (np.ones((2, 384), np.float32), 1)):
            with self.assertRaises(ValueError): validate_embeddings(data, rows, 384)

    def test_deterministic_provider_is_repeatable_and_forbidden_for_measured(self):
        first = DeterministicEmbeddingProvider(measured=False).embed_queries(["alpha"])
        second = DeterministicEmbeddingProvider(measured=False).embed_queries(["alpha"])
        np.testing.assert_array_equal(first, second)
        with self.assertRaises(ValueError): DeterministicEmbeddingProvider(measured=True)

    def test_asset_lock_is_stable_and_rejects_mutable_revision(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); (root / "openvino_model.xml").write_text("model", encoding="utf-8")
            (root / "snapshot.json").write_text('{"revision":"0123456789abcdef0123456789abcdef01234567"}', encoding="utf-8")
            first = lock_model_assets(root); second = lock_model_assets(root)
            self.assertEqual(first, second); self.assertEqual(2, len(first["files"])); self.assertEqual(384, first["dimension"])
            (root / "snapshot.json").write_text('{"revision":"main"}', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "immutable"): lock_model_assets(root)


if __name__ == "__main__": unittest.main()
