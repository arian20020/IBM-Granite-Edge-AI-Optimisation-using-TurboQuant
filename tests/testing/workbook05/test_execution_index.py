from __future__ import annotations

import csv
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
TRACEABILITY = ROOT / "docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv"
INDEX = ROOT / "docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv"


class ExecutionIndexTests(unittest.TestCase):
    def _rows(self, path: Path) -> list[dict[str, str]]:
        with path.open(newline="", encoding="utf-8-sig") as handle:
            return list(csv.DictReader(handle))

    def test_index_contains_every_openvino_traceability_id_once(self) -> None:
        expected = {
            row["Test_ID"]
            for row in self._rows(TRACEABILITY)
            if row["Workbook_ID"] in {"WB-04", "WB-05"}
        }
        rows = self._rows(INDEX)
        actual = [row["Test_ID"] for row in rows]

        self.assertEqual(expected, set(actual))
        self.assertEqual(len(actual), len(set(actual)))

    def test_route_and_memory_states_are_explicit(self) -> None:
        rows = self._rows(INDEX)
        allowed_routes = {"route-a-merged-openvino", "route-b-experimental-qjl-polar"}
        for row in rows:
            self.assertIn(row["Route_ID"], allowed_routes)
            self.assertIn(row["Memory_Rank_Status"], {"Not applicable", "Pending conformance", "Verified"})
            self.assertNotEqual("", row["Frontier_Status"])

    def test_generation_is_byte_deterministic(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            first = Path(temporary_directory) / "first.csv"
            second = Path(temporary_directory) / "second.csv"

            # Reuse the interpreter that launched the test. On the Intel job,
            # the workflow launches this suite with the pinned machine-wide
            # Python 3.12.10 executable. On the hosted validation job,
            # setup-python supplies the controlled interpreter. Hard-coding a
            # machine path here would test installation layout rather than the
            # execution-index generator's deterministic behaviour.
            command = [
                sys.executable,
                "-m",
                "scripts.testing.workbook05.generate_execution_index",
                "--traceability",
                str(TRACEABILITY),
            ]
            subprocess.run(command + ["--output", str(first)], check=True)
            subprocess.run(command + ["--output", str(second)], check=True)
            self.assertEqual(first.read_bytes(), second.read_bytes())


if __name__ == "__main__":
    unittest.main()
