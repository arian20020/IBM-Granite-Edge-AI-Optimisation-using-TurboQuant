# Workbook 05 Route B Phase 2 Closure

## Decision

```text
Route: route-b-experimental-qjl-polar
Source: EgorDuplensky/openvino@1827f6458d049de11c1a8203c793af67c99935dc
Phase 2 status: Blocked
Runtime build authorised: no
GenAI compatibility build authorised: no
Model execution authorised: no
```

Route B is closed as blocked for the current Workbook 05 Phase 2 documented-build matrix. Route A remains independent and may continue through its own Runtime and GenAI gates.

## Controlling workflow outcome

- Workflow: `Workbook 05 Route B repair`
- Workflow run: `31088162027`
- Exact project head: `8d2d7e8039fc084012fa66a10a78a08a8fb39a2f`
- Latest run attempt: `6`
- Final workflow conclusion: `failure`
- Latest collector conclusion: `cancelled`
- Latest hosted-validator conclusion: `cancelled`
- Repository contract job: `success`

The failed workflow conclusion is not being interpreted as an algorithm failure. The latest evidence-producing jobs were cancelled, so the required executable evidence was never completed.

## Retained attempt 4 artifact

- Artifact name: `workbook-05-route-b-repair-31088162027-4`
- Artifact ID: `8974029140`
- Size: `17,591` bytes
- Digest: `sha256:981bbaf75af4a4bd254d81cb2fe2c496f4971bfdd8423ed1ce65a5a1988213a8`
- Retention expiry: `2026-09-05T16:02:11Z`

### What the artifact proves

The preserved artifact contains evidence that:

1. The external source was `https://github.com/EgorDuplensky/openvino.git` at exact commit `1827f6458d049de11c1a8203c793af67c99935dc`.
2. Thirty-six recursive submodules were recorded as complete.
3. The source was clean before the repair.
4. Exactly one source file changed:
   `src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake`.
5. No algorithm file was reported as changed.
6. The pinned benchmark already contained the unconditional f32 safeguard and the K=f32/V=TurboQuant asymmetric safeguard.
7. CMake configuration completed with `Visual Studio 17 2022`, `x64`, tests enabled, CPU-specific target-per-test enabled and Intel GPU disabled.
8. `target-membership.json` records both the shared class source and x64 instance source for `ov_cpu_func_subgraph_concat_sdp_turboq`.

These observations make the repair technically informative, but they do not meet the executable-candidate gate.

### What the artifact does not prove

The retained artifact has:

- no `decision.json`;
- no `manifest.sha256`;
- no compiled-target result;
- no test-discovery summary with a non-zero count;
- no six-case test result;
- no baseline f32 execution result;
- no QJL3 or QJL4 execution result;
- no PolarQuant3 or PolarQuant4 execution result;
- no asymmetric f32/TurboQuant execution result;
- no packed-record-size execution evidence;
- no selected-codec versus requested-codec evidence;
- no no-fallback evidence;
- no independently completed hosted validation.

The artifact therefore cannot be classified as `ExecutableCandidate`.

## Phase 2 consequence

The Phase 2 Route B Runtime workflow must remain disabled because its prerequisite is an independently validated and project-owner-accepted `ExecutableCandidate` BR8 artifact. That prerequisite does not exist.

The Route B GenAI compatibility path is also blocked. A configure-only Runtime repair artifact cannot establish a reviewed Runtime/GenAI source pair or GenAI compatibility.

No conclusion is drawn about whether QJL or PolarQuant would compile or execute after a future successful repair rerun. The current conclusion is narrower:

> The available evidence is incomplete, so Route B is not admitted to Phase 2.

## Reopening criteria

Route B may be reopened only through a new immutable repair run that provides all of the following:

1. A complete SHA-256 manifest.
2. A final `decision.json` classified as `ExecutableCandidate`.
3. Successful narrow-target compilation.
4. Non-zero test discovery.
5. Six required cases, each executed, passed, and unskipped.
6. Exact source, repair, target-membership and packed-record evidence.
7. Independent hosted validation of the exact artifact.
8. Explicit project-owner acceptance of that exact digest.

Until then, the machine-readable campaign decision remains `Blocked` and all model, activation, packed-storage, performance and quality authorisations remain false.
