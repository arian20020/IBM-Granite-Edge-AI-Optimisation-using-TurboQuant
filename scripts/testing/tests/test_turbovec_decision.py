import unittest

from scripts.testing.turbovec.contracts import Decision, Thresholds
from scripts.testing.turbovec.decision import ConfigurationResult, decide


def candidate(bits=4, recall=.95, ndcg=.98, p95=10.5, size=400, lifecycle=True):
    return ConfigurationResult(f"tq{bits}", bits, recall, ndcg, p95, size, lifecycle)


class DecisionTests(unittest.TestCase):
    def test_precedence_blocked_then_exclude(self):
        self.assertEqual(Decision.BLOCKED, decide([], exact_p95=10, exact_bytes=1000, prerequisites_complete=False).outcome)
        self.assertEqual(Decision.EXCLUDE, decide([candidate(lifecycle=False)], exact_p95=10, exact_bytes=1000, prerequisites_complete=True).outcome)

    def test_demonstrator_when_no_configuration_passes_all_thresholds(self):
        self.assertEqual(Decision.DEMONSTRATOR_ONLY, decide([candidate(size=600)], exact_p95=10, exact_bytes=1000, prerequisites_complete=True).outcome)

    def test_integrate_selects_deterministically(self):
        result = decide([candidate(2,p95=10.2,size=450), candidate(3,p95=10.1,size=480), candidate(4,p95=10.1,size=480)], exact_p95=10, exact_bytes=1000, prerequisites_complete=True)
        self.assertEqual(Decision.INTEGRATE, result.outcome); self.assertEqual("tq4", result.selected_configuration)
        self.assertTrue(all(item["passed"] for item in result.thresholds["tq4"].values()))

    def test_incomplete_metrics_never_integrate(self):
        with self.assertRaises(ValueError): decide([candidate(recall=None)], exact_p95=10, exact_bytes=1000, prerequisites_complete=True)


if __name__ == "__main__": unittest.main()
