from __future__ import annotations

import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
from unittest.mock import patch

from scripts.testing.workbook05.phase3 import model_assets


class MetadataBoundaryApi:
    """Network-free adapter that creates one payload and one metadata file."""

    def model_info(self, repo_id: str, revision: str) -> SimpleNamespace:
        # Return the exact immutable identity required by the resolver.
        return SimpleNamespace(
            sha="bef400f943f2fcf440cf1d4c38c6f844e2d4a387",
            siblings=[SimpleNamespace(rfilename="config.json")],
        )

    def snapshot_download(
        self,
        *,
        repo_id: str,
        revision: str,
        local_dir: str,
        allow_patterns: list[str],
    ) -> str:
        # Reproduce the documented local-dir payload and metadata structure.
        destination = Path(local_dir)
        destination.mkdir()
        (destination / "config.json").write_text("{}\n", encoding="utf-8")
        metadata = destination / ".cache" / "huggingface" / "download"
        metadata.mkdir(parents=True)
        (metadata / "fixture.metadata").write_text(
            "metadata\n",
            encoding="utf-8",
        )
        return str(destination)


class Phase3ModelAssetMetadataTests(unittest.TestCase):
    """Prove ignored Hub metadata is still checked for redirected paths."""

    def test_reparse_point_inside_hub_metadata_is_rejected(self) -> None:
        """A metadata subtree cannot hide a link or Windows reparse point."""

        api = MetadataBoundaryApi()
        resolved = model_assets.resolve_model(
            api,
            model_assets.FORMAL_GRANITE_REPOSITORY,
            "main",
        )
        original_check = model_assets._is_link_or_reparse

        def classify_path(path: Path) -> bool:
            # Simulate a reparse point portably on hosted Windows and Linux.
            if path.name == "fixture.metadata":
                return True
            return original_check(path)

        with TemporaryDirectory() as directory:
            destination = Path(directory) / "metadata-boundary"
            with patch.object(
                model_assets,
                "_is_link_or_reparse",
                side_effect=classify_path,
            ):
                with self.assertRaisesRegex(
                    ValueError,
                    "link or reparse point",
                ):
                    model_assets.download_snapshot(
                        api,
                        resolved,
                        destination,
                    )

            # Failure evidence is retained; the adapter never cleans the folder.
            self.assertTrue(destination.is_dir())


if __name__ == "__main__":
    unittest.main()
