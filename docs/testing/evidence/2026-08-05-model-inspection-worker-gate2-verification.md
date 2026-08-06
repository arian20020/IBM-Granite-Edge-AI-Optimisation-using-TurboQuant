# Model Inspection Worker Gate 2 Verification

> Living evidence record for the protected Model Inspection worker boundary. Gate 2 remains incomplete until Tasks 5–11 and the final closure checklist are verified on one exact documentation head.

## Verified prerequisite tasks

### Task 1 — project shells and architecture fitness

- Verified head: `c247559f4bdcc7504621374a176830c5d6f4eaee`
- Windows workflow: `31040807199`
- Job: `92424504782`
- Contract tests: 69/69 passed
- Packaged tests: 207/207 passed
- All eight Gate 2 projects built with zero warnings and zero errors

### Task 2 — bounded UTF-8 transport

- Verified head: `f577a3589566614a430b2c39323f34118f6221d9`
- Focused workflow: `31054971261`
- Focused job: `92470388749`
- Transport tests: 20/20 passed
- Full workflow: `31054971260`
- Full job: `92470408809`
- Contract tests: 69/69 passed
- Packaged tests: 207/207 passed

### Task 3 — WorkerClient execution domain

- Verified head: `0d0e486967a2cbc46b4b618e1b91e6a0f5428d8a`
- Focused workflow: `31061909890`
- Focused job: `92491436316`
- WorkerClient domain tests: 13/13 passed
- Full workflow: `31061909876`
- Full job: `92491458419`
- Contract tests: 69/69 passed
- Packaged tests: 207/207 passed

## Task 4 — trusted executable and environment policies

### Scope verified

Task 4 implements policy and ownership only. It does not launch a process.

The verified implementation provides:

- a fixed worker-relative-path resolver beneath one absolute approved root;
- canonical path normalization and separator-aware containment checks;
- reparse-point rejection for every checked path component;
- regular-file validation;
- minimal Portable Executable parsing and an AMD64-only requirement;
- final directory and executable path resolution through maintained Windows handles;
- repeated containment after final-path resolution;
- a maintained verification handle owned until disposal;
- an empty-by-default child environment with only the hardened allowlist;
- forced disabling of all approved .NET diagnostic entry points;
- deterministic case-insensitive environment sorting;
- an exact UTF-16 double-NUL environment block;
- zeroing and releasing unmanaged environment memory exactly once;
- privacy-safe expected failures that do not preserve raw path-bearing exceptions.

The child environment copies only:

```text
SystemRoot
WINDIR
TEMP
TMP
DOTNET_ROOT       (optional and validated)
DOTNET_ROOT_X64   (optional and validated)
```

It then fixes:

```text
DOTNET_EnableDiagnostics=0
DOTNET_EnableDiagnostics_IPC=0
DOTNET_EnableDiagnostics_Debugger=0
DOTNET_EnableDiagnostics_Profiler=0
```

It deliberately excludes `PATH`, `PATHEXT`, `ComSpec`, processor variables, `COMPlus_*`, diagnostic-port variables, model/request identifiers and secret-bearing provider variables.

### Test-first evidence

Initial clean red:

```text
Workflow: 31063610349
Job:      92496601556
```

The workflow completed checkout, short-path staging and SDK setup, then failed at the first missing production abstraction, `IWorkerExecutableFileSystem`. No unrelated warning or regression preceded the expected failure.

Analyzer-driven corrections were retained rather than suppressed:

- run `31064214089`, job `92498423682`: source-generated `LibraryImport` required enabling unsafe code; the project-wide `AllowUnsafeBlocks=false` policy was preserved and the declarations were replaced with narrowly constrained `DllImport` calls using `System32`, Unicode, exact spelling and last-error capture;
- run `31064365606`, job `92498866121`: `CA1859` identified an unnecessarily broad private collection type;
- run `31064585887`, job `92499527389`: `CA1861` identified a repeatedly allocated expected-key array in test code.

First focused green before reviewer hardening:

```text
Head:     5256614fbc132b09210a64fe420afecfdbb7601b
Workflow: 31064716141
Job:      92499921281
Tests:    32/32 passed
```

The reviewer gate then found that an expected filesystem exception could retain a raw absolute path in the inner exception even though the stable public failure message was safe. A new regression test was added.

Reviewer-regression red:

```text
Head:     a725353fae30411a476b9abb5b6e269589b1b543
Workflow: 31064947152
Job:      92500640457
Result:   32 passed, 1 failed
```

The sole failure proved that the path-bearing exception remained visible through the complete exception chain.

Final focused green:

```text
Head:       4fd837ff31914d5e6686d42363cd115da4b525a3
Workflow:   31065161555
Job:        92501258960
Conclusion: success
Tests:      33/33 passed
Failed:     0
Skipped:    0
```

Final full Windows regression on the same exact code head:

```text
Workflow:   31065161523
Job:        92501261825
Conclusion: success
Contracts:  69/69 passed
Packaged:   207/207 passed
```

Every Gate 2 project restored and built successfully with zero warnings and zero errors. The packaged WinUI build retained the existing 47 warnings and zero errors; those warnings are the previously recorded XAML `WMC1506` warnings and missing local `win-x64.pubxml` warning.

Artifact:

```text
Name:   unit-test-results-31065161523-1
ID:     8953675151
Size:   53,964 bytes
SHA256: caf64842a818399168507b198dd098cfed9fb17f99827f09b8663ac544d92c42
```

Evidence-only commits followed the verified code head. Their workflows are regression checks only; no Task 4 production source changed. Final Task 11 will replace this living evidence with one immutable final-head block.

### Task 4 reviewer checklist

- [x] The worker location comes from a fixed relative path and controlled absolute root.
- [x] Lexical containment is repeated after final handle-based path resolution.
- [x] Path-component reparse points are rejected.
- [x] The executable must be a regular AMD64 PE file.
- [x] The verification file handle is maintained and disposed exactly once.
- [x] The child environment is empty by default and allowlist-only.
- [x] `PATH`, secrets, identifiers and diagnostic ports are not inherited.
- [x] The Unicode environment block is sorted and double-NUL terminated.
- [x] Expected trust-policy failures do not expose raw paths in the complete exception chain.
- [x] Unsafe code remains disabled for the WorkerClient project.
- [x] Task 4 does not launch a process or claim operating-system containment.

## Remaining Gate 2 work

- [ ] Task 5 — safe Win32 process primitives
- [ ] Task 6 — creation-time containment and exact handle inheritance
- [ ] Task 7 — production worker host and controlled engine seam
- [ ] Task 8 — isolated abnormal-process fixture
- [ ] Task 9 — handshake, conversation and exit integrity
- [ ] Task 10 — timeout, cancellation, process-tree, stderr and concurrency proof
- [ ] Task 11 — final architecture, CI, documentation, artifact and review closure
