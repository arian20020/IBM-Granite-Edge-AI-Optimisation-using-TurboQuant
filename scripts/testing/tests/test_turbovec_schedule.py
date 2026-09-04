from collections import Counter, defaultdict
import unittest

from scripts.testing.turbovec.schedule import CONFIGURATIONS, build_schedule


class ScheduleTests(unittest.TestCase):
    def test_five_repetitions_are_deterministic_complete_and_counterbalanced(self):
        first = build_schedule(seed=240904, repetitions=5, query_count=128)
        second = build_schedule(seed=240904, repetitions=5, query_count=128)
        self.assertEqual(first, second)
        self.assertEqual(5, len(first))
        positions = defaultdict(Counter)
        for repetition in first:
            self.assertEqual(set(CONFIGURATIONS), set(repetition.configuration_order))
            self.assertEqual(128, len(repetition.query_order))
            self.assertEqual(set(range(128)), set(repetition.query_order))
            for position, name in enumerate(repetition.configuration_order):
                positions[name][position] += 1
        for counts in positions.values():
            self.assertLessEqual(max(counts.values()) - min(counts.values()), 1)

    def test_schedule_rejects_incomplete_experiment(self):
        with self.assertRaisesRegex(ValueError, "repetitions"):
            build_schedule(seed=1, repetitions=0, query_count=10)
        with self.assertRaisesRegex(ValueError, "query count"):
            build_schedule(seed=1, repetitions=5, query_count=0)


if __name__ == "__main__":
    unittest.main()
