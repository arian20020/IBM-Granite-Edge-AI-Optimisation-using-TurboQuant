# OpenVINO CPU KV Allocation Observability Task 03 Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to
> implement this plan task by task. Do not begin Task 03 until the accepted
> Task 02 commit has explicit independent `SPEC PASS` and `QUALITY PASS`.

**Goal:** Add the default-off, CPU-debug-capability-only JSONL observer trigger
at the private CPU `query_state()` boundary, with strict opt-in path and model
context parsing, process-wide non-reused request IDs and monotonic snapshot
sequence numbers, exact inference/reset phase tracking, whole-line concurrent
writes, and no public API, payload content, requested-device claim, or raw
address.

**Architecture:** Keep all declarations, state, parsing, serialization,
writing, and request hooks lexically inside `CPU_DEBUG_CAPS`. Extend the
accepted Task 02 private snapshot module with a bounded duplicate-key-rejecting
JSON validator, a serialized-line writer, immutable model context parsing, a
small request-lifecycle state machine, and the final capture-and-append
function. `DebugCapsConfig` reads exactly
`OV_CPU_STATE_ALLOCATION_DUMP_PATH`; `CompiledModel` parses private nested
model `rt_info` only when that config is enabled; each enabled
`SyncInferRequest` receives a process-wide never-reused ID. Every inference
attempt clears success before any cancellation or graph work and marks success
only after `Graph::Infer` and `Graph::PullOutputData` both finish. A leaf CPU
request appends before public state wrappers are constructed; a delegating
request emits no aggregate line.

**Tech Stack:** OpenVINO pinned at
`ede283a88e35465f0d680dabbf1f44080f8fc387`, C++17, GTest, CMake/Visual
Studio 18 2026, PowerShell, Git, the accepted owned-process Job Object guard,
and the private Intel CPU plugin implementation.

---

## Non-negotiable execution contract

1. Work only in the accepted physical derived OpenVINO checkout
   `C:\ov-wb04\2026-07-19\openvino-cpu-state-observer`, exposed by the already
   accepted `O:` substitution. Do not create, delete, or remap `O:` in this
   task.
2. Treat the parent repository, clean upstream checkout, canonical
   materialization identity, Task 02 plan, Task 02 receipt, and Task 02 review
   evidence as read-only.
3. The Task 02 commit is the immutable Task 03 preimage. Freeze the exact blob
   ID of every Task 03 path before editing and reject any drift.
4. Use strict RED-GREEN TDD. The first unit build must fail for the absent Task
   03 contracts, not for environment, memory, path, generator, or stale-build
   problems.
5. Every Python process, CMake configure, build, compiled test, and compiled
   probe must run through
   `R:\scripts\testing\invoke_guarded_command.ps1` with the fixed 2,048 MiB
   minimum. No direct retry is allowed. A retry gets a fresh attempt directory
   and fresh labels. Execute Steps 1-13 for one attempt in one persistent
   Windows PowerShell 5.1 controller process. That process sets only
   `Process=Bypass`; every wrapper block reasserts it and proves
   `CurrentUser` and `LocalMachine` stayed unchanged. An interrupted
   controller restarts at Step 1 with a new attempt number.
6. After every guarded command, require a valid guard receipt, observed RAM at
   or above 2,147,483,648 bytes, suspended creation and assignment before
   resume, zero active Job processes, zero survivor PIDs, no timeout, no
   low-memory stop, no emergency action, and a matching log SHA-256.
7. Do not weaken Task 02 capture/serialization behavior. Do not materialize a
   public state tensor, infer retained capacity from descriptor size, serialize
   prompts, generated text, tensor contents, addresses, requested devices, or
   actual execution devices.
8. Do not add a public property, supported-property entry, exported symbol
   annotation, CLI switch, or second environment variable.
9. The observer is fail-closed when enabled: invalid path, missing or malformed
   private context, invalid JSON, duplicate keys, request/sequence exhaustion,
   capture failure, serialization failure, open/write/flush failure, or a null
   private state must prevent a successful `query_state()` result.
10. Task 03 never runs an enabled, valid production `query_state()` and never
    accepts a physical observation. Closing the performance aggregation
    window and receiving controller acknowledgement are caller/controller
    protocol obligations: every later enabled consumer, beginning with the
    Task 04 synthetic probe and finally the Task 06 controller, must prove
    that ordering before it invokes `query_state()`. Do not add a second
    environment variable or model-truth field to fake that acknowledgement.
11. Ordinary both-options-OFF binary and symbol absence remains owned by
    roadmap Task 07. Task 03 must prove exact lexical containment now and must
    not claim the later compiled ordinary-route gate has run.
12. Commit only the nine Task 03 paths with subject
    `feat(cpu): emit opt-in state allocation snapshots`. Do not push, export
    the tracked patch, update the canonical identity JSON, or run a parent
    publication controller.
13. Task 03 is accepted only after independent `SPEC PASS` and `QUALITY PASS`
    name the same 40-hex Task 03 commit.

## Exact Task 03 path set

Modify:

- `src/plugins/intel_cpu/src/compiled_model.h`
- `src/plugins/intel_cpu/src/compiled_model.cpp`
- `src/plugins/intel_cpu/src/utils/debug_caps_config.h`
- `src/plugins/intel_cpu/src/utils/debug_caps_config.cpp`
- `src/plugins/intel_cpu/src/infer_request.h`
- `src/plugins/intel_cpu/src/infer_request.cpp`
- `src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp`
- `src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp`

Create:

- `src/plugins/intel_cpu/tests/unit/state_allocations_writer_test.cpp`

The roadmap's prose file list omitted the two
`state_allocations_dump.*` modifications, but its Task 03 interface, writer,
and exact commit command require them. They are therefore mandatory and no
CMake file is changed: the plugin and unit targets glob these source trees,
and the fresh configure below proves discovery.

## Pinned source facts used by this plan

- `CompiledModelHolder::id()` is a decrementing/reusable per-model request
  count. It is forbidden as observer identity.
- `SyncInferRequest::infer()` currently spans the graph path containing
  `graph.Infer(this)` followed later by `graph.PullOutputData(m_outputs)`.
  The reset hook belongs before the graph lock and first cancellation check;
  the success hook belongs immediately after `PullOutputData`.
- `SyncInferRequest::query_state()` currently delegates at the start and
  constructs public leaf wrappers in its final return. The writer hook belongs
  after the delegating return and before that leaf return.
- `Config::debugCaps` exists only under `CPU_DEBUG_CAPS`, and
  `DebugCapsConfig` reads environment state in its constructor.
- The unit target does not provide a duplicate-key-rejecting JSON dependency.
  Task 03 therefore adds one bounded private syntax validator rather than
  silently relying on a permissive or unavailable parser.
- The private model marker is nested `ov::RTMap` data:
  `openvino_genai` -> `cpu_state_allocation_observer` -> exact keys `schema`
  and `correlation_id`. `schema` must be exactly `uint32_t{1}`; signed,
  wider, string, Boolean, or other numerically equivalent representations are
  malformed. The CPU child copies only schema/correlation.

## Step 1: Freeze the accepted Task 02 boundary and exact Task 03 preimages

- [ ] Read the complete roadmap, complete Task 02 plan, Task 02 receipt, and
      both independent Task 02 verdicts.
- [ ] Resolve the shared repository and canonical derived-core identity.
- [ ] Require exact Task 02 commit/tree/subject/path set and clean status.
- [ ] Verify `O:` already maps to that exact physical derived core without
      changing any substitution.
- [ ] Freeze every Task 03 preimage blob and all committed guard blobs.

Run these direct read-only PowerShell/Git checks from the parent recovery
worktree:

```powershell
$ErrorActionPreference = "Stop"
$task03CurrentUserPolicyBefore =
  Get-ExecutionPolicy -Scope CurrentUser
$task03LocalMachinePolicyBefore =
  Get-ExecutionPolicy -Scope LocalMachine
function Set-AndAssert-Task03ProcessBypass {
  Set-ExecutionPolicy -Scope Process Bypass -Force
  if ((Get-ExecutionPolicy -Scope Process) -ne "Bypass" -or
      (Get-ExecutionPolicy -Scope CurrentUser) -ne
        $task03CurrentUserPolicyBefore -or
      (Get-ExecutionPolicy -Scope LocalMachine) -ne
        $task03LocalMachinePolicyBefore) {
    throw "Task 03 controller changed a persistent execution-policy scope"
  }
}
function Assert-Task03ControllerPolicy {
  if ((Get-ExecutionPolicy -Scope Process) -ne "Bypass" -or
      (Get-ExecutionPolicy -Scope CurrentUser) -ne
        $task03CurrentUserPolicyBefore -or
      (Get-ExecutionPolicy -Scope LocalMachine) -ne
        $task03LocalMachinePolicyBefore) {
    throw "Task 03 controller execution-policy invariant changed"
  }
}
Set-AndAssert-Task03ProcessBypass

$roadmap = "docs/superpowers/plans/2026-07-28-openvino-cpu-kv-allocation-observability.md"
$task02Plan = "docs/superpowers/plans/2026-07-28-openvino-cpu-kv-allocation-observability-task-02.md"
$expectedRoadmapHash =
  "9497cba857247af42bc7f889c17fc2e248edd6a02cf5703906aec23434504a3e"
$roadmapHash = (
  Get-FileHash -LiteralPath $roadmap -Algorithm SHA256
).Hash.ToLowerInvariant()
if ($roadmapHash -ne $expectedRoadmapHash) {
  throw "Roadmap drifted before Task 03"
}
if (-not (Test-Path -LiteralPath $task02Plan)) {
  throw "Complete Task 02 plan is missing"
}

$commonDirRaw = (& git rev-parse --git-common-dir).Trim()
$commonDir = if ([IO.Path]::IsPathRooted($commonDirRaw)) {
  [IO.Path]::GetFullPath($commonDirRaw)
} else {
  (Resolve-Path -LiteralPath $commonDirRaw).Path
}
$sharedRepoRoot = Split-Path -Parent $commonDir
$sharedTop = (& git -C $sharedRepoRoot rev-parse --show-toplevel).Trim()
if (-not [IO.Path]::GetFullPath($sharedTop).Equals(
    [IO.Path]::GetFullPath($sharedRepoRoot),
    [StringComparison]::OrdinalIgnoreCase)) {
  throw "Shared repository root resolution failed"
}
$controllerRepoRoot = (
  Resolve-Path -LiteralPath (
    (& git rev-parse --show-toplevel).Trim())
).Path
$existingRLines = @(& subst.exe) |
  Where-Object { $_ -match '^R:\\: => ' }
if ($existingRLines.Count -ne 1 -or
    $existingRLines[0] -notmatch
      '^R:\\: => (?<target>.+)$') {
  throw "Task 03 requires exactly one pre-existing R: substitution"
}
$actualRTarget =
  [IO.Path]::GetFullPath($Matches.target).TrimEnd('\')
$expectedRTarget =
  [IO.Path]::GetFullPath($controllerRepoRoot).TrimEnd('\')
if (-not $actualRTarget.Equals(
    $expectedRTarget,
    [StringComparison]::OrdinalIgnoreCase)) {
  throw "R: does not map to the accepted Task 03 controller worktree"
}

$base = "ede283a88e35465f0d680dabbf1f44080f8fc387"
$cleanCore = Join-Path $sharedRepoRoot `
  "external\official-openvino\2026-07-19\openvino"
$identityPath =
  "R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer.identity.json"
$identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
if ($identity -isnot [PSCustomObject] -or
    $identity.destination_path -isnot [string] -or
    $identity.base_commit -isnot [string] -or
    $identity.upstream_commit -isnot [string] -or
    $identity.patch_commit -isnot [string] -or
    $identity.dirty -isnot [bool]) {
  throw "Canonical materialization identity has invalid JSON types"
}
$expectedDerivedCore =
  "C:\ov-wb04\2026-07-19\openvino-cpu-state-observer"
if (-not [IO.Path]::GetFullPath([string]$identity.destination_path).Equals(
    [IO.Path]::GetFullPath($expectedDerivedCore),
    [StringComparison]::OrdinalIgnoreCase)) {
  throw "Canonical identity does not name the accepted physical derived core"
}
if ($identity.base_commit -ne $base -or
    $identity.upstream_commit -ne $base -or
    $identity.patch_commit -ne $base -or
    $identity.dirty -ne $false) {
  throw "Canonical Task 01 materialization identity has drifted"
}
$derivedCore = (Resolve-Path -LiteralPath $identity.destination_path).Path
if (-not [IO.Path]::GetFullPath($derivedCore).Equals(
    [IO.Path]::GetFullPath($expectedDerivedCore),
    [StringComparison]::OrdinalIgnoreCase)) {
  throw "Resolved derived core is not the accepted physical destination"
}

$task02ReceiptPath = "R:\.superpowers\sdd\task02-receipt.json"
if (-not (Test-Path -LiteralPath $task02ReceiptPath)) {
  throw "Accepted Task 02 receipt is missing"
}
$task02Receipt =
  Get-Content -LiteralPath $task02ReceiptPath -Raw | ConvertFrom-Json
if ($task02Receipt -isnot [PSCustomObject] -or
    $task02Receipt.schema -isnot [string] -or
    $task02Receipt.roadmap_sha256 -isnot [string] -or
    $task02Receipt.clean_core_commit -isnot [string] -or
    $task02Receipt.clean_core_tree -isnot [string] -or
    $task02Receipt.task2_commit -isnot [string] -or
    $task02Receipt.task2_tree -isnot [string] -or
    $task02Receipt.task2_commit_subject -isnot [string] -or
    $task02Receipt.changed_paths -isnot [System.Array] -or
    $task02Receipt.guard_blobs -isnot [PSCustomObject] -or
    $task02Receipt.schema -ne
      "openvino-cpu-observer-task02-receipt/v1" -or
    $task02Receipt.roadmap_sha256 -ne $expectedRoadmapHash -or
    $task02Receipt.clean_core_commit -ne $base -or
    $task02Receipt.task2_commit_subject -ne
      "feat(cpu): capture physical variable-state allocations") {
  throw "Task 02 receipt identity is invalid"
}
$task02Head = [string]$task02Receipt.task2_commit
$task02Tree = [string]$task02Receipt.task2_tree
if ($task02Head -notmatch '^[0-9a-f]{40}$' -or
    $task02Tree -notmatch '^[0-9a-f]{40}$') {
  throw "Task 02 receipt does not contain full commit/tree identities"
}
$task02Subject =
  (& git -C $derivedCore show -s --format=%s $task02Head).Trim()
$task02Parent =
  (& git -C $derivedCore rev-parse "$task02Head^").Trim()
if ($LASTEXITCODE -ne 0 -or
    $task02Subject -ne
      "feat(cpu): capture physical variable-state allocations" -or
    $task02Parent -ne $base -or
    (& git -C $derivedCore rev-parse "$task02Head^{tree}").Trim() -ne
      $task02Tree) {
  throw "Task 02 subject, immediate parent, or tree is not exact"
}
$expectedTask02Paths = @(
  "src/plugins/intel_cpu/src/cpu_memory.cpp",
  "src/plugins/intel_cpu/src/cpu_memory.h",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"
) | Sort-Object
if (@(
    $task02Receipt.changed_paths |
      Where-Object { $_ -isnot [string] }
  ).Count -ne 0) {
  throw "Task 02 receipt changed_paths contains a non-string"
}
$receiptTask02Paths = @($task02Receipt.changed_paths) | Sort-Object
if (Compare-Object $expectedTask02Paths $receiptTask02Paths) {
  throw "Task 02 receipt path set is not exact"
}
$actualTask02Paths = @(
  & git -C $derivedCore diff --name-only $base $task02Head
) | Where-Object { $_ } | Sort-Object
if (Compare-Object $expectedTask02Paths $actualTask02Paths) {
  throw "Recomputed Task 02 Git diff path set is not exact"
}

if ((& git -C $cleanCore rev-parse HEAD).Trim() -ne $base -or
    (& git -C $cleanCore rev-parse "HEAD^{tree}").Trim() -ne
      $task02Receipt.clean_core_tree -or
    (& git -C $cleanCore status --porcelain --untracked-files=all)) {
  throw "Immutable clean upstream commit/tree/status is not exact"
}
if ((& git -C $derivedCore branch --show-current).Trim() -ne
    "project/cpu-state-allocation-observer") {
  throw "Derived core is on the wrong branch"
}
if ((& git -C $derivedCore rev-parse HEAD).Trim() -ne $task02Head -or
    (& git -C $derivedCore rev-parse "HEAD^{tree}").Trim() -ne $task02Tree) {
  throw "Derived core is not exactly the accepted Task 02 commit/tree"
}
if (& git -C $derivedCore status --porcelain --untracked-files=all) {
  throw "Derived core must be clean before Task 03"
}
& git -C $derivedCore merge-base --is-ancestor $base $task02Head
if ($LASTEXITCODE -ne 0) {
  throw "Accepted Task 02 commit is not based on the immutable pin"
}

$progressPath = "R:\.superpowers\sdd\progress.md"
$progressText = if (Test-Path -LiteralPath $progressPath) {
  Get-Content -LiteralPath $progressPath -Raw
} else {
  ""
}
$escapedTask02Head = [regex]::Escape($task02Head)
if ($progressText -notmatch "(?im)Task\s*0?2.*SPEC PASS.*$escapedTask02Head" -or
    $progressText -notmatch "(?im)Task\s*0?2.*QUALITY PASS.*$escapedTask02Head") {
  throw "Task 02 does not have recorded independent SPEC and QUALITY PASS for its exact commit"
}

$expectedOTarget = [IO.Path]::GetFullPath($derivedCore).TrimEnd('\')
$existingOLines = @(& subst.exe) | Where-Object { $_ -match '^O:\\: => ' }
if ($existingOLines.Count -ne 1 -or
    $existingOLines[0] -notmatch '^O:\\: => (?<target>.+)$') {
  throw "Task 03 requires exactly one pre-existing O: substitution"
}
$actualOTarget = [IO.Path]::GetFullPath($Matches.target).TrimEnd('\')
if (-not $actualOTarget.Equals(
    $expectedOTarget, [StringComparison]::OrdinalIgnoreCase)) {
  throw "O: does not map to the accepted physical derived core"
}
if ((& git -C O:\ rev-parse HEAD).Trim() -ne $task02Head) {
  throw "O: does not expose the accepted Task 02 commit"
}

$task03Paths = @(
  "src/plugins/intel_cpu/src/compiled_model.h",
  "src/plugins/intel_cpu/src/compiled_model.cpp",
  "src/plugins/intel_cpu/src/utils/debug_caps_config.h",
  "src/plugins/intel_cpu/src/utils/debug_caps_config.cpp",
  "src/plugins/intel_cpu/src/infer_request.h",
  "src/plugins/intel_cpu/src/infer_request.cpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_writer_test.cpp"
)
function Get-ExactBlobOrAbsent {
  param(
    [Parameter(Mandatory=$true)][string]$Repository,
    [Parameter(Mandatory=$true)][string]$Commit,
    [Parameter(Mandatory=$true)][string]$Path
  )
  $object = "$Commit`:$Path"
  $previousErrorActionPreference = $ErrorActionPreference
  try {
    # Windows PowerShell 5.1 surfaces native stderr as error records.
    # The exact native exit/diagnostic pair is authoritative here.
    $ErrorActionPreference = "Continue"
    $diagnostic = @(
      & git -C $Repository cat-file -e $object 2>&1 |
        ForEach-Object { [string]$_ }
    )
    $catExit = $LASTEXITCODE
  } finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }
  if ($catExit -eq 0) {
    if ($diagnostic.Count -ne 0) {
      throw "cat-file emitted unexpected diagnostics for $Path"
    }
    $type = (& git -C $Repository cat-file -t $object).Trim()
    $blob = (& git -C $Repository rev-parse $object).Trim()
    if ($LASTEXITCODE -ne 0 -or
        $type -ne "blob" -or
        $blob -notmatch '^[0-9a-f]{40}$') {
      throw "Existing preimage is not one exact blob: $Path"
    }
    return $blob
  }
  $expectedMissing =
    "fatal: path '$Path' does not exist in '$Commit'"
  if ($catExit -eq 128 -and
      $diagnostic.Count -eq 1 -and
      [StringComparer]::Ordinal.Equals(
        $diagnostic[0],
        $expectedMissing)) {
    return "ABSENT"
  }
  throw (
    "Operational cat-file failure for $Path, exit=$catExit, " +
    "diagnostic=$($diagnostic -join ' | ')")
}

$task03Preimages = [ordered]@{}
foreach ($path in $task03Paths) {
  $task03Preimages[$path] = Get-ExactBlobOrAbsent `
    -Repository $derivedCore `
    -Commit $task02Head `
    -Path $path
}
$basePinned = [ordered]@{
  "src/plugins/intel_cpu/src/compiled_model.h" =
    "81d8ce96b1d2307ad90379666d3ec851035d6eba"
  "src/plugins/intel_cpu/src/compiled_model.cpp" =
    "7aca18dcc5293c6c87ac416476e6447f26d7977d"
  "src/plugins/intel_cpu/src/utils/debug_caps_config.h" =
    "d50047b9075bfb0d32e80e6f87d4fc83a73dce82"
  "src/plugins/intel_cpu/src/utils/debug_caps_config.cpp" =
    "93dddd09301eabed10848f97a0cc3e435d611363"
  "src/plugins/intel_cpu/src/infer_request.h" =
    "5da6bf302922f21238a8cc1a33d76e8266ee64a0"
  "src/plugins/intel_cpu/src/infer_request.cpp" =
    "bbef187969be0dd556890b2f726275a3f7a8b25f"
}
foreach ($entry in $basePinned.GetEnumerator()) {
  if ($task03Preimages[$entry.Key] -ne $entry.Value) {
    throw "Task 02 unexpectedly changed a Task 03-only source: $($entry.Key)"
  }
}
if ($task03Preimages[
      "src/plugins/intel_cpu/tests/unit/state_allocations_writer_test.cpp"
    ] -ne "ABSENT") {
  throw "Task 03 writer test pre-exists"
}
foreach ($task02Output in @(
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp"
)) {
  if ($task03Preimages[$task02Output] -eq "ABSENT") {
    throw "Accepted Task 02 output is missing: $task02Output"
  }
}

$guardPaths = @(
  "scripts/testing/official_openvino/owned_process_guard.py",
  "scripts/testing/official_openvino/guarded_build.py",
  "scripts/testing/invoke_guarded_command.ps1"
)
$receiptGuardPaths = @(
  $task02Receipt.guard_blobs.PSObject.Properties.Name
) | Sort-Object
if (Compare-Object `
    (@($guardPaths) | Sort-Object) `
    $receiptGuardPaths) {
  throw "Task 02 accepted guard-blob property set is not exact"
}
$guardBlobs = [ordered]@{}
foreach ($path in $guardPaths) {
  $diagnostic = @(
    & git cat-file -e "HEAD:$path" 2>&1 |
      ForEach-Object { [string]$_ }
  )
  $catExit = $LASTEXITCODE
  if ($catExit -ne 0 -or $diagnostic.Count -ne 0) {
    throw (
      "Committed guard prerequisite lookup failed for $path, " +
      "exit=$catExit, diagnostic=$($diagnostic -join ' | ')")
  }
  $guardBlobs[$path] = (& git rev-parse "HEAD:$path").Trim()
  $worktreeBlob =
    (& git hash-object "--path=$path" -- $path).Trim()
  if ($LASTEXITCODE -ne 0 -or
      $guardBlobs[$path] -notmatch '^[0-9a-f]{40}$' -or
      $worktreeBlob -ne $guardBlobs[$path]) {
    throw "Guard worktree bytes differ from committed reviewed blob: $path"
  }
  $receiptGuardValue =
    $task02Receipt.guard_blobs.PSObject.Properties[
      $path
    ].Value
  if ($receiptGuardValue -isnot [string]) {
    throw "Task 02 guard blob is not an exact JSON string: $path"
  }
  $receiptGuardBlob = [string]$receiptGuardValue
  if ($receiptGuardBlob -ne $guardBlobs[$path]) {
    throw "Task 02 did not use the accepted committed guard blob: $path"
  }
}
& git diff --quiet HEAD -- @guardPaths
if ($LASTEXITCODE -ne 0) {
  throw "Guard worktree differs from committed reviewed files"
}
& git diff --cached --quiet HEAD -- @guardPaths
if ($LASTEXITCODE -ne 0) {
  throw "Guard index differs from committed reviewed files"
}
$guardStatus = @(
  & git status --porcelain --untracked-files=all -- @guardPaths
)
if ($guardStatus.Count -ne 0) {
  throw "Guard paths are not clean at Task 03 start"
}

$boundary = [ordered]@{
  schema = "openvino-cpu-observer-task03-boundary/v1"
  roadmap_sha256 = $roadmapHash
  controller_repo_root = $controllerRepoRoot
  clean_core_commit = $base
  clean_core_tree = (& git -C $cleanCore rev-parse "$base^{tree}").Trim()
  canonical_identity_sha256 = (
    Get-FileHash -LiteralPath $identityPath -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  task02_receipt_sha256 = (
    Get-FileHash -LiteralPath $task02ReceiptPath -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  task02_plan_sha256 = (
    Get-FileHash -LiteralPath $task02Plan -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  task02_commit = $task02Head
  task02_tree = $task02Tree
  task02_parent = $task02Parent
  task02_subject = $task02Subject
  task03_preimage_blobs = $task03Preimages
  guard_blobs = $guardBlobs
}
$boundaryPath =
  "R:\.superpowers\sdd\task03-boundary.json"
$boundaryJson = $boundary | ConvertTo-Json -Depth 8
Set-Content -LiteralPath $boundaryPath `
  -Value $boundaryJson -Encoding UTF8 -NoNewline
$persistedBoundaryRaw =
  Get-Content -LiteralPath $boundaryPath -Raw
if (-not [StringComparer]::Ordinal.Equals(
    $persistedBoundaryRaw,
    $boundaryJson)) {
  throw "Task 03 boundary did not persist exactly"
}
```

The only write above is ignored scratch evidence, and its exact serialized
field set is re-read immediately. The canonical identity intentionally
remains the Task 01 materialization receipt; it is not rewritten to pretend it
tracks private development commits.

## Step 2: Write the complete 21-test RED file

- [ ] Create only the new writer test source.
- [ ] Preserve the exact test bodies below.
- [ ] Do not add production declarations yet.

Create
`O:\src\plugins\intel_cpu\tests\unit\state_allocations_writer_test.cpp` with
this exact body:

```cpp
// Copyright (C) 2018-2026 Intel Corporation
// SPDX-License-Identifier: Apache-2.0
//

#include <gtest/gtest.h>

#ifdef CPU_DEBUG_CAPS

#include <atomic>
#include <cstdint>
#include <exception>
#include <filesystem>
#include <fstream>
#include <functional>
#include <iterator>
#include <limits>
#include <map>
#include <memory>
#include <mutex>
#include <optional>
#include <ostream>
#include <streambuf>
#include <string>
#include <string_view>
#include <thread>
#include <utility>
#include <vector>

#include "openvino/core/any.hpp"
#include "openvino/core/except.hpp"
#include "openvino/runtime/exception.hpp"
#include "utils/debug_caps_config.h"
#include "utils/state_allocations_dump.hpp"

namespace ov::intel_cpu {
namespace {

using Environment = std::map<std::string, std::string>;

class WriteFailingBuffer final : public std::streambuf {
protected:
    std::streamsize xsputn(
        const char*,
        std::streamsize) override {
        return 0;
    }

    int_type overflow(int_type) override {
        return traits_type::eof();
    }
};

class FlushFailingBuffer final : public std::streambuf {
protected:
    std::streamsize xsputn(
        const char*,
        std::streamsize count) override {
        return count;
    }

    int_type overflow(int_type value) override {
        return traits_type::not_eof(value);
    }

    int sync() override {
        return -1;
    }
};

class StateAllocationsWriter : public testing::Test {
protected:
    void SetUp() override {
        static std::atomic<uint64_t> next_directory{1};
        m_temporary_root =
            std::filesystem::weakly_canonical(
                std::filesystem::temp_directory_path());
        for (;;) {
            m_temporary_directory =
                m_temporary_root /
                ("openvino-cpu-state-observer-" +
                 std::to_string(current_process_id()) + "-" +
                 std::to_string(
                     next_directory.fetch_add(1)));
            std::error_code error;
            if (std::filesystem::create_directory(
                    m_temporary_directory,
                    error)) {
                m_owns_temporary_directory = true;
                break;
            }
            ASSERT_FALSE(error);
        }
    }

    void TearDown() override {
        if (!m_owns_temporary_directory) {
            return;
        }
        std::error_code status_error;
        const auto status = std::filesystem::symlink_status(
            m_temporary_directory,
            status_error);
        ASSERT_FALSE(status_error);
        ASSERT_TRUE(std::filesystem::is_directory(status));
        ASSERT_FALSE(std::filesystem::is_symlink(status));
        const auto normalized_directory =
            std::filesystem::weakly_canonical(
                m_temporary_directory);
        ASSERT_EQ(
            normalized_directory.parent_path(),
            m_temporary_root);
        ASSERT_NE(normalized_directory, m_temporary_root);
        ASSERT_EQ(
            normalized_directory.filename().string().find(
                "openvino-cpu-state-observer-"),
            0u);
        std::error_code error;
        std::filesystem::remove_all(
            normalized_directory,
            error);
        ASSERT_FALSE(error);
    }

    [[nodiscard]] const std::filesystem::path& temporary_directory()
        const noexcept {
        return m_temporary_directory;
    }

    StateAllocationDumpConfig parse(
        const Environment& environment) const {
        return
            parse_state_allocation_dump_config(
            [&](std::string_view name)
                -> std::optional<std::string> {
                const auto found =
                    environment.find(std::string{name});
                return found == environment.end()
                           ? std::nullopt
                           : std::optional<std::string>{
                                 found->second};
            });
    }

    StateAllocationDumpConfig parse_with_path_kind(
        const Environment& environment,
        StateAllocationPathKind kind) const {
        return
            parse_state_allocation_dump_config_with_inspector_for_test(
            [&](std::string_view name)
                -> std::optional<std::string> {
                const auto found =
                    environment.find(std::string{name});
                return found == environment.end()
                           ? std::nullopt
                           : std::optional<std::string>{
                                 found->second};
            },
            [kind](const std::filesystem::path&) {
                return kind;
            });
    }

    static ov::RTMap valid_model_context() {
        ov::AnyMap observer;
        observer.emplace("schema", uint32_t{1});
        observer.emplace(
            "correlation_id",
            std::string{
                "0123456789abcdef0123456789abcdef"});
        ov::AnyMap genai;
        genai.emplace(
            "cpu_state_allocation_observer",
            std::move(observer));
        ov::RTMap root;
        root.emplace("openvino_genai", std::move(genai));
        return root;
    }

    static SnapshotContext snapshot_context(
        uint64_t observer_request_id,
        std::string phase = "fresh") {
        return SnapshotContext{
            4242,
            observer_request_id,
            "0123456789abcdef0123456789abcdef",
            "query_state",
            std::move(phase),
            "CPU",
        };
    }

    static std::string serialized_fixture(
        uint32_t pid,
        uint64_t sequence) {
        ObservedAllocation allocation{
            "state_0",
            "VariableStateSingleBuffer",
            StateMemoryRole::INPUT,
            AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
            1,
            "f16",
            {1},
            {0},
            {1},
            64,
            64,
            true,
            false,
        };
        auto context = snapshot_context(9000 + sequence);
        context.pid = pid;
        return serialize_state_allocation_snapshot(
            build_state_allocation_snapshot(
                {std::move(allocation)},
                sequence,
                context));
    }

    static void append_serialized_snapshot(
        const StateAllocationDumpConfig& config,
        const std::string& serialized) {
        append_serialized_state_allocation_snapshot(
            config,
            serialized);
    }

    static std::string read_bytes(
        const std::filesystem::path& path) {
        std::ifstream input(path, std::ios::binary);
        if (!input) {
            OPENVINO_THROW("unable to read test file");
        }
        return {
            std::istreambuf_iterator<char>{input},
            std::istreambuf_iterator<char>{}};
    }

    static std::vector<std::string> read_nonempty_lines(
        const std::filesystem::path& path) {
        std::ifstream input(path, std::ios::binary);
        if (!input) {
            OPENVINO_THROW("unable to open JSONL test output");
        }
        input.seekg(0, std::ios::end);
        if (input.tellg() <= std::streampos{0}) {
            OPENVINO_THROW("JSONL test output is empty");
        }
        input.seekg(-1, std::ios::end);
        char terminal = '\0';
        input.get(terminal);
        if (terminal != '\n') {
            OPENVINO_THROW(
                "JSONL test output lacks a terminal newline");
        }
        input.clear();
        input.seekg(0, std::ios::beg);
        std::vector<std::string> lines;
        std::string line;
        while (std::getline(input, line)) {
            if (line.empty() || line.back() == '\r') {
                OPENVINO_THROW(
                    "JSONL test output contains a blank or CR line");
            }
            lines.push_back(std::move(line));
        }
        if (input.bad()) {
            OPENVINO_THROW("unable to read complete JSONL test output");
        }
        return lines;
    }

    static void parse_json_object_with_unique_keys(
        const std::string& line) {
        validate_json_object_with_unique_keys(line);
    }

    static uint64_t extract_sequence(const std::string& line) {
        constexpr std::string_view marker = "\"sequence\":";
        const auto start = line.find(marker);
        if (start == std::string::npos) {
            OPENVINO_THROW("sequence key is missing");
        }
        size_t cursor = start + marker.size();
        if (cursor == line.size() ||
            line[cursor] < '0' || line[cursor] > '9') {
            OPENVINO_THROW("sequence value is not unsigned");
        }
        uint64_t value = 0;
        while (cursor < line.size() &&
               line[cursor] >= '0' && line[cursor] <= '9') {
            const auto digit =
                static_cast<uint64_t>(line[cursor] - '0');
            if (value >
                (std::numeric_limits<uint64_t>::max() - digit) /
                    10) {
                OPENVINO_THROW("sequence extraction overflow");
            }
            value = value * 10 + digit;
            ++cursor;
        }
        return value;
    }

    std::filesystem::path m_temporary_root;
    std::filesystem::path m_temporary_directory;
    bool m_owns_temporary_directory = false;
};

TEST_F(StateAllocationsWriter, AbsentEnvironmentDisablesWriter) {
    EXPECT_FALSE(parse({}).enabled());
}

TEST_F(StateAllocationsWriter, EmptyEnvironmentValueDisablesWriter) {
    EXPECT_FALSE(
        parse({{"OV_CPU_STATE_ALLOCATION_DUMP_PATH", ""}})
            .enabled());
}

TEST_F(StateAllocationsWriter, ReadsOnlyExactOptInEnvironment) {
    std::vector<std::string> names;
    const auto config = parse_state_allocation_dump_config(
        [&](std::string_view name)
            -> std::optional<std::string> {
            names.emplace_back(name);
            return std::nullopt;
        });
    EXPECT_FALSE(config.enabled());
    ASSERT_EQ(names.size(), 1u);
    EXPECT_EQ(
        names.front(),
        "OV_CPU_STATE_ALLOCATION_DUMP_PATH");
}

TEST_F(
    StateAllocationsWriter,
    RejectsDirectoryNonJsonlRootUncMappedRemoteAndReparsePaths) {
    const auto jsonl_directory =
        temporary_directory() / "directory.jsonl";
    ASSERT_TRUE(
        std::filesystem::create_directory(jsonl_directory));
    for (const auto& path :
         {jsonl_directory.string(),
          (temporary_directory() / "snapshot.json").string(),
          (temporary_directory() / "snapshot.JSONL").string(),
          (temporary_directory().root_path() /
           "root-output.jsonl")
              .string()}) {
        EXPECT_THROW(
            parse({{"OV_CPU_STATE_ALLOCATION_DUMP_PATH", path}}),
            ov::Exception);
    }

    try {
        static_cast<void>(parse({
            {"OV_CPU_STATE_ALLOCATION_DUMP_PATH",
             R"(\\server\share\snapshot.jsonl)"},
        }));
        FAIL() << "network path was accepted";
    } catch (const ov::Exception& error) {
        EXPECT_NE(
            std::string{error.what()}.find(
                "state allocation dump path must be local"),
            std::string::npos);
    }

    const Environment local_candidate{
        {"OV_CPU_STATE_ALLOCATION_DUMP_PATH",
         (temporary_directory() / "snapshot.jsonl").string()},
    };
    EXPECT_THROW(
        parse_with_path_kind(
            local_candidate,
            StateAllocationPathKind::REMOTE),
        ov::Exception);
    EXPECT_THROW(
        parse_with_path_kind(
            local_candidate,
            StateAllocationPathKind::REPARSE),
        ov::Exception);
    EXPECT_THROW(
        parse_with_path_kind(
            local_candidate,
            StateAllocationPathKind::UNSUPPORTED),
        ov::Exception);
}

TEST_F(StateAllocationsWriter, RejectsMissingOutputParent) {
    EXPECT_THROW(
        parse({
            {"OV_CPU_STATE_ALLOCATION_DUMP_PATH",
             (temporary_directory() / "missing" /
              "snapshot.jsonl")
                 .string()},
        }),
        ov::Exception);

    const auto removed_parent =
        temporary_directory() / "removed-before-open";
    ASSERT_TRUE(
        std::filesystem::create_directory(removed_parent));
    const auto config = parse({
        {"OV_CPU_STATE_ALLOCATION_DUMP_PATH",
         (removed_parent / "snapshot.jsonl").string()},
    });
    ASSERT_EQ(std::filesystem::remove_all(removed_parent), 1u);
    bool factory_called = false;
    EXPECT_THROW(
        append_serialized_state_allocation_snapshot_with_factory(
            config,
            serialized_fixture(4242, 1),
            [&](const std::filesystem::path&) {
                factory_called = true;
                return std::make_unique<std::ostream>(nullptr);
            }),
        ov::Exception);
    EXPECT_FALSE(factory_called);
}

TEST_F(StateAllocationsWriter, ConcurrentWritesRemainWholeJsonLines) {
    const auto path = temporary_directory() / "snapshot.jsonl";
    const auto config = parse({
        {"OV_CPU_STATE_ALLOCATION_DUMP_PATH", path.string()},
    });
    std::vector<std::thread> writers;
    std::vector<std::exception_ptr> failures(8);
    for (size_t index = 0; index < failures.size(); ++index) {
        writers.emplace_back([&, index] {
            try {
                append_serialized_snapshot(
                    config,
                    serialized_fixture(4242, index + 1));
            } catch (...) {
                failures[index] = std::current_exception();
            }
        });
    }
    for (auto& writer : writers) {
        writer.join();
    }
    for (const auto& failure : failures) {
        EXPECT_FALSE(static_cast<bool>(failure));
    }
    {
        const auto lines = read_nonempty_lines(path);
        ASSERT_EQ(lines.size(), 8u);
        for (const auto& line : lines) {
            EXPECT_NO_THROW(
                parse_json_object_with_unique_keys(line));
        }
    }

    append_serialized_snapshot(
        config,
        serialized_fixture(4242, 9));
    ASSERT_EQ(read_nonempty_lines(path).size(), 9u);

    const auto empty_path =
        temporary_directory() / "empty.jsonl";
    {
        std::ofstream empty{
            empty_path,
            std::ios::binary | std::ios::trunc};
        ASSERT_TRUE(empty);
    }
    append_serialized_snapshot(
        parse({
            {"OV_CPU_STATE_ALLOCATION_DUMP_PATH",
             empty_path.string()},
        }),
        serialized_fixture(4242, 10));
    ASSERT_EQ(read_nonempty_lines(empty_path).size(), 1u);

    const auto unterminated_path =
        temporary_directory() / "unterminated.jsonl";
    const auto unterminated =
        serialized_fixture(4242, 11);
    {
        std::ofstream output{
            unterminated_path,
            std::ios::binary | std::ios::trunc};
        ASSERT_TRUE(output);
        output.write(
            unterminated.data(),
            static_cast<std::streamsize>(
                unterminated.size()));
        output.flush();
        ASSERT_TRUE(output);
    }
    const auto unterminated_config = parse({
        {"OV_CPU_STATE_ALLOCATION_DUMP_PATH",
         unterminated_path.string()},
    });
    EXPECT_THROW(
        append_serialized_snapshot(
            unterminated_config,
            serialized_fixture(4242, 12)),
        ov::Exception);
    EXPECT_EQ(read_bytes(unterminated_path), unterminated);

    const auto malformed_path =
        temporary_directory() / "malformed.jsonl";
    const std::string malformed = "{\"broken\":}\n";
    {
        std::ofstream output{
            malformed_path,
            std::ios::binary | std::ios::trunc};
        ASSERT_TRUE(output);
        output.write(
            malformed.data(),
            static_cast<std::streamsize>(
                malformed.size()));
        output.flush();
        ASSERT_TRUE(output);
    }
    const auto malformed_config = parse({
        {"OV_CPU_STATE_ALLOCATION_DUMP_PATH",
         malformed_path.string()},
    });
    EXPECT_THROW(
        append_serialized_snapshot(
            malformed_config,
            serialized_fixture(4242, 13)),
        ov::Exception);
    EXPECT_EQ(read_bytes(malformed_path), malformed);
}

TEST_F(
    StateAllocationsWriter,
    RejectsMalformedOrDuplicateJsonBeforeWriting) {
    const auto path = temporary_directory() / "snapshot.jsonl";
    const auto config = parse({
        {"OV_CPU_STATE_ALLOCATION_DUMP_PATH", path.string()},
    });
    EXPECT_THROW(
        append_serialized_snapshot(
            config,
            "{\"schema_version\":1,\"schema_version\":1}"),
        ov::Exception);
    EXPECT_THROW(
        append_serialized_snapshot(
            config,
            "{\"schema_version\":1,"
            "\"\\u0073chema_version\":1}"),
        ov::Exception);
    EXPECT_THROW(
        append_serialized_snapshot(
            config,
            "{\"outer\":{\"key\":1,\"key\":2}}"),
        ov::Exception);
    EXPECT_THROW(
        append_serialized_snapshot(config, "{\"schema_version\":"),
        ov::Exception);
    EXPECT_THROW(
        append_serialized_snapshot(
            config,
            "{\"value\":\"\\n\"}"),
        ov::Exception);
    EXPECT_THROW(
        append_serialized_snapshot(
            config,
            "{\"value\":\"\\uD800\"}"),
        ov::Exception);
    EXPECT_NO_THROW(
        parse_json_object_with_unique_keys(
            "{\"value\":\"\\uD83D\\uDE00\"}"));

    std::string deeply_nested = "{\"value\":";
    deeply_nested.append(66, '[');
    deeply_nested.push_back('0');
    deeply_nested.append(66, ']');
    deeply_nested.push_back('}');
    EXPECT_THROW(
        append_serialized_snapshot(config, deeply_nested),
        ov::Exception);

    std::string oversized = "{\"value\":\"";
    oversized.append(
        kMaxStateAllocationSnapshotBytes,
        'a');
    oversized.append("\"}");
    ASSERT_GT(
        oversized.size(),
        kMaxStateAllocationSnapshotBytes);
    EXPECT_THROW(
        append_serialized_snapshot(config, oversized),
        ov::Exception);

    const auto valid = serialized_fixture(4242, 20);
    EXPECT_THROW(
        append_serialized_state_allocation_snapshot_with_factory(
            config,
            valid,
            [](const std::filesystem::path&) {
                return std::make_unique<std::ostream>(nullptr);
            }),
        ov::Exception);

    WriteFailingBuffer write_failing_buffer;
    EXPECT_THROW(
        append_serialized_state_allocation_snapshot_with_factory(
            config,
            valid,
            [&](const std::filesystem::path&) {
                return std::make_unique<std::ostream>(
                    &write_failing_buffer);
            }),
        ov::Exception);

    FlushFailingBuffer flush_failing_buffer;
    EXPECT_THROW(
        append_serialized_state_allocation_snapshot_with_factory(
            config,
            valid,
            [&](const std::filesystem::path&) {
                return std::make_unique<std::ostream>(
                    &flush_failing_buffer);
            }),
        ov::Exception);
    EXPECT_FALSE(std::filesystem::exists(path));
}

TEST_F(StateAllocationsWriter, RecordsNoTensorContentsOrRawAddresses) {
    const auto serialized = serialized_fixture(4242, 1);
    EXPECT_EQ(
        serialized.find("tensor_contents"),
        std::string::npos);
    EXPECT_EQ(serialized.find("prompt"), std::string::npos);
    EXPECT_EQ(serialized.find("response"), std::string::npos);
    EXPECT_EQ(
        serialized.find("raw_address"),
        std::string::npos);
    EXPECT_EQ(serialized.find("0x"), std::string::npos);
}

TEST_F(StateAllocationsWriter, ParsesValidPrivateModelContext) {
    const auto context =
        parse_state_allocation_observer_context(
            valid_model_context());
    EXPECT_EQ(context.schema_version, 1u);
    EXPECT_EQ(
        context.correlation_id,
        "0123456789abcdef0123456789abcdef");
}

TEST_F(StateAllocationsWriter, RejectsMalformedPrivateModelContext) {
    auto wrong_schema = valid_model_context();
    wrong_schema.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .at("schema") = uint32_t{2};
    EXPECT_THROW(
        parse_state_allocation_observer_context(wrong_schema),
        ov::Exception);

    auto uppercase = valid_model_context();
    uppercase.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .at("correlation_id") =
            std::string{
                "0123456789ABCDEF0123456789ABCDEF"};
    EXPECT_THROW(
        parse_state_allocation_observer_context(uppercase),
        ov::Exception);

    auto short_correlation = valid_model_context();
    short_correlation.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .at("correlation_id") = std::string{"0123"};
    EXPECT_THROW(
        parse_state_allocation_observer_context(
            short_correlation),
        ov::Exception);

    auto string_schema = valid_model_context();
    string_schema.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .at("schema") = std::string{"1"};
    EXPECT_THROW(
        parse_state_allocation_observer_context(string_schema),
        ov::Exception);

    auto int64_schema = valid_model_context();
    int64_schema.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .at("schema") = int64_t{1};
    EXPECT_THROW(
        parse_state_allocation_observer_context(int64_schema),
        ov::Exception);

    auto boolean_schema = valid_model_context();
    boolean_schema.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .at("schema") = true;
    EXPECT_THROW(
        parse_state_allocation_observer_context(boolean_schema),
        ov::Exception);

    auto signed32_schema = valid_model_context();
    signed32_schema.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .at("schema") = int32_t{1};
    EXPECT_THROW(
        parse_state_allocation_observer_context(signed32_schema),
        ov::Exception);

    auto uint64_schema = valid_model_context();
    uint64_schema.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .at("schema") = uint64_t{1};
    EXPECT_THROW(
        parse_state_allocation_observer_context(uint64_schema),
        ov::Exception);

    auto extra_key = valid_model_context();
    extra_key.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .emplace("requested_device", std::string{"CPU"});
    EXPECT_THROW(
        parse_state_allocation_observer_context(extra_key),
        ov::Exception);
}

TEST_F(StateAllocationsWriter, RejectsMissingPrivateModelContext) {
    EXPECT_THROW(
        parse_state_allocation_observer_context({}),
        ov::Exception);

    ov::RTMap missing_observer;
    missing_observer.emplace("openvino_genai", ov::AnyMap{});
    EXPECT_THROW(
        parse_state_allocation_observer_context(missing_observer),
        ov::Exception);

    auto missing_correlation = valid_model_context();
    missing_correlation.at("openvino_genai")
        .as<ov::AnyMap>()
        .at("cpu_state_allocation_observer")
        .as<ov::AnyMap>()
        .erase("correlation_id");
    EXPECT_THROW(
        parse_state_allocation_observer_context(
            missing_correlation),
        ov::Exception);
}

TEST_F(
    StateAllocationsWriter,
    RequestStateIdsIncreaseAcrossLifetimesAndSaturateWithoutMutation) {
    const StateAllocationDumpConfig enabled{
        temporary_directory() / "snapshot.jsonl"};
    const StateAllocationRequestState first_request{
        enabled};
    const auto first =
        first_request.observer_request_id();

    uint64_t second = 0;
    {
        const StateAllocationRequestState second_request{enabled};
        second =
            second_request.observer_request_id();
        EXPECT_GT(second, first);
    }

    const StateAllocationRequestState after_destruction{enabled};
    EXPECT_GT(
        after_destruction.observer_request_id(),
        second);

    std::atomic<uint64_t> near_exhaustion{
        std::numeric_limits<uint64_t>::max() - 1};
    uint64_t taken = 0;
    EXPECT_TRUE(
        try_take_nonwrapping_counter(
            near_exhaustion,
            taken));
    EXPECT_EQ(
        taken,
        std::numeric_limits<uint64_t>::max() - 1);
    EXPECT_EQ(
        near_exhaustion.load(),
        std::numeric_limits<uint64_t>::max());

    taken = 17;
    EXPECT_FALSE(
        try_take_nonwrapping_counter(
            near_exhaustion,
            taken));
    EXPECT_EQ(taken, 17u);
    EXPECT_EQ(
        near_exhaustion.load(),
        std::numeric_limits<uint64_t>::max());

    std::atomic<uint64_t> invalid_zero{0};
    taken = 23;
    EXPECT_FALSE(
        try_take_nonwrapping_counter(
            invalid_zero,
            taken));
    EXPECT_EQ(taken, 23u);
    EXPECT_EQ(invalid_zero.load(), 0u);
}

TEST_F(
    StateAllocationsWriter,
    DisabledObserverDoesNotAllocateRequestId) {
    const auto before = next_observer_request_id();
    const StateAllocationRequestState disabled{parse({})};
    const auto after = next_observer_request_id();
    EXPECT_FALSE(disabled.enabled());
    EXPECT_EQ(disabled.observer_request_id(), 0u);
    EXPECT_EQ(after, before + 1);
}

TEST_F(StateAllocationsWriter, FreshPhaseWhenAllStatesAreReset) {
    const StateAllocationRequestState state{
        StateAllocationDumpConfig{
            temporary_directory() / "snapshot.jsonl"}};
    state.begin_infer_attempt();
    state.complete_infer_attempt();
    EXPECT_EQ(state.phase(true), "fresh");
    EXPECT_EQ(state.phase(false), "seeded_no_infer");
}

TEST_F(StateAllocationsWriter, SeededNoInferPhaseBeforeSuccess) {
    const StateAllocationRequestState state{
        StateAllocationDumpConfig{
            temporary_directory() / "snapshot.jsonl"}};
    EXPECT_EQ(state.phase(false), "seeded_no_infer");
}

TEST_F(StateAllocationsWriter, PostInferPhaseAfterSuccessfulAttempt) {
    const StateAllocationRequestState state{
        StateAllocationDumpConfig{
            temporary_directory() / "snapshot.jsonl"}};
    state.begin_infer_attempt();
    state.complete_infer_attempt();
    EXPECT_EQ(state.phase(false), "post_infer");
}

TEST_F(
    StateAllocationsWriter,
    FailedAttemptClearsPriorPostInferPhase) {
    const StateAllocationRequestState state{
        StateAllocationDumpConfig{
            temporary_directory() / "snapshot.jsonl"}};
    state.begin_infer_attempt();
    state.complete_infer_attempt();
    ASSERT_EQ(state.phase(false), "post_infer");
    const auto fail = [&] {
        StateAllocationInferenceAttempt attempt{state};
        OPENVINO_THROW("synthetic inference failure");
    };
    EXPECT_THROW(fail(), ov::Exception);
    EXPECT_EQ(state.phase(false), "seeded_no_infer");
}

TEST_F(
    StateAllocationsWriter,
    CancelledAttemptClearsPriorPostInferPhase) {
    const StateAllocationRequestState state{
        StateAllocationDumpConfig{
            temporary_directory() / "snapshot.jsonl"}};
    state.begin_infer_attempt();
    state.complete_infer_attempt();
    ASSERT_EQ(state.phase(false), "post_infer");
    const auto cancel = [&] {
        StateAllocationInferenceAttempt attempt{state};
        throw ov::Cancelled{"synthetic cancellation"};
    };
    EXPECT_THROW(cancel(), ov::Cancelled);
    EXPECT_EQ(state.phase(false), "seeded_no_infer");
}

TEST_F(
    StateAllocationsWriter,
    StateResetClearsPriorPostInferPhase) {
    const StateAllocationRequestState state{
        StateAllocationDumpConfig{
            temporary_directory() / "snapshot.jsonl"}};
    state.begin_infer_attempt();
    state.complete_infer_attempt();
    ASSERT_EQ(state.phase(false), "post_infer");
    EXPECT_EQ(state.phase(true), "fresh");
    EXPECT_EQ(state.phase(false), "seeded_no_infer");
}

TEST_F(
    StateAllocationsWriter,
    EmissionDecisionRejectsDelegatingAndDisabledRequests) {
    const StateAllocationRequestState enabled{
        StateAllocationDumpConfig{
            temporary_directory() / "snapshot.jsonl"}};
    EXPECT_FALSE(enabled.should_emit(true));
    EXPECT_TRUE(enabled.should_emit(false));

    const StateAllocationRequestState disabled{
        StateAllocationDumpConfig{}};
    EXPECT_FALSE(disabled.should_emit(false));
    EXPECT_FALSE(disabled.should_emit(true));
}

TEST_F(
    StateAllocationsWriter,
    ProcessWideSequenceRemainsMonotonicAcrossThreads) {
    const auto path = temporary_directory() / "snapshot.jsonl";
    const auto config = parse({
        {"OV_CPU_STATE_ALLOCATION_DUMP_PATH", path.string()},
    });
    std::vector<std::thread> writers;
    std::vector<std::exception_ptr> failures(8);
    for (size_t index = 0; index < failures.size(); ++index) {
        writers.emplace_back([&, index] {
            try {
                append_state_allocation_snapshot(
                    config,
                    {},
                    snapshot_context(index + 1));
            } catch (...) {
                failures[index] = std::current_exception();
            }
        });
    }
    for (auto& writer : writers) {
        writer.join();
    }
    for (const auto& failure : failures) {
        EXPECT_FALSE(static_cast<bool>(failure));
    }

    const auto lines = read_nonempty_lines(path);
    ASSERT_EQ(lines.size(), 8u);
    uint64_t prior = 0;
    for (const auto& line : lines) {
        parse_json_object_with_unique_keys(line);
        const auto sequence = extract_sequence(line);
        EXPECT_GT(sequence, prior);
        prior = sequence;
    }

    std::atomic<uint64_t> exhausted_sequence{
        std::numeric_limits<uint64_t>::max()};
    uint64_t untouched = 99;
    EXPECT_FALSE(
        try_take_nonwrapping_counter(
            exhausted_sequence,
            untouched));
    EXPECT_EQ(untouched, 99u);
    EXPECT_EQ(
        exhausted_sequence.load(),
        std::numeric_limits<uint64_t>::max());
}

}  // namespace
}  // namespace ov::intel_cpu

#endif  // CPU_DEBUG_CAPS
```

Do not add a 22nd test or weaken any assertion. The two failure/cancellation
tests intentionally exercise the exact `StateAllocationInferenceAttempt` RAII
type used by the production `infer()` hook: its unconditional constructor
transition clears an earlier success before either failure path can exit.

## Step 3: Fresh configure and prove the exact RED reason under the guard

- [ ] Create a never-used Task 03 attempt evidence directory.
- [ ] Re-run the Task 02 observer configure so the new globbed test is present.
- [ ] Build the focused unit target and require nonzero for absent Task 03 APIs.
- [ ] Validate both guard records immediately.

Every invocation below is synchronous. The accepted wrapper itself reads the
new receipt and log, validates the exact command, expected child exit, 2,048
MiB floor, suspended Job assignment, complete memory samples, zero active
processes/survivors, timeout and emergency fields, and log hash before it can
return successfully. Therefore a wrapper exception stops the sequence
immediately; Step 10 independently re-reads all persisted records as a
cumulative audit.

```powershell
Set-AndAssert-Task03ProcessBypass
$guardEvidence =
  "R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task03-attempt-001"
if ((Test-Path -LiteralPath $guardEvidence) -and
    @(Get-ChildItem -LiteralPath $guardEvidence -Force).Count -ne 0) {
  throw "Task 03 evidence exists; audit it and choose a new numbered attempt"
}
$wrapper = "R:\scripts\testing\invoke_guarded_command.ps1"
$cmake =
  "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
$observerVariable = "OV_CPU_STATE_ALLOCATION_DUMP_PATH"
$task03PriorObserverValue =
  [Environment]::GetEnvironmentVariable(
    $observerVariable,
    "Process")
Remove-Item "Env:$observerVariable" -ErrorAction SilentlyContinue
$configureCommand = @(
  $cmake, "-S", "O:\", "-B", "C:\ov-build\state-observer",
  "-G", "Visual Studio 18 2026", "-A", "x64",
  "-DENABLE_DEBUG_CAPS=ON", "-DENABLE_CPU_DEBUG_CAPS=ON",
  "-DBUILD_SHARED_LIBS=ON", "-DENABLE_HETERO=OFF",
  "-DENABLE_TESTS=ON", "-DENABLE_FUNCTIONAL_TESTS=OFF",
  "-DENABLE_SAMPLES=OFF", "-DENABLE_PYTHON=OFF",
  "-DENABLE_INTEL_GPU=OFF", "-DENABLE_INTEL_NPU=OFF",
  "-DENABLE_OV_ONNX_FRONTEND=OFF",
  "-DENABLE_OV_PADDLE_FRONTEND=OFF",
  "-DENABLE_OV_TF_FRONTEND=OFF", "-DENABLE_LTO=OFF"
)
$buildUnitCommand = @(
  $cmake, "--build", "C:\ov-build\state-observer",
  "--config", "Release", "--target", "ov_cpu_unit_tests",
  "--parallel", "2"
)

& $wrapper `
  -Label task3-configure-red `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $configureCommand
Assert-Task03ControllerPolicy

$cache =
  Get-Content -LiteralPath "C:\ov-build\state-observer\CMakeCache.txt"
foreach ($required in @(
  "BUILD_SHARED_LIBS:BOOL=ON",
  "ENABLE_HETERO:BOOL=OFF",
  "ENABLE_DEBUG_CAPS:BOOL=ON",
  "ENABLE_CPU_DEBUG_CAPS:BOOL=ON",
  "ENABLE_TESTS:BOOL=ON",
  "ENABLE_FUNCTIONAL_TESTS:BOOL=OFF"
)) {
  if ($cache -notcontains $required) {
    throw "Observer configure cache is missing '$required'"
  }
}

& $wrapper `
  -Label task3-build-red `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit NonZero `
  -Command $buildUnitCommand
Assert-Task03ControllerPolicy

$redLog =
  Get-Content -LiteralPath "$guardEvidence\task3-build-red.log" -Raw
$requiredMissingContract =
  "StateAllocationDumpConfig|StateAllocationPathKind|" +
  "StateAllocationRequestState|try_take_nonwrapping_counter|" +
  "append_serialized_state_allocation_snapshot_with_factory|" +
  "validate_json_object_with_unique_keys"
if ($redLog -notmatch $requiredMissingContract) {
  throw "Task 03 RED did not fail for an absent Task 03 contract"
}
if ($redLog -match
    "out of memory|low memory|timed out|cannot find.*O:|CMake Error|" +
    "fatal error C|error LNK") {
  throw "Task 03 RED is environmental rather than contractual"
}
$errorDiagnostics = @(
  $redLog -split '\r?\n' |
    Where-Object {
      $_ -match
        '(?i)(?:CMake Error|fatal error C\d+|' +
        'error (?:C|LNK|MSB)\d+\s*:)'
    }
)
$allowedMissingContractDiagnostic =
  'state_allocations_writer_test\.cpp\(\d+(?:,\d+)?\)\s*:' +
  '\s*error C(?:2039|2061|2065|2143|2146|2440|2653|2672|3646|3861|4430)\s*:'
$selectedContractErrors = @(
  $errorDiagnostics |
    Where-Object {
      $_ -match $allowedMissingContractDiagnostic
    }
)
if ($selectedContractErrors.Count -eq 0) {
  throw "Task 03 RED contains no selected missing-contract diagnostic"
}
$unexpectedErrorDiagnostics = @(
  $errorDiagnostics |
    Where-Object {
      $_ -notmatch $allowedMissingContractDiagnostic
    }
)
if ($unexpectedErrorDiagnostics.Count -ne 0) {
  throw (
    "Task 03 RED contains an unrelated error diagnostic: " +
    ($unexpectedErrorDiagnostics -join " | "))
}
```

Expected: configure zero; build nonzero; the compiler reaches the new writer
test and fails only because Task 03 declarations/definitions do not exist.
The guard audit in Step 10 covers these RED records as mandatory evidence.

## Step 4: Add strict default-off path configuration

- [ ] Add the config value and pure parser under `CPU_DEBUG_CAPS`.
- [ ] Read exactly one observer environment name.
- [ ] Normalize to an absolute path, require exact lowercase `.jsonl`, an
      existing local directory parent, and either an absent target or a
      regular local file.
- [ ] Create no directory and expose no public property.

Apply these exact edits to
`src/plugins/intel_cpu/src/utils/debug_caps_config.h`.

Add the four headers inside the existing `#ifdef CPU_DEBUG_CAPS` include
group:

```cpp
#    include <filesystem>
#    include <functional>
#    include <optional>
#    include <string_view>
```

Immediately after `namespace ov::intel_cpu {`, add:

```cpp
struct StateAllocationDumpConfig {
    std::filesystem::path path;

    [[nodiscard]] bool enabled() const noexcept {
        return !path.empty();
    }
};

using EnvironmentLookup =
    std::function<std::optional<std::string>(std::string_view)>;

enum class StateAllocationPathKind {
    LOCAL_NORMAL,
    REMOTE,
    REPARSE,
    UNSUPPORTED,
};

using StateAllocationPathInspector =
    std::function<StateAllocationPathKind(
        const std::filesystem::path&)>;

StateAllocationDumpConfig parse_state_allocation_dump_config(
    const EnvironmentLookup& lookup);

StateAllocationDumpConfig
parse_state_allocation_dump_config_with_inspector_for_test(
    const EnvironmentLookup& lookup,
    const StateAllocationPathInspector& inspector);

void revalidate_state_allocation_dump_path(
    const std::filesystem::path& path);
```

Immediately after the existing `memoryStatisticsDumpPath` field, add:

```cpp
    StateAllocationDumpConfig stateAllocationDump;
```

In `src/plugins/intel_cpu/src/utils/debug_caps_config.cpp`, inside the existing
`#ifdef CPU_DEBUG_CAPS` region and immediately after
`#include "debug_caps_config.h"`, add these local-filesystem includes:

```cpp
#include <array>

#ifdef _WIN32
#    ifndef WIN32_LEAN_AND_MEAN
#        define WIN32_LEAN_AND_MEAN
#    endif
#    ifndef NOMINMAX
#        define NOMINMAX
#    endif
#    include <windows.h>
#elif defined(__linux__)
#    include <sys/vfs.h>
#elif defined(__APPLE__)
#    include <sys/mount.h>
#endif
```

Immediately after `namespace ov::intel_cpu {`, add these exact local-path
helpers and parser:

```cpp
namespace {

bool has_network_style_prefix(std::string_view value) {
    return value.size() >= 2 &&
           ((value[0] == '\\' && value[1] == '\\') ||
            (value[0] == '/' && value[1] == '/'));
}

StateAllocationPathKind inspect_existing_components(
    const std::filesystem::path& path) {
#ifdef _WIN32
    const auto root = path.root_path().wstring();
    if (root.empty()) {
        return StateAllocationPathKind::UNSUPPORTED;
    }
    const auto drive_type = GetDriveTypeW(root.c_str());
    if (drive_type == DRIVE_REMOTE) {
        return StateAllocationPathKind::REMOTE;
    }
    if (drive_type != DRIVE_FIXED &&
        drive_type != DRIVE_REMOVABLE &&
        drive_type != DRIVE_RAMDISK) {
        return StateAllocationPathKind::UNSUPPORTED;
    }
    const auto root_attributes =
        GetFileAttributesW(path.root_path().c_str());
    if (root_attributes == INVALID_FILE_ATTRIBUTES) {
        return StateAllocationPathKind::UNSUPPORTED;
    }
    if ((root_attributes &
         FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
        return StateAllocationPathKind::REPARSE;
    }

    auto current = path.root_path();
    const auto relative =
        path.lexically_relative(path.root_path());
    for (const auto& component : relative) {
        current /= component;
        const auto attributes =
            GetFileAttributesW(current.c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES) {
            const auto failure = GetLastError();
            if (failure == ERROR_FILE_NOT_FOUND ||
                failure == ERROR_PATH_NOT_FOUND) {
                break;
            }
            return StateAllocationPathKind::UNSUPPORTED;
        }
        if ((attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
            return StateAllocationPathKind::REPARSE;
        }
    }
    return StateAllocationPathKind::LOCAL_NORMAL;
#else
    auto current = path.root_path();
    std::error_code root_error;
    const auto root_status =
        std::filesystem::symlink_status(
            current,
            root_error);
    if (root_error) {
        return StateAllocationPathKind::UNSUPPORTED;
    }
    if (std::filesystem::is_symlink(root_status)) {
        return StateAllocationPathKind::REPARSE;
    }
    const auto relative =
        path.lexically_relative(path.root_path());
    for (const auto& component : relative) {
        current /= component;
        std::error_code error;
        const auto status =
            std::filesystem::symlink_status(current, error);
        if (error == std::errc::no_such_file_or_directory ||
            status.type() == std::filesystem::file_type::not_found) {
            break;
        }
        if (error) {
            return StateAllocationPathKind::UNSUPPORTED;
        }
        if (std::filesystem::is_symlink(status)) {
            return StateAllocationPathKind::REPARSE;
        }
    }

#    if defined(__linux__)
    struct statfs filesystem {};
    if (::statfs(path.parent_path().c_str(), &filesystem) != 0) {
        return StateAllocationPathKind::UNSUPPORTED;
    }
    constexpr std::array<long, 7> network_types{
        0x00006969L,
        0x0000517BL,
        static_cast<long>(0xFF534D42UL),
        0x73757245L,
        0x01021997L,
        0x00C36400L,
        0x5346414FL,
    };
    for (const auto network_type : network_types) {
        if (filesystem.f_type == network_type) {
            return StateAllocationPathKind::REMOTE;
        }
    }
    return StateAllocationPathKind::LOCAL_NORMAL;
#    elif defined(__APPLE__)
    struct statfs filesystem {};
    if (::statfs(path.parent_path().c_str(), &filesystem) != 0) {
        return StateAllocationPathKind::UNSUPPORTED;
    }
    return (filesystem.f_flags & MNT_LOCAL) != 0
               ? StateAllocationPathKind::LOCAL_NORMAL
               : StateAllocationPathKind::REMOTE;
#    else
    static_cast<void>(path);
    return StateAllocationPathKind::UNSUPPORTED;
#    endif
#endif
}

std::filesystem::path validate_state_allocation_dump_path(
    const std::filesystem::path& configured,
    const StateAllocationPathInspector& inspector) {
    if (!inspector) {
        OPENVINO_THROW(
            "state allocation path inspector is empty");
    }
    const auto configured_text = configured.string();
    if (configured.empty() ||
        has_network_style_prefix(configured_text)) {
        OPENVINO_THROW(
            "state allocation dump path must be local");
    }

    std::error_code error;
    auto path = std::filesystem::absolute(
        configured,
        error);
    if (error) {
        OPENVINO_THROW(
            "unable to resolve state allocation dump path: ",
            error.message());
    }
    path = path.lexically_normal();
    if (path == path.root_path() ||
        path.parent_path() == path.root_path()) {
        OPENVINO_THROW(
            "state allocation dump path cannot use a filesystem root");
    }
    if (path.extension() != ".jsonl") {
        OPENVINO_THROW(
            "state allocation dump path must end in .jsonl");
    }

    const auto initial_kind = inspector(path);
    if (initial_kind !=
        StateAllocationPathKind::LOCAL_NORMAL) {
        OPENVINO_THROW(
            "state allocation dump path is remote, reparse, "
            "or unsupported");
    }

    const auto parent = path.parent_path();
    if (parent.empty() ||
        !std::filesystem::exists(parent, error) || error) {
        OPENVINO_THROW(
            "state allocation dump parent does not exist");
    }
    if (!std::filesystem::is_directory(parent, error) || error) {
        OPENVINO_THROW(
            "state allocation dump parent is not a directory");
    }

    path = std::filesystem::weakly_canonical(path, error);
    if (error) {
        OPENVINO_THROW(
            "unable to canonicalize state allocation dump path: ",
            error.message());
    }
    if (path.extension() != ".jsonl") {
        OPENVINO_THROW(
            "canonical state allocation dump path must end in .jsonl");
    }
    if (inspector(path) !=
        StateAllocationPathKind::LOCAL_NORMAL) {
        OPENVINO_THROW(
            "canonical state allocation dump path is remote, "
            "reparse, or unsupported");
    }

    const bool target_exists =
        std::filesystem::exists(path, error);
    if (error) {
        OPENVINO_THROW(
            "unable to inspect state allocation dump target: ",
            error.message());
    }
    if (target_exists &&
        (!std::filesystem::is_regular_file(path, error) || error)) {
        OPENVINO_THROW(
            "state allocation dump target is not a regular file");
    }
    return {std::move(path)};
}

}  // namespace

StateAllocationDumpConfig
parse_state_allocation_dump_config_with_inspector_for_test(
    const EnvironmentLookup& lookup,
    const StateAllocationPathInspector& inspector) {
    if (!lookup) {
        OPENVINO_THROW(
            "state allocation environment lookup is empty");
    }
    const auto configured =
        lookup("OV_CPU_STATE_ALLOCATION_DUMP_PATH");
    if (!configured.has_value() || configured->empty()) {
        return {};
    }
    return {
        validate_state_allocation_dump_path(
            std::filesystem::path{*configured},
            inspector)};
}

StateAllocationDumpConfig parse_state_allocation_dump_config(
    const EnvironmentLookup& lookup) {
    return
        parse_state_allocation_dump_config_with_inspector_for_test(
            lookup,
            inspect_existing_components);
}

void revalidate_state_allocation_dump_path(
    const std::filesystem::path& path) {
    const auto validated =
        validate_state_allocation_dump_path(
            path,
            inspect_existing_components);
    if (validated != path) {
        OPENVINO_THROW(
            "state allocation dump path changed after configuration");
    }
}
```

At the end of `DebugCapsConfig::readProperties()`, immediately after the
existing `OV_CPU_MEMORY_STATISTICS_PATH` block and before the function's
closing brace, add:

```cpp
    stateAllocationDump = parse_state_allocation_dump_config(
        [&](std::string_view name)
            -> std::optional<std::string> {
            const auto key = std::string{name};
            const auto* value = readEnv(key.c_str());
            return value == nullptr
                       ? std::nullopt
                       : std::optional<std::string>{value};
        });
```

The pure parser invokes its lookup once with the exact observer variable.
Direct UNC/network-style and filesystem-root destinations fail before
filesystem probing. On Windows, `GetDriveTypeW` rejects mapped remote drives
before component probing, and `FILE_ATTRIBUTE_REPARSE_POINT` rejects the root
or any existing component. Linux rejects known NFS/SMB/CIFS/CODA/9P/Ceph/AFS
filesystem types and symlink components; macOS requires `MNT_LOCAL` and no
symlink component; unknown platforms fail closed. The same system inspector
runs immediately before each file open in Step 5. The injected inspector is a
private debug-only test seam, never used by production configuration. Other
pre-existing debug-capability variables remain unrelated; no observer truth
is read from them.

## Step 5: Extend the private Task 02 module with exact writer contracts

- [ ] Replace the Task 02 private header with the exact resulting body below.
- [ ] Add the bounded JSON syntax/duplicate validator and locked line writer.
- [ ] Add strict nested private-context parsing.
- [ ] Add process-wide request and sequence counters with no wraparound reuse.
- [ ] Add the request lifecycle state machine and final capture/append entry.

Replace
`O:\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp` with this
exact body:

```cpp
// Copyright (C) 2018-2026 Intel Corporation
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#ifdef CPU_DEBUG_CAPS

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <functional>
#include <memory>
#include <optional>
#include <ostream>
#include <string>
#include <string_view>
#include <vector>

#include "cpu_types.h"
#include "memory_state.h"
#include "openvino/core/any.hpp"
#include "utils/debug_caps_config.h"

namespace ov::intel_cpu {

inline constexpr size_t kMaxStateAllocationSnapshotBytes =
    1024U * 1024U;

enum class StateMemoryRole {
    INPUT,
    OUTPUT,
    KV,
    BEAM,
    SCALE_ZP,
};

struct SnapshotContext {
    uint32_t pid;
    uint64_t observer_request_id;
    std::string correlation_id;
    std::string trigger = "query_state";
    std::string phase;
    std::string observer_plugin_device = "CPU";
};

struct StateAllocationObserverContext {
    uint32_t schema_version = 0;
    std::string correlation_id;
};

enum class AllocationOwnerDomain {
    DNNL_MEMORY_BLOCK,
    PLAIN_TENSOR_OWNER,
};

struct ObservedAllocation {
    std::string state_name;
    std::string state_class;
    StateMemoryRole role;
    AllocationOwnerDomain source_owner_domain;
    size_t source_owner_ordinal;
    std::string element_type;
    VectorDims shape;
    VectorDims order;
    VectorDims strides;
    size_t active_descriptor_bytes;
    std::optional<size_t> reserved_backing_bytes;
    bool owned_backing;
    bool external_backing;
};

struct StateAllocationRecord {
    std::string state_name;
    std::string state_class;
    StateMemoryRole role;
    AllocationOwnerDomain owner_domain;
    size_t block_ordinal;
    std::optional<size_t> aliases_block_ordinal;
    std::string element_type;
    VectorDims shape;
    VectorDims order;
    VectorDims strides;
    size_t active_descriptor_bytes;
    std::optional<size_t> reserved_backing_bytes;
    bool owned_backing;
    bool external_backing;
};

struct StateAllocationTotal {
    std::string state_name;
    size_t active_descriptor_bytes = 0;
    size_t unique_reserved_backing_bytes = 0;
    size_t beam_reserved_bytes = 0;
    size_t scale_zp_reserved_bytes = 0;
    size_t total_physical_state_bytes = 0;
};

struct StateAllocationSnapshot {
    uint32_t schema_version = 1;
    uint64_t sequence = 0;
    uint32_t pid = 0;
    uint64_t observer_request_id = 0;
    std::string correlation_id;
    std::string trigger;
    std::string phase;
    std::string observer_plugin_device;
    std::vector<StateAllocationRecord> records;
    std::vector<StateAllocationTotal> state_totals;
    size_t unique_reserved_bytes = 0;
    size_t beam_reserved_bytes = 0;
    size_t scale_zp_reserved_bytes = 0;
    size_t total_physical_state_bytes = 0;
};

class StateAllocationRequestState {
public:
    explicit StateAllocationRequestState(
        const StateAllocationDumpConfig& config) noexcept;

    StateAllocationRequestState(
        const StateAllocationRequestState&) = delete;
    StateAllocationRequestState& operator=(
        const StateAllocationRequestState&) = delete;

    [[nodiscard]] bool enabled() const noexcept;
    [[nodiscard]] uint64_t observer_request_id() const noexcept;
    void begin_infer_attempt() const noexcept;
    void complete_infer_attempt() const noexcept;
    [[nodiscard]] std::string phase(
        bool all_states_reset) const;
    [[nodiscard]] bool should_emit(
        bool is_aggregate_request) const noexcept;

private:
    uint64_t m_observer_request_id = 0;
    mutable std::atomic_bool m_latest_infer_succeeded{false};
};

class StateAllocationInferenceAttempt {
public:
    explicit StateAllocationInferenceAttempt(
        const StateAllocationRequestState& state) noexcept;

    StateAllocationInferenceAttempt(
        const StateAllocationInferenceAttempt&) = delete;
    StateAllocationInferenceAttempt& operator=(
        const StateAllocationInferenceAttempt&) = delete;

    void complete() noexcept;

private:
    const StateAllocationRequestState& m_state;
};

StateAllocationSnapshot build_state_allocation_snapshot(
    std::vector<ObservedAllocation> observed,
    uint64_t sequence,
    const SnapshotContext& context);

StateAllocationSnapshot capture_state_allocations(
    const std::vector<MemStatePtr>& states,
    uint64_t sequence,
    const SnapshotContext& context);

std::string serialize_state_allocation_snapshot(
    const StateAllocationSnapshot& snapshot);

StateAllocationObserverContext
parse_state_allocation_observer_context(
    const ov::RTMap& model_rt_info);

uint32_t current_process_id();
uint64_t next_observer_request_id() noexcept;
bool try_take_nonwrapping_counter(
    std::atomic<uint64_t>& next,
    uint64_t& value) noexcept;

void validate_json_object_with_unique_keys(
    std::string_view serialized);

using StateAllocationOutputFactory =
    std::function<std::unique_ptr<std::ostream>(
        const std::filesystem::path&)>;

void append_serialized_state_allocation_snapshot(
    const StateAllocationDumpConfig& config,
    std::string_view serialized);

void
append_serialized_state_allocation_snapshot_with_factory(
    const StateAllocationDumpConfig& config,
    std::string_view serialized,
    const StateAllocationOutputFactory& output_factory);

void append_state_allocation_snapshot(
    const StateAllocationDumpConfig& config,
    const std::vector<MemStatePtr>& states,
    const SnapshotContext& context);

}  // namespace ov::intel_cpu

#endif  // CPU_DEBUG_CAPS
```

The serialized-line entry is private plugin ABI. It exists so the writer can
be tested independently with complete Task 02 serializer output; it validates
before opening the file. Production `query_state()` uses only
`append_state_allocation_snapshot`.

In
`O:\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp`, preserve the
accepted Task 02 implementation exactly and add these includes inside its
existing `#ifdef CPU_DEBUG_CAPS` block:

```cpp
#include <atomic>
#include <exception>
#include <filesystem>
#include <fstream>
#include <mutex>
#include <string_view>
```

Add the platform process header after standard-library includes:

```cpp
#ifdef _WIN32
#    include <process.h>
#else
#    include <unistd.h>
#endif
```

Immediately after the accepted Task 02
`is_valid_utf8_without_controls` function and before `require_text`, add this
exact bounded duplicate-key-rejecting JSON parser:

```cpp
class UniqueKeyJsonParser {
public:
    explicit UniqueKeyJsonParser(std::string_view input)
        : m_input(input) {}

    void parse_object_document() {
        skip_whitespace();
        parse_object(0);
        skip_whitespace();
        if (m_position != m_input.size()) {
            fail("trailing data");
        }
    }

private:
    [[noreturn]] void fail(const char* reason) const {
        OPENVINO_THROW(
            "invalid state allocation JSON at byte ",
            m_position,
            ": ",
            reason);
    }

    void skip_whitespace() {
        while (m_position < m_input.size()) {
            const auto value = m_input[m_position];
            if (value != ' ' && value != '\t' &&
                value != '\r' && value != '\n') {
                return;
            }
            ++m_position;
        }
    }

    bool consume(char expected) {
        if (m_position < m_input.size() &&
            m_input[m_position] == expected) {
            ++m_position;
            return true;
        }
        return false;
    }

    void expect(char expected) {
        if (!consume(expected)) {
            fail("unexpected token");
        }
    }

    static bool is_digit(char value) {
        return value >= '0' && value <= '9';
    }

    static uint32_t hex_value(char value) {
        if (value >= '0' && value <= '9') {
            return static_cast<uint32_t>(value - '0');
        }
        if (value >= 'a' && value <= 'f') {
            return static_cast<uint32_t>(value - 'a' + 10);
        }
        if (value >= 'A' && value <= 'F') {
            return static_cast<uint32_t>(value - 'A' + 10);
        }
        OPENVINO_THROW("invalid JSON Unicode escape");
    }

    uint32_t parse_hex_quad() {
        if (m_input.size() - m_position < 4) {
            fail("truncated Unicode escape");
        }
        uint32_t value = 0;
        for (size_t index = 0; index < 4; ++index) {
            value =
                (value << 4U) |
                hex_value(m_input[m_position++]);
        }
        return value;
    }

    static void append_utf8(
        std::string& output,
        uint32_t code_point) {
        if (code_point <= 0x7FU) {
            output.push_back(static_cast<char>(code_point));
        } else if (code_point <= 0x7FFU) {
            output.push_back(static_cast<char>(
                0xC0U | (code_point >> 6U)));
            output.push_back(static_cast<char>(
                0x80U | (code_point & 0x3FU)));
        } else if (code_point <= 0xFFFFU) {
            output.push_back(static_cast<char>(
                0xE0U | (code_point >> 12U)));
            output.push_back(static_cast<char>(
                0x80U | ((code_point >> 6U) & 0x3FU)));
            output.push_back(static_cast<char>(
                0x80U | (code_point & 0x3FU)));
        } else {
            output.push_back(static_cast<char>(
                0xF0U | (code_point >> 18U)));
            output.push_back(static_cast<char>(
                0x80U | ((code_point >> 12U) & 0x3FU)));
            output.push_back(static_cast<char>(
                0x80U | ((code_point >> 6U) & 0x3FU)));
            output.push_back(static_cast<char>(
                0x80U | (code_point & 0x3FU)));
        }
    }

    std::string parse_string() {
        expect('"');
        std::string decoded;
        while (m_position < m_input.size()) {
            const auto raw = static_cast<uint8_t>(
                m_input[m_position++]);
            if (raw == static_cast<uint8_t>('"')) {
                if (!is_valid_utf8_without_controls(decoded)) {
                    fail("invalid string encoding or control");
                }
                return decoded;
            }
            if (raw < 0x20U) {
                fail("unescaped string control");
            }
            if (raw != static_cast<uint8_t>('\\')) {
                decoded.push_back(static_cast<char>(raw));
                continue;
            }
            if (m_position == m_input.size()) {
                fail("truncated string escape");
            }
            const auto escaped = m_input[m_position++];
            switch (escaped) {
            case '"':
                decoded.push_back('"');
                break;
            case '\\':
                decoded.push_back('\\');
                break;
            case '/':
                decoded.push_back('/');
                break;
            case 'b':
                decoded.push_back('\b');
                break;
            case 'f':
                decoded.push_back('\f');
                break;
            case 'n':
                decoded.push_back('\n');
                break;
            case 'r':
                decoded.push_back('\r');
                break;
            case 't':
                decoded.push_back('\t');
                break;
            case 'u': {
                uint32_t code_point = parse_hex_quad();
                if (code_point >= 0xD800U &&
                    code_point <= 0xDBFFU) {
                    if (!consume('\\') || !consume('u')) {
                        fail("missing low Unicode surrogate");
                    }
                    const auto low = parse_hex_quad();
                    if (low < 0xDC00U || low > 0xDFFFU) {
                        fail("invalid low Unicode surrogate");
                    }
                    code_point =
                        0x10000U +
                        ((code_point - 0xD800U) << 10U) +
                        (low - 0xDC00U);
                } else if (
                    code_point >= 0xDC00U &&
                    code_point <= 0xDFFFU) {
                    fail("unexpected low Unicode surrogate");
                }
                append_utf8(decoded, code_point);
                break;
            }
            default:
                fail("unsupported string escape");
            }
        }
        fail("unterminated string");
    }

    void parse_literal(std::string_view literal) {
        if (m_input.size() - m_position < literal.size() ||
            m_input.substr(m_position, literal.size()) != literal) {
            fail("invalid literal");
        }
        m_position += literal.size();
    }

    void parse_number() {
        consume('-');
        if (m_position == m_input.size()) {
            fail("truncated number");
        }
        if (consume('0')) {
            if (m_position < m_input.size() &&
                is_digit(m_input[m_position])) {
                fail("leading zero");
            }
        } else {
            if (m_position == m_input.size() ||
                m_input[m_position] < '1' ||
                m_input[m_position] > '9') {
                fail("invalid integer");
            }
            while (m_position < m_input.size() &&
                   is_digit(m_input[m_position])) {
                ++m_position;
            }
        }
        if (consume('.')) {
            if (m_position == m_input.size() ||
                !is_digit(m_input[m_position])) {
                fail("invalid fraction");
            }
            while (m_position < m_input.size() &&
                   is_digit(m_input[m_position])) {
                ++m_position;
            }
        }
        if (m_position < m_input.size() &&
            (m_input[m_position] == 'e' ||
             m_input[m_position] == 'E')) {
            ++m_position;
            if (m_position < m_input.size() &&
                (m_input[m_position] == '+' ||
                 m_input[m_position] == '-')) {
                ++m_position;
            }
            if (m_position == m_input.size() ||
                !is_digit(m_input[m_position])) {
                fail("invalid exponent");
            }
            while (m_position < m_input.size() &&
                   is_digit(m_input[m_position])) {
                ++m_position;
            }
        }
    }

    void parse_array(size_t depth) {
        if (depth > 64) {
            fail("nesting exceeds 64");
        }
        expect('[');
        skip_whitespace();
        if (consume(']')) {
            return;
        }
        while (true) {
            parse_value(depth + 1);
            skip_whitespace();
            if (consume(']')) {
                return;
            }
            expect(',');
            skip_whitespace();
        }
    }

    void parse_object(size_t depth) {
        if (depth > 64) {
            fail("nesting exceeds 64");
        }
        expect('{');
        skip_whitespace();
        std::set<std::string> keys;
        if (consume('}')) {
            return;
        }
        while (true) {
            if (m_position == m_input.size() ||
                m_input[m_position] != '"') {
                fail("object key is not a string");
            }
            auto key = parse_string();
            if (!keys.insert(std::move(key)).second) {
                fail("duplicate object key");
            }
            skip_whitespace();
            expect(':');
            skip_whitespace();
            parse_value(depth + 1);
            skip_whitespace();
            if (consume('}')) {
                return;
            }
            expect(',');
            skip_whitespace();
        }
    }

    void parse_value(size_t depth) {
        if (depth > 64 || m_position == m_input.size()) {
            fail("missing or over-nested value");
        }
        switch (m_input[m_position]) {
        case '{':
            parse_object(depth);
            return;
        case '[':
            parse_array(depth);
            return;
        case '"':
            static_cast<void>(parse_string());
            return;
        case 't':
            parse_literal("true");
            return;
        case 'f':
            parse_literal("false");
            return;
        case 'n':
            parse_literal("null");
            return;
        default:
            if (m_input[m_position] == '-' ||
                is_digit(m_input[m_position])) {
                parse_number();
                return;
            }
            fail("unsupported value");
        }
    }

    std::string_view m_input;
    size_t m_position = 0;
};
```

The validator deliberately rejects decoded control characters as well as
invalid JSON. Task 02 already guarantees observer strings contain no control
characters, so this strengthens rather than broadens the accepted writer
surface.

Still inside the anonymous namespace, immediately after that parser class,
add these exact process-wide writer helpers:

```cpp
std::mutex& state_allocation_writer_mutex() {
    static std::mutex mutex;
    return mutex;
}

uint64_t next_snapshot_sequence_locked() noexcept {
    static std::atomic<uint64_t> next_sequence{1};
    uint64_t sequence = 0;
    if (!try_take_nonwrapping_counter(
            next_sequence,
            sequence)) {
        std::terminate();
    }
    return sequence;
}

void validate_serialized_json_line(
    std::string_view serialized) {
    if (serialized.empty() ||
        serialized.size() >
            kMaxStateAllocationSnapshotBytes) {
        OPENVINO_THROW(
            "state allocation JSON line has invalid size");
    }
    if (serialized.find('\r') != std::string_view::npos ||
        serialized.find('\n') != std::string_view::npos) {
        OPENVINO_THROW(
            "state allocation JSON must be one line");
    }
    validate_json_object_with_unique_keys(serialized);
}

void validate_existing_jsonl_unlocked(
    const StateAllocationDumpConfig& config) {
    revalidate_state_allocation_dump_path(config.path);

    std::error_code error;
    const auto exists =
        std::filesystem::exists(config.path, error);
    if (error) {
        OPENVINO_THROW(
            "unable to inspect existing state allocation JSONL");
    }
    if (!exists) {
        return;
    }

    const auto size =
        std::filesystem::file_size(config.path, error);
    if (error) {
        OPENVINO_THROW(
            "unable to size existing state allocation JSONL");
    }
    if (size == 0) {
        return;
    }

    std::ifstream input(config.path, std::ios::binary);
    if (!input) {
        OPENVINO_THROW(
            "unable to open existing state allocation JSONL");
    }
    std::string line;
    line.reserve(4096);
    char value = '\0';
    while (input.get(value)) {
        if (value == '\r') {
            OPENVINO_THROW(
                "existing state allocation JSONL contains CR");
        }
        if (value == '\n') {
            if (line.empty()) {
                OPENVINO_THROW(
                    "existing state allocation JSONL has an "
                    "empty record");
            }
            validate_serialized_json_line(line);
            line.clear();
            continue;
        }
        if (line.size() ==
            kMaxStateAllocationSnapshotBytes) {
            OPENVINO_THROW(
                "existing state allocation JSONL line is too large");
        }
        line.push_back(value);
    }
    if (input.bad()) {
        OPENVINO_THROW(
            "unable to read existing state allocation JSONL");
    }
    if (!line.empty()) {
        OPENVINO_THROW(
            "existing state allocation JSONL has an "
            "unterminated tail");
    }
}

std::unique_ptr<std::ostream> open_append_stream(
    const std::filesystem::path& path) {
    return std::make_unique<std::ofstream>(
        path,
        std::ios::binary | std::ios::out | std::ios::app);
}

void append_json_line_unlocked(
    const StateAllocationDumpConfig& config,
    std::string_view serialized,
    const StateAllocationOutputFactory& output_factory) {
    if (!config.enabled() ||
        config.path.extension() != ".jsonl" ||
        !output_factory) {
        OPENVINO_THROW(
            "state allocation writer is not validly configured");
    }
    validate_existing_jsonl_unlocked(config);
    revalidate_state_allocation_dump_path(config.path);
    auto output = output_factory(config.path);
    if (!output || !*output) {
        OPENVINO_THROW(
            "unable to open state allocation JSONL output");
    }
    std::string line{serialized};
    line.push_back('\n');
    output->write(
        line.data(),
        static_cast<std::streamsize>(line.size()));
    if (!*output) {
        OPENVINO_THROW(
            "unable to write complete state allocation JSONL line");
    }
    output->flush();
    if (!*output) {
        OPENVINO_THROW(
            "unable to flush complete state allocation JSONL line");
    }
}

const ov::AnyMap& require_nested_map(
    const ov::AnyMap& parent,
    const char* key) {
    const auto found = parent.find(key);
    if (found == parent.end() ||
        !found->second.is<ov::AnyMap>()) {
        OPENVINO_THROW(
            "missing or malformed private model context: ",
            key);
    }
    return found->second.as<ov::AnyMap>();
}

bool is_schema_one(const ov::Any& value) {
    return value.is<uint32_t>() &&
           value.as<uint32_t>() == 1;
}
```

Finally, after the accepted Task 02
`serialize_state_allocation_snapshot` definition and before the closing
`namespace ov::intel_cpu` brace, add these exact definitions:

```cpp
StateAllocationObserverContext
parse_state_allocation_observer_context(
    const ov::RTMap& model_rt_info) {
    const auto& genai =
        require_nested_map(model_rt_info, "openvino_genai");
    const auto& observer = require_nested_map(
        genai,
        "cpu_state_allocation_observer");
    if (observer.size() != 2) {
        OPENVINO_THROW(
            "private state allocation observer context must have "
            "exactly schema and correlation_id");
    }

    const auto schema = observer.find("schema");
    const auto correlation = observer.find("correlation_id");
    if (schema == observer.end() ||
        !is_schema_one(schema->second) ||
        correlation == observer.end() ||
        !correlation->second.is<std::string>()) {
        OPENVINO_THROW(
            "private state allocation observer context is malformed");
    }
    const auto& correlation_id =
        correlation->second.as<std::string>();
    if (!is_lower_hex_correlation(correlation_id)) {
        OPENVINO_THROW(
            "private observer correlation_id must be 32 "
            "lowercase hexadecimal characters");
    }
    return {1, correlation_id};
}

uint32_t current_process_id() {
#ifdef _WIN32
    const auto pid = _getpid();
#else
    const auto pid = getpid();
#endif
    if (pid <= 0) {
        OPENVINO_THROW(
            "unable to obtain a positive observer process ID");
    }
    return static_cast<uint32_t>(pid);
}

bool try_take_nonwrapping_counter(
    std::atomic<uint64_t>& next,
    uint64_t& value) noexcept {
    auto current = next.load(std::memory_order_relaxed);
    while (current != 0 &&
           current != std::numeric_limits<uint64_t>::max()) {
        if (next.compare_exchange_weak(
                current,
                current + 1,
                std::memory_order_relaxed,
                std::memory_order_relaxed)) {
            value = current;
            return true;
        }
    }
    return false;
}

uint64_t next_observer_request_id() noexcept {
    static std::atomic<uint64_t> next_id{1};
    uint64_t request_id = 0;
    if (!try_take_nonwrapping_counter(
            next_id,
            request_id)) {
        std::terminate();
    }
    return request_id;
}

StateAllocationRequestState::StateAllocationRequestState(
    const StateAllocationDumpConfig& config) noexcept
    : m_observer_request_id{
          config.enabled() ? next_observer_request_id() : 0} {}

bool StateAllocationRequestState::enabled() const noexcept {
    return m_observer_request_id != 0;
}

uint64_t
StateAllocationRequestState::observer_request_id() const noexcept {
    return m_observer_request_id;
}

void StateAllocationRequestState::begin_infer_attempt()
    const noexcept {
    if (enabled()) {
        m_latest_infer_succeeded.store(
            false,
            std::memory_order_release);
    }
}

void StateAllocationRequestState::complete_infer_attempt()
    const noexcept {
    if (enabled()) {
        m_latest_infer_succeeded.store(
            true,
            std::memory_order_release);
    }
}

StateAllocationInferenceAttempt::StateAllocationInferenceAttempt(
    const StateAllocationRequestState& state) noexcept
    : m_state(state) {
    m_state.begin_infer_attempt();
}

void StateAllocationInferenceAttempt::complete() noexcept {
    m_state.complete_infer_attempt();
}

std::string StateAllocationRequestState::phase(
    bool all_states_reset) const {
    if (!enabled()) {
        OPENVINO_THROW(
            "disabled state allocation observer has no phase");
    }
    if (all_states_reset) {
        m_latest_infer_succeeded.store(
            false,
            std::memory_order_release);
        return "fresh";
    }
    return m_latest_infer_succeeded.load(
               std::memory_order_acquire)
               ? "post_infer"
               : "seeded_no_infer";
}

bool StateAllocationRequestState::should_emit(
    bool is_aggregate_request) const noexcept {
    return enabled() && !is_aggregate_request;
}

void validate_json_object_with_unique_keys(
    std::string_view serialized) {
    if (serialized.empty() ||
        serialized.size() >
            kMaxStateAllocationSnapshotBytes) {
        OPENVINO_THROW(
            "state allocation JSON object has invalid size");
    }
    UniqueKeyJsonParser{serialized}.parse_object_document();
}

void append_serialized_state_allocation_snapshot(
    const StateAllocationDumpConfig& config,
    std::string_view serialized) {
    append_serialized_state_allocation_snapshot_with_factory(
        config,
        serialized,
        open_append_stream);
}

void
append_serialized_state_allocation_snapshot_with_factory(
    const StateAllocationDumpConfig& config,
    std::string_view serialized,
    const StateAllocationOutputFactory& output_factory) {
    validate_serialized_json_line(serialized);
    const std::lock_guard<std::mutex> lock{
        state_allocation_writer_mutex()};
    append_json_line_unlocked(
        config,
        serialized,
        output_factory);
}

void append_state_allocation_snapshot(
    const StateAllocationDumpConfig& config,
    const std::vector<MemStatePtr>& states,
    const SnapshotContext& context) {
    if (!config.enabled()) {
        OPENVINO_THROW(
            "disabled state allocation observer cannot append");
    }
    const std::lock_guard<std::mutex> lock{
        state_allocation_writer_mutex()};
    const auto sequence = next_snapshot_sequence_locked();
    const auto serialized =
        serialize_state_allocation_snapshot(
            capture_state_allocations(
                states,
                sequence,
                context));
    validate_serialized_json_line(serialized);
    append_json_line_unlocked(
        config,
        serialized,
        open_append_stream);
}
```

The writer mutex covers sequence assignment, physical capture, serialization,
and the one complete append, so line order is also strictly increasing in
`sequence`. The request ID and sequence atomics are separate process-wide
function statics. Exhaustion terminates instead of wrapping and reusing an
identity: the shared CAS helper leaves both counter and output unchanged at
zero or `UINT64_MAX`, so concurrent exhaustion cannot publish or wrap through
zero/one. Existing nonempty output is scanned as bounded strict JSONL before
append; empty and fully valid newline-terminated JSONL are accepted, while
blank, malformed, oversized, CR-containing, or unterminated records fail
before output open. The path is revalidated before reading and again
immediately before opening the append stream, bounding but not claiming to
eliminate an external filesystem replacement race. No counter is touched on
the disabled path.

## Step 6: Parse immutable model context only for enabled compiled models

- [ ] Keep all new includes, members, initialization, and accessors under
      `CPU_DEBUG_CAPS`.
- [ ] Parse the private model marker exactly once in each enabled
      `CompiledModel` constructor.
- [ ] Do not parse any model metadata when the path is absent/empty.
- [ ] Expose the immutable value only through `CompiledModelHolder`.

In `src/plugins/intel_cpu/src/compiled_model.h`, add this guarded include
immediately after `#include "weights_cache.hpp"`:

```cpp
#ifdef CPU_DEBUG_CAPS
#    include "utils/state_allocations_dump.hpp"
#endif
```

Immediately after `const bool m_loaded_from_cache;`, add:

```cpp
#ifdef CPU_DEBUG_CAPS
    const StateAllocationObserverContext
        m_state_allocation_observer_context;
#endif
```

In `CompiledModelHolder`, immediately after the existing `id()` accessor and
before `private:`, add:

```cpp
#ifdef CPU_DEBUG_CAPS
    [[nodiscard]] const StateAllocationObserverContext&
    state_allocation_observer_context() const {
        OPENVINO_ASSERT(
            m_compiled_model->m_cfg.debugCaps
                .stateAllocationDump.enabled(),
            "state allocation observer context requested while "
            "the observer is disabled");
        OPENVINO_ASSERT(
            m_compiled_model
                    ->m_state_allocation_observer_context
                    .schema_version == 1,
            "state allocation observer context is not initialized");
        return m_compiled_model
            ->m_state_allocation_observer_context;
    }
#endif
```

In the `CompiledModel` initializer list in
`src/plugins/intel_cpu/src/compiled_model.cpp`, replace this exact preimage:

```cpp
      m_name{model->get_name()},
      m_loaded_from_cache(loaded_from_cache),
      m_sub_memory_manager(std::move(sub_memory_manager)) {
```

with:

```cpp
      m_name{model->get_name()},
      m_loaded_from_cache(loaded_from_cache),
#ifdef CPU_DEBUG_CAPS
      m_state_allocation_observer_context{
          m_cfg.debugCaps.stateAllocationDump.enabled()
              ? parse_state_allocation_observer_context(
                    model->get_rt_info())
              : StateAllocationObserverContext{}},
#endif
      m_sub_memory_manager(std::move(sub_memory_manager)) {
```

The conditional operator does not evaluate the parse branch when disabled.
The stored member is `const`, has no public property surface, and contains only
schema/correlation. Nested sub-models receive their own immutable copy by the
same constructor rule; no requested or actual device value is read.

## Step 7: Integrate lifecycle and the leaf `query_state()` trigger

- [ ] Allocate an observer ID only in enabled `SyncInferRequest` construction.
- [ ] Clear prior success as the first inference-attempt action.
- [ ] Mark success only after output pull completes.
- [ ] Compute phase from every private state reset flag.
- [ ] Let real subrequests emit; never append an aggregate delegation line.
- [ ] Append at the leaf before public wrappers are constructed.

In `src/plugins/intel_cpu/src/infer_request.h`, add this guarded include
immediately after `#include "proxy_mem_blk.h"`:

```cpp
#ifdef CPU_DEBUG_CAPS
#    include "utils/state_allocations_dump.hpp"
#endif
```

In the private method section, immediately after `sub_streams_infer();`, add:

```cpp
#ifdef CPU_DEBUG_CAPS
    [[nodiscard]] std::string observer_phase() const;
#endif
```

Immediately after the existing `CompiledModelHolder m_compiled_model;` member,
add this guarded default member initializer. C++ initializes members in
declaration order, so the already-constructed holder supplies the config
without changing the ordinary constructor text:

```cpp
#ifdef CPU_DEBUG_CAPS
    StateAllocationRequestState m_state_allocation_observer{
        m_compiled_model.graph()
            .getConfig()
            .debugCaps
            .stateAllocationDump};
#endif
```

Do not edit or reformat the constructor initializer. In particular, preserve
this exact ordinary text:

```cpp
SyncInferRequest::SyncInferRequest(CompiledModelHolder compiled_model)
    : ov::ISyncInferRequest(compiled_model.compiled_model()),
      m_compiled_model(std::move(compiled_model)) {
```

Do not use `CompiledModelHolder::id()`. The initializer above is the only
observer request-ID allocation site.

At the very start of `SyncInferRequest::infer()`, before
`OV_ITT_SCOPED_TASK_BASE`, add:

```cpp
#ifdef CPU_DEBUG_CAPS
    StateAllocationInferenceAttempt observer_attempt{
        m_state_allocation_observer};
#endif
```

Immediately after the existing successful:

```cpp
    graph.PullOutputData(m_outputs);
```

add:

```cpp
#ifdef CPU_DEBUG_CAPS
    observer_attempt.complete();
#endif
```

The exact RAII object's constructor is the begin hook tested by both synthetic
failure and real `ov::Cancelled` unit paths. There is no destructor that
restores success and no catch that marks completion. Therefore an exception or
cancellation after construction and before `complete()` leaves the latest
attempt unsuccessful.

Immediately before `SyncInferRequest::query_state()`, add this exact helper:

```cpp
#ifdef CPU_DEBUG_CAPS
std::string SyncInferRequest::observer_phase() const {
    bool all_states_reset = true;
    for (const auto& state : m_memory_states) {
        if (!state) {
            OPENVINO_THROW(
                "CPU query_state contains a null private state");
        }
        if (!state->is_reset_state()) {
            all_states_reset = false;
        }
    }
    return m_state_allocation_observer.phase(
        all_states_reset);
}
#endif
```

Do not replace or reformat the ordinary `query_state()` implementation. Its
delegation branch, local variable name `cur`, `states.insert(...)`, and
one-line leaf return remain byte-for-byte unchanged. Insert only this guarded
block after the existing delegating branch's closing brace and immediately
before the existing
`return {m_memory_states.begin(), m_memory_states.end()};`:

```cpp
#ifdef CPU_DEBUG_CAPS
    const auto& dump_config =
        m_compiled_model.graph()
            .getConfig()
            .debugCaps
            .stateAllocationDump;
    if (m_state_allocation_observer.should_emit(
            false)) {
        append_state_allocation_snapshot(
            dump_config,
            m_memory_states,
            SnapshotContext{
                current_process_id(),
                m_state_allocation_observer
                    .observer_request_id(),
                m_compiled_model
                    .state_allocation_observer_context()
                    .correlation_id,
                "query_state",
                observer_phase(),
                "CPU",
            });
    }
#endif
```

This hook reads only private CPU state owners and reset flags. The output field
is the literal `observer_plugin_device: "CPU"` supplied through the accepted
Task 02 context. It never reads requested-device, execution-device, phase,
request-ID, or plugin truth from the environment or model marker. The
delegating branch returns before the aggregate can append, while each real CPU
subrequest reaches this leaf hook. No declaration, include, condition, local
name, insertion expression, or return formatting is changed on the ordinary
CPU branch; Step 10 strips positive observer guards from the current and Task
02 sources and requires byte-for-byte equality.

## Step 8: Reconfigure, build, enumerate, and run exact GREEN twice

- [ ] Re-run a fresh configure after both globbed Task 03 sources exist.
- [ ] Build the focused unit target through the guard.
- [ ] Prove exactly 21 writer tests and 25 accepted dump tests are selected.
- [ ] Run the exact combined 46-test filter twice in separate guarded
      processes.
- [ ] Reject failed, skipped, disabled, or unstable selection.

```powershell
Set-AndAssert-Task03ProcessBypass
& $wrapper `
  -Label task3-configure-green `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $configureCommand
Assert-Task03ControllerPolicy

& $wrapper `
  -Label task3-build-unit-green `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $buildUnitCommand
Assert-Task03ControllerPolicy

$unitExecutable =
  "C:\ov-build\state-observer\bin\intel64\Release\ov_cpu_unit_tests.exe"
$focusedFilter =
  "StateAllocationsWriter.*:StateAllocationsDump.*"
& $wrapper `
  -Label task3-list-green `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command @(
    $unitExecutable,
    "--gtest_filter=$focusedFilter",
    "--gtest_list_tests"
  )
Assert-Task03ControllerPolicy

$listLog =
  Get-Content -LiteralPath "$guardEvidence\task3-list-green.log" -Raw
if ($listLog -notmatch '(?m)^StateAllocationsWriter\.\r?$' -or
    $listLog -notmatch '(?m)^StateAllocationsDump\.\r?$') {
  throw "Both focused Task 02/03 suites were not listed"
}
$writerSection = [regex]::Match(
  $listLog,
  '(?m)^StateAllocationsWriter\.\r?\n' +
    '(?<body>(?:  [^\r\n]+(?:\r?\n|$))+)' )
$dumpSection = [regex]::Match(
  $listLog,
  '(?m)^StateAllocationsDump\.\r?\n' +
    '(?<body>(?:  [^\r\n]+(?:\r?\n|$))+)' )
$writerCount = @(
  $writerSection.Groups['body'].Value -split '\r?\n' |
    Where-Object { $_ -match '^  [A-Za-z0-9_]+$' }
).Count
$dumpCount = @(
  $dumpSection.Groups['body'].Value -split '\r?\n' |
    Where-Object { $_ -match '^  [A-Za-z0-9_]+$' }
).Count
if ($writerCount -ne 21 -or $dumpCount -ne 25) {
  throw "Expected writer=21 and dump=25; got writer=$writerCount dump=$dumpCount"
}

foreach ($run in 1..2) {
  & $wrapper `
    -Label "task3-writer-green-$run" `
    -WorkingDirectory R:\ `
    -EvidenceRoot $guardEvidence `
    -ExpectedExit Zero `
    -Command @(
      $unitExecutable,
      "--gtest_filter=$focusedFilter"
    )
  Assert-Task03ControllerPolicy
  $runLog = Get-Content -LiteralPath `
    "$guardEvidence\task3-writer-green-$run.log" -Raw
  if ($runLog -notmatch
      '\[\s*PASSED\s*\]\s+46 tests?\.') {
    throw "Task 03 focused run $run did not pass exactly 46 tests"
  }
  if ($runLog -match
      '\[\s*(FAILED|SKIPPED|DISABLED)\s*\]') {
    throw "Task 03 focused run $run contains a non-passing result"
  }
}
```

If a compiler/test defect appears, add or strengthen a regression first, run a
new uniquely labelled RED, make the minimum fix, then repeat the entire fresh
configure/build/list/twice-GREEN sequence. Never edit assertions merely to
match an implementation.

## Step 9: Build production and run the real stock query-state gates

- [ ] Build the observer-enabled production CPU plugin through the guard.
- [ ] Configure a separate functional-tests-ON tree from the same exact source.
- [ ] Build and enumerate the real x64 CPU variable-state query fixture.
- [ ] Run it with the observer variable absent and prove no JSONL.
- [ ] Run only the invalid `.txt` path case, require nonzero for the path gate,
      and prove no file.

Build the production plugin in the main observer tree:

```powershell
Set-AndAssert-Task03ProcessBypass
$buildPluginCommand = @(
  $cmake, "--build", "C:\ov-build\state-observer",
  "--config", "Release",
  "--target", "openvino_intel_cpu_plugin",
  "--parallel", "2"
)
& $wrapper `
  -Label task3-plugin-green `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $buildPluginCommand
Assert-Task03ControllerPolicy

$pluginDll =
  "C:\ov-build\state-observer\bin\intel64\Release\openvino_intel_cpu_plugin.dll"
if (-not (Test-Path -LiteralPath $pluginDll)) {
  throw "Task 03 production CPU plugin DLL is missing"
}
foreach ($marker in @(
  "OV_CPU_STATE_ALLOCATION_DUMP_PATH",
  "cpu_state_allocation_observer",
  "state allocation dump path must be local",
  "state allocation JSONL"
)) {
  & rg -a -n --fixed-strings $marker $pluginDll
  if ($LASTEXITCODE -ne 0) {
    throw "Observer-enabled production DLL lacks marker '$marker'"
  }
}
```

The Task 02 configure deliberately had functional tests OFF, so do not pretend
that tree contains `ov_cpu_func_tests`. Configure a separate tree with the
same source and options except the explicit functional-tests switch:

```powershell
Set-AndAssert-Task03ProcessBypass
$functionalBuild =
  "C:\ov-build\state-observer-functional-task03"
$configureFunctionalCommand = @(
  $cmake, "-S", "O:\", "-B", $functionalBuild,
  "-G", "Visual Studio 18 2026", "-A", "x64",
  "-DENABLE_DEBUG_CAPS=ON", "-DENABLE_CPU_DEBUG_CAPS=ON",
  "-DBUILD_SHARED_LIBS=ON", "-DENABLE_HETERO=OFF",
  "-DENABLE_TESTS=ON", "-DENABLE_FUNCTIONAL_TESTS=ON",
  "-DENABLE_SAMPLES=OFF", "-DENABLE_PYTHON=OFF",
  "-DENABLE_INTEL_GPU=OFF", "-DENABLE_INTEL_NPU=OFF",
  "-DENABLE_OV_ONNX_FRONTEND=OFF",
  "-DENABLE_OV_PADDLE_FRONTEND=OFF",
  "-DENABLE_OV_TF_FRONTEND=OFF", "-DENABLE_LTO=OFF"
)
$buildFunctionalCommand = @(
  $cmake, "--build", $functionalBuild,
  "--config", "Release",
  "--target", "ov_cpu_func_tests",
  "--parallel", "2"
)

& $wrapper `
  -Label task3-configure-functional `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $configureFunctionalCommand
Assert-Task03ControllerPolicy

$functionalCache =
  Get-Content -LiteralPath "$functionalBuild\CMakeCache.txt"
foreach ($required in @(
  "BUILD_SHARED_LIBS:BOOL=ON",
  "ENABLE_HETERO:BOOL=OFF",
  "ENABLE_DEBUG_CAPS:BOOL=ON",
  "ENABLE_CPU_DEBUG_CAPS:BOOL=ON",
  "ENABLE_TESTS:BOOL=ON",
  "ENABLE_FUNCTIONAL_TESTS:BOOL=ON"
)) {
  if ($functionalCache -notcontains $required) {
    throw "Functional configure cache is missing '$required'"
  }
}

& $wrapper `
  -Label task3-build-functional `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $buildFunctionalCommand
Assert-Task03ControllerPolicy

$functionalExecutable =
  "$functionalBuild\bin\intel64\Release\ov_cpu_func_tests.exe"
$stockFilter =
  "smoke_VariableState/OVInferRequestVariableStateTest.inferreq_smoke_VariableState_QueryState/*"
& $wrapper `
  -Label task3-list-stock `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command @(
    $functionalExecutable,
    "--gtest_filter=$stockFilter",
    "--gtest_list_tests"
  )
Assert-Task03ControllerPolicy
$stockList =
  Get-Content -LiteralPath "$guardEvidence\task3-list-stock.log" -Raw
$stockSelected = @(
  $stockList -split '\r?\n' |
    Where-Object {
      $_ -match
        '^\s{2}inferreq_smoke_VariableState_QueryState/'
    }
)
if ($stockSelected.Count -ne 1 -or
    $stockList -notmatch 'targetDevice=CPU') {
  throw "Expected exactly one x64 CPU stock query-state fixture"
}
```

Run the stock test with the path absent from a fresh isolated working
directory. Preserve and restore any process-level value, and compare the
complete evidence-directory JSONL set plus the complete isolated
working-directory file set:

```powershell
Set-AndAssert-Task03ProcessBypass
$stockUnsetCwd = Join-Path $guardEvidence "stock-unset-cwd"
if (Test-Path -LiteralPath $stockUnsetCwd) {
  if (@(Get-ChildItem -LiteralPath $stockUnsetCwd -Force).Count -ne 0) {
    throw "Stock unset working directory is not fresh and empty"
  }
} else {
  New-Item -ItemType Directory -Path $stockUnsetCwd |
    Out-Null
}
$beforeJsonl = @(
  Get-ChildItem -LiteralPath $guardEvidence -Recurse `
    -Filter *.jsonl -File -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName }
) | Sort-Object
$beforeStockFiles = @(
  Get-ChildItem -LiteralPath $stockUnsetCwd -Recurse `
    -File -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName }
) | Sort-Object
try {
  Remove-Item "Env:$observerVariable" -ErrorAction SilentlyContinue
  & $wrapper `
    -Label task3-stock-unset `
    -WorkingDirectory $stockUnsetCwd `
    -EvidenceRoot $guardEvidence `
    -ExpectedExit Zero `
    -Command @(
      $functionalExecutable,
      "--gtest_filter=$stockFilter"
    )
} finally {
  if ($null -eq $task03PriorObserverValue) {
    Remove-Item "Env:$observerVariable" `
      -ErrorAction SilentlyContinue
  } else {
    [Environment]::SetEnvironmentVariable(
      $observerVariable,
      $task03PriorObserverValue,
    "Process")
  }
}
Assert-Task03ControllerPolicy
$stockLog =
  Get-Content -LiteralPath "$guardEvidence\task3-stock-unset.log" -Raw
if ($stockLog -notmatch '\[\s*PASSED\s*\]\s+1 test\.' -or
    $stockLog -match
      '\[\s*(FAILED|SKIPPED|DISABLED)\s*\]') {
  throw "Unset stock query-state gate did not pass exactly once"
}
$afterJsonl = @(
  Get-ChildItem -LiteralPath $guardEvidence -Recurse `
    -Filter *.jsonl -File -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName }
) | Sort-Object
if (Compare-Object $beforeJsonl $afterJsonl) {
  throw "Unset observer path created JSONL output"
}
$afterStockFiles = @(
  Get-ChildItem -LiteralPath $stockUnsetCwd -Recurse `
    -File -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName }
) | Sort-Object
if (Compare-Object $beforeStockFiles $afterStockFiles) {
  throw "Unset stock query created a file in its isolated working directory"
}
```

The in-process unit
`DisabledObserverDoesNotAllocateRequestId` proves the counter does not advance;
the stock functional process proves the real public `query_state()` remains
unchanged and produces no JSONL. A separate process cannot meaningfully
compare a process-local counter, so these two pieces of evidence are both
required and are not conflated.

Now run only the invalid configured-path gate:

```powershell
Set-AndAssert-Task03ProcessBypass
$invalidPath = Join-Path $guardEvidence "must-not-exist.txt"
if (Test-Path -LiteralPath $invalidPath) {
  throw "Invalid-path sentinel unexpectedly exists"
}
try {
  [Environment]::SetEnvironmentVariable(
    $observerVariable,
    $invalidPath,
    "Process")
  & $wrapper `
    -Label task3-stock-invalid `
    -WorkingDirectory R:\ `
    -EvidenceRoot $guardEvidence `
    -ExpectedExit NonZero `
    -Command @(
      $functionalExecutable,
      "--gtest_filter=$stockFilter"
    )
} finally {
  if ($null -eq $task03PriorObserverValue) {
    Remove-Item "Env:$observerVariable" `
      -ErrorAction SilentlyContinue
  } else {
    [Environment]::SetEnvironmentVariable(
      $observerVariable,
      $task03PriorObserverValue,
    "Process")
  }
}
Assert-Task03ControllerPolicy
if (Test-Path -LiteralPath $invalidPath) {
  throw "Invalid non-JSONL path created an output file"
}
$invalidLog =
  Get-Content -LiteralPath "$guardEvidence\task3-stock-invalid.log" -Raw
if ($invalidLog -notmatch
    'state allocation dump path must end in \.jsonl') {
  throw "Invalid-path process failed for the wrong reason"
}
```

Do not use an enabled valid path with this stock model: by design it has no
private GenAI observer marker and must fail closed. Task 04 supplies the first
synthetic enabled model with valid private context and native state
allocations. Its micro-plan must explicitly close any performance aggregation
window, obtain its controller acknowledgement, and record that ordering before
calling `query_state()`. Task 06 owns the final accepted controller protocol.
The direct writer unit tests here exercise private serialization mechanics,
not an accepted physical observation, and therefore cannot substitute for
that acknowledgement gate.

## Step 10: Run exact CMake, source, diff, containment, and guard audits

- [ ] Prove glob membership without changing CMake.
- [ ] Prove the diff is exactly nine paths and based on frozen preimages.
- [ ] Structurally prove every Task 02/03 observer marker is inside a positive
      `CPU_DEBUG_CAPS` branch.
- [ ] Prove the real constructor, `infer()`, compiled-model parser, and
      `query_state()` hooks have the required lexical placement and order.
- [ ] Prove no forbidden truth/content/address/public-API addition.
- [ ] Audit all 13 guard receipts including expected nonzero RED/invalid runs.
- [ ] Explicitly record the ordinary binary/symbol gate as deferred to Task 07.

First prove generated project membership:

```powershell
$productionProjects = @(
  & rg -l --fixed-strings "state_allocations_dump.cpp" `
    "C:\ov-build\state-observer" -g "*.vcxproj"
)
if ($LASTEXITCODE -ne 0 -or $productionProjects.Count -lt 1) {
  throw "Production projects did not discover state_allocations_dump.cpp"
}
$writerProjects = @(
  & rg -l --fixed-strings "state_allocations_writer_test.cpp" `
    "C:\ov-build\state-observer" -g "*.vcxproj"
)
if ($LASTEXITCODE -ne 0 -or
    $writerProjects.Count -ne 1 -or
    [IO.Path]::GetFileName($writerProjects[0]) -ne
      "ov_cpu_unit_tests.vcxproj") {
  throw "Writer test was not discovered exactly once by the unit target"
}
$uniqueProductionProjects = @(
  $productionProjects |
    ForEach-Object { [IO.Path]::GetFullPath($_) }
) | Sort-Object -Unique
$actualProductionProjectNames = @(
  $uniqueProductionProjects |
    ForEach-Object { [IO.Path]::GetFileName($_) }
) | Sort-Object
$expectedPluginProjectNames = @(
  "openvino_intel_cpu_plugin.vcxproj",
  "openvino_intel_cpu_plugin_obj.vcxproj"
) | Sort-Object
if ($uniqueProductionProjects.Count -ne 2 -or
    (Compare-Object `
      $expectedPluginProjectNames `
      $actualProductionProjectNames)) {
  throw (
    "Complete unique production project set is not exactly the " +
    "object/shared CPU plugin pair")
}
```

Require the exact diff and immutable Task 02 blobs:

```powershell
function Assert-ExactPropertySet {
  param(
    [Parameter(Mandatory=$true)]$Object,
    [Parameter(Mandatory=$true)][string[]]$Expected,
    [Parameter(Mandatory=$true)][string]$Label
  )
  $actual = @($Object.PSObject.Properties.Name) |
    Sort-Object
  $required = @($Expected) | Sort-Object
  if (Compare-Object $required $actual) {
    throw "$Label property set is not exact"
  }
}

$boundary = Get-Content -LiteralPath `
  "R:\.superpowers\sdd\task03-boundary.json" -Raw |
  ConvertFrom-Json
if ($boundary -isnot [PSCustomObject] -or
    $boundary.task03_preimage_blobs -isnot
      [PSCustomObject] -or
    $boundary.guard_blobs -isnot [PSCustomObject]) {
  throw "Task 03 boundary has invalid object/map JSON types"
}
foreach ($field in @(
  "schema",
  "roadmap_sha256",
  "controller_repo_root",
  "clean_core_commit",
  "clean_core_tree",
  "canonical_identity_sha256",
  "task02_receipt_sha256",
  "task02_plan_sha256",
  "task02_commit",
  "task02_tree",
  "task02_parent",
  "task02_subject"
)) {
  if ($boundary.$field -isnot [string]) {
    throw "Task 03 boundary field is not an exact string: $field"
  }
}
Assert-ExactPropertySet `
  -Object $boundary `
  -Label "Task 03 boundary" `
  -Expected @(
    "schema",
    "roadmap_sha256",
    "controller_repo_root",
    "clean_core_commit",
    "clean_core_tree",
    "canonical_identity_sha256",
    "task02_receipt_sha256",
    "task02_plan_sha256",
    "task02_commit",
    "task02_tree",
    "task02_parent",
    "task02_subject",
    "task03_preimage_blobs",
    "guard_blobs"
  )
Assert-ExactPropertySet `
  -Object $boundary.task03_preimage_blobs `
  -Expected $task03Paths `
  -Label "Task 03 preimage map"
Assert-ExactPropertySet `
  -Object $boundary.guard_blobs `
  -Expected $guardPaths `
  -Label "Task 03 guard-blob map"
if ($boundary.schema -ne
      "openvino-cpu-observer-task03-boundary/v1" -or
    $boundary.roadmap_sha256 -ne $expectedRoadmapHash -or
    -not [StringComparer]::OrdinalIgnoreCase.Equals(
      [string]$boundary.controller_repo_root,
      $controllerRepoRoot) -or
    $boundary.clean_core_commit -ne $base -or
    $boundary.clean_core_tree -ne
      (& git -C $cleanCore rev-parse "$base^{tree}").Trim()) {
  throw "Task 03 boundary identity is invalid"
}
$cleanCoreStatus = @(
  & git -C $cleanCore status --porcelain --untracked-files=all
)
if ((& git -C $cleanCore rev-parse HEAD).Trim() -ne $base -or
    $cleanCoreStatus.Count -ne 0) {
  throw "Immutable clean-core commit/status changed"
}
$liveRoadmapHash = (
  Get-FileHash -LiteralPath $roadmap -Algorithm SHA256
).Hash.ToLowerInvariant()
$liveIdentityHash = (
  Get-FileHash -LiteralPath $identityPath -Algorithm SHA256
).Hash.ToLowerInvariant()
$liveTask02ReceiptHash = (
  Get-FileHash -LiteralPath $task02ReceiptPath -Algorithm SHA256
).Hash.ToLowerInvariant()
$liveTask02PlanHash = (
  Get-FileHash -LiteralPath $task02Plan -Algorithm SHA256
).Hash.ToLowerInvariant()
if ($liveRoadmapHash -ne $boundary.roadmap_sha256 -or
    $liveIdentityHash -ne
      $boundary.canonical_identity_sha256 -or
    $liveTask02ReceiptHash -ne
      $boundary.task02_receipt_sha256 -or
    $liveTask02PlanHash -ne
      $boundary.task02_plan_sha256) {
  throw "A frozen Task 03 prerequisite changed before evidence acceptance"
}
$liveTask02Receipt =
  Get-Content -LiteralPath $task02ReceiptPath -Raw |
  ConvertFrom-Json
if ($liveTask02Receipt -isnot [PSCustomObject] -or
    $liveTask02Receipt.task2_commit -isnot [string] -or
    $liveTask02Receipt.task2_tree -isnot [string] -or
    $liveTask02Receipt.task2_commit_subject -isnot
      [string] -or
    $liveTask02Receipt.changed_paths -isnot
      [System.Array] -or
    $liveTask02Receipt.guard_blobs -isnot
      [PSCustomObject] -or
    $liveTask02Receipt.task2_commit -ne
      $boundary.task02_commit -or
    $liveTask02Receipt.task2_tree -ne
      $boundary.task02_tree -or
    $liveTask02Receipt.task2_commit_subject -ne
      $boundary.task02_subject) {
  throw "Live Task 02 receipt no longer names the frozen preimage"
}
$liveTask02Subject =
  (& git -C $derivedCore show -s --format=%s `
      $boundary.task02_commit).Trim()
$liveTask02Parent =
  (& git -C $derivedCore rev-parse `
      "$($boundary.task02_commit)^").Trim()
$liveTask02Paths = @(
  & git -C $derivedCore diff --name-only `
    $base $boundary.task02_commit
) | Where-Object { $_ } | Sort-Object
if (@(
    $liveTask02Receipt.changed_paths |
      Where-Object { $_ -isnot [string] }
  ).Count -ne 0) {
  throw "Live Task 02 changed_paths contains a non-string"
}
if ($liveTask02Subject -ne $boundary.task02_subject -or
    $liveTask02Subject -ne
      "feat(cpu): capture physical variable-state allocations" -or
    $liveTask02Parent -ne $boundary.task02_parent -or
    $liveTask02Parent -ne $base -or
    (Compare-Object $expectedTask02Paths $liveTask02Paths)) {
  throw "Recomputed Task 02 subject/parent/path boundary changed"
}
Assert-ExactPropertySet `
  -Object $liveTask02Receipt.guard_blobs `
  -Expected $guardPaths `
  -Label "Task 02 accepted guard-blob map"
$guardCommit = (& git rev-parse HEAD).Trim()
foreach ($path in $guardPaths) {
  $expectedGuardValue =
    $boundary.guard_blobs.PSObject.Properties[$path].Value
  $receiptGuardValue =
    $liveTask02Receipt.guard_blobs.PSObject.Properties[
      $path
    ].Value
  if ($expectedGuardValue -isnot [string] -or
      $receiptGuardValue -isnot [string]) {
    throw "Guard blob map value is not an exact string: $path"
  }
  $expectedGuardBlob = [string]$expectedGuardValue
  $headGuardBlob = Get-ExactBlobOrAbsent `
    -Repository $sharedRepoRoot `
    -Commit $guardCommit `
    -Path $path
  $worktreeGuardBlob =
    (& git hash-object "--path=$path" -- $path).Trim()
  $receiptGuardBlob = [string]$receiptGuardValue
  if ($expectedGuardBlob -notmatch '^[0-9a-f]{40}$' -or
      $headGuardBlob -ne $expectedGuardBlob -or
      $worktreeGuardBlob -ne $expectedGuardBlob -or
      $receiptGuardBlob -ne $expectedGuardBlob) {
    throw "Frozen guard changed before evidence acceptance: $path"
  }
}
& git diff --quiet HEAD -- @guardPaths
if ($LASTEXITCODE -ne 0) {
  throw "Guard worktree differs from the reviewed committed blobs"
}
& git diff --cached --quiet HEAD -- @guardPaths
if ($LASTEXITCODE -ne 0) {
  throw "Guard index differs from the reviewed committed blobs"
}
$guardStatus = @(
  & git status --porcelain --untracked-files=all -- @guardPaths
)
if ($guardStatus.Count -ne 0) {
  throw "Guard paths are not clean at cumulative acceptance"
}
$progressText =
  Get-Content -LiteralPath $progressPath -Raw
if ($progressText -notmatch
      "(?im)Task\s*0?2.*SPEC PASS.*$escapedTask02Head" -or
    $progressText -notmatch
      "(?im)Task\s*0?2.*QUALITY PASS.*$escapedTask02Head") {
  throw "Frozen Task 02 review prerequisite is no longer recorded"
}
if ((& git -C $derivedCore rev-parse HEAD).Trim() -ne
      $boundary.task02_commit -or
    (& git -C $derivedCore rev-parse "HEAD^{tree}").Trim() -ne
      $boundary.task02_tree) {
  throw "Task 03 pre-commit HEAD moved away from Task 02"
}
foreach ($path in $task03Paths) {
  $expectedValue =
    $boundary.task03_preimage_blobs.PSObject.Properties[
      $path
    ].Value
  if ($expectedValue -isnot [string]) {
    throw "Task 03 preimage map value is not an exact string: $path"
  }
  $expected = [string]$expectedValue
  $actual = Get-ExactBlobOrAbsent `
    -Repository $derivedCore `
    -Commit $boundary.task02_commit `
    -Path $path
  if ($actual -ne $expected) {
    throw "Frozen Task 03 preimage changed for $path"
  }
}
$actualChanged = @(
  (& git -C $derivedCore diff --name-only) +
  (& git -C $derivedCore diff --cached --name-only) +
  (& git -C $derivedCore ls-files --others --exclude-standard)
) | Where-Object { $_ } | Sort-Object -Unique
$expectedChanged = @($task03Paths) | Sort-Object
if (Compare-Object $expectedChanged $actualChanged) {
  throw "Task 03 diff contains an unexpected or missing path"
}
& git -C $derivedCore diff --check
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 whitespace audit failed"
}
& git -C $derivedCore diff --cached --check
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 staged whitespace audit failed"
}
& git -C $derivedCore diff --stat
& git -C $derivedCore diff -- @task03Paths
```

Use this structural preprocessor audit rather than a single greedy regex:

```powershell
function Get-CpuConditionPossibility {
  param(
    [Parameter(Mandatory=$true)][string]$Kind,
    [Parameter(Mandatory=$true)][string]$Expression,
    [Parameter(Mandatory=$true)][bool]$CpuEnabled,
    [Parameter(Mandatory=$true)][string]$Location
  )
  $value = $null
  $token = $Expression.Trim()
  if ($Kind -eq "ifdef" -or $Kind -eq "ifndef") {
    if ($token -eq "CPU_DEBUG_CAPS") {
      $value = $CpuEnabled
      if ($Kind -eq "ifndef") {
        $value = -not $value
      }
    }
  } else {
    $compact = $token -replace '\s', ''
    if ($compact -in @(
        "defined(CPU_DEBUG_CAPS)",
        "definedCPU_DEBUG_CAPS",
        "CPU_DEBUG_CAPS"
      )) {
      $value = $CpuEnabled
    } elseif ($compact -in @(
        "!defined(CPU_DEBUG_CAPS)",
        "!definedCPU_DEBUG_CAPS",
        "!CPU_DEBUG_CAPS"
      )) {
      $value = -not $CpuEnabled
    }
  }
  if ($null -ne $value) {
    return [pscustomobject]@{
      CanBeTrue = [bool]$value
      CanBeFalse = -not [bool]$value
      CpuControlled = $true
    }
  }
  if ($token -match '\bCPU_DEBUG_CAPS\b') {
    throw "Unsupported compound CPU_DEBUG_CAPS condition at $Location"
  }
  return [pscustomobject]@{
    CanBeTrue = $true
    CanBeFalse = $true
    CpuControlled = $false
  }
}

function Assert-ObserverMarkersGuarded {
  param(
    [Parameter(Mandatory=$true)][string]$Path,
    [Parameter(Mandatory=$true)][string]$MarkerPattern
  )
  $states = @(
    [pscustomobject]@{
      CpuEnabled = $false
      CurrentMayBeActive = $true
      Frames = [System.Collections.ArrayList]::new()
    },
    [pscustomobject]@{
      CpuEnabled = $true
      CurrentMayBeActive = $true
      Frames = [System.Collections.ArrayList]::new()
    }
  )
  $markerCount = 0
  $lineNumber = 0
  foreach ($line in Get-Content -LiteralPath $Path) {
    ++$lineNumber
    $trimmed = $line.Trim()
    if ($trimmed -match
        '^#\s*(?<kind>if|ifdef|ifndef)\b(?<expression>.*)$') {
      foreach ($state in $states) {
        $condition = Get-CpuConditionPossibility `
          -Kind $Matches.kind `
          -Expression $Matches.expression `
          -CpuEnabled $state.CpuEnabled `
          -Location "${Path}:$lineNumber"
        $parent = [bool]$state.CurrentMayBeActive
        [void]$state.Frames.Add([pscustomobject]@{
          ParentMayBeActive = $parent
          RemainingMayBeActive =
            ($parent -and $condition.CanBeFalse)
          SawElse = $false
        })
        $state.CurrentMayBeActive =
          ($parent -and $condition.CanBeTrue)
      }
      continue
    }
    if ($trimmed -match
        '^#\s*elif\b(?<expression>.*)$') {
      foreach ($state in $states) {
        if ($state.Frames.Count -eq 0) {
          throw "Unbalanced #elif in ${Path}:$lineNumber"
        }
        $frame = $state.Frames[$state.Frames.Count - 1]
        if ($frame.SawElse) {
          throw "#elif follows #else in ${Path}:$lineNumber"
        }
        $condition = Get-CpuConditionPossibility `
          -Kind "elif" `
          -Expression $Matches.expression `
          -CpuEnabled $state.CpuEnabled `
          -Location "${Path}:$lineNumber"
        $state.CurrentMayBeActive =
          ($frame.RemainingMayBeActive -and
           $condition.CanBeTrue)
        $frame.RemainingMayBeActive =
          ($frame.RemainingMayBeActive -and
           $condition.CanBeFalse)
      }
      continue
    }
    if ($trimmed -match '^#\s*else(?:\s|$)') {
      foreach ($state in $states) {
        if ($state.Frames.Count -eq 0) {
          throw "Unbalanced #else in ${Path}:$lineNumber"
        }
        $frame = $state.Frames[$state.Frames.Count - 1]
        if ($frame.SawElse) {
          throw "Duplicate #else in ${Path}:$lineNumber"
        }
        $state.CurrentMayBeActive =
          [bool]$frame.RemainingMayBeActive
        $frame.RemainingMayBeActive = $false
        $frame.SawElse = $true
      }
      continue
    }
    if ($trimmed -match '^#\s*endif(?:\s|$)') {
      foreach ($state in $states) {
        if ($state.Frames.Count -eq 0) {
          throw "Unbalanced #endif in ${Path}:$lineNumber"
        }
        $frame = $state.Frames[$state.Frames.Count - 1]
        $state.Frames.RemoveAt($state.Frames.Count - 1)
        $state.CurrentMayBeActive =
          [bool]$frame.ParentMayBeActive
      }
      continue
    }
    if ($line -match $MarkerPattern) {
      ++$markerCount
      $disabledState = $states |
        Where-Object { -not $_.CpuEnabled }
      $enabledState = $states |
        Where-Object { $_.CpuEnabled }
      if ($disabledState.CurrentMayBeActive) {
        throw (
          "Observer marker may be active with CPU_DEBUG_CAPS disabled " +
          "at ${Path}:$lineNumber")
      }
      if (-not $enabledState.CurrentMayBeActive) {
        throw (
          "Observer marker is unreachable with CPU_DEBUG_CAPS enabled " +
          "at ${Path}:$lineNumber")
      }
    }
  }
  foreach ($state in $states) {
    if ($state.Frames.Count -ne 0) {
      throw "Unbalanced preprocessor nesting in $Path"
    }
  }
  return $markerCount
}

$markerPattern =
  'StateAllocationDumpConfig|StateAllocationPathKind|' +
  'StateAllocationPathInspector|StateAllocationOutputFactory|' +
  'StateAllocationObserverContext|StateAllocationRequestState|' +
  'StateAllocationInferenceAttempt|StateAllocationSnapshot|' +
  'ObservedAllocation|getAllocatedSize|stateAllocationDump|' +
  'state_allocation_observer_context|m_state_allocation_observer|' +
  'observer_attempt|append_state_allocation_snapshot|' +
  'append_serialized_state_allocation_snapshot|' +
  'validate_json_object_with_unique_keys|observer_phase|' +
  'next_observer_request_id|next_snapshot_sequence_locked|' +
  'try_take_nonwrapping_counter|' +
  'revalidate_state_allocation_dump_path|latest_infer_succeeded'
$guardedMarkerCount = 0
$containmentPaths = @(
  $task03Paths
  "src/plugins/intel_cpu/src/cpu_memory.h"
  "src/plugins/intel_cpu/src/cpu_memory.cpp"
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"
) | Sort-Object -Unique
foreach ($path in $containmentPaths) {
  $guardedMarkerCount += Assert-ObserverMarkersGuarded `
    -Path (Join-Path $derivedCore $path) `
    -MarkerPattern $markerPattern
}
if ($guardedMarkerCount -lt 40) {
  throw "Structural audit found too few guarded observer markers"
}

function Get-CpuDebugDisabledProjection {
  param(
    [Parameter(Mandatory=$true)][string]$Text,
    [Parameter(Mandatory=$true)][string]$Label
  )
  $builder = [Text.StringBuilder]::new()
  $frames = [System.Collections.ArrayList]::new()
  $emit = $true
  $lineNumber = 0
  $chunks = [regex]::Matches(
    $Text,
    '(?:[^\r\n]*(?:\r\n|\n|\r)|[^\r\n]+$)') |
      Where-Object { $_.Value.Length -ne 0 }
  foreach ($match in $chunks) {
    ++$lineNumber
    $chunk = $match.Value
    $line = $chunk -replace '(?:\r\n|\n|\r)$', ''
    $trimmed = $line.Trim()
    if ($trimmed -match
        '^#\s*(?<kind>if|ifdef|ifndef)\b(?<expression>.*)$') {
      $condition = Get-CpuConditionPossibility `
        -Kind $Matches.kind `
        -Expression $Matches.expression `
        -CpuEnabled $false `
        -Location "${Label}:$lineNumber"
      [void]$frames.Add([pscustomobject]@{
        ParentEmit = $emit
        CpuControlled = $condition.CpuControlled
        BranchTaken = (
          $condition.CpuControlled -and
          $condition.CanBeTrue)
        SawElse = $false
      })
      if ($condition.CpuControlled) {
        $emit = $emit -and $condition.CanBeTrue
      } elseif ($emit) {
        [void]$builder.Append($chunk)
      }
      continue
    }
    if ($trimmed -match
        '^#\s*elif\b(?<expression>.*)$') {
      if ($frames.Count -eq 0) {
        throw "Unbalanced #elif in ${Label}:$lineNumber"
      }
      $frame = $frames[$frames.Count - 1]
      if ($frame.SawElse) {
        throw "#elif follows #else in ${Label}:$lineNumber"
      }
      $condition = Get-CpuConditionPossibility `
        -Kind "elif" `
        -Expression $Matches.expression `
        -CpuEnabled $false `
        -Location "${Label}:$lineNumber"
      if ($frame.CpuControlled) {
        if (-not $condition.CpuControlled) {
          throw (
            "CPU-controlled chain has an unrelated #elif at " +
            "${Label}:$lineNumber")
        }
        $take = (-not $frame.BranchTaken) -and
          $condition.CanBeTrue
        $emit = $frame.ParentEmit -and $take
        $frame.BranchTaken =
          $frame.BranchTaken -or $take
      } else {
        if ($condition.CpuControlled) {
          throw (
            "Unrelated chain becomes CPU-controlled at " +
            "${Label}:$lineNumber")
        }
        $emit = $frame.ParentEmit
        if ($emit) {
          [void]$builder.Append($chunk)
        }
      }
      continue
    }
    if ($trimmed -match '^#\s*else(?:\s|$)') {
      if ($frames.Count -eq 0) {
        throw "Unbalanced #else in ${Label}:$lineNumber"
      }
      $frame = $frames[$frames.Count - 1]
      if ($frame.SawElse) {
        throw "Duplicate #else in ${Label}:$lineNumber"
      }
      $frame.SawElse = $true
      if ($frame.CpuControlled) {
        $take = -not $frame.BranchTaken
        $emit = $frame.ParentEmit -and $take
        $frame.BranchTaken = $true
      } else {
        $emit = $frame.ParentEmit
        if ($emit) {
          [void]$builder.Append($chunk)
        }
      }
      continue
    }
    if ($trimmed -match '^#\s*endif(?:\s|$)') {
      if ($frames.Count -eq 0) {
        throw "Unbalanced #endif in ${Label}:$lineNumber"
      }
      $frame = $frames[$frames.Count - 1]
      $frames.RemoveAt($frames.Count - 1)
      $emit = [bool]$frame.ParentEmit
      if (-not $frame.CpuControlled -and $emit) {
        [void]$builder.Append($chunk)
      }
      continue
    }
    if ($emit) {
      [void]$builder.Append($chunk)
    }
  }
  if ($frames.Count -ne 0) {
    throw "Unbalanced projection input: $Label"
  }
  return $builder.ToString()
}

function Read-ExactGitBlobText {
  param(
    [Parameter(Mandatory=$true)][string]$Repository,
    [Parameter(Mandatory=$true)][string]$Commit,
    [Parameter(Mandatory=$true)][string]$Path
  )
  $git = (Get-Command git -ErrorAction Stop).Source
  $startInfo = [Diagnostics.ProcessStartInfo]::new()
  $startInfo.FileName = $git
  $startInfo.Arguments =
    "-C `"$Repository`" cat-file blob `"$Commit`:$Path`""
  $startInfo.UseShellExecute = $false
  $startInfo.RedirectStandardOutput = $true
  $startInfo.RedirectStandardError = $true
  $process = [Diagnostics.Process]::new()
  $process.StartInfo = $startInfo
  if (-not $process.Start()) {
    throw "Unable to start git cat-file for $Path"
  }
  $errorTask = $process.StandardError.ReadToEndAsync()
  $bytes = [IO.MemoryStream]::new()
  $process.StandardOutput.BaseStream.CopyTo($bytes)
  $process.WaitForExit()
  $diagnostic = $errorTask.Result
  if ($process.ExitCode -ne 0 -or $diagnostic.Length -ne 0) {
    throw (
      "Unable to read exact Task 02 blob ${Path}: " +
      $diagnostic.Trim())
  }
  $strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
  return $strictUtf8.GetString($bytes.ToArray())
}

function Assert-ExactUtf8Bytes {
  param(
    [Parameter(Mandatory=$true)][string]$Expected,
    [Parameter(Mandatory=$true)][string]$Actual,
    [Parameter(Mandatory=$true)][string]$Label
  )
  $utf8 = [Text.UTF8Encoding]::new($false, $true)
  $expectedBytes = $utf8.GetBytes($Expected)
  $actualBytes = $utf8.GetBytes($Actual)
  if ($expectedBytes.Length -ne $actualBytes.Length) {
    throw "$Label ordinary projection length changed"
  }
  for ($index = 0; $index -lt $expectedBytes.Length; ++$index) {
    if ($expectedBytes[$index] -ne $actualBytes[$index]) {
      throw "$Label ordinary projection byte $index changed"
    }
  }
}

$ordinaryProductionPaths = @(
  $task03Paths |
    Where-Object {
      $_ -ne
        "src/plugins/intel_cpu/tests/unit/state_allocations_writer_test.cpp"
    }
)
foreach ($path in $ordinaryProductionPaths) {
  $task02Text = Read-ExactGitBlobText `
    -Repository $derivedCore `
    -Commit $task02Head `
    -Path $path
  $currentText = [IO.File]::ReadAllText(
    (Join-Path $derivedCore $path),
    [Text.UTF8Encoding]::new($false, $true))
  $task02Ordinary = Get-CpuDebugDisabledProjection `
    -Text $task02Text `
    -Label "Task 02 $path"
  $currentOrdinary = Get-CpuDebugDisabledProjection `
    -Text $currentText `
    -Label "Task 03 $path"
  Assert-ExactUtf8Bytes `
    -Expected $task02Ordinary `
    -Actual $currentOrdinary `
    -Label $path
}
```

The audit computes complete may-active parent truth for both macro values
through `if`/`ifdef`/`ifndef`/`elif`/`else`, conservatively treats unrelated
conditions as unknown, and rejects unsupported compound uses of the CPU macro.
It therefore rejects any observer marker that can be active with the macro
off. The second audit removes exact positive CPU branches from both the frozen
Task 02 blobs and current sources, preserves original line endings, and
compares strict UTF-8 bytes. This independently proves the ordinary CPU source
projection—including includes, delegation locals, insertion, and return
formatting—is unchanged.

Now prove that the guarded markers occur in the real production control-flow
sites and in the required order. This is deliberately positional rather than a
greedy regex:

```powershell
function Get-RequiredMarkerIndex {
  param(
    [Parameter(Mandatory=$true)][string]$Text,
    [Parameter(Mandatory=$true)][string]$Marker,
    [Parameter(Mandatory=$true)][int]$Start,
    [Parameter(Mandatory=$true)][int]$End,
    [Parameter(Mandatory=$true)][string]$Label
  )
  $index = $Text.IndexOf(
    $Marker,
    $Start,
    [System.StringComparison]::Ordinal)
  if ($index -lt $Start -or $index -ge $End) {
    throw "Missing or misplaced $Label marker: $Marker"
  }
  return $index
}

$inferRequestPath = Join-Path $derivedCore `
  "src/plugins/intel_cpu/src/infer_request.cpp"
$inferRequestHeaderPath = Join-Path $derivedCore `
  "src/plugins/intel_cpu/src/infer_request.h"
$inferText = Get-Content -LiteralPath $inferRequestPath -Raw
$inferHeaderText =
  Get-Content -LiteralPath $inferRequestHeaderPath -Raw

$constructorStart = $inferText.IndexOf(
  "SyncInferRequest::SyncInferRequest(CompiledModelHolder compiled_model)",
  [System.StringComparison]::Ordinal)
$constructorEnd = $inferText.IndexOf(
  "void SyncInferRequest::create_infer_request()",
  $constructorStart,
  [System.StringComparison]::Ordinal)
if ($constructorStart -lt 0 -or
    $constructorEnd -le $constructorStart) {
  throw "Unable to isolate SyncInferRequest constructor"
}
$constructorText =
  $inferText.Substring(
    $constructorStart,
    $constructorEnd - $constructorStart)
if ($constructorText -notmatch
      '(?m)^\s*m_compiled_model\(std::move\(compiled_model\)\) \{\s*$' -or
    $constructorText -match (
      'm_state_allocation_observer|CompiledModelHolder::id|' +
      'm_compiled_model\.id\s*\(')) {
  throw "Ordinary SyncInferRequest constructor text was changed"
}
$headerObserverMembers = [regex]::Matches(
  $inferHeaderText,
  '(?m)^\s*StateAllocationRequestState\s+' +
    'm_state_allocation_observer\s*\{')
if ($headerObserverMembers.Count -ne 1) {
  throw "SyncInferRequest must own exactly one observer request state member"
}
$compiledModelMemberIndex = $inferHeaderText.IndexOf(
  "CompiledModelHolder m_compiled_model;",
  [StringComparison]::Ordinal)
$observerMemberIndex = $inferHeaderText.IndexOf(
  "StateAllocationRequestState m_state_allocation_observer{",
  [StringComparison]::Ordinal)
$observerMemberEnd = $inferHeaderText.IndexOf(
  ".stateAllocationDump};",
  $observerMemberIndex,
  [StringComparison]::Ordinal)
if ($compiledModelMemberIndex -lt 0 -or
    $observerMemberIndex -le $compiledModelMemberIndex -or
    $observerMemberEnd -le $observerMemberIndex -or
    $inferHeaderText -match
      'CompiledModelHolder::id|m_compiled_model\.id\s*\(') {
  throw "Observer request-state member initialization is invalid"
}

$inferStart = $inferText.IndexOf(
  "void SyncInferRequest::infer()",
  [System.StringComparison]::Ordinal)
$inferEnd = $inferText.IndexOf(
  "SyncInferRequest::get_profiling_info() const",
  $inferStart,
  [System.StringComparison]::Ordinal)
if ($inferStart -lt 0 -or $inferEnd -le $inferStart) {
  throw "Unable to isolate SyncInferRequest::infer()"
}
$attemptIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "StateAllocationInferenceAttempt observer_attempt" `
  -Start $inferStart `
  -End $inferEnd `
  -Label "begin-attempt"
$ittIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "OV_ITT_SCOPED_TASK_BASE" `
  -Start $inferStart `
  -End $inferEnd `
  -Label "infer ITT"
$graphLockIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "auto graphLock" `
  -Start $inferStart `
  -End $inferEnd `
  -Label "graph lock"
$graphInferIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "graph.Infer(this);" `
  -Start $inferStart `
  -End $inferEnd `
  -Label "graph infer"
$pullOutputIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "graph.PullOutputData(m_outputs);" `
  -Start $inferStart `
  -End $inferEnd `
  -Label "output pull"
$completeIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "observer_attempt.complete();" `
  -Start $inferStart `
  -End $inferEnd `
  -Label "successful completion"
if (-not (
    $inferStart -lt $attemptIndex -and
    $attemptIndex -lt $ittIndex -and
    $ittIndex -lt $graphLockIndex -and
    $graphLockIndex -lt $graphInferIndex -and
    $graphInferIndex -lt $pullOutputIndex -and
    $pullOutputIndex -lt $completeIndex -and
    $completeIndex -lt $inferEnd)) {
  throw "Inference observer lifecycle markers are out of order"
}

$queryStart = $inferText.IndexOf(
  "SyncInferRequest::query_state() const",
  [System.StringComparison]::Ordinal)
$queryEnd = $inferText.IndexOf(
  "void SyncInferRequest::set_async_request",
  $queryStart,
  [System.StringComparison]::Ordinal)
if ($queryStart -lt 0 -or $queryEnd -le $queryStart) {
  throw "Unable to isolate SyncInferRequest::query_state()"
}
$delegateBranchIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "if (m_asyncRequest->m_has_sub_infers)" `
  -Start $queryStart `
  -End $queryEnd `
  -Label "unchanged delegating branch"
$curIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "auto cur = request->query_state();" `
  -Start $delegateBranchIndex `
  -End $queryEnd `
  -Label "unchanged delegation local"
$insertIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "states.insert(states.end(), cur.begin(), cur.end());" `
  -Start $curIndex `
  -End $queryEnd `
  -Label "unchanged delegation insertion"
$delegateReturnIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "return states;" `
  -Start $insertIndex `
  -End $queryEnd `
  -Label "delegating return"
$guardOpenIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "#ifdef CPU_DEBUG_CAPS" `
  -Start $delegateReturnIndex `
  -End $queryEnd `
  -Label "leaf observer guard open"
$decisionIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "m_state_allocation_observer.should_emit(" `
  -Start $guardOpenIndex `
  -End $queryEnd `
  -Label "leaf emission decision"
$appendIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "append_state_allocation_snapshot(" `
  -Start $decisionIndex `
  -End $queryEnd `
  -Label "leaf append"
$guardCloseIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "#endif" `
  -Start $appendIndex `
  -End $queryEnd `
  -Label "leaf observer guard close"
$wrapperReturnIndex = Get-RequiredMarkerIndex `
  -Text $inferText `
  -Marker "return {m_memory_states.begin(), m_memory_states.end()};" `
  -Start $guardCloseIndex `
  -End $queryEnd `
  -Label "unchanged one-line leaf wrapper return"
$queryText = $inferText.Substring(
  $queryStart,
  $queryEnd - $queryStart)
if ($queryText.Contains("delegates_to_subrequests") -or
    $queryText.Contains("auto current =") -or
    [regex]::Matches(
      $queryText,
      'append_state_allocation_snapshot\s*\('
    ).Count -ne 1) {
  throw "Query-state ordinary branch was refactored or append is not unique"
}
if (-not (
    $queryStart -lt $delegateBranchIndex -and
    $delegateBranchIndex -lt $curIndex -and
    $curIndex -lt $insertIndex -and
    $insertIndex -lt $delegateReturnIndex -and
    $delegateReturnIndex -lt $guardOpenIndex -and
    $guardOpenIndex -lt $decisionIndex -and
    $decisionIndex -lt $appendIndex -and
    $appendIndex -lt $guardCloseIndex -and
    $guardCloseIndex -lt $wrapperReturnIndex -and
    $wrapperReturnIndex -lt $queryEnd)) {
  throw "Query-state delegation/leaf markers are out of order"
}

$compiledModelPath = Join-Path $derivedCore `
  "src/plugins/intel_cpu/src/compiled_model.cpp"
$compiledModelText =
  Get-Content -LiteralPath $compiledModelPath -Raw
$compiledConstructorStart = $compiledModelText.IndexOf(
  "CompiledModel::CompiledModel(",
  [System.StringComparison]::Ordinal)
$compiledConstructorEnd = $compiledModelText.IndexOf(
  "CompiledModel::GraphGuard::Lock CompiledModel::get_graph() const",
  $compiledConstructorStart,
  [System.StringComparison]::Ordinal)
if ($compiledConstructorStart -lt 0 -or
    $compiledConstructorEnd -le $compiledConstructorStart) {
  throw "Unable to isolate CompiledModel constructor"
}
$contextInitializer = Get-RequiredMarkerIndex `
  -Text $compiledModelText `
  -Marker "m_state_allocation_observer_context{" `
  -Start $compiledConstructorStart `
  -End $compiledConstructorEnd `
  -Label "compiled-model observer context"
$enabledCheck = Get-RequiredMarkerIndex `
  -Text $compiledModelText `
  -Marker "stateAllocationDump.enabled()" `
  -Start $contextInitializer `
  -End $compiledConstructorEnd `
  -Label "observer enabled check"
$contextParse = Get-RequiredMarkerIndex `
  -Text $compiledModelText `
  -Marker "parse_state_allocation_observer_context(" `
  -Start $enabledCheck `
  -End $compiledConstructorEnd `
  -Label "observer metadata parse"
$subMemoryInitializer = Get-RequiredMarkerIndex `
  -Text $compiledModelText `
  -Marker "m_sub_memory_manager(std::move(sub_memory_manager))" `
  -Start $contextParse `
  -End $compiledConstructorEnd `
  -Label "following compiled-model initializer"
if (-not (
    $compiledConstructorStart -lt $contextInitializer -and
    $contextInitializer -lt $enabledCheck -and
    $enabledCheck -lt $contextParse -and
    $contextParse -lt $subMemoryInitializer -and
    $subMemoryInitializer -lt $compiledConstructorEnd)) {
  throw "Compiled-model observer context markers are out of order"
}

$dumpSourcePath = Join-Path $derivedCore `
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp"
$dumpSourceText =
  Get-Content -LiteralPath $dumpSourcePath -Raw
function Get-RequiredSourceSlice {
  param(
    [Parameter(Mandatory=$true)][string]$Text,
    [Parameter(Mandatory=$true)][string]$StartMarker,
    [Parameter(Mandatory=$true)][string]$EndMarker,
    [Parameter(Mandatory=$true)][string]$Label
  )
  $start = $Text.IndexOf(
    $StartMarker,
    [StringComparison]::Ordinal)
  $end = $Text.IndexOf(
    $EndMarker,
    $start + $StartMarker.Length,
    [StringComparison]::Ordinal)
  if ($start -lt 0 -or $end -le $start) {
    throw "Unable to isolate $Label"
  }
  return $Text.Substring($start, $end - $start)
}

$counterSlice = Get-RequiredSourceSlice `
  -Text $dumpSourceText `
  -StartMarker "bool try_take_nonwrapping_counter(" `
  -EndMarker "uint64_t next_observer_request_id()" `
  -Label "non-wrapping CAS helper"
if ($counterSlice -notmatch
      'compare_exchange_weak\s*\(' -or
    $counterSlice -notmatch
      'current\s*!=\s*0' -or
    $counterSlice -notmatch
      'current\s*!=\s*std::numeric_limits<uint64_t>::max\(\)' -or
    $counterSlice -match 'fetch_add') {
  throw "Non-wrapping CAS helper structure is invalid"
}
foreach ($counterContract in @(
  [pscustomobject]@{
    Name = "snapshot sequence"
    Start = "uint64_t next_snapshot_sequence_locked()"
    End = "void validate_serialized_json_line("
  },
  [pscustomobject]@{
    Name = "observer request ID"
    Start = "uint64_t next_observer_request_id()"
    End =
      "StateAllocationRequestState::StateAllocationRequestState("
  }
)) {
  $slice = Get-RequiredSourceSlice `
    -Text $dumpSourceText `
    -StartMarker $counterContract.Start `
    -EndMarker $counterContract.End `
    -Label $counterContract.Name
  if ([regex]::Matches(
        $slice,
        'try_take_nonwrapping_counter\s*\('
      ).Count -ne 1 -or
      $slice -notmatch 'std::terminate\s*\(\s*\)' -or
      $slice -match 'fetch_add') {
    throw "$($counterContract.Name) does not use the exact saturating CAS gate"
  }
}

$existingJsonlSlice = Get-RequiredSourceSlice `
  -Text $dumpSourceText `
  -StartMarker "void validate_existing_jsonl_unlocked(" `
  -EndMarker "std::unique_ptr<std::ostream> open_append_stream(" `
  -Label "existing JSONL validation"
$appendSlice = Get-RequiredSourceSlice `
  -Text $dumpSourceText `
  -StartMarker "void append_json_line_unlocked(" `
  -EndMarker "const ov::AnyMap& require_nested_map(" `
  -Label "JSONL append/open sequence"
if ([regex]::Matches(
      $existingJsonlSlice,
      'revalidate_state_allocation_dump_path\s*\('
    ).Count -ne 1) {
  throw "Existing JSONL validation lacks its path revalidation"
}
$existingValidationIndex = $appendSlice.IndexOf(
  "validate_existing_jsonl_unlocked(config);",
  [StringComparison]::Ordinal)
$openRevalidationIndex = $appendSlice.IndexOf(
  "revalidate_state_allocation_dump_path(config.path);",
  [StringComparison]::Ordinal)
$factoryIndex = $appendSlice.IndexOf(
  "output_factory(config.path)",
  [StringComparison]::Ordinal)
$writeIndex = $appendSlice.IndexOf(
  "output->write(",
  [StringComparison]::Ordinal)
$newlineIndex = $appendSlice.IndexOf(
  "line.push_back('\n');",
  [StringComparison]::Ordinal)
$flushIndex = $appendSlice.IndexOf(
  "output->flush();",
  [StringComparison]::Ordinal)
if (-not (
    0 -le $existingValidationIndex -and
    $existingValidationIndex -lt $openRevalidationIndex -and
    $openRevalidationIndex -lt $factoryIndex -and
    $factoryIndex -lt $newlineIndex -and
    $newlineIndex -lt $writeIndex -and
    $writeIndex -lt $flushIndex)) {
  throw "JSONL validation/revalidation/open/write/flush order is invalid"
}
```

The test across three `StateAllocationRequestState` lifetimes proves that the
process-static allocator does not reuse IDs; the exact constructor/member
audit above proves every enabled real `SyncInferRequest` owns that
allocator-backed state instead of using the reusable compiled-model holder
ID. The emission-decision helper test proves only that helper, while the exact
branch/return/append audit proves the production aggregate path returns before
the leaf writer and that a leaf appends before constructing public wrappers.
The RAII failure/cancellation tests and the exact `infer()` audit together
prove the tested attempt object is placed before graph execution and is
completed only after output pull.

Now audit only added production lines for forbidden truth and API surfaces:

```powershell
$productionPaths = $task03Paths |
  Where-Object {
    $_ -ne
      "src/plugins/intel_cpu/tests/unit/state_allocations_writer_test.cpp"
  }
$addedProductionLines = @(
  & git -C $derivedCore diff -U0 $task02Head -- @productionPaths |
    Where-Object {
      $_ -match '^\+' -and $_ -notmatch '^\+\+\+'
    }
) -join "`n"
foreach ($forbidden in @(
  'requested_device',
  'actual_devices',
  'actual_execution_devices',
  'tensor_contents',
  'raw_address',
  'OPENVINO_API',
  'OPENVINO_RUNTIME_API',
  'OPENVINO_C_API',
  'supported_properties'
)) {
  if ($addedProductionLines -match [regex]::Escape($forbidden)) {
    throw "Forbidden Task 03 production addition: $forbidden"
  }
}
if ($addedProductionLines -match
    'reinterpret_cast.*(?:long|int|size_t)|uintptr_t') {
  throw "Task 03 added a pointer-to-integer/address conversion"
}
$observerEnvOccurrences = (
  [regex]::Matches(
    $addedProductionLines,
    'OV_CPU_STATE_ALLOCATION_DUMP_PATH')
).Count
if ($observerEnvOccurrences -ne 1) {
  throw "Production observer environment name must be added exactly once"
}
if ($addedProductionLines -match
    'OV_CPU_[A-Z0-9_]*(?:DEVICE|PHASE|REQUEST)') {
  throw "Task 03 added forbidden environment-derived observer truth"
}

& rg -n 'get_state\s*\(' `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp"
$getStateExit = $LASTEXITCODE
if ($getStateExit -eq 0) {
  throw "Observer writer/capture materializes a public state tensor"
}
if ($getStateExit -ne 1) {
  throw "Public state materialization scan failed"
}
& rg -n 'TQDBG|raw_address|uintptr_t' `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp" `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp"
$addressExit = $LASTEXITCODE
if ($addressExit -eq 0) {
  throw "Observer module contains a forbidden marker/address field"
}
if ($addressExit -ne 1) {
  throw "Observer forbidden-marker scan failed"
}
```

Audit every guarded command:

> **PENDING FINAL GUARD CONTRACT FREEZE:** Do not execute this audit or
> continue to Step 11 until the final committed wrapper/controller contract
> has passed its independent exact-JSON-type and post-run substitution-binding
> reviews, Task 02 has recorded those exact committed guard blobs, and this
> block has been updated to validate the accepted `run_id` plus persisted
> wrapper-verification/controller-binding evidence. The block below captures
> the non-binding resource/argv baseline only; it is intentionally not yet an
> acceptance gate.

```powershell
Assert-Task03ControllerPolicy
$guardContracts = [ordered]@{
  "task3-configure-red" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @($configureCommand)
  }
  "task3-build-red" = [pscustomobject]@{
    ExpectedExit = "nonzero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @($buildUnitCommand)
  }
  "task3-configure-green" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @($configureCommand)
  }
  "task3-build-unit-green" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @($buildUnitCommand)
  }
  "task3-list-green" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @(
      $unitExecutable,
      "--gtest_filter=$focusedFilter",
      "--gtest_list_tests"
    )
  }
  "task3-writer-green-1" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @(
      $unitExecutable,
      "--gtest_filter=$focusedFilter"
    )
  }
  "task3-writer-green-2" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @(
      $unitExecutable,
      "--gtest_filter=$focusedFilter"
    )
  }
  "task3-plugin-green" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @($buildPluginCommand)
  }
  "task3-configure-functional" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @($configureFunctionalCommand)
  }
  "task3-build-functional" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @($buildFunctionalCommand)
  }
  "task3-list-stock" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @(
      $functionalExecutable,
      "--gtest_filter=$stockFilter",
      "--gtest_list_tests"
    )
  }
  "task3-stock-unset" = [pscustomobject]@{
    ExpectedExit = "zero"
    WorkingDirectory = $stockUnsetCwd
    TimeoutSeconds = 7200.0
    Command = @(
      $functionalExecutable,
      "--gtest_filter=$stockFilter"
    )
  }
  "task3-stock-invalid" = [pscustomobject]@{
    ExpectedExit = "nonzero"
    WorkingDirectory = "R:\"
    TimeoutSeconds = 7200.0
    Command = @(
      $functionalExecutable,
      "--gtest_filter=$stockFilter"
    )
  }
}
if ($guardContracts.Count -ne 13) {
  throw "Task 03 guard contract set is not exactly 13 labels"
}
function Convert-Task03RequestedPathToPhysical {
  param(
    [Parameter(Mandatory=$true)][string]$Path
  )
  $requested = [IO.Path]::GetFullPath($Path)
  $requestedRoot = [IO.Path]::GetPathRoot($requested)
  if (-not [StringComparer]::OrdinalIgnoreCase.Equals(
      $requestedRoot,
      "R:\")) {
    throw "Task 03 guard request escaped the frozen R: root: $Path"
  }
  $relative = $requested.Substring($requestedRoot.Length)
  if ($relative.Length -eq 0) {
    return (
      [IO.Path]::GetFullPath(
        $controllerRepoRoot)
    ).TrimEnd('\')
  }
  return [IO.Path]::GetFullPath(
    (Join-Path $controllerRepoRoot $relative))
}

foreach ($entry in $guardContracts.GetEnumerator()) {
  $contract = $entry.Value
  $receiptPath =
    Join-Path $guardEvidence ($entry.Key + ".json")
  $logPath =
    Join-Path $guardEvidence ($entry.Key + ".log")
  if (-not (Test-Path -LiteralPath $receiptPath) -or
      -not (Test-Path -LiteralPath $logPath)) {
    throw "Missing guard evidence: $($entry.Key)"
  }
  $record =
    Get-Content -LiteralPath $receiptPath -Raw |
    ConvertFrom-Json
  $requestedWorkingDirectory =
    (Resolve-Path -LiteralPath $contract.WorkingDirectory).Path
  $requestedLogPath = [IO.Path]::GetFullPath($logPath)
  $requestedEvidencePath =
    [IO.Path]::GetFullPath($receiptPath)
  $canonicalWorkingDirectory =
    Convert-Task03RequestedPathToPhysical `
      -Path $requestedWorkingDirectory
  $canonicalLogPath =
    Convert-Task03RequestedPathToPhysical `
      -Path $requestedLogPath
  $canonicalEvidencePath =
    Convert-Task03RequestedPathToPhysical `
      -Path $requestedEvidencePath
  $recordCommand = @($record.command)
  $expectedCommand = @($contract.Command)
  if ($recordCommand.Count -ne $expectedCommand.Count) {
    throw "Guard argv count mismatch: $($entry.Key)"
  }
  for ($index = 0;
       $index -lt $expectedCommand.Count;
       ++$index) {
    if (-not [StringComparer]::Ordinal.Equals(
        [string]$recordCommand[$index],
        [string]$expectedCommand[$index])) {
      throw (
        "Guard argv mismatch at $index for " +
        $entry.Key)
    }
  }
  foreach ($pathPair in @(
    [pscustomobject]@{
      Name = "canonical working directory"
      Actual = [string]$record.working_directory
      Expected = $canonicalWorkingDirectory
    },
    [pscustomobject]@{
      Name = "canonical log path"
      Actual = [string]$record.log_path
      Expected = $canonicalLogPath
    },
    [pscustomobject]@{
      Name = "canonical evidence path"
      Actual = [string]$record.evidence_path
      Expected = $canonicalEvidencePath
    },
    [pscustomobject]@{
      Name = "requested working directory"
      Actual = [string](
        $record.requested_path_provenance.working_directory)
      Expected = $requestedWorkingDirectory
    },
    [pscustomobject]@{
      Name = "requested log path"
      Actual = [string](
        $record.requested_path_provenance.log_path)
      Expected = $requestedLogPath
    },
    [pscustomobject]@{
      Name = "requested evidence path"
      Actual = [string](
        $record.requested_path_provenance.evidence_path)
      Expected = $requestedEvidencePath
    }
  )) {
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals(
        $pathPair.Actual,
        $pathPair.Expected)) {
      throw "$($pathPair.Name) mismatch: $($entry.Key)"
    }
  }
  $ramBefore =
    $record.observed_available_ram_bytes.before
  $ramMinimum =
    $record.observed_available_ram_bytes.minimum
  $ramAfter =
    $record.observed_available_ram_bytes.after
  if ($record.schema -ne
        "official-openvino-owned-process-guard/v1" -or
      $record.expected_exit -ne $contract.ExpectedExit -or
      $null -eq $record.exit_code -or
      [double]$record.maximum_runtime_seconds -ne
        [double]$contract.TimeoutSeconds -or
      $record.valid -ne $true -or
      [int64]$record.configured_minimum_available_ram_bytes -ne
        2147483648 -or
      $null -eq $ramBefore -or
      $null -eq $ramMinimum -or
      $null -eq $ramAfter -or
      [int64]$ramMinimum -lt
        2147483648 -or
      [int64]$ramMinimum -gt [int64]$ramBefore -or
      [int64]$ramMinimum -gt [int64]$ramAfter -or
      [int]$record.memory_sample_count -lt 1 -or
      [int64]$record.peak_working_set_bytes -le 0 -or
      [int64]$record.peak_private_bytes -le 0 -or
      $record.launch_governance.created_suspended -ne $true -or
      $record.launch_governance.assigned_before_resume -ne $true -or
      $null -ne
        $record.launch_governance.cpu_affinity_mask -or
      $null -ne
        $record.launch_governance.cpu_rate_hard_cap_percent -or
      $record.msbuild_disable_node_reuse -ne "1" -or
      $record.path_identity_verified.working_directory -isnot
        [bool] -or
      $record.path_identity_verified.working_directory -ne
        $true -or
      $record.path_identity_verified.log_path -isnot
        [bool] -or
      $record.path_identity_verified.log_path -ne $true -or
      $record.path_identity_verified.evidence_path -isnot
        [bool] -or
      $record.path_identity_verified.evidence_path -ne
        $true -or
      $record.job_object.setup_ok -ne $true -or
      $record.job_object.query_ok -ne $true -or
      [int]$record.job_object.queried_active_process_count_after_cleanup -ne
        0 -or
      @($record.job_object.survivor_pids_after_cleanup).Count -ne 0 -or
      $record.timed_out -ne $false -or
      $record.low_memory_stop -ne $false -or
      $null -ne $record.termination_reason -or
      @($record.emergency_actions).Count -ne 0 -or
      @($record.validation_errors).Count -ne 0) {
    throw "Guard receipt failed RAM/zero-survivor validation: $($entry.Key)"
  }
  if ($contract.ExpectedExit -eq "nonzero") {
    if ([int]$record.exit_code -eq 0 -or
        $record.expected_exit -ne "nonzero") {
      throw "Expected guarded nonzero is invalid: $($entry.Key)"
    }
  } elseif ([int]$record.exit_code -ne 0 -or
            $record.expected_exit -ne "zero") {
    throw "Expected guarded zero is invalid: $($entry.Key)"
  }
  $observedLogHash = (
    Get-FileHash -LiteralPath $logPath -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  if ($record.log_sha256 -ne $observedLogHash) {
    throw "Guard log hash mismatch: $($entry.Key)"
  }
}
Assert-Task03ControllerPolicy
```

Record this exact deferral in `.superpowers/sdd/progress.md`:

```text
Task 03 ordinary-route status: STRUCTURAL PASS only.
Compiled both-options-OFF DLL marker absence, dumpbin symbol absence, and
ordinary stock-query equivalence remain mandatory Task 07 gates and were not
claimed or substituted during Task 03.
```

Do not build an ad hoc ordinary route here. The roadmap requires Task 07 to
perform that proof from the final replayed patch tree, which is stronger than
an intermediate Task 03 binary.

## Step 11: Commit the exact Task 03 boundary

- [ ] Stage only the nine approved paths.
- [ ] Re-run cached whitespace/path checks.
- [ ] Commit with the exact subject.
- [ ] Require a clean derived core and full commit/tree identities.

```powershell
& git -C $derivedCore add `
  src/plugins/intel_cpu/src/compiled_model.h `
  src/plugins/intel_cpu/src/compiled_model.cpp `
  src/plugins/intel_cpu/src/utils/debug_caps_config.h `
  src/plugins/intel_cpu/src/utils/debug_caps_config.cpp `
  src/plugins/intel_cpu/src/infer_request.h `
  src/plugins/intel_cpu/src/infer_request.cpp `
  src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp `
  src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp `
  src/plugins/intel_cpu/tests/unit/state_allocations_writer_test.cpp
if ($LASTEXITCODE -ne 0) {
  throw "Unable to stage Task 03 paths"
}

$staged = @(
  & git -C $derivedCore diff --cached --name-only
) | Where-Object { $_ } | Sort-Object
if (Compare-Object $expectedChanged $staged) {
  throw "Staged Task 03 path set is not exact"
}
& git -C $derivedCore diff --cached --check
if ($LASTEXITCODE -ne 0) {
  throw "Staged Task 03 whitespace audit failed"
}
& git -C $derivedCore diff --cached --stat
& git -C $derivedCore diff --cached -- @task03Paths

& git -C $derivedCore commit -m `
  "feat(cpu): emit opt-in state allocation snapshots"
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 commit failed"
}
$task03Head = (& git -C $derivedCore rev-parse HEAD).Trim()
$task03Tree =
  (& git -C $derivedCore rev-parse "HEAD^{tree}").Trim()
if ($task03Head -notmatch '^[0-9a-f]{40}$' -or
    $task03Tree -notmatch '^[0-9a-f]{40}$') {
  throw "Task 03 commit/tree identity is not full length"
}
if ((& git -C $derivedCore show -s --format=%s $task03Head).Trim() -ne
    "feat(cpu): emit opt-in state allocation snapshots") {
  throw "Task 03 commit subject is not exact"
}
if ((& git -C $derivedCore rev-parse "$task03Head^").Trim() -ne
    $task02Head) {
  throw "Task 03 is not one focused child of accepted Task 02"
}
if (& git -C $derivedCore status --porcelain --untracked-files=all) {
  throw "Derived core is not clean after Task 03 commit"
}
```

Do not push this private derived-core commit or update the materialization
identity. Task 07 owns publication as one replayed parent patch.

## Step 12: Prove delta and cumulative replay identity, then write the receipt

- [ ] Export ignored scratch delta and cumulative binary-safe patches.
- [ ] Replay Task 03 delta onto Task 02 and cumulative work onto the immutable
      base in no-hardlink scratch clones.
- [ ] Require both replay trees to equal the Task 03 tree.
- [ ] Write and re-read one exact Task 03 receipt.

```powershell
$task03DeltaPatch =
  "R:\.superpowers\sdd\task03-delta-preview.patch"
$task03CumulativePatch =
  "R:\.superpowers\sdd\task03-cumulative-preview.patch"
& git -C $derivedCore diff --binary $task02Head $task03Head `
  --output=$task03DeltaPatch
if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path -LiteralPath $task03DeltaPatch) -or
    (Get-Item -LiteralPath $task03DeltaPatch).Length -eq 0) {
  throw "Task 03 delta preview patch is empty"
}
& git -C $derivedCore diff --binary $base $task03Head `
  --output=$task03CumulativePatch
if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path -LiteralPath $task03CumulativePatch) -or
    (Get-Item -LiteralPath $task03CumulativePatch).Length -eq 0) {
  throw "Task 03 cumulative preview patch is empty"
}

function Assert-NormalDirectoryChain {
  param(
    [Parameter(Mandatory=$true)][string]$Path
  )
  $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
  $root = [IO.Path]::GetPathRoot($full)
  $rootItem = Get-Item -LiteralPath $root -Force
  if (-not $rootItem.PSIsContainer -or
      ($rootItem.Attributes -band
        [IO.FileAttributes]::ReparsePoint)) {
    throw "Scratch root is not one normal directory: $root"
  }
  $relative = $full.Substring($root.Length)
  $current = $root
  foreach ($component in @(
      $relative -split '[\\/]'
    ) | Where-Object { $_ }) {
    $current = Join-Path $current $component
    $item = Get-Item -LiteralPath $current -Force
    if (-not $item.PSIsContainer -or
        ($item.Attributes -band
          [IO.FileAttributes]::ReparsePoint)) {
      throw "Scratch path component is not a normal directory: $current"
    }
  }
}

$approvedScratchParent =
  [IO.Path]::GetFullPath(
    (Join-Path $controllerRepoRoot ".superpowers\sdd")
  ).TrimEnd('\')
Assert-NormalDirectoryChain -Path $approvedScratchParent
$resolvedScratchParent =
  (Resolve-Path -LiteralPath $approvedScratchParent).Path.TrimEnd('\')
if (-not [StringComparer]::OrdinalIgnoreCase.Equals(
    $resolvedScratchParent,
    $approvedScratchParent)) {
  throw "Approved scratch parent resolves elsewhere"
}

function Get-Task03ScratchOwnerText {
  param(
    [Parameter(Mandatory=$true)][string]$Leaf
  )
  return (
    "openvino-task03-replay-owner/v1`n" +
    "leaf=$Leaf`n" +
    "base=$base")
}

function Get-ExactTask03ScratchPath {
  param(
    [Parameter(Mandatory=$true)]
    [ValidateSet(
      "task03-delta-replay",
      "task03-cumulative-replay"
    )]
    [string]$Leaf
  )
  if ($Leaf -cnotin @(
      "task03-delta-replay",
      "task03-cumulative-replay"
    )) {
    throw "Scratch leaf spelling/case is not exact: $Leaf"
  }
  $candidate =
    [IO.Path]::GetFullPath(
      (Join-Path $approvedScratchParent $Leaf)
    ).TrimEnd('\')
  $expectedPrefix = $approvedScratchParent + "\"
  if (-not $candidate.StartsWith(
      $expectedPrefix,
      [StringComparison]::OrdinalIgnoreCase) -or
      -not [StringComparer]::OrdinalIgnoreCase.Equals(
        [IO.Path]::GetDirectoryName($candidate),
        $approvedScratchParent) -or
      -not [StringComparer]::Ordinal.Equals(
        [IO.Path]::GetFileName($candidate),
        $Leaf)) {
    throw "Scratch parent/leaf proof failed: $candidate"
  }
  return $candidate
}

function Remove-Task03OwnedScratchDirectory {
  param(
    [Parameter(Mandatory=$true)]
    [ValidateSet(
      "task03-delta-replay",
      "task03-cumulative-replay"
    )]
    [string]$Leaf
  )
  Assert-NormalDirectoryChain -Path $approvedScratchParent
  $candidate = Get-ExactTask03ScratchPath -Leaf $Leaf
  if (-not (Test-Path -LiteralPath $candidate)) {
    return
  }
  $target = Get-Item -LiteralPath $candidate -Force
  if (-not $target.PSIsContainer -or
      ($target.Attributes -band
        [IO.FileAttributes]::ReparsePoint)) {
    throw "Scratch target is not a normal directory: $candidate"
  }
  $sentinel = Join-Path $candidate ".task03-replay-owner"
  $sentinelItem = Get-Item -LiteralPath $sentinel -Force
  if ($sentinelItem.PSIsContainer -or
      ($sentinelItem.Attributes -band
        [IO.FileAttributes]::ReparsePoint) -or
      -not [StringComparer]::Ordinal.Equals(
        (Get-Content -LiteralPath $sentinel -Raw),
        (Get-Task03ScratchOwnerText -Leaf $Leaf))) {
    throw "Scratch ownership sentinel is absent or invalid: $candidate"
  }

  $pending = [Collections.Generic.Stack[string]]::new()
  $pending.Push($candidate)
  while ($pending.Count -ne 0) {
    $directory = $pending.Pop()
    foreach ($child in Get-ChildItem `
        -LiteralPath $directory -Force) {
      if ($child.Attributes -band
          [IO.FileAttributes]::ReparsePoint) {
        throw "Nested scratch reparse point blocks cleanup: $($child.FullName)"
      }
      if ($child.PSIsContainer) {
        $pending.Push($child.FullName)
      }
    }
  }
  Remove-Item -LiteralPath $candidate -Recurse -Force
  if (Test-Path -LiteralPath $candidate) {
    throw "Owned scratch cleanup did not remove exact leaf: $candidate"
  }
}

function Initialize-Task03ScratchOwnership {
  param(
    [Parameter(Mandatory=$true)]
    [ValidateSet(
      "task03-delta-replay",
      "task03-cumulative-replay"
    )]
    [string]$Leaf
  )
  $candidate = Get-ExactTask03ScratchPath -Leaf $Leaf
  Assert-NormalDirectoryChain -Path $candidate
  $sentinel = Join-Path $candidate ".task03-replay-owner"
  if (Test-Path -LiteralPath $sentinel) {
    throw "Scratch ownership sentinel unexpectedly pre-exists"
  }
  Set-Content -LiteralPath $sentinel `
    -Value (Get-Task03ScratchOwnerText -Leaf $Leaf) `
    -Encoding UTF8 -NoNewline
  $sentinelItem = Get-Item -LiteralPath $sentinel -Force
  if ($sentinelItem.PSIsContainer -or
      ($sentinelItem.Attributes -band
        [IO.FileAttributes]::ReparsePoint) -or
      -not [StringComparer]::Ordinal.Equals(
        (Get-Content -LiteralPath $sentinel -Raw),
        (Get-Task03ScratchOwnerText -Leaf $Leaf))) {
    throw "Unable to establish exact Task 03 scratch ownership"
  }
}

$deltaScratchLeaf = "task03-delta-replay"
$deltaScratch =
  Get-ExactTask03ScratchPath -Leaf $deltaScratchLeaf
Remove-Task03OwnedScratchDirectory -Leaf $deltaScratchLeaf
& git clone --local --no-hardlinks --no-checkout `
  $derivedCore $deltaScratch
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 delta replay clone failed"
}
Initialize-Task03ScratchOwnership -Leaf $deltaScratchLeaf
& git -C $deltaScratch checkout --detach $task02Head
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 delta replay checkout failed"
}
& git -C $deltaScratch apply --cached --check $task03DeltaPatch
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 delta patch check failed"
}
& git -C $deltaScratch apply --cached $task03DeltaPatch
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 delta patch apply failed"
}
$deltaReplayTree =
  (& git -C $deltaScratch write-tree).Trim()
if ($deltaReplayTree -ne $task03Tree) {
  throw "Task 03 delta replay tree differs from Task 03 tree"
}

$cumulativeScratchLeaf = "task03-cumulative-replay"
$cumulativeScratch =
  Get-ExactTask03ScratchPath -Leaf $cumulativeScratchLeaf
Remove-Task03OwnedScratchDirectory `
  -Leaf $cumulativeScratchLeaf
& git clone --local --no-hardlinks --no-checkout `
  $cleanCore $cumulativeScratch
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 cumulative replay clone failed"
}
Initialize-Task03ScratchOwnership `
  -Leaf $cumulativeScratchLeaf
& git -C $cumulativeScratch checkout --detach $base
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 cumulative replay base checkout failed"
}
& git -C $cumulativeScratch apply --cached --check `
  $task03CumulativePatch
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 cumulative patch check failed"
}
& git -C $cumulativeScratch apply --cached `
  $task03CumulativePatch
if ($LASTEXITCODE -ne 0) {
  throw "Task 03 cumulative patch apply failed"
}
$cumulativeReplayTree =
  (& git -C $cumulativeScratch write-tree).Trim()
if ($cumulativeReplayTree -ne $task03Tree) {
  throw "Task 03 cumulative replay tree differs from Task 03 tree"
}

$cumulativeExpectedPaths = @(
  "src/plugins/intel_cpu/src/compiled_model.cpp",
  "src/plugins/intel_cpu/src/compiled_model.h",
  "src/plugins/intel_cpu/src/cpu_memory.cpp",
  "src/plugins/intel_cpu/src/cpu_memory.h",
  "src/plugins/intel_cpu/src/infer_request.cpp",
  "src/plugins/intel_cpu/src/infer_request.h",
  "src/plugins/intel_cpu/src/utils/debug_caps_config.cpp",
  "src/plugins/intel_cpu/src/utils/debug_caps_config.h",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_writer_test.cpp"
) | Sort-Object
$cumulativeActualPaths = @(
  & git -C $derivedCore diff --name-only $base $task03Head
) | Sort-Object
if (Compare-Object `
    $cumulativeExpectedPaths $cumulativeActualPaths) {
  throw "Cumulative Task 02+03 path set is not exact"
}

$receipt = [ordered]@{
  schema = "openvino-cpu-observer-task03-receipt/v1"
  roadmap_sha256 = $roadmapHash
  controller_repo_root = $controllerRepoRoot
  clean_core_commit = $base
  clean_core_tree = (& git -C $cleanCore rev-parse "$base^{tree}").Trim()
  task02_commit = $task02Head
  task02_tree = $task02Tree
  task02_parent = $task02Parent
  task02_subject = $task02Subject
  task02_receipt_sha256 = (
    Get-FileHash -LiteralPath $task02ReceiptPath -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  task02_plan_sha256 = (
    Get-FileHash -LiteralPath $task02Plan -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  task03_boundary_sha256 = (
    Get-FileHash -LiteralPath `
      "R:\.superpowers\sdd\task03-boundary.json" `
      -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  task03_commit = $task03Head
  task03_tree = $task03Tree
  task03_commit_subject = (
    & git -C $derivedCore show -s --format=%s $task03Head
  ).Trim()
  changed_paths = @(
    & git -C $derivedCore diff --name-only `
      $task02Head $task03Head
  )
  cumulative_changed_paths = $cumulativeActualPaths
  writer_test_count = 21
  dump_test_count = 25
  combined_focused_test_count = 46
  focused_runs = 2
  stock_query_selected_count = 1
  local_path_policy =
    "canonical_local_only_unc_and_windows_remote_rejected"
  performance_window_status =
    "no_valid_enabled_query_state_in_task03_controller_gate_deferred"
  ordinary_route_status = "structural_only_deferred_to_task07"
  production_plugin_path = $pluginDll
  production_plugin_sha256 = (
    Get-FileHash -LiteralPath $pluginDll -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  functional_test_path = $functionalExecutable
  functional_test_sha256 = (
    Get-FileHash -LiteralPath `
      $functionalExecutable -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  delta_preview_path = $task03DeltaPatch
  delta_preview_bytes = (
    Get-Item -LiteralPath $task03DeltaPatch
  ).Length
  delta_preview_sha256 = (
    Get-FileHash -LiteralPath `
      $task03DeltaPatch -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  delta_replay_tree = $deltaReplayTree
  cumulative_preview_path = $task03CumulativePatch
  cumulative_preview_bytes = (
    Get-Item -LiteralPath $task03CumulativePatch
  ).Length
  cumulative_preview_sha256 = (
    Get-FileHash -LiteralPath `
      $task03CumulativePatch -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  cumulative_replay_tree = $cumulativeReplayTree
  guard_evidence_root = $guardEvidence
  guard_labels = @($guardContracts.Keys)
  guard_receipt_sha256 = [ordered]@{}
  guard_log_sha256 = [ordered]@{}
}
foreach ($label in $guardContracts.Keys) {
  $receipt.guard_receipt_sha256[$label] = (
    Get-FileHash -LiteralPath `
      (Join-Path $guardEvidence ($label + ".json")) `
      -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  $receipt.guard_log_sha256[$label] = (
    Get-FileHash -LiteralPath `
      (Join-Path $guardEvidence ($label + ".log")) `
      -Algorithm SHA256
  ).Hash.ToLowerInvariant()
}
$task03ReceiptPath =
  "R:\.superpowers\sdd\task03-receipt.json"
$receiptJson = $receipt | ConvertTo-Json -Depth 10
Set-Content -LiteralPath $task03ReceiptPath `
  -Value $receiptJson -Encoding UTF8 -NoNewline

$persistedRaw =
  Get-Content -LiteralPath $task03ReceiptPath -Raw
if (-not [StringComparer]::Ordinal.Equals(
    $persistedRaw,
    $receiptJson)) {
  throw "Persisted Task 03 receipt is not an exact field-for-field round trip"
}
$persisted = $persistedRaw |
  ConvertFrom-Json
if ($persisted -isnot [PSCustomObject] -or
    $persisted.task03_commit -isnot [string] -or
    $persisted.task03_tree -isnot [string] -or
    $persisted.controller_repo_root -isnot [string] -or
    $persisted.task02_parent -isnot [string] -or
    $persisted.task02_subject -isnot [string] -or
    $persisted.delta_replay_tree -isnot [string] -or
    $persisted.cumulative_replay_tree -isnot [string] -or
    (($persisted.writer_test_count -isnot [int]) -and
     ($persisted.writer_test_count -isnot [long])) -or
    (($persisted.combined_focused_test_count -isnot [int]) -and
     ($persisted.combined_focused_test_count -isnot [long])) -or
    $persisted.performance_window_status -isnot [string] -or
    $persisted.ordinary_route_status -isnot [string] -or
    $persisted.task03_commit -ne $task03Head -or
    $persisted.task03_tree -ne $task03Tree -or
    -not [StringComparer]::OrdinalIgnoreCase.Equals(
      [string]$persisted.controller_repo_root,
      $controllerRepoRoot) -or
    $persisted.task02_parent -ne $base -or
    $persisted.task02_subject -ne
      "feat(cpu): capture physical variable-state allocations" -or
    $persisted.delta_replay_tree -ne $task03Tree -or
    $persisted.cumulative_replay_tree -ne $task03Tree -or
    $persisted.writer_test_count -ne 21 -or
    $persisted.combined_focused_test_count -ne 46 -or
    $persisted.performance_window_status -ne
      "no_valid_enabled_query_state_in_task03_controller_gate_deferred" -or
    $persisted.ordinary_route_status -ne
      "structural_only_deferred_to_task07") {
  throw "Persisted Task 03 receipt is incomplete or inconsistent"
}
```

These previews are ignored evidence only. Do not copy either one to
`experiments/patches/openvino-cpu-state-observer`; Task 07 must export the
single final base-to-approved-head patch after all core tasks are accepted.

## Step 13: Independent spec and quality review gates

- [ ] Record all exact evidence in `.superpowers/sdd/progress.md`.
- [ ] Obtain a read-only independent spec verdict.
- [ ] Only after `SPEC PASS`, obtain a different independent quality verdict.
- [ ] Require both PASS verdicts to name the same Task 03 commit.

The implementer records: Task 02 prerequisite identities and reviews; Task 03
boundary hash; exact RED reason; all 13 guard receipt/log hashes; exact
21/25/46 selection; both 46-pass outputs; disabled counter test; stock x64 CPU
query-state selection/pass and no-JSONL proof; invalid-path reason and no-file
proof; root/UNC/mapped-remote/reparse/canonical-local path proof; strict
pre-existing JSONL and pre-open revalidation proof; no-valid-enabled-query/
performance-window ownership deferral; plugin/functional binary hashes;
complete two-project membership; frozen-prerequisite revalidation; exact diff,
full preprocessor truth, and byte-exact disabled projection results; exact
per-label guard argv/path/resource audit; ordinary-route deferral; Task 03
commit/tree; both preview hashes; exact receipt round trip; and both replay
trees.

Request reviews in this exact order:

1. **Spec reviewer (independent, read-only):**
   - read the complete roadmap, complete Task 02 plan, complete Task 03 plan,
     Task 02 and Task 03 receipts, and both prior Task 02 verdicts;
   - inspect the exact nine-path commit `$task03Head`, not a working tree;
   - verify default-off behavior, exact env name/canonical-local path rules,
     root, UNC, Windows mapped-remote, and existing-component reparse
     rejection, enabled-only metadata parsing,
     exact nested schema/correlation contract, immutable holder context,
     process-wide non-reused request IDs, phase transitions, delegation
     behavior, leaf-before-wrapper placement, whole JSONL writes,
     process-wide monotonic sequence, and fail-closed errors;
   - verify Task 03 performs no enabled valid production `query_state()` and
     explicitly assigns performance-window closure/acknowledgement to every
     later enabled caller/controller rather than inventing observer truth;
   - verify every roadmap Task 03 test exists, writer=21, dump=25, combined=46
     passes twice, production plugin builds, the stock x64 query fixture passes
     unset with no JSONL, invalid `.txt` fails for the exact reason with no
     file, and all 13 guard records prove 2,048 MiB/zero survivors;
   - verify no public property/API, no device-truth substitution, no content or
     address field, and only the explicitly Task 07-owned compiled ordinary
     gates are deferred;
   - return exactly `SPEC PASS <40-hex-task03-commit>` or `SPEC FAIL
     <40-hex-task03-commit>` with concrete file/line/evidence references.
     Silence, conditional approval, or "looks good" is not PASS.
2. **Quality reviewer (different independent reviewer, read-only, only after
   `SPEC PASS`):**
   - inspect overflow/exhaustion behavior, memory ordering, mutex scope,
     saturating CAS/no-mutating exhaustion, full-line open/write/flush
     failure, strict existing JSONL tails, JSON grammar and escaped-key
     duplicate detection, valid/invalid Unicode and control handling, path
     normalization/pre-open revalidation/TOCTOU limits, root and all-existing-
     component reparse checks, UNC/mapped-remote rejection after
     canonicalization, model `Any` type strictness, context lifetime,
     constructor ordering,
     cancellation/failure/reset transitions, empty-state phase, null-state
     failure, recursive sub-model/delegation semantics, and lack of public ABI;
   - independently verify the begin hook precedes graph lock/cancellation, the
     completion hook follows output pull, the aggregate branch returns before
     append, and the leaf append precedes public wrapper construction;
   - verify the complete unique production project set is exactly the object
     and shared CPU plugin pair; verify production markers, immutable
     preimages, exact path set, delta and cumulative replay identity, every
     guard's exact argv/cwd/log/evidence/timeout/RAM/sample/peak/Job/no-cap/
     no-node-reuse record, owned-sentinel replay cleanup, byte-exact ordinary
     source projection, and the honest structural-only ordinary deferral;
   - return exactly `QUALITY PASS <40-hex-task03-commit>` or `QUALITY FAIL
     <40-hex-task03-commit>` with concrete references.

Task 03 is accepted only when both verdicts explicitly PASS the same
`$task03Head`. Append the verbatim verdicts to progress so Step 1 of Task 04
can enforce them.

If either reviewer finds a defect:

1. do not continue to Task 04;
2. add a regression that fails for the reported defect;
3. run it RED through a new never-used guard label;
4. implement the minimum correction;
5. repeat fresh configure, unit build/list, both complete 46-test runs,
   production plugin build, functional configure/build/list, unset stock gate,
   invalid-path gate, every audit, and both replay proofs;
6. if this private commit has not left the private branch, amend the Task 03
   commit and regenerate the receipt; otherwise add a separately reviewed fix
   commit and make Task 07 include both;
7. restart both independent reviews against the new exact HEAD.

## Completion checklist

- [ ] Complete roadmap and Task 02 plan were read; roadmap hash is exact.
- [ ] Task 02 receipt and independent PASS verdicts name the exact preimage.
- [ ] Clean core is unchanged and derived core starts clean at Task 02 HEAD.
- [ ] Existing `O:` mapping exactly names the accepted physical derived core.
- [ ] All nine Task 03 preimages and three guard blobs are frozen.
- [ ] Exact 21-test writer source exists; RED is contract-only.
- [ ] Config reads one exact variable and accepts only a valid existing-parent
      canonical local `.jsonl` destination; root, UNC, Windows mapped-remote,
      and existing-component reparse paths fail.
- [ ] Disabled mode performs no metadata parse, request-ID allocation, capture,
      or I/O.
- [ ] Enabled context is exact nested `uint32_t{1}` schema plus 32 lowercase
      hex only.
- [ ] Request IDs never reuse `CompiledModelHolder::id()`; request-ID and
      sequence CAS counters saturate without mutating counter/output.
- [ ] Attempt start clears success; only post-pull completion sets it.
- [ ] Fresh, seeded, post-infer, failure, cancellation, and reset transitions
      all have exact passing tests.
- [ ] Delegating request emits no aggregate; each leaf emits before wrappers.
- [ ] JSON validator rejects malformed/duplicate/oversized input before open;
      existing output is empty or complete valid newline-terminated JSONL,
      and open/write/flush plus pre-open revalidation failures are tested.
- [ ] Eight concurrent writes remain whole lines; main sequence is strictly
      increasing in file order.
- [ ] No tensor contents, prompt/response, address, requested/actual device,
      public property, supported property, or exported API was added.
- [ ] Fresh main configure discovers writer and production source.
- [ ] Exactly 21 writer plus 25 dump tests are listed and pass twice.
- [ ] Production CPU plugin builds and contains Task 03 observer markers.
- [ ] Separate functional-tests-ON tree builds the exact x64 stock fixture.
- [ ] Unset stock query passes once with no JSONL; invalid `.txt` fails for the
      path gate and creates no file.
- [ ] No enabled valid production query runs in Task 03; performance-window
      closure and controller acknowledgement are explicit later-consumer
      acceptance gates, not fabricated observer inputs.
- [ ] All 13 guard receipts match exact argv, paths, 7,200-second timeout,
      complete RAM samples/peaks, suspended Job setup/query/zero survivors,
      no affinity/rate cap, and disabled MSBuild node reuse.
- [ ] Full-truth structural preprocessor audit proves every observer marker
      guarded; strict UTF-8 disabled projections equal Task 02 byte-for-byte.
- [ ] Ordinary binary/symbol absence is honestly deferred only to Task 07.
- [ ] Exact nine-path commit has the required subject and clean status.
- [ ] Exact owned-sentinel/non-reparse scratch cleanup precedes replay;
      delta-on-Task02 and cumulative-on-base both write Task 03 tree.
- [ ] No tracked patch, identity JSON, parent publication, push, or PR changed.
- [ ] Independent `SPEC PASS` and `QUALITY PASS` name the same Task 03 commit.
