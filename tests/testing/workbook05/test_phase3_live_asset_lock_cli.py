from __future__ import annotations

import unittest

from scripts.testing.workbook05.phase3.live_asset_lock import (
    classify_source_paths,
    require_converted_outputs,
)


class Phase3LiveAssetLockCliTests(unittest.TestCase):
    def test_source_catalogue_separates_model_and_tokenizer_identities(self) -> None:
        model, tokenizer = classify_source_paths(
            (
                "config.json",
                "model-00001-of-00002.safetensors",
                "model-00002-of-00002.safetensors",
                "model.safetensors.index.json",
                "tokenizer.json",
                "tokenizer_config.json",
                "special_tokens_map.json",
                "README.md",
            )
        )

        self.assertIn("model-00001-of-00002.safetensors", model)
        self.assertIn("config.json", model)
        self.assertIn("tokenizer.json", tokenizer)
        self.assertIn("special_tokens_map.json", tokenizer)
        self.assertEqual(set(model).intersection(tokenizer), set())

    def test_source_catalogue_rejects_missing_model_or_tokenizer_payload(self) -> None:
        with self.assertRaisesRegex(ValueError, "model payload"):
            classify_source_paths(("tokenizer.json", "tokenizer_config.json"))
        with self.assertRaisesRegex(ValueError, "tokenizer payload"):
            classify_source_paths(("config.json", "model.safetensors"))

    def test_conversion_outputs_require_ir_config_and_tokenizer(self) -> None:
        require_converted_outputs(
            (
                "openvino_model.xml",
                "openvino_model.bin",
                "config.json",
                "tokenizer.json",
                "tokenizer_config.json",
            )
        )

        with self.assertRaisesRegex(ValueError, "openvino_model.bin"):
            require_converted_outputs(
                (
                    "openvino_model.xml",
                    "config.json",
                    "tokenizer.json",
                )
            )
        with self.assertRaisesRegex(ValueError, "tokenizer"):
            require_converted_outputs(
                (
                    "openvino_model.xml",
                    "openvino_model.bin",
                    "config.json",
                )
            )


if __name__ == "__main__":
    unittest.main()
