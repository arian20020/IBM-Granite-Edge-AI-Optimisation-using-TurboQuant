import tempfile
import unittest
from pathlib import Path

from scripts.testing.animehacker.matrix import TestCase


def case(**overrides):
    values = dict(test_id="AH-01", phase="runtime", description="test", model_id="model",
                  model_path_env="MODEL", format="standard", cache="f16", backend="cpu",
                  context=1024, guard="none", status="planned",
                  activation_requirement="standard-cache", required_metrics=frozenset(),
                  safety_requirements=(), quality_required=True, formal_repetitions=3,
                  excluded_warmup=True)
    values.update(overrides)
    return TestCase(**values)


class AnimehackerRunnerTests(unittest.TestCase):
    def test_command_has_exact_cache_backend_and_safety_flags(self):
        from scripts.testing.animehacker.runner import build_server_command

        command = build_server_command(case(cache="tq3_0"), Path("server.exe"), Path("model.gguf"), 19001)
        self.assertIn(["-ctk", "tq3_0"], [command[i:i + 2] for i in range(len(command) - 1)])
        self.assertIn(["-ctv", "tq3_0"], [command[i:i + 2] for i in range(len(command) - 1)])
        self.assertIn(["-ngl", "0"], [command[i:i + 2] for i in range(len(command) - 1)])
        self.assertIn("--offline", command)

    def test_selection_resumes_only_reconciled_rows(self):
        from scripts.testing.animehacker.runner import select_cases

        cases = [case(test_id="AH-01"), case(test_id="AH-02")]
        state = {"attempts": {"AH-01": {"status": "complete", "reconciled": True}}}
        self.assertEqual([item.test_id for item in select_cases(cases, resume_state=state)], ["AH-02"])
        with self.assertRaisesRegex(ValueError, "unknown test id"):
            select_cases(cases, only={"AH-99"})

    def test_sycl_explicitly_disables_flash_attention(self):
        from scripts.testing.animehacker.runner import build_server_command

        command = build_server_command(case(backend="sycl-partial"), Path("server.exe"),
                                       Path("model.gguf"), 19002)
        self.assertIn(["-fa", "off"], [command[i:i + 2] for i in range(len(command) - 1)])

    def test_tq_sycl_keeps_attention_on_cpu_and_offloads_output_tensor(self):
        from scripts.testing.animehacker.runner import build_server_command

        command = build_server_command(case(backend="sycl-partial", cache="tq3_0"),
                                       Path("server.exe"), Path("model.gguf"), 19003)
        pairs = [command[i:i + 2] for i in range(len(command) - 1)]
        self.assertIn(["-ngl", "0"], pairs)
        self.assertIn(["-ot", "output.*=SYCL0"], pairs)

    def test_unique_run_ids_survive_interrupted_attempts(self):
        from scripts.testing.animehacker.runner import next_run_id

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "AH-01-2026-07-18-R0001").mkdir()
            (root / "AH-01-2026-07-18-R0003").mkdir()
            self.assertEqual(next_run_id("AH-01", "2026-07-18", root), "AH-01-2026-07-18-R0004")


if __name__ == "__main__":
    unittest.main()
