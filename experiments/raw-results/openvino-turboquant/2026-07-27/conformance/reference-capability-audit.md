# OpenVINO TurboQuant reference-capability recovery audit

Date: 2026-07-27/28 UTC

## Outcome

The accepted capability artifact is
`reference-capability.json`, SHA-256
`b727380d2f8c0f7646fef30862aa6a4fc3315744fe4f3825a0282312fbb8b3a0`.
It has schema `openvino-turboquant-reference-capability-evidence/v3` and
was published from raw attempt
`reference-capability/attempt-20260727T234930Z-bb96e61e`.

The prior v2 canonical was not deleted or silently rewritten. Its exact bytes
are retained as
`reference-capability/superseded-reference-capability-f9a05f8b8f669246.json`,
SHA-256
`f9a05f8b8f6692460821a3b0be4506bdc8ccb7ec442d4966c13123981af1377e`.
The v3 canonical and attempt summary both identify that superseded artifact.

This is controlled conformance evidence, not a performance benchmark.
The workload is deliberately constrained to one logical processor and a 1%
Windows Job Object CPU hard cap before resume so that the short clean-binary
test yields multiple anchored 100 ms observations. Microsoft defines the hard
cap as the maximum CPU-cycle rate for the job:
<https://learn.microsoft.com/windows/win32/api/winnt/ns-winnt-jobobject_cpu_rate_control_information>.

## Clean build provenance

- Source checkout:
  `R:\external\official-openvino\2026-07-19\openvino.genai-turboquant-capability-adb5fbe`
- Source commit:
  `adb5fbe37e9f8c533461c892a19191f1709ae774`
- Source tree:
  `bf6a323d10478db59392bf1c42d0ed42610fdce2`
- Source `git status --porcelain=v1 --untracked-files=all`: empty before
  run 1, before run 2, after run 2, and during the post-run audit.
- Build directory:
  `R:\external\official-openvino\2026-07-19\build-genai-turboquant-capability-adb5fbe`
- `CMakeCache.txt` SHA-256:
  `8cfbfe449965cd2595cee07331c76b59ad09cf03d25722284766104b83d2abb9`
- `CMAKE_HOME_DIRECTORY` resolves to the clean source checkout above.
- Executable:
  `tests\cpp\Release\tests_continuous_batching.exe`
- Executable size: 8,354,816 bytes.
- Executable SHA-256:
  `1ccf27ff8bce1b2c5cf179c27d5aeadc3c4f356f0e256d9d9e60d699d318f497`

The serial build command recorded by the build controller was:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' `
  --build R:\external\official-openvino\2026-07-19\build-genai-turboquant-capability-adb5fbe `
  --config Release `
  --target tests_continuous_batching `
  --parallel 1
```

It ran from `2026-07-27T23:03:11.006820Z` through
`2026-07-27T23:30:39.027980Z`, exited 0, and recorded a minimum of
6,038,392,832 available bytes against the 2,147,483,648-byte floor.
The build log SHA-256 is
`1d36d70c26e5953627fcaf3776fdf1b424e41cd10aeada28a019b5773181fb47`;
the build-status JSON SHA-256 is
`fdabcb4e9e1d42ef3f0688ffdf343e21c54b30f709c72a4a4835dab99c988d0d`.
The clean-build audit counted 139 fresh production objects and 29 fresh test
objects and found no project reference to the dirty checkout.

The final attempt snapshots the same `CMakeCache.txt` and checks identical
build provenance, executable SHA-256, derived commit, and clean status at
three points: before run 1, before run 2, and after run 2. GTest independently
reports its source as the clean checkout's
`tests\cpp\turboquant_stateful_graph.cpp:1257`.

## Harness changes and fail-closed rules

- CPU sampling now uses a dedicated CSV and anchored 100 ms deadlines.
  Provider startup is completed while the workload is suspended.
- GPU engine and memory queries run asynchronously and are recorded in a
  separate `gpu.csv`, including independent success flags and start/completion
  timestamps. GPU provider latency cannot consume a CPU sampling deadline.
- Publication requires at least two CPU rows, at least one `interval_delta`
  row, contiguous 100 ms deadlines, lateness below one interval, and
  window-derived row coverage:
  `max(2, floor(window_ms / 100) - 1)`.
- Publication requires the exact 300 s timeout, exact 2,048 MiB configured RAM
  floor, measured minimum available RAM at or above that floor, one-core
  affinity, and the 1% CPU hard cap set before resume.
- The executable must be inside the declared build directory, whose
  `CMakeCache.txt` must identify the selected clean source checkout.
- Each nested run must be marked valid, its run ID must equal its output
  directory name, and its artifact manifest must match the exact v3 mapping
  including `gpu.csv`.
- The production command is exactly the executable, the single expected GTest
  filter, and that run's absolute GTest JSON output argument.
- Failed attempts are written under a fresh attempt directory and cannot
  replace the canonical JSON.

## Preserved failed clean attempt

`reference-capability/attempt-20260727T233215Z-6f1d45dd` is retained as a
failed attempt rather than being hidden:

- attempt status: `failed`
- error:
  `ValueError: CPU interval-delta evidence requires at least two rows`
- run 1: 0.2787187 s sampling window, 2 CPU rows, 1 interval delta
- run 2: 0.1884649 s sampling window, 1 CPU row, 0 interval deltas
- both GTests passed, and both GPU engine and memory queries succeeded
- the canonical remained the prior v2 artifact

That failure established that the clean executable is materially shorter than
the earlier dirty-checkout executable. An affinity-only diagnostic still
produced one-row warm runs. A subsequent non-canonical pilot using the
pre-resume 1% CPU hard cap produced 18/7 CPU rows and proved the control before
the final attempt was authorized.

## Final command and preconditions

Immediately before the final run, the process scan found zero
`cmake`, `MSBuild`, `cl`, or `link` processes and Windows reported
7,683.8 MiB free physical memory. Task 6 had explicitly handed over the heavy
slot.

```powershell
python scripts/testing/run_openvino_reference_capability.py `
  --derived-repo R:\external\official-openvino\2026-07-19\openvino.genai-turboquant-capability-adb5fbe `
  --build-dir R:\external\official-openvino\2026-07-19\build-genai-turboquant-capability-adb5fbe `
  --executable R:\external\official-openvino\2026-07-19\build-genai-turboquant-capability-adb5fbe\tests\cpp\Release\tests_continuous_batching.exe `
  --runtime-library-dir C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.venv-official-openvino-2026.2.1\Lib\site-packages\openvino\libs `
  --output experiments\raw-results\openvino-turboquant\2026-07-27\conformance\reference-capability.json `
  --timeout-seconds 300 `
  --interval-ms 100 `
  --minimum-available-ram-mb 2048
```

Result: exit 0, status `published`, start
`2026-07-27T23:49:30.600733Z`, end
`2026-07-27T23:49:34.462707Z`.

## Accepted run measurements

| Evidence | Run 1 | Run 2 |
|---|---:|---:|
| Root PID | 2216 | 8216 |
| GTest time | 0.960 s | 0.686 s |
| Workload sampling window | 1.0055738 s | 0.7134321 s |
| Required CPU rows from window | 9 | 6 |
| Observed CPU rows | 10 | 6 |
| Interval-delta rows | 9 | 5 |
| Deadline sequence | 100..1000 ms | 100..600 ms |
| Every deadline delta | 100 ms | 100 ms |
| Peak deadline lateness | 24.0077 ms | 17.8235 ms |
| GPU rows | 1 | 1 |
| GPU engine query | succeeded | succeeded |
| GPU memory query | succeeded | succeeded |
| Minimum available RAM | 7,611.285 MiB | 7,602.527 MiB |
| Workload Job survivors | 0 | 0 |
| Sampler Job survivors | 0 | 0 |

Both runs:

- used distinct 32-hex nonces and distinct root PIDs;
- observed only their root workload PID and no child workload;
- passed exactly
  `TurboQuantStatefulGraph.PersistsOnlyCompressedStateForOneHundredSteps`;
- exited workload and sampler with code 0;
- recorded no timeout, low-memory stop, emergency stop, or validation error;
- used the clean executable SHA-256 above and OpenVINO runtime DLL SHA-256
  `7a1f9291900ed976dc72d2deed22d6bd3a2abe90ca567f25d645ae82ec1b2195`
  without drift;
- produced matching two-element output-hash arrays
  `["d7027879e0667158", "d7027879e0667158"]`;
- proved four matched Reference-layer states across steps 1, 2, 50, and 100.

At step 100, the reconciled allocation was 2,800 payload bytes, 3,200 norm
bytes, 3,200 metadata bytes, 25,600 full-precision-equivalent bytes, and
25,600 decoded scratch bytes. The combined controlled-utilization record has
16 CPU rows, 14 interval deltas, and two successful GPU observations.
Zero GPU counters is reported as a successful zero-result query, not as
evidence of GPU execution; the tested device is CPU and the runtime layer type
is Reference.

The attempt's recorded canonical hash matches the independently recomputed
file hash. Re-running the current reconciliation code over both raw `run.json`
files, fixing only `generated_utc` to the recorded value and adding the
main-controller metadata, reproduced the canonical JSON exactly.

The immediate post-run process scan found zero compiler, linker, or
`tests_continuous_batching` processes. The heavy slot was explicitly returned
to Task 6 only after that cleanup check.

## Verification

The new fail-closed regression tests were observed RED before implementation:

- weakened timeout, RAM floor, measured RAM, or nested `valid: false`:
  4 failed because no exception was raised;
- altered artifact mapping or mismatched run ID:
  2 failed because no exception was raised.

After implementation, those focused checks passed. The complete Task 5 suite
then passed twice in fresh sequential invocations:

```text
python -m pytest -q tests/test_openvino_reference_capability.py
79 passed in 17.17s

python -m pytest -q tests/test_openvino_reference_capability.py
79 passed in 17.26s
```

Additional checks:

```text
python -m py_compile scripts/testing/run_openvino_reference_capability.py
exit 0

git diff --check -- scripts/testing/collect_process_utilization.ps1 scripts/testing/run_openvino_reference_capability.py tests/test_openvino_reference_capability.py
exit 0
```

One earlier non-accepted suite invocation overlapped the independent audit's
suite. Their identical fixed fixture nonces opened the same globally named
Windows Job objects, causing one timeout-fixture infrastructure failure in
each process. Both cleanup records proved zero survivors. No concurrent suite
was used as acceptance evidence; the two sequential 79-test passes above are
the accepted verification.

## Independent review

The read-only code audit initially found three publication-boundary gaps:
configured/measured resource limits, nested validity, and artifact/run-ID
consistency. Each was reproduced with a synthetic accepted tamper, fixed
test-first, and independently rechecked. The post-fix code-audit verdict was
PASS.

The independent raw-attempt audit verdict was also PASS, with zero
contradictions or missing declared artifacts. It independently:

- recomputed the canonical, cache, executable, runtime DLL, superseded-v2,
  and external build-log hashes;
- verified all three provenance observations against the currently clean
  checkout and confirmed no project reference to the old dirty checkout;
- reconciled the two distinct PIDs/nonces, exact commands, raw CSV rows,
  window-derived coverage, interval definitions, deadlines, GPU timestamps,
  GTest source declaration, RAM minima, Job cleanup, state allocations, and
  canonical embedded run records;
- found all 13 declared artifacts in both run directories; and
- confirmed that the extra `utilization.stop` file is a valid control sentinel,
  not a missing manifest entry.

## Residual limitations

- The source/cache/executable/log chain is strong provenance evidence, not
  cryptographic compiler attestation.
- Integration tests using fixed nonces must not run concurrently because
  Windows Job names are global.
- Controlled CPU figures describe this conformance run only and must not be
  used as throughput or latency benchmarks.
- The FNV output hashes cannot be regenerated without persisted raw tensor
  bytes; cross-process equality and every persisted state/allocation field
  were independently reconciled.
- Successful zero-result GPU queries prove truthful query completion, not GPU
  execution. This capability test intentionally ran on CPU.
