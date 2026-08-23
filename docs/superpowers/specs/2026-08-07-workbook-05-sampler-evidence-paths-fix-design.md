# Workbook 05 sampler/evidence-path repair design

## Status

Approved repair scope from the 2026-08-07 Route A Runtime workflow-dispatch failure. This document freezes the design before any production or workflow behavior is changed.

## Controlled production evidence

The affected workflow-dispatch run is:

```text
run: 31195528209
stage: route-a-runtime
main commit: cf009d0666ef71bf743f5d8e0e56dcb3a0442446
collector job: 92923024297
hosted validator job: 92931650930
```

The repository contract gate passed. The self-hosted Lenovo checkout passed. The Intel-runner repository gate passed with the complete Workbook 05 suite and final `WORKBOOK05_BUILD_STAGE_GATE_PASS` marker.

The Runtime collector then failed while the shared resource sampler was being stopped:

```text
The property 'Sum' cannot be found on this object. Verify that the property exists.
Workbook05.Build.psm1:485
Receive-Job -Job $Sampler.job -ErrorAction Stop | Out-Null
FullyQualifiedErrorId : PropertyNotFoundStrict
```

The failed collector still uploaded the exact text-only artifact:

```text
name: workbook-05-build-route-a-runtime-31195528209-1
artifact ID: 9001706844
GitHub SHA-256: 91d031c2505876721c5d9361d95f7afe6034b3d11544fc24c1755f98df2fd16c
independent SHA-256: 91d031c2505876721c5d9361d95f7afe6034b3d11544fc24c1755f98df2fd16c
files: 37
```

The artifact contains `source-provenance.json` proving the exact OpenVINO repository, exact pinned Runtime commit `b9a1f201c109e0bed74763934f79483cf6c4cbf4`, clean source state, and 36 complete recursive submodules before configuration.

The resource CSV contains 19 successful samples for root CMake process `6988`. The last successful sample is at `2026-08-07T16:34:30.4834226Z`; the sampler exception was surfaced at about `16:34:33Z`. The artifact contains no `route-a-runtime-configure.command.json`, so the configure process exit code was not safely finalised and no OpenVINO configure result may be claimed from this attempt.

The independent hosted validator also progressed beyond the previously repaired `jsonschema` import boundary. It then rejected the artifact with 22 issues: 20 `COMMAND_LOG_MISSING` issues plus the expected missing decision record after collector integrity failure. The command-log files physically exist beneath `commands/`, but the command JSON records contain paths such as:

```text
route-a-runtime-git-status-verify.stdout.log
route-a-runtime-git-status-verify.stderr.log
```

while the actual bundle paths are:

```text
commands/route-a-runtime-git-status-verify.stdout.log
commands/route-a-runtime-git-status-verify.stderr.log
```

## Root causes

### Defect A — sampler process-exit race under StrictMode

Inside the background resource sampler, the loop first checks whether the root process still exists. It then takes a separate CIM process-tree snapshot and separately resolves those process IDs through `Get-Process`.

A native process can terminate between those operations. In that legitimate race, `$processes` can be empty even though the root-process existence check passed moments earlier.

The sampler then evaluates:

```powershell
# Aggregate the current sampled process tree.
$workingSet = [int64](
    ($processes | Measure-Object -Property WorkingSet64 -Sum).Sum
)
$privateBytes = [int64](
    ($processes | Measure-Object -Property PrivateMemorySize64 -Sum).Sum
)
```

With `Set-StrictMode -Version Latest`, attempting to access a property that is not present is a terminating `PropertyNotFoundStrict` error. The background job therefore fails instead of ending normally when the process tree disappears between observations.

The correct contract is: process disappearance after the root check is a normal end-of-sampling condition. The sampler must stop the loop before attempting an aggregate measurement over an empty process collection. It must not invent a synthetic zero-memory sample.

### Defect B — command evidence paths are relative to the wrong root

`Invoke-Wb05LoggedProcess` receives `EvidenceDirectory` pointing at `<bundle>/commands` and writes stdout/stderr/command JSON there. It currently serialises `stdout_path` and `stderr_path` relative to that same `commands` directory.

The independent validator correctly treats all evidence references as bundle-root-relative paths, as demonstrated by its existing valid test fixture using:

```text
commands/configure.stdout.log
commands/configure.stderr.log
```

The producer and validator therefore disagree on the path root. The validator must remain fail-closed; the producer should be corrected to serialize paths relative to the bundle root.

The shared adapter will therefore receive two explicit path roles:

- `EvidenceDirectory`: physical directory where command files are written, e.g. `<bundle>/commands`.
- `EvidenceRoot`: root against which evidence references are serialized, e.g. `<bundle>`.

All Workbook 05 build callers that use the shared adapter must pass the same bundle root explicitly rather than relying on inference.

### Defect C — known empty-stdout bug remains duplicated in GenAI and Route B

The Runtime clean-tree helper was already repaired after run `31182994474` so a successful zero-byte `git status --porcelain=v1` stdout maps to `''` instead of causing `.Trim()` on a null/no-output value.

The Route A GenAI and Route B build scripts still contain the old direct form:

```powershell
# Existing unsafe form retained in two later stages.
(Get-Content -LiteralPath $Result.stdout_path -Raw -ErrorAction Stop).Trim()
```

Both later stages perform the same clean-tree verification pattern. The exact failure mode is therefore already proven on the Lenovo and should be removed before those stages are allowed to run.

## Selected repair

### A. End the sampler cleanly when the process tree disappears

Immediately after resolving `$processes`, add an explicit zero-count guard:

```powershell
# A native process can exit after the root-process check but before this second
# snapshot resolves. That is a normal end-of-sampling boundary, not a zero-byte
# memory sample and not an integrity error.
if ($processes.Count -eq 0) {
    break
}
```

Only non-empty process collections reach the two `Measure-Object -Sum` expressions.

This keeps the sampler accurate: no artificial resource row is written after the process has disappeared.

### B. Make command evidence references bundle-root relative

Extend the shared adapter contract with mandatory `EvidenceRoot`:

```powershell
# Physical destination for command evidence files.
[string]$EvidenceDirectory,

# Root used when writing portable references into command records.
[string]$EvidenceRoot
```

The adapter will continue writing files under `EvidenceDirectory`, but command records will use:

```powershell
# Serialize references from the bundle root so independent validation can
# resolve the same path after artifact upload/download.
stdout_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stdoutPath
stderr_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stderrPath
```

Runtime, GenAI, and Route B callers will pass:

```powershell
-EvidenceDirectory (Join-Path $OutputDirectory 'commands') `
-EvidenceRoot $OutputDirectory
```

No validator relaxation is allowed.

### C. Apply the already-proven null-safe stdout representation to GenAI and Route B

Both duplicate helpers will adopt the same semantic contract as Runtime:

```powershell
# Read the captured stdout while preserving empty successful output as ''.
$capturedOutput = Get-Content `
    -LiteralPath $Result.stdout_path `
    -Raw `
    -ErrorAction Stop
if ($null -eq $capturedOutput) {
    return ''
}
return $capturedOutput.Trim()
```

`-ErrorAction Stop` remains unchanged so missing/unreadable evidence still fails closed.

## Rejected alternatives

### Relax the hosted validator to search relative to each command record

Rejected. Existing validator tests already define evidence paths as bundle-root relative. Making the validator guess multiple roots would weaken a security/integrity boundary and make malformed evidence easier to accept accidentally.

### Treat an empty process collection as zero working set/private bytes

Rejected. That would write a measurement that was not actually observed. Process disappearance means sampling should end, not that memory usage was measured as zero.

### Ignore background sampler failures

Rejected. Resource evidence is part of the documented-build contract. Discarding sampler errors would allow incomplete evidence to masquerade as complete.

### Wait for GenAI/Route B to reproduce the known empty-stdout failure

Rejected. The exact unsafe helper and exact clean-tree command pattern are already present, and the Runtime production failure has already demonstrated the behavior on the target Windows PowerShell environment.

## TDD design

No production code changes will be made before clean RED evidence.

### Regression 1 — disappearing process tree

Add a focused shared-module contract requiring an explicit `$processes.Count -eq 0` guard between process resolution and the first `Measure-Object -Sum`. The test must also enforce that the guard occurs before both aggregate calculations.

Because Linux-side tests cannot execute Windows background jobs, this repository contract test protects the exact ordering that failed in production. The real Lenovo rerun remains the behavioral integration proof.

### Regression 2 — bundle-root command paths

Add a shared-adapter contract requiring:

- mandatory `EvidenceRoot` parameter;
- `stdout_path` and `stderr_path` to call `Get-Wb05RelativePath -Root $EvidenceRoot`;
- Runtime, GenAI, and Route B callers to pass `-EvidenceRoot $OutputDirectory`.

Keep the existing independent-validator valid-bundle fixture unchanged (`commands/...`). That fixture remains the consumer-side contract.

### Regression 3 — null-safe GenAI/Route B stdout

Add focused integrity tests for both later orchestrators requiring:

- materialized `$capturedOutput`;
- explicit `$null` branch;
- `return ''` for a readable zero-byte stdout;
- trimming only after the null check;
- absence of the unsafe direct `Get-Content ... .Trim()` expression.

### RED evidence

Commit only the tests/spec/plan first. Require the PR exact-head Workbook 05 gate to fail solely because the three approved behaviors are absent.

### GREEN evidence

After the minimal production repair, require on the exact implementation head:

- complete Workbook 05 discovery suite;
- focused workflow/security contracts;
- PowerShell module import;
- `git diff --check`;
- final `WORKBOOK05_BUILD_STAGE_GATE_PASS`;
- normal WinUI application build and packaged unit-test regression with all tests passing;
- exact PR diff review;
- no unresolved review threads.

## Security and scientific boundaries

This repair must not change:

- Runtime source: `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`;
- GenAI source: `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`;
- Route B source/BR8 prerequisite rules;
- CMake generator, CPU-only flags, `--parallel 2`, short workspace roots, install roots, or tool pins;
- GitHub runner labels/timeouts;
- immutable action SHAs and read-only workflow permissions;
- text/data-only artifact boundary;
- model execution authorization;
- QJL/PolarQuant/TurboQuant activation claims;
- packed-storage, no-fallback, memory/context, TTFT/tok/s, or quality claims.

The shared resource sampler remains fail-closed for real errors. The hosted artifact validator remains unchanged and fail-closed.

## Integration sequence after implementation

1. Verify clean RED on the test-only branch head.
2. Apply only the three minimal repairs.
3. Verify GREEN on the exact implementation head.
4. Open/update a detailed PR documenting run `31195528209`, both failed jobs, artifact `9001706844`, digest, root causes, RED/GREEN evidence, and unchanged boundaries.
5. Merge only the exact verified PR head.
6. Verify fresh `main` WinUI build/test and independently validate its TRX artifact.
7. Dispatch a new `route-a-runtime` run from the new `main` commit.
8. Observe source acquisition, configure, build, install, artifact upload, and hosted validation in order.
9. Treat any new divergence as a fresh debugging problem; do not infer an OpenVINO source result unless the configure/build/install evidence is actually recorded and independently validated.

## Professional verification basis

Microsoft documents that `Set-StrictMode` turns use of non-existent properties into terminating errors, matching the observed `PropertyNotFoundStrict` failure. Microsoft also documents `Measure-Object -Sum` as an aggregation over input object properties. The repair therefore prevents the aggregate from being evaluated after the sampled process collection has disappeared, rather than weakening StrictMode.
