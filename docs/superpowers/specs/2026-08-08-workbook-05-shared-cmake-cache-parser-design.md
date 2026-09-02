# Workbook 05 Shared CMake Cache Parser Repair Design

**Date:** 2026-08-08  
**Status:** Design approved in conversation; awaiting written-spec approval before implementation  
**Base commit:** `dd48d5fa4c814211238b8748a9a63b25af76d8e5`  
**Repair branch:** `fix/workbook-05-shared-cmake-cache-parser`

## 1. Purpose

Repair the deterministic CMake-cache parsing failure exposed by live Route A Runtime workflow run `31204911650` without changing the OpenVINO source, CMake configuration, build parallelism, runner security controls, or scientific claim boundary.

This repair also closes the same duplicated parser defect in Route B before that route is allowed to reach the same production path.

The larger engineering objective is to stop using expensive 30–40 minute Lenovo source-build attempts as the first place where ordinary Windows PowerShell semantics are exercised. The repaired parser must therefore be covered by an executable PowerShell regression test that runs inside the normal Workbook 05 repository gate before another OpenVINO build begins.

## 2. Production evidence and current scientific boundary

The affected workflow was:

```text
workflow run: 31204911650
stage: route-a-runtime
repository commit: dd48d5fa4c814211238b8748a9a63b25af76d8e5
collector job: 92953825964
hosted validator job: 92963667247
artifact: workbook-05-build-route-a-runtime-31204911650-1
artifact ID: 9005499988
GitHub SHA-256: 1764a4571abd0e79e57bb46bb79f4a0eac9292b3a796c8423d072a6400b69346
independent SHA-256: 1764a4571abd0e79e57bb46bb79f4a0eac9292b3a796c8423d072a6400b69346
files: 40
```

The exact Route A source remained:

```text
openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4
```

The artifact safely finalised `commands/route-a-runtime-configure.command.json`. That record reports `exit_code: 0`. The captured CMake stdout ends with:

```text
-- Configuring done
-- Generating done
-- Build files have been written to: C:/w5a/phase2-31204911650-1/b-ov
```

Therefore the live evidence supports the narrow statement that the reviewed Route A Runtime CMake **configuration/generation stage completed successfully** on the Lenovo for this attempt.

It does **not** support a Runtime compilation, install, model, TurboQuant, performance, memory, latency, or quality claim. The script crashed before the build command was allowed to run.

The separate hosted validator correctly rejected the incomplete bundle because `decision.json` was never produced. Its two findings were downstream consequences of the collector exception:

```text
DECISION_COUNT_INVALID
REQUIRED_PATH_MISSING decision.json
```

They are not independent root causes.

## 3. Root cause

After successful CMake configure, `Invoke-Workbook05RouteARuntimeBuild.ps1` verifies the generated `CMakeCache.txt` rather than trusting command intent alone.

Current code loads the cache with:

```powershell
$cacheLines = Get-Content -LiteralPath $cachePath
```

and passes the result to a local helper:

```powershell
Get-CMakeCacheValue -Lines $cacheLines -Name $name
```

The helper declares:

```powershell
[Parameter(Mandatory = $true)]
[string[]]$Lines
```

A real CMake cache contains structural/comment/blank lines as well as `NAME:TYPE=value` entries. In the production run, `$cacheLines` included an empty string. Windows PowerShell parameter binding rejected that array before the function body ran:

```text
Cannot bind argument to parameter 'Lines' because it is an empty string.
FullyQualifiedErrorId: ParameterArgumentValidationErrorEmptyStringNotAllowed
```

Microsoft documents that mandatory string parameters reject an empty string unless `[AllowEmptyString()]` is explicitly permitted. `[AllowEmptyCollection()]` is a separate concept and would address an empty collection rather than an empty string element.

The parser's intended semantics already ignore non-matching cache lines. Therefore an empty line is valid input that should simply not match a requested `NAME:TYPE=value` entry.

A second code search found the same local `Get-CMakeCacheValue` implementation duplicated in `Invoke-Workbook05RouteBBuild.ps1`. Leaving that copy untouched would knowingly retain a production-proven defect in a later route.

## 4. Design decision

### Chosen approach: one shared, executable parser contract

Create a single reviewed public primitive in:

```text
scripts/testing/workbook05/Workbook05.Build.psm1
```

named:

```text
Get-Wb05CMakeCacheValue
```

Runtime and Route B will call that shared primitive instead of maintaining private duplicate copies.

The shared function will accept a sequence of cache lines containing legitimate empty-string elements. It will search only for the exact requested cache key using the existing `NAME:TYPE=value` grammar and return the substring after the first `=`. If no matching entry exists, it returns `$null`.

The function will not reinterpret comments, invent defaults, silently substitute missing controls, or weaken any later cache comparison. The callers remain responsible for deciding whether a missing value means `Blocked`, `Failed`, or another controlled outcome.

### Public module boundary

`Workbook05.Build.psm1` currently exports nine reviewed primitives. This change deliberately introduces a tenth:

```text
Get-Wb05CMakeCacheValue
```

`Validate-Workbook05-BuildStage.ps1` will include it in the expected exported-function list so a missing/truncated export cannot pass the repository gate.

### Runtime caller

`Invoke-Workbook05RouteARuntimeBuild.ps1` will:

1. remove its private `Get-CMakeCacheValue` helper;
2. continue reading the real `CMakeCache.txt` as text;
3. call `Get-Wb05CMakeCacheValue` for the same reviewed keys;
4. keep the existing `cmake-cache-summary.json` evidence and exact CPU-only comparisons unchanged.

No Runtime source/build argument changes are part of this repair.

### Route B caller

`Invoke-Workbook05RouteBBuild.ps1` will remove its duplicate private parser and use the same module function wherever it reads generated CMake cache controls.

No Route B source, one-file repair, BR8 prerequisite, owner-acceptance, test-catalogue, or scientific boundary changes are part of this repair.

## 5. Executable regression testing

The central improvement is that the parser behavior must be executed under PowerShell rather than protected only by Python source-text assertions.

Create a focused test script under the existing gate-discovered boundary:

```text
tests/testing/workbook05/Invoke-BuildCMakeCacheParserTests.Tests.ps1
```

`Validate-Workbook05-BuildStage.ps1` already discovers `*.Tests.ps1` recursively and invokes each file, so the regression will run in the same Windows PowerShell process used by the Workbook 05 gate.

The test will import the real `Workbook05.Build.psm1` and exercise the real exported function with a small representative cache-line collection containing:

```text
CMAKE_GENERATOR:INTERNAL=Visual Studio 17 2022

ENABLE_INTEL_GPU:BOOL=OFF

Python3_EXECUTABLE:FILEPATH=C:\Program Files\Python312\python.exe
```

The executable assertions will prove at least these behaviors:

1. blank cache lines are accepted and ignored;
2. `CMAKE_GENERATOR` returns `Visual Studio 17 2022`;
3. `ENABLE_INTEL_GPU` returns `OFF`;
4. a value containing Windows punctuation/path characters is preserved;
5. a requested missing key returns `$null` rather than inventing a value;
6. the real module exports `Get-Wb05CMakeCacheValue`.

The test script will use explicit assertions/throws and a controlled zero/non-zero process result so the existing repository gate can fail deterministically without adding a new test framework dependency.

## 6. TDD sequence

Implementation must follow RED → GREEN.

### RED

Before changing production PowerShell:

1. add the executable `*.Tests.ps1` regression;
2. add any small Python contract assertion needed to require Runtime and Route B to consume the shared primitive rather than keep private duplicate parsers;
3. run the Workbook 05 workflow on the exact test-only head;
4. accept RED only if the new regression fails because `Get-Wb05CMakeCacheValue` is absent / current callers remain duplicated;
5. reject RED evidence if failure is caused by syntax, checkout, environment setup, or test-harness mistakes.

### GREEN

Apply only the shared-parser/export/caller changes, then require fresh exact-head evidence:

- complete Workbook 05 suite passes;
- executable PowerShell parser regression passes;
- focused workflow/security contracts pass;
- module import/export gate passes;
- `git diff --check` passes;
- normal WinUI/application workflow passes;
- packaged VSTest remains 134/134 or its then-current legitimate total, with zero failures/errors;
- the unit-test artifact is independently digest-checked and TRX counters are independently inspected.

Only after merge and another fresh `main` verification may another `route-a-runtime` workflow be manually dispatched.

## 7. Error handling and fail-closed rules

The repair must distinguish valid structural cache content from invalid evidence.

Valid and ignored by the parser:

- empty lines;
- comments/non-entry lines;
- unrelated cache keys.

Still fail closed outside the parser:

- missing `CMakeCache.txt`;
- unreadable cache file;
- missing required reviewed cache controls;
- required values that do not equal the reviewed configuration;
- non-zero CMake configure/build/install exit codes;
- resource-safety stops;
- incomplete evidence records/manifests;
- validator identity/hash/schema/path failures.

The parser must not catch or suppress file I/O errors because file loading remains the caller's responsibility.

## 8. Alternatives considered

### Alternative B1: add `[AllowEmptyString()]` to both private helpers

This is the smallest textual patch and would address the observed binding failure. It is rejected as the primary design because it leaves duplicated cache-parser behavior in Runtime and Route B and does not improve the architectural/test boundary that allowed the defect to reach the Lenovo.

### Alternative B2: filter blank lines before calling the helper

For example:

```powershell
$cacheLines = @(Get-Content ... | Where-Object { $_ -ne '' })
```

Rejected because blank CMake cache lines are legitimate input. The parser should tolerate the real format rather than require every caller to pre-sanitize it. Pre-filtering would also duplicate parsing assumptions at call sites.

### Alternative B3: weaken or bypass cache verification

Rejected. The cache check is an important independent verification that the generated build tree materialised the reviewed CPU-only controls. Removing it would weaken the scientific and security boundary merely to avoid a parser bug.

## 9. Scope and non-claims

This repair must not change:

- Runtime source `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`;
- GenAI source `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`;
- Route B source or BR8 prerequisites;
- CMake generator/platform;
- CPU-only Runtime flags;
- `--parallel 2` build control;
- short external workspace/install roots;
- self-hosted runner labels/timeouts;
- workflow read-only permissions;
- immutable action SHAs;
- text/data-only artifact policy;
- hosted validator fail-closed behavior;
- model-execution authorization;
- QJL, PolarQuant, TurboQuant activation state;
- packed-storage/no-fallback claims;
- memory/context, TTFT, tokens/s, quality, or performance claims.

The only new production capability is robust parsing of a legitimate CMake cache representation that the existing control logic already intends to inspect.

## 10. Verification/reference basis

Professional primary-source verification used for this design:

- Microsoft PowerShell `about_Functions_Advanced_Parameters`: `[AllowEmptyString()]` explicitly permits empty-string values for mandatory parameters, while `[AllowEmptyCollection()]` addresses an empty collection rather than empty-string elements.
- Microsoft PowerShell language specification, validation attributes: confirms the same distinction between empty string and empty collection.
- CMake documentation: the first configure of a build tree creates `CMakeCache.txt` and stores persistent cache variables there; `-D` inputs create/update cache entries.

Project textbook basis:

- *Why Programs Fail, 2nd ed.* — reproduce, locate the first divergence, trace the bad value back to its source, and avoid treating downstream validator symptoms as independent causes.
- *The Art of Unit Testing* — turn production defects into trustworthy executable regressions rather than relying on implementation-presence tests alone.
- *Code Complete, 2nd ed.*, Chapters 22–23 — developer testing, defensive construction, and systematic debugging.
- *Designing Secure Software*, Chapter 4 — preserve fail-closed verification and minimise the scope of exceptions rather than bypassing controls.
- *Systems Engineering: Principles and Practice, 3rd ed.*, Chapter 17 — verify each engineering stage independently before advancing claims or subsequent stages.

## 11. Acceptance boundary

This design is accepted only when the written spec has been reviewed by the project owner. Implementation begins afterward with a separate Superpowers implementation plan and RED regression commit.

After implementation and merge, a new Route A Runtime run is allowed to test the next real boundary. The expected next sequence is:

```text
source/provenance
→ configure command and cache verification
→ Runtime build command
→ install command
→ decision + manifest
→ text-only artifact
→ independent hosted validation
```

A future failure at build/install is treated as a new first divergence. Successful cache parsing alone does not authorise any later scientific claim.
