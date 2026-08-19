from __future__ import annotations

import unittest

from scripts.testing.workbook05.phase3.live_asset_lock import (
    HuggingFaceHubAdapter,
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

    def test_hub_adapter_uses_only_the_pinned_v121_snapshot_parameters(self) -> None:
        """The accepted Hub 1.21 API no longer accepts legacy symlink options."""

        captured: dict[str, object] = {}

        def snapshot_download(
            *,
            repo_id: str,
            revision: str,
            local_dir: str,
            allow_patterns: list[str],
        ) -> str:
            captured.update(
                {
                    "repo_id": repo_id,
                    "revision": revision,
                    "local_dir": local_dir,
                    "allow_patterns": allow_patterns,
                }
            )
            return local_dir

        # Bypass __init__ so this contract remains network-free and does not
        # require huggingface_hub in the hosted repository-test environment.
        adapter = HuggingFaceHubAdapter.__new__(HuggingFaceHubAdapter)
        adapter._snapshot_download = snapshot_download

        result = adapter.snapshot_download(
            repo_id="ibm-granite/granite-4.1-3b",
            revision="a" * 40,
            local_dir=r"C:\w5m\sources\granite41-3b-aaaaaaaa",
            allow_patterns=["config.json", "model.safetensors"],
        )

        self.assertEqual(r"C:\w5m\sources\granite41-3b-aaaaaaaa", result)
        self.assertEqual(
            {
                "repo_id": "ibm-granite/granite-4.1-3b",
                "revision": "a" * 40,
                "local_dir": r"C:\w5m\sources\granite41-3b-aaaaaaaa",
                "allow_patterns": ["config.json", "model.safetensors"],
            },
            captured,
        )


if __name__ == "__main__":
    unittest.main()
