# Workbook 05 Runtime Compiler-Concurrency and Evidence Repair Design

## Context

Workbook 05 Route A Runtime run `31341784206`, attempt `1`, produced the retained artifact `workbook-05-build-route-a-runtime-31341784206-1` with SHA-256 `6eb7ad07980792082480a7a0793922635d6c31b9240da2611559f5d77e3e0085`.

The artifact records two independent failures:

1. The Runtime build was terminated by the resource-safety controller after Windows commit remained above 90 percent for 10 seconds. The build record reports a peak working set of `8078163968` bytes, peak private memory of `7770116096` bytes, minimum available memory of `1960452096` bytes, maximum commit of `94` percent, and exit code `-1`.
2. The hosted validator rejected `decision.json` because `required_components` contained two path strings. The build-decision schema requires each entry in that field to be a component-status object. A Runtime decision has no lower build component dependency, so this field must remain an empty array.

The verbose build log also shows repeated compiler invocations containing bare `/MP`. The existing CMake argument `--parallel 1` limits native build-tool job concurrency, but it does not set the number of compiler processes created by MSVC for `/MP`. Microsoft documents `CL_MPCount` as the property used to choose the process count for `/MP`, and CMake documents that arguments after `--` are passed to the native build tool.

## Goals

- Keep the existing one-job CMake/MSBuild build boundary.
- Add an explicit one-process MSVC `/MP` limit using `/p:CL_MPCount=1`.
- Restore schema-valid Runtime decisions by emitting `required_components = @()`.
- Preserve the ONNX and TensorFlow frontend header checks as installation hand-off checks.
- Preserve the existing resource-safety thresholds, immutable evidence handling, source pins, disabled unrelated frontends, and scientific-claim restrictions.
- Prove both changes through focused regressions and the complete Workbook 05 and WinUI verification pipelines.

## Non-goals

- Do not weaken or raise the 90 percent Windows commit safety threshold.
- Do not change the machine page-file configuration.
- Do not change OpenVINO Runtime or GenAI source commits.
- Do not enable GPU, NPU, samples, tests, or unrelated framework frontends.
- Do not run Granite, TurboQuant, QJL, PolarQuant, performance benchmarks, or quality scoring in this repair.
- Do not delete or overwrite the failed Runtime artifact or any earlier accepted/failed evidence.

## Design

### 1. Two-layer concurrency control

The Runtime build command will retain:

```text
--parallel 1
```

and append this native MSBuild option after CMake's `--` delimiter:

```text
/p:CL_MPCount=1
```

The resulting command boundary is:

```text
cmake --build <build-root> --config Release --parallel 1 --verbose -- /p:CL_MPCount=1
```

`--parallel 1` controls concurrent native build jobs. `CL_MPCount=1` controls the number of compiler processes used when generated projects contain `/MP`. The text-only command evidence will retain the exact property, so the live rerun can prove that the intended control was supplied.

### 2. Schema-correct Runtime decision

`Write-RouteARuntimeDecision` will emit:

```powershell
required_components = @()
```

This reflects the meaning of the field: lower build components required by this decision. The two frontend header paths are not components and must not be represented there.

The existing `$RequiredGenAIFrontendHeaders` array and post-install file checks remain unchanged. A Runtime installation still cannot be marked `Passed` unless both exact headers exist:

- `runtime/include/openvino/frontend/onnx/extension/conversion.hpp`
- `runtime/include/openvino/frontend/tensorflow/extension/conversion.hpp`

### 3. Regression strategy

Focused contract tests will require:

- `--parallel 1` to remain present;
- the build command to forward `/p:CL_MPCount=1` after `--`;
- higher compiler counts not to appear;
- Runtime decisions to use an empty `required_components` array;
- the two frontend header hand-off checks to remain present;
- all previously disabled surfaces to remain disabled.

The test-first sequence is:

1. Add the new expectations and prove the current production script fails them.
2. Apply the two-line production correction plus explanatory comments.
3. Prove the focused tests pass.
4. Run the complete Workbook 05 repository-controlled gate.
5. Run the normal WinUI application build and all application tests through the pull-request workflow.

### 4. Live validation boundary

Repository tests can prove the command and evidence contracts, but they cannot prove that the expanded upstream Runtime completes on the 16 GB Lenovo. After merge, a fresh `route-a-runtime` run is required. Acceptance requires:

- configure exit code `0`;
- build exit code `0`;
- install exit code `0`;
- resource-safety stop `false`;
- both frontend headers present in the installed Runtime;
- `decision.json` status `Passed`;
- hosted untrusted-data validation successful;
- artifact SHA-256 independently matching GitHub.

Only after that new Runtime is accepted may `route-a-genai` be run against it.

## Risks and mitigations

- **Longer build time:** limiting `/MP` to one process may substantially increase compilation time. This is accepted because the previous run exhausted the machine's safe commit boundary. The workflow timeout remains the controlling upper limit.
- **Property not materialising as expected:** the command record proves the property was forwarded, while the live resource trace and verbose compiler log determine whether the rerun behaves safely. No success claim is made from the static repair alone.
- **Evidence regression:** the hosted validator remains independent and fail-closed. An invalid decision record will still fail the workflow.

## Acceptance criteria

The repair is repository-ready when the focused regression, complete Workbook 05 gate, and normal WinUI build/test workflow all pass on the exact final branch head. It is operationally accepted only after the post-merge Route A Runtime workflow produces a valid `Passed` artifact under the new concurrency controls.