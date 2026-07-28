# Task 01: Reproducible OpenVINO Patch Workspace Micro-Plan

> **For the implementer:** Use `superpowers:test-driven-development` and
> `superpowers:subagent-driven-development`. Apply only the exact changes below.
> Stop after the task commit and return evidence for independent spec and
> quality review. Do not materialize the derived OpenVINO core until the parent
> commit exists and the controlling worktree is clean.

**Roadmap task:** Task 1 in
`2026-07-28-openvino-cpu-kv-allocation-observability.md`.

**Goal:** Generalize the existing deterministic OpenVINO GenAI patch controller
without changing its default behavior, add the CPU-observer patch family, and
materialize a clean, exact derived core checkout only after the controller and
its tests are committed.

**Acceptance gates satisfied:** immutable clean upstream; tracked ordered patch
inputs; branch/family separation; deterministic commit identity; patch
blob/SHA-256/tree provenance; shared-repository upstream discovery; no heavy
builds; no derived-core mutation before the parent commit.

**Starting baseline:** parent commit `76cdff1`; patch-identity suite `10 passed`.

## Files

- Replace: `scripts/testing/official_openvino/patch_identity.py`
- Replace: `scripts/testing/tests/test_official_openvino_patch_identity.py`
- Create: `scripts/testing/prepare_openvino_cpu_observer_patch.ps1`
- Create: `experiments/patches/openvino-cpu-state-observer/README.md`

## Step 1: Replace the tests and prove RED

Replace `scripts/testing/tests/test_official_openvino_patch_identity.py` with
this complete file:

```python
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
            git("-C", str(self.destination), "config", "--bool", "core.longpaths"),
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
            git("-C", str(self.destination), "config", "--bool", "core.longpaths"),
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
```

Run:

```powershell
python -m pytest scripts/testing/tests/test_official_openvino_patch_identity.py -q
```

Expected RED is capability-dependent but never collection failure: on a
standard Windows account that denies file-symlink creation, `10 passed,
16 failed`; when Developer Mode or elevation permits real file symlinks,
`9 passed, 17 failed`. The failures prove that generic specs,
ordered patch evidence, path/reparse safety, family separation, immutable
upstream containment, and the new PowerShell controller do not yet exist. The
suite must execute rather than fail during collection; do not weaken the
assertions.

## Step 2: Replace the controller and prove GREEN

Replace `scripts/testing/official_openvino/patch_identity.py` with this complete
file:

```python
"""Prepare and validate a reproducible project-patched OpenVINO checkout."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import stat
import subprocess
import tempfile
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]


@dataclass(frozen=True)
class PatchWorkspaceSpec:
    branch: str
    patch_directory: Path
    commit_message: str


@dataclass(frozen=True)
class ControlledPatch:
    path: Path
    relative_path: str
    name: str
    blob_id: str
    sha256: str
    raw_bytes: bytes
    apply_bytes: bytes


GENAI_TURBOQUANT_SPEC = PatchWorkspaceSpec(
    branch="project/turboquant-wb04",
    patch_directory=Path("experiments/patches/openvino-turboquant"),
    commit_message="Apply controlled OpenVINO TurboQuant patch set",
)

CORE_OBSERVER_SPEC = PatchWorkspaceSpec(
    branch="project/cpu-state-allocation-observer",
    patch_directory=Path("experiments/patches/openvino-cpu-state-observer"),
    commit_message="Apply controlled OpenVINO CPU state observer patch set",
)

FAMILY_SPECS: Mapping[str, PatchWorkspaceSpec] = {
    "openvino-genai-turboquant": GENAI_TURBOQUANT_SPEC,
    "openvino-cpu-observer": CORE_OBSERVER_SPEC,
}


def _git(
    repository: Path | None,
    *arguments: str,
    env: dict | None = None,
    input_text: str | None = None,
    input_bytes: bytes | None = None,
) -> str:
    command = ["git", "-c", "core.longpaths=true"]
    if repository is not None:
        command.extend(["-C", str(repository)])
    command.extend(arguments)
    if input_text is not None and input_bytes is not None:
        raise ValueError("only one Git input form may be supplied")
    binary = input_bytes is not None
    result = subprocess.run(
        command,
        check=False,
        capture_output=True,
        text=not binary,
        env=env,
        input=input_bytes if binary else input_text,
    )
    stdout = result.stdout.decode("utf-8", errors="replace") if binary else result.stdout
    stderr = result.stderr.decode("utf-8", errors="replace") if binary else result.stderr
    if result.returncode:
        detail = (stderr or stdout).strip()
        raise ValueError(f"git {' '.join(arguments)} failed ({result.returncode}): {detail}")
    return stdout.strip()


def _clean(repository: Path) -> bool:
    return _git(repository, "status", "--porcelain", "--untracked-files=all") == ""


def _same_path(left: str | Path, right: str | Path) -> bool:
    return os.path.normcase(str(Path(left).resolve())) == os.path.normcase(str(Path(right).resolve()))


def _is_link_or_reparse(path: Path) -> bool:
    try:
        metadata = path.lstat()
    except FileNotFoundError:
        return False
    attributes = getattr(metadata, "st_file_attributes", 0)
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(reparse_flag and attributes & reparse_flag)


def _path_at_or_below(path: Path, parent: Path) -> bool:
    try:
        common = Path(os.path.commonpath([str(path.resolve()), str(parent.resolve())]))
    except ValueError:
        return False
    return _same_path(common, parent)


def _patch_root(spec: PatchWorkspaceSpec) -> Path:
    repository_root = REPO_ROOT.resolve()
    relative = Path(spec.patch_directory)
    if relative.is_absolute() or ".." in relative.parts:
        raise ValueError("patch directory is outside the controlling repository")
    current = repository_root
    for component in relative.parts:
        if component in ("", "."):
            continue
        current /= component
        if os.path.lexists(current) and _is_link_or_reparse(current):
            raise ValueError(f"patch directory contains a symlink or reparse point: {current}")
    candidate = (repository_root / relative).resolve()
    try:
        common = Path(os.path.commonpath([str(repository_root), str(candidate)]))
    except ValueError as exc:
        raise ValueError("patch directory is outside the controlling repository") from exc
    if _same_path(candidate, repository_root) or not _same_path(common, repository_root):
        raise ValueError("patch directory is outside the controlling repository")
    if not candidate.is_dir():
        raise ValueError(f"patch directory not found: {candidate}")
    return candidate


def _controlled_patches(spec: PatchWorkspaceSpec) -> list[ControlledPatch]:
    if not _clean(REPO_ROOT):
        raise ValueError("controlling repository is dirty; patch provenance is not reproducible")
    patch_root = _patch_root(spec)
    paths = sorted(
        (path for path in patch_root.iterdir() if path.suffix == ".patch"),
        key=lambda path: path.name,
    )
    patches: list[ControlledPatch] = []
    for patch in paths:
        if _is_link_or_reparse(patch):
            raise ValueError(f"patch is a symlink or reparse point: {patch}")
        if not patch.is_file():
            raise ValueError(f"patch is not a regular file: {patch}")
        relative = patch.relative_to(REPO_ROOT.resolve()).as_posix()
        _git(REPO_ROOT, "ls-files", "--error-unmatch", "--", relative)
        head_blob = _git(REPO_ROOT, "rev-parse", f"HEAD:{relative}")
        raw_bytes = patch.read_bytes()
        working_blob = _git(REPO_ROOT, "hash-object", "--stdin", input_bytes=raw_bytes)
        if working_blob != head_blob:
            raise ValueError(f"patch is not identical to controlling repository HEAD: {relative}")
        patches.append(
            ControlledPatch(
                path=patch,
                relative_path=relative,
                name=patch.name,
                blob_id=head_blob,
                sha256=hashlib.sha256(raw_bytes).hexdigest(),
                raw_bytes=raw_bytes,
                apply_bytes=raw_bytes.replace(b"\r\n", b"\n"),
            )
        )
    return patches


def _patch_evidence(patches: list[ControlledPatch]) -> list[dict[str, str]]:
    return [
        {
            "name": patch.name,
            "blob_id": patch.blob_id,
            "sha256": patch.sha256,
        }
        for patch in patches
    ]


def _revalidate_controlled_patches(
    patches: list[ControlledPatch],
    spec: PatchWorkspaceSpec,
) -> None:
    try:
        if not _clean(REPO_ROOT):
            raise ValueError("controlling repository is dirty")
        patch_root = _patch_root(spec)
        current_names = sorted(
            path.name for path in patch_root.iterdir() if path.suffix == ".patch"
        )
        if current_names != [patch.name for patch in patches]:
            raise ValueError("controlled patch set changed")
        for patch in patches:
            if _is_link_or_reparse(patch.path) or not patch.path.is_file():
                raise ValueError(f"controlled patch path changed: {patch.relative_path}")
            raw_bytes = patch.path.read_bytes()
            if raw_bytes != patch.raw_bytes:
                raise ValueError(f"controlled patch bytes changed: {patch.relative_path}")
            if hashlib.sha256(raw_bytes).hexdigest() != patch.sha256:
                raise ValueError(f"controlled patch SHA-256 changed: {patch.relative_path}")
            head_blob = _git(REPO_ROOT, "rev-parse", f"HEAD:{patch.relative_path}")
            working_blob = _git(REPO_ROOT, "hash-object", "--stdin", input_bytes=raw_bytes)
            if head_blob != patch.blob_id or working_blob != patch.blob_id:
                raise ValueError(f"controlled patch blob changed: {patch.relative_path}")
    except (OSError, ValueError) as exc:
        raise ValueError(f"controlling repository changed during patch preparation: {exc}") from exc


def _create_exact_checkout(
    upstream: Path,
    destination: Path,
    expected_commit: str,
    patches: list[ControlledPatch],
    spec: PatchWorkspaceSpec,
) -> str:
    destination.parent.mkdir(parents=True, exist_ok=True)
    _git(None, "clone", "--local", "--no-hardlinks", "--no-checkout", str(upstream), str(destination))
    _git(destination, "checkout", "--detach", expected_commit)
    _git(destination, "checkout", "-b", spec.branch)
    for patch in patches:
        _git(destination, "apply", "--cached", "--check", "-", input_bytes=patch.apply_bytes)
        _git(destination, "apply", "--cached", "-", input_bytes=patch.apply_bytes)

    if _git(destination, "diff", "--cached", "--name-only"):
        tree = _git(destination, "write-tree")
        timestamp = _git(upstream, "show", "-s", "--format=%cI", expected_commit)
        commit_env = os.environ.copy()
        commit_env.update(
            {
                "GIT_AUTHOR_NAME": "IBM Granite Project",
                "GIT_AUTHOR_EMAIL": "project@example.invalid",
                "GIT_AUTHOR_DATE": timestamp,
                "GIT_COMMITTER_NAME": "IBM Granite Project",
                "GIT_COMMITTER_EMAIL": "project@example.invalid",
                "GIT_COMMITTER_DATE": timestamp,
            }
        )
        patch_commit = _git(
            destination,
            "commit-tree",
            tree,
            "-p",
            expected_commit,
            env=commit_env,
            input_text=f"{spec.commit_message}\n",
        )
        _git(destination, "update-ref", f"refs/heads/{spec.branch}", patch_commit)
        _git(destination, "reset", "--hard", patch_commit)
    else:
        patch_commit = expected_commit

    _git(destination, "submodule", "update", "--init", "--recursive")
    if not _clean(destination):
        raise ValueError("derived destination is dirty after preparation")
    _git(destination, "config", "core.longpaths", "true")
    return patch_commit


def _create_and_publish_checkout(
    upstream: Path,
    destination: Path,
    expected_commit: str,
    patches: list[ControlledPatch],
    spec: PatchWorkspaceSpec,
) -> str:
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging_root = Path(
        tempfile.mkdtemp(
            prefix=f".{destination.name}.preparing-",
            dir=destination.parent,
        )
    )
    staging = staging_root / "checkout"
    try:
        patch_commit = _create_exact_checkout(
            upstream,
            staging,
            expected_commit,
            patches,
            spec,
        )
        if destination.exists():
            raise ValueError("destination appeared while patch workspace was being prepared")
        os.replace(staging, destination)
        return patch_commit
    finally:
        shutil.rmtree(staging_root, ignore_errors=True)


def _verify_existing(
    upstream: Path,
    destination: Path,
    expected_head: str,
    spec: PatchWorkspaceSpec,
) -> None:
    if not (destination / ".git").exists():
        raise ValueError("existing destination is not a Git checkout")
    if not _clean(destination):
        raise ValueError("existing destination is dirty")
    branch = _git(destination, "branch", "--show-current")
    if branch != spec.branch:
        raise ValueError(
            f"existing destination branch mismatch: expected {spec.branch}, found {branch}"
        )
    origin = _git(destination, "remote", "get-url", "origin")
    if not _same_path(origin, upstream):
        raise ValueError(f"existing destination origin mismatch: expected {upstream}, found {origin}")
    actual_head = _git(destination, "rev-parse", "HEAD")
    if actual_head != expected_head:
        raise ValueError(
            f"existing destination derived HEAD mismatch: expected {expected_head}, found {actual_head}"
        )
    _git(destination, "config", "core.longpaths", "true")


def spec_for_family(name: str) -> PatchWorkspaceSpec:
    try:
        return FAMILY_SPECS[name]
    except KeyError as exc:
        raise ValueError(f"unknown patch family: {name}") from exc


def prepare_patch_workspace(
    upstream: Path,
    destination: Path,
    expected_commit: str,
    spec: PatchWorkspaceSpec | None = None,
) -> dict[str, object]:
    """Create or strictly verify a checkout derived from base plus controlled patches."""
    spec = GENAI_TURBOQUANT_SPEC if spec is None else spec
    upstream = Path(upstream).resolve()
    destination = Path(destination).resolve()
    if _path_at_or_below(destination, upstream):
        raise ValueError("destination must not equal or reside inside the pinned upstream")
    if not (upstream / ".git").exists():
        raise ValueError(f"pinned upstream checkout not found: {upstream}")
    upstream_commit = _git(upstream, "rev-parse", "HEAD")
    if upstream_commit != expected_commit:
        raise ValueError(f"upstream base commit mismatch: expected {expected_commit}, found {upstream_commit}")
    if not _clean(upstream):
        raise ValueError("upstream checkout is dirty")

    try:
        patches = _controlled_patches(spec)
        if destination.exists():
            with tempfile.TemporaryDirectory(prefix="openvino-patch-identity-") as temporary_directory:
                reference = Path(temporary_directory) / "reference"
                expected_head = _create_exact_checkout(
                    upstream,
                    reference,
                    expected_commit,
                    patches,
                    spec,
                )
            _verify_existing(upstream, destination, expected_head, spec)
            patch_commit = expected_head
        else:
            patch_commit = _create_and_publish_checkout(
                upstream,
                destination,
                expected_commit,
                patches,
                spec,
            )
    finally:
        final_upstream_commit = _git(upstream, "rev-parse", "HEAD")
        if final_upstream_commit != expected_commit:
            raise ValueError(
                "pinned upstream changed during patch preparation: "
                f"expected {expected_commit}, found {final_upstream_commit}"
            )
        if not _clean(upstream):
            raise ValueError("pinned upstream became dirty during patch preparation")

    record: dict[str, object] = {
        "upstream_path": str(upstream),
        "destination_path": str(destination),
        "patch_directory": str(_patch_root(spec)),
        "branch": spec.branch,
        "base_commit": expected_commit,
        "upstream_commit": upstream_commit,
        "patch_commit": patch_commit,
        "upstream_tree": _git(upstream, "rev-parse", f"{expected_commit}^{{tree}}"),
        "derived_tree": _git(destination, "rev-parse", f"{patch_commit}^{{tree}}"),
        "applied_patches": [patch.name for patch in patches],
        "patches": _patch_evidence(patches),
        "dirty": False,
    }
    validate_patch_identity(record, expected_commit)
    _revalidate_controlled_patches(patches, spec)
    return record


def validate_patch_identity(record: dict, expected_base: str) -> None:
    """Reject evidence that does not identify a clean, pinned patch workspace."""
    if record.get("base_commit") != expected_base:
        raise ValueError("base commit mismatch")
    if record.get("dirty") is not False:
        raise ValueError("dirty patch workspace")
    if not record.get("patch_commit"):
        raise ValueError("patch commit missing")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--family",
        choices=tuple(sorted(FAMILY_SPECS)),
        default="openvino-genai-turboquant",
    )
    parser.add_argument("--upstream", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--expected-commit", required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    record = prepare_patch_workspace(
        args.upstream,
        args.destination,
        args.expected_commit,
        spec_for_family(args.family),
    )
    args.evidence.parent.mkdir(parents=True, exist_ok=True)
    args.evidence.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared patch workspace: {args.destination.resolve()}")
    print(f"Identity evidence: {args.evidence.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

Run:

```powershell
foreach ($run in 1..2) {
  python -m pytest scripts/testing/tests/test_official_openvino_patch_identity.py -q
  if ($LASTEXITCODE -ne 0) { throw "Patch identity GREEN run $run failed" }
}
python -m py_compile scripts/testing/official_openvino/patch_identity.py
if ($LASTEXITCODE -ne 0) { throw "patch_identity.py compilation failed" }
```

Expected: `26 passed` in each pytest run and a zero Python compilation exit.

## Step 3: Add the CPU-observer PowerShell entry point

Create `scripts/testing/prepare_openvino_cpu_observer_patch.ps1` with this
complete file:

```powershell
[CmdletBinding()]
param(
    [string]$CampaignDate = "2026-07-19",
    [string]$ExpectedCommit = "ede283a88e35465f0d680dabbf1f44080f8fc387",
    [string]$UpstreamPath,
    [string]$DestinationPath,
    [string]$EvidencePath,
    [string]$PythonCommand = "python"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path

if (-not $UpstreamPath) {
    $commonGitDir = (& git -C $repoRoot rev-parse --path-format=absolute --git-common-dir).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the shared controlling repository" }
    $sharedRepoRoot = Split-Path -Parent $commonGitDir
    $UpstreamPath = Join-Path $sharedRepoRoot "external/official-openvino/$CampaignDate/openvino"
}
if (-not $DestinationPath) {
    $DestinationPath = Join-Path $repoRoot "external/official-openvino/$CampaignDate/openvino-cpu-state-observer"
}
if (-not $EvidencePath) {
    $EvidencePath = Join-Path (Split-Path -Parent $DestinationPath) "openvino-cpu-state-observer.identity.json"
}

Push-Location $repoRoot
try {
    & $PythonCommand -m scripts.testing.official_openvino.patch_identity `
        --family openvino-cpu-observer `
        --upstream $UpstreamPath `
        --destination $DestinationPath `
        --expected-commit $ExpectedCommit `
        --evidence $EvidencePath
    if ($LASTEXITCODE -ne 0) {
        throw "OpenVINO CPU observer patch workspace preparation failed ($LASTEXITCODE)"
    }
}
finally {
    Pop-Location
}
```

Create `experiments/patches/openvino-cpu-state-observer/README.md` with this
complete file:

```markdown
# OpenVINO CPU state-allocation observer patch set

This directory contains the portable project patch set applied to the immutable
OpenVINO `2026.2.1` baseline at commit
`ede283a88e35465f0d680dabbf1f44080f8fc387`.

Patch files must use the `.patch` suffix. The workspace controller applies them
in lexical filename order to a local, no-hardlink clone on branch
`project/cpu-state-allocation-observer`. It refuses a dirty upstream,
controlling worktree, or destination and records the ordered patch names,
tracked blob IDs, SHA-256 values, source/derived tree identities, and clean
state beside the derived checkout. New checkouts are prepared in a same-parent
staging directory and published only after complete validation, so a failed
attempt never exposes a partial final destination.

The observer is project instrumentation, not upstream OpenVINO functionality.
It must remain private, default-off, and compiled only for explicitly enabled
CPU debug-capability builds.
```

Do not edit `prepare_openvino_turboquant_patch.ps1`; the new Python default
preserves that existing entry point's GenAI family.

## Step 4: Verify the parent task and commit

Run:

```powershell
python -m pytest scripts/testing/tests/test_official_openvino_patch_identity.py -q
if ($LASTEXITCODE -ne 0) { throw "Final patch identity suite failed" }
python -m py_compile scripts/testing/official_openvino/patch_identity.py
if ($LASTEXITCODE -ne 0) { throw "Final Python compilation failed" }
git diff --check
if ($LASTEXITCODE -ne 0) { throw "Whitespace validation failed" }
git status --short
```

Expected: `26 passed`; only the four task files plus this already-committed
micro-plan differ. Review the diff and confirm the clean upstream remains:

```powershell
$commonGitDir = (& git rev-parse --path-format=absolute --git-common-dir).Trim()
$sharedRepoRoot = Split-Path -Parent $commonGitDir
$upstream = Join-Path $sharedRepoRoot "external/official-openvino/2026-07-19/openvino"
git -C $upstream rev-parse HEAD
git -C $upstream status --porcelain --untracked-files=all
```

Expected: exact `ede283a88e35465f0d680dabbf1f44080f8fc387`, then empty
status output.

Commit only the four implementation files:

```powershell
git add scripts/testing/official_openvino/patch_identity.py `
        scripts/testing/tests/test_official_openvino_patch_identity.py `
        scripts/testing/prepare_openvino_cpu_observer_patch.ps1 `
        experiments/patches/openvino-cpu-state-observer/README.md
git diff --cached --check
git commit -m "build(openvino): prepare CPU observer patch workspace"
```

## Step 5: Materialize and verify the derived core after the commit

First require a clean parent:

```powershell
if (git status --porcelain --untracked-files=all) {
    throw "Parent worktree must be clean before patch materialization"
}
& .\scripts\testing\prepare_openvino_cpu_observer_patch.ps1 `
  -CampaignDate 2026-07-19 `
  -ExpectedCommit ede283a88e35465f0d680dabbf1f44080f8fc387
if ($LASTEXITCODE -ne 0) { throw "CPU observer workspace preparation failed" }

$derived = ".\external\official-openvino\2026-07-19\openvino-cpu-state-observer"
$identity = ".\external\official-openvino\2026-07-19\openvino-cpu-state-observer.identity.json"
git -C $derived rev-parse HEAD
git -C $derived branch --show-current
git -C $derived status --porcelain --untracked-files=all
Get-Content $identity -Raw | ConvertFrom-Json | ConvertTo-Json -Depth 8
```

Acceptance:

- branch is exactly `project/cpu-state-allocation-observer`;
- base and patch commit are full 40-hex IDs;
- empty patch family initially has `patch_commit == base_commit`,
  `upstream_tree == derived_tree`, `applied_patches == []`, and `patches == []`;
- derived status output is empty;
- clean upstream status remains empty;
- no configure, compile, probe, or other heavy child was launched.

Record the commands, exits, identities, and independent spec/quality verdict in
`.superpowers/sdd/progress.md`; that ledger is ignored scratch and is not part
of the task commit.
