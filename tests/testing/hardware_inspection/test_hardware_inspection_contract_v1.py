import hashlib
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
CONTRACT = ROOT / "docs/testing/hardware-inspection/Hardware-Inspection-Contract-v1.md"
HANDOFF = ROOT / (
    "IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/"
    "Application/HardwareInspectionHandoff.cs"
)


class HardwareInspectionContractV1Tests(unittest.TestCase):
    def test_contract_snapshot_is_bound_to_exact_public_handoff(self):
        text = CONTRACT.read_text(encoding="utf-8")
        source = HANDOFF.read_text(encoding="utf-8")

        self.assertFalse(CONTRACT.read_bytes().startswith(b"\xef\xbb\xbf"))
        self.assertIn("0b0b98cb5436735a14939dae556be73e341d7685", text)
        self.assertIn("exactly two public instance properties", text)
        self.assertIn("`InspectionId`", text)
        self.assertIn("`Snapshot`", text)
        self.assertIn("neutral to GGUF and OpenVINO", text)
        self.assertIn("Hardware providers never receive", text)

        properties = re.findall(r"public\s+[\w<>?]+\s+(\w+)\s*\{\s*get;\s*\}", source)
        self.assertEqual(["InspectionId", "Snapshot"], properties)
        self.assertEqual(
            "0b0b98cb5436735a14939dae556be73e341d7685",
            re.search(r"Owner implementation commit:\*\* `([0-9a-f]{40})`", text).group(1),
        )


if __name__ == "__main__":
    unittest.main()
