# Task 01C: Reject Canonically Long Gitdirs and Publish to a Short Physical Root

> **For the implementer:** Use `superpowers:systematic-debugging` and
> `superpowers:test-driven-development`. This is the narrow follow-up to Task
> 01B commit `fedb96b`. Do not configure or build OpenVINO.

**Observed failure:** Task 01B correctly cloned and initialized under the short
system temp root, then atomically moved the checkout to
`O:\openvino-cpu-state-observer`. Git subsequently canonicalized `O:` to the
long worktree path. The nested
`thirdparty/ocl/clhpp_headers/external/CMock/vendor/c_exception` gitdir became
264 characters, so both `git status --ignore-submodules=none` and
`git submodule status --recursive` exited 128 with `fatal: '$GIT_DIR' too
big`. Branch, commits, and trees were exact, but the published checkout and its
`dirty: false` identity are rejected.

**Goal:** Prove that every initialized submodule gitfile will still address a
Windows-safe physical gitdir after publication, validate the recursively moved
checkout, roll it back into controller-owned staging on failure, and make the
PowerShell controller default to a genuinely short physical root. Keep the
canonical identity evidence in the organized campaign directory.

## Task 1: Add relocation and recursive-validation RED tests

**Files:**

- Modify: `scripts/testing/tests/test_official_openvino_patch_identity.py`

Add three tests to `PatchWorkspaceControllerTests`.

```python
    def test_relocated_submodule_gitdir_allows_220_and_rejects_221(self):
        staging = self.root / "stage"
        worktree = staging / "nested/submodule"
        gitdir = staging / ".git/modules/nested/submodule"
        worktree.mkdir(parents=True)
        gitdir.mkdir(parents=True)
        relative_gitdir = os.path.relpath(gitdir, worktree).replace("\\", "/")
        (worktree / ".git").write_text(
            f"gitdir: {relative_gitdir}\n",
            encoding="utf-8",
        )

        def destination_for_gitdir_length(target: int) -> Path:
            for width in range(1, 201):
                candidate = self.root / ("d" * width)
                relocated = list(
                    patch_identity._relocated_submodule_paths(
                        staging,
                        candidate,
                    )
                )
                if patch_identity._utf8_path_length(relocated[0][2]) == target:
                    return candidate
            self.fail(f"unable to construct {target}-character gitdir")

        allowed = destination_for_gitdir_length(220)
        rejected = destination_for_gitdir_length(221)
        with patch.object(
            patch_identity,
            "WINDOWS_GITDIR_MAX_LENGTH",
            220,
        ), patch.object(
            patch_identity,
            "WINDOWS_PATH_LIMIT",
            260,
        ):
            patch_identity._preflight_relocated_gitdirs(staging, allowed)
            with self.assertRaisesRegex(
                ValueError,
                "physical destination.*Git path limit",
            ):
                patch_identity._preflight_relocated_gitdirs(
                    staging,
                    rejected,
                )

    def test_failed_post_move_recursive_validation_rolls_back_and_cleans(self):
        short_root = self.root / "short-stage"
        moves: list[tuple[Path, Path]] = []
        real_replace = os.replace

        def create_checkout(_upstream, staging, expected, _patches, _spec):
            staging.mkdir(parents=True)
            return expected

        def record_replace(source, destination):
            moves.append((Path(source), Path(destination)))
            real_replace(source, destination)

        with patch.object(
            patch_identity.tempfile,
            "gettempdir",
            return_value=str(short_root),
        ), patch.object(
            patch_identity,
            "_create_exact_checkout",
            side_effect=create_checkout,
        ), patch.object(
            patch_identity,
            "_verify_recursive_checkout",
            side_effect=ValueError("forced recursive failure"),
        ), patch.object(
            patch_identity.os,
            "replace",
            side_effect=record_replace,
        ):
            with self.assertRaisesRegex(ValueError, "forced recursive failure"):
                patch_identity._create_and_publish_checkout(
                    self.upstream,
                    self.destination,
                    self.expected,
                    [],
                    patch_identity.GENAI_TURBOQUANT_SPEC,
                )

        self.assertFalse(self.destination.exists())
        self.assertEqual(list(short_root.glob("openvino-patch-stage-*")), [])
        self.assertEqual(len(moves), 2)
        self.assertEqual(moves[0][1], self.destination)
        self.assertEqual(moves[1][0], self.destination)
        self.assertEqual(moves[0][0], moves[1][1])

    def test_existing_destination_requires_recursive_validation(self):
        self.prepare()
        with patch.object(
            patch_identity,
            "_verify_recursive_checkout",
            side_effect=ValueError("forced recursive failure"),
        ):
            with self.assertRaisesRegex(ValueError, "forced recursive failure"):
                patch_identity._verify_existing(
                    self.upstream,
                    self.destination,
                    self.expected,
                    patch_identity.GENAI_TURBOQUANT_SPEC,
                )
        self.assertTrue(self.destination.is_dir())
```

In the existing `test_new_checkout_uses_short_same_volume_staging_root`, add
this context manager beside its mocked `_create_exact_checkout`:

```python
        ), patch.object(
            patch_identity,
            "_verify_recursive_checkout",
            return_value=None,
            create=True,
```

That test deliberately creates only an empty stand-in directory; it is testing
staging-root selection, not Git integrity. All real-checkout tests continue to
exercise the actual verifier.

In
`test_power_shell_entry_points_preserve_cwd_roots_overrides_and_exit`, replace
the default destination expectation with:

```python
        system_root = Path(f"{os.environ['SystemDrive']}\\")
        expected_destination = (
            system_root
            / "ov-wb04"
            / "2026-07-19"
            / "openvino-cpu-state-observer"
        )
        expected_evidence = (
            repository_root
            / "external/official-openvino/2026-07-19"
            / "openvino-cpu-state-observer.identity.json"
        )
```

Require both paths in the captured controller arguments:

```python
        self.assertIn(str(expected_destination).lower(), captured)
        self.assertIn(str(expected_evidence).lower(), captured)
```

In the same test, invoke the controller through a test-owned directory alias
and prove the default remains rooted at `SystemDrive`:

```python
        alias_root = self.root / "repository alias"
        if os.name == "nt":
            linked = subprocess.run(
                ["cmd", "/c", "mklink", "/J", str(alias_root), str(repository_root)],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(linked.returncode, 0, linked.stdout + linked.stderr)
        else:
            alias_root.symlink_to(repository_root, target_is_directory=True)
        try:
            alias_completed = subprocess.run(
                [
                    "powershell",
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    str(
                        alias_root
                        / "scripts/testing/prepare_openvino_cpu_observer_patch.ps1"
                    ),
                    "-PythonCommand",
                    str(fake),
                ],
                cwd=foreign,
                env=environment,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(
                alias_completed.returncode,
                0,
                alias_completed.stdout + alias_completed.stderr,
            )
            alias_capture = capture.read_text(encoding="utf-8").lower()
            self.assertIn(str(expected_destination).lower(), alias_capture)
        finally:
            if os.name == "nt":
                os.rmdir(alias_root)
            else:
                alias_root.unlink()
```

This proves the default is independent of `$PSScriptRoot`, a worktree path, a
junction, and a substituted repository drive while removing only the exact
test-owned alias.

Run:

```powershell
python -m pytest `
  scripts/testing/tests/test_official_openvino_patch_identity.py `
  -q
```

Expected RED: the three new tests fail for missing relocation/recursive
contracts and the controller-default assertion fails. Existing tests pass.

## Task 2: Implement fail-closed relocation and recursive validation

**Files:**

- Modify: `scripts/testing/official_openvino/patch_identity.py`

Add two explicit Windows limits near the other module constants:

```python
WINDOWS_GITDIR_MAX_LENGTH = 220 if os.name == "nt" else None
WINDOWS_PATH_LIMIT = 260 if os.name == "nt" else None
```

Add helpers before `_create_exact_checkout`:

```python
def _utf8_path_length(path: Path) -> int:
    return len(str(path).encode("utf-8"))


def _relocated_submodule_paths(
    staging: Path,
    destination: Path,
) -> list[tuple[Path, Path, Path]]:
    """Return relative worktree, relocated gitfile, and relocated gitdir paths."""
    destination_physical = destination.resolve()
    relocated: list[tuple[Path, Path, Path]] = []
    for gitfile in staging.rglob(".git"):
        if not gitfile.is_file():
            continue
        line = gitfile.read_text(encoding="utf-8").strip()
        if not line.startswith("gitdir: "):
            raise ValueError(f"malformed submodule gitfile: {gitfile}")
        relative_parent = gitfile.parent.relative_to(staging)
        relocated_gitfile = destination_physical / relative_parent / ".git"
        relocated_gitdir = Path(
            os.path.normpath(
                os.path.join(
                    str(relocated_gitfile.parent),
                    line.removeprefix("gitdir: "),
                )
            )
        )
        relocated.append((relative_parent, relocated_gitfile, relocated_gitdir))
    return relocated


def _preflight_relocated_gitdirs(staging: Path, destination: Path) -> None:
    """Reject submodule gitfiles that become unsafe after physical relocation."""
    if WINDOWS_GITDIR_MAX_LENGTH is None or WINDOWS_PATH_LIMIT is None:
        return
    for relative_parent, relocated_gitfile, relocated_gitdir in (
        _relocated_submodule_paths(staging, destination)
    ):
        if (
            _utf8_path_length(relocated_gitfile) >= WINDOWS_PATH_LIMIT
            or _utf8_path_length(relocated_gitdir) > WINDOWS_GITDIR_MAX_LENGTH
        ):
            raise ValueError(
                "physical destination exceeds the Windows Git path limit "
                f"for {relative_parent}: "
                f"gitfile_bytes={_utf8_path_length(relocated_gitfile)}, "
                f"gitdir_bytes={_utf8_path_length(relocated_gitdir)}, "
                f"gitdir_max={WINDOWS_GITDIR_MAX_LENGTH}, "
                f"path_limit={WINDOWS_PATH_LIMIT}; "
                "choose a shorter physical destination"
            )


def _verify_recursive_checkout(destination: Path) -> None:
    """Require every recursive submodule to be initialized, exact, and clean."""
    status = _git(destination, "submodule", "status", "--recursive")
    for line in status.splitlines():
        if line and line[0] in "-+U":
            raise ValueError(f"recursive submodule state is not exact: {line}")
    nested_dirty = _git(
        destination,
        "submodule",
        "foreach",
        "--quiet",
        "--recursive",
        "git status --porcelain --untracked-files=all",
    )
    if nested_dirty:
        raise ValueError("recursive submodule worktree is dirty")
    if not _clean(destination):
        raise ValueError("derived destination is dirty after recursive validation")
```

In `_create_exact_checkout`, replace its post-submodule `_clean()` check with
`_verify_recursive_checkout(destination)`, then retain
`core.longpaths=true`.

In `_create_and_publish_checkout`, after `_create_exact_checkout` and before
checking whether the destination appeared, call:

```python
        _preflight_relocated_gitdirs(staging, destination)
```

Replace the publication tail with a rollback-protected move:

```python
        os.replace(staging, destination)
        try:
            _verify_recursive_checkout(destination)
        except (OSError, ValueError):
            try:
                os.replace(destination, staging)
            except OSError as rollback_error:
                raise ValueError(
                    "published checkout failed recursive validation and rollback failed"
                ) from rollback_error
            raise
        return patch_commit
```

The existing `finally` must continue to remove only `staging_root`; after a
successful rollback, that deletes the rejected checkout. It must never delete
an independently appearing destination.

In `_verify_existing`, call `_verify_recursive_checkout(destination)` before
reading branch/origin/HEAD and before mutating `core.longpaths`.

Run the focused suite once before changing the PowerShell controller:

```powershell
python -m pytest `
  scripts/testing/tests/test_official_openvino_patch_identity.py `
  -q
```

Expected intermediate result: exactly `31 passed, 1 failed`. The sole failure
must be the controller smoke's intentionally new short-default assertion,
because Task 3 has not changed the controller yet. Any other failure blocks
Task 3.

## Task 3: Make the controller default short and the evidence organized

**Files:**

- Modify: `scripts/testing/prepare_openvino_cpu_observer_patch.ps1`

Replace the two default blocks with:

```powershell
if (-not $DestinationPath) {
    if (-not $env:SystemDrive -or $env:SystemDrive -notmatch "^[A-Za-z]:$") {
        throw "SystemDrive is unavailable or malformed"
    }
    $physicalSystemRoot = "$($env:SystemDrive)\"
    $DestinationPath = Join-Path $physicalSystemRoot "ov-wb04/$CampaignDate/openvino-cpu-state-observer"
}
if (-not $EvidencePath) {
    $EvidencePath = Join-Path $repoRoot "external/official-openvino/$CampaignDate/openvino-cpu-state-observer.identity.json"
}
```

Run the full acceptance twice:

```powershell
foreach ($run in 1..2) {
  python -m pytest `
    scripts/testing/tests/test_official_openvino_patch_identity.py `
    -q
  if ($LASTEXITCODE -ne 0) { throw "Task 01C GREEN run $run failed" }
}
python -m py_compile scripts/testing/official_openvino/patch_identity.py
git diff --check
```

Expected: exactly `32 passed` twice, zero skips, and zero exits from compile
and diff checks. Confirm the PowerShell smoke proves explicit overrides remain
unchanged, controller invocation through a repository junction still selects
the physical system-drive root, and the default evidence is not placed beside
the external short checkout. The Python relocation preflight remains the final
fail-closed authority if a host reports an unusual canonical system-drive
mapping.

Commit only the three implementation files:

```powershell
git add scripts/testing/official_openvino/patch_identity.py `
        scripts/testing/tests/test_official_openvino_patch_identity.py `
        scripts/testing/prepare_openvino_cpu_observer_patch.ps1
git diff --cached --check
git commit -m "fix(openvino): validate relocated submodule paths"
```

Require independent Spec PASS and Quality PASS before operational use.

## Task 4: Rematerialize once, verify, and retire the rejected checkout

No OpenVINO configure, build, or test command is allowed in this task.

1. Confirm the parent and pinned upstream are clean, the new default
   `C:\ov-wb04\2026-07-19\openvino-cpu-state-observer` is absent, available
   RAM is reported, and no process command line references the controller,
   either destination, or `openvino-patch-stage-`.
2. Preserve the rejected `O:\openvino-cpu-state-observer` and its stale
   identity until the short replacement has passed every acceptance check.
3. Run the controller once in a hidden monitored PowerShell process using the
   new default destination and a temporary replacement evidence path. Keep the
   parent PowerShell process alive so it owns the child handle and can report
   the real exit code:

```powershell
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$stdout = Join-Path $PWD ".superpowers/sdd/task01c-$stamp.stdout.log"
$stderr = Join-Path $PWD ".superpowers/sdd/task01c-$stamp.stderr.log"
$controller = (Resolve-Path `
  ".\scripts\testing\prepare_openvino_cpu_observer_patch.ps1").Path
$arguments = @(
  "-NoProfile",
  "-ExecutionPolicy", "Bypass",
  "-File", $controller,
  "-CampaignDate", "2026-07-19",
  "-ExpectedCommit", "ede283a88e35465f0d680dabbf1f44080f8fc387",
  "-EvidencePath", "O:\openvino-cpu-state-observer.replacement.identity.json"
)
$process = Start-Process powershell.exe `
  -ArgumentList $arguments `
  -WorkingDirectory $PWD `
  -WindowStyle Hidden `
  -RedirectStandardOutput $stdout `
  -RedirectStandardError $stderr `
  -PassThru
while (-not $process.WaitForExit(10000)) {
  $all = Get-CimInstance Win32_Process
  $owned = @($process.Id)
  do {
    $before = $owned.Count
    $owned += @(
      $all |
        Where-Object { $owned -contains $_.ParentProcessId } |
        Select-Object -ExpandProperty ProcessId
    )
    $owned = @($owned | Sort-Object -Unique)
  } while ($owned.Count -gt $before)
  $os = Get-CimInstance Win32_OperatingSystem
  [pscustomobject]@{
    pid = $process.Id
    available_ram_mib = [math]::Round($os.FreePhysicalMemory / 1024, 0)
    owned_processes = $owned.Count
    destination_exists = Test-Path `
      "C:\ov-wb04\2026-07-19\openvino-cpu-state-observer"
    evidence_exists = Test-Path `
      "O:\openvino-cpu-state-observer.replacement.identity.json"
  } | ConvertTo-Json -Compress
}
$process.Refresh()
if ($process.ExitCode -ne 0) {
  Get-Content -LiteralPath $stderr -Tail 80
  throw "Task 01C materialization failed with exit $($process.ExitCode)"
}
if ((Get-Item -LiteralPath $stderr).Length -ne 0) {
  throw "Task 01C materialization wrote stderr"
}
Get-Content -LiteralPath $stdout -Tail 40
```

The shell runner may yield between ten-second samples; resume that same shell
cell rather than starting another controller. Report progress to the user at
least every 60 seconds. Never infer success merely because the root PID exited.

4. Accept only:
   - branch `project/cpu-state-allocation-observer`;
   - base/upstream/patch/HEAD commit
     `ede283a88e35465f0d680dabbf1f44080f8fc387`;
   - equal upstream/derived trees;
   - empty `applied_patches` and `patches`;
   - physical destination under `C:\ov-wb04\2026-07-19`;
   - `git status --porcelain --untracked-files=all
     --ignore-submodules=none` exit 0 and empty;
   - `git submodule status --recursive` exit 0 with no `-`, `+`, or `U`
     prefixes;
   - recursive submodule `status --porcelain` exit 0 and empty;
   - clean pinned upstream and parent; and
   - zero relevant surviving processes.
5. Only after acceptance, run the following resumable, single-process identity
   publication and retirement transaction. It validates both identities and
   the rejected branch/HEAD before mutation. It atomically quarantines the
   exact stale identity, publishes the accepted replacement with no-overwrite
   `os.rename`, and only then retires the rejected checkout. If publication
   fails it restores the stale identity; if deletion later fails, the accepted
   canonical identity remains valid and the quarantine makes a retry safe. The
   live-process query excludes only the retirement Python process and its
   ancestors, preventing the owning Codex/PowerShell command from
   self-matching while still detecting unrelated users of the rejected path.

```powershell
$env:REJECTED_OPERATION = "O:\openvino-cpu-state-observer"
$env:REJECTED_CAMPAIGN = (
  Resolve-Path ".\external\official-openvino\2026-07-19"
).Path
$env:QUARANTINED_IDENTITY = `
  "O:\openvino-cpu-state-observer.task01b-rejected.identity.json"
$env:REPLACEMENT_IDENTITY = `
  "O:\openvino-cpu-state-observer.replacement.identity.json"
$env:CANONICAL_IDENTITY = "O:\openvino-cpu-state-observer.identity.json"
$env:ACCEPTED_DESTINATION = `
  "C:\ov-wb04\2026-07-19\openvino-cpu-state-observer"
$retirement = @'
import json
import os
import subprocess
from pathlib import Path

from scripts.testing.official_openvino.patch_identity import _remove_staging_tree

EXPECTED = "ede283a88e35465f0d680dabbf1f44080f8fc387"
operation = Path(os.environ["REJECTED_OPERATION"])
physical = operation.resolve()
extended_physical = Path("\\\\?\\" + str(physical))
campaign = Path(os.environ["REJECTED_CAMPAIGN"]).resolve()
quarantine = Path(os.environ["QUARANTINED_IDENTITY"])
replacement = Path(os.environ["REPLACEMENT_IDENTITY"])
canonical = Path(os.environ["CANONICAL_IDENTITY"])
accepted = Path(os.environ["ACCEPTED_DESTINATION"]).resolve()

def same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(str(left.resolve())) == os.path.normcase(
        str(right.resolve())
    )

if not same_path(physical.parent, campaign) or (
    physical.name != "openvino-cpu-state-observer"
):
    raise SystemExit(f"refusing out-of-scope retirement: {physical}")

def git(repository: Path, *arguments: str) -> str:
    completed = subprocess.run(
        ["git", "-c", "core.longpaths=true", "-C", str(repository), *arguments],
        check=False,
        capture_output=True,
        text=True,
    )
    if completed.returncode:
        raise SystemExit(completed.stderr or completed.stdout)
    return completed.stdout.strip()

def load_record(path: Path) -> dict:
    if not path.is_file():
        raise SystemExit(f"identity is missing: {path}")
    return json.loads(path.read_text(encoding="utf-8"))

def validate_common(record: dict, label: str) -> None:
    if record.get("branch") != "project/cpu-state-allocation-observer":
        raise SystemExit(f"{label} branch mismatch")
    for key in ("base_commit", "upstream_commit", "patch_commit"):
        if record.get(key) != EXPECTED:
            raise SystemExit(f"{label} {key} mismatch")
    if record.get("applied_patches") != [] or record.get("patches") != []:
        raise SystemExit(f"{label} patch set is not empty")
    if record.get("dirty") is not False:
        raise SystemExit(f"{label} is dirty")
    if (
        not record.get("upstream_tree")
        or record.get("upstream_tree") != record.get("derived_tree")
    ):
        raise SystemExit(f"{label} tree identity mismatch")

def validate_rejected(record: dict) -> None:
    validate_common(record, "rejected identity")
    if not same_path(Path(record["destination_path"]), physical):
        raise SystemExit("rejected identity physical destination mismatch")
    if os.path.normcase(record["destination_operation_path"]) != (
        os.path.normcase(str(operation))
    ):
        raise SystemExit("rejected identity operation destination mismatch")

def validate_accepted(record: dict) -> None:
    validate_common(record, "accepted identity")
    if not same_path(Path(record["destination_path"]), accepted):
        raise SystemExit("accepted identity destination mismatch")

if canonical.is_file():
    canonical_record = load_record(canonical)
    try:
        validate_accepted(canonical_record)
        canonical_is_accepted = True
    except SystemExit:
        validate_rejected(canonical_record)
        canonical_is_accepted = False
else:
    canonical_record = None
    canonical_is_accepted = False

if canonical_is_accepted:
    if replacement.exists():
        raise SystemExit("replacement remains beside an accepted canonical identity")
    if quarantine.exists():
        validate_rejected(load_record(quarantine))
    elif operation.exists():
        raise SystemExit("accepted identity has no retirement quarantine")
    record = canonical_record
else:
    if not operation.exists():
        raise SystemExit("rejected checkout is missing before identity publication")
    if git(operation, "branch", "--show-current") != (
        "project/cpu-state-allocation-observer"
    ):
        raise SystemExit("rejected checkout branch changed")
    if git(operation, "rev-parse", "HEAD") != EXPECTED:
        raise SystemExit("rejected checkout HEAD changed")
    replacement_record = load_record(replacement)
    validate_accepted(replacement_record)
    if not same_path(replacement.parent, canonical.parent):
        raise SystemExit("replacement identity is not beside the canonical identity")
    if canonical.is_file():
        if quarantine.exists():
            raise SystemExit("retirement quarantine already exists")
        os.rename(canonical, quarantine)
    else:
        validate_rejected(load_record(quarantine))
    try:
        os.rename(replacement, canonical)
    except OSError:
        if not canonical.exists() and quarantine.exists():
            os.rename(quarantine, canonical)
        raise
    record = load_record(canonical)
    validate_accepted(record)

query_environment = os.environ.copy()
query_environment["REJECTED_PHYSICAL"] = str(physical)
query_environment["RETIREMENT_PYTHON_PID"] = str(os.getpid())
process_query = r'''
$all = @(Get-CimInstance Win32_Process)
$byPid = @{}
foreach ($process in $all) {
  $byPid[[int]$process.ProcessId] = $process
}
$excluded = New-Object "System.Collections.Generic.HashSet[int]"
$cursor = [int]$env:RETIREMENT_PYTHON_PID
while ($cursor -gt 0 -and $byPid.ContainsKey($cursor)) {
  [void]$excluded.Add($cursor)
  $cursor = [int]$byPid[$cursor].ParentProcessId
}
$terms = @($env:REJECTED_OPERATION, $env:REJECTED_PHYSICAL)
$matches = @(
  $all | Where-Object {
    $command = $_.CommandLine
    (-not $excluded.Contains([int]$_.ProcessId)) -and
      [bool]$command -and
      (
        @(
          $terms | Where-Object {
            $command.IndexOf(
              $_,
              [StringComparison]::OrdinalIgnoreCase
            ) -ge 0
          }
        ).Count -gt 0
      )
  }
)
$matches.Count
'''
processes = subprocess.run(
    ["powershell", "-NoProfile", "-Command", process_query],
    check=False,
    capture_output=True,
    text=True,
    env=query_environment,
)
if operation.exists():
    if processes.returncode or processes.stdout.strip() != "0":
        raise SystemExit(
            "a process still references the rejected checkout: "
            + processes.stdout
            + processes.stderr
        )
    _remove_staging_tree(extended_physical)
    if operation.exists() or physical.exists():
        raise SystemExit("rejected checkout still exists after retirement")

published = json.loads(canonical.read_text(encoding="utf-8"))
validate_accepted(published)
if published != record or replacement.exists():
    raise SystemExit("canonical identity publication did not preserve evidence")
if quarantine.exists():
    validate_rejected(load_record(quarantine))
    quarantine.unlink()
'@
$retirement | python -
if ($LASTEXITCODE -ne 0) {
  throw "Task 01C retirement/publication transaction failed"
}
```

Do not route deletion through `cmd`, a batch builtin, or a second shell. The
identity itself is the organized location pointer; do not create a junction.

6. Require a second independent read-only verification of the final short
   workspace, canonical identity, clean repositories, and zero process tree.

Only then may the guard-bootstrap implementation plan be executed. Heavy
OpenVINO work remains forbidden until that guard passes its own reviews and
deterministic `101 passed` acceptance.
