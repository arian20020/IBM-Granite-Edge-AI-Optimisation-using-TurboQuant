import unittest

from scripts.testing.turbovec.memory import MemorySample, memory_accounting
from scripts.testing.turbovec.storage import StorageComponents, compare_storage


class AccountingTests(unittest.TestCase):
    def test_storage_ratio_uses_equivalent_total_bytes_in_correct_direction(self):
        exact = StorageComponents(800, 100, 50, 30, 20)
        candidate = StorageComponents(300, 100, 50, 30, 20)
        comparison = compare_storage(exact, candidate, vectors=10)
        self.assertEqual(1_000, comparison["exact_equivalent_bytes"])
        self.assertEqual(500, comparison["candidate_equivalent_bytes"])
        self.assertEqual(2.0, comparison["storage_ratio"])
        self.assertEqual(50.0, comparison["candidate_bytes_per_vector"])

    def test_storage_components_reject_negative_or_zero_vector_count(self):
        with self.assertRaisesRegex(ValueError, "non-negative"):
            StorageComponents(-1, 0, 0, 0, 0)
        with self.assertRaisesRegex(ValueError, "vector count"):
            compare_storage(StorageComponents(1, 0, 0, 0, 0), StorageComponents(1, 0, 0, 0, 0), vectors=0)

    def test_memory_accounting_distinguishes_process_system_and_incremental(self):
        before = MemorySample(16_000, 8_000, 9_000, 1_000, 500, 100)
        after = MemorySample(16_000, 7_000, 10_500, 1_500, 900, 250)
        result = memory_accounting(before, after, peak_process_working_set_bytes=1_100)
        self.assertEqual(400, result["incremental_process_working_set_bytes"])
        self.assertEqual(1_100, result["peak_process_working_set_bytes"])
        self.assertEqual(-1_000, result["available_system_ram_delta_bytes"])
        self.assertEqual(1_500, result["committed_memory_delta_bytes"])
        self.assertEqual(150, result["shared_gpu_memory_delta_bytes"])


if __name__ == "__main__":
    unittest.main()
