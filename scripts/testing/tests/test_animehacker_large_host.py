import json
import copy
import tempfile
import unittest
from pathlib import Path

from scripts.testing.animehacker.large_host import (
    HostInputs,
    allocate_run_root,
    preflight,
    write_manifest,
)


GIB = 1024 ** 3


class AnimehackerLargeHostTests(unittest.TestCase):
    def inputs(self, root: Path, *, selected=("AH-06", "AH-07", "AH-10")) -> HostInputs:
        paths = {
            "matrix": root / "matrix.json",
            "cpu_server": root / "cpu" / "bin" / "llama-server.exe",
            "sycl_server": root / "sycl" / "bin" / "llama-server.exe",
            "granite8_model": root / "models" / "granite-8b.gguf",
        }
        for path in paths.values():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(path.name.encode("utf-8"))
        return HostInputs(selected=frozenset(selected), **paths)

    def test_preflight_rejects_less_than_32_gib(self):
        with tempfile.TemporaryDirectory() as directory:
            result = preflight(
                self.inputs(Path(directory)),
                installed_ram_bytes=32 * GIB - 1,
                active_process_names=(),
                level_zero_devices=("Intel UHD Graphics",),
            )
        self.assertFalse(result.accepted)
        self.assertIn("at least 32 GiB", " ".join(result.errors))

    def test_preflight_accepts_32_gib_with_all_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            result = preflight(
                self.inputs(Path(directory)),
                installed_ram_bytes=32 * GIB,
                active_process_names=(),
                level_zero_devices=("Intel UHD Graphics",),
            )
        self.assertTrue(result.accepted, result.errors)
        self.assertEqual(result.installed_ram_bytes, 32 * GIB)
        self.assertEqual(len(result.file_records), 4)
        self.assertTrue(all(record["sha256"] for record in result.file_records.values()))

    def test_preflight_names_missing_required_file(self):
        with tempfile.TemporaryDirectory() as directory:
            inputs = self.inputs(Path(directory))
            inputs.granite8_model.unlink()
            result = preflight(inputs, installed_ram_bytes=32 * GIB,
                               active_process_names=(), level_zero_devices=("GPU",))
        self.assertFalse(result.accepted)
        self.assertIn("granite8_model", " ".join(result.errors))

    def test_preflight_rejects_active_llama_or_controller_process(self):
        with tempfile.TemporaryDirectory() as directory:
            result = preflight(self.inputs(Path(directory)), installed_ram_bytes=32 * GIB,
                               active_process_names=("llama-server.exe",),
                               level_zero_devices=("GPU",))
        self.assertFalse(result.accepted)
        self.assertIn("llama-server.exe", " ".join(result.errors))

    def test_ah10_requires_level_zero_device_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            result = preflight(self.inputs(Path(directory)), installed_ram_bytes=32 * GIB,
                               active_process_names=(), level_zero_devices=())
        self.assertFalse(result.accepted)
        self.assertIn("Level Zero", " ".join(result.errors))

    def test_cpu_only_selection_does_not_require_level_zero(self):
        with tempfile.TemporaryDirectory() as directory:
            result = preflight(self.inputs(Path(directory), selected=("AH-06", "AH-07")),
                               installed_ram_bytes=32 * GIB,
                               active_process_names=(), level_zero_devices=())
        self.assertTrue(result.accepted, result.errors)

    def test_run_roots_increment_without_collision(self):
        with tempfile.TemporaryDirectory() as directory:
            parent = Path(directory)
            (parent / "AH-LH-2026-07-18-R0001").mkdir()
            (parent / "AH-LH-2026-07-18-R0003").mkdir()
            allocated = allocate_run_root(parent, "2026-07-18")
        self.assertEqual(allocated.name, "AH-LH-2026-07-18-R0004")

    def test_manifest_is_created_exclusively(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            result = preflight(self.inputs(root), installed_ram_bytes=32 * GIB,
                               active_process_names=(), level_zero_devices=("GPU",))
            run_root = root / "run"
            first = write_manifest(run_root, result)
            payload = json.loads(first.read_text(encoding="utf-8"))
            self.assertTrue(payload["accepted"])
            with self.assertRaises(FileExistsError):
                write_manifest(run_root, result)

    def test_launcher_restricts_selection_to_frozen_incomplete_rows(self):
        from scripts.testing.run_animehacker_large_host import validate_selection

        self.assertEqual(validate_selection(("AH-10", "AH-06")), ("AH-06", "AH-10"))
        with self.assertRaisesRegex(ValueError, "unsupported test ID"):
            validate_selection(("AH-09",))

    def test_runtime_command_keeps_guard_and_2048_mib_floor(self):
        from scripts.testing.run_animehacker_large_host import LargeHostConfig, build_runtime_command

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config = LargeHostConfig.fixture(root)
            command = build_runtime_command(config, "AH-10", root / "runtime")
        self.assertIn("--include-guarded", command)
        self.assertIn(["--minimum-available-ram-mb", "2048"],
                      [command[i:i + 2] for i in range(len(command) - 1)])
        self.assertIn(["--only", "AH-10"], [command[i:i + 2] for i in range(len(command) - 1)])
        self.assertNotIn("--pilot-only", command)

    def test_quality_command_uses_same_runtime_and_run_scoped_quality_roots(self):
        from scripts.testing.run_animehacker_large_host import LargeHostConfig, build_quality_command

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config = LargeHostConfig.fixture(root)
            runtime = root / "run" / "runtime"
            quality = root / "run" / "quality"
            command = build_quality_command(config, "AH-06", runtime, quality)
        pairs = [command[i:i + 2] for i in range(len(command) - 1)]
        self.assertIn(["--runtime-root", str(runtime)], pairs)
        self.assertIn(["--output-root", str(quality)], pairs)
        self.assertIn(["--only", "AH-06"], pairs)

    def test_execute_is_serial_and_stops_after_nonzero_phase(self):
        from scripts.testing.run_animehacker_large_host import LargeHostConfig, execute

        calls = []

        def runner(command, **kwargs):
            calls.append(command)
            return type("Completed", (), {"returncode": 9, "stdout": "", "stderr": "failed"})()

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config = LargeHostConfig.fixture(root, selected=("AH-06", "AH-07"))
            with self.assertRaisesRegex(RuntimeError, "AH-06 runtime failed"):
                execute(config, runner=runner, installed_ram_bytes=32 * GIB,
                        active_process_names=(), level_zero_devices=())
        self.assertEqual(len(calls), 1)

    def terminal_evidence(self, test_id="AH-06"):
        from scripts.testing.run_animehacker_retest import FORMAL_FIELDS

        sample = {field: 1.0 for field in FORMAL_FIELDS}
        sample.update({"valid": True, "missing": [], "extended_missing": [],
                       "utilization": {
                           "cpu_percent": {"mean": 20.0, "median": 19.0, "peak": 30.0,
                                           "sample_count": 5},
                           "gpu_percent": {"mean": 1.0, "median": 0.0, "peak": 4.0,
                                           "sample_count": 5}}})
        aggregate = {field: {"mean": 1.0, "median": 1.0, "min": 1.0, "max": 1.0}
                     for field in FORMAL_FIELDS}
        aggregate.update({
            "cpu_percent": {"mean": 20.0, "median": 19.0, "peak": 30.0, "sample_count": 15},
            "gpu_percent": {"mean": 1.0, "median": 0.0, "peak": 4.0, "sample_count": 15},
        })
        runtime = {"test_id": test_id, "model": {"sha256": "a" * 64},
                   "samples": [copy.deepcopy(sample) for _ in range(3)],
                   "aggregate": aggregate,
                   "activation": [{"activated": True} for _ in range(3)]}
        quality = [{"prompt_id": f"P{i}", "status": "complete", "score": 6.0,
                    "output_sha256": f"{i}" * 64} for i in range(1, 7)]
        manifest = {"accepted": True, "selected": [test_id], "file_records": {
            "granite8_model": {"sha256": "a" * 64}}}
        cleanup = {"llama_process_count": 0, "controller_process_count": 0,
                   "cleanup_verified": True}
        return runtime, quality, manifest, cleanup

    def test_terminal_validator_accepts_complete_measurement_and_quality(self):
        from scripts.testing.animehacker.large_host import validate_terminal_row

        validated = validate_terminal_row("AH-06", *self.terminal_evidence())
        self.assertEqual(validated.test_id, "AH-06")
        self.assertEqual(validated.quality_mean, 6.0)

    def test_terminal_validator_rejects_every_required_evidence_family(self):
        from scripts.testing.animehacker.large_host import validate_terminal_row

        mutations = []
        runtime, quality, manifest, cleanup = self.terminal_evidence()
        fewer_samples = copy.deepcopy(runtime); fewer_samples["samples"].pop()
        mutations.append((fewer_samples, quality, manifest, cleanup, "three formal samples"))
        missing_gpu = copy.deepcopy(runtime); del missing_gpu["aggregate"]["gpu_percent"]["mean"]
        mutations.append((missing_gpu, quality, manifest, cleanup, "gpu_percent mean"))
        missing_metric = copy.deepcopy(runtime); del missing_metric["samples"][0]["ttft_ms"]
        mutations.append((missing_metric, quality, manifest, cleanup, "ttft_ms"))
        no_activation = copy.deepcopy(runtime); no_activation["activation"][0]["activated"] = False
        mutations.append((no_activation, quality, manifest, cleanup, "activation"))
        five_quality = copy.deepcopy(quality); five_quality.pop()
        mutations.append((runtime, five_quality, manifest, cleanup, "P1-P6"))
        no_hash = copy.deepcopy(quality); no_hash[0]["output_sha256"] = ""
        mutations.append((runtime, no_hash, manifest, cleanup, "output hash"))
        hash_conflict = copy.deepcopy(manifest); hash_conflict["file_records"]["granite8_model"]["sha256"] = "b" * 64
        mutations.append((runtime, quality, hash_conflict, cleanup, "model hash"))
        residual = copy.deepcopy(cleanup); residual["llama_process_count"] = 1
        mutations.append((runtime, quality, manifest, residual, "residual process"))
        for rt, ql, mf, cl, message in mutations:
            with self.subTest(message=message):
                with self.assertRaisesRegex(ValueError, message):
                    validate_terminal_row("AH-06", rt, ql, mf, cl)

    def test_operator_guide_preserves_safety_and_evidence_contract(self):
        guide = (Path(__file__).resolve().parents[3] / "docs/testing/Animehacker-Large-Host-Completion-Guide.md")
        text = guide.read_text(encoding="utf-8")
        for required in ("AH-06", "AH-07", "AH-10", "32 GiB", "2,048 MiB",
                         "preflight", "resume", "large-host-completion",
                         "import_animehacker_large_host.py", "must not bypass",
                         "must not overwrite"):
            with self.subTest(required=required):
                self.assertIn(required, text)


if __name__ == "__main__":
    unittest.main()
