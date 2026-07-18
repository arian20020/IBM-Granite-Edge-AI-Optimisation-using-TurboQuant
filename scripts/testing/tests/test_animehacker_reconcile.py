import unittest


class AnimehackerReconcileTests(unittest.TestCase):
    def test_rejects_blank_and_literal_na_cells(self):
        from scripts.testing.reconcile_animehacker_workbook import validate_markdown

        with self.assertRaisesRegex(ValueError, "blank workbook"):
            validate_markdown("| Field |  |")
        with self.assertRaisesRegex(ValueError, "literal N/A"):
            validate_markdown("| Field | N/A |")

    def test_requires_every_controlled_id(self):
        from scripts.testing.reconcile_animehacker_workbook import validate_markdown

        with self.assertRaisesRegex(ValueError, "missing test id"):
            validate_markdown("| Field | Filled |")


if __name__ == "__main__":
    unittest.main()
