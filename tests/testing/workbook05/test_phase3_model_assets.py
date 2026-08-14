from __future__ import annotations

import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
from typing import Any

from scripts.testing.workbook05.phase3.conversion import (
    REVIEWED_DIRECT_REQUIREMENTS,
)
from scripts.testing.workbook05.phase3.model_assets import (
    FORMAL_GRANITE_REPOSITORY,
    download_snapshot,
    resolve_model,
)


# Resolve repository-controlled fixtures from this test file so local and
# GitHub-hosted runs consume exactly the same fake Hub response.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
FIXTURE_PATH = (
    Path(__file__).resolve().parent
    / "fixtures"
    / "phase3"
    / "assets"
    / "fake_hub_manifest.json"
)
REQUIREMENTS_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "requirements.phase3-assets.in"
)


class FakeHubApi:
    """Small injected Hub boundary that performs no network access."""

    def __init__(
        self,
        *,
        resolved_revision: str,
        siblings: list[str],
        returned_directory: Path | None = None,
        unexpected_files: tuple[str, ...] = (),
        omitted_files: tuple[str, ...] = (),
    ) -> None:
        # Retain the fake response and every call so assertions can verify that
        # validation happens before acquisition and that immutable values cross
        # the adapter boundary unchanged.
        self.resolved_revision = resolved_revision
        self.siblings = siblings
        self.returned_directory = returned_directory
        self.unexpected_files = unexpected_files
        self.omitted_files = set(omitted_files)
        self.model_info_calls: list[tuple[str, str]] = []
        self.snapshot_download_calls: list[dict[str, Any]] = []

    def model_info(self, repo_id: str, revision: str) -> Any:
        """Return the small metadata shape used by the production adapter."""

        self.model_info_calls.append((repo_id, revision))
        return SimpleNamespace(
            sha=self.resolved_revision,
            siblings=[
                SimpleNamespace(rfilename=name)
                for name in self.siblings
            ],
        )

    def snapshot_download(
        self,
        *,
        repo_id: str,
        revision: str,
        local_dir: str,
        allow_patterns: list[str],
    ) -> str:
        """Materialise deterministic fixture bytes beneath the new folder."""

        self.snapshot_download_calls.append(
            {
                "repo_id": repo_id,
                "revision": revision,
                "local_dir": local_dir,
                "allow_patterns": list(allow_patterns),
            }
        )

        destination = Path(local_dir)
        destination.mkdir(parents=False, exist_ok=False)
        for relative_path in allow_patterns:
            if relative_path in self.omitted_files:
                continue
            target = destination.joinpath(*relative_path.split("/"))
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(
                f"fixture:{relative_path}\n",
                encoding="utf-8",
            )

        # Hugging Face local-directory mode creates downloader metadata. It is
        # not part of the model payload catalogue and must be inspected safely.
        metadata = destination / ".cache" / "huggingface" / "download"
        metadata.mkdir(parents=True, exist_ok=True)
        (metadata / "fixture.metadata").write_text(
            "metadata\n",
            encoding="utf-8",
        )

        # Adversarial mutations prove that the post-download inventory fails
        # closed and preserves the partial directory for later investigation.
        for relative_path in self.unexpected_files:
            target = destination.joinpath(*relative_path.split("/"))
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text("unexpected\n", encoding="utf-8")

        return str(self.returned_directory or destination)


class Phase3ModelAssetTests(unittest.TestCase):
    """Prove immutable Hub resolution and exact local snapshot inventory."""

    @staticmethod
    def _fixture() -> dict[str, Any]:
        """Load a new fixture object so mutations cannot leak between tests."""

        return json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

    @classmethod
    def _valid_api(cls, **overrides: Any) -> FakeHubApi:
        """Build a valid fake by default and permit one explicit mutation."""

        fixture = cls._fixture()
        return FakeHubApi(
            resolved_revision=overrides.pop(
                "resolved_revision",
                fixture["resolved_revision"],
            ),
            siblings=overrides.pop(
                "siblings",
                list(fixture["siblings"]),
            ),
            **overrides,
        )

    def test_resolution_records_full_immutable_revision(self) -> None:
        """A moving request is bound to the full lowercase Hub commit SHA."""

        fixture = self._fixture()
        api = self._valid_api()

        result = resolve_model(
            api,
            fixture["repository"],
            fixture["requested_revision"],
        )

        self.assertEqual(
            fixture["resolved_revision"],
            result.resolved_revision,
        )
        self.assertRegex(result.resolved_revision, r"^[0-9a-f]{40}$")
        self.assertEqual(
            [(fixture["repository"], fixture["requested_revision"])],
            api.model_info_calls,
        )
        self.assertEqual(
            sorted(fixture["siblings"], key=str.casefold),
            [item.relative_path for item in result.siblings],
        )

    def test_unapproved_repository_is_rejected_before_api_access(self) -> None:
        """C1 must not resolve an arbitrary third-party model repository."""

        api = self._valid_api()

        with self.assertRaisesRegex(ValueError, "not approved"):
            resolve_model(api, "other-owner/other-model", "main")

        self.assertEqual([], api.model_info_calls)

    def test_explicit_diagnostic_repository_can_be_separately_scoped(self) -> None:
        """A diagnostic repository is allowed only when supplied explicitly."""

        diagnostic_repository = (
            "openvino-internal-testing/tiny-random-model"
        )
        result = resolve_model(
            self._valid_api(),
            diagnostic_repository,
            "main",
            diagnostic_repository=diagnostic_repository,
        )

        self.assertEqual(diagnostic_repository, result.repository)

    def test_short_or_uppercase_resolved_revision_is_rejected(self) -> None:
        """Only an exact lowercase forty-character commit can lock an asset."""

        for invalid_revision in (
            "bef400f9",
            "BEF400F943F2FCF440CF1D4C38C6F844E2D4A387",
        ):
            with self.subTest(revision=invalid_revision):
                api = self._valid_api(
                    resolved_revision=invalid_revision,
                )
                with self.assertRaisesRegex(ValueError, "full lowercase"):
                    resolve_model(
                        api,
                        FORMAL_GRANITE_REPOSITORY,
                        "main",
                    )

    def test_empty_or_duplicate_sibling_catalogue_is_rejected(self) -> None:
        """The resolver cannot invent files or collapse Windows path aliases."""

        for siblings in (
            [],
            ["config.json", "CONFIG.JSON"],
        ):
            with self.subTest(siblings=siblings):
                with self.assertRaises(ValueError):
                    resolve_model(
                        self._valid_api(siblings=siblings),
                        FORMAL_GRANITE_REPOSITORY,
                        "main",
                    )

    def test_unsafe_hub_filename_is_rejected(self) -> None:
        """Hub names must remain portable relative POSIX file paths."""

        for invalid_name in (
            "../outside.json",
            "/absolute.json",
            r"folder\windows.json",
            "folder/./file.json",
            "C:/drive.json",
            "folder//empty.json",
        ):
            with self.subTest(path=invalid_name):
                with self.assertRaisesRegex(ValueError, "Hub file path"):
                    resolve_model(
                        self._valid_api(siblings=[invalid_name]),
                        FORMAL_GRANITE_REPOSITORY,
                        "main",
                    )

    def test_download_uses_resolved_sha_and_exact_allow_patterns(self) -> None:
        """Snapshot acquisition must use the immutable SHA, not ``main``."""

        api = self._valid_api()
        model = resolve_model(
            api,
            FORMAL_GRANITE_REPOSITORY,
            "main",
        )

        with TemporaryDirectory() as directory:
            destination = Path(directory) / "granite41-3b-bef400f9"
            downloaded = download_snapshot(api, model, destination)

            self.assertTrue(destination.is_dir())
            self.assertTrue(
                (destination / ".cache" / "huggingface").is_dir()
            )
            downloaded_relative_paths = [
                path.relative_to(destination).as_posix()
                for path in downloaded
            ]

        self.assertEqual(1, len(api.snapshot_download_calls))
        call = api.snapshot_download_calls[0]
        self.assertEqual(model.repository, call["repo_id"])
        self.assertEqual(model.resolved_revision, call["revision"])
        self.assertEqual(
            [item.relative_path for item in model.siblings],
            call["allow_patterns"],
        )
        self.assertEqual(
            [item.relative_path for item in model.siblings],
            downloaded_relative_paths,
        )

    def test_existing_destination_is_rejected_before_download(self) -> None:
        """C1 never overwrites, repairs, or silently reuses an asset folder."""

        api = self._valid_api()
        model = resolve_model(
            api,
            FORMAL_GRANITE_REPOSITORY,
            "main",
        )

        with TemporaryDirectory() as directory:
            destination = Path(directory) / "existing"
            destination.mkdir()

            with self.assertRaisesRegex(
                ValueError,
                "must not already exist",
            ):
                download_snapshot(api, model, destination)

        self.assertEqual([], api.snapshot_download_calls)

    def test_missing_downloaded_file_is_rejected_and_retained(self) -> None:
        """A partial snapshot is evidence, never an accepted model asset."""

        fixture = self._fixture()
        missing = fixture["siblings"][0]
        api = self._valid_api(omitted_files=(missing,))
        model = resolve_model(
            api,
            FORMAL_GRANITE_REPOSITORY,
            "main",
        )

        with TemporaryDirectory() as directory:
            destination = Path(directory) / "missing-file"
            with self.assertRaisesRegex(
                ValueError,
                "missing expected files",
            ):
                download_snapshot(api, model, destination)

            self.assertTrue(destination.is_dir())
            self.assertFalse(
                destination.joinpath(*missing.split("/")).exists()
            )

    def test_unexpected_downloaded_file_is_rejected_and_retained(self) -> None:
        """An extra payload cannot be silently admitted to the model lock."""

        api = self._valid_api(unexpected_files=("rogue.txt",))
        model = resolve_model(
            api,
            FORMAL_GRANITE_REPOSITORY,
            "main",
        )

        with TemporaryDirectory() as directory:
            destination = Path(directory) / "unexpected-file"
            with self.assertRaisesRegex(
                ValueError,
                "unexpected files",
            ):
                download_snapshot(api, model, destination)

            self.assertTrue((destination / "rogue.txt").is_file())

    def test_unexpected_adapter_directory_is_rejected(self) -> None:
        """The Hub adapter cannot redirect acquisition outside the destination."""

        with TemporaryDirectory() as directory:
            root = Path(directory)
            returned_directory = root / "other"
            returned_directory.mkdir()
            api = self._valid_api(
                returned_directory=returned_directory,
            )
            model = resolve_model(
                api,
                FORMAL_GRANITE_REPOSITORY,
                "main",
            )

            with self.assertRaisesRegex(
                ValueError,
                "unexpected directory",
            ):
                download_snapshot(
                    api,
                    model,
                    root / "intended",
                )

    def test_direct_dependency_input_matches_the_reviewed_candidate(self) -> None:
        """Acquisition and conversion share one exact reviewed direct set."""

        observed = tuple(
            line.strip()
            for line in REQUIREMENTS_PATH.read_text(
                encoding="utf-8",
            ).splitlines()
            if line.strip() and not line.lstrip().startswith("#")
        )

        self.assertEqual(REVIEWED_DIRECT_REQUIREMENTS, observed)


if __name__ == "__main__":
    unittest.main()
