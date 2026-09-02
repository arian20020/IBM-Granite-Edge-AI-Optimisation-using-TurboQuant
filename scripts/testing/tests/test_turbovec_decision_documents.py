import json
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[3]
RUN_ID = "EXP-TV-COMP-001-20260902T231605Z-005"
COMMIT = "ccab9f325e6ce2a270a87daf01ae4e443bcf2d49"
CONTROLLED = (
    "docs/architecture/decisions/ADR-TurboVec.md",
    "docs/requirements/catalogue/Research-Requirements.md",
    "docs/requirements/catalogue/Functional-Requirements.md",
    "docs/requirements/MoSCoW-Requirements-v1.2.md",
    "docs/requirements/Requirements-Traceability-Matrix-v1.3.md",
    "docs/risks/Licence-Register.md",
    "docs/risks/Licence-Review-Notes.md",
    "docs/risks/Risk-Register.md",
    "docs/testing/TurboVec-Feasibility-Result-v1.md",
)


class DecisionDocumentTests(unittest.TestCase):
    def test_decision_is_machine_readable_and_demonstrator_only(self):
        path = ROOT / "experiments/processed-results/EXP-TV-COMP-001" / RUN_ID / "decision.json"
        decision = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual("DEMONSTRATOR_ONLY", decision["outcome"])
        self.assertEqual(COMMIT, decision["candidate"]["commit"])
        self.assertEqual("deferred", decision["product_status"])

    def test_controlled_documents_are_reconciled(self):
        for relative in CONTROLLED:
            with self.subTest(relative=relative):
                text = (ROOT / relative).read_text(encoding="utf-8")
                self.assertIn("DEMONSTRATOR_ONLY", text)
                self.assertIn(COMMIT, text)
                self.assertIn(RUN_ID, text)
                self.assertNotIn("TurboVec production ready", text)


if __name__ == "__main__":
    unittest.main()
