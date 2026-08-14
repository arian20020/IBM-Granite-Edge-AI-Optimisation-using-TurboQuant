from __future__ import annotations

import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
from typing import Any

from scripts.testing.workbook05.phase3.model_assets import (
    FORMAL_GRANITE_REPOSITORY,
    download_snapshot,
    resolve_model,
)


# Resolve the repository fixture from this test file so local and hosted runs
# consume the same committed fake Hub response.
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
    """Small injected adapter that performs no network access."""

    def __init__(
        self,
        *,
        resolved_revision: str,
        siblings: list[str],
        returned_directory: Path | None = None,
        unexpected_files: tuple[str, ...] = (),
        omitted_files: tuple[str, ...] = (),
    ) -> None:
        # Store the fake response and later call evidence for assertions.
        self.resolved_revision = resolved_revision
        self.siblings = siblings
        self.returned_directory = returned_directory
        self.unexpected_files = unexpected_files
        self.omitted_files = set(omitted_files)
        self.model_info_calls: list[tuple[str, str]] = []
        self.snapshot_download_calls: list[dict[str, Any]] = []

    def model_info(self, repo_id: str, revision: str) -> Any:
        # Record the exact moving reference that was resolved by the caller.
        self.model_info_calls.append((repo_id, revision))
        return SimpleNamespace(
            sha=self.resolved_revision,
            siblings=[SimpleNamespace(rfilename=name) for name in self.siblings],
        )

    def snapshot_download(
        self,
        *,
        repo_id: str,
        revision: str,
        local_dir: str,
        allow_patterns: list[str],
    ) -> str:
        # Record the immutable acquisition request without contacting the Hub.
        call = {
            "repo_id": repo_id,
            "revision": revision,
            "local_dir": local_dir,
            "allow_patterns": list(allow_patterns),
        }
        self.snapshot_download_calls.append(call)

        # Materialise only deterministic fixture bytes beneath the new folder.
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

        # Reproduce the documented local-dir metadata subtree. It is not a model
        # payload and must not be mistaken for an unexpected repository file.
        metadata = destination / ".cache" / "huggingface" / "download"
        metadata.mkdir(parents=True, exist_ok=True)
        (metadata / "fixture.metadata").write_text("metadata\n", encoding="utf-8")

        # Optional extra files let tests prove the post-download inventory fails
        # closed without deleting the retained partial snapshot.
        for relative_path in self.unexpected_files:
            target = destination.joinpath(*relative_path.split("/"))
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text("unexpected\n", encoding="utf-8")

        return str(self.returned_directory or destination)


class Phase3ModelAssetTests(unittest.TestCase):
    """Prove immutable Hub resolution and exact local snapshot inventory."""

    @staticmethod
    def _fixture() -> dict[str, Any]:
        # Load one committed fake response so tests remain network-free.
        return json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

    @classmethod
    def _valid_api(cls, **overrides: Any) -> FakeHubApi:
        # Build a valid fake by default and permit one adversarial mutation.
        fixture = cls._fixture()
        return FakeHubApi(
            resolved_revision=overrides.pop(
                "resolved_revision",
                fixture["resolved_revision"],
            ),
            siblings=overrides.pop("siblings", list(fixture["siblings"])),
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

        self.assertEqual(fixture["resolved_revision"], result.resolved_revision)
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

        api = self._valid_api()
        result = resolve_model(
            api,
            "openvino-internal-testing/tiny-random-model",
            "main",
            diagnostic_repository=(
                "openvino-internal-testing/tiny-random-model"
            ),
        )

        self.assertEqual(
            "openvino-internal-testing/tiny-random-model",
            result.repository,
        )

    def test_short_or_uppercase_resolved_revision_is_rejected(self) -> None:
        """Only an exact lowercase forty-character commit can lock an asset."""

        for invalid_revision in (
            "bef400f9",
            "BEF400F943F2FCF440CF1D4C38C6F844E2D4A387",
        ):
            with self.subTest(revision=invalid_revision):
                api = self._valid_api(resolved_revision=invalid_revision)
                with self.assertRaisesRegex(ValueError, "full lowercase"):
                    resolve_model(api, FORMAL_GRANITE_REPOSITORY, "main")

    def test_empty_or_duplicate_sibling_catalogue_is_rejected(self) -> None:
        """The resolver cannot invent files or collapse Windows path aliases."""

        invalid_catalogues = (
            [],
            ["config.json", "CONFIG.JSON"],
        )
        for siblings in invalid_catalogues:
            with self.subTest(siblings=siblings):
                api = self._valid_api(siblings=siblings)
                with self.assertRaises(ValueError):
                    resolve_model(api, FORMAL_GRANITE_REPOSITORY, "main")

    def test_unsafe_hub_filename_is_rejected(self) -> None:
        """Hub names must remain portable relative POSIX file paths."""

        invalid_names = (
            "../outside.json",
            "/absolute.json",
            r"folder\windows.json",
            "folder/./file.json",
            "C:/drive.json",
            "folder//empty.json",
        )
        for invalid_name in invalid_names:
            with self.subTest(path=invalid_name):
                api = self._valid_api(siblings=[invalid_name])
                with self.assertRaisesRegex(ValueError, "Hub file path"):
                    resolve_model(api, FORMAL_GRANITE_REPOSITORY, "main")

    def test_download_uses_resolved_sha_and_exact_allow_patterns(self) -> None:
        """Snapshot acquisition must use the immutable SHA, not `main`."""

        api = self._valid_api()
        model = resolve_model(api, FORMAL_GRANITE_REPOSITORY, "main")

        with TemporaryDirectory() as directory:
            destination = Path(directory) / "granite41-3b-bef400f9"
            downloaded = download_snapshot(api, model, destination)

            self.assertTrue(destination.is_dir())
            self.assertTrue(
                (destination / ".cache" / "huggingface").is_dir()
            )

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
            [path.relative_to(destination).as_posix() for path in downloaded],
        )

    def test_existing_destination_is_rejected_before_download(self) -> None:
        """C1 never overwrites, repairs, or silently reuses an asset folder."""

        api = self._valid_api()
        model = resolve_model(api, FORMAL_GRANITE_REPOSITORY, "main")

        with TemporaryDirectory() as directory:
            destination = Path(directory) / "existing"
            destination.mkdiŠ
BˆÚ]Ù[‹˜\ÜÙ\˜Z\Ù\Ô™YÙ^
˜[YQ\œ›Ü‹›]\İ›İ[™XYH^\İŠN‚ˆİÛ›ØYÜÛ˜\Úİ
\K[Ù[\İ[˜][ÛŠB‚ˆÙ[‹˜\ÜÙ\\]X[
×K\KœÛ˜\ÚİÙİÛ›ØYØØ[ÊB‚ˆYˆ\İÛZ\ÜÚ[™×ÛÜ—İ[™^XİYÜ^[ØYÙš[WÚ\×Ü™Z™XİYØ[™Ü™\Ù\™Y
Ù[ŠHOˆ›Û™N‚ˆˆˆ”ÜİYİÛ›ØYšY˜Z[ÈÛÜÙYÚ[H™]Z[š[™È›Ü™[œÚXÈ]šY[˜ÙKˆˆˆ‚‚ˆš^\™HHÙ[‹—Ùš^\™J
BˆØ\Ù\ÈH
ˆÂˆ›ÛZ]YÙš[\Èˆ
š^\™VÈœÚX›[™ÜÈ—VÌK
Kˆ[™^XİYÙš[\Èˆ

Kˆ›Y\ÜØYÙHˆ›Z\ÜÚ[™È^XİYš[\È‹ˆKˆÂˆ›ÛZ]YÙš[\Èˆ

Kˆ[™^XİYÙš[\Èˆ
[™^XİY˜š[ˆ‹
Kˆ›Y\ÜØYÙHˆ[™^XİYš[\È‹ˆKˆ
B‚ˆ›ÜˆØ\ÙH[ˆØ\Ù\Î‚ˆÚ]Ù[‹œİX•\İ
Y\ÜØYÙOXØ\ÙVÈ›Y\ÜØYÙH—JN‚ˆ\HHÙ[‹—İ˜[YØ\JˆÛZ]YÙš[\ÏXØ\ÙVÈ›ÛZ]YÙš[\È—Kˆ[™^XİYÙš[\ÏXØ\ÙVÈ[™^XİYÙš[\È—Kˆ
Bˆ[Ù[H™\ÛÛ™WÛ[Ù[
\K“Ô“PSÑÔS’UWÔ‘TÔÒUÔ–K›XZ[ˆŠB‚ˆÚ][\Ü˜\Q\™XİÜJ
H\È\™XİÜN‚ˆ\İ[˜][ÛˆH]
\™XİÜJHÈœ™]Z[™YY˜Z[\™H‚ˆÚ]Ù[‹˜\ÜÙ\˜Z\Ù\Ô™YÙ^
˜[YQ\œ›Ü‹Ø\ÙVÈ›Y\ÜØYÙH—JN‚ˆİÛ›ØYÜÛ˜\Úİ
\K[Ù[\İ[˜][ÛŠB‚ˆÙ[‹˜\ÜÙ\YJ\İ[˜][Û‹š\×Ù\Š
JBˆÙ[‹˜\ÜÙ\YJ[J\İ[˜][Û‹œ™ÛØŠŠˆŠJJB‚ˆYˆ\İØ\WÜ™]\›š[™×Ø[›İ\—Ù\™XİÜWÚ\×Ü™Z™XİY
Ù[ŠHOˆ›Û™N‚ˆˆˆ•HY\\ˆ]\İš[™H™\ÜÛœÙHÈHØ[\‹X\›İ™Y›Û\‹ˆˆˆ‚‚ˆÚ][\Ü˜\Q\™XİÜJ
H\È\™XİÜN‚ˆ›ÛİH]
\™XİÜJBˆ\İ[˜][ÛˆH›ÛİÈ˜\›İ™Y‚ˆİ\ˆH›ÛİÈ›İ\ˆ‚ˆİ\‹›ZÙ\Š
Bˆ\HHÙ[‹—İ˜[YØ\J™]\›™YÙ\™XİÜO[İ\ŠBˆ[Ù[H™\ÛÛ™WÛ[Ù[
\K“Ô“PSÑÔS’UWÔ‘TÔÒUÔ–K›XZ[ˆŠB‚ˆÚ]Ù[‹˜\ÜÙ\˜Z\Ù\Ô™YÙ^
˜[YQ\œ›Ü‹[™^XİY\™XİÜHŠN‚ˆİÛ›ØYÜÛ˜\Úİ
\K[Ù[\İ[˜][ÛŠB‚ˆÙ[‹˜\ÜÙ\YJ\İ[˜][Û‹š\×Ù\Š
JB‚ˆYˆ\İÙ\™XİÙ\[™[˜ŞWÚ\×Ù^XİWÜ[›™Y
Ù[ŠHOˆ›Û™N‚ˆˆˆ•\ÚÈH\Ù\ÈH™]šY]ÙYXˆ™\œÚ[Ûˆ[™›È[İš[™È™\]Z\™[Y[ˆˆˆ‚‚ˆ[™\ÈHÂˆ[™Kœİš\

Bˆ›Üˆ[™H[ˆ‘TURT‘SQS•×ÔUœ™XYİ^
[˜ÛÙ[™ÏH]‹NŠKœÜ][™\Ê
BˆYˆ[™Kœİš\

H[™›İ[™K›İš\

Kœİ\İÚ]
ˆÈŠBˆBˆÙ[‹˜\ÜÙ\\]X[
ÈšYÙÚ[™Ù˜XÙKZXOLKŒŒKŒ—K[™\ÊB‚‚šYˆ×Û˜[YW×ÈOH—×ÛXZ[—×È‚ˆ[š]\İ›XZ[Š
B