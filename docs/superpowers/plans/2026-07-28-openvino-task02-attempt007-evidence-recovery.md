# OpenVINO Task 02 Attempt 007 Evidence Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:executing-plans` to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce one clean, independently reviewable evidence replay for the
already-committed Task 02 CPU allocation observer.

**Architecture:** Keep the derived OpenVINO source commit immutable. Run a
minimal configure, selected-source compile, LLD relink, focused test replay,
artifact audit, and Git tree replay through the committed single-process guard,
then publish one terminal receipt.

**Tech Stack:** Windows PowerShell 5.1, Python 3.11, CMake, Visual Studio 18
2026 MSBuild, LLVM 22.1.8 LLD, GTest, Git.

## Global Constraints

- Derived core is exactly `O:\` at commit
  `222ad430d201ac4ebf7add6fe4009551c216c378`.
- Base is exactly `ede283a88e35465f0d680dabbf1f44080f8fc387`.
- Evidence root is exactly
  `R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-007`.
- The evidence root must be absent before execution.
- Every configure, build, compiled test, and compiled probe uses the committed
  wrapper with a 2,048 MiB minimum RAM floor.
- A failed guarded command closes Attempt 007; do not retry in this namespace.
- Do not edit the derived source, reuse Attempt 006 evidence, recursively
  clean, push, or update the workbook in this plan.

## Attempt 008 Recovery Amendment

Attempt 007 is permanently closed after the Task 2 wrapper failed before
startup because the controller PowerShell process prohibited script execution.
It produced zero evidence files. The approved Attempt 007 Task 1 boundary is
immutable and remains the historical boundary for that closed attempt.

For Task 2 onward, this amendment supersedes the Attempt 007 namespace:

- Use the parallel boundary
  `.superpowers/sdd/task02-attempt008-boundary.json`, with schema
  `openvino-cpu-observer-task02-attempt008-boundary/v1` and otherwise identical
  immutable identities.
- Use evidence root exactly
  `R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-008`.
- Use only `t2a8-*` labels for Tasks 2 and 3.
- Use replay scratch
  `R:\.superpowers\sdd\replay\task02-attempt008`.
- Use receipt
  `.superpowers/sdd/task02-attempt008-receipt.json` with schema
  `openvino-cpu-observer-task02-attempt008-receipt/v1`.
- A failed guarded command closes Attempt 008; do not retry or reuse its label.
- Before Task 2 execution, capture the effective `CurrentUser` and
  `LocalMachine` execution policies, set only the current process policy to
  `Bypass`, require the effective Process policy to be `Bypass`, and require
  both persistent-scope policies to remain exactly unchanged.
- The Task 2 invocation function reasserts those execution-policy invariants
  immediately before and after every wrapper call.

## Attempt 009 Recovery Amendment

Attempt 008 is permanently closed after its second record rejected the
semicolon-delimited multi-file `SelectedFiles` property. Its configure and
failed production-compile records are immutable.

For Task 2 onward, this amendment supersedes the Attempt 008 namespace and
command set:

- Use the parallel boundary
  `.superpowers/sdd/task02-attempt009-boundary.json`, with schema
  `openvino-cpu-observer-task02-attempt009-boundary/v1` and otherwise identical
  immutable identities.
- Use evidence root exactly
  `R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-009`.
- Use only `t2a9-*` labels for Tasks 2 and 3.
- Compile the two production sources in two strictly serial guarded commands,
  each with exactly one `SelectedFiles` path.
- Use replay scratch
  `R:\.superpowers\sdd\replay\task02-attempt009`.
- Use receipt
  `.superpowers/sdd/task02-attempt009-receipt.json` with schema
  `openvino-cpu-observer-task02-attempt009-receipt/v1`.
- Revalidate exactly ten guard records: configure, two production compiles,
  unit compile, two links, plugin load, list, and two test runs.
- A failed guarded command closes Attempt 009; do not retry or reuse its label.
- Retain the Attempt 008 process-only `Bypass` preflight and before/after
  persistent-policy invariants unchanged.

## Attempt 010 Recovery Amendment

Attempt 009 is permanently closed after six valid Task 2 records and a failed
multiline `-Command` plugin-load record. Preserve all 14 Attempt 009 evidence
files unchanged.

The non-acceptance diagnostic `plugin-load-encoded` under
`task02-plugin-load-preflight-001` proved that the complete load script survives
the committed guard as one UTF-16LE Base64 `-EncodedCommand` argument and loads
and frees the rebuilt plugin successfully.

For Task 2 onward, this amendment supersedes the Attempt 009 namespace:

- Use `.superpowers/sdd/task02-attempt010-boundary.json`, with schema
  `openvino-cpu-observer-task02-attempt010-boundary/v1` and otherwise identical
  immutable identities.
- Use evidence root exactly
  `R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-010`.
- Use only `t2a10-*` labels for Tasks 2 and 3.
- Retain the two strictly serial single-file production compiles.
- Encode the complete plugin-load script with
  `[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($pluginLoadScript))`
  and pass exactly eight argv entries ending in
  `-EncodedCommand`, `<single base64 string>`.
- Use replay scratch
  `R:\.superpowers\sdd\replay\task02-attempt010`.
- Use receipt `.superpowers/sdd/task02-attempt010-receipt.json` with schema
  `openvino-cpu-observer-task02-attempt010-receipt/v1`.
- Revalidate exactly ten acceptance guard records.
- A failed guarded command closes Attempt 010; do not retry or reuse its label.
- Retain the process-only `Bypass` and persistent-policy invariants unchanged.

---

### Task 1: Freeze the boundary

**Files:**
- Read:
  `.superpowers/sdd/task02-receipt.json`
- Read:
  `docs/superpowers/specs/2026-07-28-openvino-task02-attempt007-evidence-recovery-design.md`
- Create:
  `.superpowers/sdd/task02-attempt007-boundary.json`

**Interfaces:**
- Consumes: accepted Task 02 commit, receipt, and committed guard blobs.
- Produces: immutable boundary identities used by every later task.

- [ ] **Step 1: Verify RAM, substitutions, repository identity, and cleanliness**

Run from the controller worktree:

```powershell
$ErrorActionPreference = "Stop"
$root = (Resolve-Path ((& git rev-parse --show-toplevel).Trim())).Path
$rLine = @(
  @(& subst.exe) |
    Where-Object { $_ -match '^R:\\: => (?<target>.+)$' }
)
if ($rLine.Count -ne 1 -or
    -not ($rLine[0] -match '^R:\\: => (?<target>.+)$')) {
  throw "R: controller mapping is not active"
}
$rRoot = [IO.Path]::GetFullPath($Matches.target).TrimEnd("\")
$controllerRoot = [IO.Path]::GetFullPath($root).TrimEnd("\")
if (-not $controllerRoot.Equals(
    $rRoot, [StringComparison]::OrdinalIgnoreCase)) {
  throw "R: controller mapping is not active"
}
$expectedCore = "C:\ov-wb04\2026-07-19\openvino-cpu-state-observer"
$oLine = @(
  @(& subst.exe) | Where-Object { $_ -match '^O:\\: => ' }
)
if ($oLine.Count -ne 1 -or
    -not $oLine[0].EndsWith($expectedCore,
      [StringComparison]::OrdinalIgnoreCase)) {
  throw "O: mapping drifted"
}
$os = Get-CimInstance Win32_OperatingSystem
if ([int64]$os.FreePhysicalMemory * 1KB -lt 2GB) {
  throw "Insufficient RAM before Attempt 007"
}
$task02 = "222ad430d201ac4ebf7add6fe4009551c216c378"
$task02Tree = "c7ec0322ec6e8c259c683454868458d8ca0a1ad5"
$base = "ede283a88e35465f0d680dabbf1f44080f8fc387"
if ((& git -C O:\ rev-parse HEAD).Trim() -cne $task02 -or
    (& git -C O:\ rev-parse "HEAD^{tree}").Trim() -cne $task02Tree -or
    (& git -C O:\ rev-parse "HEAD^").Trim() -cne $base -or
    @(& git -C O:\ status --porcelain --untracked-files=all).Count -ne 0) {
  throw "Derived Task 02 boundary drifted"
}
```

- [ ] **Step 2: Verify the exact five-path delta and guard blobs**

```powershell
$expectedPaths = @(
  "src/plugins/intel_cpu/src/cpu_memory.cpp",
  "src/plugins/intel_cpu/src/cpu_memory.h",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"
) | Sort-Object
$actualPaths = @(& git -C O:\ diff --name-only $base $task02) |
  Where-Object { $_ } | Sort-Object
if (Compare-Object $expectedPaths $actualPaths) {
  throw "Task 02 path set drifted"
}
$receipt = Get-Content .superpowers/sdd/task02-receipt.json -Raw |
  ConvertFrom-Json
foreach ($property in $receipt.guard_blobs.PSObject.Properties) {
  if ((& git rev-parse "HEAD:$($property.Name)").Trim() -cne
      [string]$property.Value -or
      (& git hash-object "--path=$($property.Name)" --
        $property.Name).Trim() -cne [string]$property.Value) {
    throw "Guard blob drifted: $($property.Name)"
  }
}
```

- [ ] **Step 3: Require a new empty namespace and persist the boundary**

```powershell
$evidence =
  "R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-007"
if (Test-Path -LiteralPath $evidence) {
  throw "Attempt 007 evidence root already exists"
}
$boundary = [ordered]@{
  schema = "openvino-cpu-observer-task02-attempt007-boundary/v1"
  base_commit = $base
  task02_commit = $task02
  task02_tree = $task02Tree
  task02_subject = (& git -C O:\ show -s --format=%s HEAD).Trim()
  changed_paths = $expectedPaths
  guard_blobs = $receipt.guard_blobs
}
$boundary | ConvertTo-Json -Depth 8
```

Capture the emitted JSON and create
`.superpowers/sdd/task02-attempt007-boundary.json` with `apply_patch`. Re-read
it with `ConvertFrom-Json` and compare every field to `$boundary`.

### Task 2: Configure and rebuild only affected outputs

**Files:**
- Rebuild:
  `C:\ov-build\state-observer\src\plugins\intel_cpu\openvino_intel_cpu_plugin_obj.vcxproj`
- Rebuild:
  `C:\ov-build\state-observer\src\plugins\intel_cpu\tests\unit\ov_cpu_unit_tests.vcxproj`
- Relink:
  `O:\bin\intel64\Release\ov_cpu_unit_tests.exe`
- Relink:
  `O:\bin\intel64\Release\openvino_intel_cpu_plugin.dll`

**Interfaces:**
- Consumes: immutable boundary and committed guard.
- Produces: freshly compiled Task 02 objects and relinked test/plugin binaries.

- [ ] **Step 1: Establish the controller execution-policy invariant and define
  the fixed guarded invocation**

```powershell
$policyBefore = [ordered]@{
  CurrentUser = Get-ExecutionPolicy -Scope CurrentUser
  LocalMachine = Get-ExecutionPolicy -Scope LocalMachine
}
Set-ExecutionPolicy -Scope Process Bypass -Force
if ((Get-ExecutionPolicy -Scope Process) -cne "Bypass" -or
    (Get-ExecutionPolicy -Scope CurrentUser) -cne
      [string]$policyBefore.CurrentUser -or
    (Get-ExecutionPolicy -Scope LocalMachine) -cne
      [string]$policyBefore.LocalMachine) {
  throw "Controller execution-policy preflight failed"
}
$wrapper = "R:\scripts\testing\invoke_guarded_command.ps1"
$python =
  "C:\Users\Student\AppData\Local\Programs\Python\Python311\python.exe"
$pythonSha =
  "5f7b89a612c9b8af1d6456cdfcd1dbe5ca630849e79aebced9bee9a6694952ec"
$pythonDllSha =
  "0817a2a657a24c0d5fbb60df56960f42fc66b3039d522ec952dab83e2d869364"
$cmake =
  "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
$msbuild =
  "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
$evidence =
  "R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-010"
if (Test-Path -LiteralPath $evidence) {
  throw "Attempt 010 evidence root already exists"
}
function Assert-Attempt010ExecutionPolicy {
  if ((Get-ExecutionPolicy -Scope Process) -cne "Bypass" -or
      (Get-ExecutionPolicy -Scope CurrentUser) -cne
        [string]$policyBefore.CurrentUser -or
      (Get-ExecutionPolicy -Scope LocalMachine) -cne
        [string]$policyBefore.LocalMachine) {
    throw "Attempt 010 execution-policy invariant failed"
  }
}
function Invoke-Attempt010Guard {
  param(
    [string]$Label,
    [string[]]$Command,
    [double]$TimeoutSeconds = 7200
  )
  Assert-Attempt010ExecutionPolicy
  $wrapperExit = $null
  try {
    & $wrapper -Label $Label -WorkingDirectory R:\ `
      -EvidenceRoot $evidence -ExpectedExit Zero `
      -TimeoutSeconds $TimeoutSeconds -MinimumAvailableRamMiB 2048 `
      -PythonExecutable $python -PythonSha256 $pythonSha `
      -PythonDllSha256 $pythonDllSha -Command $Command
    $wrapperExit = $LASTEXITCODE
  }
  finally {
    Assert-Attempt010ExecutionPolicy
  }
  if ($null -eq $wrapperExit -or $wrapperExit -ne 0) {
    throw "$Label wrapper failed"
  }
}
```

- [ ] **Step 2: Configure the validated functional-test build**

```powershell
Invoke-Attempt010Guard "t2a10-cfg-g" @(
  $cmake, "-S", "O:\", "-B", "C:\ov-build\state-observer",
  "-G", "Visual Studio 18 2026", "-A", "x64",
  "-DENABLE_DEBUG_CAPS=ON", "-DENABLE_CPU_DEBUG_CAPS=ON",
  "-DENABLE_TESTS=ON", "-DENABLE_FUNCTIONAL_TESTS=ON",
  "-DENABLE_HETERO=OFF", "-DENABLE_SAMPLES=OFF",
  "-DENABLE_PYTHON=OFF", "-DENABLE_INTEL_GPU=OFF",
  "-DENABLE_INTEL_NPU=OFF", "-DENABLE_OV_ONNX_FRONTEND=OFF",
  "-DENABLE_OV_PADDLE_FRONTEND=OFF", "-DENABLE_OV_TF_FRONTEND=OFF",
  "-DENABLE_LTO=OFF", "-DBUILD_SHARED_LIBS=ON"
) 1200
```

- [ ] **Step 3: Compile each affected production source serially**

```powershell
Invoke-Attempt010Guard "t2a10-cpu-memory-g" @(
  $msbuild,
  "C:\ov-build\state-observer\src\plugins\intel_cpu\openvino_intel_cpu_plugin_obj.vcxproj",
  "/t:ClCompile",
  "/p:SelectedFiles=O:\src\plugins\intel_cpu\src\cpu_memory.cpp",
  "/p:Configuration=Release", "/p:Platform=x64",
  "/p:BuildProjectReferences=false", "/p:BuildInParallel=false",
  "/p:UseMultiToolTask=false", "/p:TrackFileAccess=false",
  "/m:1", "/nr:false", "/v:minimal"
)
Invoke-Attempt010Guard "t2a10-dump-compile-g" @(
  $msbuild,
  "C:\ov-build\state-observer\src\plugins\intel_cpu\openvino_intel_cpu_plugin_obj.vcxproj",
  "/t:ClCompile",
  "/p:SelectedFiles=O:\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp",
  "/p:Configuration=Release", "/p:Platform=x64",
  "/p:BuildProjectReferences=false", "/p:BuildInParallel=false",
  "/p:UseMultiToolTask=false", "/p:TrackFileAccess=false",
  "/m:1", "/nr:false", "/v:minimal"
)
```

- [ ] **Step 4: Compile the focused unit source**

```powershell
Invoke-Attempt010Guard "t2a10-unit-compile-g" @(
  $msbuild,
  "C:\ov-build\state-observer\src\plugins\intel_cpu\tests\unit\ov_cpu_unit_tests.vcxproj",
  "/t:ClCompile",
  "/p:SelectedFiles=O:\src\plugins\intel_cpu\tests\unit\state_allocations_dump_test.cpp",
  "/p:Configuration=Release", "/p:Platform=x64",
  "/p:BuildProjectReferences=false", "/p:BuildInParallel=false",
  "/p:UseMultiToolTask=false", "/p:TrackFileAccess=false",
  "/m:1", "/nr:false", "/v:minimal"
)
```

- [ ] **Step 5: Relink the unit executable and CPU plugin with LLD**

```powershell
$lldProperties = @(
  "/p:Configuration=Release", "/p:Platform=x64",
  "/p:BuildProjectReferences=false", "/p:BuildInParallel=false",
  "/p:UseMultiToolTask=false", "/p:TrackFileAccess=false",
  "/p:LinkToolExe=lld-link.exe",
  "/p:LinkToolPath=C:\Program Files\LLVM\bin",
  "/m:1", "/nr:false", "/v:minimal"
)
Invoke-Attempt010Guard "t2a10-unit-link-g" (
  @($msbuild,
    "C:\ov-build\state-observer\src\plugins\intel_cpu\tests\unit\ov_cpu_unit_tests.vcxproj",
    "/t:_Link") + $lldProperties
)
Invoke-Attempt010Guard "t2a10-plugin-link-g" (
  @($msbuild,
    "C:\ov-build\state-observer\src\plugins\intel_cpu\openvino_intel_cpu_plugin.vcxproj",
    "/t:_Link") + $lldProperties
)
```

- [ ] **Step 6: Load the rebuilt CPU plugin in a guarded compiled probe**

```powershell
$pluginLoadScript = @'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class NativePluginProbe {
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  public static extern bool SetDllDirectory(string path);
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  public static extern IntPtr LoadLibrary(string path);
  [DllImport("kernel32.dll", SetLastError = true)]
  public static extern bool FreeLibrary(IntPtr module);
}
"@
$directory = "O:\bin\intel64\Release"
if (-not [NativePluginProbe]::SetDllDirectory($directory)) { exit 20 }
$path = Join-Path $directory "openvino_intel_cpu_plugin.dll"
$module = [NativePluginProbe]::LoadLibrary($path)
if ($module -eq [IntPtr]::Zero) { exit 21 }
if (-not [NativePluginProbe]::FreeLibrary($module)) { exit 22 }
'@
$pluginLoadEncoded = [Convert]::ToBase64String(
  [Text.Encoding]::Unicode.GetBytes($pluginLoadScript)
)
Invoke-Attempt010Guard "t2a10-plugin-load-g" @(
  "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe",
  "-NoLogo", "-NoProfile", "-NonInteractive",
  "-ExecutionPolicy", "Bypass", "-EncodedCommand", $pluginLoadEncoded
) 300
```

### Task 3: Run and verify the exact focused suite

**Files:**
- Execute:
  `O:\bin\intel64\Release\ov_cpu_unit_tests.exe`
- Verify:
  `O:\bin\intel64\Release\openvino_intel_cpu_plugin.dll`
- Verify:
  `O:\bin\intel64\Release\openvino.dll`

**Interfaces:**
- Consumes: freshly linked binaries.
- Produces: one list record, two independent 25/25 run records, and artifact
  identities.

- [ ] **Step 1: List exactly 25 focused tests**

```powershell
$unit = "O:\bin\intel64\Release\ov_cpu_unit_tests.exe"
Invoke-Attempt010Guard "t2a10-list-g" @(
  $unit, "--gtest_list_tests",
  "--gtest_filter=StateAllocationsDump.*", "--gtest_color=no"
) 300
$listText = Get-Content "$evidence\t2a10-list-g.log" -Raw
$testCount = @(
  $listText -split "\r?\n" |
    Where-Object { $_ -match '^\s{2}[A-Za-z0-9_]+$' }
).Count
if ($testCount -ne 25) { throw "Focused selection is $testCount, expected 25" }
```

- [ ] **Step 2: Run the same 25-test selection twice**

```powershell
foreach ($label in "t2a10-run-g1", "t2a10-run-g2") {
  Invoke-Attempt010Guard $label @(
    $unit, "--gtest_filter=StateAllocationsDump.*", "--gtest_color=no"
  ) 300
  $text = Get-Content "$evidence\$label.log" -Raw
  if ($text -notmatch '\[\s+PASSED\s+\]\s+25 tests\.' -or
      $text -match '\[\s+FAILED\s+\]') {
    throw "$label is not an exact 25/25 pass"
  }
}
```

- [ ] **Step 3: Hash the final artifacts**

```powershell
$artifactPaths = [ordered]@{
  unit = $unit
  plugin = "O:\bin\intel64\Release\openvino_intel_cpu_plugin.dll"
  runtime = "O:\bin\intel64\Release\openvino.dll"
}
$artifacts = [ordered]@{}
foreach ($entry in $artifactPaths.GetEnumerator()) {
  $item = Get-Item -LiteralPath $entry.Value
  if ($item.Length -le 0) { throw "Empty artifact: $($entry.Value)" }
  $artifacts[$entry.Key] = [ordered]@{
    path = $item.FullName
    bytes = [int64]$item.Length
    sha256 = (Get-FileHash $item.FullName -Algorithm SHA256).
      Hash.ToLowerInvariant()
  }
}
```

### Task 4: Replay, receipt, and independent acceptance

**Files:**
- Create:
  `.superpowers/sdd/task02-attempt010-receipt.json`
- Modify:
  `.superpowers/sdd/progress.md`

**Interfaces:**
- Consumes: boundary, all ten guard records/logs, artifacts, and Task 02 Git
  delta.
- Produces: terminal receipt and two independent review verdicts.

- [ ] **Step 1: Replay the exact Task 02 delta in owned scratch**

```powershell
$scratch =
  "R:\.superpowers\sdd\replay\task02-attempt010"
if (Test-Path $scratch) { throw "Replay scratch already exists" }
& git clone --no-hardlinks --no-checkout O:\ $scratch
if ($LASTEXITCODE -ne 0) { throw "Replay clone failed" }
& git -C $scratch checkout --detach $base
if ($LASTEXITCODE -ne 0) { throw "Replay base checkout failed" }
& git -C $scratch cherry-pick --no-commit $task02
if ($LASTEXITCODE -ne 0) { throw "Task 02 replay cherry-pick failed" }
$replayTree = (& git -C $scratch write-tree).Trim()
if ($replayTree -cne $task02Tree) { throw "Replay tree mismatch" }
```

- [ ] **Step 2: Revalidate every guard record and log**

```powershell
$labels = @(
  "t2a10-cfg-g", "t2a10-cpu-memory-g", "t2a10-dump-compile-g",
  "t2a10-unit-compile-g", "t2a10-unit-link-g", "t2a10-plugin-link-g",
  "t2a10-plugin-load-g", "t2a10-list-g", "t2a10-run-g1",
  "t2a10-run-g2"
)
$guardReceipts = [ordered]@{}
foreach ($label in $labels) {
  $jsonPath = "$evidence\$label.json"
  $logPath = "$evidence\$label.log"
  $record = Get-Content $jsonPath -Raw | ConvertFrom-Json
  $actualLogHash =
    (Get-FileHash $logPath -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($record.valid -ne $true -or [int64]$record.exit_code -ne 0 -or
      $record.timed_out -ne $false -or
      $record.low_memory_stop -ne $false -or
      [int64]$record.configured_minimum_available_ram_bytes -ne 2GB -or
      [int64]$record.observed_available_ram_bytes.minimum -lt 2GB -or
      $record.job_object.setup_ok -ne $true -or
      $record.job_object.query_ok -ne $true -or
      [int64]$record.job_object.queried_active_process_count_after_cleanup -ne 0 -or
      @($record.job_object.survivor_pids_after_cleanup).Count -ne 0 -or
      @($record.emergency_actions).Count -ne 0 -or
      @($record.validation_errors).Count -ne 0 -or
      $record.log_sha256 -cne $actualLogHash) {
    throw "Guard revalidation failed: $label"
  }
  $guardReceipts[$label] = [ordered]@{
    record_sha256 =
      (Get-FileHash $jsonPath -Algorithm SHA256).Hash.ToLowerInvariant()
    log_sha256 = $actualLogHash
  }
}
```

- [ ] **Step 3: Publish and round-trip the terminal receipt**

```powershell
$receiptPath = ".superpowers\sdd\task02-attempt010-receipt.json"
$final = [ordered]@{
  schema = "openvino-cpu-observer-task02-attempt010-receipt/v1"
  accepted = $true
  base_commit = $base
  task02_commit = $task02
  task02_tree = $task02Tree
  changed_paths = $expectedPaths
  test_selection = "StateAllocationsDump.*"
  listed_tests = 25
  run1_passed = 25
  run2_passed = 25
  artifacts = $artifacts
  replay_tree = $replayTree
  guards = $guardReceipts
}
$serialized = $final | ConvertTo-Json -Depth 10
$serialized
```

Capture the emitted JSON and create `$receiptPath` with `apply_patch`, then
continue with this exact round-trip check:

```powershell
$roundTrip = Get-Content $receiptPath -Raw | ConvertFrom-Json
if ($roundTrip.schema -cne $final.schema -or
    $roundTrip.accepted -ne $true -or
    $roundTrip.task02_commit -cne $task02 -or
    $roundTrip.replay_tree -cne $task02Tree -or
    @($roundTrip.guards.PSObject.Properties).Count -ne 10) {
  throw "Terminal receipt round trip failed"
}
```

- [ ] **Step 4: Obtain independent spec and quality verdicts**

The spec reviewer reads the design, this plan, boundary, exact commit diff,
all ten guard records/logs, artifact bytes, replay checkout, and terminal
receipt. It returns exactly:

`SPEC PASS 222ad430d201ac4ebf7add6fe4009551c216c378`, followed on
the same line by the computed 64-hex receipt SHA-256.

Only after spec pass, a different quality reviewer independently checks guard
integrity, log hashes, RAM samples, zero survivors, test parsing, artifact
hashes, replay identity, and lack of source drift. It returns exactly:

`QUALITY PASS 222ad430d201ac4ebf7add6fe4009551c216c378`, followed on
the same line by the same computed 64-hex receipt SHA-256.

Append both verbatim verdicts to `.superpowers/sdd/progress.md`. Any other
verdict closes Attempt 010 and prevents Task 03.
