from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
LIVE_SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "Invoke-Workbook05Phase3AssetLockLive.ps1"
)


class Phase3LiveAssetEnvironmentContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.script = LIVE_SCRIPT_PATH.read_text(encoding="utf-8")

    def test_public_hub_download_cannot_inherit_an_operator_token(self) -> None:
        """The official public model must not consume unrelated machine credentials."""

        download = self.script.index("'resolve-download'")
        for variable in (
            "HF_TOKEN",
            "HUGGING_FACE_HUB_TOKEN",
            "HUGGINGFACE_HUB_TOKEN",
            "HF_TOKEN_PATH",
        ):
            with self.subTest(variable=variable):
                removal = self.script.index(
                    f"Remove-Item Env:{variable} -ErrorAction SilentlyContinue"
                )
                self.assertLess(removal, download)

    def test_hugging_face_cache_is_fresh_and_attempt_scoped(self) -> None:
        """Model acquisition must not silently depend on a user-global cache root."""

        self.assertIn("$HfHome = Join-Path $AttemptRoot 'hf-home'", self.script)
        self.assertIn("$env:HF_HOME = $HfHome", self.script)
        self.assertIn(
            "$env:HF_HUB_CACHE = Join-Path $HfHome 'hub'",
            self.script,
        )
        self.assertIn("Unexpected Hugging Face cache root", self.script)

    def test_caller_environment_is_restored_after_the_attempt(self) -> None:
        for variable in (
            "HF_TOKEN",
            "HUGGING_FACE_HUB_TOKEN",
            "HUGGINGFACE_HUB_TOKEN",
            "HF_TOKEN_PATH",
            "HF_HOME",
            "HF_HUB_CACHE",
            "HF_HUB_DISABLE_TELEMETRY",
            "TOKENIZERS_PARALLELISM",
        ):
            with self.subTest(variable=variable):
                self.assertIn(f"$OriginalEnvironment['{variable}']", self.script)
                self.assertIn(
                    f"$env:{variable} = $OriginalEnvironment['{variable}']",
                    self.script,
                )


if __name__ == "__main__":
    unittest.main()
