# Task 01B: Use a Genuinely Short Same-Volume Staging Root

> **For the implementer:** Use `superpowers:systematic-debugging` and
> `superpowers:test-driven-development`. This is the narrow follow-up to Task
> 01A commit `b9927fb`. Do not configure or build OpenVINO.

**Root cause:** Python preserved the explicit `O:` alias, but Git canonicalized
the substituted drive internally and wrote the full physical path into nested
submodule Git directories. Recursive initialization therefore still failed
with `Filename too long` and `fatal: '$GIT_DIR' too big`. A direct diagnostic
proved `os.replace()` can atomically move a directory from the system temp
directory to the `O:` destination because both are on the same physical
volume.

**Goal:** Create and fully validate the checkout under a genuinely short system
temp path on the destination's physical volume, then atomically publish it to
the requested destination. Clean failed staging trees deterministically,
including read-only Git files.

### Task 1: Add short-root tests and implement same-volume publication

**Files:**

- Modify: `scripts/testing/tests/test_official_openvino_patch_identity.py`
- Modify: `scripts/testing/official_openvino/patch_identity.py`

Add these exact tests to `PatchWorkspaceControllerTests`:

```python
    def test_new_checkout_uses_short_same_volume_staging_root(self):
        short_root = self.root / "short-stage"
        observed: list[Path] = []

        def create_checkout(_upstream, staging, expected, _patches, _spec):
            observed.append(staging)
            staging.mkdir(parents=True)
            return expected

        destination = self.root / "very/long/parent/derived"
        with patch.object(
            patch_identity.tempfile,
            "gettempdir",
            return_value=str(short_root),
        ), patch.object(
            patch_identity,
            "_create_exact_checkout",
            side_effect=create_checkout,
        ):
            commit = patch_identity._create_and_publish_checkout(
                self.upstream,
                destination,
                self.expected,
                [],
                patch_identity.GENAI_TURBOQUANT_SPEC,
            )
        self.assertEqual(commit, self.expected)
        self.assertEqual(len(observed), 1)
        self.assertEqual(observed[0].parent.parent, short_root)
        self.assertTrue(destination.is_dir())
        self.assertEqual(list(short_root.glob("openvino-patch-stage-*")), [])

    def test_short_staging_root_must_share_destination_volume(self):
        with patch.object(
            patch_identity.tempfile,
            "gettempdir",
            return_value="Z:/openvino-stage",
        ), patch.object(
            patch_identity.os.path,
            "splitdrive",
            side_effect=lambda value: (
                ("Z:", "") if str(value).upper().startswith("Z:") else ("C:", "")
            ),
        ):
            with self.assertRaisesRegex(ValueError, "same volume"):
                patch_identity._short_staging_parent(self.destination)
```

In the existing
`test_failed_staging_creation_cleans_up_and_retry_succeeds`, define:

```python
        short_root = self.root / "short-stage"
```

Wrap both the failing call and the retry in:

```python
        with patch.object(
            patch_identity.tempfile,
            "gettempdir",
            return_value=str(short_root),
        ):
```

Replace its old destination-parent staging assertion with:

```python
        self.assertEqual(list(short_root.glob("openvino-patch-stage-*")), [])
```

Run the focused suite. Expected RED: two new failures; the existing 27 tests
pass.

Add this exact helper after `_operational_path`:

```python
def _short_staging_parent(destination: Path) -> Path:
    """Return a short staging directory on the destination's physical volume."""
    staging_parent = _operational_path(tempfile.gettempdir())
    destination_physical = destination.resolve()
    staging_physical = staging_parent.resolve()
    destination_drive = os.path.splitdrive(str(destination_physical))[0]
    staging_drive = os.path.splitdrive(str(staging_physical))[0]
    if os.path.normcase(destination_drive) != os.path.normcase(staging_drive):
        raise ValueError(
            "short patch staging root must be on the same volume as destination"
        )
    staging_parent.mkdir(parents=True, exist_ok=True)
    return staging_parent


def _remove_staging_tree(path: Path) -> None:
    """Remove only the controller-owned short staging tree."""
    def make_writable_and_retry(function, failed_path, _error) -> None:
        os.chmod(failed_path, stat.S_IWRITE)
        function(failed_path)

    shutil.rmtree(path, onerror=make_writable_and_retry)
```

In `_create_and_publish_checkout`, replace the current staging-root
construction:

```python
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging_parent = _short_staging_parent(destination)
    staging_root = Path(
        tempfile.mkdtemp(
            prefix="openvino-patch-stage-",
            dir=staging_parent,
        )
    )
```

Keep `staging = staging_root / "checkout"` and the existing atomic
`os.replace(staging, destination)`. Replace the current ignored cleanup:

```python
        shutil.rmtree(staging_root, ignore_errors=True)
```

with:

```python
        if staging_root.exists():
            _remove_staging_tree(staging_root)
```

Run twice:

```powershell
foreach ($run in 1..2) {
  python -m pytest `
    scripts/testing/tests/test_official_openvino_patch_identity.py `
    -q
  if ($LASTEXITCODE -ne 0) { throw "Task 01B GREEN run $run failed" }
}
python -m py_compile scripts/testing/official_openvino/patch_identity.py
git diff --check
```

Expected: `29 passed` twice, zero skips, and zero exits from compilation/diff
checks.

Commit only the two implementation files:

```powershell
git add scripts/testing/official_openvino/patch_identity.py `
        scripts/testing/tests/test_official_openvino_patch_identity.py
git diff --cached --check
git commit -m "fix(openvino): shorten patch staging root"
```

After independent review, require no final destination, identity, or stale
staging directory. Run the controller once with the existing `O:` destination.
Accept only exact branch/base/HEAD/tree/empty-patch identities, clean parent and
upstream, successful recursive submodule status, and zero relevant surviving
processes.
