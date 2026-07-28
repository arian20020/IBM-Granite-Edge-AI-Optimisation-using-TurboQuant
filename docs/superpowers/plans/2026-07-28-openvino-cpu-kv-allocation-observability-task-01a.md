# Task 01A: Preserve a Short Windows Patch-Staging Path

> **For the implementer:** Use `superpowers:systematic-debugging` and
> `superpowers:test-driven-development`. This is the narrow corrective task
> discovered after Task 01 commit `6a3dbfe`. Do not run a build.

**Observed failure:** recursive OpenVINO submodule initialization from the full
same-parent staging path exited `1` with `Filename too long` and
`fatal: '$GIT_DIR' too big`. The controller removed the failed staging tree,
published no destination, left zero child processes, and kept both repositories
clean. A `subst` drive is ineffective while `Path.resolve()` expands it before
staging.

**Goal:** Preserve an explicitly supplied absolute drive alias for filesystem
operations while retaining the fully resolved physical destination in identity
evidence.

### Task 1: Add the drive-alias regression and minimal implementation

**Files:**

- Modify: `scripts/testing/tests/test_official_openvino_patch_identity.py`
- Modify: `scripts/testing/official_openvino/patch_identity.py`

Add this exact test to `PatchWorkspaceControllerTests`:

```python
    @unittest.skipUnless(os.name == "nt", "Windows drive-alias contract")
    def test_operational_path_preserves_explicit_windows_drive_alias(self):
        alias = Path("O:/openvino-cpu-state-observer")
        operational = patch_identity._operational_path(alias)
        self.assertEqual(operational.drive.upper(), "O:")
        self.assertEqual(
            operational,
            Path("O:/openvino-cpu-state-observer"),
        )
```

Run:

```powershell
python -m pytest `
  scripts/testing/tests/test_official_openvino_patch_identity.py `
  -q
```

Expected RED: one failure because `_operational_path` does not exist; the other
26 tests pass.

Add this exact helper immediately after `_same_path`:

```python
def _operational_path(path: str | Path) -> Path:
    """Make a path absolute without dereferencing a Windows drive alias."""
    candidate = Path(os.path.abspath(os.fspath(path)))
    if not candidate.is_absolute():
        raise ValueError(f"operational path is not absolute: {path}")
    return candidate
```

In `prepare_patch_workspace`, replace:

```python
    destination = Path(destination).resolve()
```

with:

```python
    destination = _operational_path(destination)
```

In the identity record replace:

```python
        "destination_path": str(destination),
```

with:

```python
        "destination_path": str(destination.resolve()),
        "destination_operation_path": str(destination),
```

Run twice:

```powershell
foreach ($run in 1..2) {
  python -m pytest `
    scripts/testing/tests/test_official_openvino_patch_identity.py `
    -q
  if ($LASTEXITCODE -ne 0) { throw "Task 01A GREEN run $run failed" }
}
python -m py_compile scripts/testing/official_openvino/patch_identity.py
git diff --check
```

Expected: `27 passed` twice and zero exits from compilation/diff checks.

Commit only the two implementation files:

```powershell
git add scripts/testing/official_openvino/patch_identity.py `
        scripts/testing/tests/test_official_openvino_patch_identity.py
git diff --cached --check
git commit -m "fix(openvino): preserve short observer staging path"
```

After the commit, require a clean parent and map `O:` to the intended physical
campaign directory. Invoke the existing controller exactly once with:

```powershell
& .\scripts\testing\prepare_openvino_cpu_observer_patch.ps1 `
  -CampaignDate 2026-07-19 `
  -ExpectedCommit ede283a88e35465f0d680dabbf1f44080f8fc387 `
  -DestinationPath O:\openvino-cpu-state-observer `
  -EvidencePath O:\openvino-cpu-state-observer.identity.json
```

Accept only exact branch `project/cpu-state-allocation-observer`, exact HEAD
`ede283a88e35465f0d680dabbf1f44080f8fc387`, empty status, physical
`destination_path` under the parent campaign directory,
`destination_operation_path == "O:\\openvino-cpu-state-observer"`, empty patch
arrays, equal upstream/derived trees, clean immutable upstream, and zero
surviving preparation children.
