# Workbook 05 Route A Runtime Narrow-Frontend Repair Design

**Date:** 2026-08-08  
**Branch:** `fix/workbook-05-narrow-runtime-frontends`  
**Base:** `45c2f44b2e5bdb200a70702851be3340c4a082c6`  
**Scope:** Route A OpenVINO Runtime build configuration and its verification contract only

## 1. Purpose

Route A Runtime has now crossed two earlier infrastructure boundaries and reached a normal upstream build failure. The accepted evidence from workflow run `31236734532` shows that reducing Runtime build parallelism from two jobs to one prevented the reviewed resource-safety controller from terminating compilation. The build then ran for almost four hours before Windows App Control / Device Guard blocked the locally built OpenVINO `protoc.exe` used by the ONNX/Protobuf generation path.

This repair keeps Windows application-control enforcement intact. Instead of weakening the machine security policy, Route A Runtime will explicitly build only the OpenVINO frontend surface required by this project's current Runtime-to-GenAI path.

The change is intentionally narrow. It does not claim that every optional OpenVINO feature is unnecessary in general; it states only that the current Route A experiment does not need the framework frontends that caused the Protobuf compiler path to be built and executed.

## 2. Production evidence that motivates the change

The relevant live attempt is:

```text
workflow run: 31236734532
workflow: Workbook 05 documented build
main commit: 45c2f44b2e5bdb200a70702851be3340c4a082c6
Runtime collector job: 93050652069
independent Runtime validator job: 93077075729
artifact: workbook-05-build-route-a-runtime-31236734532-1
artifact ID: 9018788358
artifact SHA-256: bb1944ab207fac6ea74caedd0dc3008daead816c20e038ce49dc8b1f8adb4cba
```

The evidence pipeline itself completed successfully: the collector retained the attempt, the hosted validator accepted the bundle as untrusted data, and the workflow was green. That green workflow result is not a Runtime-build success claim. `decision.json` correctly records the Runtime result as `Failed` because the build command returned exit code `1`.

The build resource record establishes a new first divergence:

- Runtime build parallelism was `1`.
- The actual OpenVINO build ran for approximately 3 hours 55 minutes.
- `safety_stop_triggered` was `false`.
- Windows committed-memory use peaked around the existing safety boundary but did not satisfy the reviewed consecutive-sample termination rule.
- The first decisive build failure was not a memory stop.
- The generated OpenVINO `protoc.exe` was blocked by the machine's Device Guard / application-control policy.
- MSBuild then reported `MSB8066` for the ONNX protocol-buffer generation target.
- Installation was not reached, so Route A Runtime remains unaccepted and Route A GenAI remains blocked.

This means the memory experiment did what it was designed to test: it moved the first divergence away from the previous resource-safety termination. The next repair must address the new executable-policy boundary without changing unrelated controls.

## 3. Primary-source basis

The design is based on the exact pinned upstream sources, not on assumptions about current OpenVINO defaults.

The Runtime source remains:

```text
openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4
```

At that revision, OpenVINO exposes independent CMake options for the IR, ONNX, PaddlePaddle, TensorFlow, TensorFlow Lite, PyTorch and JAX frontends. The pinned source also defines `ENABLE_SYSTEM_PROTOBUF` as a dependent option used when ONNX, PaddlePaddle or TensorFlow frontend support requires Protobuf. Its default is OFF because OpenVINO normally builds its own static Protobuf rather than depending on a system installation.

OpenVINO's custom-compilation documentation explicitly presents these frontends and samples as configurable build components. Official 2026 deployment documentation likewise treats the IR frontend as the component that provides OpenVINO IR model loading.

The exact pinned GenAI source remains:

```text
openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f
```

Its top-level CMake asks OpenVINO for the `Runtime` and `Threading` components. It does not request the ONNX, PaddlePaddle, TensorFlow, TensorFlow Lite, PyTorch or JAX frontends. The project's Route A GenAI orchestration also consumes one accepted `OpenVINOConfig.cmake` from the Runtime install and refuses to run unless the Runtime decision is `Passed`.

Microsoft's App Control for Business guidance treats executable blocking as an application-control policy decision and directs diagnosis through the Code Integrity operational log. Microsoft also provides policy mechanisms for intentionally authorizing trusted applications. This design deliberately does not disable App Control or weaken the machine-wide policy, because the blocked executable belongs to build surface that the current Route A path can avoid entirely.

Professional references used for this design:

- Microsoft Learn, **App Control debugging and troubleshooting guide**.
- Microsoft Learn, **Application Control for Windows / App Control for Business**.
- OpenVINO 2026 documentation, **Deploy Locally**.
- Exact pinned OpenVINO `cmake/features.cmake`.
- Exact pinned OpenVINO `docs/dev/cmake_options_for_custom_compilation.md`.
- Exact pinned OpenVINO GenAI `CMakeLists.txt` and `src/docs/BUILD.md`.

## 4. Selected design

Route A Runtime remains a source build on the Lenovo self-hosted runner. The exact source commit, Visual Studio generator, Release/x64 configuration, Python version, external workspace, one-job build concurrency and resource-safety controls remain unchanged.

The configure command will explicitly require the project-relevant Runtime surface and explicitly disable the unused framework-import surface.

### 4.1 Controls that remain enabled or explicitly required

```text
ENABLE_INTEL_CPU=ON
ENABLE_OV_IR_FRONTEND=ON
ENABLE_PYTHON=ON
ENABLE_WHEEL=OFF
```

`ENABLE_PYTHON` remains ON for this repair. The current Route A GenAI configuration enables its Python bindings, and the pinned source-build guidance describes the OpenVINO Python environment as part of the source-built hand-off. Removing Runtime Python at the same time as fixing the Device Guard divergence would introduce another independent compatibility variable.

### 4.2 Controls explicitly disabled

```text
ENABLE_INTEL_GPU=OFF
ENABLE_INTEL_NPU=OFF
ENABLE_TESTS=OFF
ENABLE_FUNCTIONAL_TESTS=OFF
ENABLE_SAMPLES=OFF
ENABLE_JS=OFF

ENABLE_OV_ONNX_FRONTEND=OFF
ENABLE_OV_PADDLE_FRONTEND=OFF
ENABLE_OV_TF_FRONTEND=OFF
ENABLE_OV_TF_LITE_FRONTEND=OFF
ENABLE_OV_PYTORCH_FRONTEND=OFF
ENABLE_OV_JAX_FRONTEND=OFF
```

The IR frontend remains ON because Route A is for OpenVINO IR consumption. The six framework frontends above are disabled because the current application/GenAI hand-off does not require them, and the production failure occurred in the ONNX/Protobuf build path.

`ENABLE_SAMPLES` changes from ON to OFF because the Route A build exists to produce the Runtime consumed by the application/GenAI stage, not OpenVINO sample applications. `ENABLE_JS` is explicitly OFF for the same least-functionality reason; the current route uses C++/Python-facing Runtime and GenAI components, not JavaScript bindings.

No additional plugins such as AUTO, MULTI, HETERO, AUTO_BATCH or TEMPLATE are changed in this repair. They were not the proven first divergence, and altering them would broaden the experiment beyond the approved causal boundary.

## 5. Protobuf evidence rule

The authoritative causal acceptance controls are the six frontend flags, not `ENABLE_SYSTEM_PROTOBUF` by itself.

The pinned OpenVINO source declares `ENABLE_SYSTEM_PROTOBUF` as a dependent option whose applicability depends on ONNX, PaddlePaddle or TensorFlow frontend use. Therefore the evidence contract will not require that `ENABLE_SYSTEM_PROTOBUF` must exist in `CMakeCache.txt` after all relevant frontend consumers have been disabled.

If the pinned configure does materialize `ENABLE_SYSTEM_PROTOBUF`, `cmake-cache-summary.json` may record it and validation must reject the configuration if its value is `ON`. If it is absent, that absence is not a failure provided the direct frontend controls are present and exactly OFF.

This avoids a brittle proxy assertion while still preventing an unexpected system-Protobuf substitution.

## 6. Evidence and fail-closed behavior

The producer must not trust the configure command merely because it was requested with the right arguments. After CMake returns success, the generated `CMakeCache.txt` remains the authoritative materialized-control evidence.

`cmake-cache-summary.json` will include and verify at least:

```text
CMAKE_GENERATOR
CMAKE_GENERATOR_PLATFORM
ENABLE_INTEL_CPU
ENABLE_INTEL_GPU
ENABLE_INTEL_NPU
ENABLE_TESTS
ENABLE_FUNCTIONAL_TESTS
ENABLE_SAMPLES
ENABLE_PYTHON
ENABLE_WHEEL
ENABLE_JS
ENABLE_OV_IR_FRONTEND
ENABLE_OV_ONNX_FRONTEND
ENABLE_OV_PADDLE_FRONTEND
ENABLE_OV_TF_FRONTEND
ENABLE_OV_TF_LITE_FRONTEND
ENABLE_OV_PYTORCH_FRONTEND
ENABLE_OV_JAX_FRONTEND
Python3_EXECUTABLE
```

The Runtime stage must return `Blocked` before compilation if the materialized cache does not match the reviewed controls. It must not silently proceed when an expected frontend is missing, remains ON, or the CPU/IR requirements are not present.

A normal non-zero CMake build result remains `Failed`. A resource-controller termination remains `Infrastructure interrupted`. Integrity/provenance problems remain `IntegrityFailure`. These classifications are not changed by this repair.

The independent hosted validator remains unchanged and continues treating the uploaded bundle as untrusted data.

## 7. Security boundary

This repair will not:

- disable Device Guard, Smart App Control or App Control for Business;
- add allow-all or broad path-based application-control rules;
- change registry/security-policy state;
- change the 90% maximum commit boundary or five-consecutive-sample rule;
- bypass the hosted independent validator;
- run a downloaded arbitrary executable outside the reviewed build process;
- copy Runtime binaries into GitHub artifacts or the repository.

The design follows least functionality: when a security control blocks an executable belonging to a feature the project does not require, remove that unnecessary feature from the build instead of weakening the security control.

## 8. Scientific boundary

This remains a build-feasibility repair only.

A successful narrow Runtime build would establish only that the exact pinned OpenVINO Runtime can configure, compile and install on the reviewed Lenovo environment with the approved CPU/IR build surface. It would not establish any Granite model result or any TurboQuant claim.

The following remain unauthorized:

```text
Granite inference claim
QJL activation claim
PolarQuant activation claim
TurboQuant activation claim
packed KV-storage claim
no-fallback claim
memory/context-length claim
TTFT claim
tokens-per-second claim
quality claim
```

## 9. TDD strategy

Production behavior will not be changed first.

The existing Route A Runtime PowerShell contract suite will receive a focused narrow-surface regression test before the Runtime script is modified. The RED contract will require the exact reviewed flags in the configure argument array and will require the cache-verification surface to verify those same controls.

The RED checkpoint must fail on current production for the intended reason: current Runtime still has `ENABLE_SAMPLES=ON` and does not explicitly disable the six framework frontends or JavaScript, nor explicitly require CPU and IR frontend controls.

Only after that clean RED result will the Runtime producer be changed.

The GREEN implementation will be minimal:

1. update the Runtime configure argument array;
2. extend the cache-control name list;
3. extend the exact `$cacheMatches` expression;
4. change the samples control from ON to OFF;
5. preserve all unrelated paths, pins, build/install commands and safety behavior.

No workflow YAML change is expected.

## 10. Verification sequence

Before another Lenovo OpenVINO build is allowed, the exact implementation head must pass all repository-level acceptance boundaries:

1. complete Workbook 05 test discovery, including the new narrow-Runtime contract;
2. executable PowerShell CMake-cache regression;
3. focused workflow/security contracts;
4. PowerShell module/import and strict-text checks;
5. `git diff --check`;
6. normal WinUI application restore/build;
7. packaged WinUI/unit-test execution with every discovered test passing;
8. independent download, SHA-256 verification and TRX counter parsing of the unit-test artifact;
9. exact PR diff review and review-thread check;
10. merge only the verified expected head;
11. fresh push-triggered `main` Build-and-test run;
12. independent post-merge artifact digest/TRX verification.

Only then may one brand-new `route-a-runtime` workflow be dispatched. The previous run `31236734532` must not be rerun because it is bound to the older broad-frontend configuration.

## 11. Live experiment acceptance criteria

A new Route A Runtime attempt is accepted as `Passed` only if all of the following occur in the same attempt:

- exact source provenance and recursive submodules verify;
- CMake configure exits zero;
- the generated cache exactly matches the narrow CPU/IR controls;
- Runtime build exits zero without a resource-safety stop;
- Runtime install exits zero without a resource-safety stop;
- the install contains hashable binary outputs;
- `decision.json` records `Passed`;
- all scientific authorization flags remain false;
- the independent hosted validator accepts the exact same-attempt evidence artifact.

If a new normal compiler/build failure appears, that becomes the next first divergence and is debugged separately. The repair must not respond by weakening Device Guard or the resource boundary without a new evidence-based design.

## 12. Route A GenAI hand-off

Route A GenAI remains unchanged during this repair.

It may proceed only after a Runtime attempt produces an accepted `Passed` decision and retained `i-ov` install. The GenAI stage will then consume that exact Runtime installation read-only and validate the exact `OpenVINOConfig.cmake` as already designed.

The narrow Runtime build therefore changes build surface, not the prerequisite contract: GenAI still requires an independently accepted Runtime result before it can start.

## 13. Engineering-practice basis

This design follows the project's engineering references:

- **Why Programs Fail, 2nd ed.**: follow the newly observed first divergence instead of continuing to tune the previous memory hypothesis.
- **Designing Secure Software**: least functionality, secure-by-default behavior and avoiding unnecessary weakening of platform security controls.
- **Code Complete, 2nd ed., Chapters 22–23**: developer testing before expensive system execution and root-cause-oriented debugging.
- **The Art of Unit Testing, 3rd ed.**: convert the intended build behavior into a trustworthy regression contract before modifying production behavior.
- **Systems Engineering: Principles and Practice, 3rd ed., Chapter 17**: retain explicit verification gates and do not advance Route A GenAI until Runtime has passed its own independently verified stage.

`windows-apps.pdf` remains the project's Windows application-development reference, but it is not used as the authority for OpenVINO source-build component selection or Windows App Control behavior; those decisions are grounded in the corresponding OpenVINO and Microsoft primary sources above.
