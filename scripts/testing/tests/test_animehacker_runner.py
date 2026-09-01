import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.campaigns.animehacker.matrix import TestCase


def case(**overrides):
    values = dict(test_id="AH-01", phase="runtime", description="test", model_id="model",
                  model_path_env="MODEL", format="standard", cache="f16", backend="cpu",
                  context=1024, guard="none", status="planned",
                  activation_requirement="standard-cache", required_metrics=frozenset(),
                  safety_requirements=(), quality_required=True, formal_repetitions=3,
                  excluded_warmup=True)
    values.update(overrides)
    return TestCase(**values)


def complete_runtime_summary(test_id="AH-09"):
    from scripts.testing.run_animehacker_retest import FORMAL_FIELDS

    sample = {field: 1.0 for field in FORMAL_FIELDS}
    sample.update({"valid": True, "missing": [], "extended_missing": [],
                   "utilization": {
                       "cpu_percent": {"mean": 1.0, "median": 1.0, "peak": 1.0,
                                       "sample_count": 1},
                       "gpu_percent": {"mean": 1.0, "median": 1.0, "peak": 1.0,
                                       "sample_count": 1},
                   }})
    aggregate = {field: {"mean": 1.0, "median": 1.0, "min": 1.0, "max": 1.0}
                 for field in FORMAL_FIELDS}
    aggregate.update({
        "cpu_percent": {"mean": 1.0, "median": 1.0, "peak": 1.0, "sample_count": 3},
        "gpu_percent": {"mean": 1.0, "median": 1.0, "peak": 1.0, "sample_count": 3},
    })
    return {"test_id": test_id, "samples": [dict(sample) for _ in range(3)],
            "aggregate": aggregate,
            "activation": [{"activated": True} for _ in range(3)]}


class AnimehackerRunnerTests(unittest.TestCase):
    def test_quality_selection_accepts_recovered_rows(self):
        from scripts.testing.run_animehacker_quality import quality_case_ids

        self.assertIn("AH-09", quality_case_ids())
        self.assertIn("AH-10", quality_case_ids())

    def test_recovered_quality_rejects_missing_runtime_summary(self):
        from scripts.testing.run_animehacker_quality import runtime_summary_is_complete

        self.assertFalse(runtime_summary_is_complete("AH-09", None))

    def test_recovered_quality_rejects_invalid_runtime_summary(self):
        from scripts.testing.run_animehacker_quality import runtime_summary_is_complete

        summary = complete_runtime_summary()
        summary["samples"].pop()
        self.assertFalse(runtime_summary_is_complete("AH-09", summary))

    def test_recovered_quality_accepts_complete_three_sample_runtime_summary(self):
        from scripts.testing.run_animehacker_quality import runtime_summary_is_complete

        self.assertTrue(runtime_summary_is_complete("AH-09", complete_runtime_summary()))

    def test_recovered_quality_requires_runtime_summary_file(self):
        from scripts.testing.run_animehacker_quality import require_recovered_runtime_summaries

        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(RuntimeError, "AH-09 requires complete runtime evidence"):
                require_recovered_runtime_summaries([case(test_id="AH-09")], Path(directory))

    def test_large_host_cpu_rows_require_runtime_summary_file(self):
        from scripts.testing.run_animehacker_quality import require_recovered_runtime_summaries

        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(RuntimeError, "AH-06 requires complete runtime evidence"):
                require_recovered_runtime_summaries([case(test_id="AH-06")], Path(directory))

    def test_recovered_quality_preflight_permits_valid_runtime_summary(self):
        from scripts.testing.run_animehacker_quality import require_recovered_runtime_summaries

        with tempfile.TemporaryDirectory() as directory:
            summary_path = Path(directory) / "AH-10" / "summary.json"
            summary_path.parent.mkdir()
            summary_path.write_text(json.dumps(complete_runtime_summary("AH-10")), encoding="utf-8")
            require_recovered_runtime_summaries([case(test_id="AH-10")], Path(directory))

    def test_existing_quality_rows_do_not_require_runtime_summary(self):
        from scripts.testing.run_animehacker_quality import require_recovered_runtime_summaries

        with tempfile.TemporaryDirectory() as directory:
            require_recovered_runtime_summaries([case(test_id="AH-01")], Path(directory))

    def test_command_has_exact_cache_backend_and_safety_flags(self):
        from scripts.testing.campaigns.animehacker.runner import build_server_command

        command = build_server_command(case(cache="tq3_0"), Path("server.exe"), Path("model.gguf"), 19001)
        self.assertIn(["-ctk", "tq3_0"], [command[i:i + 2] for i in range(len(command) - 1)])
        self.assertIn(["-ctv", "tq3_0"], [command[i:i + 2] for i in range(len(command) - 1)])
        self.assertIn(["-ngl", "0"], [command[i:i + 2] for i in range(len(command) - 1)])
        self.assertIn("--offline", command)

    def test_selection_resumes_only_reconciled_rows(self):
        from scripts.testing.campaigns.animehacker.runner import select_cases

        cases = [case(test_id="AH-01"), case(test_id="AH-02")]
        state = {"attempts": {"AH-01": {"status": "complete", "reconciled": True}}}
        self.assertEqual([item.test_id for item in select_cases(cases, resume_state=state)], ["AH-02"])
        with self.assertRaisesRegex(ValueError, "unknown test id"):
            select_cases(cases, only={"AH-99"})

    def test_tq_sycl_uses_proven_level_zero_partial_offload_flags(self):
        from scripts.testing.campaigns.animehacker.runner import build_server_command

        command = build_server_command(case(backend="sycl-partial", cache="tq3_0"),
                                       Path("server.exe"), Path("model.gguf"), 19003)
        pairs = [command[i:i + 2] for i in range(len(command) - 1)]
        self.assertIn(["-ngl", "1"], pairs)
        self.assertIn(["-sm", "none"], pairs)
        self.assertIn(["-mg", "0"], pairs)
        self.assertNotIn(["-ot", "output.*=SYCL0"], pairs)
        self.assertNotIn(["-fa", "off"], pairs)

    def test_non_tq_sycl_retains_forced_flash_attention_off(self):
        from scripts.testing.campaigns.animehacker.runner import build_server_command

        command = build_server_command(case(backend="sycl-partial", cache="f16"),
                                       Path("server.exe"), Path("model.gguf"), 19002)
        self.assertIn(["-fa", "off"],
                      [command[i:i + 2] for i in range(len(command) - 1)])

    def test_runtime_environment_distinguishes_tq3_from_non_tq3_sycl(self):
        from scripts.testing.run_animehacker_retest import runtime_environment

        self.assertEqual(
            runtime_environment(case(backend="sycl-partial", cache="tq3_0")),
            {"ONEAPI_DEVICE_SELECTOR": "level_zero:0"},
        )
        self.assertEqual(
            runtime_environment(case(backend="sycl-partial", cache="f16")),
            {"ONEAPI_DEVICE_SELECTOR": "opencl:gpu", "GGML_SYCL_ENABLE_FLASH_ATTN": "0"},
        )
        self.assertEqual(runtime_environment(case()), {})

    def test_tq_sycl_runtime_prompt_uses_granite_role_tokens(self):
        from scripts.testing.campaigns.animehacker.runner import format_runtime_prompt

        item = case(backend="sycl-partial", cache="tq3_0")
        self.assertEqual(
            format_runtime_prompt(item, "Question"),
            "<|start_of_role|>user<|end_of_role|>Question<|end_of_text|>\n"
            "<|start_of_role|>assistant<|end_of_role|>",
        )

    def test_cpu_runtime_prompt_is_unchanged(self):
        from scripts.testing.campaigns.animehacker.runner import format_runtime_prompt

        self.assertEqual(format_runtime_prompt(case(), "Question"), "Question")

    def test_unique_run_ids_survive_interrupted_attempts(self):
        from scripts.testing.campaigns.animehacker.runner import next_run_id

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "AH-01-2026-07-18-R0001").mkdir()
            (root / "AH-01-2026-07-18-R0003").mkdir()
            self.assertEqual(next_run_id("AH-01", "2026-07-18", root), "AH-01-2026-07-18-R0004")


if __name__ == "__main__":
    unittest.main()
