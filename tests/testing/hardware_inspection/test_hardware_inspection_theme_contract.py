import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
THEME = (
    ROOT
    / "IBM Granite with TurboQuant (Intel)"
    / "Features"
    / "HardwareInspection"
    / "Presentation"
    / "HardwareInspectionTheme.xaml"
)
XAML = "http://schemas.microsoft.com/winfx/2006/xaml"
PRESENTATION = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
KEY = f"{{{XAML}}}Key"


class HardwareInspectionThemeContractTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(THEME.is_file(), f"required Hardware theme is missing: {THEME}")
        raw = THEME.read_bytes()
        self.assertFalse(raw.startswith(b"\xef\xbb\xbf"), "theme must not contain a UTF-8 BOM")
        self.assertNotIn(b"\r\r\n", raw, "theme must use consistent Windows or Unix line endings")
        self.assertNotIn(b"\r", raw.replace(b"\r\n", b""), "theme contains a lone carriage return")
        self.root = ET.fromstring(raw.decode("utf-8"))

    def _resources(self, dictionary):
        return {child.attrib[KEY]: child for child in dictionary if KEY in child.attrib}

    def test_theme_has_matching_light_dark_and_high_contrast_semantic_brushes(self):
        dictionaries = self.root.find(f"{{{PRESENTATION}}}ResourceDictionary.ThemeDictionaries")
        self.assertIsNotNone(dictionaries)
        themes = {item.attrib[KEY]: self._resources(item) for item in dictionaries}
        self.assertEqual({"Light", "Dark", "HighContrast"}, set(themes))

        expected = {
            "HardwareInspectionTextPrimaryBrush",
            "HardwareInspectionTextSecondaryBrush",
            "HardwareInspectionTextMutedBrush",
            "HardwareInspectionAccentBrush",
            "HardwareInspectionAccentSurfaceBrush",
            "HardwareInspectionAccentBorderBrush",
            "HardwareInspectionSuccessSurfaceBrush",
            "HardwareInspectionSuccessTextBrush",
            "HardwareInspectionSuccessBorderBrush",
            "HardwareInspectionWarningSurfaceBrush",
            "HardwareInspectionWarningTextBrush",
            "HardwareInspectionWarningBorderBrush",
            "HardwareInspectionErrorSurfaceBrush",
            "HardwareInspectionErrorTextBrush",
            "HardwareInspectionErrorBorderBrush",
            "HardwareInspectionCanvasBrush",
            "HardwareInspectionSurfaceBrush",
            "HardwareInspectionSurfaceSubtleBrush",
            "HardwareInspectionBorderBrush",
            "HardwareInspectionBorderStrongBrush",
            "HardwareInspectionDisabledTextBrush",
            "HardwareInspectionFocusBrush",
        }
        for name, resources in themes.items():
            self.assertEqual(expected, set(resources), name)

        for resource in themes["HighContrast"].values():
            colour = resource.attrib["Color"]
            self.assertTrue(colour.startswith("{ThemeResource SystemColor"), colour)

    def test_theme_locks_approved_visual_measurements_and_accessibility_targets(self):
        resources = self._resources(self.root)
        exact_values = {
            "HardwareInspectionPageTitleFontSize": "32",
            "HardwareInspectionSectionTitleFontSize": "18",
            "HardwareInspectionBodyFontSize": "14",
            "HardwareInspectionHelperFontSize": "12",
            "HardwareInspectionLabelFontSize": "10",
            "HardwareInspectionContentColumnWidth": "840",
            "HardwareInspectionWideBreakpoint": "888",
            "HardwareInspectionCompactBreakpoint": "600",
            "HardwareInspectionMinimumTargetSize": "44",
            "HardwareInspectionMajorGap": "16",
            "HardwareInspectionFactTileGap": "12",
        }
        for key, expected in exact_values.items():
            self.assertIn(key, resources)
            self.assertEqual(expected, (resources[key].text or "").strip(), key)

        self.assertEqual("24,28,24,32", resources["HardwareInspectionPageMargin"].text.strip())
        self.assertEqual("16,24,16,24", resources["HardwareInspectionCompactPageMargin"].text.strip())
        self.assertEqual("24", resources["HardwareInspectionCardPadding"].text.strip())
        self.assertEqual("16", resources["HardwareInspectionCompactCardPadding"].text.strip())
        self.assertEqual("12", resources["HardwareInspectionCardCornerRadius"].text.strip())
        self.assertEqual("1", resources["HardwareInspectionBorderThickness"].text.strip())

    def test_theme_is_hardware_owned_and_does_not_modify_or_merge_model_resources(self):
        raw = THEME.read_text(encoding="utf-8")
        self.assertNotIn("ModelInspectionTheme", raw)
        for element in self.root.iter():
            key = element.attrib.get(KEY)
            if key and element.tag != f"{{{PRESENTATION}}}ResourceDictionary":
                self.assertTrue(key.startswith("HardwareInspection"), key)


if __name__ == "__main__":
    unittest.main()
