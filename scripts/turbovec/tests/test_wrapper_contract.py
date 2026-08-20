import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


WRAPPER = Path(__file__).resolve().parents[1] / "Invoke-TurboVecResearch.ps1"
PACKAGE_ROOT = WRAPPER.parent
APPROVED_INPUT_EXAMPLE = PACKAGE_ROOT / "approved-input.example.json"
REQUIREMENTS_LOCK = PACKAGE_ROOT / "requirements.lock.txt"


def run_wrapper(*arguments: str, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            "powershell",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(WRAPPER),
            *arguments,
        ],
        capture_output=True,
        text=True,
        check=False,
        env=env,
    )


class WrapperContractTests(unittest.TestCase):
    def test_wrapper_uses_offline_module_invocation_without_command_evaluation(self) -> None:
        self.assertTrue(WRAPPER.is_file(), "TurboVec wrapper must exist")
        source = WRAPPER.read_text(encoding="utf-8")

        for required in (
            "GRANITE_TURBOVEC_PYTHON",
            "HF_HUB_OFFLINE",
            "TRANSFORMERS_OFFLINE",
            "$repoRoot",
            "'-m'",
            "'granite_turbovec.cli'",
        ):
            self.assertIn(required, source)

        self.assertNotIn("Invoke-Expression", source)
        self.assertNotIn("Start-Process", source)

    def test_missing_interpreter_returns_fixed_privacy_safe_error(self) -> None:
        env = os.environ.copy()
        env.pop("GRANITE_TURBOVEC_PYTHON", None)

        result = run_wrapper(env=env)

        self.assertEqual(2, result.returncode)
        self.assertEqual(
            "TurboVec Python interpreter is not configured or does not exist.",
            result.stderr.strip(),
        )

    def test_direct_invocation_preserves_arguments_environment_and_exit_code(self) -> None:
        with tempfile.TemporaryDirectory(prefix="turbovec wrapper ") as temporary_directory:
            root = Path(temporary_directory)
            fake_interpreter = root / "fake python.cmd"
            helper = root / "capture.py"
            output = root / "capture.json"
            helper.write_text(
                """import json, os, pathlib, sys
args = sys.argv[1:]
output = pathlib.Path(args[args.index('--output') + 1])
exit_code = int(args[args.index('--exit-code') + 1])
output.write_text(json.dumps({
    'args': args,
    'HF_HUB_OFFLINE': os.environ.get('HF_HUB_OFFLINE'),
    'TRANSFORMERS_OFFLINE': os.environ.get('TRANSFORMERS_OFFLINE'),
    'HF_HUB_DISABLE_TELEMETRY': os.environ.get('HF_HUB_DISABLE_TELEMETRY'),
    'PYTHONPATH': os.environ.get('PYTHONPATH'),
}), encoding='utf-8')
raise SystemExit(exit_code)
""",
                encoding="utf-8",
            )
            fake_interpreter.write_text(
                f'@echo off\r\n"{sys.executable}" "%~dp0capture.py" %*\r\nexit /b %ERRORLEVEL%\r\n',
                encoding="utf-8",
            )
            env = os.environ.copy()
            env["PYTHONPATH"] = "existing-pythonpath-marker"
            untrusted_argument = "literal;Write-Output should-not-run"

            result = run_wrapper(
                "-PythonPath",
                str(fake_interpreter),
                "--output",
                str(output),
                "--exit-code",
                "7",
                untrusted_argument,
                env=env,
            )

            self.assertEqual(7, result.returncode)
            captured = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(["-m", "granite_turbovec.cli", "--output"], captured["args"][:3])
            self.assertEqual("capture.json", Path(captured["args"][3]).name)
            self.assertEqual(["--exit-code", "7", untrusted_argument], captured["args"][4:])
            self.assertEqual("1", captured["HF_HUB_OFFLINE"])
            self.assertEqual("1", captured["TRANSFORMERS_OFFLINE"])
            self.assertEqual("1", captured["HF_HUB_DISABLE_TELEMETRY"])
            self.assertEqual(
                f"{PACKAGE_ROOT}{os.pathsep}existing-pythonpath-marker",
                captured["PYTHONPATH"],
            )

    def test_environment_interpreter_is_used_when_parameter_is_omitted(self) -> None:
        with tempfile.TemporaryDirectory(prefix="turbovec env wrapper ") as temporary_directory:
            fake_interpreter = Path(temporary_directory) / "environment python.cmd"
            fake_interpreter.write_text("@exit /b 13\r\n", encoding="utf-8")
            env = os.environ.copy()
            env["GRANITE_TURBOVEC_PYTHON"] = str(fake_interpreter)

            result = run_wrapper("--literal-argument", env=env)

            self.assertEqual(13, result.returncode)


class ResearchArtifactContractTests(unittest.TestCase):
    def test_approved_input_example_is_valid_and_unambiguously_placeholder_only(self) -> None:
        self.assertTrue(APPROVED_INPUT_EXAMPLE.is_file(), "approved input example must exist")
        approved_input = json.loads(APPROVED_INPUT_EXAMPLE.read_text(encoding="utf-8"))

        self.assertEqual(1, approved_input["schema_version"])
        self.assertEqual("3.12.10", approved_input["python_version"])
        self.assertEqual("1.0.0", approved_input["turbovec_version"])
        self.assertEqual(
            "ccab9f325e6ce2a270a87daf01ae4e443bcf2d49",
            approved_input["turbovec_source_commit"],
        )
        self.assertEqual("BAAI/bge-small-en-v1.5", approved_input["embedding_model"])
        self.assertEqual("MIT", approved_input["embedding_model_license"])
        self.assertEqual(r"C:\approved\fastembed-cache", approved_input["model_cache_root"])
        self.assertEqual("example_only_replace_hashes_before_use", approved_input["approval_status"])

        self.assertEqual("0" * 64, approved_input["turbovec_wheel_sha256"])
        self.assertEqual("f" * 64, approved_input["embedding_model_manifest_sha256"])
        self.assertRegex(approved_input["turbovec_wheel_sha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(approved_input["embedding_model_manifest_sha256"], r"^[0-9a-f]{64}$")

    def test_requirements_lock_contains_only_exact_pins_from_controlled_stack(self) -> None:
        self.assertTrue(REQUIREMENTS_LOCK.is_file(), "requirements lock must exist")
        pins: dict[str, str] = {}
        for line in REQUIREMENTS_LOCK.read_text(encoding="utf-8").splitlines():
            if not line:
                continue
            self.assertRegex(line, r"^[A-Za-z0-9_.-]+==[^\s/@\\]+$")
            name, version = line.split("==", 1)
            pins[name.lower()] = version

        self.assertEqual("2.5.2", pins["numpy"])
        self.assertEqual("0.8.0", pins["fastembed"])
        self.assertEqual("1.29.0", pins["onnxruntime"])
        self.assertEqual("1.0.0", pins["turbovec"])


if __name__ == "__main__":
    unittest.main()
