# Workbook 05 Route A GenAI Version and JSON-Array Repair Design

## Status

Approved repair design for the failure captured by GitHub Actions run `31317870488`, attempt `1`, artifact `workbook-05-build-route-a-genai-31317870488-1`.

This repair is deliberately narrow. It corrects the first proven Route A GenAI integration mismatch and the independently detected evidence-serialization defect. It does not change the successful Route A Runtime commit, rebuild policy, TurboQuant algorithms, model configuration, benchmark methodology, quality rubric, security permissions, or Route B.

## Evidence and root causes

The retained GenAI evidence bundle proves two independent defects.

### Root cause 1: source versions are not compatible

The accepted Route A Runtime was built from OpenVINO commit:

`b9a1f201c109e0bed74763934f79483cf6c4cbf4`

That source identifies itself as OpenVINO `2026.3.0`.

The current Route A GenAI pin is:

`05e5c7670b597746f858946974d11f38e3baf42f`

Its root `CMakeLists.txt` identifies itself as OpenVINO GenAI `2026.4.0.0` and requests OpenVINO `2026.4.0`. CMake therefore rejected the accepted Runtime package at configuration time:

```text
Could not find a configuration file for package "OpenVINO" that is
compatible with requested version "2026.4.0".

OpenVINOConfig.cmake, version: 2026.3.0
```

The matching official GenAI release tag `2026.3.0.0` resolves to immutable commit:

`bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`

Its root `CMakeLists.txt` identifies itself as OpenVINO GenAI `2026.3.0.0` and therefore requests OpenVINO `2026.3.0`. Its public `LLMPipeline` constructors accept an `ov::AnyMap` of Runtime/plugin properties, and the same API surface exposes generation performance metrics. This preserves the intended Route A design: GenAI supplies the LLM pipeline while the accepted modified Runtime owns the merged TurboQuant cache properties and codec implementation.

### Root cause 2: a singleton dependency collection is emitted as an object

`Invoke-Workbook05RouteAGenAIBuild.ps1` passes a one-element collection to `Write-Wb05Json` for `dependencies.json`.

`Write-Wb05Json` currently serialises through the PowerShell pipeline:

```powershell
$json = $Value | ConvertTo-Json -Depth 32
```

Windows PowerShell enumerates the one-element collection before `ConvertTo-Json`, so the file is written as one JSON object instead of the required JSON array. The independent hosted validator correctly rejects it with:

```text
dependencies.json must contain a JSON array.
```

## Selected repair

### 1. Pin a source-matched GenAI revision

Replace the active Route A GenAI pin with:

`bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`

Update every active machine-readable and executable control that must agree on the pin:

- Route A source-admission settings;
- pinned GenAI document sources;
- Route A source-admission manifest;
- untrusted source-admission bundle validator;
- Route A GenAI build script;
- documented-build workflow collection and validation commands;
- active regression tests that freeze the pin.

Historical specifications and plans remain unchanged as records of the earlier decision. This repair adds an explicit amendment instead of rewriting history.

### 2. Preserve collection shape at the shared JSON boundary

Change the shared writer to call `ConvertTo-Json` with `-InputObject` rather than piping the value:

```powershell
$json = ConvertTo-Json -InputObject $Value -Depth 32
```

This keeps an explicitly supplied one-element array as an array while preserving object serialization, nested depth, deterministic UTF-8 without BOM, and the trailing newline.

The repair belongs in the shared writer because the fault occurs at the serialization boundary. Adding special-case brackets only in the GenAI caller would hide the general defect and leave every other one-element evidence collection vulnerable.

## Data flow after the repair

```text
Accepted Runtime 2026.3.0 installation and Passed decision
        ↓
Route A GenAI 2026.3.0.0 exact source commit
        ↓
CMake finds the accepted OpenVINO 2026.3.0 package
        ↓
Configure → build → install
        ↓
Evidence records written through shape-preserving JSON writer
        ↓
Text-only artifact uploaded
        ↓
Separate hosted runner validates hashes, identities, schemas, arrays, paths,
source commit, decision status, and forbidden payload boundaries
```

## Error handling and security boundaries

- Version compatibility remains fail-closed through upstream CMake package checks.
- Source commits remain immutable 40-character SHAs.
- The accepted Runtime installation is consumed read-only.
- Each GenAI attempt uses a fresh short external workspace.
- The workflow remains repository read-only and uploads text-only evidence.
- The hosted validator continues to treat the artifact as untrusted data.
- A failed GenAI build still produces a truthful `Failed` decision and retained evidence.
- No model is downloaded or executed by this repair.
- No performance, quality, activation, packed-storage, or fallback claim is authorised by a successful build alone.

## Test strategy

Implementation is test-first.

1. Add a PowerShell regression test that writes one dependency record through `Write-Wb05Json`, parses the result, and proves the JSON root is an array with exactly one member.
2. Add a contract test that proves Route A Runtime and GenAI pins are release-matched at `2026.3` and that every active GenAI control uses the new exact commit.
3. Keep the existing object-writing tests to prove normal object evidence remains unchanged.
4. Run the complete Workbook 05 gate: Python suite, PowerShell module tests, focused workflow/security contracts, import checks, forbidden-pattern scan, and `git diff --check`.
5. Run the normal WinUI build and packaged tests on the same head.
6. After merge, rerun only `route-a-genai` against the existing accepted Runtime installation and decision file.
7. Accept the repair only when the build decision is `Passed`, the exact text-only artifact is retained, its digest is independently matched, and the hosted validator succeeds.

## Acceptance criteria

The repair is complete only when all of the following are true:

1. no active Route A control still pins `05e5c7670b597746f858946974d11f38e3baf42f`;
2. every active Route A GenAI control pins `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`;
3. the accepted Runtime source commit remains unchanged;
4. singleton arrays serialise as JSON arrays;
5. ordinary JSON objects still serialise as objects;
6. the regression tests demonstrate the pre-repair failure and pass after the repair;
7. the complete Workbook 05 gate passes;
8. the normal application build and packaged tests pass;
9. a fresh post-merge Route A GenAI attempt configures, builds, installs, uploads evidence, and passes independent hosted validation;
10. no Runtime rebuild, model inference, benchmark, quality score, or scientific support claim is introduced by this repair.

## Deferred work

Codec capability execution, no-fallback proof, packed-storage measurement, lowest-memory-first frontier discovery, Granite performance measurements, P1–P6 quality scoring, raw-output retention, QJL/PolarQuant Route B work, and final scientific conclusions remain separate later phases.
