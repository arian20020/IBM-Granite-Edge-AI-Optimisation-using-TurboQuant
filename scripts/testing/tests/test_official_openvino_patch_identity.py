import hashlib
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.testing.official_openvino import patch_identity
from scripts.testing.official_openvino.patch_identity import (
    prepare_patch_workspace,
    validate_patch_identity,
)


EXPECTED = "7dea0459b2ac7d8dfd877fd9df6737674fd8371d"


def git(*args: str, cwd: Path | None = None) -> str:
    return subprocess.check_output(
        ["git", "-c", "core.longpaths=true", *args],
        cwd=cwd,
        text=True,
        stderr=subprocess.STDOUT,
    ).strip()


class PatchIdentityTests(unittest.TestCase):
    def test_patch_identity_accepts_clean_expected_base_with_patch_commit(self):
        validate_patch_identity(
            {"base_commit": EXPECTED, "patch_commit": EXPECTED, "dirty": False},
            EXPECTED,
        )

    def test_patch_identity_rejects_wrong_or_dirty_base(self):
        with self.assertRaisesRegex(ValueError, "base commit"):
            validate_patch_identity(
                {"base_commit": "wrong", "patch_commit": EXPECTED, "dirty": False},
                EXPECTED,
            )
        with self.assertRaisesRegex(ValueError, "dirty"):
            validate_patch_identity(
                {"base_commit": EXPECTED, "patch_commit": EXPECTED, "dirty": True},
                EXPECTED,
            )

    def test_patch_identity_rejects_missing_patch_commit(self):
        with self.assertRaisesRegex(ValueError, "patch commit"):
            validate_patch_identity({"base_commit": EXPECTED, "dirty": False}, EXPECTED)


class PatchWorkspaceControllerTests(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.control = self.root / "control"
        self.upstream = self.root / "upstream"
        self.destination = self.root / "derived"

        git("init", str(self.control))
        for directory in (
            "experiments/patches/openvino-turboquant",
            "experiments/patches/openvino-cpu-state-observer",
        ):
            patch_dir = self.control / directory
            patch_dir.mkdir(parents=True)
            (patch_dir / "README.md").write_text("controlled patches\n", encoding="utf-8")
        git("-C", str(self.control), "add", ".")
        git(
            "-C",
            str(self.control),
            "-c",
            "user.name=Test",
            "-c",
            "user.email=test@example.invalid",
            "commit",
            "-m",
            "control",
        )

        git("init", str(self.upstream))
        (self.upstream / "value.txt").write_text("base\n", encoding="utf-8")
        git("-C", str(self.upstream), "add", "value.txt")
        git(
            "-C",
            str(self.upstream),
            "-c",
            "user.name=Test",
            "-c",
            "user.email=test@example.invalid",
            "commit",
            "-m",
            "base",
        )
        self.expected = git("-C", str(self.upstream), "rev-parse", "HEAD")

    def tearDown(self):
        self.temporary_directory.cleanup()

    def prepare(
        self,
        spec=None,
        destination: Path | None = None,
    ) -> dict[str, object]:
        arguments = {}
        if spec is not None:
            arguments["spec"] = spec
        with patch.object(patch_identity, "REPO_ROOT", self.control):
            return prepare_patch_workspace(
                self.upstream,
                self.destination if destination is None else destination,
                self.expected,
                **arguments,
            )

    def add_tracked_patch(self, spec=None) -> Path:
        spec = patch_identity.GENAI_TURBOQUANT_SPEC if spec is None else spec
        patch_path = self.control / spec.patch_directory / "0001-value.patch"
        (self.upstream / "value.txt").write_text("patched\n", encoding="utf-8")
        patch_text = subprocess.check_output(
            ["git", "-c", "core.longpaths=true", "-C", str(self.upstream), "diff"],
            text=True,
        )
        patch_path.write_text(patch_text, encoding="utf-8")
        git("-C", str(self.upstream), "checkout", "--", "value.txt")
        git("-C", str(self.control), "add", patch_path.relative_to(self.control).as_posix())
        git(
            "-C",
            str(self.control),
            "-c",
            "user.name=Test",
            "-c",
            "user.email=test@example.invalid",
            "commit",
            "-m",
            "patch",
        )
        return patch_path

    def add_literal_patch(self, name: str, target: str, content: str) -> Path:
        patch_path = (
            self.control
            / patch_identity.GENAI_TURBOQUANT_SPEC.patch_directory
            / name
        )
        patch_path.write_text(
            "\n".join(
                (
                    f"diff --git a/{target} b/{target}",
                    "new file mode 100644",
                    "--- /dev/null",
                    f"+++ b/{target}",
                    "@@ -0,0 +1 @@",
                    f"+{content}",
                    "",
                )
            ),
            encoding="utf-8",
        )
        return patch_path

    def commit_control(self, message: str) -> None:
        git("-C", str(self.control), "add", ".")
        git(
            "-C",
            str(self.control),
            "-c",
            "user.name=Test",
            "-c",
            "user.email=test@example.invalid",
            "commit",
            "-m",
            message,
        )

    def test_callable_applies_tracked_patch_and_preserves_provenance_on_rerun(self):
        patch_path = self.add_tracked_patch()
        first = self.prepare()
        second = self.prepare()

        self.assertEqual(first, second)
        self.assertEqual(first["patch_commit"], second["patch_commit"])
        self.assertEqual(second["applied_patches"], ["0001-value.patch"])
        self.assertEqual(
            second["patches"],
            [
                {
                    "name": "0001-value.patch",
                    "blob_id": git(
                        "-C",
                        str(self.control),
                        "rev-parse",
                        "HEAD:experiments/patches/openvino-turboquant/0001-value.patch",
                    ),
                    "sha256": hashlib.sha256(patch_path.read_bytes()).hexdigest(),
                }
            ],
        )
        self.assertEqual(second["upstream_tree"], git("-C", str(self.upstream), "rev-parse", "HEAD^{tree}"))
        self.assertEqual(second["derived_tree"], git("-C", str(self.destination), "rev-parse", "HEAD^{tree}"))
        self.assertEqual((self.destination / "value.txt").read_text(), "patched\n")
        self.assertEqual(git("-C", str(self.destination), "status", "--porcelain"), "")
        self.assertEqual(git("-C", str(self.destination), "config", "--bool", "core.longpaths"), "true")
        self.assertEqual(git("-C", str(self.upstream), "rev-parse", "HEAD"), self.expected)
        self.assertEqual(
            git("-C", str(self.upstream), "status", "--porcelain", "--untracked-files=all"),
            "",
        )

    def test_orders_two_patch_records_and_uses_exact_raw_bytes(self):
        second = self.add_literal_patch("0002-alpha.patch", "alpha.txt", "alpha")
        first = self.add_literal_patch("0001-beta.patch", "beta.txt", "beta")
        self.commit_control("ordered patches")
        record = self.prepare()
        self.assertEqual(
            record["applied_patches"],
            ["0001-beta.patch", "0002-alpha.patch"],
        )
        self.assertEqual(
            [item["name"] for item in record["patches"]],
            ["0001-beta.patch", "0002-alpha.patch"],
        )
        expected_sha = {
            first.name: hashlib.sha256(first.read_bytes()).hexdigest(),
            second.name: hashlib.sha256(second.read_bytes()).hexdigest(),
        }
        for item in record["patches"]:
            self.assertEqual(item["sha256"], expected_sha[item["name"]])
            self.assertRegex(item["blob_id"], r"^[0-9a-f]{40}$")
            self.assertRegex(item["sha256"], r"^[0-9a-f]{64}$")

    def test_commit_identity_is_reproducible_and_binds_commit_message(self):
        self.add_tracked_patch()
        first = self.prepare(destination=self.root / "derived-a")
        second = self.prepare(destination=self.root / "derived-b")
        altered = patch_identity.PatchWorkspaceSpec(
            branch="project/alternate-message",
            patch_directory=patch_identity.GENAI_TURBOQUANT_SPEC.patch_directory,
            commit_message="A different controlled patch message",
        )
        third = self.prepare(altered, self.root / "derived-c")
        self.assertEqual(first["patch_commit"], second["patch_commit"])
        self.assertEqual(first["derived_tree"], second["derived_tree"])
        self.assertNotEqual(first["patch_commit"], third["patch_commit"])
        self.assertEqual(first["derived_tree"], third["derived_tree"])

    def test_default_spec_preserves_genai_contract(self):
        with patch.object(patch_identity, "REPO_ROOT", self.control):
            record = prepare_patch_workspace(self.upstream, self.destination, self.expected)
        self.assertEqual(record["branch"], patch_identity.GENAI_TURBOQUANT_SPEC.branch)
        self.assertEqual(Path(str(record["patch_directory"])).name, "openvino-turboquant")

    def test_prepares_core_observer_patch_family_without_genai_constants(self):
        record = self.prepare(patch_identity.CORE_OBSERVER_SPEC)
        self.assertEqual(record["branch"], patch_identity.CORE_OBSERVER_SPEC.branch)
        self.assertEqual(Path(str(record["patch_directory"])).name, "openvino-cpu-state-observer")
        self.assertEqual(record["base_commit"], self.expected)
        self.assertEqual(
            record["upstream_tree"],
            git("-C", str(self.upstream), "rev-parse", "HEAD^{tree}"),
        )
        self.assertEqual(len(str(record["derived_tree"])), 40)
        self.assertEqual(record["applied_patches"], [])
        self.assertEqual(record["patches"], [])

    def test_rejects_existing_destination_from_another_patch_family(self):
        self.prepare(patch_identity.CORE_OBSERVER_SPEC)
        head = git("-C", str(self.destination), "rev-parse", "HEAD")
        git("-C", str(self.destination), "config", "core.longpaths", "false")
        with self.assertRaisesRegex(ValueError, "branch mismatch"):
            self.prepare(patch_identity.GENAI_TURBOQUANT_SPEC)
        self.assertEqual(git("-C", str(self.destination), "rev-parse", "HEAD"), head)
        self.assertEqual(
            subprocess.check_output(
                [
                    "git",
                    "-C",
                    str(self.destination),
                    "config",
                    "--bool",
                    "core.longpaths",
                ],
                text=True,
            ).strip(),
            "false",
        )

    def test_rejects_reverse_existing_destination_family_without_mutation(self):
        self.prepare(patch_identity.GENAI_TURBOQUANT_SPEC)
        head = git("-C", str(self.destination), "rev-parse", "HEAD")
        git("-C", str(self.destination), "config", "core.longpaths", "false")
        with self.assertRaisesRegex(ValueError, "branch mismatch"):
            self.prepare(patch_identity.CORE_OBSERVER_SPEC)
        self.assertEqual(git("-C", str(self.destination), "rev-parse", "HEAD"), head)
        self.assertEqual(
            subprocess.check_output(
                [
                    "git",
                    "-C",
                    str(self.destination),
                    "config",
                    "--bool",
                    "core.longpaths",
                ],
                text=True,
            ).strip(),
            "false",
        )

    def test_rejects_patch_directory_outside_controlling_repository(self):
        unsafe = patch_identity.PatchWorkspaceSpec(
            branch="project/unsafe",
            patch_directory=Path("../outside"),
            commit_message="unsafe",
        )
        with self.assertRaisesRegex(ValueError, "patch directory"):
            self.prepare(unsafe)

    def test_rejects_absolute_and_missing_patch_directories(self):
        cases = (
            self.root / "outside",
            Path("experiments/patches/missing"),
        )
        for index, patch_directory in enumerate(cases):
            with self.subTest(patch_directory=patch_directory):
                unsafe = patch_identity.PatchWorkspaceSpec(
                    branch=f"project/unsafe-{index}",
                    patch_directory=patch_directory,
                    commit_message="unsafe",
                )
                with self.assertRaisesRegex(ValueError, "patch directory"):
                    self.prepare(unsafe, self.root / f"derived-{index}")

    def test_detects_real_file_symlink_when_platform_allows_creation(self):
        target = self.root / "outside.patch"
        target.write_text("external bytes\n", encoding="utf-8")
        link = (
            self.control
            / "experiments/patches/openvino-turboquant/0001-external.patch"
        )
        try:
            link.symlink_to(target)
        except OSError as exc:
            if os.name != "nt":
                raise
            self.assertEqual(
                getattr(exc, "winerror", None),
                1314,
                f"unexpected file-symlink creation failure: {exc}",
            )
            self.assertFalse(link.exists())
            return
        self.assertTrue(patch_identity._is_link_or_reparse(link))
        with patch.object(patch_identity, "_clean", return_value=True):
            with self.assertRaisesRegex(ValueError, "symlink|reparse"):
                patch_identity._controlled_patches(
                    patch_identity.GENAI_TURBOQUANT_SPEC
                )

    def test_rejects_real_patch_root_junction_or_directory_symlink(self):
        outside = self.root / "outside-patch-root"
        outside.mkdir()
        link = self.control / "experiments/patches/reparse-root"
        if os.name == "nt":
            result = subprocess.run(
                ["cmd", "/c", "mklink", "/J", str(link), str(outside)],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        else:
            link.symlink_to(outside, target_is_directory=True)
        unsafe = patch_identity.PatchWorkspaceSpec(
            branch="project/reparse",
            patch_directory=Path("experiments/patches/reparse-root"),
            commit_message="unsafe",
        )
        self.assertTrue(patch_identity._is_link_or_reparse(link))
        with self.assertRaisesRegex(ValueError, "symlink|reparse"):
            patch_identity._patch_root(unsafe)

    def test_rejects_destination_inside_upstream_without_mutation(self):
        original_head = git("-C", str(self.upstream), "rev-parse", "HEAD")
        nested = self.upstream / "derived"
        with self.assertRaisesRegex(ValueError, "destination.*upstream"):
            self.prepare(destination=nested)
        self.assertEqual(git("-C", str(self.upstream), "rev-parse", "HEAD"), original_head)
        self.assertEqual(
            git("-C", str(self.upstream), "status", "--porcelain", "--untracked-files=all"),
            "",
        )
        self.assertFalse(nested.exists())

    def test_rejects_patch_mutation_after_snapshot_instead_of_misreporting_it(self):
        patch_path = self.add_tracked_patch()
        original_publish = patch_identity._create_and_publish_checkout

        def mutate_after_publish(*args, **kwargs):
            result = original_publish(*args, **kwargs)
            patch_path.write_bytes(patch_path.read_bytes() + b"\n")
            return result

        with patch.object(
            patch_identity,
            "_create_and_publish_checkout",
            side_effect=mutate_after_publish,
        ):
            with self.assertRaisesRegex(ValueError, "controlling repository changed"):
                self.prepare()

    def test_failed_staging_creation_cleans_up_and_retry_succeeds(self):
        def fail_mid_create(_upstream, staging, *_args):
            staging.mkdir(parents=True)
            (staging / "partial.txt").write_text("partial\n", encoding="utf-8")
            raise ValueError("forced create failure")

        with patch.object(
            patch_identity,
            "_create_exact_checkout",
            side_effect=fail_mid_create,
        ):
            with self.assertRaisesRegex(ValueError, "forced create failure"):
                self.prepare()
        self.assertFalse(self.destination.exists())
        self.assertEqual(
            list(self.destination.parent.glob(f".{self.destination.name}.preparing-*")),
            [],
        )
        record = self.prepare()
        self.assertEqual(record["patch_commit"], self.expected)
        self.assertEqual(git("-C", str(self.destination), "status", "--porcelain"), "")

    def test_rejects_unknown_family(self):
        with self.assertRaisesRegex(ValueError, "unknown patch family"):
            patch_identity.spec_for_family("unknown")

    def test_cli_default_family_preserves_legacy_genai_behavior(self):
        evidence = self.root / "identity.json"
        record = {
            "base_commit": self.expected,
            "patch_commit": self.expected,
            "dirty": False,
        }
        argv = [
            "patch_identity",
            "--upstream",
            str(self.upstream),
            "--destination",
            str(self.destination),
            "--expected-commit",
            self.expected,
            "--evidence",
            str(evidence),
        ]
        with patch.object(sys, "argv", argv), patch.object(
            patch_identity,
            "prepare_patch_workspace",
            return_value=record,
        ) as prepare:
            self.assertEqual(patch_identity.main(), 0)
        self.assertIs(
            prepare.call_args.args[3],
            patch_identity.GENAI_TURBOQUANT_SPEC,
        )
        self.assertEqual(evidence.read_text(encoding="utf-8").endswith("\n"), True)

    def test_power_shell_entry_points_preserve_cwd_roots_overrides_and_exit(self):
        repository_root = Path(__file__).resolve().parents[3]
        controller = repository_root / "scripts/testing/prepare_openvino_cpu_observer_patch.ps1"
        legacy = repository_root / "scripts/testing/prepare_openvino_turboquant_patch.ps1"
        fake_dir = self.root / "fake command with spaces"
        fake_dir.mkdir()
        fake = fake_dir / "capture.cmd"
        capture = self.root / "captured.txt"
        fake.write_bytes(
            b"@echo off\r\n"
            b'> "%CAPTURE_PATH%" echo cwd=%CD%\r\n'
            b'>> "%CAPTURE_PATH%" echo args=%*\r\n'
            b"exit /b %FAKE_EXIT%\r\n"
        )
        foreign = self.root / "foreign cwd"
        foreign.mkdir()
        environment = os.environ.copy()
        environment.update({"CAPTURE_PATH": str(capture), "FAKE_EXIT": "0"})

        completed = subprocess.run(
            [
                "powershell",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(controller),
                "-PythonCommand",
                str(fake),
            ],
            cwd=foreign,
            env=environment,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
        captured = capture.read_text(encoding="utf-8").lower()
        common = Path(
            git(
                "-C",
                str(repository_root),
                "rev-parse",
                "--path-format=absolute",
                "--git-common-dir",
            )
        )
        shared_root = common.parent
        expected_upstream = (
            shared_root / "external/official-openvino/2026-07-19/openvino"
        )
        expected_destination = (
            repository_root
            / "external/official-openvino/2026-07-19/openvino-cpu-state-observer"
        )
        self.assertIn(f"cwd={repository_root}".lower(), captured)
        self.assertIn("--family openvino-cpu-observer", captured)
        self.assertIn(str(expected_upstream).lower(), captured)
        self.assertIn(str(expected_destination).lower(), captured)

        completed = subprocess.run(
            [
                "powershell",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(legacy),
                "-UpstreamPath",
                str(self.root / "legacy upstream with spaces"),
                "-DestinationPath",
                str(self.root / "legacy destination with spaces"),
                "-EvidencePath",
                str(self.root / "legacy evidence with spaces.json"),
                "-PythonCommand",
                str(fake),
            ],
            cwd=repository_root,
            env=environment,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
        legacy_capture = capture.read_text(encoding="utf-8").lower()
        self.assertIn("-m scripts.testing.official_openvino.patch_identity", legacy_capture)
        self.assertNotIn("--family", legacy_capture)

        environment["FAKE_EXIT"] = "7"
        completed = subprocess.run(
            [
                "powershell",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(controller),
                "-UpstreamPath",
                str(self.root / "upstream with spaces"),
                "-DestinationPath",
                str(self.root / "destination with spaces"),
                "-EvidencePath",
                str(self.root / "evidence with spaces.json"),
                "-PythonCommand",
                str(fake),
            ],
            cwd=foreign,
            env=environment,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(completed.returncode, 0)
        failed_capture = capture.read_text(encoding="utf-8").lower()
        self.assertIn(str(self.root / "upstream with spaces").lower(), failed_capture)
        self.assertIn(str(self.root / "destination with spaces").lower(), failed_capture)

    def test_rejects_dirty_upstream(self):
        (self.upstream / "dirty.txt").write_text("dirty\n")
        with self.assertRaisesRegex(ValueError, "upstream.*dirty"):
            self.prepare()

    def test_rejects_dirty_destination_without_mutating_its_config(self):
        self.prepare()
        subprocess.run(
            ["git", "-C", str(self.destination), "config", "core.longpaths", "false"],
            check=True,
        )
        (self.destination / "dirty.txt").write_text("dirty\n")
        with self.assertRaisesRegex(ValueError, "destination.*dirty"):
            self.prepare()
        self.assertEqual(
            subprocess.check_output(
                ["git", "-C", str(self.destination), "config", "--bool", "core.longpaths"],
                text=True,
            ).strip(),
            "false",
        )

    def test_rejects_wrong_branch(self):
        self.prepare()
        git("-C", str(self.destination), "branch", "-m", "wrong")
        with self.assertRaisesRegex(ValueError, "branch"):
            self.prepare()

    def test_rejects_arbitrary_descendant_head(self):
        self.prepare()
        git(
            "-C",
            str(self.destination),
            "-c",
            "user.name=Test",
            "-c",
            "user.email=test@example.invalid",
            "commit",
            "--allow-empty",
            "-m",
            "foreign",
        )
        with self.assertRaisesRegex(ValueError, "derived HEAD"):
            self.prepare()

    def test_rejects_foreign_origin(self):
        self.prepare()
        foreign = self.root / "foreign"
        git("init", str(foreign))
        git("-C", str(self.destination), "remote", "set-url", "origin", str(foreign))
        with self.assertRaisesRegex(ValueError, "origin"):
            self.prepare()

    def test_rejects_untracked_patch_and_dirty_controlling_repo(self):
        patch_path = self.control / "experiments/patches/openvino-turboquant/untracked.patch"
        patch_path.write_text("untracked\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "controlling repository.*dirty"):
            self.prepare()


if __name__ == "__main__":
    unittest.main()
