# Workbook 05 Route A GenAI CMake Path Comparison Repair Design

## Status

Approved narrow repair for GitHub Actions run `31322416220`, attempt `1`, artifact `workbook-05-build-route-a-genai-31322416220-1`.

The artifact ZIP has SHA-256:

`4725d1396c5042859b483e42e057db999d82051dc5aa9ea3ee540b460d75738e`

The artifact manifest verifies successfully. The Route A GenAI source-matched revision configured successfully against the accepted OpenVINO Runtime, but the build script recorded `Blocked` before compilation because it compared two equivalent Windows paths as different strings.

## Proven first divergence

The accepted Runtime package directory recorded by the build script was:

```text
C:\w5a\phase2-31261978552-2\i-ov\runtime\cmake
```

CMake materialised the same directory in `CMakeCache.txt` as:

```text
C:\w5a\phase2-31261978552-2\i-ov\runtime\cmake
```

However, the current cache check compares that value against:

```powershell
$openvinoConfigDirectory.Replace('\', '/')
```

which produces:

```text
C:/w5a/phase2-31261978552-2/i-ov/runtime/cmake
```

The two strings use different separator styles but identify the same absolute Windows directory. The remaining cache controls matched exactly:

- `CMAKE_GENERATOR = Visual Studio 17 2022`
- `CMAKE_GENERATOR_PLATFORM = x64`
- `ENABLE_PYTHON = ON`
- `ENABLE_JS = OFF`

The configure command exited `0`; no resource safety stop occurred. Therefore the first proven defect is the raw `OpenVINO_DIR` string comparison, not OpenVINO configuration, Runtime compatibility, memory pressure, or source provenance.

## Selected repair

Add one reviewed shared Windows-path equivalence primitive:

```powershell
Test-Wb05SameWindowsPath -Left <path> -Right <path>
```

The helper will:

1. reject null or empty input at its public boundary;
2. canonicalise each path with `[IO.Path]::GetFullPath(...)`;
3. remove a trailing directory separator from each canonical value;
4. compare with `[StringComparison]::OrdinalIgnoreCase`;
5. return a Boolean and perform no filesystem mutation.

Route A GenAI will continue to fail closed when `OpenVINO_DIR` is missing. When it is present, the script will use the helper instead of comparing raw path text.

## Why this boundary

The shared module already owns controlled Windows build primitives and already canonicalises paths for `Assert-Wb05SafePath`. Path equivalence is therefore a shared build concern, not an OpenVINO-specific workaround. Centralising it prevents another caller from repeating separator-sensitive or case-sensitive comparison logic.

The helper does not use `Resolve-Path`, because the cache value is evidence text and path equivalence should not depend on a second filesystem lookup after CMake has already generated the cache. `Path.GetFullPath` provides lexical absolute-path canonicalisation, including normal Windows separator handling.

## Security and fail-closed behaviour

The repair must not weaken the accepted Runtime boundary:

- `Assert-RouteARuntimeInstall` still proves the Runtime installation is under `C:\w5a`, is named `i-ov`, is a directory, and has no reparse-point ancestry.
- The CMake cache must still contain the expected generator, x64 platform, Python enabled, and JavaScript disabled.
- A missing or empty `OpenVINO_DIR` remains a failed cache match.
- Sibling paths such as `i-ov-other\runtime\cmake` must not compare equal.
- No Runtime pin, GenAI pin, TurboQuant implementation, Route B code, model, benchmark, quality rubric, permission, or machine security policy changes.

## Test strategy

Implementation is test-first.

1. Add an executable PowerShell regression that imports the real shared module and proves:
   - backslash and forward-slash representations of the same absolute Windows path compare equal;
   - case-only differences compare equal;
   - a trailing separator does not change identity;
   - a distinct sibling path compares unequal.
2. Run the Workbook 05 gate before implementation and retain the expected red result showing that `Test-Wb05SameWindowsPath` is missing.
3. Add the minimal shared helper and export it.
4. Replace only the Route A GenAI `OpenVINO_DIR` raw comparison.
5. Update exact export and script-use contracts.
6. Run the complete Workbook 05 gate and normal application regression.
7. After merge, rerun only `route-a-genai` using the already accepted Runtime installation and decision file.

## Acceptance criteria

The repository repair is accepted only when:

1. the new path-equivalence regression fails before production code changes and passes afterward;
2. all existing Workbook 05 tests pass;
3. the normal WinUI build and packaged tests pass;
4. the branch diff remains limited to the design/plan, shared helper, Route A GenAI comparison, gate export list, and focused tests;
5. a fresh live Route A GenAI attempt advances past the cache check;
6. build and install complete, `decision.json` says `Passed`, and the hosted untrusted-data validator succeeds.

A green repository repair does not itself authorise model, activation, packed-storage, performance, or quality claims. Those remain later experimental stages.