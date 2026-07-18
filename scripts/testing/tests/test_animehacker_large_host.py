import json
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


if __name__ == "__main__":
    unittest.main()
