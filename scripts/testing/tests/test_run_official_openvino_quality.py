import hashlib
import json
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.testing.run_official_openvino_quality import (
    QualityConfiguration,
    atomic_write_json,
    load_prompt_contract,
    parse_json_bytes_strict,
    require_runtime_summary,
    run_quality_campaign,
)


ROOT = Path(__file__).resolve().parents[3]
PROMPT_SET = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "prompts"
    / "fixed-feasibility-prompt-set-v1.json"
)
RENDERED = PROMPT_SET.parent / "rendered"


COMPLETE_OUTPUTS = {
    "P1": (
        "- Benefit: A smaller KV cache reduces memory use.\n"
        "- Benefit: It leaves capacity for offline inference.\n"
        "- Limitation: Quantisation can reduce answer quality.\n"
        "- Limitation: Hardware support can vary.\n"
        "- Check: Compare accuracy and memory before deployment."
    ),
    "P2": (
        "BENEFIT: It reduces cache memory.\n"
        "LIMITATION: It can reduce quality.\n"
        "CHECK: Compare results with an uncompressed cache."
    ),
    "P3": json.dumps(
        {
            "optimisation": "TurboQuant",
            "memory_effect": "reduces KV-cache memory",
            "quality_risk": "may reduce answer quality",
            "verification": "compare against the baseline",
        }
    ),
    "P4": (
        "IBM Granite 4.1 3B ran through upstream llama.cpp with Q4_K_M weights "
        "and Q8_0 K and V caches at a 4096-token context. "
        "The baseline ran locally on a Windows Intel laptop, and TurboQuant was "
        "not active."
    ),
    "P5": "MARKER:IXN-TQ-7319",
    "P6": "amber:4821",
}


def contains_null(value):
    if value is None:
        return True
    if isinstance(value, dict):
        return any(contains_null(item) for item in value.values())
    if isinstance(value, list):
        return any(contains_null(item) for item in value)
    return False


class RecordingExecutor:
    def __init__(self, fail_once_at=None):
        self.fail_once_at = fail_once_at
        self.calls = []

    def __call__(self, configuration, request_path, response_path):
        request = json.loads(request_path.read_text(encoding="utf-8"))
        prompt_id = request["prompt_id"]
        self.calls.append((configuration.blind_label, prompt_id))
        if self.fail_once_at == prompt_id:
            self.fail_once_at = None
            raise RuntimeError("simulated worker crash")
        result = {"status": "complete", "output": COMPLETE_OUTPUTS[prompt_id]}
        if prompt_id == "P6":
            result["turn_1"] = "SAVED"
        response_path.write_text(json.dumps(result), encoding="utf-8")


class OfficialOpenVINOQualityRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.runtime = self.root / "runtime.json"
        self.configuration_sha256 = "a" * 64
        self.runtime.write_text(
            json.dumps(
                {
                    "test_id": "OV-TQ-03",
                    "status": "complete",
                    "accepted": True,
                    "configuration_sha256": self.configuration_sha256,
                    "cleanup_process_count": 0,
                }
            ),
            encoding="utf-8",
        )
        self.configuration = QualityConfiguration(
            test_id="OV-TQ-03",
            blind_label="response-A7",
            configuration_sha256=self.configuration_sha256,
            runtime_summary_path=self.runtime,
            executor_command=(),
        )
        self.output = self.root / "quality"

    def tearDown(self):
        self.temporary.cleanup()

    def run_campaign(self, executor, *, resume=False, configurations=None):
        return run_quality_campaign(
            configurations or [self.configuration],
            prompt_set_path=PROMPT_SET,
            rendered_root=RENDERED,
            output_root=self.output,
            executor=executor,
            resume=resume,
        )

    def test_campaign_identity_rejects_null_outside_governed_optional_fields(self):
        with self.assertRaisesRegex(
            ValueError,
            r"null value is prohibited at \$\.unexpected",
        ):
            parse_json_bytes_strict(
                b'{"unexpected":null}',
                source="campaign-identity.json",
            )

    def test_campaign_identity_rejects_dotted_key_aliases_of_optional_fields(self):
        aliases = (
            b'{"identity.matrix.case.artifact_terminal_path":null}',
            b'{"identity":{"matrix":{"case.artifact_terminal_sha256":null}}}',
        )
        for raw in aliases:
            with self.subTest(raw=raw):
                with self.assertRaisesRegex(ValueError, "null value is prohibited"):
                    parse_json_bytes_strict(
                        raw,
                        source="campaign-identity.json",
                    )

    def test_quality_requires_complete_runtime_summary(self):
        with self.assertRaisesRegex(RuntimeError, "complete runtime evidence"):
            require_runtime_summary("OV-TQ-03", None)

    def test_frozen_prompt_sources_reject_same_id_substitution(self):
        prompt_root = self.root / "prompts"
        shutil.copytree(PROMPT_SET.parent, prompt_root)
        copied_prompt_set = prompt_root / PROMPT_SET.name
        copied_rendered = prompt_root / "rendered"

        p1 = copied_rendered / "P1.txt"
        p1.write_text(
            p1.read_text(encoding="utf-8") + "\nSubstituted instruction.",
            encoding="utf-8",
        )
        with self.assertRaisesRegex(ValueError, "frozen rendered prompt hash"):
            load_prompt_contract(copied_prompt_set, copied_rendered)

        shutil.rmtree(prompt_root)
        shutil.copytree(PROMPT_SET.parent, prompt_root)
        copied_prompt_set = prompt_root / PROMPT_SET.name
        prompt_payload = json.loads(copied_prompt_set.read_text(encoding="utf-8"))
        prompt_payload["purpose"] = "same ID, substituted prompt set"
        copied_prompt_set.write_text(json.dumps(prompt_payload), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "frozen prompt-set hash"):
            load_prompt_contract(copied_prompt_set, prompt_root / "rendered")

    def test_prompt_contract_parses_the_exact_bytes_that_were_hashed(self):
        altered = json.loads(PROMPT_SET.read_text(encoding="utf-8"))
        altered["generation_defaults"]["seed"] = 999
        original_read_text = Path.read_text

        def switched_read_text(path, *args, **kwargs):
            if path == PROMPT_SET:
                return json.dumps(altered)
            return original_read_text(path, *args, **kwargs)

        with patch.object(Path, "read_text", switched_read_text):
            contract = load_prompt_contract(PROMPT_SET, RENDERED)
        self.assertEqual(contract["generation_settings"]["seed"], 42)

    def test_atomic_publication_cannot_clobber_a_racing_writer(self):
        target = self.root / "evidence.json"
        real_link = os.link

        def racing_link(source, destination):
            Path(destination).write_text("competitor", encoding="utf-8")
            return real_link(source, destination)

        with patch(
            "scripts.testing.run_official_openvino_quality.os.link",
            side_effect=racing_link,
        ):
            with self.assertRaises(FileExistsError):
                atomic_write_json(target, {"status": "complete"})
        self.assertEqual(target.read_text(encoding="utf-8"), "competitor")
        self.assertEqual(list(self.root.glob("*.tmp")), [])

    def test_crash_leaves_atomic_prompt_boundary_and_resume_skips_valid_work(self):
        first = RecordingExecutor(fail_once_at="P3")
        with self.assertRaisesRegex(RuntimeError, "simulated worker crash"):
            self.run_campaign(first)

        row = self.output / "response-A7"
        self.assertTrue((row / "P1" / "response.json").is_file())
        self.assertTrue((row / "P2" / "response.json").is_file())
        self.assertTrue((row / "P3" / "request.json").is_file())
        self.assertFalse((row / "P3" / "response.json").exists())
        self.assertFalse((row / "completion.json").exists())
        self.assertEqual(list(row.rglob("*.tmp")), [])

        resumed = RecordingExecutor()
        result = self.run_campaign(resumed, resume=True)
        self.assertEqual(
            resumed.calls,
            [("response-A7", prompt_id) for prompt_id in ("P3", "P4", "P5", "P6")],
        )
        self.assertEqual(result["status"], "complete")

        completion = json.loads(
            (row / "completion.json").read_text(encoding="utf-8")
        )
        self.assertEqual(set(completion["prompts"]), {f"P{i}" for i in range(1, 7)})
        self.assertTrue(
            all(item["status"] == "complete" for item in completion["prompts"].values())
        )
        p6_response = json.loads(
            (row / "P6" / "response.json").read_text(encoding="utf-8")
        )
        self.assertNotEqual(
            p6_response["response_sha256"], p6_response["output_sha256"]
        )
        self.assertEqual(
            completion["prompts"]["P6"]["response_sha256"],
            p6_response["response_sha256"],
        )
        self.assertFalse(contains_null(completion))

        final_resume = RecordingExecutor()
        self.run_campaign(final_resume, resume=True)
        self.assertEqual(final_resume.calls, [])

    def test_existing_artifacts_are_never_overwritten_without_resume(self):
        self.run_campaign(RecordingExecutor())
        response = self.output / "response-A7" / "P1" / "response.json"
        before = response.read_bytes()

        executor = RecordingExecutor()
        with self.assertRaises(FileExistsError):
            self.run_campaign(executor)
        self.assertEqual(executor.calls, [])
        self.assertEqual(response.read_bytes(), before)

    def test_completed_resume_rejects_private_identity_tampering(self):
        self.run_campaign(RecordingExecutor())
        row = self.output / "response-A7"
        request_path = row / "P1" / "request.json"
        response_path = row / "P1" / "response.json"
        completion_path = row / "completion.json"
        request = json.loads(request_path.read_text(encoding="utf-8"))
        response = json.loads(response_path.read_text(encoding="utf-8"))
        completion = json.loads(completion_path.read_text(encoding="utf-8"))

        request["configuration_sha256"] = "c" * 64
        unsigned = {
            key: value for key, value in request.items() if key != "request_sha256"
        }
        request["request_sha256"] = hashlib.sha256(
            json.dumps(
                unsigned,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        response["request_sha256"] = request["request_sha256"]
        completion["prompts"]["P1"]["request_sha256"] = request["request_sha256"]
        request_path.write_text(json.dumps(request), encoding="utf-8")
        response_path.write_text(json.dumps(response), encoding="utf-8")
        completion_path.write_text(json.dumps(completion), encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "configuration identity"):
            self.run_campaign(RecordingExecutor(), resume=True)

    def test_runtime_identity_must_be_complete_and_exact_before_output_is_created(self):
        bad = json.loads(self.runtime.read_text(encoding="utf-8"))
        bad["configuration_sha256"] = "b" * 64
        self.runtime.write_text(json.dumps(bad), encoding="utf-8")

        with self.assertRaisesRegex(
            RuntimeError, "accepted complete runtime evidence"
        ):
            self.run_campaign(RecordingExecutor())
        self.assertFalse(self.output.exists())

    def test_blind_label_cannot_reveal_comparison_role_or_format(self):
        for label in ("baseline", "optimized-GPU", "TBQ3-response"):
            with self.subTest(label=label):
                configuration = QualityConfiguration(
                    test_id=self.configuration.test_id,
                    blind_label=label,
                    configuration_sha256=self.configuration.configuration_sha256,
                    runtime_summary_path=self.configuration.runtime_summary_path,
                    executor_command=(),
                )
                with self.assertRaisesRegex(ValueError, "blind_label"):
                    self.run_campaign(
                        RecordingExecutor(), configurations=[configuration]
                    )
                self.assertFalse(self.output.exists())

    def test_requests_are_label_blind_and_p6_uses_actual_turn_one_history(self):
        self.run_campaign(RecordingExecutor())
        row = self.output / "response-A7"
        all_json = "\n".join(
            path.read_text(encoding="utf-8") for path in sorted(row.rglob("*.json"))
        )
        self.assertNotIn("OV-TQ-03", all_json)
        self.assertNotIn("TBQ3", all_json)
        self.assertNotIn('"u3"', all_json)

        request = json.loads(
            (row / "P6" / "request.json").read_text(encoding="utf-8")
        )
        self.assertEqual(request["execution"]["mode"], "multi_turn")
        self.assertEqual(
            request["execution"]["history_policy"], "actual_turn_1_output"
        )
        self.assertEqual(
            set(request["generation_settings"]),
            {"temperature", "top_p", "seed", "max_output_tokens"},
        )

    def test_configurations_execute_serially_in_p1_to_p6_order(self):
        runtime_b = self.root / "runtime-b.json"
        runtime_b.write_text(
            json.dumps(
                {
                    "test_id": "OV-TQ-04",
                    "status": "passed",
                    "accepted": True,
                    "configuration_sha256": "b" * 64,
                    "cleanup_process_count": 0,
                }
            ),
            encoding="utf-8",
        )
        configuration_b = QualityConfiguration(
            test_id="OV-TQ-04",
            blind_label="response-K2",
            configuration_sha256="b" * 64,
            runtime_summary_path=runtime_b,
            executor_command=(),
        )
        executor = RecordingExecutor()
        self.run_campaign(
            executor, configurations=[self.configuration, configuration_b]
        )
        self.assertEqual(
            executor.calls,
            [
                (label, f"P{number}")
                for label in ("response-A7", "response-K2")
                for number in range(1, 7)
            ],
        )

    def test_file_worker_command_contract_executes_without_runtime_imports(self):
        worker_code = (
            "import json,sys;"
            "request=json.load(open(sys.argv[1],encoding='utf-8'));"
            f"outputs=json.loads({json.dumps(json.dumps(COMPLETE_OUTPUTS))});"
            "result={'status':'complete','output':outputs[request['prompt_id']]};"
            "result.update({'turn_1':'SAVED'} if request['prompt_id']=='P6' else {});"
            "json.dump(result,open(sys.argv[2],'w',encoding='utf-8'))"
        )
        configuration = QualityConfiguration(
            test_id=self.configuration.test_id,
            blind_label=self.configuration.blind_label,
            configuration_sha256=self.configuration.configuration_sha256,
            runtime_summary_path=self.configuration.runtime_summary_path,
            executor_command=(
                sys.executable,
                "-c",
                worker_code,
                "{request_json}",
                "{response_json}",
            ),
        )

        result = run_quality_campaign(
            [configuration],
            prompt_set_path=PROMPT_SET,
            rendered_root=RENDERED,
            output_root=self.output,
        )
        self.assertEqual(result["status"], "complete")
        self.assertTrue(
            (self.output / "response-A7" / "completion.json").is_file()
        )

    def test_null_worker_output_cannot_be_published(self):
        def empty_executor(_configuration, request_path, response_path):
            prompt_id = json.loads(request_path.read_text(encoding="utf-8"))[
                "prompt_id"
            ]
            response_path.write_text(
                json.dumps({"status": "complete", "output": None}),
                encoding="utf-8",
            )
            self.assertEqual(prompt_id, "P1")

        with self.assertRaisesRegex(ValueError, "null"):
            self.run_campaign(empty_executor)
        self.assertFalse(
            (
                self.output
                / "response-A7"
                / "P1"
                / "response.json"
            ).exists()
        )


if __name__ == "__main__":
    unittest.main()
