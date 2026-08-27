# UCL cross-route native validation final handoff

Date: 2026-08-27

Status: **BLOCKED — do not claim final native acceptance**

This handoff records the completed integration work and the exact acceptance
boundaries observed on the UCL laptop. It deliberately distinguishes verified
build and route evidence from the two conditions that prevent the requested
same-package, both-route completion claim.

## Immutable source identities

- Integration branch: `integration/ucl-cross-route-final-validation-v1`
- Frozen base: `f599c358181bd4da44087ab0c64d36d02d23316a`
- Frozen base tree: `1b0fd485301626f75866809ea939003da386114a`
- Application evidence commit: `107b74cdf9e4177dd975f4e38742e05e5ea54b03`
- Application evidence tree: `aa3d1d4bf74353fa332268d83fa92d96567ff053`
- OpenVINO route source commit: `b586cb3855514bdb058ad47b5d3d08475170942e`
- OpenVINO integrated commit: `c9fc39e6d46d52cd16e4e56531a03b8bd60ff527`
- GGUF route source commit: `57ab54c5d0701f2fcaa8f4d23108e71aaadb12f4`
- GGUF integrated commit: `5a97e414f84f2f947c309202ec754022d1badbf8`

No push or merge to the protected main branch was performed.

## Integrated changes

- Preserved the current shared import, inspection, hardware, compatibility,
  optimisation, onboarding, navigation, theme and chat implementation.
- Integrated the OpenVINO V2 validation-preservation route changes.
- Integrated the GGUF native toolchain and verification changes.
- Made verified converter and TurboQuant closures mandatory in production and
  cross-route evidence builds, matching the existing official-worker and GGUF
  quantiser fail-closed packaging boundaries.
- Closed the legacy public optimisation-plan issuance surface without widening
  production authority. The retained legacy seam is internal and visible only
  to the two named test assemblies.
- Corrected GGUF quantiser MASM pinning and worker-client test invocation.
- Corrected the GGUF package-closure verifier to use PowerShell invocation
  success state instead of a stale native exit code.
- Updated the chat static interaction assertion to match the existing
  `PreviewKeyDown` handling and to reject a duplicate `KeyDown` handler.

## Trusted native inputs

All digests are SHA-256. Local absolute paths are intentionally omitted.

| Payload | Trusted digest | Result |
|---|---|---|
| OpenVINO converter manifest | `b975313fccb90250cdf3ba58416b1ea019a93c7889c34a5b425df54df7056983` | manifest and inventory verified |
| OpenVINO official worker A manifest | `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3` | manifest and inventory verified |
| OpenVINO official worker B manifest | `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3` | manifest and inventory verified |
| OpenVINO TurboQuant worker manifest | `d4748d69ecacf13b1e1d6756f86df79d3a43e361bc10a0676ca270c9d08b9492` | manifest and inventory verified |
| OpenVINO TurboQuant runtime manifest | `3b26a537ddfedad6d2f75fd1bf4578703a314f2544f4a81a6acfcade8d330c85` | verified |
| GGUF quantiser manifest | `be44b38ca5ce66470a657f7d41d99b11c9233a5846f83a71c99b87672021765d` | verified |
| Packaged GGUF runtime manifest | `189f64573e69f50d69425769e1dac471b06df4e495f80d5103236ab994db8369` | verified |
| IBM Granite 4.1 3B Q4_K_M GGUF | `662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29` | 2,099,501,664 bytes; exact source digest matched |

The supplied OpenVINO outer archive had no matching sibling digest file. Its
measured digest was
`722135bc131e93d16f332909cf0c9b293231e21b3b3c65ae73560e9e6c249262`.
The inner inventory was verified, but outer-transfer trust is not claimed.

## Final production build

Visual Studio Community MSBuild built the Release x64 application at the
application evidence commit. Package generation remained enabled and signing
remained disabled as required. The build supplied all verified stage roots and
all five caller-pinned manifest digests. It did not use a diagnostic packaging
bypass.

The build completed successfully. It emitted non-fatal PRI249/PRI263 warnings
for Python package qualifier-like paths and neutral-resource selection.

- MSIX length: `551054595`
- MSIX SHA-256: `99ed6641adc8351e4578f9fddcc376c9d2a2c7730ce80c909a6e385be461c25b`
- fresh application recipe length: `6683414`
- fresh application recipe SHA-256:
  `608155d8d09447944e182159159a86273eda6457f7700e1d9c2e3ca41f3fca65`

The short-path UnitTests recipe used for the packaged-test attempt was also
fresh and verified:

- recipe length: `6681983`
- recipe SHA-256:
  `46e2e6dcb080f537af5a240ad55311dbaeebe7c263d75c5e51cfe942e18c89b3`

## Managed and native validation

| Gate | Result |
|---|---|
| Model/hardware compatibility full managed suite | 1,050 passed, 0 failed, 0 skipped |
| OpenVINO contract suite | 205 passed, 0 failed, 0 skipped |
| OpenVINO unit suite with real stages | 409 passed, 0 failed, 0 skipped |
| OpenVINO worker-client suite | 13 passed, 0 failed, 0 skipped |
| GGUF native-adapter suite with the real IBM model | 41 passed, 0 failed, 0 skipped |
| GGUF package-closure focused suite after the causal fix | 5 passed, 0 failed, 0 skipped |
| GGUF worker-process real-model smoke | 0 passed, 1 failed, 0 skipped |
| OpenVINO native campaign | 58 passed, 10 failed, 1 explicit GPU skip |

The full GGUF gate immediately before the package-closure implementation fix
reported 131 passed, 2 failed and 3 unconfigured-model skips across 136 tests.
The package-closure failure was then fixed and its complete five-test class
passed. The other failure was reproduced twice with the controlled real-model
configuration: native model loading, two streamed turns, stop, close and the
forced length completion all worked, but the next continuation emitted no text
delta. Therefore GGUF continuation acceptance is not claimed.

The OpenVINO native direct probes were serialised and observed the official
worker hello boundary followed by `worker_failed` with exit code 2. The
converter reached its own runtime integrity boundary and returned
`runtime_integrity_failed` with exit code 1. No AppLocker, Code Integrity,
loader-policy or crash-policy block was observed for those direct probes.
OpenVINO native conversion/optimisation/chat success is not claimed.

## Packaged-test and registration blocker

Visual Studio Community `vstest.console.exe` successfully deployed the fresh
short-path UnitTests recipe and registered its test package. The test host then
exited before discovery. Windows Application and Services logs identify the
cause precisely:

- .NET Runtime event 1026: the UnitTests managed DLL load raised
  `FileLoadException` with `0x800711C7` because Application Control blocked it.
- Code Integrity events 3033 and 3077: the DLL did not meet the active Custom 1
  signing level under policy ID
  `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`.

Consequently none of the required packaged filters — ModelImport,
ModelInspection, HardwareInspection, ModelHardwareCompatibility,
ModelOptimization, Onboarding, GgufRuntime, OpenVino or CrossRoute — achieved
non-zero discovery on this host. No filter-pass claim is made.

Direct registration of the final unsigned production MSIX also failed as
expected with `0x800B0100`: no signature was present. Altering the machine's
Application Control policy or inventing unsigned results was outside scope.

## Native acceptance matrix

| Route/stage | Native result | Final claim |
|---|---|---|
| GGUF real model load and ordinary streaming | passed through native adapter and worker | partial native evidence only |
| GGUF stop and length completion | passed | partial native evidence only |
| GGUF post-length continuation | no text delta, reproduced twice | failed |
| GGUF quantiser closure | manifest/digest/package verification passed | no real quantisation E2E claim |
| OpenVINO official worker | hello then `worker_failed` | failed |
| OpenVINO converter | `runtime_integrity_failed` | failed |
| OpenVINO TurboQuant closure | manifest/digest/package verification passed | no native optimisation E2E claim |
| Common registered-package journey | blocked before packaged test discovery | not run |

## UI, hardware and privacy

Shared route-neutral UI code and static UI contracts were preserved. A truthful
same-size screenshot pair and manual common-journey parity claim cannot be
produced because the final package cannot be registered/launched under the
active signing policy.

The acquired hardware helper reported its exact required version and returned
valid structured system data. Sanitised decision inputs were 15.7 GiB total
RAM, 4.41 GiB currently available RAM, GPU present, and one GPU. Raw hardware
names/output, user/host identities, model paths and private local paths are not
committed.

## Cleanup and non-claims

- Native ownership lock ended as `UCL-NATIVE-LOCK: IDLE`.
- The task-owned packaged UnitTests registration was removed.
- No task-owned test host, worker, adapter, converter or quantiser process was
  left running.
- A pre-existing debug application process outside the task worktrees was left
  untouched.
- Reproducible external stages, the verified model and generated build layouts
  were retained. One partial short-path deployment layout remains unregistered;
  an automated cleanup command was rejected by host command policy, so no
  destructive workaround was attempted.
- No final native success, visual parity, package filter, OpenVINO optimisation,
  GGUF quantisation, benchmark, energy or all-green completion claim is made.

The work is ready for rerun on a host that can execute the explicitly unsigned
packaged test payload (or with a trusted signing certificate), after the two
route-native failures above are resolved.
