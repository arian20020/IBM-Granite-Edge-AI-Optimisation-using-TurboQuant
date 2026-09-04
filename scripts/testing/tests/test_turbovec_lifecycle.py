from pathlib import Path
import tempfile
import unittest

import numpy as np

from scripts.testing.turbovec.embedding import DeterministicEmbeddingProvider
from scripts.testing.turbovec.indexes import ExactIndex, TurboVecIndex
from scripts.testing.turbovec.lifecycle import execute_lifecycle


class LifecycleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        provider = DeterministicEmbeddingProvider()
        cls.vectors = provider.embed_documents([f"document {i}" for i in range(20)])
        cls.queries = provider.embed_queries([f"document {i}" for i in range(4)])
        cls.ids = np.arange(1, 21, dtype=np.uint64)

    def test_exact_and_turbovec_complete_save_reload_corruption_and_cleanup(self):
        factories = [lambda: ExactIndex(), lambda: TurboVecIndex(bit_width=4)]
        for factory in factories:
            with self.subTest(factory=factory), tempfile.TemporaryDirectory() as directory:
                result = execute_lifecycle(factory, self.vectors, self.ids, self.queries, Path(directory) / "index")
                self.assertTrue(result.build_completed)
                self.assertTrue(result.query_completed)
                self.assertTrue(result.save_completed)
                self.assertTrue(result.reload_completed)
                self.assertTrue(result.post_reload_equal)
                self.assertTrue(result.corruption_rejected)
                self.assertTrue(result.cleanup_completed)
                self.assertFalse((Path(directory) / "index").exists())

    def test_cancelled_and_interrupted_publication_never_leave_valid_index(self):
        for mode in ("cancelled", "interrupted"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as directory:
                target = Path(directory) / "index"
                result = execute_lifecycle(
                    lambda: ExactIndex(), self.vectors, self.ids, self.queries, target, stop_before_publish=mode
                )
                self.assertFalse(result.save_completed)
                self.assertTrue(result.cleanup_completed)
                self.assertFalse(target.exists())
                self.assertEqual(mode, result.terminal_status)


if __name__ == "__main__":
    unittest.main()
