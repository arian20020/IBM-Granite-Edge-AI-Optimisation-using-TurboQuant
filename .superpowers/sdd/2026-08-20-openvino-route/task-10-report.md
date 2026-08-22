# Task 10 report — explicit Intel GPU execution

## Outcome

The official worker now accepts only `CPU`, `GPU`, or canonical enumerated
`GPU.n` device identities. `AUTO`, `HETERO`, `MULTI`, alternate device types,
case variants, whitespace, and noncanonical indices are rejected at both the
managed and native protocol boundaries.

Before a session is published, the native worker enumerates OpenVINO devices,
requires the exact requested device, compiles the locked main IR on that exact
device, reads `ov::execution_devices`, and requires a single ordinal-equal
result. The same exact device string is passed to both validation and generation
`LLMPipeline` instances. An absent or unsupported GPU/driver returns
`runtime_device_unavailable`; an execution-device disagreement returns
`runtime_device_mismatch`; neither path retries on CPU.

The closed worker distribution now includes
`openvino_intel_gpu_plugin.dll` in its manifest, packaged inventory, app trust
map, and x64 machine checks. The app-visible capability remains `CPU`: no GPU
choice is exposed until `GPU-01` supplies physical Intel GPU evidence.

## Evidence boundary

The manual UCL workflow has an optional GPU branch requiring exact `GPU-01`
authorization, an explicit `GPU`/`GPU.n`, and fixed Intel adapter name plus
driver version. The retained campaign checks the installed adapter without
installing or changing a driver, runs the nonexistent-device negative and the
physical one-turn/two-turn/cancellation/cleanup test, and emits a separate
`gpu.json` artifact.

`Test-OpenVinoGpuEvidence.ps1` binds that document to the exact commit, requested
and measured execution device, fixed adapter/driver, GPU plugin digest, runtime
builds, SDPA configuration digest, named passing GPU tests, forced-negative
results, and cleanup disposition. It uses bounded closed JSON and DTD-disabled
TRX parsing and rejects path/host/account/prompt/answer/native-output fields.

This workstation reports NVIDIA and AMD graphics only. Therefore the physical
GPU test is deliberately inconclusive, `GPU-01` remains open, no GPU evidence
or acceptance is claimed, and the stable CPU route remains unaffected.

## TDD and verification

- RED: 13/17 new device-contract rows failed because arbitrary device strings
  were accepted.
- GREEN: 18/18 GPU device/sequence contract rows passed.
- RED: the fresh process negative returned `runtime_protocol_failed` instead of
  `runtime_device_unavailable`.
- GREEN: the explicit nonexistent GPU process negative passed without CPU
  fallback.
- Independent clean native closures A and B: 7/7 each.
- Release x64 contracts: 162/162.
- Release x64 route/unit suite: 181/181.
- Release x64 WorkerClient suite: 13/13.
- Release x64 protected-process suite: 42 passed, 0 failed, 1 deliberately
  skipped physical-GPU test (`GPU-01` absent).
- Release x64 packaged app build against the fresh GPU-aware stage manifest:
  passed.
- GPU evidence positive/hostile verifier contract: passed.
- Affected PowerShell scripts: zero parser errors.
- `git diff --check`: clean apart from repository line-ending notices.
- Worker/fixture process residue: zero.

Fresh closures:

- `C:\Users\Arian\AppData\Local\Temp\granite-o1-inline-task10-build-a`
- `C:\Users\Arian\AppData\Local\Temp\granite-o1-inline-task10-stage-a`
- `C:\Users\Arian\AppData\Local\Temp\granite-o1-inline-task10-build-b`
- `C:\Users\Arian\AppData\Local\Temp\granite-o1-inline-task10-stage-b`

Review was performed inline per user direction; no subagent was used.
