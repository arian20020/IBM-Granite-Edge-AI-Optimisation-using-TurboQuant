# Workbook 05 Route A Runtime Single-Job Build Experiment Design

## Purpose

Run one controlled follow-up experiment after workflow run `31231872859` reached real OpenVINO Runtime compilation but was terminated by the existing resource-safety boundary. The only experimental variable is Route A Runtime build concurrency: reduce CMake build parallelism from `2` to `1`.

This is not a claim that OpenVINO Runtime will build successfully. It is a bounded experiment to test whether reducing concurrent compilation lowers peak committed-memory pressure enough for the reviewed Runtime build to complete on the Lenovo runner.

## Evidence that motivates the experiment

The accepted evidence bundle from run `31231872859` is:

- repository commit: `e4624417f6824a31a04ab93a4a16728fe2060833`
- Route A Runtime source: `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`
- artifact: `workbook-05-build-route-a-runtime-31231872859-1`
- artifact ID: `9014875921`
- artifact SHA-256: `4771115658d3f326321c2792af6b53c7d16b4086a25e88cb53db240ffe383805`
- independent hosted artifact validation: passed

The bundle records:

- source provenance: exact pinned commit and complete recursive submodules
- CMake configure: completed successfully
- generated CMake cache: matched the reviewed CPU-only controls
- build command: `cmake --build <b-ov> --config Release --parallel 2 --verbose`
- build elapsed before termination: `653.76` seconds
- build peak working set: `7,937,298,432` bytes
- build peak private bytes: `7,756,967,936` bytes
- minimum physical memory still available: `2,591,092,736` bytes
- maximum Windows commit usage: `92%`
- safety stop: triggered because commit usage remained above the reviewed `90%` boundary
- final Runtime decision: `Infrastructure interrupted`
- install stage: not reached

The user then collected read-only Windows diagnostics after the run:

- `AutomaticManagedPagefile = True`
- current page file allocation: `1024 MB`
- current page-file usage: `212 MB`; peak usage: `651 MB`
- current committed bytes: `8,610,738,176`
- current commit limit: `17,931,558,912`
- current committed percentage: about `48.02%`
- free space on `C:`: about `57.5 GB`

These diagnostics do not prove why Windows did not expand the system-managed page file during the build. Therefore this design does not change paging-file configuration.

## Professional reference check

The pinned OpenVINO Windows build document explicitly supports selecting a build job count with `cmake --build . --config Release --verbose -j<number_of_jobs>` and warns that the build may take time.

CMake's official documentation defines `--parallel <jobs>` as the maximum number of concurrent build processes and explicitly states that a value of `1` can be used to limit a build to a single job.

Therefore `--parallel 1` is a supported build-control experiment rather than a repository-specific workaround.

## Selected design

### One experimental variable

Change Route A Runtime build parallelism from `2` to `1` in exactly the two places that describe the same control:

1. `environment.json` producer:

```powershell
parallelism = 1
```

2. actual Runtime build command:

```powershell
'--parallel', '1'
```

Both must change together. Recording `parallelism = 2` while executing `--parallel 1`, or the reverse, would make the evidence internally inconsistent.

### What remains unchanged

The following remain exactly as reviewed:

- Route A Runtime source repository and commit
- OpenVINO Windows build document identity
- Visual Studio generator and x64 platform
- Release configuration
- CPU-only flags
- tests/functional-tests disabled
- samples and Python enabled
- wheel disabled
- Python 3.12.10
- short `C:\w5a` external workspace
- source/build/install directory separation
- resource sampling implementation
- `90%` maximum commit boundary
- `5` consecutive safety samples
- minimum available-memory boundary
- heartbeat boundary
- evidence schemas and manifest hashing
- independent hosted validator
- read-only workflow permissions and immutable action SHAs
- Route A GenAI configuration, including its existing parallelism
- Route B controls and BR8 prerequisite
- Windows page-file configuration

No model execution, Granite inference, TurboQuant/QJL/PolarQuant activation, packed-storage, no-fallback, memory/context, TTFT, throughput, or quality claim is authorised by this experiment.

## Testing design

### TDD RED contract

Before changing the Runtime script, update/add a focused repository contract that requires:

- Route A Runtime evidence metadata to record `parallelism = 1`
- Route A Runtime build arguments to contain `'--parallel', '1'`
- the Runtime script not to retain `'--parallel', '2'`

Run this test against the unchanged production script first. RED is valid only if it fails because the script still records/uses parallelism `2`.

### GREEN implementation

Then change only the two Runtime parallelism representations from `2` to `1`.

Required branch verification:

- complete Workbook 05 Python discovery: all pass
- focused workflow/security contracts: all pass
- executable PowerShell regressions: all pass
- `git diff --check`: pass
- `WORKBOOK05_BUILD_STAGE_GATE_PASS`
- normal WinUI application restore/build: pass
- packaged VSTest: current legitimate total (`134`) all pass
- independently downloaded unit-test artifact SHA-256 matches GitHub
- independently parsed TRX has zero failures/errors/aborts/timeouts/not-executed

## Integration and next live experiment

After exact-head verification:

1. review the complete PR diff for scope drift
2. merge only the verified head with expected-head protection
3. run a fresh post-merge `Build and test` on the new `main` merge commit
4. independently verify the post-merge unit-test artifact again
5. only then dispatch a brand-new `Workbook 05 documented build` with `stage=route-a-runtime`

Do not rerun run `31231872859`, because it is bound to the pre-experiment `--parallel 2` commit.

## Success and failure interpretation

If the new Runtime evidence reaches `Passed`, then Route A Runtime has configured, built, installed, and produced hashable outputs under the reviewed single-job control. Only then may Route A GenAI be attempted using the accepted Runtime install/decision inputs.

If the build is again `Infrastructure interrupted`, do not weaken the safety boundary automatically. Compare the new resource summary with run `31231872859` first. That comparison will determine whether page-file/commit-limit configuration deserves a separate reviewed experiment.

If the build exits with a normal non-zero compiler/build error before any resource stop, treat that as a new upstream/toolchain first divergence and debug it separately.

## Design rationale

This design follows a one-variable experimental method. It directly tests the most conservative supported build-control change while preserving machine configuration and the safety system that prevented a potentially unstable high-commit build.
