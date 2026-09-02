import tempfile
import unittest
from pathlib import Path

import numpy as np

from scripts.testing.turbovec.indexes import ExactIndex, TurboVecIndex, stable_uint64_id


def unit_vectors(rows=12, dim=384, seed=7):
    values = np.random.default_rng(seed).normal(size=(rows, dim)).astype(np.float32)
    return np.ascontiguousarray(values / np.linalg.norm(values, axis=1, keepdims=True))


class IndexContractTests(unittest.TestCase):
    def factories(self):
        return [lambda: ExactIndex(384), *(lambda bits=bits: TurboVecIndex(384, bits) for bits in (2, 3, 4))]

    def test_lifecycle_remove_and_save_load(self):
        vectors = unit_vectors(); ids = np.arange(100, 112, dtype=np.uint64)
        for factory in self.factories():
            with self.subTest(factory=factory):
                index = factory(); index.add(vectors, ids)
                before = index.search(vectors[:2], 4); self.assertEqual((2, 4), before.ids.shape)
                self.assertTrue(index.remove(105)); self.assertFalse(index.remove(105))
                self.assertNotIn(105, index.search(vectors[5:6], 11).ids[0])
                with tempfile.TemporaryDirectory() as directory:
                    index.save(Path(directory)); reopened = type(index).load(Path(directory))
                    np.testing.assert_array_equal(index.search(vectors[:2], 4).ids, reopened.search(vectors[:2], 4).ids)

    def test_rejects_invalid_vectors_ids_queries_and_k(self):
        valid = unit_vectors(2); ids = np.array([1, 2], np.uint64)
        invalid = [valid[:, :383], np.full((2, 384), np.nan, np.float32), np.zeros((2, 384), np.float32), valid[:, ::-1]]
        for factory in self.factories():
            for values in invalid:
                with self.assertRaises(ValueError): factory().add(values, ids)
            with self.assertRaises(ValueError): factory().add(valid, np.array([1, 1], np.uint64))
            index = factory(); index.add(valid, ids)
            with self.assertRaises(ValueError): index.search(valid[:1], 0)

    def test_corrupt_save_is_rejected(self):
        for factory in self.factories():
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory); index = factory(); index.add(unit_vectors(2), np.array([1, 2], np.uint64)); index.save(root)
                target = next(path for path in root.iterdir() if path.name != "metadata.json")
                target.write_bytes(target.read_bytes() + b"corrupt")
                with self.assertRaises(ValueError): type(index).load(root)

    def test_stable_id_and_short_internal_filename(self):
        self.assertEqual(stable_uint64_id("a" * 64), stable_uint64_id("a" * 64))
        with self.assertRaises(ValueError): stable_uint64_id("not-a-hash")
        with tempfile.TemporaryDirectory() as directory:
            source_display_name = "x" * 200 + ".pdf"
            root = Path(directory) / "index"; root.mkdir()
            index = TurboVecIndex(384, 4); index.add(unit_vectors(1), np.array([1], np.uint64)); index.save(root)
            self.assertTrue((root / "index-4.tvim").is_file())
            self.assertFalse(any(source_display_name in path.name for path in root.iterdir()))


if __name__ == "__main__": unittest.main()
