# Task 16 report: separate TurboQuant worker

## Result

Task 16 is complete locally. The repository now contains a distinct
`openvino.turboquant/1` CLI worker, a separate sealed runtime/worker closure,
typed activation evidence, an exact fail-closed activation verifier, and a
manual-only trusted UCL workflow. The official `openvino.official/1` route and
its package remain independent.

This report does not claim real Granite TurboQuant acceptance. The local tiny
fixture has no source-model SDPA nodes, so its otherwise successful two-turn
run is deliberately rejected as `turboquant_activation_unverified`. Only an
authorized workflow run using the pinned Granite package can produce
`turboquant_active`. Matched quality, repeatability, context scaling, memory,
TTFT, throughput, cancellation, cleanup, and corruption evidence remains an
external Task 16 acceptance prerequisite. External security and licence review
also remains open, so the worker is not registered or distributed by the app.

## Implementation

- Added protocol-scoped TurboQuant build evidence tied to exact OpenVINO source,
  implementation, patch-series, runtime-manifest, and worker-manifest digests.
- Added the exact TBQ4 runtime option and a typed per-turn activation event with
  requested/actual codecs, CPU-SDPA/head-dimension identity, executed profiling
  dispatches, encoded-record accounting, packed/full-precision byte accounting,
  model SDPA node count, profiling origin, and a forced-scalar negative.
- Required one activation event before each successful TurboQuant turn and
  rejected TurboQuant runtime/evidence on the official protocol.
- Added `OpenVinoTurboQuant.Worker.exe` plus a delayed
  `OpenVinoTurboQuant.Probe.dll`. The worker reuses the reviewed protected
  process/session facade while compiling as a distinct protocol and binary.
- Applied exact CPU SDPA `u4`/`TURBO` key/value properties. The probe executes a
  real two-turn stateful SDPA fixture, counts only typed `EXECUTED` profiling
  records, proves packed TurboQuant state opacity against readable scalar-u4
  state, validates 32-byte versus 128-byte records, and fail-closes unless every
  source-model SDPA input has head dimension 64 and retains exact fused cache
  nodes after compilation.
- Added a separate Release x64 build/stage pipeline and exact manifest scripts.
  The outer worker manifest is cryptographically re-bound to every entry in the
  embedded Task 15 runtime manifest; build/stage roots cannot overlap each
  other, the repository, or controlled input roots.
- Added a strict activation verifier that binds the reviewed commit, package
  manifest digest, model digest and length, exact build closure, CPU device,
  activation counters, two completed turns, streaming, and cleanup.
- Added a manual protected-environment UCL workflow with pinned actions, exact
  commit verification, trusted runner labels, controlled inputs, the repository's
  .NET 10 `--project` test syntax, forced-negative gating, always-run cleanup,
  and a sanitized 30-day typed artifact. Upload cannot occur unless activation,
  negative-control, and cleanup steps all pass.

## Inline audit corrections

1. Added an executable regression proving an attacker cannot change a runtime
   file and rebase only the outer worker manifest around the changed bytes.
2. Rejected overlapping build, stage, source, archive, and runtime-stage roots
   before creating operation-owned directories.
3. Corrected the UCL workflow from legacy positional `dotnet test` syntax,
   which discovered zero tests under Microsoft Testing Platform, to the required
   `dotnet test --project` form and removed the unsupported no-restore assumption.
4. Bound package-manifest digest and model length into the typed activation
   artifact and verifier.
5. Required the forced-negative step to succeed before artifact upload.
6. Bounded hostile encoded-record values before cache-byte multiplication so
   overflow maps to `OpenVinoProtocolException` rather than escaping as an
   untyped runtime exception.

## Verification

- Fresh MSVC Release x64 worker build:
  `C:\openvino-o1-task16-worker-build-f` ->
  `C:\openvino-o1-task16-worker-stage-f`; build disposition
  `turboquant_worker_built`.
- Fresh native activation CTest: 1/1 passed.
- Prior MSVC ASAN activation probe: passed in
  `C:\openvino-o1-task16-worker-asan`.
- Contracts: 167/167 passed with zero skipped.
- Worker client: 13/13 passed with zero skipped.
- TurboQuant managed/native integration: 8/8 passed with zero skipped against
  the exact final stage.
- Official native regression: 7/7 passed.
- Final worker manifest: `turboquant_worker_manifest_valid`.
- Modified probe DLL: `turboquant_worker_manifest_invalid`, exit 1.
- Local two-turn fixture: managed run passed, then activation verifier returned
  `turboquant_activation_unverified`, exit 1, with `modelSdpaNodeCount = 0`.
- All affected PowerShell scripts parse without errors; the full contract suite
  parses the new workflow as valid manual-only YAML.

## Open external gates

- `UCL-01`: no workflow dispatch was authorized or performed.
- Real pinned Granite activation and matched official/TurboQuant quality,
  repeatability, context, memory, TTFT, throughput, cancellation, cleanup, and
  corruption evidence is absent.
- `TQ-01`/`LIC-01`: external security and licence approvals are pending.
- App registration, packaging, distribution, and an `Active` capability claim
  remain forbidden until all of those gates close on the same immutable commit.
