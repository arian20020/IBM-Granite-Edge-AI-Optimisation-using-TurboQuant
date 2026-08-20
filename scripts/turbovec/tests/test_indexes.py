import tempfile
import unittest
from pathlib import Path
from unittest import mock

import numpy as np

from granite_turbovec.contracts import ResearchError
from granite_turbovec.embedding import FastEmbedder
from granite_turbovec.indexes import Float32Index, TurboVecIndex, validate_ids, validate_vectors


class FakeSession:
    def get_providers(self):
        return ["CPUExecutionProvider"]


class FakeFastEmbedModel:
    instances = []
    dimension = 3

    def __init__(self, **kwargs):
        self.kwargs = kwargs
        self.model = type("Model", (), {"session": FakeSession()})()
        self.instances.append(self)

    def embed(self, texts):
        return (np.array([len(text), 1, 0], dtype=np.float64) for text in texts)

    def query_embed(self, texts):
        return (np.array([len(text), 0, 1], dtype=np.float32) for text in texts)


class FakeFastEmbedModule:
    TextEmbedding = FakeFastEmbedModel


class FastEmbedderTests(unittest.TestCase):
    def setUp(self):
        FakeFastEmbedModel.instances.clear()

    def test_constructor_is_offline_cpu_only_and_reports_actual_providers(self):
        with tempfile.TemporaryDirectory() as directory:
            embedder = FastEmbedder(directory, fastembed_module=FakeFastEmbedModule)

        instance = FakeFastEmbedModel.instances[-1]
        self.assertEqual("BAAI/bge-small-en-v1.5", instance.kwargs["model_name"])
        self.assertEqual(["CPUExecutionProvider"], instance.kwargs["providers"])
        self.assertTrue(instance.kwargs["local_files_only"])
        self.assertEqual(["CPUExecutionProvider"], embedder.providers)
        self.assertEqual("BAAI/bge-small-en-v1.5", embedder.model_identity)

    def test_constructor_rejects_missing_or_non_directory_cache_without_importing(self):
        with tempfile.TemporaryDirectory() as directory:
            missing = Path(directory) / "missing"
            file_path = Path(directory) / "file"
            file_path.write_text("x", encoding="utf-8")
            for path in (missing, file_path):
                with self.subTest(path=path.name), self.assertRaises(ResearchError) as context:
                    FastEmbedder(path, fastembed_module=FakeFastEmbedModule)
                self.assertEqual("embedding-cache-invalid", context.exception.code)

        with mock.patch("granite_turbovec.embedding.Path.is_dir", side_effect=OSError("private")):
            with self.assertRaises(ResearchError) as context:
                FastEmbedder("opaque-cache", fastembed_module=FakeFastEmbedModule)
        self.assertEqual("embedding-cache-invalid", str(context.exception))

    def test_provider_introspection_never_substitutes_requested_provider_or_leaks_backend_errors(self):
        class OpaqueModel(FakeFastEmbedModel):
            @property
            def model(self):
                raise RuntimeError("private backend detail")

            @model.setter
            def model(self, value):
                pass

        module = type("Module", (), {"TextEmbedding": OpaqueModel})
        with tempfile.TemporaryDirectory() as directory:
            embedder = FastEmbedder(directory, fastembed_module=module)
        self.assertEqual([], embedder.providers)

    def test_embedding_outputs_are_finite_contiguous_float32_and_dimension_is_fixed(self):
        with tempfile.TemporaryDirectory() as directory:
            embedder = FastEmbedder(directory, fastembed_module=FakeFastEmbedModule)
            documents = embedder.embed_documents(["a", "abcd"])
            queries = embedder.embed_queries(["xy"])
            empty = embedder.embed_documents([])

        self.assertEqual((2, 3), documents.shape)
        self.assertEqual((1, 3), queries.shape)
        self.assertEqual((0, 3), empty.shape)
        self.assertEqual(3, embedder.dimension)
        self.assertEqual(np.float32, documents.dtype)
        self.assertTrue(documents.flags.c_contiguous)

    def test_embedding_rejects_row_count_dimension_and_nonfinite_output(self):
        cases = (
            (lambda _: iter(()), "embedding-row-count-invalid"),
            (lambda _: iter((np.ones(3), np.ones(4))), "embedding-dimension-invalid"),
            (lambda _: iter((np.array([1, np.nan, 3]),)), "embedding-nonfinite"),
            (
                lambda _: iter((np.array([float(np.finfo(np.float32).max) * 2, 1, 2], dtype=np.float64),)),
                "embedding-nonfinite",
            ),
        )
        for embed, expected in cases:
            class Broken(FakeFastEmbedModel):
                def embed(self, texts):
                    return embed(texts)

            module = type("Module", (), {"TextEmbedding": Broken})
            with self.subTest(expected=expected), tempfile.TemporaryDirectory() as directory:
                instance = FastEmbedder(directory, fastembed_module=module)
                with self.assertRaises(ResearchError) as context:
                    instance.embed_documents(["a"] if expected != "embedding-dimension-invalid" else ["a", "b"])
                self.assertEqual(expected, context.exception.code)


class ValidationTests(unittest.TestCase):
    def test_validate_vectors_returns_defensive_contiguous_float32(self):
        source = np.arange(12, dtype=np.float32).reshape(3, 4)[:, ::2]
        result = validate_vectors(source, dimension=2, expected_count=3)
        source[0, 0] = 99
        self.assertEqual(np.float32, result.dtype)
        self.assertTrue(result.flags.c_contiguous)
        self.assertEqual(0, result[0, 0])

    def test_validate_vectors_rejects_shapes_types_counts_empty_and_nonfinite(self):
        cases = (
            (np.ones(3, dtype=np.float32), {}, "vectors-shape-invalid"),
            (np.ones((2, 3), dtype=np.float32), {"dimension": 2}, "vectors-dimension-invalid"),
            (np.ones((2, 3), dtype=np.float32), {"expected_count": 1}, "vectors-count-invalid"),
            (np.empty((0, 3), dtype=np.float32), {}, "vectors-empty"),
            (np.array([[1, np.inf]], dtype=np.float32), {}, "vectors-nonfinite"),
            (np.ones((1, 2), dtype=np.float64), {}, "vector-dtype-invalid"),
            (np.array([[1.0 + 2**-30]], dtype=np.float64), {}, "vector-dtype-invalid"),
            (np.ones((1, 2), dtype=np.int64), {}, "vector-dtype-invalid"),
            (np.ones((1, 2), dtype=np.complex64), {}, "vector-dtype-invalid"),
            (np.array([[object()]], dtype=object), {}, "vector-dtype-invalid"),
        )
        for value, kwargs, expected in cases:
            with self.subTest(expected=expected), self.assertRaises(ResearchError) as context:
                validate_vectors(value, **kwargs)
            self.assertEqual(expected, context.exception.code)

    def test_validate_ids_accepts_uint64_compatible_values_and_copies(self):
        source = np.array([101, 102, 2**63 + 1], dtype=np.uint64)
        result = validate_ids(source, expected_count=3)
        source[0] = 999
        self.assertEqual(np.uint64, result.dtype)
        self.assertEqual(101, result[0])

    def test_validate_ids_rejects_invalid_shape_count_duplicates_and_lossy_types(self):
        cases = (
            (np.array([[1]]), 1, "ids-shape-invalid"),
            (np.array([1, 2]), 1, "ids-count-invalid"),
            (np.array([1, 1]), 2, "ids-duplicate"),
            (np.array([-1]), 1, "ids-type-invalid"),
            (np.array([1.0]), 1, "ids-type-invalid"),
            (np.array([2**64], dtype=object), 1, "ids-type-invalid"),
            (np.array([], dtype=np.uint64), 0, "ids-empty"),
        )
        for value, count, expected in cases:
            with self.subTest(expected=expected), self.assertRaises(ResearchError) as context:
                validate_ids(value, expected_count=count)
            self.assertEqual(expected, context.exception.code)


class Float32IndexTests(unittest.TestCase):
    def test_identity_vectors_return_stable_ids_and_expected_shape(self):
        vectors = np.eye(8, dtype=np.float32)
        index = Float32Index(vectors, np.arange(101, 109, dtype=np.uint64))
        scores, ids = index.search(vectors[[0]], 3)
        self.assertEqual((1, 3), scores.shape)
        self.assertEqual((1, 3), ids.shape)
        self.assertEqual(101, ids[0, 0])

    def test_ties_sort_by_score_descending_then_id_ascending_for_each_query(self):
        vectors = np.array([[1, 0], [1, 0], [0, 1]], dtype=np.float32)
        ids = np.array([9, 3, 7], dtype=np.uint64)
        scores, result_ids = Float32Index(vectors, ids).search(np.array([[1, 0], [0, 1]], np.float32), 3)
        np.testing.assert_array_equal(result_ids[0], [3, 9, 7])
        np.testing.assert_array_equal(result_ids[1], [7, 3, 9])
        self.assertTrue(np.isfinite(scores).all())

    def test_constructor_and_search_use_defensive_copies(self):
        vectors = np.eye(2, dtype=np.float32)
        ids = np.array([2, 1], dtype=np.uint64)
        index = Float32Index(vectors, ids)
        vectors[:] = 0
        ids[:] = 99
        scores, result_ids = index.search(np.array([[1, 0]], dtype=np.float32), 1)
        self.assertEqual(2, result_ids[0, 0])
        self.assertEqual(1, scores[0, 0])

    def test_invalid_queries_and_k_use_fixed_codes(self):
        index = Float32Index(np.eye(2, dtype=np.float32), np.array([1, 2], np.uint64))
        cases = (
            (np.ones(2), 1, "query-shape-invalid"),
            (np.ones((0, 2), np.float32), 1, "query-empty"),
            (np.ones((1, 3), np.float32), 1, "query-dimension-invalid"),
            (np.array([[np.nan, 0]], np.float32), 1, "query-nonfinite"),
            (np.ones((1, 2), np.float32), 0, "search-k-invalid"),
            (np.ones((1, 2), np.float32), 3, "search-k-invalid"),
            (np.ones((1, 2), np.float32), True, "search-k-invalid"),
        )
        for query, k, expected in cases:
            with self.subTest(expected=expected), self.assertRaises(ResearchError) as context:
                index.search(query, k)
            self.assertEqual(expected, context.exception.code)


class FakeTurboIndex:
    stored = {}

    def __init__(self, dim=None, bit_width=None):
        self.dim = dim
        self.bit_width = bit_width
        self.vectors = None
        self.ids = None

    def add_with_ids(self, vectors, ids):
        self.vectors = np.array(vectors, copy=True)
        self.ids = np.array(ids, copy=True)

    def __len__(self):
        return 0 if self.ids is None else len(self.ids)

    def search(self, queries, k):
        scores = np.asarray(queries) @ self.vectors.T
        order = np.argsort(-scores, axis=1, kind="stable")[:, :k]
        return np.take_along_axis(scores, order, axis=1), self.ids[order]

    def write(self, path, durable=True):
        self.stored[str(path)] = (self.dim, self.bit_width, self.vectors.copy(), self.ids.copy())

    @classmethod
    def load(cls, path):
        dim, bit_width, vectors, ids = cls.stored[str(path)]
        result = cls(dim, bit_width)
        result.vectors, result.ids = vectors, ids
        return result


class FakeTurboModule:
    IdMapIndex = FakeTurboIndex


class TurboVecIndexTests(unittest.TestCase):
    def setUp(self):
        FakeTurboIndex.stored.clear()

    def test_add_search_and_multi_query_preserve_stable_uint64_ids(self):
        index = TurboVecIndex(3, bits=4, turbovec_module=FakeTurboModule)
        vectors = np.eye(3, dtype=np.float32)
        ids = np.array([2**63 + 1, 7, 9], dtype=np.uint64)
        index.add_with_ids(vectors, ids)
        scores, result_ids = index.search(vectors[[0, 2]], 2)
        self.assertEqual((2, 2), scores.shape)
        self.assertEqual((2, 2), result_ids.shape)
        self.assertEqual(np.uint64, result_ids.dtype)
        self.assertEqual(2**63 + 1, result_ids[0, 0])

    def test_only_two_and_four_bits_are_supported(self):
        for bits in (1, 3, 8, True):
            with self.subTest(bits=bits), self.assertRaises(ResearchError) as context:
                TurboVecIndex(3, bits=bits, turbovec_module=FakeTurboModule)
            self.assertEqual("index-bits-invalid", context.exception.code)

    def test_write_and_load_round_trip_with_validated_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "index.tv"
            index = TurboVecIndex(2, bits=2, turbovec_module=FakeTurboModule)
            index.add_with_ids(np.eye(2, dtype=np.float32), np.array([101, 102], np.uint64))
            index.write(path)
            loaded = TurboVecIndex.load(path, turbovec_module=FakeTurboModule)
            _, result_ids = loaded.search(np.array([[1, 0]], np.float32), 1)
        self.assertEqual(101, result_ids[0, 0])
        self.assertEqual(2, loaded.dimension)
        self.assertEqual(2, loaded.bits)

    def test_search_validates_shape_and_rejects_malformed_backend_results(self):
        index = TurboVecIndex(2, bits=4, turbovec_module=FakeTurboModule)
        index.add_with_ids(np.eye(2, dtype=np.float32), np.array([1, 2], np.uint64))
        with self.assertRaises(ResearchError) as dimension:
            index.search(np.ones((1, 3), np.float32), 1)
        self.assertEqual("query-dimension-invalid", dimension.exception.code)

        index._index.search = lambda queries, k: (np.array([[np.nan]]), np.array([[1]]))
        with self.assertRaises(ResearchError) as malformed:
            index.search(np.ones((1, 2), np.float32), 1)
        self.assertEqual("index-search-result-invalid", malformed.exception.code)

    def test_backend_failures_use_fixed_codes_without_private_details(self):
        private = "customer-secret-directory"

        class Broken(FakeTurboIndex):
            def write(self, path, durable=True):
                raise OSError(private)

            @classmethod
            def load(cls, path):
                raise OSError(private)

        module = type("Module", (), {"IdMapIndex": Broken})
        index = TurboVecIndex(2, bits=4, turbovec_module=module)
        index.add_with_ids(np.eye(2, dtype=np.float32), np.array([1, 2], np.uint64))
        with self.assertRaises(ResearchError) as write:
            index.write(private)
        with self.assertRaises(ResearchError) as load:
            TurboVecIndex.load(private, turbovec_module=module)
        self.assertEqual("index-write-failed", str(write.exception))
        self.assertEqual("index-load-failed", str(load.exception))
        self.assertNotIn(private, str(write.exception))


if __name__ == "__main__":
    unittest.main()
